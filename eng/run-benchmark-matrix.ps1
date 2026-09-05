# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [string]$ArtifactsPath = "benchmarks/runs/BenchmarkDotNet.Artifacts",

    [Parameter(Mandatory = $false)]
    [switch]$NoBuild,

    [Parameter(Mandatory = $false)]
    [switch]$NoRestore,

    [Parameter(Mandatory = $false)]
    [switch]$ComparativeOnly,

    [Parameter(Mandatory = $false)]
    [switch]$DiagnosticsOnly,

    [Parameter(Mandatory = $false)]
    [switch]$CiSmoke,

    [Parameter(Mandatory = $false)]
    [switch]$ContinueOnError,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseFrameworkLogs,

    [Parameter(Mandatory = $false)]
    [string[]]$Classes,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfile,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfilesPath = "eng/runtime-profiles.json",

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 20)]
    [int]$RepeatCount = 1,

    # Runs the verdict self-test (safety + liveness arms) and exits. Runs no benchmarks.
    [Parameter(Mandatory = $false)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SafeCommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $false)]
        [string[]]$Arguments
    )

    try {
        $output = & $FilePath @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        if ($output -is [System.Array]) {
            return ($output -join [Environment]::NewLine).Trim()
        }

        return "$output".Trim()
    }
    catch {
        return $null
    }
}

function Get-BenchmarkEnvironmentMetadata {
    param(
        [Parameter(Mandatory = $false)]
        [string]$RuntimeProfileName,

        [Parameter(Mandatory = $false)]
        [hashtable]$RuntimeProfileVariables
    )

    $commitSha = Get-SafeCommandOutput -FilePath "git" -Arguments @("rev-parse", "HEAD")
    $branchName = Get-SafeCommandOutput -FilePath "git" -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
    $dotnetVersion = Get-SafeCommandOutput -FilePath "dotnet" -Arguments @("--version")

    return [pscustomobject]@{
        machineName             = [Environment]::MachineName
        osDescription           = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        osArchitecture          = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture     = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        frameworkDescription    = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
        processorCount          = [Environment]::ProcessorCount
        isServerGc              = [System.Runtime.GCSettings]::IsServerGC
        gcLatencyMode           = [System.Runtime.GCSettings]::LatencyMode.ToString()
        dotnetVersion           = $dotnetVersion
        commitSha               = $commitSha
        branch                  = $branchName
        isCi                    = [string]::Equals($env:GITHUB_ACTIONS, "true", [System.StringComparison]::OrdinalIgnoreCase)
        dotnetTieredPgo         = $env:DOTNET_TieredPGO
        dotnetReadyToRun        = $env:DOTNET_ReadyToRun
        dotnetGcServer          = $env:DOTNET_gcServer
        dotnetTcQuickJit        = $env:DOTNET_TC_QuickJit
        dotnetTcQuickJitForLoops= $env:DOTNET_TC_QuickJitForLoops
        comPlusTieredPgo        = $env:COMPlus_TieredPGO
        comPlusReadyToRun       = $env:COMPlus_ReadyToRun
        comPlusGcServer         = $env:COMPlus_gcServer
        runtimeProfile          = if ([string]::IsNullOrWhiteSpace($RuntimeProfileName)) { "none" } else { $RuntimeProfileName }
        runtimeProfileVariables = if ($RuntimeProfileVariables) { $RuntimeProfileVariables } else { @{} }
    }
}

function Get-RuntimeProfileVariables {
    param(
        [Parameter(Mandatory = $false)]
        [string]$ProfileName,

        [Parameter(Mandatory = $true)]
        [string]$ProfilesFilePath
    )

    if ([string]::IsNullOrWhiteSpace($ProfileName)) {
        return @{}
    }

    if (-not (Test-Path $ProfilesFilePath)) {
        throw "Runtime profiles file not found: $ProfilesFilePath"
    }

    $profilesRoot = Get-Content -Path $ProfilesFilePath -Raw | ConvertFrom-Json -AsHashtable
    if (-not $profilesRoot.ContainsKey("profiles")) {
        throw "Runtime profiles file is missing the top-level 'profiles' object: $ProfilesFilePath"
    }

    $profiles = $profilesRoot["profiles"]
    if (-not $profiles.ContainsKey($ProfileName)) {
        $availableProfiles = @($profiles.Keys) -join ", "
        throw "Runtime profile '$ProfileName' was not found in $ProfilesFilePath. Available profiles: $availableProfiles"
    }

    $selectedProfile = $profiles[$ProfileName]
    if ($selectedProfile -isnot [hashtable]) {
        throw "Runtime profile '$ProfileName' is not a valid key/value map in $ProfilesFilePath"
    }

    return $selectedProfile
}

