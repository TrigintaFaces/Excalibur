# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineResultsPath,

    [Parameter(Mandatory = $true)]
    [string]$CurrentResultsPath,

    [Parameter(Mandatory = $false)]
    [double]$MaxRegressionPercent = 5.0,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "BenchmarkDotNet.Artifacts/results/mediatr-protected-delta.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ResultsPath {
    param([string]$Path)

    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return (Resolve-Path $Path).Path
    }

    return (Resolve-Path (Join-Path $repoRoot $Path)).Path
}

function Get-LatestCsv {
    param(
        [string]$ResultsDirectory,
        [string]$Pattern
    )

    return Get-ChildItem -Path $ResultsDirectory -Filter $Pattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Normalize-MethodName {
    param([string]$Name)

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return ""
    }

    $normalized = $Name.Trim()
    if ($normalized.StartsWith("'") -and $normalized.EndsWith("'") -and $normalized.Length -ge 2) {
        $normalized = $normalized.Substring(1, $normalized.Length - 2)
    }

    return $normalized.Trim()
}

function Convert-MeanToNs {
    param([string]$RawMean)

    if ([string]::IsNullOrWhiteSpace($RawMean)) {
        return $null
    }

    $mean = $RawMean.Trim()
    if ($mean -eq "NA" -or $mean -eq "?") {
        return $null
    }

    $parts = $mean -split '\s+', 2
    if ($parts.Count -ne 2) {
        throw "Unable to parse mean value '$RawMean'."
    }

    $numericText = $parts[0].Replace(",", "")
    $unit = $parts[1].Trim()

    $numeric = 0.0
    if (-not [double]::TryParse($numericText, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$numeric)) {
        throw "Unable to parse numeric mean '$RawMean'."
    }

    switch ($unit) {
        "ns" { return $numeric }
        "us" { return $numeric * 1000.0 }
        "μs" { return $numeric * 1000.0 }
        "µs" { return $numeric * 1000.0 }
        "ms" { return $numeric * 1000000.0 }
        "s"  { return $numeric * 1000000000.0 }
        default { throw "Unsupported mean unit '$unit' in '$RawMean'." }
    }
}

function Get-MeanNsByMethod {
    param(
        [object[]]$Rows,
        [string]$MethodName
    )

    $row = $Rows | Where-Object { (Normalize-MethodName $_.Method) -eq $MethodName } | Select-Object -First 1
    if ($null -eq $row) {
        return $null
    }

    return Convert-MeanToNs $row.Mean
}

$baselineFullPath = Resolve-ResultsPath $BaselineResultsPath
$currentFullPath = Resolve-ResultsPath $CurrentResultsPath

if (-not (Test-Path $baselineFullPath)) {
    throw "Baseline results path not found: $baselineFullPath"
}

if (-not (Test-Path $currentFullPath)) {
    throw "Current results path not found: $currentFullPath"
}

$baselineCsv = Get-LatestCsv -ResultsDirectory $baselineFullPath -Pattern "*MediatRComparisonBenchmarks-report.csv"
$currentCsv = Get-LatestCsv -ResultsDirectory $currentFullPath -Pattern "*MediatRComparisonBenchmarks-report.csv"

if ($null -eq $baselineCsv) {
    throw "Baseline MediatR comparison CSV not found under $baselineFullPath"
}

if ($null -eq $currentCsv) {
    throw "Current MediatR comparison CSV not found under $currentFullPath"
}

$baselineRows = Import-Csv -Path $baselineCsv.FullName
$currentRows = Import-Csv -Path $currentCsv.FullName

$protectedRows = @(
    "Dispatch: Single command handler",
    "Dispatch: Query with return value",
    "Dispatch: Notification to 3 handlers",
    "Dispatch: 10 concurrent commands",
    "Dispatch: 100 concurrent commands"
)

$results = @()
$regressions = @()

foreach ($methodName in $protectedRows) {
    $baselineNs = Get-MeanNsByMethod -Rows $baselineRows -MethodName $methodName
    $currentNs = Get-MeanNsByMethod -Rows $currentRows -MethodName $methodName

    if ($null -eq $baselineNs) {
        throw "Baseline row '$methodName' was not found in $($baselineCsv.FullName)"
    }

    if ($null -eq $currentNs) {
        throw "Current row '$methodName' was not found in $($currentCsv.FullName)"
    }

    $deltaPercent = (($currentNs - $baselineNs) / $baselineNs) * 100.0
    $status = if ($deltaPercent -gt $MaxRegressionPercent) { "FAIL" } elseif ($deltaPercent -gt 0.0) { "REGRESSION" } else { "OK" }

    $row = [pscustomobject]@{
        Method       = $methodName
        BaselineNs   = [math]::Round($baselineNs, 2)
        CurrentNs    = [math]::Round($currentNs, 2)
        DeltaPercent = [math]::Round($deltaPercent, 2)
        Status       = $status
    }

    $results += $row

    if ($deltaPercent -gt $MaxRegressionPercent) {
        $regressions += $row
    }
}

$outputDirectory = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$lines = @()
$lines += "# MediatR Protected-Row Delta Report"
$lines += ""
$lines += "- Baseline: ``$($baselineCsv.FullName)``"
$lines += "- Current: ``$($currentCsv.FullName)``"
$lines += "- Max regression threshold: $([math]::Round($MaxRegressionPercent, 2))%"
$lines += ""
$lines += "| Method | Baseline (ns) | Current (ns) | Delta % | Status |"
$lines += "|--------|---------------|--------------|---------|--------|"
foreach ($result in $results) {
    $lines += "| $($result.Method) | $($result.BaselineNs) | $($result.CurrentNs) | $($result.DeltaPercent) | $($result.Status) |"
}

if ($regressions.Count -gt 0) {
    $lines += ""
    $lines += "## Result"
    $lines += ""
    $lines += "Regression threshold exceeded for $($regressions.Count) protected row(s)."
}
else {
    $lines += ""
    $lines += "## Result"
    $lines += ""
    $lines += "No protected-row regression exceeded threshold."
}

$lines -join "`n" | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host "Protected-row delta report written: $OutputPath" -ForegroundColor Cyan
foreach ($result in $results) {
    Write-Host ("{0}: baseline={1:N2} ns current={2:N2} ns delta={3:N2}% status={4}" -f $result.Method, $result.BaselineNs, $result.CurrentNs, $result.DeltaPercent, $result.Status)
}

if ($regressions.Count -gt 0) {
    Write-Host ("Protected-row regression threshold exceeded (>{0:N2}%)." -f $MaxRegressionPercent) -ForegroundColor Red
    exit 2
}

exit 0
