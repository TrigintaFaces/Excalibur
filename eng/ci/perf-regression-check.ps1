<#
.SYNOPSIS
    Compares BenchmarkDotNet results against recorded baselines and reports a three-state verdict.

.DESCRIPTION
    Three-state contract. REFUSE is NOT a pass:

      exit 0  PASS     benchmarks were compared and none regressed beyond the threshold
      exit 1  FAIL     at least one benchmark regressed beyond the threshold
      exit 2  REFUSE   nothing could be compared, so no claim about performance is made

    The REFUSE state exists because a gate that examined nothing must not be
    indistinguishable from a gate that examined everything and approved it. Every
    path that previously printed "No performance regressions detected" without
    having compared a single measurement now exits 2 instead of 0.

    The verdict line always reports how many comparisons actually EXECUTED, so a
    silent drop in coverage is visible in a number a reader already checks rather
    than as an absent warning.

.PARAMETER ResultsPath
    Directory searched recursively for BenchmarkDotNet '*-report.json' files.

.PARAMETER BaselinesPath
    Path to performance-baselines.json.

.PARAMETER RegressionThreshold
    Optional override of the threshold in the baselines file (0.10 = 10%).

.PARAMETER RequireResultsOnly
    Answer the narrower question "did this benchmark run actually EXECUTE anything?"
    and skip the baseline comparison entirely.

    This exists so the benchmark jobs that have no recorded baselines reuse THIS
    guard rather than growing a second one. BenchmarkDotNet exits 0 when a critical
    validator aborts the run before any benchmark executes, so a job whose success
    criterion is the exit code of `dotnet run` plus an artifact upload reports green
    over a run that measured nothing. That is a property of the shape, not a rare
    accident: any such job satisfies it by construction.

    Do NOT reach for this on a job that HAS baselines. There it would answer a
    weaker question than the one that matters and would report PASS over a real
    regression.

    Why a mode instead of the full comparison: the recorded baselines contain only
    hot-path method names, and zero of them are produced by the memory or throughput
    filters. Running the full comparison against those jobs would take the
    "read result files but matched no baseline" path on every run, forever -- a gate
    that is permanently REFUSE is indistinguishable from one that is broken, and it
    trains readers to route around the signal.

.PARAMETER AllowEmpty
    Downgrade REFUSE to PASS. For local/manual use only; CI must never pass this,
    because it re-creates the exact defect this script exists to prevent.
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory)][string]$ResultsPath,
	# NOT mandatory, so -RequireResultsOnly can be used without inventing a baselines
	# file for a job that has none. Omitting it WITHOUT that switch still REFUSEs
	# through the not-found path below -- the failure direction stays safe.
	[string]$BaselinesPath = '',
	[double]$RegressionThreshold = -1,
	[switch]$RequireResultsOnly,
	[switch]$AllowEmpty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EXIT_PASS = 0
$EXIT_FAIL = 1
$EXIT_REFUSE = 2

function Write-Verdict {
	param([string]$State, [string]$Reason, [int]$Compared, [int]$Baselines, [int]$Files)
	Write-Host ""
	Write-Host "perf-regression-check: $State" -ForegroundColor $(if ($State -eq 'PASS') { 'Green' } elseif ($State -eq 'FAIL') { 'Red' } else { 'Yellow' })
	Write-Host "  comparisons EXECUTED : $Compared"
	Write-Host "  baseline entries     : $Baselines"
	Write-Host "  result files read    : $Files"
	Write-Host "  reason               : $Reason"
}

