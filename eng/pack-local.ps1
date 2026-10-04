#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and pack the evaluated source package roster, retaining command logs and package hashes.
.PARAMETER NoBuild
    Pack existing outputs. The manifest records this as unverified build provenance.
.PARAMETER OutputDirectory
    Local feed beneath artifacts/. Defaults to artifacts/_packages for existing callers.
#>
[CmdletBinding()]
param(
    [string]$Version = '0.0.0-local',
    [switch]$NoBuild,
    [switch]$Clean,
    [switch]$LockedRestore,
    [switch]$ContinuousIntegrationBuild,
    [string]$OutputDirectory = '',
    [string]$EvidenceDirectory = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/package-composition.functions.ps1"
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $artifacts '_packages' }
$feed = [IO.Path]::GetFullPath($OutputDirectory)
$relativeFeed = [IO.Path]::GetRelativePath($artifacts,$feed)
if ($relativeFeed -eq '.' -or $relativeFeed -eq '..' -or $relativeFeed.StartsWith("..$([IO.Path]::DirectorySeparatorChar)") -or [IO.Path]::IsPathRooted($relativeFeed)) {
    throw 'The local feed must be a child directory of this repository artifacts directory.'
}
# A lexical child can still traverse a junction or symbolic link. Refuse those before deletion.
$ancestor = $feed
while ($ancestor -and $ancestor -ne [IO.Path]::GetPathRoot($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "The local feed traverses a linked directory: $ancestor"
    }
    $ancestor = Split-Path -Parent $ancestor
}
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path $artifacts ('pack-evidence/' + [guid]::NewGuid().ToString('N')) }
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($Clean -and (Test-Path -LiteralPath $feed)) {
    # Only the verified, explicitly owned feed is removed. Never clear shared NuGet caches.
    Remove-Item -LiteralPath $feed -Recurse -Force
}
New-Item -ItemType Directory -Path $feed,$evidence -Force | Out-Null
if (@(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg').Count -gt 0) { throw 'The feed must be empty. Use -Clean or a fresh -OutputDirectory.' }
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$common = @("-p:MinVerVersionOverride=$Version", '-p:BuildExamplesAndTests=true', '-p:Configuration=Release', '-p:UsePackageReferences=false')
if ($LockedRestore) { $common += '-p:RestoreLockedMode=true' }
if ($ContinuousIntegrationBuild) { $common += '-p:ContinuousIntegrationBuild=true' }
$filter = Join-Path $repo 'eng/ci/shards/ShippingOnly.slnf'
$roster = @()
$failures = [Collections.Generic.List[string]]::new()
$manifest = [ordered]@{ status='incomplete'; version=$Version; buildVerified=$false; lockedRestore=[bool]$LockedRestore; continuousIntegrationBuild=[bool]$ContinuousIntegrationBuild; candidateSha=''; projects=@(); packages=@() }
try {
    $head = & git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the candidate checkout.' }
    $manifest.candidateSha = "$head".Trim()
    $expectedSha = if ($env:COMPOSITION_EXPECTED_SHA) { $env:COMPOSITION_EXPECTED_SHA } else { $env:GITHUB_SHA }
    if ($expectedSha -and $expectedSha -cne $manifest.candidateSha) { throw 'Checkout differs from the expected workflow candidate.' }
    $sourceIdentity = Get-CompositionSourceIdentity $repo
    $sourceIdentity | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'source-inputs.json') -Encoding utf8
    $manifest.sourceSha256 = $sourceIdentity.sha256
    # Independent population: every source project, including providers, tools and metapackages.
    $projects = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter '*.csproj' -Recurse | Sort-Object FullName)
    if ($projects.Count -eq 0) { throw 'The source project roster is empty.' }
    $shipping = @((Get-Content -LiteralPath $filter -Raw | ConvertFrom-Json).solution.projects | ForEach-Object { $_.Replace('\','/') })
    foreach ($project in $projects) {
        $relative = [IO.Path]::GetRelativePath($repo,$project.FullName).Replace('\','/')
        $log = Join-Path $evidence ($project.BaseName + '.evaluate')
        Invoke-PackageCommand $dotnet (@('msbuild',$project.FullName,'-nologo','-getProperty:IsPackable,PackageId,PackageVersion') + $common) $log $repo
        $properties = (Get-Content -LiteralPath "$log.stdout.log" -Raw | ConvertFrom-Json).Properties
        $packable = $properties.IsPackable -ne 'false'
        if ($packable -and $relative -notin $shipping) { throw "Packable project omitted from ShippingOnly: $relative" }
        if ($packable -and [string]::IsNullOrWhiteSpace($properties.PackageId)) { throw "Missing evaluated package identity: $relative" }
        $roster += @{ project=$relative; id=$properties.PackageId; packable=$packable; projectSha256=(Get-FileHash $project.FullName).Hash }
    }
    $manifest.projects = $roster
    $expected = @($roster | Where-Object packable)
    if ($expected.Count -eq 0) { throw 'The evaluated package roster is empty.' }
    if (@($expected | Group-Object id | Where-Object Count -gt 1).Count -gt 0) { throw 'Duplicate evaluated package identities.' }
    if (-not $NoBuild) {
        Write-Host 'Building the shipping candidate with the requested package version.'
        Invoke-PackageCommand $dotnet (@('restore',$filter,'--verbosity','minimal') + $common) (Join-Path $evidence 'restore') $repo
        Invoke-PackageCommand $dotnet (@('build',$filter,'-c','Release','--no-restore','--verbosity','minimal','--disable-build-servers') + $common) (Join-Path $evidence 'build') $repo
        $manifest.buildVerified = $true
        # Capture source-mode runtime edges before package-mode restores overwrite obj files.
        foreach ($project in $roster) {
            $log = Join-Path $evidence ($project.id + '.source-assets-path')
            Invoke-PackageCommand $dotnet (@('msbuild',(Join-Path $repo $project.project),'-nologo','-getProperty:ProjectAssetsFile') + $common) $log $repo
            $assetsPath = (Get-Content "$log.stdout.log" -Raw).Trim()
            $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
            $project.dispatchDependencies = @{}
            $project.internalDependencies = @{}
            foreach ($target in $assets.targets.Keys) {
                $project.dispatchDependencies[$target] = @($assets.targets[$target].Keys | Where-Object { $_ -like 'Excalibur.Dispatch*/*' } | ForEach-Object { $_.Split('/')[0] } | Sort-Object -Unique)
                $project.internalDependencies[$target] = @($assets.targets[$target].Keys | Where-Object { $_ -like 'Excalibur*/*' } | ForEach-Object { $_.Split('/')[0] } | Sort-Object -Unique)
            }
            Copy-Item -LiteralPath $assetsPath -Destination (Join-Path $evidence ($project.id + '.source.assets.json'))
        }
    }
    foreach ($project in $expected) {
        Write-Host "Packing $($project.id)"
        try {
            Invoke-PackageCommand $dotnet (@('pack',(Join-Path $repo $project.project),'-o',$feed,'-c','Release','--no-build','--no-restore') + $common) (Join-Path $evidence ($project.id + '.pack')) $repo
        }
        catch { $failures.Add($_.Exception.Message); Write-Warning $_ }
    }
    if ($failures.Count -gt 0) { throw "$($failures.Count) required package command(s) failed. See $evidence" }
    $packages = Get-CompositionPackages $feed $Version
    $difference = @(Compare-Object @($expected.id | Sort-Object) @($packages.Keys | Sort-Object))
    if ($difference.Count -gt 0) { throw "Produced packages do not match the evaluated source roster: $($difference.InputObject -join ', ')" }
    $manifest.packages = @($packages.Values | Sort-Object id)
    if ((Get-CompositionSourceIdentity $repo).sha256 -cne $manifest.sourceSha256) { throw 'Candidate source changed during production.' }
    if ((& git -C $repo rev-parse HEAD).Trim() -cne $manifest.candidateSha) { throw 'Candidate commit changed during production.' }
    $manifest.status = 'passed'
    Write-Host "Local feed: $feed ($($packages.Count) packages). Evidence: $evidence"
}
finally {
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidence 'candidate-packages.json') -Encoding utf8
}
