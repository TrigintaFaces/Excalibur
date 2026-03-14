#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Release rehearsal -- validates the full shipping pipeline matches release.yml behavior.

.DESCRIPTION
    Executes the canonical release validation pipeline using ShippingOnly.slnf as the single
    shipping graph. This is the "can we ship?" check that mirrors the actual release workflow.

    Steps:
    1. Restore (ShippingOnly.slnf)
    2. Build (Release, ShippingOnly.slnf)
    3. Pack (local feed)
    4. Validate package composition
    5. Validate NuSpec dependencies
    6. Public API baseline audit
    7. Validate governance stack

    Sprint 639 C.1 (bd-bvc8e).

.PARAMETER OutDir
    Output directory for rehearsal artifacts and report. Defaults to ReleaseRehearsalReport.

.PARAMETER NoBuild
    Skip restore and build steps (use if already built in Release configuration).

.PARAMETER StopOnFirstFailure
    Stop execution at the first failing step instead of running all steps.

.EXAMPLE
    .\release-rehearsal.ps1

.EXAMPLE
    .\release-rehearsal.ps1 -NoBuild

.EXAMPLE
    .\release-rehearsal.ps1 -StopOnFirstFailure
#>
param(
    [string]$OutDir = 'ReleaseRehearsalReport',
    [switch]$NoBuild,
    [switch]$StopOnFirstFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ShippingSlnf = Join-Path $RepoRoot 'eng/ci/shards/ShippingOnly.slnf'
$StartTime = [DateTime]::UtcNow

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

if (-not (Test-Path $ShippingSlnf)) {
    throw "ShippingOnly.slnf not found at: $ShippingSlnf"
}

# --- Step tracking ---
$steps = @()
$failureCount = 0

function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    $stepStart = [DateTime]::UtcNow
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  STEP: $Name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $result = [pscustomobject]@{
        Name = $Name
        Status = 'skipped'
        Duration = [TimeSpan]::Zero
        Error = $null
    }

    try {
        & $Action
        $result.Status = 'passed'
        Write-Host "  PASSED: $Name" -ForegroundColor Green
    }
    catch {
        $result.Status = 'failed'
        $result.Error = $_.Exception.Message
        $script:failureCount++
        Write-Host "  FAILED: $Name -- $($_.Exception.Message)" -ForegroundColor Red

        if ($StopOnFirstFailure) {
            $result.Duration = ([DateTime]::UtcNow - $stepStart)
            $script:steps += $result
            throw "Release rehearsal stopped at step '$Name': $($_.Exception.Message)"
        }
    }

    $result.Duration = ([DateTime]::UtcNow - $stepStart)
    $script:steps += $result
}

# --- Step 1: Restore ---
if (-not $NoBuild) {
    Run-Step 'Restore (ShippingOnly.slnf)' {
        $output = dotnet restore $ShippingSlnf --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed (exit code $LASTEXITCODE): $($output | Select-Object -Last 5 | Out-String)"
        }
    }

    # --- Step 2: Build ---
    Run-Step 'Build (Release, ShippingOnly.slnf)' {
        $output = dotnet build $ShippingSlnf --configuration Release --no-restore --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed (exit code $LASTEXITCODE): $($output | Select-Object -Last 10 | Out-String)"
        }
    }
}
else {
    Write-Host "Skipping restore/build steps (-NoBuild specified)." -ForegroundColor Yellow
}

