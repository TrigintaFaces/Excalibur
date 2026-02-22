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

function Normalize-RepoPath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    return $PathValue.Replace('\\', '/').Trim().TrimStart('./').ToLowerInvariant()
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
$shippingCanonicalByNormalized = @{}
foreach ($project in $shippingProjects) {
    $normalized = Normalize-RepoPath -PathValue $project
    if ($normalized) {
        $shippingSet[$normalized] = $true
        if (-not $shippingCanonicalByNormalized.ContainsKey($normalized)) {
            $shippingCanonicalByNormalized[$normalized] = $project
        }
    }
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
$packableCanonicalByNormalized = @{}
foreach ($project in $packableProjects) {
    $normalized = Normalize-RepoPath -PathValue $project
    if ($normalized) {
        $packableSet[$normalized] = $true
        if (-not $packableCanonicalByNormalized.ContainsKey($normalized)) {
            $packableCanonicalByNormalized[$normalized] = $project
        }
    }
}

$missing = @()
foreach ($normalized in $packableSet.Keys) {
    if (-not $shippingSet.ContainsKey($normalized)) {
        $missing += $packableCanonicalByNormalized[$normalized]
    }
}

$extra = @()
foreach ($normalized in $shippingSet.Keys) {
    if (-not $packableSet.ContainsKey($normalized)) {
        $extra += $shippingCanonicalByNormalized[$normalized]
    }
}

$casingMismatches = @()
foreach ($normalized in $packableSet.Keys) {
    if ($shippingSet.ContainsKey($normalized)) {
        $expectedPath = $packableCanonicalByNormalized[$normalized]
        $actualPath = $shippingCanonicalByNormalized[$normalized]
        if ($expectedPath -cne $actualPath) {
            $casingMismatches += [pscustomobject]@{
                Expected = $expectedPath
                Actual = $actualPath
            }
        }
    }
}

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
    casingMismatches = @($casingMismatches | Sort-Object Expected)
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
$lines += "- Casing mismatches (non-blocking): $($casingMismatches.Count)"
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

if ($casingMismatches.Count -gt 0) {
    $lines += "## Path Casing Mismatches (Non-Blocking)"
    foreach ($item in ($casingMismatches | Sort-Object Expected)) {
        $lines += "- expected: $($item.Expected)"
        $lines += "  actual:   $($item.Actual)"
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
    if ($missing.Count -gt 0) {
        Write-Host "Missing projects from ShippingOnly.slnf:" -ForegroundColor Red
        foreach ($item in ($missing | Sort-Object)) {
            Write-Host " - $item"
        }
    }
    if ($extra.Count -gt 0) {
        Write-Host "Extra projects in ShippingOnly.slnf:" -ForegroundColor Red
        foreach ($item in ($extra | Sort-Object)) {
            Write-Host " - $item"
        }
    }
    throw "Shipping filter validation failed."
}

if ($casingMismatches.Count -gt 0) {
    Write-Warning "Shipping filter has $($casingMismatches.Count) case-only path mismatch(es)."
}

Write-Host "Shipping filter validation passed."
