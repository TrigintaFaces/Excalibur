<#
.SYNOPSIS
    Validates AOT (Ahead-of-Time) build compatibility by publishing the AOT sample app.

.DESCRIPTION
    Runs `dotnet publish` with PublishAot=true on the AOT sample application
    (samples/10-aot/), which transitively references shipping packages.
    Captures IL2xxx (trim) and IL3xxx (AOT) warnings, groups them by package,
    and produces JSON and HTML reports for CI consumption.

    Exit codes:
      0 = No warnings or errors
      1 = IL2xxx/IL3xxx warnings detected
      2 = Script or publish error
      3 = REFUSE: the publish produced no analysable output, so warning absence
          could not be distinguished from parse failure. REFUSE is not PASS.

.PARAMETER SelfTest
    Runs the verdict-logic self-test and exits without publishing.

.PARAMETER ProjectPath
    Path to the .csproj to publish. Defaults to the AOT sample app. Pass the generated coverage
    harness from Invoke-AotCoverageGate.ps1 to validate the full IsAotCompatible=true declaring
    set instead of the hand-maintained sample's reference list.

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
    [string]$BaselinePath = '',
    [string]$ProjectPath = '',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The verdict is derived HERE, from the publish exit code first. Parsed warnings and errors refine
# the message; they never override a failed publish, and an unparseable log never reads as clean.
# Kept as a function so it can be exercised directly by -SelfTest without running a real publish.
function Get-AotVerdict {
    param(
        [int]$publishExitCode,
        [int]$ErrorCount,
        [int]$WarningCount,
        [string]$PublishOutput
    )

    # SAFETY: a non-zero publish can never produce exit 0. A publish that failed emits no IL
    # diagnostics because it never got far enough to emit any, so "zero warnings found" is
    # trivially true and means nothing.
    if ($ErrorCount -gt 0 -or $publishExitCode -ne 0) {
        return @{ Code = 2; Verdict = 'ERROR'; Reason = "publish exit code: $publishExitCode; parsed errors: $ErrorCount" }
    }

    # REFUSE: exit 0 with a log we could not analyse is indeterminate, not clean. A changed SDK
    # message format, a localized toolchain, or a truncated log all land here. REFUSE is not PASS.
    if (-not (Test-AotOutputAnalysable -PublishOutput $PublishOutput)) {
        return @{ Code = 3; Verdict = 'REFUSE'; Reason = 'publish exited 0 but produced no analysable build output; warning absence is undetermined' }
    }

    if ($WarningCount -gt 0) {
        return @{ Code = 1; Verdict = 'WARNINGS'; Reason = "$WarningCount IL2xxx/IL3xxx warnings found" }
    }

    return @{ Code = 0; Verdict = 'PASSED'; Reason = 'zero warnings' }
}

# An analysable log is one that carries at least one line the MSBuild/publish pipeline is known to
# emit. Absence of ALL of them means the parser had nothing to work with -- not that the build was
# clean.
function Test-AotOutputAnalysable {
    param([string]$PublishOutput)

    if ([string]::IsNullOrWhiteSpace($PublishOutput)) { return $false }

    foreach ($line in ($PublishOutput -split "`n")) {
        if ($line -match 'Build succeeded|Determining projects to restore|\bwarning\s+[A-Z]+\d+|\berror\s+[A-Z]+\d+|->\s+\S+') {
            return $true
        }
    }

    return $false
}

