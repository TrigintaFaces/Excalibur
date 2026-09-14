#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the full governance validation stack with consolidated reporting.
.DESCRIPTION
    Consolidates the governance checks used by CI and release workflows:
      - Solution and project graph validation
      - ShippingOnly filter completeness
      - Canonical repository links validation
      - Framework governance matrix validation
      - Shipping package metadata audit
#>
param(
    [string]$SolutionFilter = 'eng/ci/shards/ShippingOnly.slnf',
    [string]$MatrixPath = 'eng/governance/framework-governance.json',
    [string]$ReportsRoot = 'artifacts/reports',
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 20
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }

    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

$solutionReportDir = Join-Path $ReportsRoot 'SolutionGovernanceReport'
$frameworkReportDir = Join-Path $ReportsRoot 'FrameworkGovernanceReport'
New-Item -ItemType Directory -Force -Path $solutionReportDir | Out-Null
New-Item -ItemType Directory -Force -Path $frameworkReportDir | Out-Null

# ---------------------------------------------------------------------------------------------
# WHY THIS STACK RECORDS INSTEAD OF THROWING
#
# It used to be five sequential `if ($LASTEXITCODE -ne 0) { throw }` blocks, so the first failing
# gate ended the run and the output said NOTHING about the gates after it. `3/5 ... FAIL 1` was
# indistinguishable from "3 failed and two were never evaluated", while the `x/5` labels went on
# advertising a denominator the run had not assessed. Three real failures were reported as one,
# and the two hidden ones went unnoticed for as long as the stack kept running.
#
# That degrades in the WORST direction: an under-reporting instrument makes the problem look
# smaller every time it runs, so each green-ish run raises confidence while the hidden population
# is unchanged.
#
# The obligation is the same one a wired gate carries, one level up: a gate must be unable to
# report a PASS it did not earn, and a COMPOSITION must be unable to imply an EVALUATION it did
# not perform. So every gate ends in exactly one of three states and NOT-RUN is neither of the
# others:
#
#     PASS      ran, clean
#     FAIL      ran, found something
#     NOT RUN   never evaluated -- reported explicitly, never as silence
#
# Gates here are mutually independent, so a failure records and the stack continues; the run ends
# non-zero listing every failure, the way a compiler reports every error rather than the first.
# An early abort is legitimate ONLY where a later gate would be VACUOUS without an earlier one --
# vacuity, never cost. No such dependency has been demonstrated among these five, so none aborts.
# ---------------------------------------------------------------------------------------------
$gateOrder = @()
$gateVerdict = @{}
$gateDetail = @{}

function Set-GateVerdict {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][ValidateSet('PASS', 'FAIL', 'NOT RUN')][string]$Verdict,
        [string]$Detail = ''
    )
    if ($script:gateOrder -notcontains $Name) { $script:gateOrder += $Name }
    $script:gateVerdict[$Name] = $Verdict
    $script:gateDetail[$Name] = $Detail
    if ($Verdict -ne 'PASS') { Write-Host "  -> $Verdict $Name $Detail" }
}

# A GATE SIGNALS FAILURE IN TWO WAYS HERE, AND ONLY ONE OF THEM IS AN EXIT CODE.
#
# These sub-gates are PowerShell scripts invoked in-process, so a failing one may either set a
# non-zero $LASTEXITCODE *or* `throw`. Under `$ErrorActionPreference = 'Stop'` a throw propagates
# straight out of this stack -- which means removing this file's own `throw`s was NECESSARY BUT
# NOT SUFFICIENT: the run still died at the first failing gate, now without even printing the
# verdict table. That was caught by deliberately failing a middle gate and finding no table at
# all, which is precisely why that arm exists: it distinguishes a stack that was REPAIRED from
# one that was merely REARRANGED.
#
# So every gate runs inside this wrapper, which converts BOTH signals into a recorded verdict and
# lets the stack continue.
function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Body
    )
    $global:LASTEXITCODE = 0
    try {
        & $Body
        if ($LASTEXITCODE -ne 0) {
            Set-GateVerdict -Name $Name -Verdict 'FAIL' -Detail "(exit $LASTEXITCODE)"
        }
        else {
            Set-GateVerdict -Name $Name -Verdict 'PASS'
        }
    }
    catch {
        Set-GateVerdict -Name $Name -Verdict 'FAIL' -Detail "($($_.Exception.Message))"
    }
}

Write-Host '1/5 Validate solution graph and manifest'
Invoke-Gate -Name '1/5 solution graph and manifest' -Body {
    ./eng/validate-solution.ps1
}

