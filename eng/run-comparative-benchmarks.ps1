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
    [switch]$CiSmoke,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseFrameworkLogs,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfile,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfilesPath = "eng/runtime-profiles.json",

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 100)]
    [double]$VarianceThreshold = 10.0,

    [Parameter(Mandatory = $false)]
    [switch]$SkipVarianceDetection
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

    return [hashtable]$profiles[$ProfileName]
}

function Get-BenchmarkVarianceReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResultsDirectory,

        [Parameter(Mandatory = $true)]
        [double]$CvThresholdPercent
    )

    $jsonFiles = Get-ChildItem -Path $ResultsDirectory -Filter "*-report.json" -ErrorAction SilentlyContinue
    if ($jsonFiles.Count -eq 0) {
        Write-Warning "No JSON report files found in $ResultsDirectory for variance analysis."
        return $null
    }

    $allBenchmarks = @()

    foreach ($jsonFile in $jsonFiles) {
        $reportData = Get-Content -Path $jsonFile.FullName -Raw | ConvertFrom-Json
        if (-not $reportData.Benchmarks) {
            continue
        }

        foreach ($benchmark in $reportData.Benchmarks) {
            $stats = $benchmark.Statistics
            if (-not $stats -or -not $stats.Mean -or $stats.Mean -eq 0) {
                continue
            }

            $mean = $stats.Mean
            $stdDev = $stats.StandardDeviation
            $cv = ($stdDev / [Math]::Abs($mean)) * 100.0

            $allBenchmarks += [PSCustomObject]@{
                Source          = $jsonFile.Name
                Method          = $benchmark.Method
                Parameters      = if ($benchmark.Parameters) { $benchmark.Parameters } else { "" }
                MeanNs          = [Math]::Round($mean, 2)
                StdDevNs        = [Math]::Round($stdDev, 2)
                CvPercent       = [Math]::Round($cv, 2)
                ExceedsThreshold = $cv -gt $CvThresholdPercent
                Iterations      = if ($stats.N) { $stats.N } else { 0 }
            }
        }
    }

    return $allBenchmarks
}