# A run that measured NOTHING must render as REFUSE -- never as OK.
#
# BenchmarkDotNet exits 0 when a benchmark class fails to produce results: it prints "NA" in
# every result cell, appends a "Benchmarks with issues" block, and returns success. This runner
# used to count result ROWS, so an all-NA class reported "OK <class> (1 rows)" and the matrix
# exited 0. Nothing distinguished it from a real run except opening each report by hand.
#
# Three states, matching the eng/ci/*.sh gates:
#   PASS   (exit 0) -- every result row carries a measurement.
#   FAIL   (exit 2) -- the run itself errored (non-zero exit from dotnet).
#   REFUSE (exit 3) -- the run completed without measuring anything usable. NOT a pass and NOT
#                      a failure: nothing from it may be published or diffed against a baseline.
$script:RefusalLogMarkers = @(
    @{
        Pattern = "Found more than one matching project file"
        Reason  = "more than one Excalibur.Dispatch.Benchmarks.csproj is reachable from the repository root -- a leftover agent worktree under .claude/worktrees/ is the usual cause. BenchmarkDotNet's default (CsProj) toolchain cannot pick one and refuses to generate, so every row of a CsProj-toolchain class reads NA. Classes pinned to InProcessEmitToolchain are unaffected, which is why part of the suite keeps working."
    },
    @{
        Pattern = "BenchmarkDotNet has failed to build"
        Reason  = "BenchmarkDotNet failed to build the generated benchmark project; no measurement was taken"
    },
    @{
        Pattern = "returned 0 benchmarks"
        Reason  = "the --filter matched zero benchmarks; BenchmarkDotNet printed its usage help instead of running anything"
    }
)

function Get-BenchmarkRunVerdict {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,

        [Parameter(Mandatory = $true)]
        [bool]$ReportFound,

        [Parameter(Mandatory = $false)]
        [string]$CsvPath,

        [Parameter(Mandatory = $false)]
        [string]$LogPath
    )

    $measuredRows = 0
    $naRows = 0

    if (-not [string]::IsNullOrWhiteSpace($CsvPath) -and (Test-Path $CsvPath)) {
        foreach ($row in @(Import-Csv -Path $CsvPath)) {
            $mean = ""
            if ($row.PSObject.Properties.Name -contains "Mean") {
                $mean = "$($row.Mean)".Trim()
            }

            # "NA" is what BenchmarkDotNet writes for a benchmark that produced no result.
            if ([string]::IsNullOrWhiteSpace($mean) -or $mean -eq "NA" -or $mean -eq "?") {
                $naRows++
            }
            else {
                $measuredRows++
            }
        }
    }

    $logReason = $null
    if (-not [string]::IsNullOrWhiteSpace($LogPath) -and (Test-Path $LogPath)) {
        $logText = Get-Content -Path $LogPath -Raw -ErrorAction SilentlyContinue
        if (-not [string]::IsNullOrWhiteSpace($logText)) {
            foreach ($marker in $script:RefusalLogMarkers) {
                if ($logText -match [regex]::Escape($marker.Pattern)) {
                    $logReason = $marker.Reason
                    break
                }
            }
        }
    }

    $verdict = "PASS"
    $reason = ""

    if ($ExitCode -ne 0) {
        $verdict = "FAIL"
        $reason = "dotnet exited with code $ExitCode"
    }
    elseif ($null -ne $logReason) {
        $verdict = "REFUSE"
        $reason = $logReason
    }
    elseif (-not $ReportFound) {
        $verdict = "REFUSE"
        $reason = "this run wrote no report of its own; nothing was measured (a report left in the results directory by an earlier run is ignored)"
    }
    elseif (($measuredRows + $naRows) -eq 0) {
        $verdict = "REFUSE"
        $reason = "the run wrote a report with zero result rows"
    }
    elseif ($naRows -gt 0) {
        $verdict = "REFUSE"
        $reason = "$naRows of $($measuredRows + $naRows) result rows read NA -- BenchmarkDotNet produced no measurement for them and still exited 0"
    }

    return [pscustomobject]@{
        Verdict      = $verdict
        Reason       = $reason
        MeasuredRows = $measuredRows
        NaRows       = $naRows
    }
}

