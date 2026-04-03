<#
.SYNOPSIS
    Validates AOT (Ahead-of-Time) build compatibility by publishing the AOT sample app.

.DESCRIPTION
    Runs `dotnet publish` with PublishAot=true on the AOT sample application
    (samples/11-aot/), which transitively references shipping packages.
    Captures IL2xxx (trim) and IL3xxx (AOT) warnings, groups them by package,
    and produces JSON and HTML reports for CI consumption.

    Exit codes:
      0 = No warnings or errors
      1 = IL2xxx/IL3xxx warnings detected
      2 = Script or publish error

.PARAMETER Configuration
    Build configuration (default: Release).

.PARAMETER Runtime
    Target runtime identifier (e.g., linux-x64, win-x64, osx-x64).

.PARAMETER OutputPath
    Directory for validation results (reports, logs).

.EXAMPLE
    ./Invoke-AotPublishValidation.ps1 -Configuration Release -Runtime linux-x64 -OutputPath ./validation-results
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = '',
    [string]$OutputPath = './validation-results',
    [string]$BaselinePath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$logFile = Join-Path $OutputPath 'aot-validation.log'
$reportJsonFile = Join-Path $OutputPath 'aot-validation-report.json'
$reportHtmlFile = Join-Path $OutputPath 'aot-validation-report.html'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $entry = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $entry
    if ($Level -eq 'ERROR' -or $Level -eq 'WARN') {
        Write-Host $entry
    }
}

Write-Log "AOT Publish Validation starting"
Write-Log "Configuration: $Configuration"
Write-Log "Runtime: $Runtime"
Write-Log "OutputPath: $OutputPath"

# Locate the AOT sample project
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$aotSampleProject = Join-Path $repoRoot 'samples' '11-aot' 'Excalibur.Dispatch.Aot.Sample' 'Excalibur.Dispatch.Aot.Sample.csproj'

if (-not (Test-Path $aotSampleProject)) {
    Write-Log "AOT sample project not found at: $aotSampleProject" 'ERROR'
    Write-Host "ERROR: AOT sample project not found at: $aotSampleProject" -ForegroundColor Red
    exit 2
}

Write-Log "AOT sample project: $aotSampleProject"

# Build publish arguments -- PublishAot=true and TrimMode=full are set in the sample .csproj.
# DO NOT pass -p:PublishAot=true on the command line: it cascades globally to ALL projects
# in the build graph, including netstandard2.0 source generators (causing NETSDK1207).
# The per-project settings apply only to the sample and its runtime dependencies.
$publishArgs = @(
    'publish', $aotSampleProject,
    '--configuration', $Configuration,
    '--verbosity', 'normal',
    '-p:SuppressTrimAnalysisWarnings=false',
    '-p:TrimmerSingleWarn=false'
)

if ($Runtime) {
    $publishArgs += @('--runtime', $Runtime)
}

$publishLog = Join-Path $OutputPath 'aot-publish.log'

Write-Log "Running: dotnet $($publishArgs -join ' ')"
Write-Host "Publishing AOT sample app (this may take several minutes)..." -ForegroundColor Cyan

$publishOutput = ''
$publishExitCode = 0

try {
    $publishOutput = & dotnet @publishArgs 2>&1 | Out-String
    $publishExitCode = $LASTEXITCODE
    $publishOutput | Out-File -FilePath $publishLog -Encoding utf8
}
catch {
    $publishOutput = $_.Exception.Message
    $publishExitCode = 2
    $publishOutput | Out-File -FilePath $publishLog -Encoding utf8
    Write-Log "Publish threw exception: $($_.Exception.Message)" 'ERROR'
}

Write-Log "Publish exit code: $publishExitCode"

# Parse IL2xxx (trim) and IL3xxx (AOT) warnings from output, grouped by package
$warningsByPackage = @{}
$allWarnings = @()

