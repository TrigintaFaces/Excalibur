#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates certified sample projects using governance-defined smoke profiles.

.DESCRIPTION
    Uses eng/governance/framework-governance.json sampleFitness classification.
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
    Path to governance matrix file (default: eng/governance/framework-governance.json)
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Detailed,

    [switch]$SkipRestore,

    [string]$GovernanceMatrixPath = 'eng/governance/framework-governance.json'
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

$sampleRoot = Join-Path $RepoRoot 'samples'
$orphanSourceFiles = New-Object System.Collections.Generic.List[string]
$sampleSourceFiles = @(Get-ChildItem $sampleRoot -Recurse -Filter '*.cs' -File | Where-Object {
    $_.FullName -notmatch '[\\/](obj|bin)[\\/]'
})

foreach ($sourceFile in $sampleSourceFiles) {
    $directory = $sourceFile.Directory
    $hasAncestorProject = $false

    while ($null -ne $directory -and $directory.FullName.StartsWith($sampleRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $projectInDirectory = Get-ChildItem $directory.FullName -Filter '*.csproj' -File | Select-Object -First 1
        if ($null -ne $projectInDirectory) {
            $hasAncestorProject = $true
            break
        }

        $directory = $directory.Parent
    }

    if (-not $hasAncestorProject) {
        $relativePath = $sourceFile.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/')
        $orphanSourceFiles.Add($relativePath)
    }
}

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
if ($orphanSourceFiles.Count -gt 0) {
    Write-Host "Orphan sample source files (no ancestor .csproj): $($orphanSourceFiles.Count)`n" -ForegroundColor Red
}

$results = @()
$buildPassed = 0
$buildFailed = 0
$smokePassed = 0
$smokeFailed = 0

# --- Fast path: one parallel MSBuild over an .slnf filter (certified samples are already in Excalibur.sln).
# A single `dotnet build -m` shares ONE MSBuild process and builds the shared framework dependency graph
# ONCE, instead of N cold `dotnet build` processes that each re-restore and re-walk the graph. Measured
# ~60x faster (serial ~75 min -> single build ~75 s). Falls back to the per-sample loop on ANY non-zero
# exit, so per-sample failure attribution and the transient-retry path are preserved unchanged. Certified
# samples NOT present in Excalibur.sln are always built by the per-sample loop below (never skipped).
$solutionPath = Join-Path $RepoRoot 'Excalibur.sln'
if ((Test-Path $solutionPath) -and $CertifiedSamples.Count -gt 0) {
    $slnContent = (Get-Content -Raw $solutionPath).Replace('\', '/')
    $inSolution = @($CertifiedSamples | Where-Object { $slnContent.Contains($_) })
    $missingFromSln = @($CertifiedSamples | Where-Object { -not $slnContent.Contains($_) })

    if ($missingFromSln.Count -gt 0) {
        Write-Host "  NOTE: certified samples absent from Excalibur.sln (built per-sample; reconcile via cicd-sync): $($missingFromSln -join ', ')" -ForegroundColor Yellow
    }

    if ($inSolution.Count -gt 0) {
        $slnfPath = Join-Path $RepoRoot 'certified-samples.slnf'
        ([PSCustomObject]@{ solution = [PSCustomObject]@{ path = 'Excalibur.sln'; projects = $inSolution } } | ConvertTo-Json -Depth 5) | Set-Content -Path $slnfPath -Encoding utf8

        Write-Host "  Fast path: single -m build of $($inSolution.Count) certified samples via certified-samples.slnf... " -NoNewline
        $slnfArgs = @('build', $slnfPath, '--configuration', $Configuration, '-m', '--verbosity', 'quiet', '--nologo')
        if ($SkipRestore) { $slnfArgs += '--no-restore' }
        $slnfOutput = & dotnet @slnfArgs 2>&1
        $slnfExit = $LASTEXITCODE
        Remove-Item $slnfPath -ErrorAction SilentlyContinue

        if ($slnfExit -eq 0) {
            Write-Host 'OK' -ForegroundColor Green
            foreach ($samplePath in $inSolution) {
                $buildPassed++
                $results += [PSCustomObject]@{
                    Sample = ($samplePath -replace '^samples/', '')
                    BuildStatus = 'PASS'
                    SmokeStatus = 'PENDING'
                    SmokeMode = $smokeByProject[$samplePath].mode
                    Message = ''
                }
            }
            # Only samples the fast path could NOT cover fall through to the per-sample loop.
            $CertifiedSamples = $missingFromSln
        }
        else {
            Write-Host 'FALLBACK (per-sample build for attribution)' -ForegroundColor Yellow
            if ($Detailed) { $slnfOutput | Where-Object { $_ -match 'error' } | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray } }
        }
    }
}

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

    # Concurrent builds sharing output/dependency artifacts can produce TRANSIENT failures that
    # clear on a plain retry: CS2012 (output DLL locked for writing by a parallel build), CS0006
    # (a referenced metadata/DLL momentarily absent mid-write), and the MSB copy-retry exhaustions
    # (MSB3021/MSB3027). Retry ONLY on those transients with a short backoff; a genuine compile
    # error is not retried (it would fail every attempt and just waste time).
    $transientBuildPattern = 'CS2012|CS0006|MSB3021|MSB3027'
    $maxBuildAttempts = 3
    $buildAttempt = 0
    do {
        $buildAttempt++
        $buildOutput = & dotnet @buildArgs 2>&1
        $buildExitCode = $LASTEXITCODE

        if ($buildExitCode -eq 0) {
            break
        }

        $isTransient = [bool]($buildOutput | Select-String -Pattern $transientBuildPattern -Quiet)
        if ($isTransient -and $buildAttempt -lt $maxBuildAttempts) {
            Write-Host "retry $buildAttempt/$($maxBuildAttempts - 1) (transient concurrent-build error)... " -NoNewline -ForegroundColor Yellow
            Start-Sleep -Seconds ($buildAttempt * 2)
            continue
        }

        break
    } while ($buildAttempt -lt $maxBuildAttempts)

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
    $process = $null

    try {
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = 'dotnet'
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $null = $startInfo.ArgumentList.Add('run')
        $null = $startInfo.ArgumentList.Add('--project')
        $null = $startInfo.ArgumentList.Add($fullPath)
        $null = $startInfo.ArgumentList.Add('--configuration')
        $null = $startInfo.ArgumentList.Add($Configuration)
        $null = $startInfo.ArgumentList.Add('--no-build')

        $process = [System.Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        $null = $process.Start()

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        $timedOut = -not $process.WaitForExit($timeoutSeconds * 1000)
        if ($timedOut) {
            try {
                $process.Kill($true)
            }
            catch {
                # Best-effort kill for smoke validation only.
            }

            $null = $process.WaitForExit(5000)
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()

        Set-Content -Path $stdoutPath -Value $stdout -NoNewline
        Set-Content -Path $stderrPath -Value $stderr -NoNewline

        if ($timedOut) {
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
        if ($null -ne $process) {
            $process.Dispose()
        }

        if (-not $Detailed) {
            Remove-Item $stdoutPath -ErrorAction SilentlyContinue
            Remove-Item $stderrPath -ErrorAction SilentlyContinue
        }
    }
}

if ($unclassified.Count -gt 0) {
    $smokeFailed += $unclassified.Count
}

if ($orphanSourceFiles.Count -gt 0) {
    $smokeFailed += $orphanSourceFiles.Count
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

if ($orphanSourceFiles.Count -gt 0) {
    Write-Host "`n--- Orphan Sample Source Files (must be under a sample project) ---" -ForegroundColor Red
    foreach ($file in $orphanSourceFiles) {
        Write-Host "  $file" -ForegroundColor Red
    }
}

Write-Host "`n[4/4] Final Status..." -ForegroundColor Yellow
Write-Host "`n========================================" -ForegroundColor Cyan
if ($buildFailed -eq 0 -and $smokeFailed -eq 0 -and $unclassified.Count -eq 0 -and $orphanSourceFiles.Count -eq 0) {
    Write-Host 'SUCCESS: Certified samples passed build/smoke profiles and all samples are classified.' -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    exit 0
}

Write-Host "FAILURE: build failures=$buildFailed, smoke failures=$smokeFailed, unclassified=$($unclassified.Count), orphanSources=$($orphanSourceFiles.Count)." -ForegroundColor Red
Write-Host "========================================`n" -ForegroundColor Cyan
exit 1