if ($SelfTest) {
    # Each case names the arm it guards. The first is RED against the pre-fix script, which exited 0
    # for a publish that had failed with exit 1.
    $cases = @(
        @{ Name = 'SAFETY: failed publish (exit 1), nothing parsed, must NOT pass'; Publish = 1; Errors = 0; Warnings = 0; Output = ''; Expect = 2 }
        @{ Name = 'SAFETY: failed publish (exit 1) with an analysable log still must NOT pass'; Publish = 1; Errors = 0; Warnings = 0; Output = 'Build succeeded.'; Expect = 2 }
        @{ Name = 'SAFETY: parsed build errors on a zero exit'; Publish = 0; Errors = 3; Warnings = 0; Output = 'Build succeeded.'; Expect = 2 }
        @{ Name = 'LIVENESS: clean analysable publish still PASSES with exit 0'; Publish = 0; Errors = 0; Warnings = 0; Output = "  Determining projects to restore...`n  Build succeeded.`n  app -> /out/app"; Expect = 0 }
        @{ Name = 'LIVENESS: warnings on a successful publish still report exit 1'; Publish = 0; Errors = 0; Warnings = 4; Output = 'Build succeeded.'; Expect = 1 }
        @{ Name = 'REFUSE: exit 0 with an empty log is undetermined, not clean'; Publish = 0; Errors = 0; Warnings = 0; Output = ''; Expect = 3 }
        @{ Name = 'REFUSE: exit 0 with an unrecognised log format is undetermined'; Publish = 0; Errors = 0; Warnings = 0; Output = "kompilointi onnistui`nvalmis"; Expect = 3 }
    )

    $failed = 0
    foreach ($case in $cases) {
        $verdict = Get-AotVerdict -publishExitCode $case.Publish -ErrorCount $case.Errors -WarningCount $case.Warnings -PublishOutput $case.Output
        if ($verdict.Code -eq $case.Expect) {
            Write-Host "  PASS  $($case.Name) -> exit $($verdict.Code) ($($verdict.Verdict))"
        }
        else {
            Write-Host "  FAIL  $($case.Name): expected exit $($case.Expect), got $($verdict.Code) ($($verdict.Verdict))" -ForegroundColor Red
            $failed++
        }
    }

    if ($failed -gt 0) {
        Write-Host "Self-test FAILED: $failed of $($cases.Count) cases." -ForegroundColor Red
        exit 1
    }

    Write-Host "Self-test passed: $($cases.Count) of $($cases.Count) cases." -ForegroundColor Green
    exit 0
}

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
$aotSampleProject = if ($ProjectPath) { $ProjectPath } else { Join-Path $repoRoot 'samples' '10-aot' 'Excalibur.Dispatch.Aot.Sample' 'Excalibur.Dispatch.Aot.Sample.csproj' }

if (-not (Test-Path $aotSampleProject)) {
    Write-Log "AOT sample project not found at: $aotSampleProject" 'ERROR'
    Write-Host "ERROR: AOT sample project not found at: $aotSampleProject" -ForegroundColor Red
    exit 2
}

Write-Log "AOT sample project: $aotSampleProject"

# On Windows, native AOT publish invokes the MSVC linker (link.exe), which MSBuild locates
# via vswhere.exe. In dev/CI shells vswhere is frequently NOT on PATH (it lives under the
# VS Installer dir), causing native link.exe to fail (exit 123, "vswhere.exe is not recognized").
# Self-locate vswhere at its known install path and prepend that dir to PATH for this process
# so the gate is robust without requiring the caller to pre-configure PATH.
function Initialize-VsWhereOnPath {
    # Only relevant on Windows (native AOT uses the MSVC toolchain there).
    # $IsWindows is a PS Core automatic variable; on Windows PowerShell 5.1 it is undefined,
    # so resolve it defensively under Set-StrictMode (5.1 is always Windows).
    $isWin = (Get-Variable -Name 'IsWindows' -ValueOnly -ErrorAction SilentlyContinue)
    if ($null -eq $isWin) { $isWin = ($env:OS -eq 'Windows_NT') }
    if (-not $isWin) {
        return
    }

    # Already discoverable — nothing to do.
    if (Get-Command 'vswhere' -ErrorAction SilentlyContinue) {
        Write-Log "vswhere already on PATH"
        return
    }

    $candidateDirs = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'),
        (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer')
    ) | Where-Object { $_ }

    foreach ($dir in $candidateDirs) {
        $vswherePath = Join-Path $dir 'vswhere.exe'
        if (Test-Path $vswherePath) {
            $env:PATH = "$dir$([System.IO.Path]::PathSeparator)$env:PATH"
            Write-Log "vswhere self-located at $vswherePath; prepended '$dir' to PATH"
            return
        }
    }

    Write-Log "vswhere.exe not found on PATH or at known VS Installer paths; native AOT link may fail" 'WARN'
}

