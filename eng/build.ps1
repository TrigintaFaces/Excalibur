#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The repository's build entry point. CI runs this; contributors run this.

.DESCRIPTION
    One command for restore/build/test/pack, so the sequence a contributor runs locally is the
    sequence CI runs. Invoke through ./build.sh or .\build.cmd at the repository root.

    Verbs are additive and run in dependency order regardless of the order given. With no verb,
    -Restore -Build is assumed.

.EXAMPLE
    ./build.sh --restore --build
    ./build.sh --test --project eng/ci/shards/UnitTests-Core.slnf
    ./build.sh --build --test --configuration Debug
    ./build.sh --pack
#>
[CmdletBinding()]
param(
    [switch]$Restore,
    [switch]$Build,
    [switch]$Test,
    [switch]$Pack,

    # A .sln or .slnf. CI shards are .slnf files under eng/ci/shards.
    [string]$Project = 'Excalibur.sln',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # dotnet test --filter expression. When set, the run is required to have executed at least one
    # test: `dotnet test --filter` exits 0 when the filter matches nothing.
    [string]$TestFilter,

    [switch]$Coverage,

    # Sets ContinuousIntegrationBuild and turns on the solution-scoped audit-coverage assertion.
    [switch]$CI,

    # Suppress the restore that -Build would otherwise imply, for callers that restored separately.
    [switch]$NoRestore,

    # restore --locked-mode: fail rather than silently update a lock file.
    [switch]$LockedMode,

    # build --no-incremental. Note this rebuilds the named project, NOT its project references;
    # see .claude/rules/process/clean-rebuild-before-trusting-locks.md.
    [switch]$NoIncremental,

    # MSBuild properties, given without the -p: prefix, e.g. --properties CI=true AuditPipeline=true.
    [string[]]$Properties = @(),

    # build -warnaserror.
    [switch]$WarnAsError,

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

# NOTE: there is deliberately no -Verbose parameter. [CmdletBinding()] already supplies one as a
# common parameter, and declaring it again is a MetadataError that fails the script before it runs.
# The previous version of this file did exactly that, which is why nothing ever called it.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    if (-not ($Restore -or $Build -or $Test -or $Pack)) {
        $Restore = $true
        $Build = $true
    }

    # -Test and -Pack need built output; asking for them alone should not silently test stale bits.
    if (($Test -or $Pack) -and -not $Build) { $Build = $true }
    # Restore is implied by build, unless the caller ran it as its own step - which CI does, so that
    # one restore can carry --locked-mode and several builds can follow it.
    if ($Build -and -not $Restore -and -not $NoRestore) { $Restore = $true }
    if ($NoRestore) { $Restore = $false }

    if (-not (Test-Path $Project)) { throw "Project or solution not found: $Project" }

    $artifacts = Join-Path $repoRoot 'artifacts'
    $commonArgs = @('--verbosity', $Verbosity)
    if ($CI) { $commonArgs += '-p:ContinuousIntegrationBuild=true' }
    # Split on comma as well as taking an array: invoked through build.sh the value arrives as one
    # argv element, so "A=1,B=2" would otherwise become a single -p:A=1,B=2.
    foreach ($prop in ($Properties | ForEach-Object { $_ -split ',' })) {
        $prop = $prop.Trim()
        if ($prop) { $commonArgs += "-p:$prop" }
    }

    function Invoke-Step {
        param([string]$Name, [string[]]$Arguments)
        Write-Host "==> dotnet $($Arguments -join ' ')"
        & dotnet @Arguments
        # Captured immediately. Anything between the call and this read - even a Write-Host - replaces
        # the value with its own.
        $code = $LASTEXITCODE
        if ($code -ne 0) { throw "$Name failed (dotnet exited $code)" }
    }

    if ($Restore) {
        $restoreArgs = @('restore', $Project)
        if ($LockedMode) { $restoreArgs += '--locked-mode' }
        Invoke-Step 'restore' ($restoreArgs + $commonArgs)
    }

    if ($Build) {
        # BuildExamplesAndTests is required or test projects are compile-skipped and produce empty
        # assemblies - see .github/actions/setup-dotnet-build.
        $buildArgs = @(
            'build', $Project, '--configuration', $Configuration, '--no-restore',
            '-p:BuildExamplesAndTests=true')
        if ($NoIncremental) { $buildArgs += '--no-incremental' }
        if ($WarnAsError) { $buildArgs += '-warnaserror' }
        Invoke-Step 'build' ($buildArgs + $commonArgs)
    }

    if ($Test) {
        $testArgs = @('test', $Project, '--configuration', $Configuration, '--no-build', '--nologo')
        if ($TestFilter) { $testArgs += @('--filter', $TestFilter) }
        $results = Join-Path $artifacts 'TestResults'
        $testArgs += @('--logger', 'trx;LogFilePrefix=build', '--results-directory', $results)
        if ($Coverage) {
            $testArgs += @('--collect:XPlat Code Coverage', '--settings', 'tests/coverage.runsettings')
        }
        Invoke-Step 'test' $testArgs

        # A filtered run that matched nothing exits 0. The count is the evidence, not the exit code.
        if ($TestFilter) {
            $executed = 0
            Get-ChildItem -Path $results -Filter '*.trx' -Recurse -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $xml = [xml](Get-Content $_.FullName)
                    $executed += [int]$xml.TestRun.ResultSummary.Counters.executed
                }
            if ($executed -eq 0) {
                throw "filter '$TestFilter' executed 0 tests. A filter that matches nothing exits 0; that is not a pass."
            }
            Write-Host "$executed test(s) executed."
        }
    }

    if ($Pack) {
        Invoke-Step 'pack' (@(
            'pack', $Project, '--configuration', $Configuration, '--no-build',
            '--output', (Join-Path $artifacts 'packages')) + $commonArgs)
    }

    Write-Host 'Build entry point completed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
