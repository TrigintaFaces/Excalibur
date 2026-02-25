# Copyright (c) 2026 The Excalibur Project
# API Compatibility Validation Script (Phase 9.5)
#
# This script validates public API surface compatibility using Microsoft.CodeAnalysis.PublicApiAnalyzers
# and ensures breaking changes are tracked in Unshipped.txt files before release.
#
# Requirements:
# - RS0016: Add public types and members to the declared API
# - RS0017: Remove deleted types and members from the declared API
# - RS0024: The contents of the public API files are invalid
# - RS0025: Do not duplicate symbols in public API files
# - RS0026: Do not add multiple public overloads with optional parameters
# - RS0027: Public API with optional parameter(s) should have the most parameters amongst its public overloads
# - RS0037: Enable tracking of nullability of reference types in the declared API
# - RS0041: Public members should not use oblivious types
# - RS0048: Missing shipped or unshipped public API file
# - RS0050: Incorrect source file encoding
# - RS0051: Do not add multiple overloads with optional parameters in a single release
#
# Exit Codes:
# 0 = No API compatibility issues
# 1 = API breaking changes or validation errors detected
# 2 = Script error or missing configuration

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPattern = "src/**/*.csproj",

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "api-compatibility-report.md",

    [Parameter(Mandatory = $false)]
    [switch]$AllowBreakingChanges
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Excalibur - API Compatibility Validation (Phase 9.5)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Configuration
# ============================================================================

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$apiAnalyzers = @("RS0016", "RS0017", "RS0024", "RS0025", "RS0026", "RS0027", "RS0037", "RS0041", "RS0048", "RS0050", "RS0051")

Write-Host "Repository Root: $repoRoot" -ForegroundColor Gray
Write-Host ""

# ============================================================================
# Find Projects with PublicAPI Tracking
# ============================================================================

Write-Host "Searching for projects with PublicAPI tracking..." -ForegroundColor Cyan

$projects = Get-ChildItem -Path $repoRoot -Filter "*.csproj" -Recurse | Where-Object {
    $projectDir = $_.DirectoryName
    $hasShipped = Test-Path (Join-Path $projectDir "PublicAPI.Shipped.txt")
    $hasUnshipped = Test-Path (Join-Path $projectDir "PublicAPI.Unshipped.txt")
    return ($hasShipped -or $hasUnshipped)
}

if ($projects.Count -eq 0) {
    Write-Host "  WARNING: No projects with PublicAPI tracking found" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To enable API tracking, add Microsoft.CodeAnalysis.PublicApiAnalyzers package:" -ForegroundColor Gray
    Write-Host "  <PackageReference Include=`"Microsoft.CodeAnalysis.PublicApiAnalyzers`" Version=`"3.3.4`">" -ForegroundColor Gray
    Write-Host "    <PrivateAssets>all</PrivateAssets>" -ForegroundColor Gray
    Write-Host "    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>" -ForegroundColor Gray
    Write-Host "  </PackageReference>" -ForegroundColor Gray
    exit 2
}

Write-Host "  Found $($projects.Count) project(s) with PublicAPI tracking" -ForegroundColor Green
foreach ($project in $projects) {
    Write-Host "    - $($project.Name)" -ForegroundColor Gray
}
Write-Host ""

# ============================================================================
# Build Projects with API Analyzer Diagnostics
# ============================================================================

Write-Host "Building projects and collecting API analyzer diagnostics..." -ForegroundColor Cyan
Write-Host ""

$allIssues = @()
$projectResults = @()

foreach ($project in $projects) {
    Write-Host "Analyzing: $($project.Name)" -ForegroundColor Gray

    $buildOutput = dotnet build $project.FullName --no-restore --verbosity quiet 2>&1 | Out-String

    # Extract API analyzer warnings/errors
    $issues = $buildOutput -split "`n" | Where-Object {
        $_ -match "warning (RS\d{4}):" -or $_ -match "error (RS\d{4}):"
    }

    $projectIssues = @()

    foreach ($issue in $issues) {
        if ($issue -match "(warning|error) (RS\d{4}): (.+?) \[(.+?)\]") {
            $severity = $matches[1]
            $code = $matches[2]
            $message = $matches[3]
            $file = $matches[4]

            $projectIssues += [PSCustomObject]@{
                Severity = $severity
                Code = $code
                Message = $message
                File = $file
                Project = $project.Name
            }
        }
    }

    if ($projectIssues.Count -gt 0) {
        $allIssues += $projectIssues
        Write-Host "  Found $($projectIssues.Count) issue(s)" -ForegroundColor Yellow
    }
    else {
        Write-Host "  ✅ No API compatibility issues" -ForegroundColor Green
    }

    $projectResults += [PSCustomObject]@{
        ProjectName = $project.Name
        Issues = $projectIssues
        ApiShippedPath = Join-Path $project.DirectoryName "PublicAPI.Shipped.txt"
        ApiUnshippedPath = Join-Path $project.DirectoryName "PublicAPI.Unshipped.txt"
    }
}

Write-Host ""

# ============================================================================
# Analyze Unshipped Changes
# ============================================================================

