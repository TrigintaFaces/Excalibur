<#
.SYNOPSIS
    Generates a compliance evidence package (ZIP archive) from collected evidence.

.DESCRIPTION
    This script packages collected compliance evidence into a versioned ZIP archive
    suitable for auditor distribution. Includes:
    - Test results
    - Security scans
    - SBOM artifacts
    - Audit logs
    - Manifest and README

.PARAMETER SourcePath
    Directory containing collected evidence. Default: ./compliance-evidence

.PARAMETER OutputPath
    Directory where the package will be created. Default: ./

.PARAMETER Version
    Package version (e.g., "1.0.0"). Default: current date (YYYY-MM-DD)

.PARAMETER IncludeTimestamp
    Include timestamp in package filename. Default: $true

.EXAMPLE
    .\generate-evidence-package.ps1
    Creates compliance-evidence-<date>.zip

.EXAMPLE
    .\generate-evidence-package.ps1 -Version "1.0.0" -IncludeTimestamp $false
    Creates compliance-evidence-v1.0.0.zip

.NOTES
    Package format: compliance-evidence-v<version>[-<timestamp>].zip
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SourcePath = ".\compliance-evidence",

    [Parameter()]
    [string]$OutputPath = ".\",

    [Parameter()]
    [string]$Version = (Get-Date -Format "yyyy-MM-dd"),

    [Parameter()]
    [bool]$IncludeTimestamp = $true
)

$ErrorActionPreference = "Stop"

# Validate source directory exists
if (-not (Test-Path $SourcePath)) {
    throw "Source directory not found: $SourcePath`nRun collect-evidence.ps1 first."
}

# Validate MANIFEST.json exists
$manifestPath = Join-Path $SourcePath "MANIFEST.json"
if (-not (Test-Path $manifestPath)) {
    throw "MANIFEST.json not found in $SourcePath`nRun collect-evidence.ps1 first."
}

# Generate package filename
$packageName = "compliance-evidence-v$Version"
if ($IncludeTimestamp) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $packageName += "-$timestamp"
}
$packagePath = Join-Path $OutputPath "$packageName.zip"

Write-Host "`n=== Excalibur Evidence Package Generator ===" -ForegroundColor Cyan
Write-Host "Source: $SourcePath" -ForegroundColor Yellow
Write-Host "Package: $packagePath`n" -ForegroundColor Yellow

# Read manifest for summary
$manifest = Get-Content $manifestPath | ConvertFrom-Json

Write-Host "Evidence Summary:" -ForegroundColor Cyan
Write-Host "  Generated: $($manifest.GeneratedAt)" -ForegroundColor Gray
Write-Host "  Run ID: $($manifest.RunId)" -ForegroundColor Gray
Write-Host "  Frameworks: $($manifest.Frameworks)" -ForegroundColor Gray
Write-Host "  Files:" -ForegroundColor Gray
Write-Host "    Test Results: $($manifest.FileCounts.TestResults)" -ForegroundColor Gray
Write-Host "    Security Scans: $($manifest.FileCounts.SecurityScans)" -ForegroundColor Gray
Write-Host "    SBOM: $($manifest.FileCounts.SBOM)" -ForegroundColor Gray
Write-Host "    Audit Logs: $($manifest.FileCounts.AuditLogs)`n" -ForegroundColor Gray

# Create ZIP archive
Write-Host "Creating ZIP archive..." -ForegroundColor Cyan

try {
    # Remove existing package if it exists
    if (Test-Path $packagePath) {
        Remove-Item $packagePath -Force
        Write-Host "  Removed existing package" -ForegroundColor Yellow
    }

    # Create ZIP archive
    Compress-Archive -Path "$SourcePath\*" -DestinationPath $packagePath -CompressionLevel Optimal

    # Get package size
    $packageSize = (Get-Item $packagePath).Length
    $packageSizeMB = [math]::Round($packageSize / 1MB, 2)

    Write-Host "✓ Package created successfully!" -ForegroundColor Green
    Write-Host "`nPackage Details:" -ForegroundColor Cyan
    Write-Host "  Path: $packagePath" -ForegroundColor Gray
    Write-Host "  Size: $packageSizeMB MB" -ForegroundColor Gray
    Write-Host "  Version: $Version" -ForegroundColor Gray

    Write-Host "`nNext Steps:" -ForegroundColor Yellow
    Write-Host "  1. Review package contents: Expand-Archive $packagePath -DestinationPath ./review" -ForegroundColor Gray
    Write-Host "  2. Verify MANIFEST.json and README.md" -ForegroundColor Gray
    Write-Host "  3. Distribute to auditor or compliance team" -ForegroundColor Gray
    Write-Host "  4. Store securely (evidence retention: 6-7 years for HIPAA/GDPR)" -ForegroundColor Gray
}
catch {
    Write-Error "Failed to create package: $_"
    exit 1
}