# Non-vacuous self-test: proves the verdict REFUSES each condition it claims to catch AND still
# passes a clean run (safety + liveness arms). Milliseconds; runs no benchmarks.
if ($SelfTest) {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("benchmark-matrix-selftest-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null
    $script:selfTestFailures = @()

    function Assert-Verdict {
        param([string]$Case, [string]$Expected, $Actual)

        if ($Actual.Verdict -eq $Expected) {
            Write-Host "  ok     $Case -> $($Actual.Verdict)" -ForegroundColor Green
        }
        else {
            Write-Host "  FAILED $Case -> expected $Expected, got $($Actual.Verdict)" -ForegroundColor Red
            $script:selfTestFailures += $Case
        }
    }

    try {
        $goodCsv = Join-Path $fixtureRoot "good.csv"
        Set-Content -Path $goodCsv -Encoding UTF8 -Value @(
            "Method,Job,Mean,Error",
            "TypeName_Cached,warmpath-inproc,2.6605 ns,0.0088 ns")

        $naCsv = Join-Path $fixtureRoot "na.csv"
        Set-Content -Path $naCsv -Encoding UTF8 -Value @(
            "Method,Job,Mean,Error",
            "TypeName_Cached,DefaultJob,NA,NA")

        $emptyCsv = Join-Path $fixtureRoot "empty.csv"
        Set-Content -Path $emptyCsv -Encoding UTF8 -Value "Method,Job,Mean,Error"

        $cleanLog = Join-Path $fixtureRoot "clean.log"
        Set-Content -Path $cleanLog -Encoding UTF8 -Value "// * Summary *"

        $dupeLog = Join-Path $fixtureRoot "dupe.log"
        Set-Content -Path $dupeLog -Encoding UTF8 -Value "System.NotSupportedException: Found more than one matching project file for Excalibur.Dispatch.Benchmarks"

        $buildLog = Join-Path $fixtureRoot "build.log"
        Set-Content -Path $buildLog -Encoding UTF8 -Value "// Build Error: BenchmarkDotNet has failed to build the auto-generated boilerplate"

        $filterLog = Join-Path $fixtureRoot "filter.log"
        Set-Content -Path $filterLog -Encoding UTF8 -Value "The filter that you have provided returned 0 benchmarks"

        Write-Host "Benchmark matrix verdict self-test" -ForegroundColor Cyan

        # LIVENESS -- a clean run must still PASS, or the gate is vacuous in the other direction.
        Assert-Verdict -Case "clean run" -Expected "PASS" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $true -CsvPath $goodCsv -LogPath $cleanLog)

        # SAFETY -- every way a run can measure nothing must REFUSE.
        Assert-Verdict -Case "all-NA report (the observed false OK)" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $true -CsvPath $naCsv -LogPath $cleanLog)
        Assert-Verdict -Case "duplicate csproj in the tree" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $true -CsvPath $goodCsv -LogPath $dupeLog)
        Assert-Verdict -Case "BenchmarkDotNet failed to build" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $true -CsvPath $goodCsv -LogPath $buildLog)
        Assert-Verdict -Case "filter matched zero benchmarks" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $false -CsvPath $emptyCsv -LogPath $filterLog)
        Assert-Verdict -Case "no report written by this run" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $false -CsvPath "" -LogPath $cleanLog)
        Assert-Verdict -Case "report with zero rows" -Expected "REFUSE" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 0 -ReportFound $true -CsvPath $emptyCsv -LogPath $cleanLog)

        # A real failure stays a FAIL, distinct from a REFUSE.
        Assert-Verdict -Case "dotnet exited non-zero" -Expected "FAIL" -Actual (
            Get-BenchmarkRunVerdict -ExitCode 1 -ReportFound $true -CsvPath $goodCsv -LogPath $cleanLog)
    }
    finally {
        Remove-Item -Recurse -Force -Path $fixtureRoot -ErrorAction SilentlyContinue
    }

    if (@($script:selfTestFailures).Count -gt 0) {
        [Console]::Error.WriteLine("Verdict self-test FAILED for: $($script:selfTestFailures -join ', ')")
        exit 1
    }

    Write-Host "Verdict self-test passed (safety + liveness arms)." -ForegroundColor Green
    exit 0
}

