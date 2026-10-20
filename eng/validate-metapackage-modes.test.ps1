#!/usr/bin/env pwsh
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Regression for ARB P06: full metapackages lost all internal dependencies in package mode.
[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/package-composition.functions.ps1"
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modeEvidence = Join-Path $RepositoryRoot ("artifacts/package-mode-comparison/" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $modeEvidence -Force | Out-Null
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$modeCache = @{}
$modeSequence = 0
$projectIds = @{}

function Get-ItemValue($Item, [string]$Name) {
    $property = $Item.PSObject.Properties[$Name]
    if ($property) { return [string]$property.Value }
    return ''
}

function Get-AssetRole($Item, [string]$Name, [string]$Default) {
    $value = Get-ItemValue $Item $Name
    if (-not $value) { $value = $Default }
    return (($value.Split(';') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Sort-Object -Unique) -join ';')
}

function Get-ReferenceRole($Item) {
    $output = Get-ItemValue $Item 'ReferenceOutputAssembly'
    if (-not $output) { $output = 'true' }
    # NuGet defaults apply to package and project dependency assets. PrivateAssets controls
    # propagation; it never exempts an assembly from the dependency-preservation comparison.
    # https://learn.microsoft.com/en-us/nuget/create-packages/select-assemblies-referenced-by-projects
    return @($output.ToLowerInvariant(), (Get-ItemValue $Item 'OutputItemType'),
        (Get-AssetRole $Item 'PrivateAssets' 'contentfiles;analyzers;build'),
        (Get-AssetRole $Item 'IncludeAssets' 'all'), (Get-AssetRole $Item 'ExcludeAssets' 'none')) -join '|'
}

function Get-EffectivePackageVersion($Item, $Evaluation) {
    foreach ($field in @('VersionOverride', 'Version')) {
        $value = Get-ItemValue $Item $field
        if ($value) { return $value }
    }
    $central = @($Evaluation.Items.PackageVersion | Where-Object Identity -ceq $Item.Identity)
    if ($central.Count -ne 1) { throw "Missing/ambiguous evaluated package version: $($Item.Identity)" }
    $value = Get-ItemValue $central[0] 'Version'
    if (-not $value) { throw "Empty evaluated package version: $($Item.Identity)" }
    return $value
}

function Get-ModeEvaluation([string]$Project, [string]$Configuration, [string]$Mode = '',
    [string]$Framework = '', [string]$PropsOverride = '', [switch]$Fresh) {
    $key = "$Project|$Configuration|$Mode|$Framework|$PropsOverride|$((Get-FileHash -LiteralPath $Project).Hash)"
    if (-not $Fresh -and $modeCache.ContainsKey($key)) { return $modeCache[$key] }
    $script:modeSequence++
    $log = Join-Path $modeEvidence ("evaluation-{0:D5}" -f $script:modeSequence)
    $arguments = @('msbuild', $Project, '-nologo', '-getItem:ProjectReference,PackageReference,PackageVersion',
        '-getProperty:PackageId,IsPackable,TargetFramework,TargetFrameworks,DirectoryBuildPropsPath,DispatchPackageVersion,ExcaliburPackageVersion',
        "-p:Configuration=$Configuration", '-p:DispatchPackageVersion=10.0.0-arb-p06-dpv', '-p:ExcaliburPackageVersion=10.0.0-arb-p06-excv')
    if ($Mode) { $arguments += "-p:UsePackageReferences=$Mode" }
    if ($Framework) { $arguments += "-p:TargetFramework=$Framework" }
    if ($PropsOverride) { $arguments += "-p:DirectoryBuildPropsPath=$PropsOverride" }
    Invoke-PackageCommand $dotnet $arguments $log $RepositoryRoot -TimeoutSeconds 90
    $result = Get-Content -LiteralPath "$log.stdout.log" -Raw | ConvertFrom-Json
    $modeCache[$key] = $result
    return $result
}

function Get-DeclaredRoutes([string]$Project) {
    [xml]$document = Get-Content -LiteralPath $Project -Raw
    $routes = @{}
    foreach ($item in $document.SelectNodes('//*[local-name()="PackageReference"]')) {
        $id = $item.GetAttribute('Include')
        if (-not $id) { continue }
        $expression = $item.GetAttribute('VersionOverride')
        if (-not $expression) {
            $child = $item.SelectSingleNode('*[local-name()="VersionOverride"]')
            if ($child) { $expression = $child.InnerText }
        }
        if (-not $expression) { $expression = $item.GetAttribute('Version') }
        if ($expression -match '^\$\((DispatchPackageVersion|ExcaliburPackageVersion)\)$') {
            if ($routes.ContainsKey($id) -and $routes[$id] -cne $Matches[1]) { throw "Ambiguous declared version route: $Project/$id" }
            $routes[$id] = $Matches[1]
        }
    }
    return $routes
}

function Compare-ModeContext($Default, $Source, $Package, $Routes, [string]$Configuration, [string]$Context) {
    foreach ($kind in @('ProjectReference', 'PackageReference', 'PackageVersion')) {
        $a = @($Default.Items.$kind | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 8 })
        $b = @($Source.Items.$kind | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 8 })
        Assert-SameSet $a $b "$Context default/false $kind"
    }
    $matchedProjects = @{}
    $matchedPackages = @{}
    foreach ($reference in @($Source.Items.ProjectReference)) {
        $same = @($Package.Items.ProjectReference | Where-Object FullPath -ceq $reference.FullPath)
        if ($same.Count) {
            $role = Get-ReferenceRole $reference
            if (@($same | Where-Object { (Get-ReferenceRole $_) -ceq $role }).Count -eq 0) { throw "Project dependency role changed: $Context/$($reference.FullPath)" }
            $matchedProjects["$($reference.FullPath)|$role"] = $true
            continue
        }
        $identityKey = "$($reference.FullPath)|$Configuration"
        if (-not $projectIds.ContainsKey($identityKey)) {
            $target = Get-ModeEvaluation $reference.FullPath $Configuration 'false'
            if (-not $target.Properties.PackageId) { throw "Unresolved project package identity: $Context/$($reference.FullPath)" }
            # A cached identity is safe only after checking all of this target's declared frameworks.
            foreach ($targetFramework in (Get-FrameworkSet @($target))) {
                $identityEvaluation = if ($target.Properties.TargetFramework -ceq $targetFramework -and -not $target.Properties.TargetFrameworks) {
                    $target
                } else { Get-ModeEvaluation $reference.FullPath $Configuration 'false' $targetFramework }
                if ($identityEvaluation.Properties.PackageId -cne $target.Properties.PackageId) {
                    throw "Framework-dependent project package identity needs explicit mapping: $Context/$($reference.FullPath)"
                }
            }
            $projectIds[$identityKey] = $target.Properties.PackageId
        }
        $id = $projectIds[$identityKey]
        $converted = @($Package.Items.PackageReference | Where-Object Identity -ceq $id)
        if ($converted.Count -ne 1) { throw "Missing/ambiguous converted dependency: $Context/$id" }
        if ((Get-ReferenceRole $converted[0]) -cne (Get-ReferenceRole $reference)) {
            throw "Converted dependency role needs explicit ownership/rationale: $Context/$id"
        }
        $route = $Routes[$id]
        if ($route -notin @('DispatchPackageVersion', 'ExcaliburPackageVersion')) { throw "Unsupported/undeclared version route: $Context/$id" }
        $expected = Get-ItemValue $Package.Properties $route
        if (-not $expected -or (Get-EffectivePackageVersion $converted[0] $Package) -cne $expected) { throw "Converted dependency changed declared pinned route: $Context/$id" }
        $matchedPackages[$id] = $true
    }
    foreach ($reference in @($Source.Items.PackageReference)) {
        $same = @($Package.Items.PackageReference | Where-Object Identity -ceq $reference.Identity)
        if ($same.Count -ne 1 -or (Get-ReferenceRole $same[0]) -cne (Get-ReferenceRole $reference) -or
            (Get-EffectivePackageVersion $same[0] $Package) -cne (Get-EffectivePackageVersion $reference $Source)) {
            throw "Existing package dependency changed: $Context/$($reference.Identity)"
        }
        $matchedPackages[$reference.Identity] = $true
    }
    # Additions are retained for review rather than banned by a universal conversion rule.
    return @($Package.Items.PackageReference | Where-Object { -not $matchedPackages.ContainsKey($_.Identity) } |
        ForEach-Object { [ordered]@{ kind='PackageReference'; identity=$_.Identity; role=(Get-ReferenceRole $_); version=(Get-EffectivePackageVersion $_ $Package) } }) +
        @($Package.Items.ProjectReference | Where-Object { -not $matchedProjects.ContainsKey("$($_.FullPath)|$(Get-ReferenceRole $_)") } |
        ForEach-Object { [ordered]@{ kind='ProjectReference'; identity=$_.FullPath; role=(Get-ReferenceRole $_) } })
}

