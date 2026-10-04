# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Shared by the existing local producer and composition entry points.
function Get-CompositionLockProperties {
    param([Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{32}$')][string]$RunId)
    # NuGet resolves this relative to EACH restored project, including transitive project references.
    # Package mode has a new graph; it must not rewrite or enforce the source-mode checked-in lock.
    return @("-p:NuGetLockFilePath=obj/package-composition/$RunId/packages.lock.json", '-p:RestoreLockedMode=false', '-p:RestorePackagesWithLockFile=true')
}

function Get-CompositionSourceIdentity {
    param([Parameter(Mandatory)][string]$Repository)
    # Include inherited sample configuration and preserve Git's case-sensitive path semantics.
    # This records repository inputs, not a claim of a hermetic SDK/environment dependency graph.
    $selectors = @('src', 'eng', 'samples/01-getting-started/DispatchOnly', 'Excalibur.sln',
        'LICENSE', 'licenses', 'images/Dispatch/png/icon.png', 'images/Excalibur/png/icon.png')
    foreach ($ancestor in @('', 'samples/', 'samples/01-getting-started/')) {
        $selectors += @("${ancestor}global.json", "${ancestor}.editorconfig", "${ancestor}.globalconfig",
            "${ancestor}.gitattributes", "${ancestor}.gitignore", ":(glob)${ancestor}Directory.*", ":(icase)${ancestor}NuGet.config")
    }
    $paths = & git -C $Repository ls-files --cached --others --exclude-standard -- @selectors
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate candidate source inputs.' }
    $orderedPaths = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in $paths) { $null = $orderedPaths.Add($path) }
    $inputs = @($orderedPaths | ForEach-Object {
        $file = Join-Path $Repository $_
        @{ path=$_; sha256=$(if (Test-Path -LiteralPath $file -PathType Leaf) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash } else { 'deleted' }) }
    })
    if ($inputs.Count -eq 0) { throw 'Candidate source input set is empty.' }
    # Ordered rows make the fingerprint deterministic, independent of hashtable enumeration order.
    $text = ($inputs | ForEach-Object { "$($_.path)`t$($_.sha256)" }) -join "`n"
    return @{ inputs=$inputs; sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))) }
}

function Invoke-PackageCommand {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Log,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [ValidateRange(1,7200)][int]$TimeoutSeconds = 1800
    )
    New-Item -ItemType Directory -Path (Split-Path $Log) -Force | Out-Null
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = [Diagnostics.ProcessStartInfo]::new($Executable)
    $process.StartInfo.WorkingDirectory = $WorkingDirectory
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $process.StartInfo.ArgumentList.Add($argument) }
    $output = [IO.File]::Create("$Log.stdout.log")
    $errorOutput = [IO.File]::Create("$Log.stderr.log")
    $receipt = [ordered]@{ executable=$Executable; arguments=$Arguments; status='incomplete'; exitCode=$null; timedOut=$false }
    try {
        if (-not $process.Start()) { throw 'Process did not start.' }
        $outputTask = $process.StandardOutput.BaseStream.CopyToAsync($output)
        $errorTask = $process.StandardError.BaseStream.CopyToAsync($errorOutput)
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $receipt.timedOut = $true
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw "Timed-out process could not be stopped: $Log" }
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outputTask,$errorTask),5000)) {
            throw "Output streams did not close: $Log"
        }
        $receipt.exitCode = $process.ExitCode
        if ($receipt.timedOut) { throw "Unexpected timeout after ${TimeoutSeconds}s: $Log" }
        if ($process.ExitCode -ne 0) { throw "Command failed with exit $($process.ExitCode): $Log" }
        $receipt.status = 'passed'
    }
    finally {
        $output.Dispose()
        $errorOutput.Dispose()
        $process.Dispose()
        $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$Log.receipt.json" -Encoding utf8
    }
}

