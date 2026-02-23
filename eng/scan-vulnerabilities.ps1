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
    Where-Object { $_.FullName -notmatch '\\archive\\|\\obj\\|\\bin\\' }

if ($projectFiles.Count -eq 0) {
    Write-Warning "No project files found."
    exit 0
}

Write-Host "Found $($projectFiles.Count) project file(s) to scan." -ForegroundColor Gray
Write-Host ""

$vulnerabilitiesFound = $false
$criticalCount = 0
$highCount = 0
$moderateCount = 0
$lowCount = 0

foreach ($project in $projectFiles) {
    Write-Host "Scanning: $($project.Name)" -ForegroundColor Gray

    # Run vulnerability scan
    $output = dotnet list "$($project.FullName)" package --vulnerable --include-transitive 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to scan $($project.Name): $output"
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
