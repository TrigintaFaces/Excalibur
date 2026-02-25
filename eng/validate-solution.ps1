#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates solution governance, solution path casing, and src project membership.
.DESCRIPTION
    Ensures all governed projects are tracked in the manifest, all required projects are in
    Excalibur.sln, and .sln project paths use the exact repository casing.
    Designed for CI enforcement (exit 1 on failure).
.PARAMETER ManifestPath
    Path to project-manifest.yaml (default: management/governance/project-manifest.yaml)
#>
param(
    [string]$ManifestPath = "management/governance/project-manifest.yaml"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$exitCode = 0
$errors = @()
$warnings = @()
$repoRoot = (Get-Location).Path
$slnPath = "Excalibur.sln"
$ExcludedDirectories = @("labs", "tools", ".claude", "node_modules", "bin", "obj")

function Convert-ToRepoPath {
    param(
        [Parameter(Mandatory = $true)][string]$FullPath,
        [Parameter(Mandatory = $true)][string]$RepoRootPath
    )

    $normalizedFull = [System.IO.Path]::GetFullPath($FullPath)
    $normalizedRoot = [System.IO.Path]::GetFullPath($RepoRootPath)

    if ($normalizedFull.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $normalizedFull.Substring($normalizedRoot.Length).TrimStart('\', '/')
        return $relative.Replace('\', '/')
    }

    return $FullPath.Replace('\', '/')
}

function Test-IsExcludedPath {
    param(
        [Parameter(Mandatory = $true)][string]$PathToCheck,
        [Parameter(Mandatory = $true)][string[]]$Exclusions
    )

    foreach ($ex in $Exclusions) {
        if ($PathToCheck -match "[/\\]$([regex]::Escape($ex))[/\\]") {
            return $true
        }
    }

    return $false
}

function Get-ProjectClassification {
    param([string]$Path)

    if ($Path -match "^src/") { return "Shipping" }
    if ($Path -match "^tests/benchmarks/") { return "Benchmark" }
    if ($Path -match "^tests/") { return "Test" }
    if ($Path -match "^benchmarks/") { return "Benchmark" }
    if ($Path -match "^load-tests/") { return "Test" }
    if ($Path -match "^samples/") { return "Sample" }
    return "Unknown"
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Solution Governance Validation" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# --- 1. Verify manifest exists ---
if (-not (Test-Path $ManifestPath)) {
    Write-Host "FAIL: Manifest not found at $ManifestPath" -ForegroundColor Red
    Write-Host "Run: pwsh eng/inventory-projects.ps1" -ForegroundColor Yellow
    exit 1
}

# --- 2. Parse manifest ---
Write-Host "[1/7] Parsing manifest..." -ForegroundColor Yellow

$manifestLines = Get-Content $ManifestPath

$version = ($manifestLines | Where-Object { $_ -match '^version:\s*"(.+)"' } | ForEach-Object { $Matches[1] }) | Select-Object -First 1
if (-not $version) {
    $warnings += "Manifest missing version field (legacy format detected)"
}
Write-Host "  Manifest version: $version"

$governedDirs = @()
$inGoverned = $false
foreach ($line in $manifestLines) {
    if ($line -match '^governed_directories:') { $inGoverned = $true; continue }
    if ($inGoverned -and $line -match '^\s+-\s+(.+)/\*\*') {
        $governedDirs += $Matches[1]
    }
    elseif ($inGoverned -and $line -notmatch '^\s+-' -and $line -notmatch '^\s*$') {
        $inGoverned = $false
    }
}
Write-Host "  Governed directories: $($governedDirs -join ', ')"

if ($governedDirs.Count -eq 0) {
    $governedDirs = @("src", "tests", "samples", "benchmarks", "load-tests")
    $warnings += "Manifest missing governed_directories; using default governed directories"
    Write-Host "  Using fallback governed directories: $($governedDirs -join ', ')" -ForegroundColor Yellow
}

$manifestProjects = @{}
$currentProject = $null
$inProjects = $false

foreach ($line in $manifestLines) {
    if ($line -match '^projects:') { $inProjects = $true; continue }
    if (-not $inProjects) { continue }

    if ($line -match '^\s+-\s+path:\s+(.+)') {
        $currentProject = $Matches[1].Trim().Replace('\', '/')
        $manifestProjects[$currentProject] = @{
            classification = $null
            in_solution = $null
            framework_owner = $null
        }
        continue
    }
    if (-not $currentProject) { continue }

    if ($line -match '^\s+classification:\s+(.+)') {
        $manifestProjects[$currentProject].classification = $Matches[1].Trim()
    }
    elseif ($line -match '^\s+in_solution:\s+(.+)') {
        $manifestProjects[$currentProject].in_solution = $Matches[1].Trim() -eq 'true'
    }
    elseif ($line -match '^\s+framework_owner:\s+(.+)') {
        $manifestProjects[$currentProject].framework_owner = $Matches[1].Trim()
    }
}

Write-Host "  Manifest entries: $($manifestProjects.Count)"

foreach ($path in $manifestProjects.Keys) {
    if (-not $manifestProjects[$path].classification) {
        $manifestProjects[$path].classification = Get-ProjectClassification -Path $path
    }
}

# --- 3. Parse solution and validate path casing ---
Write-Host "`n[2/7] Validating solution project paths..." -ForegroundColor Yellow

if (-not (Test-Path $slnPath)) {
    Write-Host "FAIL: Solution file not found: $slnPath" -ForegroundColor Red
    exit 1
}

$slnProjectPaths = @()
foreach ($line in Get-Content $slnPath) {
    if ($line -match '^Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)"') {
        $slnProjectPaths += $Matches[1].Replace('\', '/')
    }
}

if ($slnProjectPaths.Count -eq 0) {
    $exitCode = 1
    $errors += "No .csproj entries found in $slnPath"
}
else {
    Write-Host "  Projects in solution: $($slnProjectPaths.Count)"
}

$duplicates = @($slnProjectPaths | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })
if ($duplicates.Count -gt 0) {
    $exitCode = 1
    $errors += "Duplicate .sln project entries that differ only by path casing: $($duplicates.Count)"
    foreach ($d in $duplicates) {
        Write-Host "  DUPLICATE CASE PATH: $($d.Group -join ', ')" -ForegroundColor Red
    }
}

$canonicalByLower = @{}
$gitPaths = @()
try {
    $gitPaths = @(git ls-files '*.csproj' 2>$null)
}
catch {
    $gitPaths = @()
}

if ($gitPaths.Count -gt 0) {
    foreach ($path in $gitPaths) {
        $normalized = $path.Trim().Replace('\', '/')
        if (-not [string]::IsNullOrWhiteSpace($normalized)) {
            $canonicalByLower[$normalized.ToLowerInvariant()] = $normalized
        }
    }
}
else {
    Write-Host "  WARNING: git ls-files unavailable, falling back to filesystem scan for casing checks" -ForegroundColor Yellow
    foreach ($proj in Get-ChildItem -Path "." -Recurse -Filter "*.csproj" -File) {
        if (Test-IsExcludedPath -PathToCheck $proj.FullName -Exclusions $ExcludedDirectories) { continue }
        $normalized = Convert-ToRepoPath -FullPath $proj.FullName -RepoRootPath $repoRoot
        $canonicalByLower[$normalized.ToLowerInvariant()] = $normalized
    }
}

$caseMismatches = @()
$missingSlnTargets = @()
foreach ($projectPath in $slnProjectPaths) {
    $key = $projectPath.ToLowerInvariant()
    if (-not $canonicalByLower.ContainsKey($key)) {
        $missingSlnTargets += $projectPath
        continue
    }

    $canonical = $canonicalByLower[$key]
    if ($canonical -cne $projectPath) {
        $caseMismatches += [PSCustomObject]@{
            SlnPath = $projectPath
            CanonicalPath = $canonical
        }
    }
}

if (@($missingSlnTargets).Count -gt 0) {
    $exitCode = 1
    $errors += "Solution references missing/non-tracked .csproj paths: $($missingSlnTargets.Count)"
    foreach ($p in ($missingSlnTargets | Sort-Object)) {
        Write-Host "  MISSING FROM REPO: $p" -ForegroundColor Red
    }
}

if (@($caseMismatches).Count -gt 0) {
    $exitCode = 1
    $errors += "Solution path casing mismatches: $($caseMismatches.Count)"
    foreach ($m in $caseMismatches | Sort-Object SlnPath) {
        Write-Host "  CASE MISMATCH: $($m.SlnPath) -> $($m.CanonicalPath)" -ForegroundColor Red
    }
}
else {
    Write-Host "  Solution path casing matches repository casing" -ForegroundColor Green
}

$slnProjectsSet = @{}
foreach ($p in $slnProjectPaths) {
    $slnProjectsSet[$p] = $true
}

# --- 4. Scan filesystem for governed projects ---
Write-Host "`n[3/7] Scanning governed projects on disk..." -ForegroundColor Yellow

$diskPaths = @()
foreach ($dir in $governedDirs) {
    if (-not (Test-Path $dir)) { continue }

    $projects = Get-ChildItem -Path $dir -Recurse -Filter "*.csproj" -File | Where-Object {
        -not (Test-IsExcludedPath -PathToCheck $_.FullName -Exclusions $ExcludedDirectories)
    }

    foreach ($proj in $projects) {
        $diskPaths += (Convert-ToRepoPath -FullPath $proj.FullName -RepoRootPath $repoRoot)
    }
}

$diskPaths = $diskPaths | Sort-Object -Unique
Write-Host "  Projects on disk (governed dirs): $($diskPaths.Count)"

# --- 5. Check manifest completeness + stale entries ---
Write-Host "`n[4/7] Checking manifest completeness..." -ForegroundColor Yellow

$missingFromManifest = @()
foreach ($path in $diskPaths) {
    if (-not $manifestProjects.ContainsKey($path)) {
        $missingFromManifest += $path
    }
}

if (@($missingFromManifest).Count -gt 0) {
    $exitCode = 1
    $errors += "Projects on disk but not in manifest: $($missingFromManifest.Count)"
    foreach ($p in ($missingFromManifest | Sort-Object)) {
        Write-Host "  MISSING IN MANIFEST: $p" -ForegroundColor Red
    }
}
else {
    Write-Host "  All governed projects are in manifest" -ForegroundColor Green
}

Write-Host "`n[5/7] Checking stale manifest entries..." -ForegroundColor Yellow

$staleEntries = @()
foreach ($path in $manifestProjects.Keys) {
    if ($diskPaths -notcontains $path) {
        $staleEntries += $path
    }
}

if (@($staleEntries).Count -gt 0) {
    $exitCode = 1
    $errors += "Stale manifest entries (missing on disk): $($staleEntries.Count)"
    foreach ($p in ($staleEntries | Sort-Object)) {
        Write-Host "  STALE: $p" -ForegroundColor Red
    }
}
else {
    Write-Host "  No stale manifest entries" -ForegroundColor Green
}

# --- 6. Check solution membership ---
Write-Host "`n[6/7] Checking solution membership..." -ForegroundColor Yellow

$mustBeInSln = @("Shipping", "Test", "Benchmark")
$notInSlnByClassification = @()
foreach ($path in $manifestProjects.Keys) {
    $entry = $manifestProjects[$path]
    if ($mustBeInSln -contains $entry.classification) {
        if (-not $slnProjectsSet.ContainsKey($path)) {
            $notInSlnByClassification += "$path ($($entry.classification))"
        }
    }
}

if (@($notInSlnByClassification).Count -gt 0) {
    $exitCode = 1
    $errors += "Governed Shipping/Test/Benchmark projects not in solution: $($notInSlnByClassification.Count)"
    foreach ($p in ($notInSlnByClassification | Sort-Object)) {
        Write-Host "  NOT IN SLN: $p" -ForegroundColor Red
    }
}
else {
    Write-Host "  All governed Shipping/Test/Benchmark projects are in solution" -ForegroundColor Green
}

$srcPaths = @()
if (Test-Path "src") {
    $srcProjects = Get-ChildItem -Path "src" -Recurse -Filter "*.csproj" -File | Where-Object {
        -not (Test-IsExcludedPath -PathToCheck $_.FullName -Exclusions $ExcludedDirectories)
    }
    foreach ($proj in $srcProjects) {
        $srcPaths += (Convert-ToRepoPath -FullPath $proj.FullName -RepoRootPath $repoRoot)
    }
}
$srcPaths = $srcPaths | Sort-Object -Unique

$srcNotInSln = @()
foreach ($path in $srcPaths) {
    if (-not $slnProjectsSet.ContainsKey($path)) {
        $srcNotInSln += $path
    }
}

if (@($srcNotInSln).Count -gt 0) {
    $exitCode = 1
    $errors += "src projects missing from Excalibur.sln: $($srcNotInSln.Count)"
    foreach ($p in ($srcNotInSln | Sort-Object)) {
        Write-Host "  SRC NOT IN SLN: $p" -ForegroundColor Red
    }
}
else {
    Write-Host "  All src/**/*.csproj projects are included in Excalibur.sln" -ForegroundColor Green
}

# --- 7. Check framework ownership ---
Write-Host "`n[7/7] Checking framework ownership metadata..." -ForegroundColor Yellow

$hasOwnershipMetadata = (@($manifestProjects.Values | Where-Object { -not [string]::IsNullOrWhiteSpace($_.framework_owner) })).Count -gt 0
if (-not $hasOwnershipMetadata) {
    $governanceMatrixPath = Join-Path $repoRoot "management/governance/framework-governance.json"
    if (Test-Path $governanceMatrixPath) {
        Write-Host "  framework_owner metadata not present in manifest; ownership governed by framework-governance.json" -ForegroundColor Green
    }
    else {
        $warnings += "Manifest does not include framework_owner metadata; ownership check skipped"
        Write-Host "  framework_owner metadata not present in manifest; skipping check" -ForegroundColor Yellow
    }
}
else {
    $missingOwner = @()
    foreach ($path in $manifestProjects.Keys) {
        $entry = $manifestProjects[$path]
        if ($entry.classification -eq "Shipping" -and -not $entry.framework_owner) {
            $missingOwner += $path
        }
    }

    if (@($missingOwner).Count -gt 0) {
        $warnings += "Shipping projects without framework_owner: $($missingOwner.Count)"
        foreach ($p in ($missingOwner | Sort-Object)) {
            Write-Host "  WARNING: $p (no framework_owner)" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  All Shipping projects have framework_owner" -ForegroundColor Green
    }
}

# --- Summary ---
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Manifest entries: $($manifestProjects.Count)"
Write-Host "  Projects on disk (governed): $($diskPaths.Count)"
Write-Host "  Projects in solution: $($slnProjectPaths.Count)"
Write-Host "  Errors: $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  Warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })

if ($errors.Count -gt 0) {
    Write-Host "`nERRORS:" -ForegroundColor Red
    foreach ($e in $errors) {
        Write-Host "  - $e" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "`nWARNINGS:" -ForegroundColor Yellow
    foreach ($w in $warnings) {
        Write-Host "  - $w" -ForegroundColor Yellow
    }
}

if ($exitCode -eq 0) {
    Write-Host "`nPASSED: Solution governance validation succeeded" -ForegroundColor Green
}
else {
    Write-Host "`nFAILED: Fix errors above, then re-run: pwsh eng/validate-solution.ps1" -ForegroundColor Red
}

exit $exitCode
