[CmdletBinding()]
param([AllowEmptyString()][string]$Leg = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$baseFilter = '(Category=Integration|Category=EndToEnd)&Infrastructure!=CosmosEmulator'

# LEG NARROWING. The excalibur suite is one assembly pinned to maxParallelThreads=1 by its
# own xunit.runner.json, so it is split across jobs by namespace instead. Both halves of
# the split are derived from ONE list -- a leg runs its prefix, 'rest' runs the complement
# of every prefix -- so a typo moves tests between legs and cannot drop them on the floor.
# An unknown leg name THROWS rather than falling through to the unnarrowed filter, because
# falling through would silently run the whole suite on four jobs and still look green.

if ($leg) {
  $root = 'Excalibur.Integration.Tests.'
  $prefixes = [ordered]@{}
  foreach ($line in Get-Content eng/ci/integration-excalibur-legs.txt) {
    $t = $line.Trim()
    if (-not $t -or $t.StartsWith('#') -or $t -notmatch '=') { continue }
    $kv = $t.Split('=', 2)
    $prefixes[$kv[0].Trim()] = $kv[1].Trim()
  }
  if ($prefixes.Count -eq 0) { throw 'no leg prefixes parsed from eng/ci/integration-excalibur-legs.txt' }
  if ($leg -eq 'rest') {
    foreach ($v in $prefixes.Values) { $baseFilter += "&FullyQualifiedName!~$root$v" }
  } elseif ($prefixes.Contains($leg)) {
    $baseFilter += "&FullyQualifiedName~$root$($prefixes[$leg])"
  } else {
    throw "unknown leg '$leg' -- not in eng/ci/integration-excalibur-legs.txt (known: $($prefixes.Keys -join ', '), rest)"
  }
  Write-Host "Leg '$leg' narrows the filter to: $baseFilter"
}

& (Join-Path $PSScriptRoot "get-test-filter.ps1") -BaseFilter $baseFilter -Mode Exclude -AsJson
