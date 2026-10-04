# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
<#
.SYNOPSIS
  Discover a planned project/TFM roster, execute it, and compare each invocation separately.
.DESCRIPTION
  TestArguments comes from eng/build.ps1's single command composer. This wrapper preserves
  those options and changes only the source, explicit framework/settings and isolated results
  directory. All discovery completes before the first test executes. Failed runs retain their
  raw evidence and receipt. No assembly/source is dropped because it produced zero results.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$TestArguments,
    [Parameter(Mandatory)][string]$ContextPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$DotnetPath,
    [Parameter(Mandatory)][string]$SdkDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $root) { throw 'Evidence directory must be new for this invocation.' }
[void][IO.Directory]::CreateDirectory($root)
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in @('candidateSha','runId','runAttempt','job','shard','os','provider')) {
    if (-not $context.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$context[$key])) { throw "Missing external context '$key'." }
}
if ($TestArguments.Count -lt 2 -or $TestArguments[0] -cne 'test') { throw 'Expected composed dotnet test arguments.' }
$source = (Resolve-Path -LiteralPath $TestArguments[1]).Path
$configuration = 'Release'
$filter = ''
$filterSupplied = $false
$inlineSettings = @()
$baseArguments = [Collections.Generic.List[string]]::new()
for ($i = 2; $i -lt $TestArguments.Count; $i++) {
    $arg = $TestArguments[$i]
    if ($arg -eq '--') { $inlineSettings = @($TestArguments | Select-Object -Skip ($i + 1)); break }
    if ($arg -in @('--framework','-f','--settings','-s') -or $arg -match '^--(framework|settings)=') {
        throw "Explicit source restriction '$arg' is unsupported by the required-evidence composer."
    }
    if ($arg -eq '--results-directory') { $i++; continue }
    if ($arg -eq '--configuration') { $configuration = $TestArguments[$i + 1] }
    if ($arg -eq '--filter') { $filter = $TestArguments[$i + 1]; $filterSupplied = $true }
    $baseArguments.Add($arg)
}
if ($context.source -cne [IO.Path]::GetRelativePath($repo,$source).Replace('\','/') -or
    $context.sourceSha256 -ine (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -or
    $context.filter -cne $filter) { throw 'Composed test scope differs from the externally expected scope.' }
if ($source.EndsWith('.csproj', [StringComparison]::OrdinalIgnoreCase)) { $projects = @($source) }
elseif ($source.EndsWith('.slnf', [StringComparison]::OrdinalIgnoreCase)) {
    $slnf = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
    $solution = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($source)) ($slnf.solution.path -replace '\\','/')))
    $projects = @($slnf.solution.projects | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($solution)) ($_ -replace '\\','/')))
    })
} else { throw 'Required-test evidence currently accepts an explicit .csproj or .slnf roster.' }
if ($projects.Count -eq 0 -or @($projects | Sort-Object -Unique).Count -ne $projects.Count) { throw 'Empty or duplicated project roster.' }

function Read-ProjectProperties([string]$ProjectPath, [string]$Framework = '') {
    $arguments = @('msbuild',$ProjectPath,'-nologo',"-p:Configuration=$configuration",'-p:BuildExamplesAndTests=true',
        '-getProperty:IsTestProject,TargetFramework,TargetFrameworks,TargetPath,RunSettingsFilePath,AssemblyName')
    if ($Framework) { $arguments += "-p:TargetFramework=$Framework" }
    $output = & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Could not evaluate project: $ProjectPath" }
    ($output -join "`n" | ConvertFrom-Json).Properties
}
function Write-Json([string]$Path, $Value) {
    ConvertTo-Json -InputObject $Value -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}