if ($ComparativeOnly -and $DiagnosticsOnly) {
    throw "Use only one of -ComparativeOnly or -DiagnosticsOnly."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj"
$artifactsFullPath = Join-Path $repoRoot $ArtifactsPath
$resultsPath = Join-Path $artifactsFullPath "results"

if (-not (Test-Path $projectPath)) {
    throw "Benchmark project not found: $projectPath"
}

if (-not (Test-Path $artifactsFullPath)) {
    New-Item -ItemType Directory -Force -Path $artifactsFullPath | Out-Null
}

if (-not (Test-Path $resultsPath)) {
    New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
}

$comparativeClasses = @(
    "MediatRComparisonBenchmarks",
    "WolverineInProcessComparisonBenchmarks",
    "MassTransitMediatorComparisonBenchmarks",
    "TransportQueueParityComparisonBenchmarks",
    "WolverineComparisonBenchmarks",
    "MassTransitComparisonBenchmarks",
    "PipelineComparisonBenchmarks",
    "StartupComparisonBenchmarks",
    "RoutingFirstParityBenchmarks",
    "DispatchThroughputBenchmarks"
)

$diagnosticClasses = @(
    "DispatchHotPathBreakdownBenchmarks",
    "MiddlewareCostCurveBenchmarks",
    "MetricsLoggingOverheadBenchmarks",
    "HandlerResolutionBenchmarks",
    "HandlerFanOutBenchmarks",
    "TransportAdapterPhaseBenchmarks",
    "FailurePathBenchmarks",
    "LongRunAllocationGcBenchmarks",
    "CancellationCostBenchmarks",
    "RetryPolicyMicroBenchmarks",
    "DispatchContextCostBenchmarks",
    "AllocationHotspotBenchmarks",
    "ConcurrencyContentionBenchmarks",
    "FanOutColdDecompositionBenchmarks",
    "FanOutBehaviorMatrixBenchmarks",
    "TransportConcurrencyBreakdownBenchmarks",
    "ActivationStrategyBenchmarks",
    "HandlerInvokerPathBenchmarks"
)

$ciSmokeClasses = @(
    "MediatRComparisonBenchmarks",
    "WolverineInProcessComparisonBenchmarks",
    "MassTransitMediatorComparisonBenchmarks",
    "TransportQueueParityComparisonBenchmarks",
    "RoutingFirstParityBenchmarks",
    "DispatchHotPathBreakdownBenchmarks",
    "HandlerInvokerPathBenchmarks"
)

$selectedClasses = if ($Classes -and @($Classes).Count -gt 0) {
    $Classes
}
elseif ($CiSmoke) {
    $ciSmokeClasses
}
elseif ($ComparativeOnly) {
    $comparativeClasses
}
elseif ($DiagnosticsOnly) {
    $diagnosticClasses
}
else {
    $comparativeClasses + $diagnosticClasses
}

$normalizedSelectedClasses = @()
foreach ($classSelection in @($selectedClasses)) {
    foreach ($className in "$classSelection".Split(",")) {
        $trimmed = $className.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            $normalizedSelectedClasses += $trimmed
        }
    }
}

$selectedClasses = $normalizedSelectedClasses

$exporters = @("csv", "markdown", "html", "json")
$matrixStartUtc = [DateTimeOffset]::UtcNow
$matrixStart = Get-Date
$summary = @()
$failures = @()
$refusals = @()

$runtimeProfilesFullPath = if ([System.IO.Path]::IsPathRooted($RuntimeProfilesPath)) {
    $RuntimeProfilesPath
}
else {
    Join-Path $repoRoot $RuntimeProfilesPath
}
$runtimeProfileVariables = Get-RuntimeProfileVariables -ProfileName $RuntimeProfile -ProfilesFilePath $runtimeProfilesFullPath
$runtimeProfileOriginalEnv = @{}
foreach ($envVar in $runtimeProfileVariables.Keys) {
    $runtimeProfileOriginalEnv[$envVar] = [Environment]::GetEnvironmentVariable($envVar)
    [Environment]::SetEnvironmentVariable($envVar, "$($runtimeProfileVariables[$envVar])")
}

