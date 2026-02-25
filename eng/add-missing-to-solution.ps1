#Requires -Version 5.1
<#
.SYNOPSIS
    Adds missing projects to Excalibur.sln (Sprint 325 - T0.1)
.DESCRIPTION
    Reads the project manifest and adds any projects marked as in_solution=false
    to the solution file using dotnet sln add.
.PARAMETER DryRun
    If set, only shows what would be added without making changes
#>
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Load manifest to get missing projects
$manifestPath = "management/governance/project-manifest.yaml"
if (-not (Test-Path $manifestPath)) {
    Write-Error "Manifest not found at $manifestPath. Run inventory-projects.ps1 first."
    exit 1
}

# Get projects currently in solution
$slnOutput = dotnet sln Excalibur.sln list 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to read solution: $slnOutput"
    exit 1
}
$slnProjects = $slnOutput | Select-Object -Skip 2 | ForEach-Object { $_.Trim() -replace '\\','/' }
$slnProjectsSet = @{}
foreach ($p in $slnProjects) {
    $slnProjectsSet[$p] = $true
}

# Find all governed csproj files
$GovernedDirectories = @("src", "tests", "samples", "benchmarks")
$allProjects = @()
foreach ($dir in $GovernedDirectories) {
    if (Test-Path $dir) {
        $projects = Get-ChildItem -Path $dir -Recurse -Filter "*.csproj" -File
        $allProjects += $projects
    }
}

$repoRoot = (Get-Location).Path
$missingProjects = @()

foreach ($proj in $allProjects) {
    $relativePath = $proj.FullName.Replace($repoRoot + "\", "").Replace("\", "/")
    if (-not $slnProjectsSet.ContainsKey($relativePath)) {
        $missingProjects += $relativePath
    }
}

Write-Host "Found $($missingProjects.Count) projects to add to solution" -ForegroundColor Yellow

if ($missingProjects.Count -eq 0) {
    Write-Host "All governed projects are already in the solution!" -ForegroundColor Green
    exit 0
}

$added = 0
$failed = 0

foreach ($proj in ($missingProjects | Sort-Object)) {
    if ($DryRun) {
        Write-Host "[DRY RUN] Would add: $proj" -ForegroundColor Cyan
    } else {
        Write-Host "Adding: $proj" -ForegroundColor Cyan
        $result = dotnet sln Excalibur.sln add $proj 2>&1
        if ($LASTEXITCODE -eq 0) {
            $added++
            Write-Host "  OK" -ForegroundColor Green
        } else {
            $failed++
            Write-Host "  FAILED: $result" -ForegroundColor Red
        }
    }
}

if (-not $DryRun) {
    Write-Host ""
    Write-Host "=== SUMMARY ===" -ForegroundColor Yellow
    Write-Host "Added: $added"
    Write-Host "Failed: $failed"

    if ($failed -gt 0) {
        exit 1
    }
}