Write-Host "Analyzing unshipped API changes..." -ForegroundColor Cyan

$unshippedChanges = @()

foreach ($result in $projectResults) {
    if (Test-Path $result.ApiUnshippedPath) {
        $content = Get-Content $result.ApiUnshippedPath -Raw
        $lines = ($content -split "`n" | Where-Object { $_ -match '\S' -and $_ -notmatch '^#' }).Count

        if ($lines -gt 0) {
            $unshippedChanges += [PSCustomObject]@{
                Project = $result.ProjectName
                UnshippedLines = $lines
                FilePath = $result.ApiUnshippedPath
            }
            Write-Host "  $($result.ProjectName): $lines unshipped API change(s)" -ForegroundColor Yellow
        }
    }
}

if ($unshippedChanges.Count -eq 0) {
    Write-Host "  ✅ No unshipped API changes" -ForegroundColor Green
}

Write-Host ""

# ============================================================================
# Generate Report
# ============================================================================

$reportContent = @"
# API Compatibility Report
**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary

| Metric | Count |
|--------|-------|
| Total Projects | $($projects.Count) |
| Projects with Issues | $(($allIssues | Select-Object -ExpandProperty Project | Sort-Object -Unique).Count) |
| Total API Issues | $($allIssues.Count) |
| Projects with Unshipped Changes | $($unshippedChanges.Count) |

"@

if ($allIssues.Count -gt 0) {
    $reportContent += @"

## ❌ API Compatibility Issues

| Severity | Code | Project | Message |
|----------|------|---------|---------|
"@

    foreach ($issue in $allIssues | Sort-Object Severity, Code) {
        $icon = if ($issue.Severity -eq "error") { "❌" } else { "⚠️" }
        $reportContent += @"

| $icon $($issue.Severity.ToUpper()) | ``$($issue.Code)`` | ``$($issue.Project)`` | $($issue.Message.Replace("|", "\|")) |
"@
    }
}

if ($unshippedChanges.Count -gt 0) {
    $reportContent += @"


## 📝 Unshipped API Changes

The following projects have unshipped API changes that should be reviewed and shipped before release:

| Project | Unshipped Lines | File Path |
|---------|-----------------|-----------|
"@

    foreach ($change in $unshippedChanges | Sort-Object Project) {
        $reportContent += @"

| ``$($change.Project)`` | $($change.UnshippedLines) | ``$($change.FilePath)`` |
"@
    }

    $reportContent += @"


### Shipping API Changes

To ship these API changes:
1. Review all entries in ``PublicAPI.Unshipped.txt`` files
2. Move entries from ``PublicAPI.Unshipped.txt`` to ``PublicAPI.Shipped.txt``
3. Clear ``PublicAPI.Unshipped.txt`` after moving all entries
4. Commit changes with version bump

"@
}

$reportContent += @"


## Analyzer Rules Checked

"@

$analyzerDescriptions = @{
    "RS0016" = "Add public types and members to the declared API"
    "RS0017" = "Remove deleted types and members from the declared API"
    "RS0024" = "The contents of the public API files are invalid"
    "RS0025" = "Do not duplicate symbols in public API files"
    "RS0026" = "Do not add multiple public overloads with optional parameters"
    "RS0027" = "Public API with optional parameter(s) should have the most parameters amongst its public overloads"
    "RS0037" = "Enable tracking of nullability of reference types in the declared API"
    "RS0041" = "Public members should not use oblivious types"
    "RS0048" = "Missing shipped or unshipped public API file"
    "RS0050" = "Incorrect source file encoding"
    "RS0051" = "Do not add multiple overloads with optional parameters in a single release"
}

foreach ($code in $apiAnalyzers) {
    $desc = $analyzerDescriptions[$code]
    $reportContent += "- **$code**: $desc$([Environment]::NewLine)"
}

$reportContent += @"


## Exit Code

"@

# ============================================================================
# Determine Exit Code
# ============================================================================

$exitCode = 0

$errors = $allIssues | Where-Object { $_.Severity -eq "error" }

if ($errors.Count -gt 0) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - API compatibility errors detected (fails CI)$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  ❌ API COMPATIBILITY ERRORS: $($errors.Count)" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
}
elseif ($allIssues.Count -gt 0 -and -not $AllowBreakingChanges) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - API compatibility warnings detected$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  ⚠️ API COMPATIBILITY WARNINGS: $($allIssues.Count)" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
}
else {
    $reportContent += "**EXIT 0** - No API compatibility issues ✅$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ✅ API COMPATIBILITY VALIDATED" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
}

# Save report
$reportContent | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host ""
Write-Host "Report saved to: $OutputPath" -ForegroundColor Cyan

# Display summary
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total Projects: $($projects.Count)" -ForegroundColor Gray
Write-Host "  API Issues: $($allIssues.Count)" -ForegroundColor $(if ($allIssues.Count -gt 0) { "Yellow" } else { "Green" })
Write-Host "  Unshipped Changes: $($unshippedChanges.Count)" -ForegroundColor $(if ($unshippedChanges.Count -gt 0) { "Yellow" } else { "Green" })
Write-Host ""

exit $exitCode