$quietLogEnvVars = @(
    "Logging__LogLevel__Default",
    "Logging__LogLevel__Microsoft",
    "Logging__LogLevel__Wolverine",
    "Logging__LogLevel__MassTransit",
    "Logging__LogLevel__Excalibur"
)

$originalEnv = @{}
if (-not $VerboseFrameworkLogs) {
    foreach ($envVar in $quietLogEnvVars) {
        $originalEnv[$envVar] = [Environment]::GetEnvironmentVariable($envVar)
        [Environment]::SetEnvironmentVariable($envVar, "Warning")
    }
}

$environmentMetadata = Get-BenchmarkEnvironmentMetadata -RuntimeProfileName $RuntimeProfile -RuntimeProfileVariables $runtimeProfileVariables

Write-Host "Benchmark matrix root: $repoRoot" -ForegroundColor Cyan
Write-Host "Benchmark project: $projectPath" -ForegroundColor Cyan
Write-Host "Artifacts path: $artifactsFullPath" -ForegroundColor Cyan
Write-Host "Selected classes ($(@($selectedClasses).Count)): $($selectedClasses -join ', ')" -ForegroundColor Cyan
Write-Host "Repeat count: $RepeatCount" -ForegroundColor Cyan
Write-Host "Quiet framework logs: $(-not $VerboseFrameworkLogs)" -ForegroundColor Cyan
Write-Host "Runtime profile: $(if ([string]::IsNullOrWhiteSpace($RuntimeProfile)) { 'none' } else { $RuntimeProfile })" -ForegroundColor Cyan
if ($runtimeProfileVariables.Count -gt 0) {
    Write-Host "Runtime profile variables: $(($runtimeProfileVariables.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join ', ')" -ForegroundColor Cyan
}

