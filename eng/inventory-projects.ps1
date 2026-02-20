#Requires -Version 5.1
<#
.SYNOPSIS
    Inventory script for Excalibur project governance (v2.0)
.DESCRIPTION
    Scans governed directories for .csproj files, compares against Excalibur.sln,
    and generates project-manifest.yaml with v2.0 schema (framework_owner, reason fields).
.PARAMETER ManifestPath
    Output path for the YAML manifest (default: management/governance/project-manifest.yaml)
.PARAMETER Strict
    If true, exit with error code on any governance violations
.NOTES
    Originally Sprint 325 W0 Solution & Build Integrity (T0.1)
    Updated Sprint 506 — v2.0 manifest schema (framework_owner, reason, load-tests governance)
#>
param(
    [string]$ManifestPath = "management/governance/project-manifest.yaml",
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Configuration
$GovernedDirectories = @("src", "tests", "samples", "benchmarks", "load-tests")
$ExcludedDirectories = @("labs", "tools", "node_modules", "bin", "obj")
# Explicit file exclusions (duplicate projects, etc)
$ExcludedFiles = @(
    # None currently - stub files should be deleted, not excluded
)

function Get-ProjectClassification {
    param([string]$Path)

    if ($Path -match "^src/") { return "Shipping" }
    if ($Path -match "^tests/unit/") { return "Test" }
    if ($Path -match "^tests/integration/") { return "Test" }
    if ($Path -match "^tests/functional/") { return "Test" }
    if ($Path -match "^tests/conformance/") { return "Test" }
    if ($Path -match "^tests/benchmarks/") { return "Benchmark" }
    if ($Path -match "^tests/") { return "Test" }
    if ($Path -match "^samples/") { return "Sample" }
    if ($Path -match "^benchmarks/") { return "Benchmark" }
    if ($Path -match "^load-tests/") { return "Test" }
    return "Unknown"
}

function Get-FrameworkOwner {
    param([string]$Path)

    if ($Path -match "^src/Dispatch/" -or $Path -match "Dispatch\." -and $Path -notmatch "Excalibur") {
        return "Dispatch"
    }
    if ($Path -match "^src/Excalibur/" -or $Path -match "Excalibur\.") {
        return "Excalibur"
    }
    return $null
}

function Get-ProjectTier {
    param([string]$Path, [string]$Classification)

    if ($Classification -ne "Shipping") { return $null }

    if ($Path -match "(Dispatch\.Abstractions|Dispatch\.csproj|Excalibur\.Domain|Excalibur\.Data\.Abstractions)") {
        return "Core"
    }
    if ($Path -match "(SqlServer|CosmosDb|DynamoDb|Firestore|Redis|MongoDB|Postgres|Kafka|RabbitMQ|AwsSqs|AzureServiceBus|GooglePubSub)") {
        return "Provider"
    }
    if ($Path -match "Hosting") {
        return "Hosting"
    }
    return "Extension"
}

function Get-TestCategory {
    param([string]$Path)

    if ($Path -match "^tests/unit/") { return "Unit" }
    if ($Path -match "^tests/integration/") { return "Integration" }
    if ($Path -match "^tests/functional/") { return "Functional" }
    if ($Path -match "^tests/conformance/") { return "Conformance" }
    if ($Path -match "^tests/benchmarks/") { return "Benchmark" }
    if ($Path -match "^tests/Shared/" -or $Path -match "^tests/shared/") { return "Shared" }
    if ($Path -match "^tests/[Aa]rchitecture") { return "Architecture" }
    if ($Path -match "^load-tests/") { return "LoadTest" }
    return $null
}

function Get-SampleVariant {
    param([string]$Path)

    if ($Path -match "DispatchMinimal") { return "DispatchOnly" }
    if ($Path -match "ExcaliburCqrs") { return "ExcaliburFull" }
    return $null
}

# Find all .csproj files in governed directories
Write-Host "Scanning governed directories..." -ForegroundColor Cyan
$allProjects = @()
foreach ($dir in $GovernedDirectories) {
    if (Test-Path $dir) {
        $projects = Get-ChildItem -Path $dir -Recurse -Filter "*.csproj" -File |
            Where-Object {
                $exclude = $false
                foreach ($ex in $ExcludedDirectories) {
                    if ($_.FullName -match "[/\\]$([regex]::Escape($ex))[/\\]") {
                        $exclude = $true
                        break
                    }
                }
                -not $exclude
            }
        $allProjects += $projects
    }
}

Write-Host "Found $($allProjects.Count) .csproj files in governed directories" -ForegroundColor Green

# Get projects in solution
Write-Host "Reading solution file..." -ForegroundColor Cyan
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
Write-Host "Found $($slnProjects.Count) projects in solution" -ForegroundColor Green

# Build inventory
$inventory = @()
$missingFromSln = @()
$repoRoot = (Get-Location).Path

foreach ($proj in $allProjects) {
    $relativePath = $proj.FullName.Replace($repoRoot + "\", "").Replace("\", "/")

    # Skip explicitly excluded files
    if ($ExcludedFiles -contains $relativePath) {
        Write-Host "Skipping excluded file: $relativePath" -ForegroundColor DarkGray
        continue
    }
    $classification = Get-ProjectClassification -Path $relativePath
    $tier = Get-ProjectTier -Path $relativePath -Classification $classification
    $category = Get-TestCategory -Path $relativePath
    $variant = Get-SampleVariant -Path $relativePath
    $frameworkOwner = Get-FrameworkOwner -Path $relativePath
    $inSolution = $slnProjectsSet.ContainsKey($relativePath)

    $entry = [ordered]@{
        path = $relativePath
        classification = $classification
        in_solution = $inSolution
    }

    if ($tier) { $entry["tier"] = $tier }
    if ($category) { $entry["category"] = $category }
    if ($variant) { $entry["variant"] = $variant }
    if ($frameworkOwner) { $entry["framework_owner"] = $frameworkOwner }

    $inventory += $entry

    if (-not $inSolution) {
        $missingFromSln += $relativePath
    }
}

# Sort inventory by path
$inventory = $inventory | Sort-Object { $_.path }

# Report findings
Write-Host ""
Write-Host "=== INVENTORY SUMMARY ===" -ForegroundColor Yellow
Write-Host "Total governed projects: $($inventory.Count)"
Write-Host "Projects in solution: $($inventory.Count - $missingFromSln.Count)"
Write-Host "Missing from solution: $($missingFromSln.Count)" -ForegroundColor $(if ($missingFromSln.Count -gt 0) { "Red" } else { "Green" })

if ($missingFromSln.Count -gt 0) {
    Write-Host ""
    Write-Host "Projects missing from solution:" -ForegroundColor Red
    foreach ($p in $missingFromSln | Sort-Object) {
        Write-Host "  - $p"
    }
}

# Classification summary
$byClass = $inventory | Group-Object { $_.classification }
Write-Host ""
Write-Host "By classification:"
foreach ($g in $byClass) {
    Write-Host "  $($g.Name): $($g.Count)"
}

# Generate YAML manifest
$manifestDir = Split-Path $ManifestPath -Parent
if (-not (Test-Path $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
}

$yaml = @"
# Project Manifest - Excalibur Governance
# Generated: $(Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
# Schema v2.0 — Sprint 506 (extends Sprint 325 v1.0)

version: "2.0"
generated_at: "$(Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")"

governance:
  solution_file: "Excalibur.sln"

governed_directories:
  - src/**
  - tests/**
  - samples/**
  - benchmarks/**
  - load-tests/**

exclusions:
  - path: templates/**
    reason: "dotnet new templates - isolated Directory.Build.props, separate CI (eng/test-templates.ps1)"
  - path: labs/**
    reason: "Experimental - not governed"
  - path: tools/**
    reason: "Build utilities - not packaged"

projects:
"@

foreach ($entry in $inventory) {
    $yaml += "`n  - path: $($entry.path)"
    $yaml += "`n    classification: $($entry.classification)"
    $yaml += "`n    in_solution: $($entry.in_solution.ToString().ToLower())"

    if ($entry.Contains("tier") -and $entry["tier"]) {
        $yaml += "`n    tier: $($entry["tier"])"
    }
    if ($entry.Contains("category") -and $entry["category"]) {
        $yaml += "`n    category: $($entry["category"])"
    }
    if ($entry.Contains("framework_owner") -and $entry["framework_owner"]) {
        $yaml += "`n    framework_owner: $($entry["framework_owner"])"
    }
    if ($entry.Contains("variant") -and $entry["variant"]) {
        $yaml += "`n    variant: $($entry["variant"])"
        if ($entry["variant"] -eq "DispatchOnly") {
            $yaml += "`n    notes: `"Proves Dispatch-only hosting works without Excalibur`""
        }
    }
    if (-not $entry.in_solution) {
        $yaml += "`n    reason: `"Not yet added to solution - needs investigation`""
    }
    $yaml += "`n"
}

$yaml | Out-File -FilePath $ManifestPath -Encoding UTF8
Write-Host ""
Write-Host "Manifest written to: $ManifestPath" -ForegroundColor Green

# Return summary object
$summary = @{
    TotalProjects = $inventory.Count
    InSolution = $inventory.Count - $missingFromSln.Count
    MissingFromSolution = $missingFromSln.Count
    MissingProjects = $missingFromSln
    ByClassification = @{}
}
foreach ($g in $byClass) {
    $summary.ByClassification[$g.Name] = $g.Count
}

# Output as JSON for programmatic use
$summary | ConvertTo-Json -Depth 3 | Write-Output

if ($Strict -and $missingFromSln.Count -gt 0) {
    Write-Host ""
    Write-Error "STRICT MODE: $($missingFromSln.Count) projects missing from solution"
    exit 1
}
