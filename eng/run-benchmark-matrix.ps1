# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [string]$ArtifactsPath = "BenchmarkDotNet.Artifacts",

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
    [string[]]$Classes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

Write-Host "Benchmark matrix root: $repoRoot" -ForegroundColor Cyan
Write-Host "Benchmark project: $projectPath" -ForegroundColor Cyan
Write-Host "Artifacts path: $artifactsFullPath" -ForegroundColor Cyan
Write-Host "Selected classes ($(@($selectedClasses).Count)): $($selectedClasses -join ', ')" -ForegroundColor Cyan
Write-Host "Quiet framework logs: $(-not $VerboseFrameworkLogs)" -ForegroundColor Cyan

Push-Location $repoRoot
try {
    foreach ($className in $selectedClasses) {
        $classStart = Get-Date
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $logFile = Join-Path $resultsPath ("run-{0}-{1}.log" -f $className, $timestamp)

        Write-Host ""
        Write-Host "=== $className ===" -ForegroundColor Yellow

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

        $githubReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report-github.md" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        $csvReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report.csv" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        $rowCount = 0
        if ($csvReport) {
            $rowCount = @(Import-Csv $csvReport.FullName).Count
        }

        $reportFound = $null -ne $githubReport
        $reportPath = if ($githubReport) { $githubReport.FullName } else { "" }

        $summary += [pscustomobject]@{
            ClassName       = $className
            ExitCode        = $exitCode
            DurationSeconds = [math]::Round($classDuration.TotalSeconds, 1)
            BenchmarkRows   = $rowCount
            ReportFound     = $reportFound
            ReportPath      = $reportPath
            LogPath         = $logFile
        }

        if ($exitCode -ne 0 -or -not $reportFound) {
            $failures += $className
            Write-Host "FAILED $className (exit=$exitCode, reportFound=$reportFound)" -ForegroundColor Red
            Write-Host "Log: $logFile" -ForegroundColor Red
            if (-not $ContinueOnError) {
                break
            }
        }
        else {
            Write-Host "OK $className ($rowCount rows, $([math]::Round($classDuration.TotalSeconds, 1))s)" -ForegroundColor Green
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
}

$matrixEnd = Get-Date
$matrixDuration = $matrixEnd - $matrixStart
$totalRows = ($summary | Measure-Object -Property BenchmarkRows -Sum).Sum
if ($null -eq $totalRows) {
    $totalRows = 0
}

$summaryTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryJsonPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.json" -f $summaryTimestamp)
$summaryMdPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.md" -f $summaryTimestamp)

$summaryPayload = [pscustomobject]@{
    startedUtc       = $matrixStartUtc.ToString("O")
    endedUtc         = [DateTimeOffset]::UtcNow.ToString("O")
    duration         = [string]$matrixDuration
    configuration    = $Configuration
    artifactsPath    = $artifactsFullPath
    resultsPath      = $resultsPath
    selectedClasses  = $selectedClasses
    totalBenchmarks  = $totalRows
    failures         = $failures
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
$markdown += "- Total benchmark rows: $totalRows"
$markdown += "- Failures: $(if (@($failures).Count -eq 0) { 'none' } else { $failures -join ', ' })"
$markdown += ""
$markdown += "| Class | Exit | Rows | Seconds | Report | Log |"
$markdown += "|-------|------|------|---------|--------|-----|"
foreach ($result in $summary) {
    $reportCell = if ($result.ReportFound) { "yes" } else { "no" }
    $markdown += "| $($result.ClassName) | $($result.ExitCode) | $($result.BenchmarkRows) | $($result.DurationSeconds) | $reportCell | $($result.LogPath) |"
}
$markdown -join "`n" | Set-Content -Path $summaryMdPath -Encoding UTF8

Write-Host ""
Write-Host "Benchmark matrix complete." -ForegroundColor Cyan
Write-Host "Duration: $matrixDuration" -ForegroundColor Cyan
Write-Host "Total benchmark rows: $totalRows" -ForegroundColor Cyan
Write-Host "Summary JSON: $summaryJsonPath" -ForegroundColor Cyan
Write-Host "Summary Markdown: $summaryMdPath" -ForegroundColor Cyan

if (@($failures).Count -gt 0) {
    Write-Error "Benchmark matrix failed for class(es): $($failures -join ', ')"
    exit 2
}

exit 0
