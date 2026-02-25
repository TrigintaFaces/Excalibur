#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pack Dispatch projects to local feed for CI validation (AD-327-2).

.DESCRIPTION
    This script:
    1. Builds all Dispatch projects in Release mode
    2. Packs them to artifacts/_packages/ local feed
    3. The packages are then used by Excalibur projects when
       UsePackageReferences=true is set

.PARAMETER Version
    Package version. Defaults to 0.1.0-local.

.PARAMETER NoBuild
    Skip build step (use if already built).

.PARAMETER Clean
    Remove existing packages before packing.

.EXAMPLE
    .\pack-local.ps1

.EXAMPLE
    .\pack-local.ps1 -Version 0.2.0-ci

.EXAMPLE
    .\pack-local.ps1 -NoBuild -Clean
#>

[CmdletBinding()]
param(
    [string]$Version = "0.1.0-local",
    [switch]$NoBuild,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LocalFeed = Join-Path $RepoRoot "artifacts/_packages"
$DispatchSrc = Join-Path $RepoRoot "src/Dispatch"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Sprint 327 T2.2: Pack Local Feed (AD-327-2)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Clean and create local feed
if ($Clean -or !(Test-Path $LocalFeed)) {
    if (Test-Path $LocalFeed) {
        Write-Host "Cleaning existing packages..." -ForegroundColor Yellow
        Remove-Item $LocalFeed -Recurse -Force
    }
    Write-Host "Creating local feed directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $LocalFeed -Force | Out-Null
}

# Build Dispatch projects
if (-not $NoBuild) {
    Write-Host "`n[1/2] Building Dispatch projects..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    try {
        # Build each Dispatch project individually
        $DispatchProjects = Get-ChildItem -Path $DispatchSrc -Filter "*.csproj" -Recurse
        foreach ($proj in $DispatchProjects) {
            Write-Host "  Building $($proj.Name)..." -ForegroundColor Gray
            dotnet build $proj.FullName -c Release --no-restore
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed for $($proj.Name) with exit code $LASTEXITCODE"
            }
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "`n[1/2] Skipping build (--NoBuild specified)..." -ForegroundColor Yellow
}

# Pack Dispatch projects
Write-Host "`n[2/2] Packing Dispatch projects to local feed..." -ForegroundColor Yellow

$DispatchProjects = Get-ChildItem -Path $DispatchSrc -Filter "*.csproj" -Recurse

$packedCount = 0
foreach ($proj in $DispatchProjects) {
    Write-Host "  Packing $($proj.Name)..." -ForegroundColor Gray

    Push-Location $RepoRoot
    try {
        dotnet pack $proj.FullName `
            -o $LocalFeed `
            -c Release `
            -p:Version=$Version `
            --no-build `
            --no-restore

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to pack $($proj.Name)"
        }
        else {
            $packedCount++
        }
    }
    finally {
        Pop-Location
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Local Feed Ready" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Location: $LocalFeed" -ForegroundColor White
Write-Host "  Version: $Version" -ForegroundColor White

$packages = Get-ChildItem $LocalFeed -Filter "*.nupkg" -ErrorAction SilentlyContinue
$packageCount = ($packages | Measure-Object).Count
Write-Host "  Packages: $packageCount" -ForegroundColor White

if ($packageCount -gt 0) {
    Write-Host "`nPackages:" -ForegroundColor Cyan
    $packages | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
}

Write-Host "`nUsage:" -ForegroundColor Cyan
Write-Host "  dotnet build src/Excalibur -p:UsePackageReferences=true" -ForegroundColor Gray
Write-Host "  dotnet build eng/ci/shards/ShippingOnly.slnf -p:UsePackageReferences=true" -ForegroundColor Gray

exit 0