foreach ($line in ($publishOutput -split "`n")) {
    if ($line -match '(IL[23]\d{3})\s*:\s*(.+)') {
        $code = $matches[1]
        $message = $matches[2].Trim()

        # Try to extract package name from the warning path or assembly reference
        $packageName = 'Unknown'
        if ($line -match 'Excalibur\.Dispatch\.([\w.]+)') {
            $packageName = "Excalibur.Dispatch.$($matches[1])"
        }
        elseif ($line -match 'Excalibur\.([\w.]+)') {
            $packageName = "Excalibur.$($matches[1])"
        }
        elseif ($line -match 'Dispatch\.([\w.]+)') {
            $packageName = "Dispatch.$($matches[1])"
        }

        $warning = @{
            Code    = $code
            Message = $message
            Package = $packageName
        }
        $allWarnings += $warning

        if (-not $warningsByPackage.ContainsKey($packageName)) {
            $warningsByPackage[$packageName] = @()
        }
        $warningsByPackage[$packageName] += $warning
    }
}

# Load and apply baseline exclusions
$baselineWarnings = @()
if (-not $BaselinePath) {
    $BaselinePath = Join-Path $repoRoot 'eng' 'ci' 'aot-warning-baseline.json'
}
if (Test-Path $BaselinePath) {
    try {
        $baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json
        $baselineWarnings = @($baseline.warnings)
        Write-Log "Loaded $($baselineWarnings.Count) baselined warning(s) from $BaselinePath"
    }
    catch {
        Write-Log "Failed to parse baseline file: $_" 'WARN'
    }
}

# Filter out baselined warnings
$newWarnings = @()
foreach ($w in $allWarnings) {
    $isBaselined = $false
    foreach ($bw in $baselineWarnings) {
        if ($bw.code -eq $w.Code -and $bw.package -eq $w.Package -and $w.Message.Contains($bw.message_substring)) {
            $isBaselined = $true
            break
        }
    }
    if (-not $isBaselined) {
        $newWarnings += $w
    }
}

$baselinedCount = $allWarnings.Count - $newWarnings.Count
if ($baselinedCount -gt 0) {
    Write-Log "$baselinedCount warning(s) excluded by baseline"
}

# Replace allWarnings with only new (non-baselined) warnings for reporting
$allWarnings = $newWarnings
$warningsByPackage = @{}
foreach ($w in $allWarnings) {
    if (-not $warningsByPackage.ContainsKey($w.Package)) {
        $warningsByPackage[$w.Package] = @()
    }
    $warningsByPackage[$w.Package] += $w
}

# Also capture hard errors
$errors = @()
foreach ($line in ($publishOutput -split "`n")) {
    if ($line -match 'error\s+(CS\d+|IL\d+|MSB\d+|NETSDK\d+)\s*:\s*(.+)') {
        $errors += @{
            Code    = $matches[1]
            Message = $matches[2].Trim()
        }
    }
}

# Build results object
$results = @{
    Timestamp         = (Get-Date -Format 'o')
    Configuration     = $Configuration
    Runtime           = if ($Runtime) { $Runtime } else { 'default' }
    SampleProject     = $aotSampleProject
    PublishExitCode   = $publishExitCode
    PublishSuccess    = ($publishExitCode -eq 0)
    TotalWarnings     = $allWarnings.Count
    TotalErrors       = $errors.Count
    WarningsByPackage = @{}
    Warnings          = @()
    Errors            = @()
}

foreach ($pkg in $warningsByPackage.Keys | Sort-Object) {
    $results.WarningsByPackage[$pkg] = @($warningsByPackage[$pkg] | ForEach-Object {
        @{ Code = $_.Code; Message = $_.Message }
    })
}

$results.Warnings = @($allWarnings | ForEach-Object { "[$($_.Package)] $($_.Code): $($_.Message)" })
$results.Errors = @($errors | ForEach-Object { "$($_.Code): $($_.Message)" })

# Write JSON report
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportJsonFile -Encoding utf8
Write-Log "JSON report written to $reportJsonFile"

