<#
.SYNOPSIS
    Audits [UnconditionalSuppressMessage] attributes in src/ against a known baseline.

.DESCRIPTION
    Scans all .cs files under src/ for [UnconditionalSuppressMessage] attributes,
    compares them against the approved baseline (eng/ci/aot-suppression-baseline.json),
    and reports:
      - NEW suppressions not in the baseline (blocks CI)
      - STALE baseline entries no longer in source (warning)

    Baseline-first approach: existing suppressions are grandfathered. Only new
    unapproved suppressions block the build.

    Exit codes:
      0 = No new suppressions found
      1 = New unapproved suppressions detected (CI should fail)
      2 = Script error

.PARAMETER BaselinePath
    Path to the suppression baseline JSON file.

.PARAMETER SrcPath
    Root source directory to scan (default: src/).

.PARAMETER OutputPath
    Directory for audit results.

.PARAMETER GenerateBaseline
    When set, generates a new baseline from the current source instead of auditing.

.EXAMPLE
    ./Invoke-AotSuppressionAudit.ps1
    ./Invoke-AotSuppressionAudit.ps1 -GenerateBaseline
#>
[CmdletBinding()]
param(
    [string]$BaselinePath = '',
    [string]$SrcPath = '',
    [string]$OutputPath = 'aot-suppression-audit',
    [switch]$GenerateBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not $SrcPath) {
    $SrcPath = Join-Path $repoRoot 'src'
}
if (-not $BaselinePath) {
    $BaselinePath = Join-Path $repoRoot 'eng' 'ci' 'aot-suppression-baseline.json'
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# ============================================================================
# Scan source for [UnconditionalSuppressMessage] attributes
# ============================================================================

function Get-SourceSuppressions {
    param([string]$SourceRoot)

    $suppressions = @()
    $csFiles = Get-ChildItem -Path $SourceRoot -Filter '*.cs' -Recurse -File |
        Where-Object { $_.FullName -notmatch '(\\|/)obj(\\|/)' -and $_.FullName -notmatch '(\\|/)bin(\\|/)' }

    foreach ($file in $csFiles) {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $content) { continue }

        $relativePath = $file.FullName.Replace($repoRoot, '').TrimStart('\', '/')
        # Normalize to forward slashes for cross-platform consistency
        $relativePath = $relativePath -replace '\\', '/'

        $lines = $content -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match 'UnconditionalSuppressMessage') {
                # Collect the full attribute text (may span multiple lines)
                $attrText = $lines[$i]
                $j = $i + 1
                while ($j -lt $lines.Count -and $attrText -notmatch '\]') {
                    $attrText += $lines[$j]
                    $j++
                }

                # Extract warning ID (IL2xxx or IL3xxx)
                $warningId = 'unknown'
                if ($attrText -match '"(IL[23]\d{3})') {
                    $warningId = $matches[1]
                }

                # Extract justification
                $justification = ''
                if ($attrText -match 'Justification\s*=\s*"([^"]+)"') {
                    $justification = $matches[1]
                }

                $suppressions += @{
                    File          = $relativePath
                    Line          = $i + 1
                    WarningId     = $warningId
                    Justification = $justification
                }
            }
        }
    }

    return $suppressions
}

Write-Host "Scanning source for [UnconditionalSuppressMessage] attributes..." -ForegroundColor Cyan
$sourceSuppressions = @(Get-SourceSuppressions -SourceRoot $SrcPath)
Write-Host "  Found $($sourceSuppressions.Count) suppression(s) in source" -ForegroundColor Green

# ============================================================================
# Generate baseline mode
# ============================================================================

if ($GenerateBaseline) {
    Write-Host "Generating new baseline..." -ForegroundColor Yellow

    $baselineEntries = @($sourceSuppressions | ForEach-Object {
        @{
            file          = $_.File
            line          = $_.Line
            warningId     = $_.WarningId
            justification = $_.Justification
        }
    } | Sort-Object { $_.file }, { $_.line })

    $baseline = @{
        '$schema'     = 'https://json-schema.org/draft/2020-12/schema'
        description   = 'AOT suppression baseline for Excalibur.Dispatch. Existing suppressions are grandfathered. New suppressions require justification and approval.'
        generatedAt   = (Get-Date -Format 'yyyy-MM-dd')
        suppressions  = $baselineEntries
    }

    $baseline | ConvertTo-Json -Depth 5 | Out-File -FilePath $BaselinePath -Encoding utf8
    Write-Host "Baseline written to $BaselinePath ($($baselineEntries.Count) entries)" -ForegroundColor Green
    exit 0
}

# ============================================================================
# Load baseline
# ============================================================================

