# Copyright (c) 2026 The Excalibur Project
# Performance Regression Detection Script
#
# This script compares current benchmark results against baseline metrics
# and enforces regression gates to prevent performance degradation.
#
# Regression Thresholds:
# - P99 (99th percentile): >10% degradation = FAIL
# - P95 (95th percentile): >20% degradation = FAIL
# - Memory (Gen0/Gen1/Gen2 collections): >50% increase = FAIL
# - Median (P50): >15% degradation = WARN (informational)
#
# Exit Codes:
# 0 = No regressions detected
# 1 = Regressions detected (fails CI)
# 2 = Script error or missing baselines

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter(Mandatory = $true)]
    [string]$CurrentPath,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "benchmark-regression-report.md",

    [Parameter(Mandatory = $false)]
    [switch]$FailOnWarnings
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Excalibur - Performance Regression Detection" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Helper Functions
# ============================================================================

function Get-BenchmarkResults {
    param([string]$Path)

    Write-Host "Loading benchmark results from: $Path" -ForegroundColor Gray

    if (-not (Test-Path $Path)) {
        throw "Benchmark results path not found: $Path"
    }

    # Find JSON result files
    $jsonFiles = Get-ChildItem -Path $Path -Filter "*.json" -Recurse | Where-Object { $_.Name -notmatch "params|validation" }

    if ($jsonFiles.Count -eq 0) {
        throw "No JSON benchmark result files found in: $Path"
    }

    Write-Host "  Found $($jsonFiles.Count) JSON result file(s)" -ForegroundColor Gray

    $results = @{}

    foreach ($file in $jsonFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw | ConvertFrom-Json

            if ($content.Benchmarks) {
                foreach ($benchmark in $content.Benchmarks) {
                    $fullName = "$($benchmark.Type).$($benchmark.Method)"

                    $results[$fullName] = @{
                        Mean = $benchmark.Statistics.Mean
                        Median = $benchmark.Statistics.Median
                        P95 = $benchmark.Statistics.Percentiles.P95
                        P99 = $benchmark.Statistics.Percentiles.P99
                        P100 = $benchmark.Statistics.Max
                        StdDev = $benchmark.Statistics.StandardDeviation
                        Gen0 = if ($benchmark.Memory.Gen0Collections) { [int]$benchmark.Memory.Gen0Collections } else { 0 }
                        Gen1 = if ($benchmark.Memory.Gen1Collections) { [int]$benchmark.Memory.Gen1Collections } else { 0 }
                        Gen2 = if ($benchmark.Memory.Gen2Collections) { [int]$benchmark.Memory.Gen2Collections } else { 0 }
                        BytesAllocated = if ($benchmark.Memory.BytesAllocatedPerOperation) { [long]$benchmark.Memory.BytesAllocatedPerOperation } else { 0 }
                    }
                }
            }
        }
        catch {
            Write-Warning "Failed to parse JSON file: $($file.FullName)"
            Write-Warning "  Error: $_"
        }
    }

    if ($results.Count -eq 0) {
        throw "No valid benchmark results found in JSON files"
    }

    Write-Host "  Loaded $($results.Count) benchmark result(s)" -ForegroundColor Green
    return $results
}

function Compare-Metrics {
    param(
        [double]$Baseline,
        [double]$Current,
        [double]$Threshold,
        [string]$MetricName
    )

    if ($Baseline -eq 0) {
        return @{
            PercentChange = 0
            IsRegression = $false
            Severity = "OK"
            Message = "Baseline is zero - skipping comparison"
        }
    }

    $percentChange = (($Current - $Baseline) / $Baseline) * 100
    $isRegression = $percentChange -gt $Threshold

    $severity = if ($isRegression) { "FAIL" } elseif ($percentChange -gt ($Threshold * 0.8)) { "WARN" } else { "OK" }

    return @{
        PercentChange = $percentChange
        IsRegression = $isRegression
        Severity = $severity
        Message = if ($isRegression) { "Regression detected: +$([math]::Round($percentChange, 2))% (threshold: +$Threshold%)" } else { "+$([math]::Round($percentChange, 2))%" }
    }
}

