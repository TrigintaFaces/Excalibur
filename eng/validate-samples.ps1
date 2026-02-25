#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates certified sample projects using governance-defined smoke profiles.

.DESCRIPTION
    Uses management/governance/framework-governance.json sampleFitness classification.
    - Builds all certified samples
    - Executes smoke profiles (mode=build or mode=run)
    - Reports quarantined samples
    - Fails if any sample project under samples/ is unclassified

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER Detailed
    Show detailed build and smoke output

.PARAMETER SkipRestore
    Skip dotnet restore step (faster if already restored)

.PARAMETER GovernanceMatrixPath
    Path to governance matrix file (default: management/governance/framework-governance.json)
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Detailed,

    [switch]$SkipRestore,

    [string]$GovernanceMatrixPath = 'management/governance/framework-governance.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 50
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }

    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

function Normalize-RepoPath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $PathValue
    }

    return $PathValue.Replace('\', '/')
}

$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Sample Validation (Governance-Driven)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$matrixFullPath = Join-Path $RepoRoot $GovernanceMatrixPath
if (-not (Test-Path $matrixFullPath)) {
    throw "Governance matrix not found: $matrixFullPath"
}

$matrix = ConvertFrom-JsonCompat -Json (Get-Content -Raw $matrixFullPath) -Depth 50
$CertifiedSamples = @($matrix.sampleFitness.certified | ForEach-Object { Normalize-RepoPath $_ } | Sort-Object -Unique)
$QuarantinedSamples = @($matrix.sampleFitness.quarantined | ForEach-Object { Normalize-RepoPath $_ } | Sort-Object -Unique)
$SmokeProfiles = @($matrix.sampleFitness.smokeProfiles)

if ($CertifiedSamples.Count -eq 0) {
    throw 'No certified samples configured in sampleFitness.certified.'
}

$allSamples = @(Get-ChildItem (Join-Path $RepoRoot 'samples') -Recurse -Filter '*.csproj' -File | Where-Object {
    $_.FullName -notmatch '[\\/](obj|bin)[\\/]'
} | ForEach-Object {
    $_.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/')
} | Sort-Object -Unique)

$classified = @($CertifiedSamples + $QuarantinedSamples | Sort-Object -Unique)
$unclassified = @($allSamples | Where-Object { $_ -notin $classified })

$overlap = @($CertifiedSamples | Where-Object { $_ -in $QuarantinedSamples })
if ($overlap.Count -gt 0) {
    throw "sampleFitness has overlapping certified/quarantined entries: $($overlap -join ', ')"
}

$smokeByProject = @{}
foreach ($profile in $SmokeProfiles) {
    $projectPath = Normalize-RepoPath $profile.project
    if ([string]::IsNullOrWhiteSpace($projectPath)) {
        throw 'sampleFitness.smokeProfiles contains empty project path.'
    }

    if ($smokeByProject.ContainsKey($projectPath)) {
        throw "Duplicate smoke profile for sample: $projectPath"
    }

    $mode = $profile.mode
    if ($mode -notin @('build', 'run')) {
        throw "Invalid smoke profile mode '$mode' for sample $projectPath. Expected build|run."
    }

    $timeoutSeconds = 0
    if ($null -ne $profile.timeoutSeconds) {
        [int]$timeoutSeconds = $profile.timeoutSeconds
    }

    if ($mode -eq 'run' -and $timeoutSeconds -le 0) {
        throw "Run smoke profile must define timeoutSeconds > 0 for $projectPath."
    }

    $smokeByProject[$projectPath] = [PSCustomObject]@{
        mode = $mode
        timeoutSeconds = $timeoutSeconds
    }
}

foreach ($samplePath in $CertifiedSamples) {
    if (-not $smokeByProject.ContainsKey($samplePath)) {
        throw "Certified sample is missing smoke profile: $samplePath"
    }
}

foreach ($samplePath in $smokeByProject.Keys) {
    if ($CertifiedSamples -notcontains $samplePath) {
        throw "Smoke profile targets non-certified sample: $samplePath"
    }
}

