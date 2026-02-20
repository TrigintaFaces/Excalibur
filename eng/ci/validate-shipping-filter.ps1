#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates eng/ci/shards/ShippingOnly.slnf includes all and only packable src projects.
.DESCRIPTION
    Scans src/**/*.csproj, computes effective IsPackable (default true), and compares
    with the projects listed in eng/ci/shards/ShippingOnly.slnf.
#>
param(
    [string]$SolutionFilter = "eng/ci/shards/ShippingOnly.slnf",
    [string]$SourceRoot = "src",
    [string]$OutDir = "management/reports/SolutionGovernanceReport",
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $SolutionFilter)) {
    throw "Solution filter not found: $SolutionFilter"
}

if (-not (Test-Path $SourceRoot)) {
    throw "Source root not found: $SourceRoot"
}

$repoRoot = (Get-Location).Path

function Convert-ToRepoPath {
    param([string]$FullPath)

    $normalizedFull = [System.IO.Path]::GetFullPath($FullPath)
    $normalizedRoot = [System.IO.Path]::GetFullPath($repoRoot)
    $relative = $normalizedFull.Substring($normalizedRoot.Length).TrimStart('\', '/')
    return $relative.Replace('\\', '/')
}

function Get-IsPackable {
    param([string]$ProjectPath)

    [xml]$csproj = Get-Content -Raw $ProjectPath
    $nodes = @($csproj.SelectNodes("//Project/PropertyGroup/IsPackable"))

    foreach ($node in $nodes) {
        if ($null -eq $node) { continue }

        $value = $node.InnerText.Trim().ToLowerInvariant()
        if ($value -eq "false") {
            return $false
        }

        if ($value -eq "true") {
            return $true
        }
    }

    return $true
}

$slnf = Get-Content -Raw $SolutionFilter | ConvertFrom-Json
$shippingProjects = @($slnf.solution.projects | ForEach-Object { $_.Replace('\\', '/') })
$shippingSet = @{}
foreach ($project in $shippingProjects) {
    $shippingSet[$project] = $true
}

$srcProjects = Get-ChildItem -Path $SourceRoot -Recurse -Filter "*.csproj" -File | ForEach-Object { Convert-ToRepoPath -FullPath $_.FullName }
$srcProjects = @($srcProjects | Sort-Object -Unique)

$packableProjects = @()
foreach ($project in $srcProjects) {
    if (Get-IsPackable -ProjectPath $project) {
        $packableProjects += $project
    }
}

$packableProjects = @($packableProjects | Sort-Object -Unique)
$packableSet = @{}
foreach ($project in $packableProjects) {
    $packableSet[$project] = $true
}

$missing = @($packableProjects | Where-Object { -not $shippingSet.ContainsKey($_) })
$extra = @($shippingProjects | Where-Object { -not $packableSet.ContainsKey($_) })

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$summaryPath = Join-Path $OutDir "shipping-filter-validation.md"
$jsonPath = Join-Path $OutDir "shipping-filter-validation.json"

$report = [PSCustomObject]@{
    solutionFilter = $SolutionFilter
    sourceRoot = $SourceRoot
    shippingProjectCount = $shippingProjects.Count
    packableProjectCount = $packableProjects.Count
    missingFromShippingFilter = @($missing | Sort-Object)
    extraInShippingFilter = @($extra | Sort-Object)
}

$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

$lines = @()
$lines += "# Shipping Filter Validation"
$lines += ""
$lines += "- Solution filter: $SolutionFilter"
$lines += "- Packable src projects: $($packableProjects.Count)"
$lines += "- Projects in filter: $($shippingProjects.Count)"
$lines += "- Missing from filter: $($missing.Count)"
$lines += "- Extra in filter: $($extra.Count)"
$lines += ""

if ($missing.Count -gt 0) {
    $lines += "## Missing From eng/ci/shards/ShippingOnly.slnf"
    foreach ($item in ($missing | Sort-Object)) {
        $lines += "- $item"
    }
    $lines += ""
}

if ($extra.Count -gt 0) {
    $lines += "## Extra Entries In eng/ci/shards/ShippingOnly.slnf"
    foreach ($item in ($extra | Sort-Object)) {
        $lines += "- $item"
    }
    $lines += ""
}

if ($missing.Count -eq 0 -and $extra.Count -eq 0) {
    $lines += "## Result"
    $lines += "eng/ci/shards/ShippingOnly.slnf exactly matches packable src projects."
    $lines += ""
}

$lines | Out-File -FilePath $summaryPath -Encoding UTF8

Write-Host "Wrote summary: $summaryPath"
Write-Host "Wrote report: $jsonPath"

if ($Enforce -and ($missing.Count -gt 0 -or $extra.Count -gt 0)) {
    throw "Shipping filter validation failed."
}

Write-Host "Shipping filter validation passed."
