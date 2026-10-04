#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build a candidate feed, verify cross-family package consumption, and run the DispatchOnly scenario.
.DESCRIPTION
    Nightly and local runs use the same required checks. Logs, candidate hashes and separate build,
    restore and scenario outcomes remain beneath artifacts/package-composition/<run>/.
    Shared caches are never cleared. This does not replace the complete release test suite.
.PARAMETER SkipBuild
    Legacy compatibility argument. The candidate is rebuilt to establish build provenance.
.PARAMETER SkipSample
    Refused: an omitted required scenario cannot produce a passing composition verdict.
#>
[CmdletBinding()]
param(
    [string]$Version = '0.0.0-local',
    [switch]$SkipBuild,
    [switch]$SkipSample,
    [ValidateRange(1,600)][int]$SampleTimeoutSeconds = 120
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/package-composition.functions.ps1"
if ($SkipSample) { throw 'The DispatchOnly scenario is required; -SkipSample cannot certify composition.' }
if ($SkipBuild) { Write-Warning '-SkipBuild is retained for compatibility, but the candidate is rebuilt before certification.' }
$repo = Split-Path -Parent $PSScriptRoot
$runId = [guid]::NewGuid().ToString('N')
$run = Join-Path $repo ('artifacts/package-composition/' + $runId)
# NuGet's HTTP cache goes OUTSIDE the evidence tree. It is transport state, not evidence -- nothing
# reads it back -- and NuGet names its entries after the feed URL, which puts a COLON in the
# directory name. The nightly evidence upload globs artifacts/package-composition/**/*.json, which
# matches that colon-named DIRECTORY (it ends in .json), recurses into it, and GitHub then refuses
# the whole artifact because an artifact path may not contain a colon. Measured: the composition
# verdict itself PASSED -- 56 controls, production and scenario green -- and only the upload failed,
# discarding an hour of evidence at the last step.
$transport = Join-Path $repo ('artifacts/package-composition-transport/' + $runId)
$feed = Join-Path $run 'feed'
$cache = Join-Path $run 'consumer-cache'
New-Item -ItemType Directory -Path $run,$cache -Force | Out-Null
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$previous = @{}
foreach ($name in @('NUGET_PACKAGES','NUGET_HTTP_CACHE_PATH','NUGET_SCRATCH')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$verdict = [ordered]@{ status='incomplete'; candidateSha=''; version=$Version; production='incomplete'; packageBuilds=@(); scenario='incomplete'; evidenceDirectory=$run }
try {
    $env:NUGET_PACKAGES = Join-Path $run 'producer-cache'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $transport 'http-cache'
    $env:NUGET_SCRATCH = Join-Path $run 'scratch'
    & "$PSScriptRoot/pack-local.ps1" -Version $Version -OutputDirectory $feed -EvidenceDirectory (Join-Path $run 'production')
    $manifest = Get-Content (Join-Path $run 'production/candidate-packages.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($manifest.status -ne 'passed' -or -not $manifest.buildVerified) { throw 'Candidate production did not pass.' }
    $verdict.candidateSha = $manifest.candidateSha
    $verdict.sourceSha256 = $manifest.sourceSha256
    $verdict.production = 'passed'
    $packages = Get-CompositionPackages $feed $Version
    $shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    Invoke-PackageCommand $shell @('-NoProfile','-File',"$PSScriptRoot/smoke-test-packages.ps1",'-CandidateManifest',(Join-Path $run 'production/candidate-packages.json'),'-CandidateFeed',$feed,'-EvidenceDirectory',(Join-Path $run 'smoke')) (Join-Path $run 'smoke') $repo -TimeoutSeconds 3600
    $env:NUGET_PACKAGES = $cache
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    $config = Join-Path $run 'NuGet.Config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <fallbackPackageFolders><clear/></fallbackPackageFolders>
  <packageSources><clear/><add key="candidate" value="$escapedFeed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources>
  <packageSourceMapping><clear/><packageSource key="candidate"><package pattern="Excalibur*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8
    $properties = @('-p:UsePackageReferences=true',"-p:DispatchPackageVersion=$Version","-p:ExcaliburPackageVersion=$Version", "-p:MinVerVersionOverride=$Version",'-p:BuildExamplesAndTests=true','-p:Configuration=Release',"-p:RestoreConfigFile=$config","-p:RestorePackagesPath=$cache",'-p:RestoreForce=true','-p:RestoreNoCache=true')
    $lockProperties = @(Get-CompositionLockProperties -RunId (Split-Path $run -Leaf))
    $properties += $lockProperties
    $lockPath = $lockProperties[0].Substring('-p:NuGetLockFilePath='.Length)
    $failures = [Collections.Generic.List[string]]::new()
    # The producer evaluated every source project independently of the shipping filter.
    $projects = @($manifest.projects | Where-Object { $_.project.StartsWith('src/Excalibur/') })
    if ($projects.Count -eq 0) { throw 'No Excalibur projects selected for package composition.' }
    foreach ($project in $projects) {
        $path = Join-Path $repo $project.project
        $name = [IO.Path]::GetFileNameWithoutExtension($path)
        $outcome = [ordered]@{ project=$project.project; build='incomplete'; consumedPackages=@() }
        try {
            Write-Host "Validating package composition: $name"
            Invoke-PackageCommand $dotnet (@('build',$path,'-c','Release','--verbosity','minimal','--disable-build-servers') + $properties) (Join-Path $run "$name.build") $repo
            $outcome.build = 'passed'
            $evaluation = Join-Path $run "$name.assets-path"
            Invoke-PackageCommand $dotnet (@('msbuild',$path,'-nologo','-getProperty:ProjectAssetsFile') + $properties) $evaluation $repo
            $assets = (Get-Content "$evaluation.stdout.log" -Raw).Trim()
            $outcome.consumedPackages = Assert-CompositionAssets $assets $packages $cache -RequiredByTarget $project.dispatchDependencies
            Copy-Item -LiteralPath $assets -Destination (Join-Path $run "$name.project.assets.json")
            Copy-Item -LiteralPath (Join-Path (Split-Path $path) $lockPath) -Destination (Join-Path $run "$name.packages.lock.json")
        }
        catch { $failures.Add($_.Exception.Message); Write-Warning $_ }
        $verdict.packageBuilds += $outcome
    }
    if ($failures.Count -gt 0) { throw "$($failures.Count) required package-mode validation(s) failed. See $run" }

    $sample = Join-Path $repo 'samples/01-getting-started/DispatchOnly/Excalibur.DispatchOnly.csproj'
    Invoke-PackageCommand $dotnet (@('build',$sample,'-c','Release','--verbosity','minimal','--disable-build-servers') + $properties) (Join-Path $run 'sample.build') $repo
    $sampleAssets = Join-Path (Split-Path $sample) 'obj/project.assets.json'
    $null = Assert-CompositionAssets $sampleAssets $packages $cache -PackageOnly -RequiredPackages @('Excalibur.Dispatch','Excalibur.Dispatch.Abstractions')
    Copy-Item -LiteralPath $sampleAssets -Destination (Join-Path $run 'sample.project.assets.json')
    Copy-Item -LiteralPath (Join-Path (Split-Path $sample) $lockPath) -Destination (Join-Path $run 'sample.packages.lock.json')
    Invoke-PackageCommand $dotnet @((Join-Path (Split-Path $sample) 'bin/Release/net10.0/Excalibur.DispatchOnly.dll')) (Join-Path $run 'sample.scenario') $repo -TimeoutSeconds $SampleTimeoutSeconds
    $verdict.scenario = 'passed'
    Invoke-PackageCommand $shell @('-NoProfile','-File',"$PSScriptRoot/validate-package-composition.test.ps1",'-CandidateFeed',$feed,'-CandidateManifest',(Join-Path $run 'production/candidate-packages.json'),'-Version',$Version) (Join-Path $run 'scenario-controls') $repo -TimeoutSeconds 600

    # Detect candidate replacement during the run, even when id/version remain unchanged.
    $finalPackages = Get-CompositionPackages $feed $Version
    if ($finalPackages.Count -ne $manifest.packages.Count) { throw 'Candidate package population changed during validation.' }
    foreach ($package in $manifest.packages) {
        if (-not $finalPackages.ContainsKey($package.id) -or $finalPackages[$package.id].sha256 -cne $package.sha256) {
            throw "Candidate package changed during validation: $($package.id)"
        }
    }
    if ((Get-CompositionSourceIdentity $repo).sha256 -cne $manifest.sourceSha256) { throw 'Candidate source changed during composition validation.' }
    $verdict.status = 'passed'
    Write-Host "Package composition passed. DispatchOnly command, both event handlers and document scenario passed. Evidence: $run"
}
finally {
    $verdict | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'composition-verdict.json') -Encoding utf8
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name]) }
}