Write-Host "[1/4] Certified samples to validate: $($CertifiedSamples.Count)" -ForegroundColor Yellow
Write-Host "Quarantined samples: $($QuarantinedSamples.Count)" -ForegroundColor DarkGray
Write-Host "Unclassified samples: $($unclassified.Count)`n" -ForegroundColor $(if ($unclassified.Count -eq 0) { 'DarkGray' } else { 'Red' })

$results = @()
$buildPassed = 0
$buildFailed = 0
$smokePassed = 0
$smokeFailed = 0

foreach ($samplePath in $CertifiedSamples) {
    $fullPath = Join-Path $RepoRoot $samplePath
    $sampleName = $samplePath -replace '^samples/', ''

    Write-Host "  Building: $sampleName... " -NoNewline

    if (-not (Test-Path $fullPath)) {
        Write-Host 'FAIL (missing project)' -ForegroundColor Red
        $buildFailed++
        $results += [PSCustomObject]@{
            Sample = $sampleName
            BuildStatus = 'FAIL'
            SmokeStatus = 'SKIP'
            SmokeMode = $smokeByProject[$samplePath].mode
            Message = 'Project file not found'
        }
        continue
    }

    $verbosity = if ($Detailed) { 'minimal' } else { 'quiet' }
    $buildArgs = @('build', $fullPath, '--configuration', $Configuration, '--verbosity', $verbosity)
    if ($SkipRestore) {
        $buildArgs += '--no-restore'
    }

    $buildOutput = & dotnet @buildArgs 2>&1
    $buildExitCode = $LASTEXITCODE

    if ($buildExitCode -ne 0) {
        Write-Host 'FAIL' -ForegroundColor Red
        $buildFailed++
        $errorMatch = $buildOutput | Select-String -Pattern '(\d+) Error\(s\)'
        $errorCount = if ($errorMatch) { $errorMatch.Matches[0].Groups[1].Value } else { '?' }

        $results += [PSCustomObject]@{
            Sample = $sampleName
            BuildStatus = 'FAIL'
            SmokeStatus = 'SKIP'
            SmokeMode = $smokeByProject[$samplePath].mode
            Message = "$errorCount error(s)"
        }

        if ($Detailed) {
            Write-Host '    Build errors:' -ForegroundColor DarkGray
            $buildOutput | Where-Object { $_ -match 'error' } | ForEach-Object {
                Write-Host "    $_" -ForegroundColor DarkGray
            }
        }

        continue
    }

    Write-Host 'OK' -ForegroundColor Green
    $buildPassed++
    $results += [PSCustomObject]@{
        Sample = $sampleName
        BuildStatus = 'PASS'
        SmokeStatus = 'PENDING'
        SmokeMode = $smokeByProject[$samplePath].mode
        Message = ''
    }
}