# ============================================================================
# Load Baseline and Current Results
# ============================================================================

try {
    $baselineResults = Get-BenchmarkResults -Path $BaselinePath
}
catch {
    Write-Host ""
    Write-Host "ERROR: Failed to load baseline results" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    exit 2
}

try {
    $currentResults = Get-BenchmarkResults -Path $CurrentPath
}
catch {
    Write-Host ""
    Write-Host "ERROR: Failed to load current results" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    exit 2
}

Write-Host ""
Write-Host "Baseline benchmarks: $($baselineResults.Count)" -ForegroundColor Cyan
Write-Host "Current benchmarks:  $($currentResults.Count)" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Compare Results and Detect Regressions
# ============================================================================

$regressions = @()
$warnings = @()
$improvements = @()
$unchanged = @()
$missingBaselines = @()

foreach ($benchmarkName in $currentResults.Keys) {
    if (-not $baselineResults.ContainsKey($benchmarkName)) {
        $missingBaselines += $benchmarkName
        continue
    }

    $baseline = $baselineResults[$benchmarkName]
    $current = $currentResults[$benchmarkName]

    # P99 Regression Check (>10%)
    $p99Result = Compare-Metrics -Baseline $baseline.P99 -Current $current.P99 -Threshold 10 -MetricName "P99"

    # P95 Regression Check (>20%)
    $p95Result = Compare-Metrics -Baseline $baseline.P95 -Current $current.P95 -Threshold 20 -MetricName "P95"

    # P50/Median Check (>15% = WARN)
    $medianResult = Compare-Metrics -Baseline $baseline.Median -Current $current.Median -Threshold 15 -MetricName "Median"

    # Memory Regression Check (>50%)
    $totalBaselineGC = $baseline.Gen0 + $baseline.Gen1 + $baseline.Gen2
    $totalCurrentGC = $current.Gen0 + $current.Gen1 + $current.Gen2
    $memoryResult = Compare-Metrics -Baseline $totalBaselineGC -Current $totalCurrentGC -Threshold 50 -MetricName "GC"

    # Aggregate results
    $hasRegression = $p99Result.IsRegression -or $p95Result.IsRegression -or $memoryResult.IsRegression
    $hasWarning = $medianResult.IsRegression

    $result = @{
        BenchmarkName = $benchmarkName
        P99 = $p99Result
        P95 = $p95Result
        Median = $medianResult
        Memory = $memoryResult
        BaselineP99 = $baseline.P99
        CurrentP99 = $current.P99
        BaselineP95 = $baseline.P95
        CurrentP95 = $current.P95
        BaselineMedian = $baseline.Median
        CurrentMedian = $current.Median
        BaselineGC = $totalBaselineGC
        CurrentGC = $totalCurrentGC
    }

    if ($hasRegression) {
        $regressions += $result
    }
    elseif ($hasWarning) {
        $warnings += $result
    }
    elseif ($p99Result.PercentChange -lt -5 -or $p95Result.PercentChange -lt -5) {
        $improvements += $result
    }
    else {
        $unchanged += $result
    }
}

# ============================================================================
# Generate Report
# ============================================================================

$reportContent = @"
# Performance Regression Report
**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

**Baseline:** ``$BaselinePath``
**Current:**  ``$CurrentPath``

## Summary

| Metric | Count |
|--------|-------|
| Total Benchmarks | $($currentResults.Count) |
| ✅ No Regression | $($unchanged.Count) |
| ⚠️ Warnings | $($warnings.Count) |
| ❌ **Regressions** | **$($regressions.Count)** |
| 🚀 Improvements | $($improvements.Count) |
| ⚠️ Missing Baselines | $($missingBaselines.Count) |