Push-Location $repoRoot
try {
    foreach ($className in $selectedClasses) {
        for ($repeatIndex = 1; $repeatIndex -le $RepeatCount; $repeatIndex++) {
            $classStart = Get-Date
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $logFile = Join-Path $resultsPath ("run-{0}-r{1}-{2}.log" -f $className, $repeatIndex, $timestamp)

            Write-Host ""
            Write-Host "=== $className (run $repeatIndex/$RepeatCount) ===" -ForegroundColor Yellow

            $arguments = @(
                "run",
                "--project", $projectPath,
                "--configuration", $Configuration
            )

            if ($NoBuild) {
                $arguments += "--no-build"
            }

            if ($NoRestore) {
                $arguments += "--no-restore"
            }

            $arguments += @(
                "--",
                "--filter", "*$className*",
                "--exporters"
            )
            $arguments += $exporters
            $arguments += @("--artifacts", $artifactsFullPath)

            & dotnet @arguments *> $logFile
            $exitCode = $LASTEXITCODE
            $classDuration = (Get-Date) - $classStart

            # Only a report THIS run wrote counts. A stale file left by an earlier run reads
            # exactly like a fresh one, so without the freshness filter a class that produced
            # nothing at all inherits its predecessor's numbers.
            $githubReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report-github.md" -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -ge $classStart } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1
            $csvReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report.csv" -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -ge $classStart } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1

            $rowCount = 0
            if ($csvReport) {
                $rowCount = @(Import-Csv $csvReport.FullName).Count
            }

            $reportFound = $null -ne $githubReport
            $reportPath = if ($githubReport) { $githubReport.FullName } else { "" }
            $csvReportPath = if ($csvReport) { $csvReport.FullName } else { "" }

            # Snapshot reports per repeat so variance can be computed across runs.
            if ($csvReport) {
                $csvSnapshotPath = Join-Path $resultsPath ("{0}.run{1}.{2}.csv" -f [System.IO.Path]::GetFileNameWithoutExtension($csvReport.Name), $repeatIndex, $timestamp)
                Copy-Item -Path $csvReport.FullName -Destination $csvSnapshotPath -Force
                $csvReportPath = $csvSnapshotPath
            }

            if ($githubReport) {
                $githubSnapshotPath = Join-Path $resultsPath ("{0}.run{1}.{2}.md" -f [System.IO.Path]::GetFileNameWithoutExtension($githubReport.Name), $repeatIndex, $timestamp)
                Copy-Item -Path $githubReport.FullName -Destination $githubSnapshotPath -Force
                $reportPath = $githubSnapshotPath
            }

            $verdict = Get-BenchmarkRunVerdict -ExitCode $exitCode -ReportFound $reportFound -CsvPath $csvReportPath -LogPath $logFile

            $summary += [pscustomobject]@{
                ClassName       = $className
                Run             = $repeatIndex
                ExitCode        = $exitCode
                DurationSeconds = [math]::Round($classDuration.TotalSeconds, 1)
                BenchmarkRows   = $rowCount
                MeasuredRows    = $verdict.MeasuredRows
                NaRows          = $verdict.NaRows
                Verdict         = $verdict.Verdict
                VerdictReason   = $verdict.Reason
                ReportFound     = $reportFound
                ReportPath      = $reportPath
                CsvReportPath   = $csvReportPath
                LogPath         = $logFile
            }

            if ($verdict.Verdict -eq "FAIL") {
                $failures += "$className#run$repeatIndex"
                Write-Host "FAILED $className (run $repeatIndex/$RepeatCount, exit=$exitCode, reportFound=$reportFound)" -ForegroundColor Red
                Write-Host "  $($verdict.Reason)" -ForegroundColor Red
                Write-Host "Log: $logFile" -ForegroundColor Red
            }
            elseif ($verdict.Verdict -eq "REFUSE") {
                $refusals += "$className#run$repeatIndex"
                Write-Host "REFUSE $className (run $repeatIndex/$RepeatCount, $($verdict.MeasuredRows) measured / $($verdict.NaRows) NA rows, $([math]::Round($classDuration.TotalSeconds, 1))s)" -ForegroundColor Magenta
                Write-Host "  $($verdict.Reason)" -ForegroundColor Magenta
                Write-Host "  REFUSE is neither a pass nor a failure: this class measured nothing usable." -ForegroundColor Magenta
                Write-Host "  Do not publish these rows and do not diff them against a baseline." -ForegroundColor Magenta
                Write-Host "Log: $logFile" -ForegroundColor Magenta
            }
            else {
                Write-Host "OK $className (run $repeatIndex/$RepeatCount, $($verdict.MeasuredRows) rows, $([math]::Round($classDuration.TotalSeconds, 1))s)" -ForegroundColor Green
            }

            if ($verdict.Verdict -ne "PASS" -and -not $ContinueOnError) {
                break
            }
        }

        if ((@($failures).Count + @($refusals).Count) -gt 0 -and -not $ContinueOnError) {
            break
        }
    }
}
finally {
    Pop-Location

    if (-not $VerboseFrameworkLogs) {
        foreach ($envVar in $quietLogEnvVars) {
            [Environment]::SetEnvironmentVariable($envVar, $originalEnv[$envVar])
        }
    }

    foreach ($envVar in $runtimeProfileVariables.Keys) {
        [Environment]::SetEnvironmentVariable($envVar, $runtimeProfileOriginalEnv[$envVar])
    }
}

$matrixEnd = Get-Date
$matrixDuration = $matrixEnd - $matrixStart
$totalRows = ($summary | Measure-Object -Property BenchmarkRows -Sum).Sum
if ($null -eq $totalRows) {
    $totalRows = 0
}

# Measured rows -- NOT total rows -- is what "the matrix produced N results" means. An NA row is
# a row BenchmarkDotNet emitted for a benchmark it could not measure.
$totalMeasuredRows = ($summary | Measure-Object -Property MeasuredRows -Sum).Sum
if ($null -eq $totalMeasuredRows) {
    $totalMeasuredRows = 0
}
$totalNaRows = ($summary | Measure-Object -Property NaRows -Sum).Sum
if ($null -eq $totalNaRows) {
    $totalNaRows = 0
}

$summaryTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryJsonPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.json" -f $summaryTimestamp)
$summaryMdPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.md" -f $summaryTimestamp)

