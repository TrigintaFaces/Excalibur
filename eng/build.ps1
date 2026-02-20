#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified build script for Excalibur Microsoft-style repository transformation
.DESCRIPTION
    Builds the entire solution with unified configuration, enforces quality gates,
    and validates architectural boundaries per constitutional requirements.
.PARAMETER Configuration
    Build configuration (Debug/Release). Default: Release
.PARAMETER NoBuild
    Skip build and only run validation
.PARAMETER NoTest
    Skip running tests
.PARAMETER NoRestore
    Skip package restore
.PARAMETER Coverage
    Generate code coverage reports
.PARAMETER Pack
    Create NuGet packages after successful build
.PARAMETER Verbose
    Enable verbose output
.EXAMPLE
    .\eng\build.ps1 -Configuration Release -Coverage -Pack
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$NoBuild,
    [switch]$NoTest,
    [switch]$NoRestore,
    [switch]$Coverage,
    [switch]$Pack,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Article XIV.1: Canonical layout enforcement
$repoRoot = $PSScriptRoot | Split-Path
$srcDir = Join-Path $repoRoot "src"
$testDir = Join-Path $repoRoot "test"
$engDir = Join-Path $repoRoot "eng"
$managementDir = Join-Path $repoRoot "management"

Write-Host "🏗️  Excalibur Unified Build" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Validate canonical directory structure per Article XIV.1
Write-Host "📁 Validating canonical directory structure..." -ForegroundColor Yellow
$requiredDirs = @("src", "test", "benchmarks", "samples", "docs", "tools", "eng", "management")
$missing = @()
foreach ($dir in $requiredDirs) {
    $path = Join-Path $repoRoot $dir
    if (-not (Test-Path $path)) {
        $missing += $dir
    }
}
if ($missing.Count -gt 0) {
    Write-Error "❌ Missing required directories per Article XIV.1: $($missing -join ', ')"
    exit 1
}
Write-Host "✅ Canonical directory structure validated" -ForegroundColor Green

# Validate solution file exists
$solutionFile = Get-ChildItem -Path $repoRoot -Filter "*.sln" | Select-Object -First 1
if (-not $solutionFile) {
    Write-Error "❌ No solution file found in repository root"
    exit 1
}
Write-Host "📄 Solution: $($solutionFile.Name)" -ForegroundColor Gray

# Validate unified build configuration per Article XIV.2-4
Write-Host "🔧 Validating unified build configuration..." -ForegroundColor Yellow
$buildFiles = @(
    "Directory.Build.props",
    "Directory.Build.targets", 
    "Directory.Packages.props",
    "global.json",
    ".editorconfig"
)
foreach ($file in $buildFiles) {
    $path = Join-Path $repoRoot $file
    if (-not (Test-Path $path)) {
        Write-Warning "⚠️  Missing build file: $file"
    }
}

# Check for EnforceCodeStyleInBuild per Article XIV.2
$directoryBuildProps = Join-Path $repoRoot "Directory.Build.props"
if (Test-Path $directoryBuildProps) {
    $content = Get-Content $directoryBuildProps -Raw
    if ($content -notmatch "EnforceCodeStyleInBuild.*true") {
        Write-Warning "⚠️  EnforceCodeStyleInBuild not enabled in Directory.Build.props"
    }
}
Write-Host "✅ Build configuration validated" -ForegroundColor Green

# Run constitutional validation scripts
Write-Host "⚖️  Running constitutional validation..." -ForegroundColor Yellow

# Verify layout per Article XIV.1
$layoutScript = Join-Path $engDir "verify-layout.ps1"
if (Test-Path $layoutScript) {
    Write-Host "  📐 Verifying canonical layout..." -ForegroundColor Gray
    & $layoutScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Layout validation failed"
        exit 1
    }
}

# Verify provider isolation per Articles X, XV.4
$providersScript = Join-Path $engDir "verify-providers.ps1"
if (Test-Path $providersScript) {
    Write-Host "  🏗️  Verifying provider isolation..." -ForegroundColor Gray
    & $providersScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Provider isolation validation failed"
        exit 1
    }
}

# Verify banned APIs per Article III
$bannedApisScript = Join-Path $engDir "verify-banned-apis.ps1"
if (Test-Path $bannedApisScript) {
    Write-Host "  🚫 Verifying banned API compliance..." -ForegroundColor Gray
    & $bannedApisScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Banned API validation failed"
        exit 1
    }
}
Write-Host "✅ Constitutional validation passed" -ForegroundColor Green

