<#
.SYNOPSIS
    Generates API reference documentation from XML comments using DocFX.

.DESCRIPTION
    This script builds the solution to generate XML documentation files,
    then uses DocFX to generate static HTML API reference documentation.

.PARAMETER Serve
    If specified, starts a local web server to preview the documentation.

.PARAMETER Clean
    If specified, cleans the output directory before generating.

.EXAMPLE
    .\generate-api-docs.ps1
    Generates the API documentation.

.EXAMPLE
    .\generate-api-docs.ps1 -Serve
    Generates and serves the documentation locally.

.EXAMPLE
    .\generate-api-docs.ps1 -Clean
    Cleans output and regenerates documentation.
#>

param(
    [switch]$Serve,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# Navigate to docs/api directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir

try {
    # Check if DocFX is installed
    $docfxInstalled = $null
    try {
        $docfxInstalled = & docfx --version 2>&1
    } catch {
        $docfxInstalled = $null
    }

    if (-not $docfxInstalled -or $docfxInstalled -match "not recognized") {
        Write-Host "DocFX is not installed. Installing..." -ForegroundColor Yellow
        & dotnet tool install -g docfx
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install DocFX"
        }
        Write-Host "DocFX installed successfully." -ForegroundColor Green
    } else {
        Write-Host "DocFX version: $docfxInstalled" -ForegroundColor Cyan
    }

    # Clean output if requested
    if ($Clean) {
        Write-Host "Cleaning output directories..." -ForegroundColor Yellow
        if (Test-Path "_site") { Remove-Item "_site" -Recurse -Force }
        if (Test-Path "api-dispatch") { Remove-Item "api-dispatch" -Recurse -Force }
        if (Test-Path "api-excalibur") { Remove-Item "api-excalibur" -Recurse -Force }
        Write-Host "Clean complete." -ForegroundColor Green
    }

    # Build solution to generate XML docs
    Write-Host "Building solution to generate XML documentation..." -ForegroundColor Yellow
    Push-Location "../.."
    & dotnet build -c Release --no-restore -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Build completed with warnings/errors. Continuing with documentation generation..."
    }
    Pop-Location
    Write-Host "Build complete." -ForegroundColor Green

    # Generate documentation
    Write-Host "Generating API documentation with DocFX..." -ForegroundColor Yellow

    if ($Serve) {
        Write-Host "Starting local server at http://localhost:8080" -ForegroundColor Cyan
        & docfx docfx.json --serve
    } else {
        & docfx docfx.json
        if ($LASTEXITCODE -ne 0) {
            throw "DocFX documentation generation failed"
        }
        Write-Host "Documentation generated successfully in _site/" -ForegroundColor Green
        Write-Host "To view locally, run: docfx docfx.json --serve" -ForegroundColor Cyan
    }
}
finally {
    Pop-Location
}