## Regression Thresholds

- **P99 (99th percentile):** >10% degradation = FAIL
- **P95 (95th percentile):** >20% degradation = FAIL
- **Memory (GC):** >50% increase = FAIL
- **Median (P50):** >15% degradation = WARN

"@

if ($regressions.Count -gt 0) {
    $reportContent += @"

## ❌ Regressions Detected

The following benchmarks have performance regressions exceeding the configured thresholds:

| Benchmark | P99 Change | P95 Change | Memory Change | Status |
|-----------|------------|------------|---------------|--------|
"@

    foreach ($reg in $regressions) {
        $p99Status = if ($reg.P99.IsRegression) { "❌ $($reg.P99.Message)" } else { "✅ $($reg.P99.Message)" }
        $p95Status = if ($reg.P95.IsRegression) { "❌ $($reg.P95.Message)" } else { "✅ $($reg.P95.Message)" }
        $memStatus = if ($reg.Memory.IsRegression) { "❌ $($reg.Memory.Message)" } else { "✅ $($reg.Memory.Message)" }

        $reportContent += @"

| ``$($reg.BenchmarkName)`` | $p99Status | $p95Status | $memStatus | **FAIL** |
"@
    }
}

if ($warnings.Count -gt 0) {
    $reportContent += @"


## ⚠️ Warnings

The following benchmarks show median degradation but are within acceptable thresholds:

| Benchmark | Median Change | P99 Change | P95 Change |
|-----------|---------------|------------|------------|
"@

    foreach ($warn in $warnings) {
        $reportContent += @"

| ``$($warn.BenchmarkName)`` | $($warn.Median.Message) | $($warn.P99.Message) | $($warn.P95.Message) |
"@
    }
}

if ($improvements.Count -gt 0) {
    $reportContent += @"


## 🚀 Performance Improvements

The following benchmarks show measurable performance improvements:

| Benchmark | P99 Change | P95 Change | Median Change |
|-----------|------------|------------|---------------|
"@

    foreach ($imp in $improvements) {
        $reportContent += @"

| ``$($imp.BenchmarkName)`` | $($imp.P99.Message) | $($imp.P95.Message) | $($imp.Median.Message) |
"@
    }
}

if ($missingBaselines.Count -gt 0) {
    $reportContent += @"


## ⚠️ New Benchmarks (No Baseline)

The following benchmarks have no baseline for comparison:

"@

    foreach ($missing in $missingBaselines) {
        $reportContent += "- ``$missing``$([Environment]::NewLine)"
    }
}

$reportContent += @"


## Exit Code

"@

# ============================================================================
# Determine Exit Code and Write Results
# ============================================================================

$exitCode = 0

if ($regressions.Count -gt 0) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - Regressions detected (fails CI)$([Environment]::NewLine)"
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  ❌ REGRESSIONS DETECTED: $($regressions.Count)" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
}
elseif ($warnings.Count -gt 0 -and $FailOnWarnings) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - Warnings present and -FailOnWarnings specified$([Environment]::NewLine)"
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  ⚠️ WARNINGS DETECTED (treated as failures): $($warnings.Count)" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
}
else {
    $reportContent += "**EXIT 0** - No regressions detected ✅$([Environment]::NewLine)"
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ✅ NO REGRESSIONS DETECTED" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
}

# Save report
$reportContent | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host ""
Write-Host "Report saved to: $OutputPath" -ForegroundColor Cyan

# Display summary
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total Benchmarks: $($currentResults.Count)" -ForegroundColor Gray
Write-Host "  No Regression: $($unchanged.Count)" -ForegroundColor Green
Write-Host "  Warnings: $($warnings.Count)" -ForegroundColor Yellow
Write-Host "  Regressions: $($regressions.Count)" -ForegroundColor $(if ($regressions.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  Improvements: $($improvements.Count)" -ForegroundColor Cyan
Write-Host ""

exit $exitCode
