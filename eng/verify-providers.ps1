#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Provider isolation validation script per Articles X, XV.4
.DESCRIPTION
    Validates pay-for-play packaging rules to ensure no unwanted transitive dependencies
    and enforces provider isolation boundaries for cloud providers and transports.
.PARAMETER Detailed
    Show detailed dependency analysis for each project
.PARAMETER CheckTransitive
    Analyze transitive dependencies for violations
.PARAMETER ExportReport
    Export detailed report to management/reports/
.EXAMPLE
    .\eng\verify-providers.ps1 -Detailed -CheckTransitive -ExportReport
#>
[CmdletBinding()]
param(
    [switch]$Detailed,
    [switch]$CheckTransitive,
    [switch]$ExportReport
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot | Split-Path
$srcDir = Join-Path $repoRoot "src"

Write-Host "🏗️  Provider Isolation Validation (Articles X, XV.4)" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host ""

$violations = @()
$warnings = @()
$analysisResults = @{}

# Define pay-for-play packaging rules per Article X
$providerRules = @{
    "AWS" = @{
        AllowedIn = @("*.Aws", "*.Aws.*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core", "*.Azure*", "*.Google*")
        ProhibitedPackages = @("AWSSDK.*", "Amazon.*")
    }
    "Azure" = @{
        AllowedIn = @("*.Azure", "*.Azure.*")  
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core", "*.Aws*", "*.Google*")
        ProhibitedPackages = @("Azure.*", "Microsoft.Azure.*")
    }
    "Google" = @{
        AllowedIn = @("*.Google", "*.Google.*", "*.Gcp*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core", "*.Aws*", "*.Azure*")
        ProhibitedPackages = @("Google.*", "GoogleCloud.*")
    }
    "Redis" = @{
        AllowedIn = @("*.Redis*", "*.Caching.Redis*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core")
        ProhibitedPackages = @("StackExchange.Redis", "ServiceStack.Redis")
    }
    "MongoDB" = @{
        AllowedIn = @("*.MongoDB*", "*.Mongo*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core")
        ProhibitedPackages = @("MongoDB.*", "MongoDriver")
    }
    "Postgres" = @{
        AllowedIn = @("*.Postgres*", "*.Postgres*", "*.Npgsql*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core")
        ProhibitedPackages = @("Npgsql*", "Postgres.*")
    }
    "SqlServer" = @{
        AllowedIn = @("*.SqlServer*", "*.Sql*")
        ProhibitedIn = @("Excalibur.Dispatch", "Excalibur.Dispatch.Abstractions", "Excalibur.Core")
        ProhibitedPackages = @("Microsoft.Data.SqlClient", "System.Data.SqlClient")
    }
}

# Core projects that must remain provider-agnostic
$coreProjects = @(
    "Excalibur.Dispatch",
    "Excalibur.Dispatch.Abstractions", 
    "Excalibur.Dispatch.Messaging",
    "Excalibur.Dispatch.Serialization",
    "Excalibur.Core",
    "Excalibur.Abstractions"
)

function Test-WildcardMatch {
    param($Pattern, $Text)
    $regexPattern = "^" + $Pattern.Replace("*", ".*").Replace("?", ".") + "$"
    return $Text -match $regexPattern
}

function Get-ProjectPackageReferences {
    param($ProjectPath)
    
    if (-not (Test-Path $ProjectPath)) {
        return @()
    }
    
    try {
        [xml]$project = Get-Content $ProjectPath
        $packageRefs = $project.Project.ItemGroup.PackageReference | Where-Object { $_ }
        return $packageRefs | ForEach-Object { $_.Include }
    } catch {
        Write-Warning "Failed to parse project file: $ProjectPath"
        return @()
    }
}

function Get-TransitiveDependencies {
    param($ProjectPath)
    
    if (-not $CheckTransitive) {
        return @()
    }
    
    try {
        $projectDir = Split-Path $ProjectPath
        $assetsFile = Join-Path $projectDir "obj/project.assets.json"
        
        if (-not (Test-Path $assetsFile)) {
            return @()
        }
        
        $assets = Get-Content $assetsFile | ConvertFrom-Json
        $libraries = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -notlike "*/net*" }
        return $libraries | ForEach-Object { $_.Split('/')[0] }
    } catch {
        return @()
    }
}

