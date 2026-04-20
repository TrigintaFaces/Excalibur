# Git Hooks Installation Script (PowerShell)
# Installs canonical Git hooks from eng/hooks/ to .git/hooks/

$ErrorActionPreference = "Stop"

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Git Hooks Installation - Excalibur.Dispatch" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# Verify we're in the repository root
if (-not (Test-Path ".git")) {
    Write-Host "✗ ERROR: Not in repository root (no .git directory found)" -ForegroundColor Red
    Write-Host "  Please run this script from the repository root directory" -ForegroundColor Yellow
    exit 1
}

# Verify eng/hooks directory exists
if (-not (Test-Path "eng/hooks")) {
    Write-Host "✗ ERROR: eng/hooks directory not found" -ForegroundColor Red
    Write-Host "  Canonical hooks should be in eng/hooks/" -ForegroundColor Yellow
    exit 1
}

# Create .git/hooks directory if it doesn't exist
if (-not (Test-Path ".git/hooks")) {
    Write-Host "Creating .git/hooks directory..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path ".git/hooks" -Force | Out-Null
}

function Install-Hook {
    param([string]$HookName)

    $sourceHook = "eng/hooks/$HookName"
    $targetHook = ".git/hooks/$HookName"

    if (Test-Path $sourceHook) {
        Write-Host "Installing $HookName hook..." -ForegroundColor Cyan

        if (Test-Path $targetHook) {
            Write-Host "  ⚠ Existing hook found - creating backup" -ForegroundColor Yellow
            $backupPath = ".git/hooks/$HookName.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            Copy-Item $targetHook $backupPath
            Write-Host "  Backup saved: $backupPath" -ForegroundColor Gray
        }

        Copy-Item $sourceHook $targetHook -Force
        git update-index --chmod=+x $targetHook 2>$null

        Write-Host "  ✓ $HookName hook installed" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ WARNING: $sourceHook not found - skipping" -ForegroundColor Yellow
    }
}

Install-Hook "pre-commit"
Install-Hook "pre-push"

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Installation Complete" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installed Hooks:" -ForegroundColor Cyan
Get-ChildItem ".git/hooks" -File | Where-Object { $_.Name -notlike "*.sample" } | ForEach-Object {
    Write-Host "  • $($_.Name)" -ForegroundColor White
}
Write-Host ""
Write-Host "Test the pre-commit hook:" -ForegroundColor Cyan
Write-Host "  bash .git/hooks/pre-commit" -ForegroundColor Gray
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Cyan
Write-Host "  eng/hooks/README.md - Installation and usage guide" -ForegroundColor Gray
Write-Host "  .git/hooks/README.md - Detailed hook behavior (after installation)" -ForegroundColor Gray
Write-Host ""
Write-Host "✓ You're all set! Hooks installed: pre-commit (namespace depth) + pre-push (CHANGELOG)." -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