function Get-FrameworkSet($Evaluations) {
    $frameworks = @($Evaluations | ForEach-Object {
        @((Get-ItemValue $_.Properties 'TargetFramework'), (Get-ItemValue $_.Properties 'TargetFrameworks')) |
            ForEach-Object { $_.Split(';') } | Where-Object { $_ }
    } | Sort-Object -Unique)
    if ($frameworks.Count -eq 0) { throw 'Evaluated framework population is empty.' }
    return $frameworks
}

function Get-Evaluation([string]$Project, [string]$Configuration, [string]$Mode = '', [string]$Version = '') {
    $arguments = @('msbuild', $Project, '-nologo', '-getItem:ProjectReference,PackageReference',
        '-getProperty:PackageId,DispatchPackageVersion', "-p:Configuration=$Configuration", '-p:ExcaliburPackageVersion=9.9.9-poison')
    if ($Mode) { $arguments += "-p:UsePackageReferences=$Mode" }
    if ($Version) { $arguments += "-p:DispatchPackageVersion=$Version" }
    $output = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "MSBuild evaluation failed: $Project ($Configuration/$Mode)" }
    return ($output -join "`n" | ConvertFrom-Json)
}

function Assert-SameSet([string[]]$Actual, [string[]]$Expected, [string]$Context) {
    if (($Actual | Sort-Object | ConvertTo-Json -Compress) -cne ($Expected | Sort-Object | ConvertTo-Json -Compress)) {
        throw "$Context changed: expected [$($Expected -join ', ')], actual [$($Actual -join ', ')]"
    }
}