Write-Host "`n[2/4] Running sample smoke profiles..." -ForegroundColor Yellow
foreach ($result in $results | Where-Object { $_.BuildStatus -eq 'PASS' }) {
    $profile = $smokeByProject['samples/' + $result.Sample]
    if ($null -eq $profile) {
        $result.SmokeStatus = 'FAIL'
        $result.Message = 'Missing smoke profile'
        $smokeFailed++
        continue
    }

    if ($profile.mode -eq 'build') {
        $result.SmokeStatus = 'PASS'
        $result.Message = 'Build-mode smoke profile'
        $smokePassed++
        continue
    }

    $projectPath = 'samples/' + $result.Sample
    $fullPath = Join-Path $RepoRoot $projectPath
    $timeoutSeconds = [int]$profile.timeoutSeconds

    Write-Host "  Smoke run: $($result.Sample) (timeout ${timeoutSeconds}s)... " -NoNewline

    $stdoutPath = Join-Path $RepoRoot (".sample-smoke-{0}.out.log" -f ([guid]::NewGuid().ToString('N')))
    $stderrPath = Join-Path $RepoRoot (".sample-smoke-{0}.err.log" -f ([guid]::NewGuid().ToString('N')))

    try {
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $fullPath, '--configuration', $Configuration, '--no-build') -PassThru -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $timedOut = $false
        try {
            Wait-Process -Id $process.Id -Timeout $timeoutSeconds
        }
        catch {
            $timedOut = $true
        }

        if ($timedOut) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            Write-Host 'OK (timed out after startup)' -ForegroundColor Green
            $result.SmokeStatus = 'PASS'
            $result.Message = "Run-mode smoke passed (process started, timed out after ${timeoutSeconds}s)"
            $smokePassed++
        }
        elseif ($process.ExitCode -eq 0) {
            Write-Host 'OK' -ForegroundColor Green
            $result.SmokeStatus = 'PASS'
            $result.Message = 'Run-mode smoke passed'
            $smokePassed++
        }
        else {
            Write-Host "FAIL (exit $($process.ExitCode))" -ForegroundColor Red
            $result.SmokeStatus = 'FAIL'
            $result.Message = "Run-mode smoke failed with exit code $($process.ExitCode)"
            $smokeFailed++

            if ($Detailed) {
                Write-Host "    stdout: $stdoutPath" -ForegroundColor DarkGray
                Write-Host "    stderr: $stderrPath" -ForegroundColor DarkGray
                Get-Content $stderrPath -ErrorAction SilentlyContinue | ForEach-Object {
                    Write-Host "    $_" -ForegroundColor DarkGray
                }
            }
        }
    }
    finally {
        if (-not $Detailed) {
            Remove-Item $stdoutPath -ErrorAction SilentlyContinue
            Remove-Item $stderrPath -ErrorAction SilentlyContinue
        }
    }
}

if ($unclassified.Count -gt 0) {
    $smokeFailed += $unclassified.Count
}

Write-Host "`n[3/4] Results Summary..." -ForegroundColor Yellow
Write-Host "`n--- Certified Sample Validation Results ---" -ForegroundColor White
Write-Host "  Build Passed: $buildPassed" -ForegroundColor Green
Write-Host "  Build Failed: $buildFailed" -ForegroundColor $(if ($buildFailed -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Smoke Passed: $smokePassed" -ForegroundColor Green
Write-Host "  Smoke Failed: $smokeFailed" -ForegroundColor $(if ($smokeFailed -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Quarantined: $($QuarantinedSamples.Count)" -ForegroundColor DarkGray
Write-Host "  Unclassified: $($unclassified.Count)" -ForegroundColor $(if ($unclassified.Count -eq 0) { 'DarkGray' } else { 'Red' })

if ($buildFailed -gt 0 -or $smokeFailed -gt 0) {
    Write-Host "`n--- Failed Certified Samples ---" -ForegroundColor Red
    $results | Where-Object { $_.BuildStatus -eq 'FAIL' -or $_.SmokeStatus -eq 'FAIL' } | ForEach-Object {
        Write-Host "  $($_.Sample): build=$($_.BuildStatus), smoke=$($_.SmokeStatus), mode=$($_.SmokeMode), message=$($_.Message)" -ForegroundColor Red
    }
}

if ($unclassified.Count -gt 0) {
    Write-Host "`n--- Unclassified Samples (must be certified or quarantined) ---" -ForegroundColor Red
    foreach ($sample in $unclassified) {
        Write-Host "  $sample" -ForegroundColor Red
    }
}

Write-Host "`n[4/4] Final Status..." -ForegroundColor Yellow
Write-Host "`n========================================" -ForegroundColor Cyan
if ($buildFailed -eq 0 -and $smokeFailed -eq 0 -and $unclassified.Count -eq 0) {
    Write-Host 'SUCCESS: Certified samples passed build/smoke profiles and all samples are classified.' -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    exit 0
}

Write-Host "FAILURE: build failures=$buildFailed, smoke failures=$smokeFailed, unclassified=$($unclassified.Count)." -ForegroundColor Red
Write-Host "========================================`n" -ForegroundColor Cyan
exit 1