function Get-CompositionPackages {
    param([Parameter(Mandatory)][string]$Feed, [Parameter(Mandatory)][string]$Version)
    $packages = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Feed -Filter '*.nupkg') {
        $archive = [IO.Compression.ZipFile]::OpenRead($file.FullName)
        try {
            $specs = @($archive.Entries | Where-Object { $_.FullName -match '^[^/]+\.nuspec$' })
            if ($specs.Count -ne 1) { throw "Expected exactly one root nuspec: $($file.Name)" }
            $reader = [IO.StreamReader]::new($specs[0].Open())
            try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $metadata = $spec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
            $id = $metadata.SelectSingleNode('*[local-name()="id"]').InnerText
            $actualVersion = $metadata.SelectSingleNode('*[local-name()="version"]').InnerText
            if ($actualVersion -cne $Version) { throw "Wrong package version: $id/$actualVersion (expected $Version)" }
            if ($packages.ContainsKey($id)) { throw "Duplicate package identity: $id" }
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            $dependencies = @($metadata.SelectNodes('.//*[local-name()="dependency"]') | ForEach-Object {
                @{ id=$_.GetAttribute('id'); version=$_.GetAttribute('version') }
            })
            $packages[$id] = @{
                id=$id; version=$actualVersion; file=$file.Name
                sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                sha512=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($bytes))
                dependencies=$dependencies
            }
        }
        finally { $archive.Dispose() }
    }
    if ($packages.Count -eq 0) { throw 'No packages were produced.' }
    foreach ($package in $packages.Values) {
        foreach ($dependency in $package.dependencies) {
            if ($dependency.id -like 'Excalibur*') {
                if (-not $packages.ContainsKey($dependency.id)) { throw "Missing internal dependency $($dependency.id) required by $($package.id)" }
                # NuGet emits an unbounded minimum or a bracketed range. Both must start at this candidate.
                $range = $dependency.version.Replace(' ','')
                if ($range -cnotin @($Version,"[$Version]","[$Version,)","[$Version,$Version]")) {
                    throw "Wrong internal dependency version: $($package.id) -> $($dependency.id)/$($dependency.version)"
                }
            }
        }
    }
    return $packages
}

function Assert-CompositionAssets {
    param(
        [Parameter(Mandatory)][string]$AssetsPath,
        [Parameter(Mandatory)][hashtable]$Packages,
        [Parameter(Mandatory)][string]$Cache,
        [switch]$PackageOnly,
        [string[]]$RequiredPackages = @(),
        [hashtable]$RequiredByTarget = @{}
    )
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json -AsHashtable
    if (-not $assets.libraries -or -not $assets.targets) { throw "Empty restore graph: $AssetsPath" }
    $folders = @($assets.packageFolders.Keys)
    if ($folders.Count -ne 1 -or [IO.Path]::GetFullPath($folders[0]).TrimEnd('/','\') -ne [IO.Path]::GetFullPath($Cache).TrimEnd('/','\')) {
        throw "Restore graph does not use the isolated package cache: $AssetsPath"
    }
    if ($RequiredByTarget.Count -gt 0 -and @(Compare-Object @($RequiredByTarget.Keys | Sort-Object) @($assets.targets.Keys | Sort-Object)).Count -gt 0) {
        throw "Restore target population changed: $AssetsPath"
    }
    $seen = @()
    foreach ($key in $assets.libraries.Keys) {
        $library = $assets.libraries[$key]
        $id = $key.Split('/')[0]
        if ($library.type -eq 'project' -and ($PackageOnly -or $id -like 'Excalibur.Dispatch*')) {
            throw "Project reference bypasses package validation: $key in $AssetsPath"
        }
        if ($library.type -ne 'package' -or $id -notlike 'Excalibur*') { continue }
        if (-not $Packages.ContainsKey($id)) { throw "Consumed package is absent from the candidate feed: $key" }
        $expected = $Packages[$id]
        if ($key -cne "$($expected.id)/$($expected.version)" -or $library.sha512 -cne $expected.sha512) {
            throw "Consumed package identity/hash differs from candidate: $key"
        }
        if ($library.path -cne "$($id.ToLowerInvariant())/$($expected.version.ToLowerInvariant())") { throw "Unexpected restored package path: $key" }
        $cached = Join-Path $Cache "$($id.ToLowerInvariant())/$($expected.version.ToLowerInvariant())/$($id.ToLowerInvariant()).$($expected.version.ToLowerInvariant()).nupkg"
        if ((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -cne $expected.sha256) {
            throw "Cached package bytes differ from candidate: $key"
        }
        $seen += $id
    }
    foreach ($targetName in $assets.targets.Keys) {
        $target = $assets.targets[$targetName]
        if ($target.Count -eq 0) { throw "Empty restore target: $targetName" }
        $targetSeen = @()
        foreach ($key in $target.Keys) {
            if (-not $assets.libraries.ContainsKey($key) -or $target[$key].type -ne $assets.libraries[$key].type) { throw "Restore target/library mismatch: $key" }
            if ($target[$key].type -eq 'package') { $targetSeen += $key.Split('/')[0] }
        }
        $required = @($RequiredPackages)
        if ($RequiredByTarget.ContainsKey($targetName)) { $required += @($RequiredByTarget[$targetName]) }
        foreach ($id in $required) {
            if ($id -notin $seen -or $id -notin $targetSeen) { throw "Required package was not consumed in ${targetName}: $id" }
        }
    }
    return ,$seen
}
