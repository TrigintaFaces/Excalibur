# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("MediatRLocalParity", "TransportComparison")]
    [string]$Gate,

    [Parameter(Mandatory = $false)]
    [string]$ResultsPath = "BenchmarkDotNet.Artifacts/results",

    [Parameter(Mandatory = $false)]
    [double]$MediatRSingleCommandMaxRatio = 1.00,

    [Parameter(Mandatory = $false)]
    [double]$MediatRQueryMaxRatio = 1.00,

    [Parameter(Mandatory = $false)]
    [double]$TransportSingleCommandMinAdvantageRatio = 1.00,

    [Parameter(Mandatory = $false)]
    [double]$TransportConcurrent10MinAdvantageRatio = 1.00
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

function Get-LatestCsv {
    param(
        [string]$ResultsDirectory,
        [string]$Pattern
    )

    return Get-ChildItem -Path $ResultsDirectory -Filter $Pattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Find-MeanByMethod {
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

function Find-MeanByPrefix {
    param(
        [object[]]$Rows,
        [string]$Prefix
    )

    $row = $Rows |
        Where-Object { (Normalize-MethodName $_.Method).StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1

    if ($null -eq $row) {
        return $null
    }

    return Convert-MeanToNs $row.Mean
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resultsFullPath = if ([System.IO.Path]::IsPathRooted($ResultsPath)) {
    $ResultsPath
}
else {
    Join-Path $repoRoot $ResultsPath
}

if (-not (Test-Path $resultsFullPath)) {
    throw "Results path not found: $resultsFullPath"
}

Write-Host "Validating performance gate: $Gate" -ForegroundColor Cyan
Write-Host "Results path: $resultsFullPath" -ForegroundColor Cyan

$failures = @()

if ($Gate -eq "MediatRLocalParity") {
    $csv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*MediatRComparisonBenchmarks-report.csv"
    if ($null -eq $csv) {
        throw "MediatR comparison report CSV not found under $resultsFullPath"
    }

    $rows = Import-Csv -Path $csv.FullName

    $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Single command strict direct-local"
    if ($null -eq $dispatchSingle) {
        $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Single command handler"
    }
    $mediatrSingle = Find-MeanByMethod -Rows $rows -MethodName "MediatR: Single command handler"

    $dispatchQuery = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Query with return value"
    $mediatrQuery = Find-MeanByMethod -Rows $rows -MethodName "MediatR: Query with return value"

    if ($null -eq $dispatchSingle -or $null -eq $mediatrSingle) {
        throw "Required single-command rows were not found in $($csv.FullName)"
    }
    if ($null -eq $dispatchQuery -or $null -eq $mediatrQuery) {
        throw "Required query rows were not found in $($csv.FullName)"
    }

    $singleRatio = $dispatchSingle / $mediatrSingle
    $queryRatio = $dispatchQuery / $mediatrQuery

    Write-Host ("MediatR parity (single command): Dispatch={0:N2} ns, MediatR={1:N2} ns, ratio={2:N3}, max={3:N3}" -f $dispatchSingle, $mediatrSingle, $singleRatio, $MediatRSingleCommandMaxRatio) -ForegroundColor Yellow
    Write-Host ("MediatR parity (query): Dispatch={0:N2} ns, MediatR={1:N2} ns, ratio={2:N3}, max={3:N3}" -f $dispatchQuery, $mediatrQuery, $queryRatio, $MediatRQueryMaxRatio) -ForegroundColor Yellow

    if ($singleRatio -gt $MediatRSingleCommandMaxRatio) {
        $failures += "Single command ratio $([math]::Round($singleRatio, 3)) exceeds max $MediatRSingleCommandMaxRatio"
    }

    if ($queryRatio -gt $MediatRQueryMaxRatio) {
        $failures += "Query ratio $([math]::Round($queryRatio, 3)) exceeds max $MediatRQueryMaxRatio"
    }
}
elseif ($Gate -eq "TransportComparison") {
    $parityCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*TransportQueueParityComparisonBenchmarks-report.csv"

    if ($null -eq $parityCsv) {
        throw "Transport queue parity comparison report CSV not found under $resultsFullPath"
    }

    $rows = Import-Csv -Path $parityCsv.FullName

    $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch (remote): queued command end-to-end"
    $wolverineSingle = Find-MeanByMethod -Rows $rows -MethodName "Wolverine: queued command end-to-end (SendAsync)"
    $massTransitSingle = Find-MeanByMethod -Rows $rows -MethodName "MassTransit: queued command end-to-end (Publish)"

    $dispatchConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "Dispatch (remote): queued commands end-to-end (10 concurrent)"
    $wolverineConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "Wolverine: queued commands end-to-end (10 concurrent)"
    $massTransitConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "MassTransit: queued commands end-to-end (10 concurrent)"

    if ($null -eq $dispatchSingle -or $null -eq $wolverineSingle -or $null -eq $massTransitSingle) {
        throw "Required single-command rows were not found in $($parityCsv.FullName)"
    }
    if ($null -eq $dispatchConcurrent10 -or $null -eq $wolverineConcurrent10 -or $null -eq $massTransitConcurrent10) {
        throw "Required concurrent(10) rows were not found in $($parityCsv.FullName)"
    }

    $wolverineSingleAdvantage = $wolverineSingle / $dispatchSingle
    $massTransitSingleAdvantage = $massTransitSingle / $dispatchSingle
    $wolverineConcurrent10Advantage = $wolverineConcurrent10 / $dispatchConcurrent10
    $massTransitConcurrent10Advantage = $massTransitConcurrent10 / $dispatchConcurrent10

    Write-Host ("Transport comparison (Wolverine single command): advantage={0:N3}x, min={1:N3}x" -f $wolverineSingleAdvantage, $TransportSingleCommandMinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (MassTransit single command): advantage={0:N3}x, min={1:N3}x" -f $massTransitSingleAdvantage, $TransportSingleCommandMinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (Wolverine concurrent 10): advantage={0:N3}x, min={1:N3}x" -f $wolverineConcurrent10Advantage, $TransportConcurrent10MinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (MassTransit concurrent 10): advantage={0:N3}x, min={1:N3}x" -f $massTransitConcurrent10Advantage, $TransportConcurrent10MinAdvantageRatio) -ForegroundColor Yellow

    if ($wolverineSingleAdvantage -lt $TransportSingleCommandMinAdvantageRatio) {
        $failures += "Wolverine single-command advantage $([math]::Round($wolverineSingleAdvantage, 3))x is below minimum $TransportSingleCommandMinAdvantageRatio x"
    }

    if ($massTransitSingleAdvantage -lt $TransportSingleCommandMinAdvantageRatio) {
        $failures += "MassTransit single-command advantage $([math]::Round($massTransitSingleAdvantage, 3))x is below minimum $TransportSingleCommandMinAdvantageRatio x"
    }

    if ($wolverineConcurrent10Advantage -lt $TransportConcurrent10MinAdvantageRatio) {
        $failures += "Wolverine concurrent(10) advantage $([math]::Round($wolverineConcurrent10Advantage, 3))x is below minimum $TransportConcurrent10MinAdvantageRatio x"
    }

    if ($massTransitConcurrent10Advantage -lt $TransportConcurrent10MinAdvantageRatio) {
        $failures += "MassTransit concurrent(10) advantage $([math]::Round($massTransitConcurrent10Advantage, 3))x is below minimum $TransportConcurrent10MinAdvantageRatio x"
    }
}

if (@($failures).Count -gt 0) {
    Write-Error "Performance gate '$Gate' failed."
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 2
}

Write-Host "Performance gate '$Gate' passed." -ForegroundColor Green
exit 0