function Get-DependencySignature($Dependency) {
    $version = $Dependency.PSObject.Properties['Version']
    $override = $Dependency.PSObject.Properties['VersionOverride']
    return "$($Dependency.Identity)|$(if ($version) { $version.Value })|$(if ($override) { $override.Value })"
}
$previousMode = $env:UsePackageReferences
$previousVersion = $env:DispatchPackageVersion
$modeVerdict = [ordered]@{ status='incomplete'; inventory=@(); plannedContexts=@(); comparisons=@(); controls=@(); scope='Source dependencies preserved in current projects with own/ancestor UsePackageReferences item conditions; imported items evaluated in each declared context. Mode additions reported for separate review; not complete set equivalence or approval of additions.' }
try {
    $env:UsePackageReferences = $null
    $env:DispatchPackageVersion = $null
    $beforeIdentity = Get-CompositionSourceIdentity $RepositoryRoot
    $beforeHead = (& git -C $RepositoryRoot rev-parse HEAD) -join ''
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record mode comparison HEAD.' }
    $modeVerdict.sourceSha256 = $beforeIdentity.sha256
    $modeVerdict.candidateSha = $beforeHead
    $checks = 0
    foreach ($provider in @('SqlServer', 'Postgres')) {
        $project = Join-Path $RepositoryRoot "src/metapackages/Excalibur.$provider/Excalibur.$provider.csproj"
        foreach ($configuration in @('Debug', 'Release')) {
            $source = Get-Evaluation $project $configuration
            $references = @($source.Items.ProjectReference)
            if ($references.Count -ne 7) { throw "$provider source mode must retain seven ProjectReferences." }
            $expected = @($references | ForEach-Object {
                $target = Get-Evaluation $_.FullPath $configuration
                $target.Properties.PackageId
            })
            if (@($expected | Sort-Object -Unique).Count -ne 7) { throw "$provider source PackageIds must be seven distinct identities." }
            $external = @($source.Items.PackageReference | ForEach-Object { (Get-DependencySignature $_) })
            foreach ($requiredExternal in @('Microsoft.Extensions.Configuration.Binder||', 'Microsoft.Extensions.DependencyInjection.Abstractions||')) { if ($external -notcontains $requiredExternal) { throw "$provider required external dependency changed: $requiredExternal" } }
            $explicitSource = Get-Evaluation $project $configuration 'false'
            Assert-SameSet @($explicitSource.Items.ProjectReference.FullPath) @($references.FullPath) "$provider false/default source parity"
            Assert-SameSet @($explicitSource.Items.PackageReference | ForEach-Object { (Get-DependencySignature $_) }) $external "$provider false/default external parity"
            foreach ($version in @('', '1.2.3-arb-p06')) {
                $package = Get-Evaluation $project $configuration 'true' $version
                if (@($package.Items.ProjectReference).Count) { throw "$provider package mode retains ProjectReferences." }
                $internal = @($package.Items.PackageReference | Where-Object Identity -like 'Excalibur.*')
                Assert-SameSet @($internal | ForEach-Object Identity) $expected "$provider $configuration package/source dependency parity"
                $expectedVersion = if ($version) { $version } else { '0.0.0-local' }
                if ($package.Properties.DispatchPackageVersion -ne $expectedVersion) { throw "$provider DispatchPackageVersion fallback/override changed." }
                foreach ($dependency in $internal) {
                    if ($dependency.VersionOverride -ne $expectedVersion) { throw "$provider $($dependency.Identity) must use DispatchPackageVersion=$expectedVersion." }
                }
                Assert-SameSet @($package.Items.PackageReference | Where-Object Identity -notlike 'Excalibur.*' | ForEach-Object { (Get-DependencySignature $_) }) $external "$provider package/source external parity"
                $checks++
            }
        }
    }
    Write-Host "$checks metapackage mode checks passed."

    $paths = @(& git -C $RepositoryRoot ls-files --cached --others --exclude-standard -- src)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate current source projects.' }
    foreach ($relative in ($paths | Where-Object { $_ -like '*.csproj' } | Sort-Object -Unique)) {
        $path = Join-Path $RepositoryRoot $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing inventoried source project: $relative" }
        [xml]$document = Get-Content -LiteralPath $path -Raw
        $conditions = @(foreach ($item in $document.SelectNodes('//*[local-name()="ProjectReference" or local-name()="PackageReference"]')) {
            $values = @()
            $ancestor = $item
            while ($ancestor -is [Xml.XmlElement]) {
                if ($ancestor.HasAttribute('Condition')) { $values += $ancestor.GetAttribute('Condition') }
                $ancestor = $ancestor.ParentNode
            }
            if (($values -join ' ') -match 'UsePackageReferences') {
                [ordered]@{ kind=$item.LocalName; include=$item.GetAttribute('Include'); conditions=$values }
            }
        })
        if ($conditions.Count) {
            $modeVerdict.inventory += [ordered]@{ project=$relative; sha256=(Get-FileHash -LiteralPath $path).Hash; conditions=$conditions }
        }
    }
    if ($modeVerdict.inventory.Count -eq 0) { throw 'Mode-conditioned source project population is empty.' }
    $modeVerdict.inventory | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $modeEvidence 'inventory.json') -Encoding utf8
    foreach ($entry in $modeVerdict.inventory) {
        $path = Join-Path $RepositoryRoot $entry.project
        $routes = Get-DeclaredRoutes $path
        foreach ($configuration in @('Debug', 'Release')) {
            $outer = @(Get-ModeEvaluation $path $configuration; Get-ModeEvaluation $path $configuration 'false'; Get-ModeEvaluation $path $configuration 'true')
            $declared = @($outer | ForEach-Object { ,@(Get-FrameworkSet @($_)) })
            Assert-SameSet $declared[0] $declared[1] "$($entry.project)/$configuration default/false declared frameworks"
            Assert-SameSet $declared[0] $declared[2] "$($entry.project)/$configuration source/package declared frameworks need explicit support review"
            foreach ($framework in $declared[0]) {
                $context = "$($entry.project)/$configuration/$framework"
                $modeVerdict.plannedContexts += $context
                # Single-target outer evaluations already represent their actual declared framework.
                # Multi-target outer evaluations require native inner framework contexts.
                $views = @(for ($index = 0; $index -lt 3; $index++) {
                    if ($outer[$index].Properties.TargetFramework -ceq $framework -and -not $outer[$index].Properties.TargetFrameworks) { $outer[$index] }
                    else {
                        $mode = @('', 'false', 'true')[$index]
                        Get-ModeEvaluation $path $configuration $mode $framework
                    }
                })
                $default, $source, $package = $views
                $additions = @(Compare-ModeContext $default $source $package $routes $configuration $context)
                $modeVerdict.comparisons += [ordered]@{ context=$context; status='passed'; additions=$additions }
                $modeVerdict | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $modeEvidence 'mode-verdict.json') -Encoding utf8
            }
        }
    }

    # A real copied project supplies the same observer's healthy, lost-edge and restored controls.
    # Its references remain rooted in the original source tree; production project bytes are untouched.
    $original = Join-Path $RepositoryRoot 'src/metapackages/Excalibur.SqlServer/Excalibur.SqlServer.csproj'
    $originalHash = (Get-FileHash -LiteralPath $original).Hash
    $originalEvaluation = Get-ModeEvaluation $original 'Release' 'false'
    $props = $originalEvaluation.Properties.DirectoryBuildPropsPath
    if (-not $props -or -not (Test-Path -LiteralPath $props -PathType Leaf)) { throw 'Cannot preserve the native cloned-project import context.' }
    $cloneDirectory = Join-Path $modeEvidence 'native-project-control'
    New-Item -ItemType Directory -Path $cloneDirectory -Force | Out-Null
    $clone = Join-Path $cloneDirectory 'Excalibur.SqlServer.csproj'
    [xml]$cloneDocument = Get-Content -LiteralPath $original -Raw
    foreach ($item in $cloneDocument.SelectNodes('//*[local-name()="ProjectReference"]')) {
        $include = $item.GetAttribute('Include')
        if (-not $include -or $include.Contains('$') -or $include.Contains(';')) { throw 'Unsupported cloned-project reference shape.' }
        $item.SetAttribute('Include', [IO.Path]::GetFullPath($include, (Split-Path $original)))
    }
    $cloneDocument.Save($clone)
    $cloneBytes = [IO.File]::ReadAllBytes($clone)
    $cloneFramework = @(Get-FrameworkSet @($originalEvaluation))
    if ($cloneFramework.Count -ne 1) { throw 'Native metapackage mutation control requires its current single framework.' }
    function Invoke-ClonedComparison([string]$Context) {
        $default = Get-ModeEvaluation $clone 'Release' '' $cloneFramework[0] $props -Fresh
        $source = Get-ModeEvaluation $clone 'Release' 'false' $cloneFramework[0] $props -Fresh
        $package = Get-ModeEvaluation $clone 'Release' 'true' $cloneFramework[0] $props -Fresh
        $null = Compare-ModeContext $default $source $package (Get-DeclaredRoutes $clone) 'Release' $Context
    }
    Invoke-ClonedComparison 'clone-healthy'
    $modeVerdict.controls += [ordered]@{ identity='clone-healthy'; status='passed' }
    try {
        [xml]$mutation = Get-Content -LiteralPath $clone -Raw
        $removed = @($mutation.SelectNodes('//*[local-name()="PackageReference" and @Include="Excalibur.Dispatch.SqlServer"]'))
        if ($removed.Count -ne 1) { throw 'Expected exactly one native conditional package edge to remove.' }
        $null = $removed[0].ParentNode.RemoveChild($removed[0])
        $mutation.Save($clone)
        $refused = $false
        try { Invoke-ClonedComparison 'clone-missing-edge' }
        catch {
            if ($_.Exception.Message -cne 'Missing/ambiguous converted dependency: clone-missing-edge/Excalibur.Dispatch.SqlServer') { throw }
            $refused = $true
            $modeVerdict.controls += [ordered]@{ identity='clone-missing-edge'; status='refused'; diagnosis=$_.Exception.Message }
        }
        if (-not $refused) { throw 'Native conditional dependency deletion was accepted.' }
    }
    finally { [IO.File]::WriteAllBytes($clone, $cloneBytes) }
    Invoke-ClonedComparison 'clone-restored'
    $modeVerdict.controls += [ordered]@{ identity='clone-restored'; status='passed' }
    if ((Get-FileHash -LiteralPath $original).Hash -cne $originalHash) { throw 'Production project changed during its native control.' }
    $afterIdentity = Get-CompositionSourceIdentity $RepositoryRoot
    $afterHead = (& git -C $RepositoryRoot rev-parse HEAD) -join ''
    if ($LASTEXITCODE -ne 0 -or $afterHead -cne $beforeHead -or $afterIdentity.sha256 -cne $beforeIdentity.sha256) {
        throw 'Source/HEAD changed during dependency mode comparison.'
    }
    if ($modeVerdict.plannedContexts.Count -lt ($modeVerdict.inventory.Count * 2) -or $modeVerdict.controls.Count -ne 3) { throw 'Dependency mode comparison did not finish its required population.' }
    Assert-SameSet @($modeVerdict.comparisons | ForEach-Object context) $modeVerdict.plannedContexts 'Exact planned dependency contexts'
    if (@($modeVerdict.plannedContexts | Sort-Object -Unique).Count -ne $modeVerdict.plannedContexts.Count) { throw 'Planned dependency context identities are duplicated.' }
    $modeVerdict.status = 'passed'
    Write-Host "$($modeVerdict.comparisons.Count) evaluated dependency preservation contexts passed across $($modeVerdict.inventory.Count) current projects; mode additions retained for review; three native mutation controls passed. Evidence: $modeEvidence"

}
finally {
    $modeVerdict | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $modeEvidence 'mode-verdict.json') -Encoding utf8
    $env:UsePackageReferences = $previousMode
    $env:DispatchPackageVersion = $previousVersion
}