# ---- EXISTENCE-ONLY MODE ------------------------------------------------------------
# Same three-state contract, narrower question: did the run execute any benchmark at all?
# Counts BENCHMARK RECORDS, not files. A report file can exist and carry an empty
# 'Benchmarks' array, and a file-only check would read that as a successful run -- which is
# the same "green over nothing" defect one layer in.
# GLOB NOTE, and it is why this gate had never passed: BenchmarkDotNet NEVER writes '*-report.json'.
# Every JSON exporter carries a suffix -- Brief -> '-report-brief.json', Full -> '-report-full.json',
# FullCompressed (the DEFAULT) -> '-report-full-compressed.json'. Measured in this tree: 48 files named
# '*-report-full-compressed.json' under BenchmarkDotNet.Artifacts/results and ZERO named '*-report.json'.
# So the original filter was unsatisfiable by construction: no --exporters value could ever satisfy it,
# and the gate reported REFUSE on every healthy run. It had never executed in CI until 2026-09-14, which
# is the only reason that went unnoticed. '*-report*.json' matches every JSON exporter variant.
if ($RequireResultsOnly) {
	$resultFiles = @(Get-ChildItem -Path $ResultsPath -Filter '*-report*.json' -Recurse -ErrorAction SilentlyContinue)

	if ($resultFiles.Count -eq 0) {
		Write-Verdict -State 'REFUSE' -Reason "no BenchmarkDotNet JSON report files under '$ResultsPath'" -Compared 0 -Baselines 0 -Files 0
		if ($AllowEmpty) {
			Write-Host "::warning::AllowEmpty set - downgrading REFUSE to PASS. Never use this in CI."
			exit $EXIT_PASS
		}
		Write-Host "::error::perf-regression-check REFUSED: the benchmark run produced no results."
		exit $EXIT_REFUSE
	}

	$executed = 0
	foreach ($resultFile in $resultFiles) {
		try {
			$results = Get-Content $resultFile.FullName -Raw | ConvertFrom-Json
			if (-not ($results.PSObject.Properties.Name -contains 'Benchmarks')) { continue }
			$executed += @($results.Benchmarks).Count
		}
		catch {
			Write-Host "::warning::Error processing $($resultFile.Name): $_"
		}
	}

	if ($executed -eq 0) {
		Write-Verdict -State 'REFUSE' -Reason "read $($resultFiles.Count) result file(s) but they contain zero benchmark records" -Compared 0 -Baselines 0 -Files $resultFiles.Count
		if ($AllowEmpty) {
			Write-Host "::warning::AllowEmpty set - downgrading REFUSE to PASS. Never use this in CI."
			exit $EXIT_PASS
		}
		Write-Host "::error::perf-regression-check REFUSED: the benchmark run executed nothing."
		exit $EXIT_REFUSE
	}

	Write-Verdict -State 'PASS' -Reason 'existence-only check: the run executed benchmarks (no baseline comparison requested)' -Compared $executed -Baselines 0 -Files $resultFiles.Count
	exit $EXIT_PASS
}

# ---- REFUSE: baselines file absent -------------------------------------------------
# Previously an explicit `exit 0` with a warning: a missing input silently certified
# the build as regression-free.
if (-not (Test-Path $BaselinesPath)) {
	Write-Verdict -State 'REFUSE' -Reason "baselines file not found at '$BaselinesPath'" -Compared 0 -Baselines 0 -Files 0
	Write-Host "::error::perf-regression-check REFUSED: no baselines to compare against."
	exit $EXIT_REFUSE
}

$baselines = Get-Content $BaselinesPath -Raw | ConvertFrom-Json
if ($RegressionThreshold -lt 0) {
	$RegressionThreshold = [double]$baselines.regressionThreshold
}

$baselineLookup = @{}
if ($baselines.PSObject.Properties.Name -contains 'baselines') {
	foreach ($category in $baselines.baselines.PSObject.Properties) {
		foreach ($entry in $category.Value.PSObject.Properties) {
			$baselineLookup[$entry.Name] = $entry.Value
		}
	}
}

# ---- REFUSE: no baseline entries ---------------------------------------------------
# A present-but-empty baselines file compared nothing while reporting success.
if ($baselineLookup.Count -eq 0) {
	Write-Verdict -State 'REFUSE' -Reason 'baselines file contains zero measurements' -Compared 0 -Baselines 0 -Files 0
	Write-Host "::error::perf-regression-check REFUSED: baselines file has no entries."
	exit $EXIT_REFUSE
}

