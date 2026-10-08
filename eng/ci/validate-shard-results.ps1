#requires -Version 7.0
<#
.SYNOPSIS
  Reject incomplete or non-passing raw test results before distinct reporting.
.DESCRIPTION
  Every invocation must have a supported, internally consistent TRX with only passing
  executed results. Skips, aborts, duplicate artifacts/identities, runner errors, malformed
  counters and unexecuted definitions are refused before any cross-shard deduplication.
  ExpectedAssemblies additionally rejects absent assemblies. This assembly-presence check
  alone does not prove the independently expected test set, TFM, OS or provider coverage.
  Exit 0 means raw evidence passed these checks; exit 1 means an expected assembly is absent;
  exit 2 means absent, malformed, unsupported, or non-passing execution evidence.
#>
[CmdletBinding()]
param(
  # Directory containing the *.trx artifacts gathered from every shard job.
  [Parameter(Mandatory = $true)]
  [string]$TrxDir,

  # The assemblies that MUST have run, as file names (e.g. "Excalibur.Outbox.Tests.dll" or
  # "Excalibur.Outbox.Tests"). Matched case-insensitively against trx <UnitTest storage="">.
  # Supply the union of the shard .slnf test projects. Empty = set assertion skipped (NOT
  # recommended in CI — the set assertion is the missing-assembly guard).
  [string[]]$ExpectedAssemblies = @(),

  # JSON string array for process-boundary callers (PowerShell -File cannot pass array argv).
  [string]$ExpectedAssembliesFile = '',

  # When true (default), a RED result throws (non-zero exit). When false, report only.
  [bool]$Enforce = $true,

  # Non-vacuity floor (testing-patterns §3): the gate REFUSES to evaluate (exit 2) when the
  # EXPECTED set is empty or smaller than this floor, so a mis-derived/empty EXPECTED cannot pass the
  # subset check vacuously GREEN (the "gate that cannot fail" trap). CI passes the known
  # blocking-tier size; default 1 forbids an empty expected set outright.
  [int]$MinExpectedAssemblies = 1,

  # Optional report output directory.
  [string]$OutDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if ($ExpectedAssembliesFile) {
  if ($ExpectedAssemblies.Count -gt 0) { throw 'Specify expected assemblies directly or by file, not both.' }
  $expectedFromFile = Get-Content -LiteralPath $ExpectedAssembliesFile -Raw | ConvertFrom-Json -NoEnumerate
  if ($expectedFromFile -isnot [array] -or @($expectedFromFile | Where-Object { $_ -isnot [string] }).Count -gt 0) {
    throw 'Expected assemblies file must contain a JSON string array.'
  }
  $ExpectedAssemblies = $expectedFromFile
}

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

$runIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$trxHashes = [System.Collections.Generic.HashSet[string]]::new()

function Required-Attribute([System.Xml.XmlElement]$node, [string]$name) {
  if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.GetAttribute($name))) {
    throw "Missing required TRX attribute '$name'."
  }
  return $node.GetAttribute($name)
}

