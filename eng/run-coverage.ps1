# Coverage enforcement script for Excalibur.Dispatch
# Usage: .\scripts\run-coverage.ps1 [-Threshold 95] [-Component "Transport"]

param(
    [int]$Threshold = 95,
    [string]$Component = "",
    [switch]$SkipBuild,
    [switch]$GenerateHtml
)

$ErrorActionPreference = "Stop"

Write-Host "=== Excalibur Coverage Runner ===" -ForegroundColor Cyan

# Configuration
$SolutionRoot = Split-Path -Parent $PSScriptRoot
$TestsDir = Join-Path $SolutionRoot "tests"
$ArtifactsDir = Join-Path $SolutionRoot "artifacts/coverage"
$RunSettingsPath = Join-Path $TestsDir "coverage.runsettings"

# Ensure artifacts directory exists
New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null

# Build filter
$Filter = ""
if ($Component) {
    $Filter = "--filter `"Component=$Component`""
    Write-Host "Filtering tests by Component: $Component" -ForegroundColor Yellow
}

# Build if requested
if (-not $SkipBuild) {
    Write-Host "`nBuilding solution..." -ForegroundColor Yellow
    dotnet build "$SolutionRoot/Excalibur.sln" -v q -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Run tests with coverage
Write-Host "`nRunning tests with coverage collection..." -ForegroundColor Yellow
$TestCommand = "dotnet test `"$SolutionRoot/Excalibur.sln`" " +
               "--collect:`"XPlat Code Coverage`" " +
               "--settings `"$RunSettingsPath`" " +
               "--results-directory `"$ArtifactsDir`" " +
               "-c Release --no-build -v q $Filter"

Invoke-Expression $TestCommand
$TestExitCode = $LASTEXITCODE

# Find coverage files
Write-Host "`nCollecting coverage results..." -ForegroundColor Yellow
$CoverageFiles = Get-ChildItem -Path $ArtifactsDir -Recurse -Filter "coverage.cobertura.xml"

if ($CoverageFiles.Count -eq 0) {
    Write-Host "No coverage files found!" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($CoverageFiles.Count) coverage file(s)" -ForegroundColor Green

# Generate merged report
$ReportTypes = "TextSummary"
if ($GenerateHtml) {
    $ReportTypes = "TextSummary;Html;Cobertura"
}

$CoveragePattern = "$ArtifactsDir/**/coverage.cobertura.xml"
$ReportDir = Join-Path $ArtifactsDir "report"

Write-Host "`nGenerating coverage report..." -ForegroundColor Yellow
reportgenerator `
    -reports:"$CoveragePattern" `
    -targetdir:"$ReportDir" `
    -reporttypes:"$ReportTypes" `
    -assemblyfilters:"+Excalibur.Dispatch.*;+Excalibur.*" `
    -classfilters:"-*.Tests.*;-*.Benchmarks.*"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Report generation failed!" -ForegroundColor Red
    exit 1
}

# Read and display summary
$SummaryPath = Join-Path $ReportDir "Summary.txt"
if (Test-Path $SummaryPath) {
    Write-Host "`n=== Coverage Summary ===" -ForegroundColor Cyan
    $SummaryLines = Get-Content $SummaryPath | Select-Object -First 20
    $SummaryLines | ForEach-Object { Write-Host $_ }

    # Extract line coverage percentage
    $LineCoverageLine = Get-Content $SummaryPath | Where-Object { $_ -match "Line coverage:" }
    if ($LineCoverageLine -match "(\d+)%") {
        $LineCoverage = [int]$Matches[1]

        Write-Host "`n=== Coverage Gate ===" -ForegroundColor Cyan
        Write-Host "Threshold: $Threshold%" -ForegroundColor Yellow
        Write-Host "Actual:    $LineCoverage%" -ForegroundColor $(if ($LineCoverage -ge $Threshold) { "Green" } else { "Red" })

        if ($LineCoverage -lt $Threshold) {
            Write-Host "`nCOVERAGE GATE FAILED! Line coverage $LineCoverage% is below threshold $Threshold%" -ForegroundColor Red
            exit 1
        } else {
            Write-Host "`nCOVERAGE GATE PASSED!" -ForegroundColor Green
        }
    }
}

# Copy summary to artifacts
Copy-Item $SummaryPath -Destination (Join-Path $ArtifactsDir "summary.txt") -Force

if ($GenerateHtml) {
    $HtmlReport = Join-Path $ReportDir "index.html"
    if (Test-Path $HtmlReport) {
        Write-Host "`nHTML report generated at: $HtmlReport" -ForegroundColor Green
    }
}

Write-Host "`nCoverage analysis complete." -ForegroundColor Cyan

# Return test exit code
exit $TestExitCode
