<#
.SYNOPSIS
  Honest aggregation gate for sharded test runs.

.DESCRIPTION
  The prior aggregation (ci.yml integration verify) summed per-trx `failed` counters and
  treated "no trx" as zero failures. Two defects follow:

    * ar6w5k / f37o9a — a project that does NOT compile (or is silently dropped from a run)
      emits no trx, contributes 0 to the sum, and reads as CLEAN. The gate never asserts the
      EXPECTED assembly set actually compiled and produced results.
    * 1ltu6f — an assembly that is a member of more than one shard .slnf produces a trx per
      shard; a naive sum double-counts it, so no cross-shard total is a count of DISTINCT tests.

  This gate fixes both by aggregating at ASSEMBLY granularity:

    1. Joins each test result to its owning assembly (trx <UnitTest storage="...dll"> + testId).
    2. Dedupes by assembly across all trx — an assembly in N shards is counted ONCE. If a test
       passes in one shard and fails in another, the merged outcome is FAILED (fail-closed).
    3. Asserts every EXPECTED assembly produced results — a missing one is RED ("did not compile
       or was not run"), NOT summed-clean.
    4. RED on any distinct-assembly failure or any missing expected assembly; GREEN only when the
       full expected set ran and zero distinct tests failed.

  Exit codes (three-value, falsifiable per testing-patterns §3):
    0  GREEN — full expected set present, zero distinct failures.
    1  RED   — a missing/non-compiling assembly, or a distinct-test failure (enforced).
    2  ERROR — the gate could not compute a sound result (no trx, unreadable trx). A total that
               cannot be computed soundly is refused, not printed as zero.

  Proving self-test (safety + liveness arms): .claude/harness/validate-shard-results.test.ps1
#>
[CmdletBinding()]
param(
  # Directory containing the *.trx artifacts gathered from every shard job.
  [Parameter(Mandatory = $true)]
  [string]$TrxDir,

  # The assemblies that MUST have run, as file names (e.g. "Excalibur.Outbox.Tests.dll" or
  # "Excalibur.Outbox.Tests"). Matched case-insensitively against trx <UnitTest storage="">.
  # Supply the union of the shard .slnf test projects. Empty = set assertion skipped (NOT
  # recommended in CI — the set assertion is the f37o9a/ar6w5k guard).
  [string[]]$ExpectedAssemblies = @(),

  # When true (default), a RED result throws (non-zero exit). When false, report only.
  [bool]$Enforce = $true,

  # Non-vacuity floor (testing-patterns §3 / SA #28375): the gate REFUSES to evaluate (exit 2) when the
  # EXPECTED set is empty or smaller than this floor, so a mis-derived/empty EXPECTED cannot pass the
  # subset check vacuously GREEN (the fmvdpg/tpu8m2 "gate that cannot fail" trap). CI passes the known
  # blocking-tier size; default 1 forbids an empty expected set outright.
  [int]$MinExpectedAssemblies = 1,

  # Optional report output directory.
  [string]$OutDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Emit to stderr WITHOUT raising a terminating error (Write-Error under `Stop` throws and pre-empts the
# explicit `exit N`, collapsing the three-value exit to 1). This keeps ERROR=2 / RED=1 distinguishable.
function Write-GateError([string]$message) {
  [Console]::Error.WriteLine("ERROR: $message")
}

function ConvertTo-AssemblyKey([string]$storageOrName) {
  # Normalize a storage path or bare name to a lowercase assembly file name.
  if ([string]::IsNullOrWhiteSpace($storageOrName)) { return "" }
  $leaf = Split-Path -Leaf ($storageOrName -replace '\\', '/')
  if (-not $leaf.ToLowerInvariant().EndsWith(".dll")) { $leaf = "$leaf.dll" }
  return $leaf.ToLowerInvariant()
}

if (-not (Test-Path $TrxDir)) {
  Write-GateError "TrxDir not found: $TrxDir"
  exit 2
}

$trxFiles = @(Get-ChildItem -Path $TrxDir -Filter "*.trx" -Recurse -File -ErrorAction SilentlyContinue)
if ($trxFiles.Count -eq 0) {
  Write-GateError "No TRX result files found under '$TrxDir' — the test run did not produce results (cannot compute a sound total)."
  exit 2
}

# assemblyKey -> @{ Tests = @{ testName -> $true(failed)/$false(passed) }; Trx = [set of trx names] }
$assemblies = @{}

foreach ($trx in $trxFiles) {
  try {
    [xml]$xml = Get-Content -LiteralPath $trx.FullName -Raw
  }
  catch {
    Write-GateError "Unreadable TRX '$($trx.Name)': $($_.Exception.Message) (cannot compute a sound total)."
    exit 2
  }

  # testId -> assemblyKey  (from <TestDefinitions><UnitTest id="" storage="">)
  #
  # BOTH property hops are guarded because a TRX from a run that matched NO tests contains a
  # ResultSummary and nothing else -- no <TestDefinitions>, no <Results>. Under
  # Set-StrictMode -Version Latest (line 58) reading an absent property is a TERMINATING error, so
  # one such file aborted the whole aggregation with "The property 'TestDefinitions' cannot be found
  # on this object" and the shard reported failure without validating anything.
  #
  # Zero-test TRX files are normal here: an assembly with no test matching the shard's Category
  # filter still runs and still writes a TRX. Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures
  # produces one on every run by design -- it supplies fixture types and declares no tests.
  #
  # Skipping is correct rather than lenient: such a file contributes no definitions and no results,
  # so it cannot change any total. The expected-assembly count below is what catches an assembly that
  # SHOULD have produced results and did not -- that check stays untouched.
  $testIdToAssembly = @{}
  $defsNode = if ($xml.TestRun.PSObject.Properties.Name -contains 'TestDefinitions') { $xml.TestRun.TestDefinitions } else { $null }
  $defs = if ($null -ne $defsNode -and $defsNode.PSObject.Properties.Name -contains 'UnitTest') { @($defsNode.UnitTest) } else { @() }
  foreach ($def in $defs) {
    if ($null -eq $def) { continue }
    $key = ConvertTo-AssemblyKey $def.storage
    if ($key -ne "" -and $null -ne $def.id) {
      $testIdToAssembly[$def.id] = $key
    }
  }

  # Same guard, same reason as TestDefinitions above: a zero-test TRX has no <Results> element either,
  # and under StrictMode that absent property is a terminating error rather than an empty set.
  $resultsNode = if ($xml.TestRun.PSObject.Properties.Name -contains 'Results') { $xml.TestRun.Results } else { $null }
  $results = if ($null -ne $resultsNode -and $resultsNode.PSObject.Properties.Name -contains 'UnitTestResult') { @($resultsNode.UnitTestResult) } else { @() }
  foreach ($res in $results) {
    if ($null -eq $res) { continue }
    $key = if ($res.testId -and $testIdToAssembly.ContainsKey($res.testId)) { $testIdToAssembly[$res.testId] } else { "" }
    if ($key -eq "") { continue }   # result with no resolvable assembly — skip (set-check catches drops)

    if (-not $assemblies.ContainsKey($key)) {
      $assemblies[$key] = @{ Tests = @{}; Trx = New-Object System.Collections.Generic.HashSet[string] }
    }
    [void]$assemblies[$key].Trx.Add($trx.Name)

    $testName = "$($res.testName)"
    $failed = ($res.outcome -ne "Passed" -and $res.outcome -ne "NotExecuted")
    if ($assemblies[$key].Tests.ContainsKey($testName)) {
      # Same test seen in another shard — fail-closed: FAILED if it failed anywhere (1ltu6f edge).
      if ($failed) { $assemblies[$key].Tests[$testName] = $true }
    }
    else {
      $assemblies[$key].Tests[$testName] = $failed
    }
  }
}

# --- Distinct (deduped) aggregation ---
$distinctAssemblies = @($assemblies.Keys | Sort-Object)
$distinctTestCount = 0
$distinctFailedCount = 0
$failedAssemblies = New-Object System.Collections.Generic.List[string]
# assemblyKey -> the sorted names of the tests that failed in it, so the RED can name them.
$failedTestsByAssembly = @{}

foreach ($key in $distinctAssemblies) {
  $tests = $assemblies[$key].Tests
  $distinctTestCount += $tests.Count
  # Keep the NAMES, not just the count. This enumeration already has them; the previous form took
  # .Count and discarded the keys, so a RED said "excalibur.dispatch.tests.dll (1 failed)" and left
  # the reader to go hunting across 224 TRX files for which test it was. A gate that knows exactly
  # what failed and reports only how many is withholding the one fact the failure exists to convey.
  $failedNames = @($tests.GetEnumerator() | Where-Object { $_.Value } | ForEach-Object { $_.Key } | Sort-Object)
  $af = $failedNames.Count
  if ($af -gt 0) {
    $distinctFailedCount += $af
    $failedAssemblies.Add("$key ($af failed)")
    $failedTestsByAssembly[$key] = $failedNames
  }
}

# --- Expected-set assertion (f37o9a / ar6w5k). Invariant: PRODUCED ⊇ EXPECTED (every expected
#     assembly must appear in the produced set; a missing/non-compiling one is absent → RED). ---
$expectedKeys = @($ExpectedAssemblies | ForEach-Object { ConvertTo-AssemblyKey $_ } | Where-Object { $_ -ne "" } | Sort-Object -Unique)

# Non-vacuity floor: an empty / too-small EXPECTED makes the subset check vacuously GREEN. Refuse (exit 2).
if ($expectedKeys.Count -lt $MinExpectedAssemblies) {
  Write-GateError "EXPECTED assembly set has $($expectedKeys.Count) entries, below the non-vacuity floor of $MinExpectedAssemblies — refusing to evaluate (an empty/mis-derived expected set would pass vacuously). Supply -ExpectedAssemblies."
  exit 2
}

$missing = New-Object System.Collections.Generic.List[string]
foreach ($ek in $expectedKeys) {
  if (-not $assemblies.ContainsKey($ek)) {
    $missing.Add($ek)
  }
}

# --- Report ---
Write-Host "Shard result aggregation (distinct-by-assembly):"
Write-Host "  TRX files parsed        : $($trxFiles.Count)"
Write-Host "  Distinct assemblies     : $($distinctAssemblies.Count)"
Write-Host "  Distinct tests          : $distinctTestCount"
Write-Host "  Distinct failed tests   : $distinctFailedCount"
Write-Host "  Expected assemblies     : $($expectedKeys.Count)"
Write-Host "  Missing (no results)    : $($missing.Count)"

$multiShard = @($distinctAssemblies | Where-Object { $assemblies[$_].Trx.Count -gt 1 })
if ($multiShard.Count -gt 0) {
  Write-Host "  Multi-shard (counted once):"
  foreach ($m in $multiShard) { Write-Host "    - $m -> $($assemblies[$m].Trx.Count) shards" }
}

if ($OutDir -ne "") {
  New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
  $report = [pscustomobject]@{
    TrxFilesParsed      = $trxFiles.Count
    DistinctAssemblies  = $distinctAssemblies.Count
    DistinctTests       = $distinctTestCount
    DistinctFailedTests = $distinctFailedCount
    ExpectedAssemblies  = $expectedKeys.Count
    MissingAssemblies   = @($missing)
    FailedAssemblies    = @($failedAssemblies)
    MultiShardAssemblies = @($multiShard)
  }
  $report | ConvertTo-Json -Depth 5 | Out-File -FilePath (Join-Path $OutDir "shard-results.json") -Encoding UTF8
}

# --- Verdict ---
$red = $false
if ($missing.Count -gt 0) {
  $red = $true
  Write-Host ""
  Write-Host "RED: $($missing.Count) expected assembly(ies) produced NO results (did not compile or were not run):"
  foreach ($m in $missing) { Write-Host "  - $m" }
}
if ($distinctFailedCount -gt 0) {
  $red = $true
  Write-Host ""
  Write-Host "RED: $distinctFailedCount distinct test failure(s) across $($failedAssemblies.Count) assembly(ies):"
  foreach ($f in $failedAssemblies) { Write-Host "  - $f" }
  Write-Host ""
  Write-Host "Failing tests:"
  foreach ($key in ($failedTestsByAssembly.Keys | Sort-Object)) {
    Write-Host "  $key"
    foreach ($t in $failedTestsByAssembly[$key]) { Write-Host "    * $t" }
  }
}

if ($red) {
  if ($Enforce) {
    Write-GateError "Shard result aggregation FAILED (see above)."
    exit 1
  }
  Write-Warning "Shard result aggregation found issues (Enforce=`$false — not failing)."
  exit 1
}

Write-Host ""
Write-Host "GREEN: full expected assembly set ran; zero distinct failures."
exit 0
