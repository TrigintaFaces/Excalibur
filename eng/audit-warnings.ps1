#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Audit warnings in shipping projects (Sprint 329 T2.5: AD-329)

.DESCRIPTION
    Builds eng/ci/shards/ShippingOnly.slnf and analyzes warnings to help maintain warning-free shipping code.
    Use this script before committing to ensure no new warnings are introduced.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER Detailed
    Show detailed warning breakdown by file and warning code

.PARAMETER ExportCsv
    Export warnings to CSV file at specified path

.EXAMPLE
    ./eng/audit-warnings.ps1
    Basic warning audit with summary

.EXAMPLE
    ./eng/audit-warnings.ps1 -Detailed
    Show detailed breakdown by file and warning code

.EXAMPLE
    ./eng/audit-warnings.ps1 -ExportCsv "warnings.csv"
    Export all warnings to CSV for tracking

.NOTES
    Sprint: 329
    Task: W2.T2.5 Re-enable Analyzer Warning Gates
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Detailed,

    [string]$ExportCsv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$SlnFilter = Join-Path $RepoRoot "eng/ci/shards/ShippingOnly.slnf"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Sprint 329 T2.5: Warning Audit Script" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Verify solution filter exists
if (-not (Test-Path $SlnFilter)) {
    Write-Host "ERROR: eng/ci/shards/ShippingOnly.slnf not found at $SlnFilter" -ForegroundColor Red
    exit 1
}

Write-Host "[1/3] Building eng/ci/shards/ShippingOnly.slnf ($Configuration)..." -ForegroundColor Yellow

# Build and capture output
$buildOutput = dotnet build $SlnFilter --configuration $Configuration --no-incremental 2>&1 | Out-String

# Parse warnings from build output
$warningPattern = '(?<File>[^(]+)\((?<Line>\d+),(?<Col>\d+)\): warning (?<Code>\w+): (?<Message>.+?) \[(?<Project>[^\]]+)\]'
$cscWarningPattern = 'CSC : warning (?<Code>\w+): (?<Message>.+?) \[(?<Project>[^\]]+)\]'

$warnings = @()

# Match file-based warnings
$buildOutput -split "`n" | ForEach-Object {
    if ($_ -match $warningPattern) {
        $warnings += [PSCustomObject]@{
            File = $Matches['File'].Trim()
            Line = [int]$Matches['Line']
            Column = [int]$Matches['Col']
            Code = $Matches['Code']
            Message = $Matches['Message'].Trim()
            Project = (Split-Path -Leaf $Matches['Project']) -replace '::TargetFramework=\w+', ''
        }
    }
    # Match CSC-level warnings (assembly-level)
    elseif ($_ -match $cscWarningPattern) {
        $warnings += [PSCustomObject]@{
            File = "(assembly)"
            Line = 0
            Column = 0
            Code = $Matches['Code']
            Message = $Matches['Message'].Trim()
            Project = (Split-Path -Leaf $Matches['Project']) -replace '::TargetFramework=\w+', ''
        }
    }
}

# Deduplicate (multi-TFM builds report same warning twice)
$uniqueWarnings = $warnings | Sort-Object File, Line, Code -Unique

Write-Host "`n[2/3] Analyzing warnings..." -ForegroundColor Yellow

# Summary by warning code
$byCode = $uniqueWarnings | Group-Object Code | Sort-Object Count -Descending

# Summary by project
$byProject = $uniqueWarnings | Group-Object Project | Sort-Object Count -Descending

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Warning Audit Results" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$warningCount = if ($uniqueWarnings) { @($uniqueWarnings).Count } else { 0 }
Write-Host "Total Unique Warnings: " -NoNewline
if ($warningCount -eq 0) {
    Write-Host "$warningCount" -ForegroundColor Green
} else {
    Write-Host "$warningCount" -ForegroundColor Yellow
}

Write-Host "`n--- By Warning Code ---" -ForegroundColor White
foreach ($group in $byCode) {
    $codeUrl = switch -Regex ($group.Name) {
        '^CA' { "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/$($group.Name.ToLower())" }
        '^CS' { "https://learn.microsoft.com/dotnet/csharp/misc/$($group.Name.ToLower())" }
        '^IDE' { "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/$($group.Name.ToLower())" }
        default { "" }
    }
    Write-Host "  $($group.Name): $($group.Count) " -NoNewline -ForegroundColor Yellow
    if ($codeUrl) {
        Write-Host "($codeUrl)" -ForegroundColor DarkGray
    } else {
        Write-Host ""
    }
}

Write-Host "`n--- By Project ---" -ForegroundColor White
foreach ($group in $byProject) {
    Write-Host "  $($group.Name): $($group.Count)" -ForegroundColor Yellow
}

if ($Detailed -and $uniqueWarnings.Count -gt 0) {
    Write-Host "`n--- Detailed Warning List ---" -ForegroundColor White
    foreach ($warning in ($uniqueWarnings | Sort-Object Project, File, Line)) {
        Write-Host "  $($warning.Project)" -ForegroundColor Cyan
        Write-Host "    $($warning.File):$($warning.Line)" -ForegroundColor White
        Write-Host "    $($warning.Code): $($warning.Message)" -ForegroundColor DarkGray
        Write-Host ""
    }
}

# Export to CSV if requested
if ($ExportCsv) {
    Write-Host "`n[3/3] Exporting to CSV..." -ForegroundColor Yellow
    $uniqueWarnings | Export-Csv -Path $ExportCsv -NoTypeInformation
    Write-Host "Exported to: $ExportCsv" -ForegroundColor Green
} else {
    Write-Host "`n[3/3] Export skipped (use -ExportCsv to export)" -ForegroundColor DarkGray
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($warningCount -eq 0) {
    Write-Host "SUCCESS: No warnings in shipping code!" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "WARNING: $warningCount warnings found" -ForegroundColor Yellow
    Write-Host "========================================`n" -ForegroundColor Cyan
    Write-Host "Fix these warnings or add documented suppressions before enabling TreatWarningsAsErrors." -ForegroundColor White
    exit 1
}