Write-Host '2/5 Validate ShippingOnly filter parity'
Invoke-Gate -Name '2/5 ShippingOnly filter parity' -Body {
    ./eng/ci/validate-shipping-filter.ps1 `
        -SolutionFilter $SolutionFilter `
        -OutDir $solutionReportDir `
        -Enforce:$Enforce
}

Write-Host '3/5 Validate canonical repository links'
Invoke-Gate -Name '3/5 canonical repository links' -Body {
    ./eng/ci/validate-repository-links.ps1 `
        -OutDir $solutionReportDir `
        -Enforce:$Enforce
}

Write-Host '4/5 Validate framework governance matrix'
Invoke-Gate -Name '4/5 framework governance matrix' -Body {
    ./eng/ci/validate-framework-governance.ps1 `
        -Mode Governance `
        -MatrixPath $MatrixPath `
        -OutDir $frameworkReportDir `
        -Enforce:$Enforce
}

Write-Host '5/5 Audit shipping package metadata'
$metadataJsonPath = Join-Path $solutionReportDir 'package-metadata.json'
$metadataSummaryPath = Join-Path $solutionReportDir 'package-metadata-summary.md'
$metadataJson = & pwsh -NoProfile -File eng/audit-package-metadata.ps1 -OutputFormat Json
$metadataExitCode = $LASTEXITCODE
$metadataJson | Out-File -FilePath $metadataJsonPath -Encoding UTF8

# The report-building below parses a subprocess's JSON. Under `$ErrorActionPreference = 'Stop'` a
# malformed payload throws, which would abort the run before the verdict table prints -- turning a
# reporting failure into the same silence this stack was rewritten to remove. Guarded so gate 5
# records a verdict either way.
try {
    $metadata = ConvertFrom-JsonCompat -Json $metadataJson -Depth 20
    $summary = @(
        '# Package Metadata Audit',
        '',
        "- Total shipping projects: $($metadata.totalProjects)",
        "- Projects with issues: $($metadata.projectsWithIssues)",
        "- Projects complete: $($metadata.projectsComplete)",
        ''
    )

    if ($metadata.projectsWithIssues -gt 0) {
        $summary += '## Issue Summary'
        $summary += "- Missing <Description>: $($metadata.summary.missingDescription)"
        $summary += "- Missing <PackageTags>: $($metadata.summary.missingPackageTags)"
        $summary += "- Missing <PackageReadmeFile>: $($metadata.summary.missingPackageReadmeFile)"
        $summary += "- Missing README.md: $($metadata.summary.missingReadmeMd)"
        $summary += ''
    }
    else {
        $summary += '## Result'
        $summary += 'All shipping projects contain required package metadata.'
        $summary += ''
    }

    $summary | Out-File -FilePath $metadataSummaryPath -Encoding UTF8
    Write-Host "Wrote summary: $metadataSummaryPath"
    Write-Host "Wrote report: $metadataJsonPath"

    Set-GateVerdict -Name '5/5 shipping package metadata' `
        -Verdict $(if ($Enforce -and $metadataExitCode -ne 0) { 'FAIL' } else { 'PASS' }) `
        -Detail $(if ($Enforce -and $metadataExitCode -ne 0) { "(audit-package-metadata.ps1 exit $metadataExitCode)" } else { '' })
}
catch {
    Set-GateVerdict -Name '5/5 shipping package metadata' -Verdict 'FAIL' `
        -Detail "(report generation failed: $($_.Exception.Message))"
}

# ---------------------------------------------------------------------------------------------
# The verdict table. Every gate appears with exactly one state, so a reader can tell a clean run
# from a truncated one WITHOUT counting lines -- and `evaluated N of M` states the denominator the
# run actually assessed rather than the one the labels advertise.
# ---------------------------------------------------------------------------------------------
# DERIVED from the roster, never a literal. A hardcoded denominator drifts the moment a gate is
# added and then reports the old total with total confidence -- the same defect as the labels it
# is here to make honest.
$expectedGates = @(
    '1/5 solution graph and manifest',
    '2/5 ShippingOnly filter parity',
    '3/5 canonical repository links',
    '4/5 framework governance matrix',
    '5/5 shipping package metadata'
)
$expected = $expectedGates.Count
foreach ($n in $expectedGates) {
    if (-not $gateVerdict.ContainsKey($n)) {
        Set-GateVerdict -Name $n -Verdict 'NOT RUN' -Detail '(the stack ended before this gate)'
    }
}

$failed = @($gateOrder | Where-Object { $gateVerdict[$_] -eq 'FAIL' })
$notRun = @($gateOrder | Where-Object { $gateVerdict[$_] -eq 'NOT RUN' })
$evaluated = $gateOrder.Count - $notRun.Count

Write-Host ''
Write-Host '==== governance stack ===='
foreach ($n in $gateOrder) {
    Write-Host ("  {0,-7} {1} {2}" -f $gateVerdict[$n], $n, $gateDetail[$n])
}
Write-Host ("evaluated {0} of {1} | {2} failed | {3} not run" -f $evaluated, $expected, $failed.Count, $notRun.Count)

if ($evaluated -eq 0) {
    Write-Error 'governance stack: 0 gates evaluated -- nothing was checked. This is NOT a pass.'
    exit 2
}
if ($failed.Count -gt 0 -or $notRun.Count -gt 0) {
    Write-Error ("governance stack: {0} failed, {1} not run." -f $failed.Count, $notRun.Count)
    exit 1
}

Write-Host 'Governance stack validation passed.'
