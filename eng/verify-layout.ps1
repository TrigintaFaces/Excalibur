#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Canonical structure validation script per Article XIV.1
.DESCRIPTION
    Validates that the repository follows Microsoft-canonical directory structure
    and enforces organizational standards for the Excalibur framework.
.PARAMETER Fix
    Attempt to fix violations by creating missing directories
.PARAMETER Verbose
    Enable verbose output with detailed validation results
.EXAMPLE
    .\eng\verify-layout.ps1 -Fix -Verbose
#>
[CmdletBinding()]
param(
    [switch]$Fix,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot | Split-Path
Write-Host "📐 Canonical Layout Validation (Article XIV.1)" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host ""

$violations = @()
$warnings = @()

# Required top-level directories per Article XIV.1
$requiredDirs = @{
    "src" = "All source code (unified Dispatch and Excalibur)"
    "test" = "All tests (organized by type: unit, integration, functional)"
    "benchmarks" = "Performance benchmarks and microbenchmarks"
    "load-tests" = "Load and stress testing scenarios"
    "samples" = "Sample applications and usage examples"
    "docs" = "Documentation and guides"
    "tools" = "Development and build tools"
    "eng" = "Build engineering scripts and configuration"
    "management" = "Project governance and requirements tracking"
}

# Optional but recommended directories
$recommendedDirs = @{
    "artifacts" = "Build outputs, packages, and reports"
    "templates" = "Project and file templates"
    ".github" = "GitHub workflows and templates"
}

# Prohibited directories (fragmented structure)
$prohibitedDirs = @{
    "Dispatch" = "Use src/Excalibur.Dispatch.* instead"
    "Excalibur" = "Use src/Excalibur.* instead"
    "Tests" = "Use test/ instead"
    "Source" = "Use src/ instead"
    "Build" = "Use eng/ instead"
}

Write-Host "🔍 Validating required directories..." -ForegroundColor Yellow
foreach ($dir in $requiredDirs.Keys) {
    $path = Join-Path $repoRoot $dir
    if (Test-Path $path -PathType Container) {
        if ($Verbose) {
            Write-Host "  ✅ $dir/ - $($requiredDirs[$dir])" -ForegroundColor Green
        }
    } else {
        $violations += "Missing required directory: $dir/ - $($requiredDirs[$dir])"
        Write-Host "  ❌ $dir/ - MISSING" -ForegroundColor Red

        if ($Fix) {
            Write-Host "    🔧 Creating directory: $dir/" -ForegroundColor Yellow
            New-Item -ItemType Directory -Path $path -Force | Out-Null
            Write-Host "    ✅ Created: $dir/" -ForegroundColor Green
        }
    }
}

Write-Host "🔍 Checking prohibited directories..." -ForegroundColor Yellow
foreach ($dir in $prohibitedDirs.Keys) {
    $path = Join-Path $repoRoot $dir
    if (Test-Path $path -PathType Container) {
        $violations += "Prohibited directory found: $dir/ - $($prohibitedDirs[$dir])"
        Write-Host "  ❌ $dir/ - PROHIBITED ($($prohibitedDirs[$dir]))" -ForegroundColor Red
    } else {
        if ($Verbose) {
            Write-Host "  ✅ $dir/ - not present (good)" -ForegroundColor Green
        }
    }
}

Write-Host "🔍 Checking recommended directories..." -ForegroundColor Yellow
foreach ($dir in $recommendedDirs.Keys) {
    $path = Join-Path $repoRoot $dir
    if (Test-Path $path -PathType Container) {
        if ($Verbose) {
            Write-Host "  ✅ $dir/ - $($recommendedDirs[$dir])" -ForegroundColor Green
        }
    } else {
        $warnings += "Recommended directory missing: $dir/ - $($recommendedDirs[$dir])"
        Write-Host "  ⚠️  $dir/ - recommended but missing" -ForegroundColor Yellow

        if ($Fix -and $dir -eq "artifacts") {
            Write-Host "    🔧 Creating directory: $dir/" -ForegroundColor Yellow
            New-Item -ItemType Directory -Path $path -Force | Out-Null
            Write-Host "    ✅ Created: $dir/" -ForegroundColor Green
        }
    }
}


# Validate src/ structure for Dispatch/Excalibur boundaries
# Projects live under src/Dispatch/ and src/Excalibur/, not directly under src/
Write-Host "🔍 Validating src/ structure..." -ForegroundColor Yellow
$srcDir = Join-Path $repoRoot "src"
if (Test-Path $srcDir) {
    $dispatchProjects = @()
    $excaliburProjects = @()
    $otherProjects = @()

    # Scan src/Dispatch/ and src/Excalibur/ for project subdirectories
    $projectContainers = @("Dispatch", "Excalibur")
    foreach ($container in $projectContainers) {
        $containerPath = Join-Path $srcDir $container
        if (Test-Path $containerPath -PathType Container) {
            $containerProjects = Get-ChildItem -Path $containerPath -Directory
            foreach ($project in $containerProjects) {
                if ($project.Name -like "Excalibur.Dispatch.*" -or $project.Name -eq "Excalibur.Dispatch") {
                    $dispatchProjects += $project.Name
                } elseif ($project.Name -like "Excalibur.*") {
                    $excaliburProjects += $project.Name
                } else {
                    $otherProjects += $project.Name
                }
            }
        }
    }

    Write-Host "  📦 Dispatch projects: $($dispatchProjects.Count)" -ForegroundColor Gray
    if ($Verbose -and $dispatchProjects.Count -gt 0) {
        foreach ($proj in $dispatchProjects) {
            Write-Host "    - $proj" -ForegroundColor Gray
        }
    }

    Write-Host "  📦 Excalibur projects: $($excaliburProjects.Count)" -ForegroundColor Gray
    if ($Verbose -and $excaliburProjects.Count -gt 0) {
        foreach ($proj in $excaliburProjects) {
            Write-Host "    - $proj" -ForegroundColor Gray
        }
    }

    if ($otherProjects.Count -gt 0) {
        $warnings += "Projects not following Excalibur.Dispatch.*/Excalibur.* naming: $($otherProjects -join ', ')"
        Write-Host "  ⚠️  Non-standard project names: $($otherProjects -join ', ')" -ForegroundColor Yellow
    }
} else {
    $violations += "src/ directory is missing"
}

# Validate test/ structure
Write-Host "🔍 Validating test/ structure..." -ForegroundColor Yellow
$testDir = Join-Path $repoRoot "test"
if (Test-Path $testDir) {
    $expectedTestDirs = @("unit", "integration", "functional")
    foreach ($testType in $expectedTestDirs) {
        $testTypePath = Join-Path $testDir $testType
        if (Test-Path $testTypePath) {
            if ($Verbose) {
                $testProjects = Get-ChildItem -Path $testTypePath -Directory
                Write-Host "  ✅ test/$testType/ ($($testProjects.Count) projects)" -ForegroundColor Green
            }
        } else {
            $warnings += "Recommended test directory missing: test/$testType/"
            Write-Host "  ⚠️  test/$testType/ - recommended but missing" -ForegroundColor Yellow

            if ($Fix) {
                Write-Host "    🔧 Creating directory: test/$testType/" -ForegroundColor Yellow
                New-Item -ItemType Directory -Path $testTypePath -Force | Out-Null
                Write-Host "    ✅ Created: test/$testType/" -ForegroundColor Green
            }
        }
    }
} else {
    $violations += "test/ directory is missing"
}

# Validate critical build files
Write-Host "🔍 Validating build configuration files..." -ForegroundColor Yellow
$buildFiles = @{
    "Directory.Build.props" = "Central MSBuild properties per Article XIV.2"
    "Directory.Build.targets" = "Central MSBuild targets per Article XIV.3"
    "Directory.Packages.props" = "Central package management per Article XIV.4"
    "global.json" = "SDK version pinning"
    ".editorconfig" = "Code style enforcement"
    ".gitignore" = "Git ignore patterns"
}

foreach ($file in $buildFiles.Keys) {
    $path = Join-Path $repoRoot $file
    if (Test-Path $path -PathType Leaf) {
        if ($Verbose) {
            Write-Host "  ✅ $file - $($buildFiles[$file])" -ForegroundColor Green
        }
    } else {
        $violations += "Missing critical build file: $file - $($buildFiles[$file])"
        Write-Host "  ❌ $file - MISSING" -ForegroundColor Red
    }
}

# Validate solution file
Write-Host "🔍 Validating solution file..." -ForegroundColor Yellow
$solutionFiles = Get-ChildItem -Path $repoRoot -Filter "*.sln"
if ($solutionFiles.Count -eq 1) {
    if ($Verbose) {
        Write-Host "  ✅ Solution file: $($solutionFiles[0].Name)" -ForegroundColor Green
    }
} elseif ($solutionFiles.Count -eq 0) {
    $violations += "No solution file found in repository root"
    Write-Host "  ❌ No solution file found" -ForegroundColor Red
} else {
    $violations += "Multiple solution files found: $($solutionFiles.Name -join ', ')"
    Write-Host "  ❌ Multiple solution files: $($solutionFiles.Name -join ', ')" -ForegroundColor Red
}

# Summary and exit code
Write-Host ""
Write-Host "📋 Validation Summary" -ForegroundColor Cyan
Write-Host "Violations: $($violations.Count)" -ForegroundColor $(if ($violations.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ VIOLATIONS (must fix):" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host "  • $violation" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0 -and $Verbose) {
    Write-Host ""
    Write-Host "⚠️  WARNINGS (recommended):" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  • $warning" -ForegroundColor Yellow
    }
}

Write-Host ""
if ($violations.Count -eq 0) {
    Write-Host "✅ Canonical layout validation PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Canonical layout validation FAILED" -ForegroundColor Red
    if (-not $Fix) {
        Write-Host "💡 Run with -Fix to automatically create missing directories" -ForegroundColor Cyan
    }
    exit 1
}