if (-not (Test-Path $BaselinePath)) {
    Write-Host "ERROR: Baseline file not found at $BaselinePath" -ForegroundColor Red
    Write-Host "  Run with -GenerateBaseline to create one from current source." -ForegroundColor Yellow
    exit 2
}

$baselineJson = Get-Content -Path $BaselinePath -Raw | ConvertFrom-Json
$baselineSuppressions = @($baselineJson.suppressions)
Write-Host "  Loaded $($baselineSuppressions.Count) baseline suppression(s)" -ForegroundColor Green

# ============================================================================
# Compare: find NEW suppressions not in baseline
# ============================================================================

# Build a lookup set from baseline (file + line + warningId)
$baselineSet = @{}
foreach ($b in $baselineSuppressions) {
    $key = "$($b.file)|$($b.line)|$($b.warningId)"
    $baselineSet[$key] = $true
}

# Build a lookup set from source
$sourceSet = @{}
foreach ($s in $sourceSuppressions) {
    $key = "$($s.File)|$($s.Line)|$($s.WarningId)"
    $sourceSet[$key] = $true
}

# New suppressions: in source but not in baseline
$newSuppressions = @()
foreach ($s in $sourceSuppressions) {
    $key = "$($s.File)|$($s.Line)|$($s.WarningId)"
    if (-not $baselineSet.ContainsKey($key)) {
        $newSuppressions += $s
    }
}

# Stale baseline entries: in baseline but not in source
$staleEntries = @()
foreach ($b in $baselineSuppressions) {
    $key = "$($b.file)|$($b.line)|$($b.warningId)"
    if (-not $sourceSet.ContainsKey($key)) {
        $staleEntries += $b
    }
}

# ============================================================================
# Report
# ============================================================================

$reportJson = @{
    Timestamp          = (Get-Date -Format 'o')
    SourceSuppressions = $sourceSuppressions.Count
    BaselineEntries    = $baselineSuppressions.Count
    NewSuppressions    = $newSuppressions.Count
    StaleEntries       = $staleEntries.Count
    NewDetails         = @($newSuppressions | ForEach-Object {
        @{ File = $_.File; Line = $_.Line; WarningId = $_.WarningId; Justification = $_.Justification }
    })
    StaleDetails       = @($staleEntries | ForEach-Object {
        @{ File = $_.file; Line = $_.line; WarningId = $_.warningId; Justification = $_.justification }
    })
}

$reportJsonPath = Join-Path $OutputPath 'aot-suppression-audit.json'
$reportJson | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportJsonPath -Encoding utf8

Write-Host ""
Write-Host "========================================"
Write-Host "  AOT Suppression Audit Summary"
Write-Host "========================================"
Write-Host "  Source suppressions:  $($sourceSuppressions.Count)"
Write-Host "  Baseline entries:     $($baselineSuppressions.Count)"
Write-Host "  NEW (unapproved):     $($newSuppressions.Count)"
Write-Host "  STALE (removed):      $($staleEntries.Count)"
Write-Host "========================================"
Write-Host ""

if ($newSuppressions.Count -gt 0) {
    Write-Host "NEW UNAPPROVED SUPPRESSIONS:" -ForegroundColor Red
    foreach ($s in $newSuppressions) {
        Write-Host "  $($s.File):$($s.Line) $($s.WarningId)" -ForegroundColor Red
        if ($s.Justification) {
            Write-Host "    Justification: $($s.Justification)" -ForegroundColor Yellow
        } else {
            Write-Host "    Justification: MISSING" -ForegroundColor Red
        }
    }
    Write-Host ""
    Write-Host "To approve these suppressions, add them to the baseline:" -ForegroundColor Yellow
    Write-Host "  ./eng/ci/Invoke-AotSuppressionAudit.ps1 -GenerateBaseline" -ForegroundColor Yellow
    Write-Host "  Then review and commit eng/ci/aot-suppression-baseline.json" -ForegroundColor Yellow
    Write-Host ""
}

if ($staleEntries.Count -gt 0) {
    Write-Host "STALE BASELINE ENTRIES (suppression removed from source):" -ForegroundColor Yellow
    foreach ($s in $staleEntries) {
        Write-Host "  $($s.file):$($s.line) $($s.warningId)" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Consider regenerating the baseline to remove stale entries:" -ForegroundColor Yellow
    Write-Host "  ./eng/ci/Invoke-AotSuppressionAudit.ps1 -GenerateBaseline" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Report: $reportJsonPath" -ForegroundColor Cyan

# Exit code: fail only on NEW suppressions
if ($newSuppressions.Count -gt 0) {
    Write-Host "AOT suppression audit FAILED - $($newSuppressions.Count) new unapproved suppression(s)." -ForegroundColor Red
    exit 1
}

Write-Host "AOT suppression audit PASSED." -ForegroundColor Green
exit 0
