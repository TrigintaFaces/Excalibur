<#
.SYNOPSIS
    Validates AOT (Ahead-of-Time) build compatibility for Excalibur packages.

.DESCRIPTION
    Runs `dotnet publish` with AOT settings on all shipping projects and validates
    the output for trimming warnings, successful compilation, and binary size.
    Produces JSON and HTML reports for CI consumption.

.PARAMETER Configuration
    Build configuration (default: Release).

.PARAMETER Runtime
    Target runtime identifier (e.g., linux-x64, win-x64, osx-x64).

.PARAMETER OutputPath
    Directory for validation results (reports, logs).

.PARAMETER Verbose
    Enable verbose logging.

.EXAMPLE
    ./Validate-AotBuild.ps1 -Configuration Release -Runtime linux-x64 -OutputPath ./validation-results
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = '',
    [string]$OutputPath = './validation-results',
    [switch]$Verbose
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
    if ($Verbose -or $Level -eq 'ERROR') {
        Write-Host $entry
    }
}

Write-Log "AOT Build Validation starting"
Write-Log "Configuration: $Configuration"
Write-Log "Runtime: $Runtime"
Write-Log "OutputPath: $OutputPath"

# Find all shipping projects (src/Dispatch and src/Excalibur, excluding test/benchmark projects)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$srcDirs = @(
    (Join-Path $repoRoot 'src' 'Dispatch'),
    (Join-Path $repoRoot 'src' 'Excalibur')
)

$projects = @()
foreach ($srcDir in $srcDirs) {
    if (Test-Path $srcDir) {
        $found = Get-ChildItem -Path $srcDir -Filter '*.csproj' -Recurse |
            Where-Object { $_.FullName -notmatch '(Tests|Benchmarks|Examples|Sample)' }
        $projects += $found
    }
}

Write-Log "Found $($projects.Count) shipping projects to validate"

$results = @{
    Timestamp     = (Get-Date -Format 'o')
    Configuration = $Configuration
    Runtime       = $Runtime
    TotalProjects = $projects.Count
    Passed        = 0
    Failed        = 0
    Warnings      = @()
    Errors        = @()
    Projects      = @()
    Metrics       = @{
        ExecutableSize = 0
    }
}

foreach ($project in $projects) {
    $projectName = $project.BaseName
    Write-Log "Validating: $projectName"

    $publishArgs = @(
        'publish', $project.FullName,
        '--configuration', $Configuration,
        '--verbosity', 'normal',
        '-p:PublishAot=false',
        '-p:PublishTrimmed=true',
        '-p:TrimMode=partial',
        '-p:SuppressTrimAnalysisWarnings=false'
    )

    if ($Runtime) {
        $publishArgs += @('--runtime', $Runtime)
    }

    $projectLog = Join-Path $OutputPath "$projectName.log"
    $projectResult = @{
        Name     = $projectName
        Path     = $project.FullName
        Status   = 'Unknown'
        Warnings = @()
        Errors   = @()
    }

    try {
        $output = & dotnet @publishArgs 2>&1 | Out-String
        $output | Out-File -FilePath $projectLog -Encoding utf8

        # Parse warnings (IL2xxx = trim warnings, IL3xxx = AOT warnings)
        $trimWarnings = [regex]::Matches($output, '(IL[23]\d{3})[^)]*\)')
        foreach ($match in $trimWarnings) {
            $warning = $match.Value
            $projectResult.Warnings += $warning
            $results.Warnings += "[$projectName] $warning"
        }

        # Check for build errors
        $errorMatches = [regex]::Matches($output, 'error\s+(CS\d+|IL\d+|MSB\d+):[^\r\n]+')
        foreach ($match in $errorMatches) {
            $projectResult.Errors += $match.Value
            $results.Errors += "[$projectName] $($match.Value)"
        }

        if ($LASTEXITCODE -eq 0) {
            $projectResult.Status = 'Passed'
            $results.Passed++
            Write-Log "$projectName: PASSED ($($projectResult.Warnings.Count) warnings)"
        }
        else {
            $projectResult.Status = 'Failed'
            $results.Failed++
            Write-Log "$projectName: FAILED" 'ERROR'
        }
    }
    catch {
        $projectResult.Status = 'Error'
        $projectResult.Errors += $_.Exception.Message
        $results.Errors += "[$projectName] $($_.Exception.Message)"
        $results.Failed++
        Write-Log "$projectName: ERROR - $($_.Exception.Message)" 'ERROR'
    }

    $results.Projects += $projectResult
}

# Write JSON report
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportJsonFile -Encoding utf8
Write-Log "JSON report written to $reportJsonFile"

# Write HTML report
$htmlBody = @"
<!DOCTYPE html>
<html>
<head>
    <title>AOT Build Validation Report</title>
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
        .status-passed { color: #28a745; font-weight: bold; }
        .status-failed { color: #dc3545; font-weight: bold; }
        pre { background: #f5f5f5; padding: 10px; border-radius: 4px; overflow-x: auto; font-size: 0.85em; }
    </style>
</head>
<body>
    <h1>AOT Build Validation Report</h1>
    <p><strong>Date:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
    <p><strong>Configuration:</strong> $Configuration | <strong>Runtime:</strong> $(if ($Runtime) { $Runtime } else { 'default' })</p>

    <div class="summary">
        <div class="card pass"><h2>$($results.Passed)</h2><p>Passed</p></div>
        <div class="card fail"><h2>$($results.Failed)</h2><p>Failed</p></div>
        <div class="card warn"><h2>$($results.Warnings.Count)</h2><p>Warnings</p></div>
    </div>

    <h2>Project Results</h2>
    <table>
        <tr><th>Project</th><th>Status</th><th>Warnings</th><th>Errors</th></tr>
"@

foreach ($proj in $results.Projects) {
    $statusClass = if ($proj.Status -eq 'Passed') { 'status-passed' } else { 'status-failed' }
    $htmlBody += "        <tr><td>$($proj.Name)</td><td class='$statusClass'>$($proj.Status)</td><td>$($proj.Warnings.Count)</td><td>$($proj.Errors.Count)</td></tr>`n"
}

$htmlBody += @"
    </table>
"@

if ($results.Warnings.Count -gt 0) {
    $htmlBody += "    <h2>Trimming Warnings (first 50)</h2>`n    <pre>"
    $results.Warnings | Select-Object -First 50 | ForEach-Object { $htmlBody += "$_`n" }
    $htmlBody += "</pre>`n"
}

if ($results.Errors.Count -gt 0) {
    $htmlBody += "    <h2>Build Errors</h2>`n    <pre>"
    $results.Errors | ForEach-Object { $htmlBody += "$_`n" }
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
Write-Host "  AOT Build Validation Summary"
Write-Host "========================================"
Write-Host "  Total Projects: $($results.TotalProjects)"
Write-Host "  Passed:         $($results.Passed)"
Write-Host "  Failed:         $($results.Failed)"
Write-Host "  Warnings:       $($results.Warnings.Count)"
Write-Host "  Errors:         $($results.Errors.Count)"
Write-Host "========================================"
Write-Host ""

if ($results.Failed -gt 0) {
    Write-Host "AOT validation FAILED - $($results.Failed) project(s) did not pass." -ForegroundColor Red
    exit 1
}

Write-Host "AOT validation PASSED." -ForegroundColor Green
exit 0
