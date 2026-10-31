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
# The benchmark's CLASS, kept alongside its entry. Allocation reproducibility is a property of
# the class, not of the individual benchmark, so the allocation verdict below needs it.
$baselineClass = @{}
if ($baselines.PSObject.Properties.Name -contains 'baselines') {
	foreach ($category in $baselines.baselines.PSObject.Properties) {
		foreach ($entry in $category.Value.PSObject.Properties) {
			$baselineLookup[$entry.Name] = $entry.Value
			$baselineClass[$entry.Name] = $category.Name
		}
	}
}

# Classes whose allocated-bytes figure is NOT verdict-grade, declared in the baselines file.
# An entry from one of these never produces an allocation FAIL, however it got into "baselines" --
# which is the point: the defect this closes was one such entry sitting in the compared set and
# failing a build on a delta no code change could explain.
$allocNotVerdictGrade = @()
if ($baselines.PSObject.Properties.Name -contains 'allocationNotVerdictGrade') {
	$allocNotVerdictGrade = @($baselines.allocationNotVerdictGrade.classes)
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
$timingDrift = @()
$improvements = @()
$compared = 0
$allocCompared = 0
$allocSkipped = @()
# Matched entries whose BASELINE declares an allocatedBytes figure, whether or not it was
# ultimately compared. This is what separates "this file claims allocation coverage and the
# run enforced none of it" (a vacuous green) from "this file is timing-only by design"
# (legitimate -- the memory/throughput fixtures are exactly that).
$allocAvailable = 0

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

			# BenchmarkDotNet emits a benchmark record with 'Statistics': null when the
			# benchmark produced no measurements at all. Under Set-StrictMode, reading
			# .Mean off that null THROWS -- and because this loop sits inside the
			# per-file try/catch, the throw abandons every REMAINING record in the file.
			# That is a silent coverage drop: the verdict line still reports PASS, just
			# over fewer comparisons than anyone expects, with only a ::warning:: to say
			# so. Measured in this tree: the '*HotPath*' filter produces exactly one such
			# record (CompoundOperation_FullHotPathAccess, Measurements: 0), so this is a
			# shape BenchmarkDotNet really emits, not a defensive hypothetical.
			# Skip the unmeasured record and keep comparing the rest of the file.
			if ($null -eq $benchmark.Statistics -or $null -eq $benchmark.Statistics.Mean) {
				Write-Host "::warning::$methodName has a baseline but the run recorded no measurements; not compared."
				continue
			}

			$currentMeanNs = [double]$benchmark.Statistics.Mean
			$compared++
			$change = ($currentMeanNs - $baselineMeanNs) / $baselineMeanNs

			# TIMING IS ADVISORY, ALLOCATION BLOCKS -- and the baselines file says why in its own
			# measurementCaveat: these means were recorded on a developer workstation while CI compares
			# ubuntu, windows and macos runs against them, so "a cross-machine delta is not necessarily a
			# code regression". One commit measured +15%, +34% and +182% on the SAME benchmark across the
			# three runners; a signal that disagrees with itself by an order of magnitude cannot decide a
			# build. Failing on it trains everyone to ignore the gate, which costs more than it catches.
			#
			# Allocated bytes is the half that survives the move: it counts work the code actually did,
			# and a real regression -- an extra allocation per dispatch -- shows up in it exactly. So
			# allocation regressions FAIL and timing is reported for a human.
			#
			# THE MACHINE-INDEPENDENCE IS CLASS-SCOPED, AND THIS COMMENT USED TO CLAIM IT WAS UNIVERSAL
			# ("it is identical on every runner"). Measured 2026-10-31 over 3 runners x 2 consecutive
			# commits: every benchmark in the breakdown classes was byte-identical in all six runs, while
			# every benchmark in the end-to-end comparative class varied 3x to 56x -- including the two
			# that measure a third-party library this repository cannot affect. The comparative class
			# builds a full host per operation and runs at --iterationCount 3 --warmupCount 1, so its
			# one-time warm-up costs land inside the measured window unpredictably.
			# Restore timing to blocking the moment a baseline is re-recorded ON a CI runner.
			if ($change -gt $RegressionThreshold) {
				$timingDrift += @{ Method = $methodName; Change = $change; Baseline = $baselineMeanNs; Current = $currentMeanNs }
			}
			elseif ($change -lt -0.05) {
				$improvements += @{ Method = $methodName; Improvement = -$change }
			}

			$baselineAlloc = if ($null -ne $baseline.allocatedBytes) { [double]$baseline.allocatedBytes } else { -1 }
			$currentAlloc = if ($null -ne $benchmark.Memory -and $null -ne $benchmark.Memory.BytesAllocatedPerOperation) {
				[double]$benchmark.Memory.BytesAllocatedPerOperation
			} else { -1 }

			if ($baselineAlloc -gt 0 -and $currentAlloc -ge 0) { $allocAvailable++ }
			$methodClass = if ($baselineClass.ContainsKey($methodName)) { $baselineClass[$methodName] } else { '' }
			if ($methodClass -and $allocNotVerdictGrade -contains $methodClass) {
				if ($baselineAlloc -gt 0 -and $currentAlloc -ge 0) {
					$allocSkipped += @{ Method = $methodName; Class = $methodClass }
				}
			}
			elseif ($baselineAlloc -gt 0 -and $currentAlloc -ge 0) {
				$allocCompared++
				$allocChange = ($currentAlloc - $baselineAlloc) / $baselineAlloc
				if ($allocChange -gt $RegressionThreshold) {
					$regressions += @{
						Method = $methodName; Change = $allocChange
						Baseline = $baselineAlloc; Current = $currentAlloc
					}
				}
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

foreach ($drift in $timingDrift) {
	Write-Host "::warning::Timing drift in $($drift.Method): +$([math]::Round($drift.Change * 100, 1))% ($([math]::Round($drift.Baseline, 2))ns -> $([math]::Round($drift.Current, 2))ns) -- ADVISORY: the baseline was recorded on a workstation, not this runner."
}

Write-Host "allocation comparisons: $allocCompared of $compared timed benchmark(s) carried a baseline allocation figure."
foreach ($skip in $allocSkipped) {
	Write-Host "  allocation NOT compared for $($skip.Method): its class $($skip.Class) is declared not verdict-grade."
}

# ---- REFUSE: zero allocation comparisons -------------------------------------------
# Allocation is the only BLOCKING half of this gate; timing is advisory. So a run that
# compared no allocation figure enforced NOTHING, and PASSing it reports a green that no
# input could have turned red. Reachable two ways: every baseline entry losing its
# allocatedBytes field, or every remaining class being declared not verdict-grade.
if ($allocAvailable -eq 0) {
	Write-Host "::warning::no matched baseline entry declares an allocatedBytes figure, so this run enforced TIMING ONLY -- and timing is advisory here. Nothing in it could have failed."
}
elseif ($allocCompared -eq 0) {
	Write-Verdict -State 'REFUSE' -Reason "executed $compared timing comparison(s) but ZERO allocation comparisons; allocation is the only blocking check, so nothing was enforced" -Compared 0 -Baselines $baselineLookup.Count -Files $resultFiles.Count
	Write-Host "::error::perf-regression-check REFUSED: no allocation figure was compared, so this run could not have failed."
	exit $EXIT_REFUSE
}

if ($regressions.Count -gt 0) {
	foreach ($reg in $regressions) {
		Write-Host "::error::Allocation regression in $($reg.Method): +$([math]::Round($reg.Change * 100, 1))% ($([math]::Round($reg.Baseline, 0)) B -> $([math]::Round($reg.Current, 0)) B per operation)"
	}
	Write-Verdict -State 'FAIL' -Reason "$($regressions.Count) allocation regression(s) exceed $($RegressionThreshold * 100)% threshold" -Compared $allocCompared -Baselines $baselineLookup.Count -Files $resultFiles.Count
	exit $EXIT_FAIL
}

Write-Verdict -State 'PASS' -Reason "no allocation regression beyond $($RegressionThreshold * 100)% threshold ($($timingDrift.Count) timing drift(s) reported as advisory)" -Compared $allocCompared -Baselines $baselineLookup.Count -Files $resultFiles.Count
exit $EXIT_PASS