# Write HTML report
$htmlBody = @"
<!DOCTYPE html>
<html>
<head>
    <title>AOT Publish Validation Report</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }
        h1 { border-bottom: 2px solid #333; padding-bottom: 10px; }
        .summary { display: flex; gap: 20px; margin: 20px 0; }
        .card { padding: 15px; border-radius: 8px; flex: 1; color: #fff; }
        .card.pass { background: #28a745; }
        .card.fail { background: #dc3545; }
        .card.warn { background: #ffc107; color: #333; }
        .card h2 { margin: 0; font-size: 2em; }
        .card p { margin: 5px 0 0 0; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #ddd; }
        th { background: #f5f5f5; }
        pre { background: #f5f5f5; padding: 10px; border-radius: 4px; overflow-x: auto; font-size: 0.85em; }
    </style>
</head>
<body>
    <h1>AOT Publish Validation Report</h1>
    <p><strong>Date:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
    <p><strong>Configuration:</strong> $Configuration | <strong>Runtime:</strong> $(if ($Runtime) { $Runtime } else { 'default' })</p>
    <p><strong>Sample Project:</strong> samples/11-aot/Excalibur.Dispatch.Aot.Sample</p>

    <div class="summary">
        <div class="card $(if ($publishExitCode -eq 0) { 'pass' } else { 'fail' })">
            <h2>$(if ($publishExitCode -eq 0) { 'PASS' } else { 'FAIL' })</h2>
            <p>Publish $(if ($publishExitCode -eq 0) { 'succeeded' } else { "failed (exit $publishExitCode)" })</p>
        </div>
        <div class="card $(if ($allWarnings.Count -eq 0) { 'pass' } else { 'warn' })">
            <h2>$($allWarnings.Count)</h2>
            <p>IL2xxx/IL3xxx Warnings</p>
        </div>
        <div class="card $(if ($errors.Count -eq 0) { 'pass' } else { 'fail' })">
            <h2>$($errors.Count)</h2>
            <p>Errors</p>
        </div>
    </div>
"@

if ($warningsByPackage.Count -gt 0) {
    $htmlBody += "    <h2>Warnings by Package</h2>`n    <table>`n        <tr><th>Package</th><th>Code</th><th>Message</th></tr>`n"
    foreach ($pkg in $warningsByPackage.Keys | Sort-Object) {
        foreach ($w in $warningsByPackage[$pkg]) {
            $escapedMsg = $w.Message -replace '<', '&lt;' -replace '>', '&gt;'
            $htmlBody += "        <tr><td>$pkg</td><td>$($w.Code)</td><td>$escapedMsg</td></tr>`n"
        }
    }
    $htmlBody += "    </table>`n"
}

if ($errors.Count -gt 0) {
    $htmlBody += "    <h2>Build Errors</h2>`n    <pre>"
    foreach ($e in $errors) { $htmlBody += "$($e.Code): $($e.Message)`n" }
    $htmlBody += "</pre>`n"
}

$htmlBody += @"
</body>
</html>
"@

$htmlBody | Out-File -FilePath $reportHtmlFile -Encoding utf8
Write-Log "HTML report written to $reportHtmlFile"

# Summary
Write-Host ""
Write-Host "========================================"
Write-Host "  AOT Publish Validation Summary"
Write-Host "========================================"
Write-Host "  Publish:    $(if ($publishExitCode -eq 0) { 'SUCCESS' } else { "FAILED (exit $publishExitCode)" })"
Write-Host "  Warnings:   $($allWarnings.Count) IL2xxx/IL3xxx"
Write-Host "  Errors:     $($errors.Count)"
Write-Host "  Packages:   $($warningsByPackage.Count) with warnings"
Write-Host "========================================"
Write-Host ""

if ($warningsByPackage.Count -gt 0) {
    Write-Host "Warnings by package:" -ForegroundColor Yellow
    foreach ($pkg in $warningsByPackage.Keys | Sort-Object) {
        Write-Host "  $pkg : $($warningsByPackage[$pkg].Count) warning(s)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Determine exit code
if ($errors.Count -gt 0 -or $publishExitCode -gt 1) {
    Write-Host "AOT validation ERROR - publish failed or build errors detected." -ForegroundColor Red
    exit 2
}

if ($allWarnings.Count -gt 0) {
    Write-Host "AOT validation WARNINGS - $($allWarnings.Count) IL2xxx/IL3xxx warnings found." -ForegroundColor Yellow
    exit 1
}

Write-Host "AOT validation PASSED - zero warnings." -ForegroundColor Green
exit 0
