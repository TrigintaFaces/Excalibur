#!/usr/bin/env pwsh
<#
.SYNOPSIS
Validates flaky-test quarantine metadata and expiry policy.
Supports both individual test entries (fullyQualifiedName) and fixture-level entries (fixture).
#>
param(
    [string]$QuarantineFile = 'eng/ci/flaky-tests-quarantine.json',
    [string]$OutDir = 'management/reports/FlakyQuarantineReport',
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 20
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }
    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

function Parse-DateOrNull {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $parsed = $null
    if ([DateTime]::TryParse($Value, [ref]$parsed)) {
        return $parsed.Date
    }

    return $null
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$resolvedQuarantineFile = [IO.Path]::GetFullPath((Join-Path (Get-Location) $QuarantineFile))
if (-not (Test-Path $resolvedQuarantineFile)) {
    throw "Quarantine file not found: $resolvedQuarantineFile"
}

$manifest = ConvertFrom-JsonCompat -Json (Get-Content $resolvedQuarantineFile -Raw) -Depth 20
$tests = @($manifest.tests)
$today = [DateTime]::UtcNow.Date
$maxDays = 30

$invalid = New-Object System.Collections.Generic.List[string]
$expired = New-Object System.Collections.Generic.List[string]
$overAge = New-Object System.Collections.Generic.List[string]

$fqns = @()
$fixtureNames = @()
$fixtureEntries = @()
$testEntries = @()
$activeRewriteHotspots = @()
$blockingTierViolations = @()

for ($i = 0; $i -lt $tests.Count; $i++) {
    $entry = $tests[$i]
    $prefix = "tests[$i]"

    # Determine entry type: fixture-level or individual test
    $isFixture = $null -ne $entry.fixture -and -not [string]::IsNullOrWhiteSpace([string]$entry.fixture)
    $fqn = if ($null -ne $entry.fullyQualifiedName) { [string]$entry.fullyQualifiedName } else { '' }
    $fixture = if ($isFixture) { [string]$entry.fixture } else { '' }
    $owner = if ($null -ne $entry.owner) { [string]$entry.owner } else { '' }
    $issue = if ($null -ne $entry.issue) { [string]$entry.issue } else { '' }
    $reason = if ($null -ne $entry.reason) { [string]$entry.reason } else { '' }
    $addedOnRaw = if ($null -ne $entry.addedOn) { [string]$entry.addedOn } else { '' }
    $expiresOnRaw = if ($null -ne $entry.expiresOn) { [string]$entry.expiresOn } else { '' }
    $tier = if ($null -ne $entry.tier) { [string]$entry.tier } else { 'advisory' }
    $rewriteStatus = if ($null -ne $entry.rewriteStatus) { [string]$entry.rewriteStatus } else { '' }

    # Validate required fields
    if (-not $isFixture -and [string]::IsNullOrWhiteSpace($fqn)) { $invalid.Add("$prefix missing fullyQualifiedName (and no fixture field)") }
    if ($isFixture -and [string]::IsNullOrWhiteSpace($fixture)) { $invalid.Add("$prefix fixture field is empty") }
    if ([string]::IsNullOrWhiteSpace($owner)) { $invalid.Add("$prefix missing owner") }
    if ([string]::IsNullOrWhiteSpace($issue)) { $invalid.Add("$prefix missing issue") }
    if ([string]::IsNullOrWhiteSpace($reason)) { $invalid.Add("$prefix missing reason") }

    $addedOn = Parse-DateOrNull $addedOnRaw
    $expiresOn = Parse-DateOrNull $expiresOnRaw
    if ($null -eq $addedOn) { $invalid.Add("$prefix invalid addedOn '$addedOnRaw'") }
    if ($null -eq $expiresOn) { $invalid.Add("$prefix invalid expiresOn '$expiresOnRaw'") }

    if ($null -ne $addedOn -and $null -ne $expiresOn) {
        if ($expiresOn -lt $addedOn) {
            $invalid.Add("$prefix expiresOn earlier than addedOn")
        }

        $age = ($expiresOn - $addedOn).Days
        if ($age -gt $maxDays) {
            $identifier = if ($isFixture) { $fixture } else { $fqn }
            $overAge.Add("$identifier ($age days > $maxDays)")
        }

        if ($expiresOn -lt $today) {
            $identifier = if ($isFixture) { $fixture } else { $fqn }
            $expired.Add("$identifier (expired $($expiresOn.ToString('yyyy-MM-dd')))")
        }
    }

    if ($isFixture) {
        if (-not [string]::IsNullOrWhiteSpace($fixture)) {
            $fixtureNames += $fixture
            $fixtureEntries += [pscustomobject]@{
                Fixture = $fixture
                Owner = $owner
                Issue = $issue
                Reason = $reason
                Tier = $tier
                RewriteStatus = $rewriteStatus
            }

            # Track rewrite hotspots
            if (-not [string]::IsNullOrWhiteSpace($rewriteStatus) -and $rewriteStatus -ne 'complete') {
                $activeRewriteHotspots += [pscustomobject]@{
                    Fixture = $fixture
                    Status = $rewriteStatus
                    Owner = $owner
                    Issue = $issue
                }
            }

            # Track blocking-tier violations (fixture quarantine in blocking tier)
            if ($tier -eq 'blocking') {
                $blockingTierViolations += $fixture
            }
        }
    } else {
        if (-not [string]::IsNullOrWhiteSpace($fqn)) {
            $fqns += $fqn
            $testEntries += [pscustomobject]@{
                FullyQualifiedName = $fqn
                Owner = $owner
                Issue = $issue
                Tier = $tier
            }
        }
    }
}

$duplicateFqns = @(
    $fqns |
        Group-Object |
        Where-Object { $_.Count -gt 1 } |
        ForEach-Object { "$($_.Name) (x$($_.Count))" }
)

$duplicateFixtures = @(
    $fixtureNames |
        Group-Object |
        Where-Object { $_.Count -gt 1 } |
        ForEach-Object { "$($_.Name) (x$($_.Count))" }
)

$allDuplicates = @($duplicateFqns) + @($duplicateFixtures)

$summaryPath = Join-Path $OutDir 'summary.md'
$jsonPath = Join-Path $OutDir 'flaky-tests-quarantine-report.json'

$status = if ($invalid.Count -eq 0 -and $expired.Count -eq 0 -and $overAge.Count -eq 0 -and $allDuplicates.Count -eq 0 -and $blockingTierViolations.Count -eq 0) { 'pass' } else { 'fail' }

$summary = @(
    '# Flaky Test Quarantine Validation',
    '',
    "- Manifest: $resolvedQuarantineFile",
    "- Total entries: $($tests.Count)",
    "  - Individual tests: $($testEntries.Count)",
    "  - Fixture-level: $($fixtureEntries.Count)",
    "- Invalid entries: $($invalid.Count)",
    "- Expired entries: $($expired.Count)",
    "- Over-age entries (> $maxDays days): $($overAge.Count)",
    "- Duplicate entries: $($allDuplicates.Count)",
    "- Active rewrite hotspots: $($activeRewriteHotspots.Count)",
    "- Blocking-tier violations: $($blockingTierViolations.Count)",
    "- Status: $status",
    ''
)

if ($invalid.Count -gt 0) {
    $summary += '## Invalid Entries'
    $summary += ''
    $summary += ($invalid | ForEach-Object { "- $_" })
    $summary += ''
}

if ($expired.Count -gt 0) {
    $summary += '## Expired Entries'
    $summary += ''
    $summary += ($expired | ForEach-Object { "- $_" })
    $summary += ''
}

if ($overAge.Count -gt 0) {
    $summary += '## Over-Age Entries'
    $summary += ''
    $summary += ($overAge | ForEach-Object { "- $_" })
    $summary += ''
}

if ($allDuplicates.Count -gt 0) {
    $summary += '## Duplicate Entries'
    $summary += ''
    $summary += ($allDuplicates | ForEach-Object { "- $_" })
    $summary += ''
}

if ($activeRewriteHotspots.Count -gt 0) {
    $summary += '## Active Rewrite Hotspots'
    $summary += ''
    $summary += '| Fixture | Status | Owner | Issue |'
    $summary += '|---------|--------|-------|-------|'
    foreach ($h in $activeRewriteHotspots) {
        $summary += "| $($h.Fixture) | $($h.Status) | $($h.Owner) | $($h.Issue) |"
    }
    $summary += ''
}

if ($blockingTierViolations.Count -gt 0) {
    $summary += '## Blocking-Tier Violations'
    $summary += ''
    $summary += 'These fixtures are quarantined but in the blocking tier -- they should be moved to advisory or fixed:'
    $summary += ''
    $summary += ($blockingTierViolations | ForEach-Object { "- $_" })
    $summary += ''
}

if ($status -eq 'pass') {
    $summary += 'All quarantine entries are valid and within policy.'
}

$summary | Out-File -FilePath $summaryPath -Encoding UTF8

$report = [pscustomobject]@{
    manifestPath = $resolvedQuarantineFile
    entryCount = $tests.Count
    testEntryCount = $testEntries.Count
    fixtureEntryCount = $fixtureEntries.Count
    maxQuarantineDays = $maxDays
    status = $status
    invalid = $invalid
    expired = $expired
    overAge = $overAge
    duplicates = $allDuplicates
    activeRewriteHotspots = $activeRewriteHotspots
    blockingTierViolations = $blockingTierViolations
}

$report | ConvertTo-Json -Depth 8 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host "Wrote quarantine summary: $summaryPath"
Write-Host "Wrote quarantine report: $jsonPath"

if ($Enforce -and $status -ne 'pass') {
    throw 'Flaky test quarantine validation failed.'
}