foreach ($trx in $trxFiles) {
  try {
    $hash = (Get-FileHash -LiteralPath $trx.FullName -Algorithm SHA256).Hash
    if (-not $trxHashes.Add($hash)) { throw "Duplicate TRX content." }
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($trx.FullName, $settings)
    try {
      $xml = [System.Xml.XmlDocument]::new()
      $xml.XmlResolver = $null
      $xml.Load($reader)
    }
    finally { $reader.Dispose() }
    $run = $xml.DocumentElement
    if ($run.LocalName -ne 'TestRun' -or $run.NamespaceURI -ne 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010') {
      throw "Expected supported TRX TestRun root and namespace."
    }
    $runId = Required-Attribute $run 'id'
    if (-not $runIds.Add($runId)) { throw "Duplicate TestRun identity '$runId'." }
    $ns = [System.Xml.XmlNamespaceManager]::new($xml.NameTable)
    $ns.AddNamespace('t', $run.NamespaceURI)
    foreach ($element in @('ResultSummary', 'Results', 'TestDefinitions')) {
      if ($run.SelectNodes("t:$element", $ns).Count -ne 1) { throw "Expected exactly one $element element." }
    }
    $summary = $run.SelectSingleNode('t:ResultSummary', $ns)
    $runOutcome = Required-Attribute $summary 'outcome'
    if ($runOutcome -notin @('Completed', 'Passed')) {
      throw "Run did not complete successfully: $runOutcome."
    }
    foreach ($info in $summary.SelectNodes('t:RunInfos/t:RunInfo', $ns)) {
      # Observed on completed passing runs with the SDK's blame collector. This diagnostic
      # means no hang occurred; it does not excuse any failed/skipped test or other warning.
      if ($info.GetAttribute('outcome') -cne 'Warning' -or $info.InnerText.Trim() -cne
          "Data collector 'Blame' message: All tests finished running, Sequence file will not be generated.") {
        throw "Run reported unrecognized diagnostics; investigate before accepting results."
      }
    }
    $results = @($run.SelectNodes('t:Results/t:UnitTestResult', $ns))
    $resultNodes = @($run.SelectNodes('t:Results/*', $ns))
    if ($results.Count -ne $resultNodes.Count) { throw "Unsupported result shape." }
    if ($run.SelectNodes('.//t:UnitTestResult', $ns).Count -ne $results.Count -or
        $run.SelectNodes('.//t:InnerResults', $ns).Count -gt 0) { throw "Unsupported nested result shape." }
    if ($summary.SelectNodes('t:Counters', $ns).Count -ne 1) { throw "Expected exactly one Counters element." }
    $counterNode = $summary.SelectSingleNode('t:Counters', $ns)
    $counts = @{}
    foreach ($attribute in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
      $value = Required-Attribute $counterNode $attribute
      $number = 0L
      if (-not [long]::TryParse($value, [ref]$number) -or $number -lt 0) {
        throw "Invalid counter '$attribute'."
      }
      $counts[$attribute] = $number
    }
    foreach ($attribute in $counterNode.Attributes) {
      if ($attribute.Name -in @('total', 'executed', 'passed')) { continue }
      if ($attribute.Name -cnotin @('failed', 'error', 'timeout', 'aborted', 'inconclusive',
          'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning',
          'completed', 'inProgress', 'pending')) { throw "Unsupported counter '$($attribute.Name)'." }
      $number = 0L
      if (-not [long]::TryParse($attribute.Value, [ref]$number) -or $number -ne 0) {
        throw "Non-passing run counter '$($attribute.Name)=$($attribute.Value)'."
      }
    }
    if ($counts.total -ne $results.Count -or $counts.executed -ne $results.Count -or
        $counts.passed -ne $results.Count) {
      throw "Total/executed/passed counters do not match complete passing result population."
    }

    $testIdToAssembly = @{}
    if ($run.SelectNodes('t:TestDefinitions/*', $ns).Count -ne $run.SelectNodes('t:TestDefinitions/t:UnitTest', $ns).Count) {
      throw 'Unsupported test definition shape.'
    }
    foreach ($def in $run.SelectNodes('t:TestDefinitions/t:UnitTest', $ns)) {
      $id = Required-Attribute $def 'id'
      $key = ConvertTo-AssemblyKey (Required-Attribute $def 'storage')
      if ($testIdToAssembly.ContainsKey($id)) { throw "Duplicate test definition '$id'." }
      $testIdToAssembly[$id] = $key
    }
    $resultIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $executionIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($res in $results) {
      $id = Required-Attribute $res 'testId'
      $executionId = Required-Attribute $res 'executionId'
      $testName = Required-Attribute $res 'testName'
      $outcome = Required-Attribute $res 'outcome'
      if ($outcome -ne 'Passed') { throw "Required test '$testName' did not pass: $outcome." }
      if (-not $resultIds.Add($id) -or -not $executionIds.Add($executionId)) {
        throw "Duplicate test/result execution identity for '$testName'."
      }
      if (-not $testIdToAssembly.ContainsKey($id)) { throw "Orphan result '$testName' ($id)." }
      $key = $testIdToAssembly[$id]
      if (-not $assemblies.ContainsKey($key)) {
        $assemblies[$key] = @{ Tests = @{}; Trx = [System.Collections.Generic.HashSet[string]]::new() }
      }
      [void]$assemblies[$key].Trx.Add($trx.FullName)
      # Preserve distinct adapter IDs even if two cases share a display name.
      $assemblies[$key].Tests["$id|$testName"] = $false
    }
    if ($resultIds.Count -ne $testIdToAssembly.Count) {
      throw "Test definitions without executed results."
    }
  }
  catch {
    Write-GateError "TRX '$($trx.Name)' cannot establish complete passing execution: $($_.Exception.Message)"
    exit 2
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

# --- Expected-set assertion. Invariant: PRODUCED ⊇ EXPECTED (every expected
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