$resultFiles = @(Get-ChildItem -Path $ResultsPath -Filter '*-report*.json' -Recurse -ErrorAction SilentlyContinue)

# ---- REFUSE: no result files -------------------------------------------------------
# The originally filed defect: a benchmark run that produced no output passed the gate.
if ($resultFiles.Count -eq 0) {
	Write-Verdict -State 'REFUSE' -Reason "no BenchmarkDotNet JSON report files under '$ResultsPath'" -Compared 0 -Baselines $baselineLookup.Count -Files 0
	if ($AllowEmpty) {
		Write-Host "::warning::AllowEmpty set - downgrading REFUSE to PASS. Never use this in CI."
		exit $EXIT_PASS
	}
	Write-Host "::error::perf-regression-check REFUSED: the benchmark run produced no results."
	exit $EXIT_REFUSE
}

$regressions = @()
$improvements = @()
$compared = 0

foreach ($resultFile in $resultFiles) {
	try {
		$results = Get-Content $resultFile.FullName -Raw | ConvertFrom-Json
		if (-not ($results.PSObject.Properties.Name -contains 'Benchmarks')) { continue }

		foreach ($benchmark in $results.Benchmarks) {
			$methodName = $benchmark.Method
			if (-not $baselineLookup.ContainsKey($methodName)) { continue }

			$baseline = $baselineLookup[$methodName]
			$baselineMeanNs = [double]$baseline.meanNs
			if ($baselineMeanNs -le 0) { continue }

			$currentMeanNs = [double]$benchmark.Statistics.Mean
			$compared++
			$change = ($currentMeanNs - $baselineMeanNs) / $baselineMeanNs

			if ($change -gt $RegressionThreshold) {
				$regressions += @{ Method = $methodName; Change = $change; Baseline = $baselineMeanNs; Current = $currentMeanNs }
			}
			elseif ($change -lt -0.05) {
				$improvements += @{ Method = $methodName; Improvement = -$change }
			}
		}
	}
	catch {
		Write-Host "::warning::Error processing $($resultFile.Name): $_"
	}
}

# ---- REFUSE: files present but nothing matched a baseline --------------------------
# Renamed or relocated benchmarks silently stop being measured; that is a coverage
# loss, not a clean run.
if ($compared -eq 0) {
	Write-Verdict -State 'REFUSE' -Reason "read $($resultFiles.Count) result file(s) but no benchmark matched a baseline entry" -Compared 0 -Baselines $baselineLookup.Count -Files $resultFiles.Count
	Write-Host "::error::perf-regression-check REFUSED: zero comparisons executed. Benchmarks may have been renamed."
	exit $EXIT_REFUSE
}

foreach ($imp in $improvements) {
	Write-Host ("  improved {0}: {1}% faster" -f $imp.Method, [math]::Round($imp.Improvement * 100, 1)) -ForegroundColor Green
}

if ($regressions.Count -gt 0) {
	foreach ($reg in $regressions) {
		Write-Host "::error::Performance regression in $($reg.Method): +$([math]::Round($reg.Change * 100, 1))% ($([math]::Round($reg.Baseline, 2))ns -> $([math]::Round($reg.Current, 2))ns)"
	}
	Write-Verdict -State 'FAIL' -Reason "$($regressions.Count) regression(s) exceed $($RegressionThreshold * 100)% threshold" -Compared $compared -Baselines $baselineLookup.Count -Files $resultFiles.Count
	exit $EXIT_FAIL
}

Write-Verdict -State 'PASS' -Reason "no regression beyond $($RegressionThreshold * 100)% threshold" -Compared $compared -Baselines $baselineLookup.Count -Files $resultFiles.Count
exit $EXIT_PASS