$summaryPayload = [pscustomobject]@{
    startedUtc       = $matrixStartUtc.ToString("O")
    endedUtc         = [DateTimeOffset]::UtcNow.ToString("O")
    duration         = [string]$matrixDuration
    configuration    = $Configuration
    repeatCount      = $RepeatCount
    artifactsPath    = $artifactsFullPath
    resultsPath      = $resultsPath
    environment      = $environmentMetadata
    selectedClasses  = $selectedClasses
    totalBenchmarks  = $totalRows
    measuredRows     = $totalMeasuredRows
    naRows           = $totalNaRows
    failures         = $failures
    refusals         = $refusals
    classResults     = $summary
}

$summaryPayload | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryJsonPath -Encoding UTF8

$markdown = @()
$markdown += "# Benchmark Matrix Summary"
$markdown += ""
$markdown += "- Started (UTC): $($summaryPayload.startedUtc)"
$markdown += "- Ended (UTC): $($summaryPayload.endedUtc)"
$markdown += "- Duration: $($summaryPayload.duration)"
$markdown += "- Configuration: $Configuration"
$markdown += "- Repeat count: $RepeatCount"
$markdown += "- Commit SHA: $($environmentMetadata.commitSha)"
$markdown += "- .NET: $($environmentMetadata.dotnetVersion)"
$markdown += "- OS: $($environmentMetadata.osDescription)"
$markdown += "- Server GC: $($environmentMetadata.isServerGc)"
$markdown += "- Runtime profile: $($environmentMetadata.runtimeProfile)"
$markdown += "- Total benchmark rows: $totalRows"
$markdown += "- Measured rows: $totalMeasuredRows"
$markdown += "- NA rows (measured nothing): $totalNaRows"
$markdown += "- Failures: $(if (@($failures).Count -eq 0) { 'none' } else { $failures -join ', ' })"
$markdown += "- Refusals: $(if (@($refusals).Count -eq 0) { 'none' } else { $refusals -join ', ' })"
$markdown += ""
$markdown += "| Class | Run | Verdict | Exit | Measured | NA | Seconds | Report | Reason | Log |"
$markdown += "|-------|-----|---------|------|----------|----|---------|--------|--------|-----|"
foreach ($result in $summary) {
    $reportCell = if ($result.ReportFound) { "yes" } else { "no" }
    $markdown += "| $($result.ClassName) | $($result.Run) | $($result.Verdict) | $($result.ExitCode) | $($result.MeasuredRows) | $($result.NaRows) | $($result.DurationSeconds) | $reportCell | $($result.VerdictReason) | $($result.LogPath) |"
}
$markdown -join "`n" | Set-Content -Path $summaryMdPath -Encoding UTF8

Write-Host ""
Write-Host "Benchmark matrix complete." -ForegroundColor Cyan
Write-Host "Duration: $matrixDuration" -ForegroundColor Cyan
Write-Host "Total benchmark rows: $totalRows (measured: $totalMeasuredRows, NA: $totalNaRows)" -ForegroundColor Cyan
Write-Host "Summary JSON: $summaryJsonPath" -ForegroundColor Cyan
Write-Host "Summary Markdown: $summaryMdPath" -ForegroundColor Cyan

# NOTE: these use [Console]::Error, not Write-Error. Under $ErrorActionPreference = "Stop" a
# Write-Error is a TERMINATING error -- it aborts the script and pwsh exits 1, so the explicit
# exit code below never runs and FAIL, REFUSE and an unrelated crash all report the same 1.
if (@($failures).Count -gt 0) {
    [Console]::Error.WriteLine("Benchmark matrix FAILED for class(es): $($failures -join ', ')")
    exit 2
}

# REFUSE is deliberately a DIFFERENT non-zero exit from FAIL: the caller has to be able to tell
# "this got slower" from "this measured nothing".
if (@($refusals).Count -gt 0) {
    [Console]::Error.WriteLine("Benchmark matrix REFUSED class(es): $($refusals -join ', ').")
    [Console]::Error.WriteLine("They measured nothing usable -- this is neither a pass nor a failure.")
    [Console]::Error.WriteLine("Read the Reason column in $summaryMdPath before treating any number from this matrix as a measurement.")
    exit 3
}

exit 0
