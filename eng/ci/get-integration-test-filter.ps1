[CmdletBinding()]
param(
    [AllowEmptyString()][string]$Leg = '',
    [AllowEmptyString()][string]$ReleaseVersion = '',
    [AllowEmptyString()][string]$SolutionFilter = '',
    [switch]$IncludeDeferredCloudChecks
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Alpha22KeyVaultScope {
    param([string]$Version, [string]$Source, [bool]$IncludeCloud, $Metadata)
    $none = [pscustomobject]@{ suffix = ''; plannedDeferredCount = 0; deferredTests = @(); disposition = 'full-profile' }
    if ($Version -cne '10.0.0-alpha.22' -or $Source.Replace('\', '/') -cne 'eng/ci/shards/IntegrationTests-Dispatch.slnf' -or $IncludeCloud) { return $none }
    if ($null -eq $Metadata -or $Metadata.releaseVersion -cne $Version -or
        $Metadata.solutionFilter -cne 'eng/ci/shards/IntegrationTests-Dispatch.slnf' -or
        $Metadata.disposition -cne 'deferred-unverified' -or @($Metadata.tests).Count -ne 11) {
        throw 'Invalid alpha.22 Key Vault disposition metadata.'
    }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $prefix = 'Excalibur.Dispatch.Integration.Tests.Compliance.Azure.AzureKeyVaultProviderIntegrationShould.'
    foreach ($test in $Metadata.tests) {
        $id = [guid]::Empty
        if (-not [guid]::TryParseExact([string]$test.id, 'D', [ref]$id) -or -not $ids.Add([string]$test.id) -or
            [string]$test.fullyQualifiedName -cnotmatch ('^' + [regex]::Escape($prefix) + '[A-Za-z_][A-Za-z0-9_]*$') -or
            -not $names.Add([string]$test.fullyQualifiedName)) { throw 'Invalid or duplicate alpha.22 Key Vault identity.' }
    }
    $suffix = (@($Metadata.tests | ForEach-Object { 'FullyQualifiedName!=' + $_.fullyQualifiedName }) -join '&')
    return [pscustomobject]@{ suffix = $suffix; plannedDeferredCount = 11; deferredTests = @($Metadata.tests); disposition = 'deferred-unverified' }
}
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

$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'alpha22-keyvault-unverified.json') -Raw | ConvertFrom-Json
$scope = Get-Alpha22KeyVaultScope -Version $ReleaseVersion -Source $SolutionFilter -IncludeCloud ([bool]$IncludeDeferredCloudChecks) -Metadata $metadata
$fullInfo = & (Join-Path $PSScriptRoot 'get-test-filter.ps1') -BaseFilter $baseFilter -Mode Exclude -AsJson | ConvertFrom-Json
$result = [ordered]@{}
foreach ($property in $fullInfo.PSObject.Properties) { $result[$property.Name] = $property.Value }
$result['fullFilter'] = $fullInfo.filter
if ($scope.plannedDeferredCount -gt 0) { $result['filter'] = '(' + $fullInfo.filter + ')&' + $scope.suffix }
$result['releaseVersion'] = $ReleaseVersion
$result['solutionFilter'] = $SolutionFilter
$result['disposition'] = $scope.disposition
$result['plannedDeferredCount'] = $scope.plannedDeferredCount
$result['deferredTests'] = $scope.deferredTests
# Planned selection is not an execution count: actual SDK census/result evidence must establish the partition.
$result | ConvertTo-Json -Depth 8 -Compress
