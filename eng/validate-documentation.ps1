#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates documentation consistency against source code.

.DESCRIPTION
    Checks for common documentation drift issues:
    1. Stale CancellationToken = default in code examples
    2. Known deleted/renamed types still referenced
    3. DI extension method naming mismatches
    4. docs-site build verification

.EXAMPLE
    ./eng/validate-documentation.ps1
    ./eng/validate-documentation.ps1 -SkipBuild
#>

param(
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = 'Continue'
$script:errors = 0
$script:warnings = 0

function Write-Check {
    param([string]$Name)
    Write-Host "`n--- CHECK: $Name ---" -ForegroundColor Cyan
}

function Write-Issue {
    param(
        [string]$Severity,
        [string]$Message,
        [string]$File = ""
    )
    if ($Severity -eq "ERROR") {
        Write-Host "  ERROR: $Message" -ForegroundColor Red
        if ($File) { Write-Host "    File: $File" -ForegroundColor DarkGray }
        $script:errors++
    }
    elseif ($Severity -eq "WARN") {
        Write-Host "  WARN: $Message" -ForegroundColor Yellow
        if ($File) { Write-Host "    File: $File" -ForegroundColor DarkGray }
        $script:warnings++
    }
    else {
        Write-Host "  OK: $Message" -ForegroundColor Green
    }
}

$repoRoot = $PSScriptRoot | Split-Path -Parent
if (-not (Test-Path (Join-Path $repoRoot "docs-site"))) {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
$docsPath = Join-Path $repoRoot "docs-site" "docs"

# ============================================================
# CHECK 1: CancellationToken = default in code examples
# ============================================================
Write-Check "CancellationToken = default in docs"

$ctResults = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse |
    Select-String -Pattern "CancellationToken\s+\w+\s*=\s*default" -AllMatches

if ($ctResults.Count -gt 0) {
    foreach ($match in $ctResults) {
        Write-Issue -Severity "ERROR" -Message "Stale CancellationToken = default" -File "$($match.Filename):$($match.LineNumber)"
    }
}
else {
    Write-Issue -Severity "OK" -Message "No stale CancellationToken = default found"
}

# ============================================================
# CHECK 2: Known deleted types
# ============================================================
Write-Check "References to deleted/renamed types"

$deletedTypes = @(
    "ICloudMessagePublisher",
    "CloudMessage",
    "ICloudMessageConsumer"
)

foreach ($type in $deletedTypes) {
    $typeResults = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse |
        Select-String -Pattern $type -AllMatches

    if ($typeResults.Count -gt 0) {
        foreach ($match in $typeResults) {
            Write-Issue -Severity "WARN" -Message "Reference to deleted type '$type'" -File "$($match.Filename):$($match.LineNumber)"
        }
    }
}

if ($script:warnings -eq 0 -and $script:errors -eq 0) {
    Write-Issue -Severity "OK" -Message "No references to deleted types"
}

# ============================================================
# CHECK 3: Naming mismatches (common phantom patterns)
# ============================================================
Write-Check "Known naming mismatches"

$phantomPatterns = @{
    "AddExcaliburEventSourcingInstrumentation" = "Should be AddEventSourcingInstrumentation"
    "AddExcaliburOutboxInstrumentation"        = "Does not exist; use AddMeter(Excalibur.Outbox.*)"
}

foreach ($pattern in $phantomPatterns.Keys) {
    $results = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse |
        Select-String -Pattern $pattern -AllMatches

    if ($results.Count -gt 0) {
        foreach ($match in $results) {
            Write-Issue -Severity "WARN" -Message "Phantom API '$pattern' ($($phantomPatterns[$pattern]))" -File "$($match.Filename):$($match.LineNumber)"
        }
    }
}

# ============================================================
# CHECK 4: Structural section coverage
# ============================================================
Write-Check "Structural sections (Before You Start / See Also)"

$allDocs = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse
$totalDocs = $allDocs.Count

$beforeYouStart = ($allDocs | Select-String -Pattern "## Before You Start" -SimpleMatch | Select-Object -ExpandProperty Path -Unique).Count
$seeAlso = ($allDocs | Select-String -Pattern "## See Also" -SimpleMatch | Select-Object -ExpandProperty Path -Unique).Count
$nextSteps = ($allDocs | Select-String -Pattern "## Next Steps" -SimpleMatch | Select-Object -ExpandProperty Path -Unique).Count

$bysPercent = [math]::Round(($beforeYouStart / $totalDocs) * 100, 1)
$saPercent = [math]::Round(($seeAlso / $totalDocs) * 100, 1)

Write-Host "  Before You Start: $beforeYouStart / $totalDocs ($bysPercent%)" -ForegroundColor $(if ($bysPercent -ge 80) { "Green" } elseif ($bysPercent -ge 60) { "Yellow" } else { "Red" })
Write-Host "  See Also:         $seeAlso / $totalDocs ($saPercent%)" -ForegroundColor $(if ($saPercent -ge 60) { "Green" } elseif ($saPercent -ge 40) { "Yellow" } else { "Red" })
Write-Host "  Next Steps:       $nextSteps / $totalDocs" -ForegroundColor DarkGray

if ($bysPercent -lt 60) {
    Write-Issue -Severity "WARN" -Message "Less than 60% of pages have 'Before You Start' sections ($bysPercent%)"
}
if ($saPercent -lt 40) {
    Write-Issue -Severity "WARN" -Message "Less than 40% of pages have 'See Also' sections ($saPercent%)"
}

# ============================================================
# CHECK 5: docs-site build
# ============================================================
if (-not $SkipBuild) {
    Write-Check "docs-site build"

    $buildDir = Join-Path $repoRoot "docs-site"
    if (Test-Path (Join-Path $buildDir "package.json")) {
        Push-Location $buildDir
        try {
            $buildOutput = & npm run build 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Issue -Severity "OK" -Message "docs-site builds successfully"
            }
            else {
                Write-Issue -Severity "ERROR" -Message "docs-site build failed"
                if ($Verbose) {
                    $buildOutput | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
                }
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Issue -Severity "WARN" -Message "docs-site/package.json not found, skipping build"
    }
}
else {
    Write-Host "`n--- SKIP: docs-site build (use without -SkipBuild to enable) ---" -ForegroundColor DarkGray
}

# ============================================================
# SUMMARY
# ============================================================
Write-Host "`n============================================" -ForegroundColor White
Write-Host "Documentation Validation Summary" -ForegroundColor White
Write-Host "============================================" -ForegroundColor White
Write-Host "  Errors:   $($script:errors)" -ForegroundColor $(if ($script:errors -gt 0) { "Red" } else { "Green" })
Write-Host "  Warnings: $($script:warnings)" -ForegroundColor $(if ($script:warnings -gt 0) { "Yellow" } else { "Green" })

if ($script:errors -gt 0) {
    Write-Host "`nFAILED - Fix errors before merging." -ForegroundColor Red
    exit 1
}
elseif ($script:warnings -gt 0) {
    Write-Host "`nPASSED with warnings." -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "`nPASSED - Documentation is clean." -ForegroundColor Green
    exit 0
}
