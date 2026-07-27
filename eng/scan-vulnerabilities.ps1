#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Scans for vulnerable NuGet packages in the solution.

.DESCRIPTION
    Uses 'dotnet list package --vulnerable --include-transitive' to detect
    known security vulnerabilities in direct and transitive dependencies.

    Enforces R20.10 (Security gate): 0 critical/high vulnerabilities allowed.

.PARAMETER FailOnVulnerabilities
    If true (default), fails the script if vulnerabilities are detected.

.EXAMPLE
    .\eng\scan-vulnerabilities.ps1
    .\eng\scan-vulnerabilities.ps1 -FailOnVulnerabilities $false
#>

[CmdletBinding()]
param(
    [Parameter()]
    [bool]$FailOnVulnerabilities = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "🔍 Scanning for vulnerable NuGet packages..." -ForegroundColor Cyan
Write-Host ""

# Find all project files
$projectFiles = Get-ChildItem -Path "$PSScriptRoot\.." -Recurse -Filter "*.csproj" |
    Where-Object { $_.FullName -notmatch '\\archive\\|\\obj\\|\\bin\\|\\templates\\' }

# templates\ holds `dotnet new` TEMPLATE CONTENT -- placeholder projects that belong to no solution and
# cannot be restored in place, so `dotnet list package` can never evaluate them. Measured: all ten of
# them, and only them, hit the REFUSE below on a full run. Excluding them is deliberate and narrow; a
# gate that fails on every single run is a gate somebody switches off, and then it protects nothing.
#
# THIS IS A STATED COVERAGE GAP, NOT COVERAGE. A vulnerable pin inside a template still reaches
# consumers through `dotnet new`, and nothing here checks that. It needs a control that scans an
# INSTANTIATED template. It is not silently folded into this scan's PASS.

if ($projectFiles.Count -eq 0) {
    # A scan over zero projects finds zero vulnerabilities, trivially. Exiting 0 there reports a clean
    # tree on the strength of having looked at nothing -- the safety property satisfied by inaction.
    Write-Host "REFUSE: no project files found to scan. An empty scan is not a clean scan." -ForegroundColor Red
    exit 2
}

Write-Host "Found $($projectFiles.Count) project file(s) to scan." -ForegroundColor Gray
Write-Host ""

$vulnerabilitiesFound = $false
$unscannable = @()
$criticalCount = 0
$highCount = 0
$moderateCount = 0
$lowCount = 0

foreach ($project in $projectFiles) {
    Write-Host "Scanning: $($project.Name)" -ForegroundColor Gray

    # Run vulnerability scan, with a bounded retry.
    #
    # A single attempt conflates two different failures. `dotnet list package` reads the restore assets,
    # so it fails TRANSIENTLY when something else is writing them (a concurrent build) or when the feed
    # blips -- and it fails GENUINELY when a project was never restored at all. Measured here: a batch
    # run refused six projects, and re-running one of them by hand immediately succeeded. Refusing on
    # the first failure would make this gate flaky, and a flaky security gate gets switched off.
    #
    # So: retry a bounded number of times, then refuse LOUDLY. Transient clears within the bound;
    # genuine persists and still fails closed.
    $attempt = 0
    $maxAttempts = 3
    $output = $null
    $scanExit = 1
    while ($attempt -lt $maxAttempts) {
        $attempt++
        $output = dotnet list "$($project.FullName)" package --vulnerable --include-transitive 2>&1
        $scanExit = $LASTEXITCODE
        if ($scanExit -eq 0) { break }
        if ($attempt -lt $maxAttempts) {
            Write-Host "  retry $attempt/$maxAttempts for $($project.Name) (exit $scanExit)" -ForegroundColor DarkYellow
            Start-Sleep -Seconds 2
        }
    }

    if ($scanExit -ne 0) {
        # A project that CANNOT be scanned is not a project with no vulnerabilities. This previously
        # warned and continued, so the run still ended `exit 0` -- a project outside the restored
        # solution filter (benchmarks, samples) fails `dotnet list` for want of assets and was counted
        # as clean. That is fail-open under exactly the condition nobody inspects: the scanner is
        # loudest about what it CAN read and silent about what it could not.
        Write-Host "REFUSE: could not scan $($project.Name) -- $output" -ForegroundColor Red
        $unscannable += $project.Name
        continue
    }

    # Parse output for vulnerabilities.
    # Do not key off raw severity words alone: terms like "following" contain "Low"
    # and create false positives.
    $outputText = $output | Out-String
    $hasVulnerabilitySignal =
        $outputText -match '(?im)has the following vulnerable packages' -or
        $outputText -match '(?im)\bGHSA-[0-9A-Za-z-]+\b' -or
        $outputText -match '(?im)\bCVE-\d{4}-\d+\b'

    if ($hasVulnerabilitySignal) {
        $vulnerabilitiesFound = $true
        Write-Host "❌ Vulnerabilities detected in $($project.Name):" -ForegroundColor Red
        Write-Host $outputText -ForegroundColor Yellow

        # Count severity levels
        # dotnet list prints severity in a table column; use boundaries to avoid substring matches.
        $criticalCount += ([regex]::Matches($outputText, '(?im)\bCritical\b')).Count
        $highCount += ([regex]::Matches($outputText, '(?im)\bHigh\b')).Count
        $moderateCount += ([regex]::Matches($outputText, '(?im)\bModerate\b')).Count
        $lowCount += ([regex]::Matches($outputText, '(?im)\bLow\b')).Count
    }
    else {
        Write-Host "✅ No vulnerabilities detected" -ForegroundColor Green
    }

    Write-Host ""
}

# Summary
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📊 Vulnerability Scan Summary" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Critical:  $criticalCount" -ForegroundColor $(if ($criticalCount -gt 0) { "Red" } else { "Green" })
Write-Host "High:      $highCount" -ForegroundColor $(if ($highCount -gt 0) { "Red" } else { "Green" })
Write-Host "Moderate:  $moderateCount" -ForegroundColor $(if ($moderateCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "Low:       $lowCount" -ForegroundColor $(if ($lowCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# An unscannable project is a coverage hole, and a coverage hole is reported BEFORE any verdict --
# never folded into a pass. REFUSE is not PASS.
if ($unscannable.Count -gt 0) {
    Write-Host ""
    Write-Host "SECURITY SCAN REFUSED - $($unscannable.Count) project(s) could not be scanned:" -ForegroundColor Red
    foreach ($name in $unscannable) { Write-Host "  - $name" -ForegroundColor Red }
    Write-Host "These projects were NOT checked for vulnerable packages. Restore them (they may sit" -ForegroundColor Yellow
    Write-Host "outside the solution filter this job restored) and re-run, or exclude them deliberately." -ForegroundColor Yellow
    exit 2
}

# Enforce R20.10: 0 critical/high vulnerabilities
if ($vulnerabilitiesFound) {
    $blockingCount = $criticalCount + $highCount

    if ($blockingCount -gt 0) {
        Write-Host "❌ SECURITY GATE FAILED (R20.10)" -ForegroundColor Red
        Write-Host "Found $blockingCount critical/high severity vulnerabilities." -ForegroundColor Red
        Write-Host ""
        Write-Host "Action Required:" -ForegroundColor Yellow
        Write-Host "  1. Update vulnerable packages to patched versions" -ForegroundColor Yellow
        Write-Host "  2. If no patch available, document waiver in:" -ForegroundColor Yellow
        Write-Host "     management/security/vulnerability-waivers.md" -ForegroundColor Yellow
        Write-Host ""

        if ($FailOnVulnerabilities) {
            exit 1
        }
    }
    else {
        Write-Host "⚠️  Moderate/Low vulnerabilities detected (non-blocking)" -ForegroundColor Yellow
        Write-Host "Consider updating packages when possible." -ForegroundColor Yellow
    }
}
else {
    Write-Host "✅ No vulnerabilities detected - Security gate passed" -ForegroundColor Green
}

exit 0
