#Requires -Version 7.0
<#
.SYNOPSIS
    Adds missing projects to Excalibur.sln
.DESCRIPTION
    Compares governed filesystem projects with exact solution paths and adds missing
    buildable projects using dotnet sln add. Raw template payloads remain solution items;
    use validate-solution.ps1 to check those items and the manifest after a repair.
.PARAMETER DryRun
    If set, only shows what would be added without making changes
#>
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Require the governance manifest before performing a repair.
$manifestPath = "eng/governance/project-manifest.yaml"
if (-not (Test-Path $manifestPath)) {
    Write-Error "Manifest not found at $manifestPath. Run inventory-projects.ps1 first."
    exit 1
}

# Get projects currently in solution
$slnProjects = @(Get-Content -LiteralPath Excalibur.sln | ForEach-Object {
    if ($_.Trim() -match '^Project\("[^"]+"\) = "[^"]+", "([^"]+\.csproj)", "\{[^}]+\}"$') { $Matches[1].Replace([char]92, '/') }
})
$slnProjectsSet = [Collections.Generic.Dictionary[string,bool]]::new([StringComparer]::Ordinal)
foreach ($p in $slnProjects) {
    $slnProjectsSet.Add($p, $true)
}

# Find all governed csproj files
$GovernedDirectories = @("src", "tests", "samples", "benchmarks", "load-tests")
$allProjects = @()
foreach ($dir in $GovernedDirectories) {
    if (Test-Path $dir) {
        $projects = Get-ChildItem -Path $dir -Recurse -Filter "*.csproj" -File | Where-Object {
            $relative = [IO.Path]::GetRelativePath((Get-Location).Path, $_.FullName).Replace([char]92, '/')
            -not @($relative.Split('/') | Where-Object {
                $_.StartsWith('.') -or $_ -cin @('bin','obj','node_modules','labs','tools','BenchmarkDotNet.Artifacts')
            }).Count
        }
        $allProjects += $projects
    }
}
$allProjects += Get-Item -LiteralPath 'templates/Excalibur.Dispatch.Templates.csproj'

$repoRoot = (Get-Location).Path
$missingProjects = @()

foreach ($proj in $allProjects) {
    $relativePath = [IO.Path]::GetRelativePath($repoRoot, $proj.FullName).Replace([char]92, '/')
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