Write-Host "🔍 Scanning source projects..." -ForegroundColor Yellow

if (-not (Test-Path $srcDir)) {
    Write-Error "❌ src/ directory not found"
    exit 1
}

$projects = Get-ChildItem -Path $srcDir -Filter "*.csproj" -Recurse
Write-Host "Found $($projects.Count) projects" -ForegroundColor Gray
Write-Host ""

foreach ($project in $projects) {
    $projectName = $project.BaseName
    $relativePath = $project.FullName.Substring($repoRoot.Length + 1)
    
    Write-Host "📦 Analyzing: $projectName" -ForegroundColor Cyan
    if ($Detailed) {
        Write-Host "   Path: $relativePath" -ForegroundColor Gray
    }
    
    # Get package references
    $packages = Get-ProjectPackageReferences -ProjectPath $project.FullName
    $transitivePackages = Get-TransitiveDependencies -ProjectPath $project.FullName
    
    $projectAnalysis = @{
        Name = $projectName
        Path = $relativePath
        DirectPackages = $packages
        TransitivePackages = $transitivePackages
        Violations = @()
        Warnings = @()
    }
    
    # Check each provider rule
    foreach ($provider in $providerRules.Keys) {
        $rule = $providerRules[$provider]
        
        # Check if project is allowed to use this provider
        $isAllowedProject = $false
        foreach ($allowedPattern in $rule.AllowedIn) {
            if (Test-WildcardMatch -Pattern $allowedPattern -Text $projectName) {
                $isAllowedProject = $true
                break
            }
        }
        
        # Check if project is prohibited from using this provider
        $isProhibitedProject = $false
        foreach ($prohibitedPattern in $rule.ProhibitedIn) {
            if (Test-WildcardMatch -Pattern $prohibitedPattern -Text $projectName) {
                $isProhibitedProject = $true
                break
            }
        }
        
        # Check for prohibited packages
        $foundProhibitedPackages = @()
        foreach ($prohibitedPackage in $rule.ProhibitedPackages) {
            foreach ($package in ($packages + $transitivePackages)) {
                if (Test-WildcardMatch -Pattern $prohibitedPackage -Text $package) {
                    $foundProhibitedPackages += $package
                }
            }
        }
        
        # Report violations
        if ($foundProhibitedPackages.Count -gt 0) {
            if ($isProhibitedProject) {
                $violation = "Project $projectName (prohibited from $provider) contains $provider packages: $($foundProhibitedPackages -join ', ')"
                $violations += $violation
                $projectAnalysis.Violations += $violation
                Write-Host "   ❌ $provider packages found in prohibited project" -ForegroundColor Red
                
                if ($Detailed) {
                    foreach ($pkg in $foundProhibitedPackages) {
                        Write-Host "      - $pkg" -ForegroundColor Red
                    }
                }
            } elseif (-not $isAllowedProject) {
                $warning = "Project $projectName contains $provider packages but is not explicitly allowed: $($foundProhibitedPackages -join ', ')"
                $warnings += $warning
                $projectAnalysis.Warnings += $warning
                Write-Host "   ⚠️  $provider packages in non-provider project" -ForegroundColor Yellow
                
                if ($Detailed) {
                    foreach ($pkg in $foundProhibitedPackages) {
                        Write-Host "      - $pkg" -ForegroundColor Yellow
                    }
                }
            } else {
                Write-Host "   ✅ $provider packages correctly isolated" -ForegroundColor Green
            }
        }
    }
    
    # Special validation for core projects
    if ($coreProjects -contains $projectName) {
        $coreViolations = @()
        foreach ($package in ($packages + $transitivePackages)) {
            foreach ($provider in $providerRules.Keys) {
                foreach ($prohibitedPackage in $providerRules[$provider].ProhibitedPackages) {
                    if (Test-WildcardMatch -Pattern $prohibitedPackage -Text $package) {
                        $coreViolations += "$package ($provider)"
                    }
                }
            }
        }
        
        if ($coreViolations.Count -gt 0) {
            $violation = "Core project $projectName contains provider-specific packages: $($coreViolations -join ', ')"
            $violations += $violation
            $projectAnalysis.Violations += $violation
            Write-Host "   ❌ Provider packages in core project" -ForegroundColor Red
        } else {
            Write-Host "   ✅ Core project isolation maintained" -ForegroundColor Green
        }
    }
    
    if ($Detailed -and $packages.Count -gt 0) {
        Write-Host "   📋 Direct packages: $($packages.Count)" -ForegroundColor Gray
        foreach ($pkg in $packages | Sort-Object) {
            Write-Host "      - $pkg" -ForegroundColor Gray
        }
    }
    
    $analysisResults[$projectName] = $projectAnalysis
    Write-Host ""
}