Initialize-VsWhereOnPath

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

        # ATTRIBUTE BY THE WARNING'S SUBJECT, NEVER BY THE RAW LINE.
        #
        # The previous version matched 'Excalibur.Dispatch.([\w.]+)' against $line. Every MSBuild
        # warning line ENDS with the building project's own file in brackets --
        #   ... [D:\Excalibur.Dispatch\eng\ci\.aot-coverage-harness\Excalibur.AotCoverageHarness.csproj]
        # -- and that path always contains the string 'Excalibur.Dispatch'. So any warning with no
        # Excalibur-owned caller earlier in the line fell through to the TRAILING PATH and was
        # attributed to Excalibur.Dispatch.
        #
        # It mislabelled 1,994 warnings that way. Every one of them names a third-party caller
        # (Oracle, Azure, MongoDB, Dapper), and Excalibur.Dispatch.csproj has no PackageReference to
        # any of those packages -- it cannot own them. A report that says otherwise sends someone to
        # remediate a package that has nothing to remediate.
        #
        # $message is the text AFTER the IL code, so the trailing project bracket is still in it.
        # Strip that first, then read the subject: the token immediately after 'IL####: ' is the type
        # whose reflection ILC could not prove safe. That is the owner.
        $subjectText = $message -replace '\s*\[[^\]]*\.(cs|vb|fs)proj\]\s*$', ''
        $packageName = 'Unknown'
        if ($subjectText -match '^\s*([A-Za-z][\w]*(?:\.[A-Za-z][\w]*)+)') {
            $subject = $matches[1]
            if ($subject -like 'Excalibur.*') {
                # Own code: keep the assembly-level prefix, not the full type name.
                $parts = $subject -split '\.'
                $packageName = if ($parts.Count -ge 3) { "$($parts[0]).$($parts[1]).$($parts[2])" }
                               elseif ($parts.Count -eq 2) { "$($parts[0]).$($parts[1])" }
                               else { $subject }
            }
            else {
                # Third-party: attribute to the dependency that owns it, so a reader can see at a
                # glance that this is not ours to fix by editing our code.
                $parts = $subject -split '\.'
                $packageName = if ($parts.Count -ge 2) { "$($parts[0]).$($parts[1]) (third-party)" }
                               else { "$subject (third-party)" }
            }
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
    <p><strong>Sample Project:</strong> samples/10-aot/Excalibur.Dispatch.Aot.Sample</p>

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

# Determine exit code.
#
# `-ne 0`, NOT `-gt 1`. A failed `dotnet publish` exits **1** — that is the ordinary MSBuild failure
# code, not a warning code — so `-gt 1` let every real publish failure through this guard. The rest of
# the script then found zero IL warnings (a publish that FAILED emits none, because it never got far
# enough to emit any) and printed "AOT validation PASSED - zero warnings", exit 0.
#
# That is a safety property satisfied by inaction: "no AOT warnings were found" is trivially true when
# nothing was analysed. The summary block above printed "Publish: FAILED (exit 1)" one screen earlier
# and the verdict discarded it. A gate must not be able to report a PASS it did not earn.
$verdict = Get-AotVerdict `
    -publishExitCode $publishExitCode `
    -ErrorCount $errors.Count `
    -WarningCount $allWarnings.Count `
    -PublishOutput $publishOutput

$verdictColour = switch ($verdict.Code) {
    0 { 'Green' }
    1 { 'Yellow' }
    default { 'Red' }
}

Write-Host "AOT validation $($verdict.Verdict) - $($verdict.Reason)." -ForegroundColor $verdictColour
Write-Log "Verdict: $($verdict.Verdict) (exit $($verdict.Code)) - $($verdict.Reason)"
exit $verdict.Code