function Reference([string]$Path) {
    @{ path = [IO.Path]::GetRelativePath($root, $Path).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
}
function Set-Setting([xml]$Xml, [string]$Name, [string]$Value) {
    $node = $Xml.DocumentElement
    foreach ($part in $Name.Split('.')) {
        if ($part -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "Unsupported setting path: $Name" }
        $children = @($node.SelectNodes($part))
        if ($children.Count -gt 1) { throw "Ambiguous setting path: $Name" }
        if ($children.Count -eq 0) { $node = $node.AppendChild($Xml.CreateElement($part)) } else { $node = $children[0] }
    }
    $node.InnerText = $Value
}
function Read-Inputs($Invocation) {
    $directory = [IO.Path]::GetDirectoryName($Invocation.assemblyPath)
    @{
        assemblySha256 = (Get-FileHash -LiteralPath $Invocation.assemblyPath -Algorithm SHA256).Hash
        adapterSha256 = (Get-FileHash -LiteralPath (Join-Path $directory 'xunit.runner.visualstudio.testadapter.dll') -Algorithm SHA256).Hash
        runSettingsSha256 = (Get-FileHash -LiteralPath $Invocation.settingsPath -Algorithm SHA256).Hash
        inputBundle = @(Get-ChildItem -LiteralPath $directory -File -Recurse | Sort-Object FullName | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath($directory,$_.FullName).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
    }
}

$invocations = [Collections.Generic.List[object]]::new()
$nonTestProjects = [Collections.Generic.List[object]]::new()
$evaluatedRoster = [Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $properties = Read-ProjectProperties $project
    $frameworks = if ($properties.TargetFrameworks) { @($properties.TargetFrameworks.Split(';')) } else { @($properties.TargetFramework) }
    $projectHadTests = $false
    $projectRelative = [IO.Path]::GetRelativePath($repo,$project).Replace('\','/')
    foreach ($framework in $frameworks) {
        if (-not $framework) { throw "No evaluated target framework for $project" }
        $properties = Read-ProjectProperties $project $framework
        $rosterEntry = @{
            project = $projectRelative; targetFramework = $framework
            evaluatedIsTestProject = $properties.IsTestProject
            targetPath = $properties.TargetPath; assembly = $properties.AssemblyName + '.dll'
            sourceRunSettings = $properties.RunSettingsFilePath
        }
        $evaluatedRoster.Add($rosterEntry)
        if ($properties.IsTestProject -notin @('true','false','')) { throw 'Unsupported IsTestProject evaluation.' }
        if ($properties.IsTestProject -cne 'true') { continue }
        $projectHadTests = $true
        if (-not $properties.RunSettingsFilePath) { throw "No effective runsettings for $project" }
        $id = 'invocation-{0:d4}' -f $invocations.Count
        $directory = Join-Path $root $id
        [void][IO.Directory]::CreateDirectory($directory)
        $settingsFile = Join-Path $directory 'effective.runsettings'
        $xmlReaderSettings = [Xml.XmlReaderSettings]::new()
        $xmlReaderSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $xmlReaderSettings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create($properties.RunSettingsFilePath, $xmlReaderSettings)
        try { $settings = [xml]::new(); $settings.XmlResolver = $null; $settings.Load($reader) } finally { $reader.Dispose() }
        if ($settings.DocumentElement.Name -cne 'RunSettings') { throw 'Expected RunSettings root.' }
        $inheritedFilter = $settings.SelectSingleNode('/RunSettings/RunConfiguration/TestCaseFilter')
        if (-not $filterSupplied -and $null -ne $inheritedFilter -and $inheritedFilter.InnerText) {
            throw 'Inherited runsettings filter must be supplied explicitly in the expected command scope.'
        }
        Set-Setting $settings 'RunConfiguration.TestCaseFilter' $filter
        $platformNode = $settings.SelectSingleNode('/RunSettings/RunConfiguration/TargetPlatform')
        if ($null -eq $platformNode) {
            Set-Setting $settings 'RunConfiguration.TargetPlatform' ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant())
        }
        foreach ($setting in $inlineSettings) {
            $parts = $setting.Split('=',2)
            if ($parts.Count -ne 2) { throw "Invalid composed RunSetting: $setting" }
            Set-Setting $settings $parts[0] $parts[1]
        }
        # A filter in both locations must agree; there is no second filter implementation here.
        if ($settings.SelectSingleNode('/RunSettings/RunConfiguration/TestCaseFilter').InnerText -cne $filter) {
            throw 'Inline TestCaseFilter contradicts the composed command filter.'
        }
        $settings.Save($settingsFile)
        $invocationContext = $context.Clone()
        $invocationContext.targetFramework = $framework
        $invocationContext.targetPlatform = $settings.SelectSingleNode('/RunSettings/RunConfiguration/TargetPlatform').InnerText
        $invocationContextFile = Join-Path $directory 'context.json'
        Write-Json $invocationContextFile $invocationContext
        $rosterEntry.targetPlatform = $invocationContext.targetPlatform
        $rosterEntry.effectiveSettings = Reference $settingsFile
        $invocation = @{
            id = $id; project = [IO.Path]::GetRelativePath($repo,$project).Replace('\','/')
            targetFramework = $framework; targetPlatform = $invocationContext.targetPlatform
            assembly = $properties.AssemblyName + '.dll'; assemblyPath = $properties.TargetPath
            settingsPath = $settingsFile; receipt = "$id/receipt.json"
        }
        $invocations.Add($invocation)
    }
    if (-not $projectHadTests) { $nonTestProjects.Add(@{ project = $projectRelative; evaluatedIsTestProject = $properties.IsTestProject }) }
}
# Seal the complete evaluated source/TFM denominator before adapter discovery begins.
$rosterPath = Join-Path $root 'evaluated-roster.json'
Write-Json $rosterPath @{ schemaVersion = 1; context = $context; entries = $evaluatedRoster.ToArray() }
$rosterReference = Reference $rosterPath
foreach ($invocation in $invocations) {
    $directory = Join-Path $root $invocation.id
    $contextFile = Join-Path $directory 'context.json'
    $censusPath = Join-Path $directory 'discovery.json'
    & (Join-Path $PSScriptRoot 'discover-required-tests.ps1') -AssemblyPath $invocation.assemblyPath `
        -EffectiveRunSettings $invocation.settingsPath -SdkDirectory $SdkDirectory -ContextPath $contextFile -OutputPath $censusPath
    $invocation.census = Reference $censusPath
    $census = Get-Content -LiteralPath $censusPath -Raw | ConvertFrom-Json
    if ($census.discoveredCount -eq 0) {
        [xml]$settings = Get-Content -LiteralPath $invocation.settingsPath -Raw
        Set-Setting $settings 'RunConfiguration.TestCaseFilter' ''
        $unfilteredSettings = Join-Path $directory 'unfiltered.runsettings'
        $settings.Save($unfilteredSettings)
        $unfilteredPath = Join-Path $directory 'unfiltered-discovery.json'
        & (Join-Path $PSScriptRoot 'discover-required-tests.ps1') -AssemblyPath $invocation.assemblyPath `
            -EffectiveRunSettings $unfilteredSettings -SdkDirectory $SdkDirectory -ContextPath $contextFile -OutputPath $unfilteredPath -SourcePresenceOnly
        $invocation.unfilteredCensus = Reference $unfilteredPath
        if ((Get-Content -LiteralPath $unfilteredPath -Raw | ConvertFrom-Json).discoveredCount -le 0) {
            throw "Test project produced no unfiltered identities: $($invocation.project)"
        }
    }
}
if ((Get-FileHash -LiteralPath $rosterPath -Algorithm SHA256).Hash -cne $rosterReference.sha256) { throw 'Evaluated roster changed during discovery.' }
$plan = @{ schemaVersion = 1; context = $context; evaluatedRoster = $rosterReference; invocations = $invocations.ToArray(); nonTestProjects = $nonTestProjects.ToArray() }
$planPath = Join-Path $root 'plan.json'
Write-Json $planPath $plan
$planHash = (Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedAssemblies = @($invocations | Where-Object {
    (Get-Content -LiteralPath (Join-Path $root $_.census.path) -Raw | ConvertFrom-Json).discoveredCount -gt 0
} | ForEach-Object { $_.assembly } | Sort-Object -Unique)
$expectedFile = Join-Path $root 'expected-assemblies.json'
Write-Json $expectedFile @($expectedAssemblies)
$failed = $false
foreach ($invocation in $invocations) {
    $census = Get-Content -LiteralPath (Join-Path $root $invocation.census.path) -Raw | ConvertFrom-Json
    $before = Read-Inputs $invocation
    foreach ($field in @('assemblySha256','adapterSha256','runSettingsSha256')) {
        if ($before[$field] -cne $census.$field) { throw "Input changed since discovery: $field" }
    }
    if (($before.inputBundle | ConvertTo-Json -Compress -Depth 5) -cne ($census.inputBundle | ConvertTo-Json -Compress -Depth 5)) {
        throw 'Input bundle changed since discovery.'
    }
    $resultDirectory = Join-Path $root "$($invocation.id)/results"
    $exitCode = 0
    $disposition = 'filtered-zero'
    if ($census.discoveredCount -gt 0) {
        $disposition = 'executed'
        $arguments = @('test',(Join-Path $repo $invocation.project)) + $baseArguments.ToArray() + @(
            '--framework',$invocation.targetFramework,'--settings',$invocation.settingsPath,'--results-directory',$resultDirectory)
        Write-Host "==> independently planned $($invocation.project) [$($invocation.targetFramework)]"
        & $DotnetPath @arguments
        $exitCode = $LASTEXITCODE
    }
    $after = Read-Inputs $invocation
    $receipt = @{
        schemaVersion = 1; planSha256 = $planHash; invocationId = $invocation.id
        censusSha256 = $invocation.census.sha256.ToLowerInvariant(); exitCode = $exitCode
        disposition = $disposition; before = $before; after = $after
        trx = @(Get-ChildItem -LiteralPath $resultDirectory -Filter '*.trx' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { Reference $_.FullName })
    }
    Write-Json (Join-Path $root $invocation.receipt) $receipt
    if ($exitCode -ne 0) { $failed = $true }
}
# Verify all invocations even when one failed, preserving the complete raw evidence first.
& python (Join-Path $PSScriptRoot 'required-test-evidence.py') --plan $planPath --expected-context $ContextPath --repo-root $repo
if ($LASTEXITCODE -ne 0 -or $failed) { throw 'Required test invocation evidence failed.' }
& (Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })) -NoProfile -File `
    (Join-Path $PSScriptRoot 'validate-shard-results.ps1') -TrxDir $root -ExpectedAssembliesFile $expectedFile
if ($LASTEXITCODE -ne 0) { throw 'Raw required test evidence failed.' }