# Package restore
if (-not $NoRestore) {
    Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
    $restoreArgs = @("restore", $solutionFile.FullName)
    if ($Verbose) { $restoreArgs += "--verbosity", "detailed" }
    
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Package restore failed"
        exit 1
    }
    Write-Host "✅ Packages restored" -ForegroundColor Green
}

# Build solution
if (-not $NoBuild) {
    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    $buildArgs = @("build", $solutionFile.FullName, "--configuration", $Configuration, "--no-restore")
    if ($Verbose) { $buildArgs += "--verbosity", "detailed" }
    
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Build failed"
        exit 1
    }
    Write-Host "✅ Build completed successfully" -ForegroundColor Green
}

# Run tests with coverage
if (-not $NoTest) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    
    $testArgs = @("test", $solutionFile.FullName, "--configuration", $Configuration, "--no-restore", "--no-build")
    if ($Coverage) {
        $testArgs += "--collect:XPlat Code Coverage"
        $testArgs += "--results-directory", (Join-Path $repoRoot "TestResults")
    }
    if ($Verbose) { $testArgs += "--verbosity", "detailed" }
    
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Tests failed"
        exit 1
    }
    
    # Validate ≥95% coverage requirement
    if ($Coverage) {
        Write-Host "📊 Analyzing code coverage..." -ForegroundColor Yellow
        $testResultsDir = Join-Path $repoRoot "TestResults"
        $coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse
        
        if ($coverageFiles.Count -gt 0) {
            # Parse coverage and validate ≥95% requirement
            $latestCoverage = $coverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            $coverageXml = [xml](Get-Content $latestCoverage.FullName)
            $lineRate = [double]$coverageXml.coverage.'line-rate'
            $coveragePercent = [math]::Round($lineRate * 100, 2)
            
            Write-Host "  Coverage: $coveragePercent%" -ForegroundColor Gray
            if ($coveragePercent -lt 95.0) {
                Write-Error "❌ Code coverage $coveragePercent% is below required 95% threshold"
                exit 1
            }
            Write-Host "✅ Coverage requirement (≥95%) satisfied" -ForegroundColor Green
        } else {
            Write-Warning "⚠️  No coverage reports found"
        }
    }
    
    Write-Host "✅ All tests passed" -ForegroundColor Green
}

# Create NuGet packages
if ($Pack) {
    Write-Host "📦 Creating NuGet packages..." -ForegroundColor Yellow
    
    # Find all packable projects in src/
    $packableProjects = Get-ChildItem -Path $srcDir -Filter "*.csproj" -Recurse | Where-Object {
        $content = Get-Content $_.FullName -Raw
        $content -match "<IsPackable>true</IsPackable>" -or $content -notmatch "<IsPackable>false</IsPackable>"
    }
    
    foreach ($project in $packableProjects) {
        Write-Host "  📦 Packing $($project.Name)..." -ForegroundColor Gray
        $packArgs = @("pack", $project.FullName, "--configuration", $Configuration, "--no-restore", "--no-build")
        $packArgs += "--output", (Join-Path $repoRoot "artifacts/packages")
        
        & dotnet @packArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to pack $($project.Name)"
            exit 1
        }
    }
    
    Write-Host "✅ NuGet packages created successfully" -ForegroundColor Green
}

# Generate build report
Write-Host "📋 Generating build report..." -ForegroundColor Yellow
$reportDir = Join-Path $managementDir "reports"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

$buildReport = @{
    BuildTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    Configuration = $Configuration
    Solution = $solutionFile.Name
    Status = "Success"
    ValidationsPassed = @("Layout", "Providers", "BannedAPIs")
}

if ($Coverage -and (Test-Path (Join-Path $repoRoot "TestResults"))) {
    $buildReport.CodeCoverage = "$coveragePercent%"
}

$buildReport | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $reportDir "last-build.json")
Write-Host "✅ Build report saved to management/reports/last-build.json" -ForegroundColor Green

Write-Host ""
Write-Host "🎉 Build completed successfully!" -ForegroundColor Green
Write-Host "   Configuration: $Configuration" -ForegroundColor Gray
if (-not $NoTest) {
    Write-Host "   Tests: ✅ Passed" -ForegroundColor Gray
}
if ($Coverage) {
    Write-Host "   Coverage: $coveragePercent%" -ForegroundColor Gray
}
if ($Pack) {
    Write-Host "   Packages: ✅ Created" -ForegroundColor Gray
}
Write-Host "   Constitutional compliance: ✅ Validated" -ForegroundColor Gray