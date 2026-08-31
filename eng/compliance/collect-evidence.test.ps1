<#
.SYNOPSIS
    Regression lock for eng/compliance/collect-evidence.ps1.

.DESCRIPTION
    THE DEFECT THIS LOCKS. Every compliance figure in the evidence MANIFEST.json used to be a literal
    inside the document template: 14 of 14 FedRAMP controls documented, 80 GDPR conformance tests, 17
    SOC 2 controls, 12 HIPAA technical controls, and a README line reading "Controls Covered: 14/14
    (100% complete)". No input could make any of them print anything else, so a run that downloaded no
    artifacts at all still asserted complete control documentation. The audience for that document is
    an auditor, and this script is published, so a consumer could generate it against their own
    repository and be handed our numbers as if they were theirs.

    The arms are a safety/liveness pair on purpose. Reporting zero for everything would satisfy both
    safety arms and be useless, so C and E fail any version that has learned only to say nothing.

      A  zero collected evidence          -> every framework reports ControlsDocumented 0   (SAFETY)
      B  a missing evidence directory     -> REFUSE (rc 2), count null, never 0             (SAFETY)
      C  genuine collected evidence       -> accurate NON-ZERO figures                      (LIVENESS)
      D  the pre-fix hardcoded manifest   -> arms A and B REJECT it                         (NON-VACUITY)
      E  a smaller control map            -> ControlsInScope follows the map, not a constant
      F  a framework absent from the map  -> REFUSE, never "0 of 0 controls"
      G  a placeholder template alone     -> documents nothing (a blank form is not evidence)

    Arm D is what keeps A and B honest. It replays the exact hashtable this script was written to
    remove and asserts the safety assertions fail against it. Without D, A and B could be weakened to
    nothing and still pass, which is the failure mode they exist to prevent.

    Hermetic: no network, no gh, no GitHub token, no workflow run. The collector is dot-sourced and its
    manifest generator called directly against fixture directories.

.EXAMPLE
    pwsh -File eng/compliance/collect-evidence.test.ps1
    Exit 0 = all green; non-zero = a lock failed.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$ScriptDir = $PSScriptRoot
$Collector = if ($env:EVIDENCE_COLLECTOR_PS) { $env:EVIDENCE_COLLECTOR_PS } else { Join-Path $ScriptDir 'collect-evidence.ps1' }

if (-not (Test-Path -LiteralPath $Collector -PathType Leaf)) {
    Write-Error "FATAL: collector not found at $Collector"
    exit 3
}

$script:Failures = 0
function Pass { param([string]$m) Write-Host "  [PASS] $m" }
function Fail { param([string]$m) Write-Host "  [FAIL] $m"; $script:Failures++ }

