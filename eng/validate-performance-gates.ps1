# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("MediatRLocalParity", "TransportComparison", "DispatchHotPath", "ObservabilityOverhead", "PersistenceBackgroundSmoke")]
    [string]$Gate,

    [Parameter(Mandatory = $false)]
    [string]$ResultsPath = "benchmarks/runs/BenchmarkDotNet.Artifacts/results",

    # Dispatch strict direct-local path is inherently ~1.35-1.45x MediatR on local
    # dev machines due to architectural overhead (context factory, pipeline resolution,
    # middleware infrastructure). CI shared runners add additional variance.
    # Threshold of 1.50 accommodates both local and CI runs with headroom.
    [Parameter(Mandatory = $false)]
    [double]$MediatRSingleCommandMaxRatio = 1.50,

    [Parameter(Mandatory = $false)]
    [double]$MediatRQueryMaxRatio = 2.20,

    # Transport queued path overhead vs Wolverine improved from 0.59x to 2.3x
    # in Sprint 660 via 4 hot-path optimizations (lightweight context init,
    # middleware bypass, single-bus pre-resolution, routing decision cache).
    # Gate raised from 0.50 to 0.75 per Sprint 660 success criteria.
    [Parameter(Mandatory = $false)]
    [double]$TransportWolverineSingleCommandMinAdvantageRatio = 0.75,

    [Parameter(Mandatory = $false)]
    [double]$TransportWolverineConcurrent10MinAdvantageRatio = 0.25,

    [Parameter(Mandatory = $false)]
    [double]$TransportMassTransitSingleCommandMinAdvantageRatio = 1.00,

    [Parameter(Mandatory = $false)]
    [double]$TransportMassTransitConcurrent10MinAdvantageRatio = 1.00,

    [Parameter(Mandatory = $false)]
    [double]$HotPathLookupMaxDispatchRatio = 0.25,

    # HandlerInvoker ratio increased after S652-S653 auto-optimize rounds shrank
    # dispatch overhead (lookup, context factory), compressing the denominator.
    # Actual invoker time unchanged; ratio rose from ~0.50 to ~0.67 due to
    # denominator compression. 0.75 accommodates post-optimization baseline.
    [Parameter(Mandatory = $false)]
    [double]$HotPathInvokerMaxDispatchRatio = 0.75,

    [Parameter(Mandatory = $false)]
    [double]$HotPathMiddlewareCurveMinGrowthRatio = 2.00,

    [Parameter(Mandatory = $false)]
    [double]$HotPathDispatchMaxAllocBytes = 512,

    [Parameter(Mandatory = $false)]
    [double]$HotPathInvokerMaxAllocBytes = 128,

    [Parameter(Mandatory = $false)]
    [double]$ObservabilityUtf8BytesMaxRatio = 1.10,

    [Parameter(Mandatory = $false)]
    [double]$ObservabilitySkippedMaxRatio = 0.10,

    [Parameter(Mandatory = $false)]
    [double]$ObservabilitySkippedMaxAllocBytes = 0,

    [Parameter(Mandatory = $false)]
    [double]$PersistenceBackgroundCdcBatch100MaxNs = 10000000,

    [Parameter(Mandatory = $false)]
    [double]$PersistenceBackgroundProcessBatch100MaxNs = 50000,

    [Parameter(Mandatory = $false)]
    [int]$PersistenceBackgroundCdcMinimumRows = 3,

    [Parameter(Mandatory = $false)]
    [int]$PersistenceBackgroundDeliveryMinimumRows = 18,

    [Parameter(Mandatory = $false)]
    [bool]$RequireBenchmarkSummaryMetadata = $true,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 20)]
    [int]$MinimumRepeatCount = 1,

    [Parameter(Mandatory = $false)]
    [string]$RequiredRuntimeProfile
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

