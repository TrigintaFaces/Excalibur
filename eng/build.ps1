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

    # Suppress the build that -Test would otherwise imply, for callers that built separately -- which
    # CI does, in its own step, so that one build can serve several test invocations. Without this a
    # -Test call rebuilds what the caller just built, and rebuilds it with THESE flags rather than the
    # caller's, which is a second build behaving differently from the first.
    [switch]$NoBuild,

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
    [string]$Verbosity = 'minimal',

    # ---- CI-shaped test settings -------------------------------------------------------------------
    # These exist so the composite action can call THIS script rather than composing its own
    # `dotnet test`. Two implementations of that command is the drift this entry point exists to
    # prevent, and the divergence would be silent: a swallowed RunSetting still runs, still passes,
    # and simply ignores the setting.
    #
    # A contributor never needs to pass any of them; each is inert when unset.

    # --blame-hang-timeout. Ends a wedged run and names what was stuck.
    [string]$BlameTimeout,

    # trx LogFilePrefix. Shards share a results directory, so a per-shard prefix keeps their trx apart.
    [string]$ResultsPrefix,

    # Where trx files land. CI uploads from the repo-root TestResults; the default here is under
    # artifacts/ so a contributor's runs do not litter the tree.
    [string]$ResultsDirectory,

    # RunConfiguration.TestSessionTimeout, in milliseconds. Applies with or WITHOUT coverage: it is
    # what ends a wedged run while emitting a trx that names how far it got. Without it the job is
    # killed by the workflow wall and reports nothing at all.
    [string]$TestSessionTimeout,

    # dotnet test -m:N. A dotnet-test argument, NOT a RunSetting -- see the composition below.
    [string]$MaxCpuCount,

    # Extra RunSettings / xUnit args, space-separated (e.g. serial execution for a shard whose
    # concurrency roots a native thread leak). Word-split deliberately.
    [string]$ExtraRunSettings,

    # Print the composed `dotnet test` argv and exit without running it. This is what makes the
    # composition testable: the traps below are invisible at runtime -- a dropped setting produces a
    # passing run -- so they are asserted against the printed argv instead.
    [switch]$ShowTestCommand
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
    # -NoBuild is the caller asserting they built already, which is what CI's separate build step is.
    if (($Test -or $Pack) -and -not $Build -and -not $NoBuild) { $Build = $true }
    if ($NoBuild) { $Build = $false }
    # Restore is implied by build, unless the caller ran it as its own step - which CI does, so that
    # one restore can carry --locked-mode and several builds can follow it.
    if ($Build -and -not $Restore -and -not $NoRestore) { $Restore = $true }
    if ($NoRestore) { $Restore = $false }

    if (-not (Test-Path $Project)) { throw "Project or solution not found: $Project" }

    # ---------------------------------------------------------------------------------------------
    # Provision the pinned SDK before anything invokes dotnet.
    #
    # global.json pins an exact version with rollForward disabled, which is what makes a locked-mode
    # restore reproducible: the SDK decides the version of the packs it injects, so an SDK that moves
    # silently invalidates every lock file. Pinning without provisioning only moves the problem --
    # the machine that lacks that exact SDK gets "A compatible .NET SDK was not found" and cannot
    # build at all. That is not hypothetical: a routine update replaced the pinned SDK on a working
    # machine the day after it was pinned.
    #
    # So the pin and the acquisition are one mechanism. If the pinned SDK is absent, install it into
    # a repository-local .dotnet and put that first on PATH. Nothing outside the repository is
    # touched: no system install, no machine-wide version change, and the directory is ignored.
    # ---------------------------------------------------------------------------------------------
    function Initialize-PinnedSdk {
        param([string]$RepoRoot)

        $globalJsonPath = Join-Path $RepoRoot 'global.json'
        if (-not (Test-Path $globalJsonPath)) { return }

        $pinned = (Get-Content $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
        if (-not $pinned) { return }

        # `dotnet --list-sdks` is the authority on what is present. Parsing its output is the only
        # way to ask "is THIS exact version here", which is the question rollForward:disable asks.
        $installed = @()
        if (Get-Command dotnet -ErrorAction SilentlyContinue) {
            $installed = & dotnet --list-sdks 2>$null | ForEach-Object { ($_ -split ' ')[0] }
        }

        if ($installed -contains $pinned) { return }

        $localRoot = Join-Path $RepoRoot '.dotnet'
        $localSdk = Join-Path $localRoot "sdk/$pinned"
        if (-not (Test-Path $localSdk)) {
            Write-Host "==> pinned .NET SDK $pinned not found; installing it to .dotnet (repository-local)"
            $installer = Join-Path ([System.IO.Path]::GetTempPath()) "dotnet-install-$pinned"

            if ($IsWindows) {
                $installer += '.ps1'
                Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
                & $installer -Version $pinned -InstallDir $localRoot -NoPath
            }
            else {
                $installer += '.sh'
                Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.sh' -OutFile $installer -UseBasicParsing
                & chmod +x $installer
                & $installer --version $pinned --install-dir $localRoot --no-path
            }

            # Captured immediately; anything between the call and this read replaces it.
            $code = $LASTEXITCODE
            if ($code -ne 0) { throw "dotnet-install failed for SDK $pinned (exited $code)" }
        }

        if (-not (Test-Path $localSdk)) {
            throw "SDK $pinned was not present under $localRoot after install. Refusing to continue: a build against a different SDK is what global.json exists to prevent."
        }

        $env:PATH = "$localRoot$([System.IO.Path]::PathSeparator)$env:PATH"
        $env:DOTNET_ROOT = $localRoot
        $env:DOTNET_MULTILEVEL_LOOKUP = '0'
        Write-Host "==> using repository-local .NET SDK $pinned from .dotnet"
    }

    Initialize-PinnedSdk -RepoRoot $repoRoot

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

        # A binary log, on CI, always. This is the ONLY artifact that answers "what did the build
        # actually do" after the fact: it records every target, property and skip decision, none of
        # which a --verbosity quiet console log keeps. Without it a build that succeeded while doing
        # the wrong thing is indistinguishable from one that did the right thing, and the evidence is
        # gone when the runner is recycled.
        #
        # The composite action already did this for the jobs that use it, but the jobs that call this
        # entry point -- roughly twice as many -- produced nothing. So "the build emits a binary log
        # by default in CI" was true of the smaller half of the pipeline and false of the larger,
        # which is the shape of a claim that reads as satisfied and is not.
        #
        # Named per project so parallel shards in one job cannot overwrite each other's log; the
        # project path is flattened because it contains separators.
        if ($CI) {
            $logName = ($Project -replace '[\\/]', '_') -replace '[^A-Za-z0-9._-]', ''
            $buildArgs += "-bl:$(Join-Path $artifacts "build.$logName.binlog")"
        }

        Invoke-Step 'build' ($buildArgs + $commonArgs)
    }

    if ($Test) {
        # ONE RunSettings separator, and everything that must follow it collected first.
        #
        # `dotnet test` accepts exactly one `--`. A branch that emits its own silently swallows every
        # setting a later branch would have added, and the loss is invisible: the run still starts,
        # still passes, and simply ignores the setting. That is why the settings are accumulated and
        # emitted once, at the end, rather than appended where they are decided.
        $testArgs = @('test', $Project, '--configuration', $Configuration, '--no-build', '--nologo')
        $runSettings = @()

        if ($BlameTimeout) { $testArgs += @('--blame-hang-timeout', $BlameTimeout) }

        $results = if ($ResultsDirectory) { $ResultsDirectory } else { Join-Path $artifacts 'TestResults' }
        $prefix = if ($ResultsPrefix) { $ResultsPrefix } else { 'build' }
        $testArgs += @('--logger', "trx;LogFilePrefix=$prefix",
                       '--logger', 'console;verbosity=minimal',
                       '--results-directory', $results)

        if ($TestFilter) { $testArgs += @('--filter', $TestFilter) }

        if ($Coverage) {
            $testArgs += '--collect:XPlat Code Coverage'
            $runSettings += 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura'
        }

        # -m:N is a dotnet-test argument, NOT a RunSetting: after the separator it is parsed as a
        # setting name and silently does nothing.
        if ($MaxCpuCount) { $testArgs += "-m:$MaxCpuCount" }

        # With or without coverage. This was once inside the coverage branch, which left it inert on
        # every run that did not collect coverage -- the runs most likely to wedge.
        if ($TestSessionTimeout) { $runSettings += "RunConfiguration.TestSessionTimeout=$TestSessionTimeout" }

        # Word-split deliberately: the caller passes several settings as one string.
        if ($ExtraRunSettings) {
            $runSettings += ($ExtraRunSettings -split '\s+' | Where-Object { $_ })
        }

        if ($runSettings.Count -gt 0) { $testArgs += @('--') + $runSettings }

        # Echoed always: a dropped or misplaced argument is otherwise invisible in the log, which is
        # exactly what let a session timeout stay inert across every coverage run.
        Write-Host "[build] dotnet $($testArgs -join ' ')"
        if ($ShowTestCommand) {
            Write-Host 'ShowTestCommand: composed only, nothing executed.'
            return
        }

        # Stamped before the run, so the guard below counts only what this invocation produced.
        # A second of slack absorbs filesystem timestamp granularity.
        $testStartedUtc = (Get-Date).ToUniversalTime().AddSeconds(-1)
        Invoke-Step 'test' $testArgs

        # A filtered run that matched nothing exits 0. The count is the evidence, not the exit code.
        #
        # Counted from THIS RUN'S trx only, by write time. Summing every trx in the directory is the
        # same guard with the same message and no meaning: results directories accumulate, so a run
        # that executed nothing still sees six figures of historical executions and passes. Measured
        # locally against a shared ./TestResults: 20 tests ran and the guard reported 404,902.
        #
        # That is a false green in the check whose entire purpose is to catch a false green.
        if ($TestFilter) {
            $executed = 0
            Get-ChildItem -Path $results -Filter '*.trx' -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTimeUtc -ge $testStartedUtc } |
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
