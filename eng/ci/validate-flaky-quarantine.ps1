#!/usr/bin/env pwsh
<#
.SYNOPSIS
Validates flaky-test quarantine metadata and expiry policy.
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

for ($i = 0; $i -lt $tests.Count; $i++) {
    $entry = $tests[$i]
    $prefix = "tests[$i]"

    $fqn = if ($null -ne $entry.fullyQualifiedName) { [string]$entry.fullyQualifiedName } else { '' }
    $owner = if ($null -ne $entry.owner) { [string]$entry.owner } else { '' }
    $issue = if ($null -ne $entry.issue) { [string]$entry.issue } else { '' }
    $reason = if ($null -ne $entry.reason) { [string]$entry.reason } else { '' }
    $addedOnRaw = if ($null -ne $entry.addedOn) { [string]$entry.addedOn } else { '' }
    $expiresOnRaw = if ($null -ne $entry.expiresOn) { [string]$entry.expiresOn } else { '' }

    if ([string]::IsNullOrWhiteSpace($fqn)) { $invalid.Add("$prefix missing fullyQualifiedName") }
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
            $overAge.Add("$fqn ($age days > $maxDays)")
        }

        if ($expiresOn -lt $today) {
            $expired.Add("$fqn (expired $($expiresOn.ToString('yyyy-MM-dd')))")
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($fqn)) {
        $fqns += $fqn
    }
}

$duplicateFqns = @(
    $fqns |
        Group-Object |
        Where-Object { $_.Count -gt 1 } |
        ForEach-Object { "$($_.Name) (x$($_.Count))" }
)

$summaryPath = Join-Path $OutDir 'summary.md'
$jsonPath = Join-Path $OutDir 'flaky-tests-quarantine-report.json'

$status = if ($invalid.Count -eq 0 -and $expired.Count -eq 0 -and $overAge.Count -eq 0 -and $duplicateFqns.Count -eq 0) { 'pass' } else { 'fail' }

$summary = @(
    '# Flaky Test Quarantine Validation',
    '',
    "- Manifest: $resolvedQuarantineFile",
    "- Entries: $($tests.Count)",
    "- Invalid entries: $($invalid.Count)",
    "- Expired entries: $($expired.Count)",
    "- Over-age entries (> $maxDays days): $($overAge.Count)",
    "- Duplicate test entries: $($duplicateFqns.Count)",
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

if ($duplicateFqns.Count -gt 0) {
    $summary += '## Duplicate Entries'
    $summary += ''
    $summary += ($duplicateFqns | ForEach-Object { "- $_" })
    $summary += ''
}

if ($status -eq 'pass') {
    $summary += 'All quarantine entries are valid and within policy.'
}

$summary | Out-File -FilePath $summaryPath -Encoding UTF8

$report = [pscustomobject]@{
    manifestPath = $resolvedQuarantineFile
    entryCount = $tests.Count
    maxQuarantineDays = $maxDays
    status = $status
    invalid = $invalid
    expired = $expired
    overAge = $overAge
    duplicates = $duplicateFqns
}

$report | ConvertTo-Json -Depth 8 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host "Wrote quarantine summary: $summaryPath"
Write-Host "Wrote quarantine report: $jsonPath"

if ($Enforce -and $status -ne 'pass') {
    throw 'Flaky test quarantine validation failed.'
}