# Validate no cross-provider contamination
Write-Host "🔍 Checking cross-provider contamination..." -ForegroundColor Yellow
$providerProjects = @{}
foreach ($provider in $providerRules.Keys) {
    $providerProjects[$provider] = @()
    foreach ($projectName in $analysisResults.Keys) {
        foreach ($allowedPattern in $providerRules[$provider].AllowedIn) {
            if (Test-WildcardMatch -Pattern $allowedPattern -Text $projectName) {
                $providerProjects[$provider] += $projectName
                break
            }
        }
    }
}

foreach ($provider1 in $providerProjects.Keys) {
    foreach ($provider2 in $providerProjects.Keys) {
        if ($provider1 -eq $provider2) { continue }
        
        foreach ($project1 in $providerProjects[$provider1]) {
            $analysis = $analysisResults[$project1]
            foreach ($package in ($analysis.DirectPackages + $analysis.TransitivePackages)) {
                foreach ($prohibitedPackage in $providerRules[$provider2].ProhibitedPackages) {
                    if (Test-WildcardMatch -Pattern $prohibitedPackage -Text $package) {
                        $violation = "Cross-provider contamination: $project1 ($provider1) contains $provider2 package: $package"
                        $violations += $violation
                        Write-Host "❌ Cross-contamination: $project1 → $package" -ForegroundColor Red
                    }
                }
            }
        }
    }
}

# Generate report
if ($ExportReport) {
    Write-Host "📊 Generating provider isolation report..." -ForegroundColor Yellow
    $reportDir = Join-Path $repoRoot "management/reports"
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    
    $report = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
        Summary = @{
            TotalProjects = $projects.Count
            Violations = $violations.Count
            Warnings = $warnings.Count
            Status = if ($violations.Count -eq 0) { "PASS" } else { "FAIL" }
        }
        ProviderRules = $providerRules
        ProjectAnalysis = $analysisResults
        Violations = $violations
        Warnings = $warnings
    }
    
    $reportPath = Join-Path $reportDir "provider-isolation-report.json"
    $report | ConvertTo-Json -Depth 10 | Set-Content $reportPath
    Write-Host "✅ Report saved to: $reportPath" -ForegroundColor Green
}

# Summary
Write-Host "📋 Provider Isolation Summary" -ForegroundColor Cyan
Write-Host "Projects analyzed: $($projects.Count)" -ForegroundColor Gray
Write-Host "Violations: $($violations.Count)" -ForegroundColor $(if ($violations.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ VIOLATIONS (must fix):" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host "  • $violation" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0 -and $Detailed) {
    Write-Host ""
    Write-Host "⚠️  WARNINGS:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  • $warning" -ForegroundColor Yellow
    }
}

Write-Host ""
if ($violations.Count -eq 0) {
    Write-Host "✅ Provider isolation validation PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Provider isolation validation FAILED" -ForegroundColor Red
    Write-Host "💡 Review pay-for-play packaging rules and move provider dependencies to appropriate projects" -ForegroundColor Cyan
    exit 1
}