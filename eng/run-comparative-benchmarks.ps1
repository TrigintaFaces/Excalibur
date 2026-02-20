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
    [switch]$CiSmoke,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseFrameworkLogs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "benchmarks/Excalibur.Dispatch.Benchmarks"
$artifactsFullPath = Join-Path $repoRoot $ArtifactsPath
$resultsPath = Join-Path $artifactsFullPath "results"

if (-not (Test-Path $projectPath)) {
    throw "Benchmark project path not found: $projectPath"
}

if (-not (Test-Path $artifactsFullPath)) {
    New-Item -ItemType Directory -Force -Path $artifactsFullPath | Out-Null
}

$filters = if ($CiSmoke) {
    @(
        "*MediatRComparisonBenchmarks.Dispatch_SingleCommand*",
        "*WolverineInProcessComparisonBenchmarks.Dispatch_SingleCommand*",
        "*MassTransitMediatorComparisonBenchmarks.Dispatch_SingleCommand*",
        "*TransportQueueParityComparisonBenchmarks.Dispatch_QueuedCommand_EndToEnd*",
        "*WolverineComparisonBenchmarks.Dispatch_SingleCommand*",
        "*MassTransitComparisonBenchmarks.Dispatch_SingleCommand*"
    )
}
else {
    @(
        "*MediatRComparisonBenchmarks*",
        "*WolverineInProcessComparisonBenchmarks*",
        "*MassTransitMediatorComparisonBenchmarks*",
        "*TransportQueueParityComparisonBenchmarks*",
        "*WolverineComparisonBenchmarks*",
        "*MassTransitComparisonBenchmarks*"
    )
}

$exporters = @("csv", "markdown", "html", "json")

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
    "--filter"
)
$arguments += $filters

$arguments += @(
    "--exporters"
)
$arguments += $exporters

$arguments += @("--artifacts", $artifactsFullPath)

Write-Host "Running comparative benchmarks from repository root: $repoRoot" -ForegroundColor Cyan
Write-Host "Benchmark project: $projectPath" -ForegroundColor Cyan
Write-Host "Artifacts path: $artifactsFullPath" -ForegroundColor Cyan
Write-Host "Filters: $($filters -join ', ')" -ForegroundColor Cyan
Write-Host "CI smoke mode: $CiSmoke" -ForegroundColor Cyan
Write-Host "Quiet framework logs: $(-not $VerboseFrameworkLogs)" -ForegroundColor Cyan

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

Push-Location $repoRoot
try {
    & dotnet @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "dotnet benchmark command failed with exit code $exitCode"
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

$expectedReports = @(
    "Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.WolverineComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.MassTransitComparisonBenchmarks-report-github.md"
)

$missingReports = @()
foreach ($report in $expectedReports) {
    $reportPath = Join-Path $resultsPath $report
    if (-not (Test-Path $reportPath)) {
        $missingReports += $reportPath
    }
}

if ($missingReports.Count -gt 0) {
    Write-Error "Comparative benchmark run completed, but expected reports are missing:"
    foreach ($missing in $missingReports) {
        Write-Host "  - $missing" -ForegroundColor Red
    }
    exit 2
}

Write-Host ""
Write-Host "Comparative benchmark run completed successfully." -ForegroundColor Green
Write-Host "Result artifacts:" -ForegroundColor Green
Write-Host "  - $resultsPath" -ForegroundColor Green
foreach ($report in $expectedReports) {
    Write-Host "  - $(Join-Path $resultsPath $report)" -ForegroundColor Green
}