function Write-VarianceSummary {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        $Benchmarks,

        [Parameter(Mandatory = $true)]
        [double]$CvThresholdPercent,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    if (-not $Benchmarks -or $Benchmarks.Count -eq 0) {
        Write-Host "No benchmark statistics available for variance analysis." -ForegroundColor Yellow
        return
    }

    $flagged = @($Benchmarks | Where-Object { $_.ExceedsThreshold })
    $clean = @($Benchmarks | Where-Object { -not $_.ExceedsThreshold })

    $lines = @()
    $lines += "# Benchmark Variance Report"
    $lines += ""
    $lines += "- **Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $lines += "- **CV Threshold:** $CvThresholdPercent%"
    $lines += "- **Total benchmarks analyzed:** $($Benchmarks.Count)"
    $lines += "- **High-variance (flagged):** $($flagged.Count)"
    $lines += "- **Stable:** $($clean.Count)"
    $lines += ""

    if ($flagged.Count -gt 0) {
        $lines += "## High-Variance Benchmarks (CV > $CvThresholdPercent%)"
        $lines += ""
        $lines += "| Method | Parameters | Mean (ns) | StdDev (ns) | CV (%) | Iterations | Source |"
        $lines += "|--------|-----------|-----------|-------------|--------|------------|--------|"
        foreach ($b in ($flagged | Sort-Object CvPercent -Descending)) {
            $lines += "| $($b.Method) | $($b.Parameters) | $($b.MeanNs) | $($b.StdDevNs) | **$($b.CvPercent)** | $($b.Iterations) | $($b.Source -replace '-report\.json$','') |"
        }
        $lines += ""
    }

    $lines += "## All Benchmarks"
    $lines += ""
    $lines += "| Method | Parameters | Mean (ns) | StdDev (ns) | CV (%) | Status |"
    $lines += "|--------|-----------|-----------|-------------|--------|--------|"
    foreach ($b in ($Benchmarks | Sort-Object CvPercent -Descending)) {
        $status = if ($b.ExceedsThreshold) { "**HIGH**" } else { "OK" }
        $lines += "| $($b.Method) | $($b.Parameters) | $($b.MeanNs) | $($b.StdDevNs) | $($b.CvPercent) | $status |"
    }
    $lines += ""

    $content = $lines -join "`n"
    Set-Content -Path $OutputPath -Value $content -Encoding UTF8

    # Console summary
    Write-Host ""
    Write-Host "Variance Analysis (CV threshold: $CvThresholdPercent%)" -ForegroundColor Cyan
    Write-Host "  Total benchmarks: $($Benchmarks.Count)" -ForegroundColor Cyan
    Write-Host "  Stable: $($clean.Count)" -ForegroundColor Green

    if ($flagged.Count -gt 0) {
        Write-Host "  High-variance: $($flagged.Count)" -ForegroundColor Yellow
        foreach ($b in ($flagged | Sort-Object CvPercent -Descending)) {
            Write-Host "    - $($b.Method) $(if ($b.Parameters) { "($($b.Parameters))" }) CV=$($b.CvPercent)%" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  High-variance: 0 (all benchmarks are stable)" -ForegroundColor Green
    }

    Write-Host "  Report: $OutputPath" -ForegroundColor Cyan
}

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

$filters = if ($CiSmoke) {
    @(
        "*MediatRComparisonBenchmarks.Dispatch_SingleCommand*",
        "*WolverineInProcessComparisonBenchmarks.Dispatch_SingleCommand*",
        "*MassTransitMediatorComparisonBenchmarks.Dispatch_SingleCommand*",
        "*TransportQueueParityComparisonBenchmarks.Dispatch_QueuedCommand_EndToEnd*",
        "*WolverineComparisonBenchmarks.Dispatch_SingleCommand*",
        "*MassTransitComparisonBenchmarks.Dispatch_SingleCommand*",
        "*PipelineComparisonBenchmarks.Dispatch_WithPipelineBehaviors*"
    )
}
else {
    @(
        "*MediatRComparisonBenchmarks*",
        "*WolverineInProcessComparisonBenchmarks*",
        "*MassTransitMediatorComparisonBenchmarks*",
        "*TransportQueueParityComparisonBenchmarks*",
        "*WolverineComparisonBenchmarks*",
        "*MassTransitComparisonBenchmarks*",
        "*PipelineComparisonBenchmarks*"
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
Write-Host "Runtime profile: $(if ([string]::IsNullOrWhiteSpace($RuntimeProfile)) { 'none' } else { $RuntimeProfile })" -ForegroundColor Cyan
Write-Host "Variance detection: $(if ($SkipVarianceDetection) { 'disabled' } else { "enabled (CV threshold: $VarianceThreshold%)" })" -ForegroundColor Cyan
if ($runtimeProfileVariables.Count -gt 0) {
    Write-Host "Runtime profile variables: $(($runtimeProfileVariables.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join ', ')" -ForegroundColor Cyan
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

    foreach ($envVar in $runtimeProfileVariables.Keys) {
        [Environment]::SetEnvironmentVariable($envVar, $runtimeProfileOriginalEnv[$envVar])
    }
}

$expectedReports = @(
    "Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.WolverineComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.MassTransitComparisonBenchmarks-report-github.md",
    "Excalibur.Dispatch.Benchmarks.Comparative.PipelineComparisonBenchmarks-report-github.md"
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

# Variance detection
if (-not $SkipVarianceDetection) {
    $benchmarkResults = Get-BenchmarkVarianceReport -ResultsDirectory $resultsPath -CvThresholdPercent $VarianceThreshold
    $varianceReportPath = Join-Path $resultsPath "variance-report.md"
    Write-VarianceSummary -Benchmarks $benchmarkResults -CvThresholdPercent $VarianceThreshold -OutputPath $varianceReportPath
}