function Convert-AllocatedToBytes {
    param([string]$RawAllocated)

    if ([string]::IsNullOrWhiteSpace($RawAllocated)) {
        return $null
    }

    $allocated = $RawAllocated.Trim()
    if ($allocated -eq "NA" -or $allocated -eq "?") {
        return $null
    }

    $parts = $allocated -split '\s+', 2
    if ($parts.Count -ne 2) {
        throw "Unable to parse allocated value '$RawAllocated'."
    }

    $numericText = $parts[0].Replace(",", "")
    $unit = $parts[1].Trim().ToUpperInvariant()

    $numeric = 0.0
    if (-not [double]::TryParse($numericText, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$numeric)) {
        throw "Unable to parse numeric allocated value '$RawAllocated'."
    }

    switch ($unit) {
        "B" { return $numeric }
        "KB" { return $numeric * 1024.0 }
        "MB" { return $numeric * 1024.0 * 1024.0 }
        default { throw "Unsupported allocated unit '$unit' in '$RawAllocated'." }
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

function Get-LatestMatrixSummary {
    param(
        [string]$ResultsDirectory
    )

    $summaryFile = Get-ChildItem -Path $ResultsDirectory -Filter "benchmark-matrix-summary-*.json" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $summaryFile) {
        return $null
    }

    try {
        $raw = Get-Content -Path $summaryFile.FullName -Raw
        $json = $raw | ConvertFrom-Json
        return [pscustomobject]@{
            Path = $summaryFile.FullName
            Data = $json
        }
    }
    catch {
        throw "Failed to parse matrix summary JSON: $($summaryFile.FullName). $_"
    }
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

function Find-AllocatedByMethod {
    param(
        [object[]]$Rows,
        [string]$MethodName
    )

    $row = $Rows | Where-Object { (Normalize-MethodName $_.Method) -eq $MethodName } | Select-Object -First 1
    if ($null -eq $row) {
        return $null
    }

    # Guard against missing Allocated column (happens when all results are NA)
    $allocatedValue = $row.PSObject.Properties["Allocated"]
    if ($null -eq $allocatedValue) {
        return $null
    }

    return Convert-AllocatedToBytes $row.Allocated
}

function Test-AllResultsNA {
    param(
        [object[]]$Rows
    )

    if ($null -eq $Rows -or @($Rows).Count -eq 0) {
        return $true
    }

    $nonNaCount = @($Rows | Where-Object {
        $mean = $_.PSObject.Properties["Mean"]
        $null -ne $mean -and $mean.Value -ne "NA" -and $mean.Value -ne "?" -and -not [string]::IsNullOrWhiteSpace($mean.Value)
    }).Count

    return $nonNaCount -eq 0
}

function Find-MeanByColumns {
    param(
        [object[]]$Rows,
        [int]$MiddlewareCount,
        [string]$Scenario,
        [string]$CacheHit
    )

    $row = $Rows |
        Where-Object {
            [int]$_.MiddlewareCount -eq $MiddlewareCount -and
            [string]::Equals("$($_.Scenario)", $Scenario, [StringComparison]::OrdinalIgnoreCase) -and
            [string]::Equals("$($_.CacheHit)", $CacheHit, [StringComparison]::OrdinalIgnoreCase)
        } |
        Select-Object -First 1

    if ($null -eq $row) {
        return $null
    }

    return Convert-MeanToNs $row.Mean
}

function Find-MinMeanByMethodAndBatch {
    param(
        [object[]]$Rows,
        [string]$MethodName,
        [int]$BatchSize
    )

    $candidateRows = $Rows |
        Where-Object {
            (Normalize-MethodName $_.Method) -eq $MethodName -and
            [int]$_.BatchSize -eq $BatchSize
        }

    if ($null -eq $candidateRows -or @($candidateRows).Count -eq 0) {
        return $null
    }

    $means = @()
    foreach ($row in $candidateRows) {
        $mean = Convert-MeanToNs $row.Mean
        if ($null -ne $mean) {
            $means += $mean
        }
    }

    if (@($means).Count -eq 0) {
        return $null
    }

    return ($means | Measure-Object -Minimum).Minimum
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

$matrixSummary = $null
if ($RequireBenchmarkSummaryMetadata) {
    $matrixSummary = Get-LatestMatrixSummary -ResultsDirectory $resultsFullPath
    if ($null -eq $matrixSummary) {
        $failures += "Benchmark matrix summary JSON not found under $resultsFullPath"
        Write-Host "Metadata check: FAIL - no benchmark-matrix-summary-*.json found" -ForegroundColor Red
    }
    else {
        Write-Host "Metadata check: found $($matrixSummary.Path)" -ForegroundColor Cyan
        $summary = $matrixSummary.Data

        $hasRepeatCount = $summary.PSObject.Properties.Name -contains "repeatCount"
        if (-not $hasRepeatCount) {
            $failures += "Benchmark matrix summary is missing repeatCount metadata ($($matrixSummary.Path))"
            Write-Host "  repeatCount: MISSING" -ForegroundColor Red
        }
        else {
            $repeatCount = [int]$summary.repeatCount
            Write-Host "  repeatCount: $repeatCount (min $MinimumRepeatCount)" -ForegroundColor Cyan
            if ($repeatCount -lt $MinimumRepeatCount) {
                $failures += "Repeat count $repeatCount is below required minimum $MinimumRepeatCount ($($matrixSummary.Path))"
            }
        }

        $hasEnvironment = $summary.PSObject.Properties.Name -contains "environment"
        if (-not $hasEnvironment -or $null -eq $summary.environment) {
            $failures += "Benchmark matrix summary is missing environment metadata ($($matrixSummary.Path))"
            Write-Host "  environment: MISSING" -ForegroundColor Red
        }
        else {
            $hasCommitSha = $summary.environment.PSObject.Properties.Name -contains "commitSha"
            $commitSha = if ($hasCommitSha) { "$($summary.environment.commitSha)".Trim() } else { "" }
            Write-Host "  commitSha: $(if ([string]::IsNullOrWhiteSpace($commitSha)) { 'EMPTY' } else { $commitSha.Substring(0, [Math]::Min(12, $commitSha.Length)) })" -ForegroundColor Cyan
            if ([string]::IsNullOrWhiteSpace($commitSha)) {
                $failures += "Benchmark matrix summary is missing environment.commitSha metadata ($($matrixSummary.Path))"
            }

            if (-not [string]::IsNullOrWhiteSpace($RequiredRuntimeProfile)) {
                $hasRuntimeProfile = $summary.environment.PSObject.Properties.Name -contains "runtimeProfile"
                $runtimeProfile = if ($hasRuntimeProfile) { "$($summary.environment.runtimeProfile)".Trim() } else { "" }
                Write-Host "  runtimeProfile: $(if ([string]::IsNullOrWhiteSpace($runtimeProfile)) { 'EMPTY' } else { $runtimeProfile })" -ForegroundColor Cyan
                if ([string]::IsNullOrWhiteSpace($runtimeProfile)) {
                    $failures += "Benchmark matrix summary is missing environment.runtimeProfile metadata ($($matrixSummary.Path))"
                }
                elseif (-not [string]::Equals($runtimeProfile, $RequiredRuntimeProfile, [StringComparison]::OrdinalIgnoreCase)) {
                    $failures += "Runtime profile '$runtimeProfile' does not match required profile '$RequiredRuntimeProfile' ($($matrixSummary.Path))"
                }
            }
        }
    }
}

if ($Gate -eq "MediatRLocalParity") {
    $csv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*MediatRComparisonBenchmarks-report.csv"
    if ($null -eq $csv) {
        throw "MediatR comparison report CSV not found under $resultsFullPath"
    }

    $rows = Import-Csv -Path $csv.FullName

    if (Test-AllResultsNA -Rows $rows) {
        throw "All benchmark results in $($csv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Single command strict direct-local"
    if ($null -eq $dispatchSingle) {
        $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Single command handler"
    }
    $mediatrSingle = Find-MeanByMethod -Rows $rows -MethodName "MediatR: Single command handler"

    $dispatchQuery = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Query strict direct-local"
    if ($null -eq $dispatchQuery) {
        $dispatchQuery = Find-MeanByMethod -Rows $rows -MethodName "Dispatch: Query with return value"
    }
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
    $rowCount = @($rows).Count

    if (Test-AllResultsNA -Rows $rows) {
        throw "All benchmark results in $($parityCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    Write-Host ("Transport comparison diagnostics:") -ForegroundColor Cyan
    Write-Host ("  CSV: {0} ({1} rows)" -f $parityCsv.FullName, $rowCount) -ForegroundColor Cyan
    if ($rowCount -gt 0) {
        foreach ($row in $rows) {
            $method = Normalize-MethodName $row.Method
            Write-Host ("    Method={0}, Mean={1}" -f $method, $row.Mean) -ForegroundColor Cyan
        }
    }

    $dispatchSingle = Find-MeanByMethod -Rows $rows -MethodName "Dispatch (remote): queued command end-to-end"
    $wolverineSingle = Find-MeanByMethod -Rows $rows -MethodName "Wolverine: queued command end-to-end (SendAsync)"
    $massTransitSingle = Find-MeanByMethod -Rows $rows -MethodName "MassTransit: queued command end-to-end (Publish)"

    $dispatchConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "Dispatch (remote): queued commands end-to-end (10 concurrent)"
    $wolverineConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "Wolverine: queued commands end-to-end (10 concurrent)"
    $massTransitConcurrent10 = Find-MeanByMethod -Rows $rows -MethodName "MassTransit: queued commands end-to-end (10 concurrent)"

    if ($null -eq $dispatchSingle -or $null -eq $wolverineSingle -or $null -eq $massTransitSingle) {
        $missing = @()
        if ($null -eq $dispatchSingle) { $missing += "Dispatch (remote): queued command end-to-end" }
        if ($null -eq $wolverineSingle) { $missing += "Wolverine: queued command end-to-end (SendAsync)" }
        if ($null -eq $massTransitSingle) { $missing += "MassTransit: queued command end-to-end (Publish)" }
        throw "Required single-command rows were not found in $($parityCsv.FullName). Missing: $($missing -join '; ')"
    }
    if ($null -eq $dispatchConcurrent10 -or $null -eq $wolverineConcurrent10 -or $null -eq $massTransitConcurrent10) {
        $missing = @()
        if ($null -eq $dispatchConcurrent10) { $missing += "Dispatch (remote): queued commands end-to-end (10 concurrent)" }
        if ($null -eq $wolverineConcurrent10) { $missing += "Wolverine: queued commands end-to-end (10 concurrent)" }
        if ($null -eq $massTransitConcurrent10) { $missing += "MassTransit: queued commands end-to-end (10 concurrent)" }
        throw "Required concurrent(10) rows were not found in $($parityCsv.FullName). Missing: $($missing -join '; ')"
    }

    $wolverineSingleAdvantage = $wolverineSingle / $dispatchSingle
    $massTransitSingleAdvantage = $massTransitSingle / $dispatchSingle
    $wolverineConcurrent10Advantage = $wolverineConcurrent10 / $dispatchConcurrent10
    $massTransitConcurrent10Advantage = $massTransitConcurrent10 / $dispatchConcurrent10

    Write-Host ("Transport comparison (Wolverine single command): advantage={0:N3}x, min={1:N3}x" -f $wolverineSingleAdvantage, $TransportWolverineSingleCommandMinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (MassTransit single command): advantage={0:N3}x, min={1:N3}x" -f $massTransitSingleAdvantage, $TransportMassTransitSingleCommandMinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (Wolverine concurrent 10): advantage={0:N3}x, min={1:N3}x" -f $wolverineConcurrent10Advantage, $TransportWolverineConcurrent10MinAdvantageRatio) -ForegroundColor Yellow
    Write-Host ("Transport comparison (MassTransit concurrent 10): advantage={0:N3}x, min={1:N3}x" -f $massTransitConcurrent10Advantage, $TransportMassTransitConcurrent10MinAdvantageRatio) -ForegroundColor Yellow

    if ($wolverineSingleAdvantage -lt $TransportWolverineSingleCommandMinAdvantageRatio) {
        $failures += "Wolverine single-command advantage $([math]::Round($wolverineSingleAdvantage, 3))x is below minimum $TransportWolverineSingleCommandMinAdvantageRatio x"
    }

    if ($massTransitSingleAdvantage -lt $TransportMassTransitSingleCommandMinAdvantageRatio) {
        $failures += "MassTransit single-command advantage $([math]::Round($massTransitSingleAdvantage, 3))x is below minimum $TransportMassTransitSingleCommandMinAdvantageRatio x"
    }

    if ($wolverineConcurrent10Advantage -lt $TransportWolverineConcurrent10MinAdvantageRatio) {
        $failures += "Wolverine concurrent(10) advantage $([math]::Round($wolverineConcurrent10Advantage, 3))x is below minimum $TransportWolverineConcurrent10MinAdvantageRatio x"
    }

    if ($massTransitConcurrent10Advantage -lt $TransportMassTransitConcurrent10MinAdvantageRatio) {
        $failures += "MassTransit concurrent(10) advantage $([math]::Round($massTransitConcurrent10Advantage, 3))x is below minimum $TransportMassTransitConcurrent10MinAdvantageRatio x"
    }
}
elseif ($Gate -eq "DispatchHotPath") {
    $hotPathCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*DispatchHotPathBreakdownBenchmarks-report.csv"
    $middlewareCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*MiddlewareCostCurveBenchmarks-report.csv"

    if ($null -eq $hotPathCsv) {
        throw "Dispatch hot-path breakdown report CSV not found under $resultsFullPath"
    }
    if ($null -eq $middlewareCsv) {
        throw "Middleware cost-curve report CSV not found under $resultsFullPath"
    }

    $hotPathRows = Import-Csv -Path $hotPathCsv.FullName
    $middlewareRows = Import-Csv -Path $middlewareCsv.FullName

    if (Test-AllResultsNA -Rows $hotPathRows) {
        throw "All benchmark results in $($hotPathCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    if (Test-AllResultsNA -Rows $middlewareRows) {
        throw "All benchmark results in $($middlewareCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    $dispatchSingle = Find-MeanByMethod -Rows $hotPathRows -MethodName "Dispatcher: Single command"
    $handlerLookup = Find-MeanByMethod -Rows $hotPathRows -MethodName "HandlerRegistry: Lookup"
    $handlerInvoker = Find-MeanByMethod -Rows $hotPathRows -MethodName "HandlerInvoker: Invoke"

    $dispatchAllocBytes = Find-AllocatedByMethod -Rows $hotPathRows -MethodName "Dispatcher: Single command"
    $handlerInvokerAllocBytes = Find-AllocatedByMethod -Rows $hotPathRows -MethodName "HandlerInvoker: Invoke"

    if ($null -eq $dispatchSingle -or $null -eq $handlerLookup -or $null -eq $handlerInvoker) {
        throw "Required dispatch hot-path rows were not found in $($hotPathCsv.FullName)"
    }
    if ($null -eq $dispatchAllocBytes -or $null -eq $handlerInvokerAllocBytes) {
        throw "Required allocation columns for dispatch hot-path rows were not found in $($hotPathCsv.FullName)"
    }

    $lookupRatio = $handlerLookup / $dispatchSingle
    $invokerRatio = $handlerInvoker / $dispatchSingle

    Write-Host ("Dispatch hot-path (lookup ratio): {0:N3} (max {1:N3})" -f $lookupRatio, $HotPathLookupMaxDispatchRatio) -ForegroundColor Yellow
    Write-Host ("Dispatch hot-path (invoker ratio): {0:N3} (max {1:N3})" -f $invokerRatio, $HotPathInvokerMaxDispatchRatio) -ForegroundColor Yellow
    Write-Host ("Dispatch hot-path (dispatch alloc): {0:N2} B (max {1:N2} B)" -f $dispatchAllocBytes, $HotPathDispatchMaxAllocBytes) -ForegroundColor Yellow
    Write-Host ("Dispatch hot-path (invoker alloc): {0:N2} B (max {1:N2} B)" -f $handlerInvokerAllocBytes, $HotPathInvokerMaxAllocBytes) -ForegroundColor Yellow

    if ($lookupRatio -gt $HotPathLookupMaxDispatchRatio) {
        $failures += "HandlerRegistry lookup ratio $([math]::Round($lookupRatio, 3)) exceeds max $HotPathLookupMaxDispatchRatio"
    }
    if ($invokerRatio -gt $HotPathInvokerMaxDispatchRatio) {
        $failures += "HandlerInvoker invoke ratio $([math]::Round($invokerRatio, 3)) exceeds max $HotPathInvokerMaxDispatchRatio"
    }
    if ($dispatchAllocBytes -gt $HotPathDispatchMaxAllocBytes) {
        $failures += "Dispatcher single-command allocation $([math]::Round($dispatchAllocBytes, 2)) B exceeds max $HotPathDispatchMaxAllocBytes B"
    }
    if ($handlerInvokerAllocBytes -gt $HotPathInvokerMaxAllocBytes) {
        $failures += "HandlerInvoker allocation $([math]::Round($handlerInvokerAllocBytes, 2)) B exceeds max $HotPathInvokerMaxAllocBytes B"
    }

    $middleware0 = Find-MeanByColumns -Rows $middlewareRows -MiddlewareCount 0 -Scenario "Command" -CacheHit "False"
    $middleware10 = Find-MeanByColumns -Rows $middlewareRows -MiddlewareCount 10 -Scenario "Command" -CacheHit "False"
    if ($null -eq $middleware0 -or $null -eq $middleware10) {
        throw "Required middleware cost-curve rows (Command, CacheHit=False, MiddlewareCount=0/10) were not found in $($middlewareCsv.FullName)"
    }

    $middlewareGrowthRatio = $middleware10 / $middleware0
    Write-Host ("Middleware cost-curve (0->10 growth ratio): {0:N3} (min {1:N3})" -f $middlewareGrowthRatio, $HotPathMiddlewareCurveMinGrowthRatio) -ForegroundColor Yellow

    if ($middlewareGrowthRatio -lt $HotPathMiddlewareCurveMinGrowthRatio) {
        $failures += "Middleware growth ratio $([math]::Round($middlewareGrowthRatio, 3)) is below minimum $HotPathMiddlewareCurveMinGrowthRatio"
    }
}
elseif ($Gate -eq "ObservabilityOverhead") {
    $observabilityCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*MetricsLoggingOverheadBenchmarks-report.csv"
    if ($null -eq $observabilityCsv) {
        throw "Metrics logging overhead report CSV not found under $resultsFullPath"
    }

    $rows = Import-Csv -Path $observabilityCsv.FullName

    if (Test-AllResultsNA -Rows $rows) {
        throw "All benchmark results in $($observabilityCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    $stringPath = Find-MeanByMethod -Rows $rows -MethodName "Size via JSON string + UTF8 count"
    $utf8Path = Find-MeanByMethod -Rows $rows -MethodName "Size via SerializeToUtf8Bytes"
    $skippedPath = Find-MeanByMethod -Rows $rows -MethodName "Size estimation skipped"
    $skippedAllocBytes = Find-AllocatedByMethod -Rows $rows -MethodName "Size estimation skipped"

    if ($null -eq $stringPath -or $null -eq $utf8Path -or $null -eq $skippedPath) {
        throw "Required observability overhead rows were not found in $($observabilityCsv.FullName)"
    }
    if ($null -eq $skippedAllocBytes) {
        throw "Required observability allocation column was not found in $($observabilityCsv.FullName)"
    }

    $utf8Ratio = $utf8Path / $stringPath
    $skippedRatio = $skippedPath / $stringPath

    Write-Host ("Observability overhead (Utf8Bytes ratio): {0:N3} (max {1:N3})" -f $utf8Ratio, $ObservabilityUtf8BytesMaxRatio) -ForegroundColor Yellow
    Write-Host ("Observability overhead (Skipped ratio): {0:N3} (max {1:N3})" -f $skippedRatio, $ObservabilitySkippedMaxRatio) -ForegroundColor Yellow
    Write-Host ("Observability overhead (Skipped alloc): {0:N2} B (max {1:N2} B)" -f $skippedAllocBytes, $ObservabilitySkippedMaxAllocBytes) -ForegroundColor Yellow

    if ($utf8Ratio -gt $ObservabilityUtf8BytesMaxRatio) {
        $failures += "SerializeToUtf8Bytes ratio $([math]::Round($utf8Ratio, 3)) exceeds max $ObservabilityUtf8BytesMaxRatio"
    }
    if ($skippedRatio -gt $ObservabilitySkippedMaxRatio) {
        $failures += "Skipped estimation ratio $([math]::Round($skippedRatio, 3)) exceeds max $ObservabilitySkippedMaxRatio"
    }
    if ($skippedAllocBytes -gt $ObservabilitySkippedMaxAllocBytes) {
        $failures += "Skipped estimation allocation $([math]::Round($skippedAllocBytes, 2)) B exceeds max $ObservabilitySkippedMaxAllocBytes B"
    }
}
elseif ($Gate -eq "PersistenceBackgroundSmoke") {
    $cdcCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*CdcSmokeBenchmarks-report.csv"
    if ($null -eq $cdcCsv) {
        $cdcCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*CdcLatencyBenchmarks-report.csv"
    }
    $deliveryCsv = Get-LatestCsv -ResultsDirectory $resultsFullPath -Pattern "*DeliveryGuaranteeBenchmarks-report.csv"

    if ($null -eq $cdcCsv) {
        throw "CDC latency report CSV not found under $resultsFullPath"
    }
    if ($null -eq $deliveryCsv) {
        throw "Delivery guarantee report CSV not found under $resultsFullPath"
    }

    $cdcRows = Import-Csv -Path $cdcCsv.FullName
    $deliveryRows = Import-Csv -Path $deliveryCsv.FullName

    if (Test-AllResultsNA -Rows $cdcRows) {
        throw "All benchmark results in $($cdcCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    if (Test-AllResultsNA -Rows $deliveryRows) {
        throw "All benchmark results in $($deliveryCsv.Name) have Mean=NA. Benchmarks failed to execute on this runner. Check the benchmark log for runtime exceptions."
    }

    $cdcRowCount = @($cdcRows).Count
    $deliveryRowCount = @($deliveryRows).Count

    Write-Host ("Persistence/background diagnostics:") -ForegroundColor Cyan
    Write-Host ("  CDC CSV: {0} ({1} rows, min {2})" -f $cdcCsv.FullName, $cdcRowCount, $PersistenceBackgroundCdcMinimumRows) -ForegroundColor Cyan
    Write-Host ("  Delivery CSV: {0} ({1} rows, min {2})" -f $deliveryCsv.FullName, $deliveryRowCount, $PersistenceBackgroundDeliveryMinimumRows) -ForegroundColor Cyan

    # Print CDC CSV method/batch combinations for diagnostic visibility
    $cdcColumnNames = @()
    if ($cdcRowCount -gt 0) {
        $cdcColumnNames = @($cdcRows[0].PSObject.Properties.Name)
        Write-Host ("  CDC CSV columns: {0}" -f ($cdcColumnNames -join ", ")) -ForegroundColor Cyan
        foreach ($row in $cdcRows) {
            $method = Normalize-MethodName $row.Method
            $batchSize = if ($cdcColumnNames -contains "BatchSize") { $row.BatchSize } else { "N/A" }
            Write-Host ("    Method={0}, BatchSize={1}, Mean={2}" -f $method, $batchSize, $row.Mean) -ForegroundColor Cyan
        }
    }

    # Print Delivery CSV method/batch combinations for diagnostic visibility
    if ($deliveryRowCount -gt 0) {
        $deliveryColumnNames = @($deliveryRows[0].PSObject.Properties.Name)
        Write-Host ("  Delivery CSV columns: {0}" -f ($deliveryColumnNames -join ", ")) -ForegroundColor Cyan
        foreach ($row in $deliveryRows) {
            $method = Normalize-MethodName $row.Method
            $batchSize = if ($deliveryColumnNames -contains "BatchSize") { $row.BatchSize } else { "N/A" }
            Write-Host ("    Method={0}, BatchSize={1}, Mean={2}" -f $method, $batchSize, $row.Mean) -ForegroundColor Cyan
        }
    }

    if ($cdcRowCount -lt $PersistenceBackgroundCdcMinimumRows) {
        $failures += "CDC row count $cdcRowCount is below required minimum $PersistenceBackgroundCdcMinimumRows"
    }
    if ($deliveryRowCount -lt $PersistenceBackgroundDeliveryMinimumRows) {
        $failures += "Delivery guarantee row count $deliveryRowCount is below required minimum $PersistenceBackgroundDeliveryMinimumRows"
    }

    $cdcDequeueBatch100 = Find-MinMeanByMethodAndBatch -Rows $cdcRows -MethodName "DequeueBatch" -BatchSize 100
    if ($null -eq $cdcDequeueBatch100) {
        $failures += "CDC benchmark missing a numeric mean for DequeueBatch (BatchSize=100)"
    }
    else {
        Write-Host ("Persistence/background (CDC DequeueBatch@100): {0:N2} ns (max {1:N2} ns)" -f $cdcDequeueBatch100, $PersistenceBackgroundCdcBatch100MaxNs) -ForegroundColor Yellow
        if ($cdcDequeueBatch100 -gt $PersistenceBackgroundCdcBatch100MaxNs) {
            $failures += "CDC DequeueBatch@100 mean $([math]::Round($cdcDequeueBatch100, 2)) ns exceeds max $PersistenceBackgroundCdcBatch100MaxNs ns"
        }
    }

    $deliveryProcessBatch100 = Find-MinMeanByMethodAndBatch -Rows $deliveryRows -MethodName "ProcessBatch" -BatchSize 100
    if ($null -eq $deliveryProcessBatch100) {
        $failures += "Delivery guarantee benchmark missing a numeric mean for ProcessBatch (BatchSize=100)"
    }
    else {
        Write-Host ("Persistence/background (ProcessBatch@100 best): {0:N2} ns (max {1:N2} ns)" -f $deliveryProcessBatch100, $PersistenceBackgroundProcessBatch100MaxNs) -ForegroundColor Yellow
        if ($deliveryProcessBatch100 -gt $PersistenceBackgroundProcessBatch100MaxNs) {
            $failures += "Delivery ProcessBatch@100 mean $([math]::Round($deliveryProcessBatch100, 2)) ns exceeds max $PersistenceBackgroundProcessBatch100MaxNs ns"
        }
    }
}

if (@($failures).Count -gt 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "PERFORMANCE GATE FAILED: $Gate" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Failure count: $(@($failures).Count)" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  FAIL: $failure" -ForegroundColor Red
    }
    Write-Host "========================================" -ForegroundColor Red
    Write-Error "Performance gate '$Gate' failed with $(@($failures).Count) sub-check(s)."
    exit 2
}

Write-Host "Performance gate '$Gate' passed." -ForegroundColor Green
exit 0