$Work = Join-Path ([System.IO.Path]::GetTempPath()) ("collectevidencetest." + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Work -Force | Out-Null

Write-Host "collect-evidence.test.ps1 - locking $Collector"

# Keep every arm off the network regardless of whether gh is installed and authenticated here.
$env:EVIDENCE_REPOSITORY = 'fixture/repo'

# Dot-source the collector's function definitions WITHOUT running its main block. The main block
# downloads artifacts and needs gh; the manifest generator is what is under test.
$collectorText = Get-Content -LiteralPath $Collector -Raw
$mainIndex = $collectorText.IndexOf('# Main execution')
if ($mainIndex -lt 0) {
    Write-Error "FATAL: could not find the main-execution boundary in $Collector"
    exit 3
}
$definitionsOnly = $collectorText.Substring(0, $mainIndex)
$defsPath = Join-Path $Work 'collector-definitions.ps1'
Set-Content -LiteralPath $defsPath -Value $definitionsOnly -Encoding UTF8

# $Frameworks and $PSScriptRoot are read by the definitions; supply them as the real script would.
$Frameworks = @('All')
$PSScriptRootOverride = $ScriptDir
$env:CONTROL_EVIDENCE_MAP = Join-Path $ScriptDir 'control-evidence-map.tsv'
. $defsPath

# --- helpers ----------------------------------------------------------------------------------------

# A package root with the named categories present and empty. With no categories, all five.
function New-FixturePackage {
    param([string]$Name, [string[]]$Categories)
    $root = Join-Path $Work $Name
    if (-not $Categories -or $Categories.Count -eq 0) {
        $Categories = @('test-results', 'security-scans', 'sbom', 'audit-logs', 'rtm')
    }
    foreach ($c in $Categories) { New-Item -ItemType Directory -Path (Join-Path $root $c) -Force | Out-Null }
    return $root
}

# Runs the manifest generator and returns its REAL return value. The value is captured on the very
# next statement: a pipeline or a trailing Write-Host would report its own status and mask the refusal
# this test exists to observe.
function Invoke-Generate {
    param([string]$Root)
    $rc = New-EvidenceManifest -OutputPath $Root -RunId 'fixture-run' 3>$null 4>$null
    return [int]($rc | Select-Object -Last 1)
}

function Read-Manifest {
    param([string]$Root)
    $path = Join-Path $Root 'MANIFEST.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail "no MANIFEST.json was written to $Root - the generator did not run"
        return $null
    }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

# The reusable SAFETY assertions. Arms A/B call them on the real generator; arm D calls the same
# functions on the pre-fix manifest to prove they can fail.
function Assert-NoDocumentedControls {
    param($Manifest)
    # An absent manifest is not a manifest with no documented controls.
    if ($null -eq $Manifest -or $null -eq $Manifest.ControlCoverage) { return $false }
    foreach ($p in $Manifest.ControlCoverage.PSObject.Properties) {
        if ($p.Name -in @('Source', 'Basis')) { continue }
        $documented = $p.Value.PSObject.Properties['ControlsDocumented']
        if ($null -ne $documented -and [int]$documented.Value -ne 0) { return $false }
    }
    return $true
}

function Assert-AbsentDirNotReportedZero {
    param($Manifest, [string]$Category)
    if ($null -eq $Manifest -or $null -eq $Manifest.EvidenceCounts) { return $false }
    return ($null -eq $Manifest.EvidenceCounts.PSObject.Properties[$Category].Value)
}

# --- A. zero collected evidence -> ControlsDocumented 0 everywhere (SAFETY) --------------------------
$pkgA = New-FixturePackage -Name 'a'
$rcA = Invoke-Generate -Root $pkgA
$manA = Read-Manifest -Root $pkgA
if ($rcA -eq 0 -and (Assert-NoDocumentedControls -Manifest $manA)) {
    Pass 'A: an empty package documents 0 controls (all frameworks)'
} else {
    Fail "A: empty package claimed documented controls, or did not complete (rc=$rcA)"
}

# The stale fabricated figures must be gone entirely, not merely recomputed to something else.
$rawAPath = Join-Path $pkgA 'MANIFEST.json'
$rawA = if (Test-Path -LiteralPath $rawAPath -PathType Leaf) { Get-Content -LiteralPath $rawAPath -Raw } else { $null }
if ($null -eq $rawA) {
    Fail 'A2: no MANIFEST.json to inspect'
} elseif ($rawA -match '"(ConformanceTests|TechnicalControls)"') {
    Fail 'A2: the fabricated ConformanceTests/TechnicalControls figures are still emitted'
} else {
    Pass 'A2: the fabricated ConformanceTests/TechnicalControls figures are gone'
}

# --- B. a missing evidence directory -> REFUSE, count null, never 0 (SAFETY) -------------------------
$pkgB = New-FixturePackage -Name 'b' -Categories @('test-results', 'security-scans', 'sbom', 'rtm')
$rcB = Invoke-Generate -Root $pkgB
$manB = Read-Manifest -Root $pkgB
if ($rcB -eq 2 -and (Assert-AbsentDirNotReportedZero -Manifest $manB -Category 'audit-logs')) {
    Pass 'B: an absent category refuses (rc 2) and reports null, not 0'
} else {
    Fail "B: absent category did not refuse or was reported as 0 (rc=$rcB)"
}

# --- C. genuine collected evidence -> accurate NON-ZERO figures (LIVENESS) ---------------------------
$pkgC = New-FixturePackage -Name 'c'
foreach ($c in @('test-results', 'security-scans', 'sbom', 'audit-logs', 'rtm')) {
    Set-Content -LiteralPath (Join-Path $pkgC "$c/evidence.json") -Value '{}' -Encoding UTF8
}
$rcC = Invoke-Generate -Root $pkgC
$manC = Read-Manifest -Root $pkgC
$documentedC = 0
foreach ($p in $manC.ControlCoverage.PSObject.Properties) {
    if ($p.Name -in @('Source', 'Basis')) { continue }
    $d = $p.Value.PSObject.Properties['ControlsDocumented']
    if ($null -ne $d) { $documentedC += [int]$d.Value }
}
if ($rcC -eq 0 -and $documentedC -gt 0 -and [int]$manC.EvidenceCounts.'test-results' -eq 1) {
    Pass "C: a package with real evidence documents $documentedC controls and counts 1 test-results file"
} else {
    Fail "C: a package with real evidence reported nothing (rc=$rcC, documented=$documentedC)"
}

# --- D. the pre-fix hardcoded manifest -> arms A and B REJECT it (NON-VACUITY) -----------------------
# The exact shape this rewrite removed. If the safety assertions above were weakened to nothing, they
# would accept this document, so this arm is what keeps them load-bearing.
$prefix = [pscustomobject]@{
    EvidenceCounts  = [pscustomobject]@{ 'test-results' = 0; 'security-scans' = 0; 'sbom' = 0; 'audit-logs' = 0; 'rtm' = 0 }
    # audit-logs is 0 above, standing in for a directory that was never looked at - the pre-fix shape.
    ControlCoverage = [pscustomobject]@{
        Source  = 'hardcoded'
        Basis   = 'hardcoded'
        FedRAMP = [pscustomobject]@{ ControlsInScope = 14; ControlsDocumented = 14 }
    }
}
if ((Assert-NoDocumentedControls -Manifest $prefix)) {
    Fail 'D: the safety assertion ACCEPTS the pre-fix hardcoded manifest - arms A/B prove nothing'
} else {
    Pass 'D: the safety assertion rejects the pre-fix hardcoded manifest (arms A/B are non-vacuous)'
}
if ((Assert-AbsentDirNotReportedZero -Manifest $prefix -Category 'audit-logs')) {
    Fail 'D2: the null-vs-zero assertion ACCEPTS a 0 for an unmeasured category'
} else {
    Pass 'D2: the null-vs-zero assertion rejects a 0 for an unmeasured category'
}

# --- E. a smaller control map -> ControlsInScope follows the map, not a constant ---------------------
$smallMap = Join-Path $Work 'small-map.tsv'
Set-Content -LiteralPath $smallMap -Encoding UTF8 -Value @(
    '# fixture map',
    "FedRAMP`tSA-15`ttest-results",
    "FedRAMP`tCM-8`tsbom"
)
$env:CONTROL_EVIDENCE_MAP = $smallMap
$script:ControlMap = $smallMap
$Frameworks = @('All')
$pkgE = New-FixturePackage -Name 'e'
$rcE = Invoke-Generate -Root $pkgE
$manE = Read-Manifest -Root $pkgE
if ([int]$manE.ControlCoverage.FedRAMP.ControlsInScope -eq 2) {
    Pass 'E: ControlsInScope follows the control map (2), not a baked-in constant'
} else {
    Fail "E: ControlsInScope did not follow the map (got $($manE.ControlCoverage.FedRAMP.ControlsInScope), expected 2)"
}

# --- F. a framework absent from the map -> REFUSE, never "0 of 0 controls" ---------------------------
$Frameworks = @('HIPAA')
$pkgF = New-FixturePackage -Name 'f'
$rcF = Invoke-Generate -Root $pkgF
$manF = Read-Manifest -Root $pkgF
if ($rcF -eq 2 -and $manF.ControlCoverage.HIPAA.Status -eq 'REFUSED') {
    Pass 'F: a framework absent from the map refuses rather than reporting 0 of 0'
} else {
    Fail "F: an unmapped framework did not refuse (rc=$rcF)"
}

# --- G. a placeholder template alone -> documents nothing -------------------------------------------
$env:CONTROL_EVIDENCE_MAP = Join-Path $ScriptDir 'control-evidence-map.tsv'
$script:ControlMap = Join-Path $ScriptDir 'control-evidence-map.tsv'
$Frameworks = @('All')
$pkgG = New-FixturePackage -Name 'g'
foreach ($c in @('test-results', 'security-scans', 'sbom', 'audit-logs', 'rtm')) {
    Set-Content -LiteralPath (Join-Path $pkgG "$c/blank.template.json") -Value '{}' -Encoding UTF8
}
$rcG = Invoke-Generate -Root $pkgG
$manG = Read-Manifest -Root $pkgG
if ($rcG -eq 0 -and (Assert-NoDocumentedControls -Manifest $manG) -and [int]$manG.EvidenceCounts.'test-results' -eq 0) {
    Pass 'G: a blank *.template.json form is not evidence and documents nothing'
} else {
    Fail "G: a placeholder template was counted as evidence (rc=$rcG, test-results=$($manG.EvidenceCounts.'test-results'))"
}

# --- result -----------------------------------------------------------------------------------------
Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue

if ($script:Failures -eq 0) {
    Write-Host "`nAll arms green."
    exit 0
}
Write-Host "`n$($script:Failures) arm(s) failed."
exit 1