# --- Step 3: Pack ---
Run-Step 'Pack (local feed)' {
    $packScript = Join-Path $RepoRoot 'eng/pack-local.ps1'
    if (-not (Test-Path $packScript)) {
        throw "pack-local.ps1 not found at: $packScript"
    }
    $packArgs = @()
    if ($NoBuild) { $packArgs += '-NoBuild' }
    & $packScript @packArgs
    if ($LASTEXITCODE -ne 0) {
        throw "pack-local.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 4: Validate package composition ---
Run-Step 'Validate package composition' {
    $compScript = Join-Path $RepoRoot 'eng/validate-package-composition.ps1'
    if (-not (Test-Path $compScript)) {
        throw "validate-package-composition.ps1 not found at: $compScript"
    }
    & $compScript -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "validate-package-composition.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 5: Validate NuSpec dependencies ---
Run-Step 'Validate NuSpec dependencies' {
    $nuspecScript = Join-Path $RepoRoot 'eng/ci/validate-nuspec-dependencies.ps1'
    if (-not (Test-Path $nuspecScript)) {
        throw "validate-nuspec-dependencies.ps1 not found at: $nuspecScript"
    }
    & $nuspecScript
    if ($LASTEXITCODE -ne 0) {
        throw "validate-nuspec-dependencies.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 6: Public API baseline audit ---
Run-Step 'Public API baseline audit' {
    $apiScript = Join-Path $RepoRoot 'eng/ci/public-api-baseline-audit.ps1'
    if (-not (Test-Path $apiScript)) {
        throw "public-api-baseline-audit.ps1 not found at: $apiScript"
    }
    & $apiScript
    if ($LASTEXITCODE -ne 0) {
        throw "public-api-baseline-audit.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 7: Validate governance stack ---
Run-Step 'Validate governance stack' {
    $govScript = Join-Path $RepoRoot 'eng/ci/validate-governance-stack.ps1'
    if (-not (Test-Path $govScript)) {
        throw "validate-governance-stack.ps1 not found at: $govScript"
    }
    & $govScript
    if ($LASTEXITCODE -ne 0) {
        throw "validate-governance-stack.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Report ---
$totalDuration = ([DateTime]::UtcNow - $StartTime)
$passedCount = @($steps | Where-Object { $_.Status -eq 'passed' }).Count
$failedCount = @($steps | Where-Object { $_.Status -eq 'failed' }).Count
$skippedCount = @($steps | Where-Object { $_.Status -eq 'skipped' }).Count
$overallStatus = if ($failedCount -eq 0) { 'PASSED' } else { 'FAILED' }

$summaryPath = Join-Path $OutDir 'summary.md'
$jsonPath = Join-Path $OutDir 'release-rehearsal-report.json'

$summary = @(
    '# Release Rehearsal Report',
    '',
    "- **Status:** $overallStatus",
    "- **Date:** $($StartTime.ToString('yyyy-MM-dd HH:mm:ss')) UTC",
    "- **Duration:** $($totalDuration.ToString('hh\:mm\:ss'))",
    "- **Shipping graph:** ShippingOnly.slnf",
    "- **Steps:** $($steps.Count) total ($passedCount passed, $failedCount failed, $skippedCount skipped)",
    '',
    '## Step Results',
    '',
    '| Step | Status | Duration |',
    '|------|--------|----------|'
)

foreach ($step in $steps) {
    $icon = switch ($step.Status) {
        'passed' { 'PASS' }
        'failed' { 'FAIL' }
        'skipped' { 'SKIP' }
    }
    $summary += "| $($step.Name) | $icon | $($step.Duration.ToString('mm\:ss')) |"
}

if ($failedCount -gt 0) {
    $summary += ''
    $summary += '## Failures'
    $summary += ''
    foreach ($step in ($steps | Where-Object { $_.Status -eq 'failed' })) {
        $summary += "### $($step.Name)"
        $summary += ''
        $summary += "``$($step.Error)``"
        $summary += ''
    }
}

$summary | Out-File -FilePath $summaryPath -Encoding UTF8

$report = [pscustomobject]@{
    status = $overallStatus
    date = $StartTime.ToString('o')
    durationSeconds = [int]$totalDuration.TotalSeconds
    shippingGraph = 'eng/ci/shards/ShippingOnly.slnf'
    steps = $steps
}
$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  RELEASE REHEARSAL: $overallStatus" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  Duration: $($totalDuration.ToString('hh\:mm\:ss'))" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  Report: $summaryPath" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "========================================" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })

if ($failedCount -gt 0) {
    exit 1
}
