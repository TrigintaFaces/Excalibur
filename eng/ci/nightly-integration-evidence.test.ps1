# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DotnetPath,
    [string]$PackageCache = $env:NUGET_PACKAGES
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Join-Path $repo ('artifacts/tools/nightly-evidence-control-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$dotnet = (Resolve-Path -LiteralPath $DotnetPath).Path
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
if (-not $PackageCache) {
    # The user-profile default is WRONG for this repository: NuGet.config pins globalPackagesFolder to a
    # repo-relative path, so every package the shard build restored sits under the repo and ~/.nuget/packages
    # is empty. Assuming the default made the offline fixture restore fail NU1101 on all three packages with
    # the cache reported as containing none of them. Read the pin instead of guessing; NUGET_PACKAGES, which
    # outranks the pin in NuGet's own precedence, still wins because it is this parameter's default.
    # None of that GUARANTEES the result is the folder the build populated -- on a hosted Linux runner
    # it was neither the pin nor a populated cache, and the restore failed NU1101 again. See the source
    # list below for why that is no longer fatal: this block is a fast offline-first guess now, not a
    # correctness guarantee, so do not read the confident wording above as one.
    $pinned = $null
    $configPath = Join-Path $repo 'NuGet.config'
    if (Test-Path -LiteralPath $configPath) {
        $node = ([xml](Get-Content -LiteralPath $configPath -Raw)).SelectSingleNode(
            "/configuration/config/add[@key='globalPackagesFolder']")
        if ($node) { $pinned = $node.GetAttribute('value') }
    }
    $PackageCache = if ($pinned) {
        # The pinned value is authored with a Windows separator, which stays literal on Linux.
        [IO.Path]::GetFullPath((Join-Path $repo ($pinned -replace '\\', '/')))
    } else {
        Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
    }
}
# The fixture needs its own NuGet config. The repository config enables PackageSourceMapping, and when
# that is on NuGet silently DISCARDS any source no mapping covers -- so passing the cache with --source
# while also passing the repo --configfile resolved nothing and the restore failed NU1100 on all three
# fixture packages, with the cache named in the message as 'not considered'. A throwaway config with
# <clear /> and unmapped sources is the same isolation this script already applies to
# Directory.Build.props and Directory.Build.targets. Central package management still resolves versions
# from the repository's Directory.Packages.props.
#
# nuget.org is listed BESIDE the cache rather than the cache alone, and that is the load-bearing part:
# an offline-only restore makes this self-test fail whenever the resolved cache is not the folder the
# build actually populated, and nothing about that failure resembles the control it is meant to prove.
# That divergence is reachable three ways -- NUGET_PACKAGES outranks the repository's
# globalPackagesFolder pin in NuGet's own precedence, the pin is authored with a Windows separator that
# stays literal on Linux, and a cache key can restore an empty folder -- and it was live on the hosted
# runner, where this restore failed NU1101 on every package while the shard's own build had restored
# fine seconds earlier. The cache stays first so a populated one still serves the restore offline;
# nuget.org makes a wrong or empty cache cost latency instead of a false RED. Every other step in this
# job already restores from nuget.org, so this adds no dependency the job did not already have.
$fixtureNuGetConfig = Join-Path $root 'NuGet.Config'
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fixture-cache" value="$PackageCache" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $fixtureNuGetConfig
'<Project><PropertyGroup><RunSettingsFilePath>$(MSBuildThisFileDirectory)fixture.runsettings</RunSettingsFilePath></PropertyGroup></Project>' | Set-Content (Join-Path $root 'Directory.Build.props')
'<RunSettings><xUnit><PreEnumerateTheories>true</PreEnumerateTheories></xUnit></RunSettings>' | Set-Content (Join-Path $root 'fixture.runsettings')
'<Project><PropertyGroup><IsTestingPlatformApplication>false</IsTestingPlatformApplication></PropertyGroup></Project>' | Set-Content (Join-Path $root 'Directory.Build.targets')
$projects = @()
foreach ($name in @('First','Second')) {
    $directory = Join-Path $root $name
    [void][IO.Directory]::CreateDirectory($directory)
    $project = Join-Path $directory "$name.csproj"
    $projects += "$name/$name.csproj"
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <NuGetAudit>false</NuGetAudit><RunAnalyzers>false</RunAnalyzers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
'@ | Set-Content $project
    @'
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
public sealed class Cleanup : IAsyncLifetime
{
    public static string Fault => Assembly.GetExecutingAssembly().GetName().Name == "Second"
        ? Environment.GetEnvironmentVariable("CI03_FAULT") : null;
    public ValueTask InitializeAsync() => default;
    public ValueTask DisposeAsync()
    {
        if (Fault == "cleanup-crash") Environment.Exit(37);
        return default;
    }
}
public sealed class Cases : IClassFixture<Cleanup>
{
    [Fact, Trait("Category","Integration")]
    public void Required()
    {
        if (Cleanup.Fault == "skip") Assert.Skip("intentional negative control");
        // VSTest's five-second timeout is the assertion. Bound the deliberate faulty host too:
        // its inherited output pipe can otherwise survive the aborted runner indefinitely.
        if (Cleanup.Fault == "hang") { Thread.Sleep(TimeSpan.FromSeconds(15)); Environment.Exit(38); }
        Assert.True(true);
    }
}
'@ | Set-Content (Join-Path $directory 'Cases.cs')
    & $dotnet restore $project --configfile $fixtureNuGetConfig --packages $PackageCache -p:NuGetAudit=false *> (Join-Path $directory 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw "Fixture restore failed: $directory" }
    & $dotnet build $project -c Release --no-restore -warnaserror *> (Join-Path $directory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Fixture build failed: $directory" }
}
$source = Join-Path $root 'fixture.slnf'
@{solution=@{path='fixture.sln';projects=$projects}} | ConvertTo-Json -Depth 4 | Set-Content $source
# The evidence wrapper evaluates every explicit project; the no-build fixture does not require solution compilation.
'Microsoft Visual Studio Solution File, Format Version 12.00' | Set-Content (Join-Path $root 'fixture.sln')
$saved = @{}
foreach ($name in @('PATH','GITHUB_SHA','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','GITHUB_JOB','RUNNER_OS','CI03_FAULT')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
$count = 0
function Verify-Control([string]$Name, [string]$Directory, [bool]$Pass, [string]$Outcome = 'success') {
    & $shell -NoProfile -File (Join-Path $PSScriptRoot 'nightly-integration-evidence.ps1') -Mode Verify `
        -Source $source -Shard 'fixture' -ResultsDirectory $Directory -RunnerOutcome $Outcome *> (Join-Path $root "$Name.verify.log")
    if (($LASTEXITCODE -eq 0) -ne $Pass) { throw "${Name}: wrong verifier outcome; see $root/$Name.verify.log" }
    $script:count++
    Write-Host "PASS: $Name"
}
try {
    $env:PATH = [IO.Path]::GetDirectoryName($dotnet) + [IO.Path]::PathSeparator + $env:PATH
    $env:GITHUB_SHA = (& git -C $repo rev-parse HEAD)
    $env:GITHUB_RUN_ID = 'local-ci03'; $env:GITHUB_RUN_ATTEMPT = '1'; $env:GITHUB_JOB = 'integration-tests'
    $env:RUNNER_OS = if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'macOS' }
    foreach ($fault in @('pass','cleanup-crash','skip')) {
        $env:CI03_FAULT = $fault
        $directory = Join-Path $root $fault
        & $shell -NoProfile -File (Join-Path $PSScriptRoot 'nightly-integration-evidence.ps1') -Mode Run `
            -Source $source -Shard 'fixture' -ResultsDirectory $directory *> (Join-Path $root "$fault.run.log")
        $exit = $LASTEXITCODE
        if (($exit -eq 0) -ne ($fault -eq 'pass')) { throw "${fault}: wrong runner outcome; see $root/$fault.run.log" }
        $plan = Get-Content (Join-Path $directory 'nightly-integration-fixture.required/plan.json') -Raw | ConvertFrom-Json
        if ($plan.invocations.Count -ne 2) { throw 'Fixture did not independently plan both assemblies.' }
        $first = Get-Content (Join-Path $directory ('nightly-integration-fixture.required/' + $plan.invocations[0].receipt)) -Raw | ConvertFrom-Json
        if ($first.exitCode -ne 0 -or $first.trx.Count -ne 1) { throw 'Early positive assembly did not pass.' }
        Verify-Control $fault $directory ($fault -eq 'pass') $(if ($exit -eq 0) {'success'} else {'failure'})
    }
    $passing = Join-Path $root 'pass'
    Verify-Control 'failed-step-with-passing-results' $passing $false 'failure'
    Verify-Control 'cancelled-step-with-passing-results' $passing $false 'cancelled'
    $runnerPath = Join-Path $passing 'nightly-integration-fixture.runner.json'
    $originalRunner = Get-Content $runnerPath -Raw
    $runner = $originalRunner | ConvertFrom-Json
    $runner.exitCode = 37
    $runner | ConvertTo-Json -Depth 8 | Set-Content $runnerPath
    Verify-Control 'nonzero-runner-with-passing-results' $passing $false
    Set-Content $runnerPath $originalRunner
    $evidence = Join-Path $passing 'nightly-integration-fixture.required'
    $plan = Get-Content (Join-Path $evidence 'plan.json') -Raw | ConvertFrom-Json
    $receiptPath = Join-Path $evidence $plan.invocations[1].receipt
    $originalReceipt = Get-Content $receiptPath -Raw
    Remove-Item -LiteralPath $receiptPath
    Verify-Control 'missing-later-receipt' $passing $false
    Set-Content $receiptPath $originalReceipt
    $receipt = $originalReceipt | ConvertFrom-Json
    $trxPath = Join-Path $evidence $receipt.trx[0].path
    $originalTrx = [IO.File]::ReadAllBytes($trxPath)
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $trx.TestRun.ResultSummary.SetAttribute('outcome','Aborted')
    $trx.Save($trxPath)
    $receipt.trx[0].sha256 = (Get-FileHash $trxPath).Hash.ToLowerInvariant()
    $receipt | ConvertTo-Json -Depth 12 | Set-Content $receiptPath
    Verify-Control 'all-passed-identities-aborted-summary' $passing $false
    [IO.File]::WriteAllBytes($trxPath,$originalTrx)
    Set-Content $receiptPath $originalReceipt
    $env:GITHUB_RUN_ATTEMPT = '2'
    Verify-Control 'replayed-attempt' $passing $false
    $env:GITHUB_RUN_ATTEMPT = '1'
    Verify-Control 'restored-positive-control' $passing $true
    # Keep a self-consistent artifact for only the first project while the external shard still
    # requires both. This must fail independently of missing-receipt or unclaimed-TRX checks.
    $omitted = Join-Path $root 'omitted-project'
    $omittedEvidence = Join-Path $omitted 'nightly-integration-fixture.required'
    [void][IO.Directory]::CreateDirectory($omittedEvidence)
    Copy-Item -LiteralPath $runnerPath -Destination (Join-Path $omitted 'nightly-integration-fixture.runner.json')
    foreach ($file in Get-ChildItem -LiteralPath $evidence -File) { Copy-Item -LiteralPath $file.FullName -Destination $omittedEvidence }
    Copy-Item -LiteralPath (Join-Path $evidence $plan.invocations[0].id) -Destination $omittedEvidence -Recurse
    $omittedPlan = Get-Content (Join-Path $omittedEvidence 'plan.json') -Raw | ConvertFrom-Json
    $omittedPlan.invocations = @($omittedPlan.invocations[0])
    $rosterPath = Join-Path $omittedEvidence $omittedPlan.evaluatedRoster.path
    $roster = Get-Content $rosterPath -Raw | ConvertFrom-Json
    $roster.entries = @($roster.entries | Where-Object project -eq $omittedPlan.invocations[0].project)
    $roster | ConvertTo-Json -Depth 20 | Set-Content $rosterPath
    $omittedPlan.evaluatedRoster.sha256 = (Get-FileHash $rosterPath).Hash
    $omittedPlanPath = Join-Path $omittedEvidence 'plan.json'
    $omittedPlan | ConvertTo-Json -Depth 20 | Set-Content $omittedPlanPath
    $omittedReceiptPath = Join-Path $omittedEvidence $omittedPlan.invocations[0].receipt
    $omittedReceipt = Get-Content $omittedReceiptPath -Raw | ConvertFrom-Json
    $omittedReceipt.planSha256 = (Get-FileHash $omittedPlanPath).Hash.ToLowerInvariant()
    $omittedReceipt | ConvertTo-Json -Depth 20 | Set-Content $omittedReceiptPath
    ConvertTo-Json -InputObject @('First.dll') | Set-Content (Join-Path $omittedEvidence 'expected-assemblies.json')
    Verify-Control 'omitted-project-with-consistent-artifact' $omitted $false
    if ((Get-Content (Join-Path $root 'omitted-project-with-consistent-artifact.verify.log') -Raw) -notmatch 'Planned project roster differs from external source') {
        throw 'Omitted project was not rejected against the independent source roster.'
    }
    Verify-Control 'missing-shard-evidence' (Join-Path $root 'missing') $false
    # Exercise the shared execution path with a short real VSTest deadline; production keeps its
    # 75-minute session budget. An early passing assembly must not conceal the later timed-out one.
    $env:CI03_FAULT = 'hang'
    $timeoutDirectory = Join-Path $root 'execution-timeout'
    $timeoutDriver = Join-Path $root 'timeout-control.ps1'
    @'
param($Repo,$Source,$Context,$Results)
& (Join-Path $Repo 'eng/build.ps1') -Test -NoRestore -NoBuild -Project $Source `
    -TestFilter 'Category=Integration|Category=EndToEnd' -MaxCpuCount 1 -BlameTimeout 5s `
    -TestSessionTimeout 5000 -RequiredTestContext $Context -ResultsDirectory $Results -ResultsPrefix timeout
'@ | Set-Content $timeoutDriver
    & $shell -NoProfile -File $timeoutDriver -Repo $repo -Source $source `
        -Context (Join-Path $passing 'nightly-integration-fixture.run.context.json') -Results $timeoutDirectory *> (Join-Path $root 'execution-timeout.log')
    if ($LASTEXITCODE -eq 0) { throw 'Timed-out execution reported success.' }
    $timeoutPlan = Get-Content (Join-Path $timeoutDirectory 'timeout.required/plan.json') -Raw | ConvertFrom-Json
    $firstTimeout = Get-Content (Join-Path $timeoutDirectory ('timeout.required/' + $timeoutPlan.invocations[0].receipt)) -Raw | ConvertFrom-Json
    $secondTimeout = Get-Content (Join-Path $timeoutDirectory ('timeout.required/' + $timeoutPlan.invocations[1].receipt)) -Raw | ConvertFrom-Json
    if ($firstTimeout.exitCode -ne 0 -or $secondTimeout.exitCode -eq 0) { throw 'Execution timeout did not preserve both raw runner outcomes.' }
    if ((Get-Content (Join-Path $root 'execution-timeout.log') -Raw) -notmatch 'test run timeout of 5000 milliseconds exceeded') {
        throw 'Execution failed without exercising the required VSTest timeout.'
    }
    $count++
    Write-Host 'PASS: execution-timeout-after-passing-assembly'
    Write-Host "PASS: $count nightly integration evidence controls. Evidence: $root"
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name]) }
}
exit 0
