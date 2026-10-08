# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
#requires -Version 7.0
<#
.SYNOPSIS
  Require that every skip in a TRX DECLARED itself a capability exemption, and that something ran.
.DESCRIPTION
  A console summary reports "Skipped: 19" and nothing else, so it cannot tell a transport that
  structurally cannot filter apart from a test someone silenced. Both print the same number. This
  reads the TRX instead, where the skip REASON is recorded, and refuses any skip that does not
  declare itself.

  Accepted: a skip whose reason contains the literal marker [capability-not-applicable] -- the fact
  does not apply to this implementation, which is a property of the type system rather than a
  renewable decision.

  Refused: every other non-executed result. That deliberately includes [transport-unavailable],
  because absent infrastructure is the case where a green is least earned: the arms that would have
  detected the fault are the arms that did not run.

  The marker is matched LITERALLY and never inferred from prose. A gate that guesses intent from a
  message silently reclassifies a suppression the day somebody rewords it.

  SCOPE: this establishes that something executed, that nothing failed, and that every skip declared
  a category. It does NOT establish that the right tests ran, or that a declared exemption is
  truthful -- a suite could mark a fact inapplicable when it is merely inconvenient. Only a reader
  can judge that; this gate makes the claim visible instead of letting a bare count hide it.
.EXAMPLE
  pwsh -File eng/ci/assert-declared-skips-only.ps1 -TrxDir ./transport-conformance-results
#>
[CmdletBinding()]
param(
    [string]$TrxDir,
    [switch]$SelfTest
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MARKER = '[capability-not-applicable]'
$REFUSE = 3

function Invoke-Assertion {
    param([string]$Directory)

    if (-not (Test-Path -LiteralPath $Directory)) {
        Write-Host "REFUSE (declared-skips-only): results directory '$Directory' does not exist."
        return $REFUSE
    }
    $trx = @(Get-ChildItem -LiteralPath $Directory -Filter '*.trx' -Recurse -File)
    if ($trx.Count -eq 0) {
        # No TRX is not an empty run, it is an unmeasured one, and the two must not read alike.
        Write-Host "REFUSE (declared-skips-only): no .trx under '$Directory'. A run that left no evidence is unmeasured, not clean."
        return $REFUSE
    }

    $passed = 0; $failed = @(); $declared = @(); $undeclared = @()
    foreach ($file in $trx) {
        $xml = [xml](Get-Content -LiteralPath $file.FullName -Raw)
        # Dotted access ignores the TRX default namespace, matching how ci.yml reads the same shape.
        # @() is load-bearing: a single result would otherwise not enumerate.
        foreach ($r in @($xml.TestRun.Results.UnitTestResult)) {
            if (-not $r) { continue }
            # GetAttribute and SelectSingleNode are METHODS: they return '' and $null for something
            # absent, where dotted access THROWS under StrictMode. That matters here rather than being
            # style, because a skip carrying no ErrorInfo at all is exactly the undeclared case this
            # gate exists to catch -- reading it must not crash the gate that would have refused it.
            $name = $r.GetAttribute('testName')
            switch ($r.GetAttribute('outcome')) {
                'Passed' { $passed++ }
                'Failed' { $failed += $name }
                default {
                    $node = $r.SelectSingleNode('./*[local-name()="Output"]/*[local-name()="ErrorInfo"]/*[local-name()="Message"]')
                    $reason = if ($node) { "$($node.InnerText)" } else { '' }
                    if ($reason.Contains($MARKER)) { $declared += "$name  --  $reason" }
                    else { $undeclared += "$name  --  $(if ($reason) { $reason } else { '<no reason recorded>' })" }
                }
            }
        }
    }

    Write-Host "trx files        = $($trx.Count)"
    Write-Host "passed           = $passed"
    Write-Host "failed           = $($failed.Count)"
    Write-Host "declared skips   = $($declared.Count)   ($MARKER)"
    Write-Host "undeclared skips = $($undeclared.Count)"

    $refused = $false
    if ($passed -eq 0) {
        # The original defect on this path: a filter that matches nothing exits 0 and reports success.
        Write-Host 'REFUSE (declared-skips-only): zero tests executed. Exit 0 with nothing run is not a pass.'
        $refused = $true
    }
    if ($failed.Count -gt 0) {
        Write-Host 'REFUSE (declared-skips-only): failed tests present.'
        $failed | ForEach-Object { Write-Host "  FAILED      $_" }
        $refused = $true
    }
    if ($undeclared.Count -gt 0) {
        Write-Host "REFUSE (declared-skips-only): $($undeclared.Count) skip(s) declared no category. A skip that does not say why it did not run is indistinguishable from a silenced test."
        $undeclared | ForEach-Object { Write-Host "  UNDECLARED  $_" }
        $refused = $true
    }
    if ($refused) { return $REFUSE }

    # Printed in full, because the one moment anyone reads these is when the gate passes.
    if ($declared.Count -gt 0) {
        Write-Host "ACCEPTED -- every skip declared itself a capability exemption:"
        $declared | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host "PASS: $passed executed and passed; $($declared.Count) declared capability exemption(s); 0 undeclared."
    Write-Host 'NOT PROVEN by this gate: that the right tests ran, or that a declared exemption is truthful.'
    return 0
}

function Invoke-SelfTest {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("declared-skips-selftest-" + [guid]::NewGuid().ToString('N'))
    $script:arms = 0
    function New-Trx {
        param([string]$Dir, [object[]]$Results)
        [void][IO.Directory]::CreateDirectory($Dir)
        $rows = foreach ($r in $Results) {
            if ($null -eq $r.Reason) {
                "    <UnitTestResult testName=`"$($r.Name)`" outcome=`"$($r.Outcome)`" />"
            } else {
                "    <UnitTestResult testName=`"$($r.Name)`" outcome=`"$($r.Outcome)`"><Output><ErrorInfo><Message>$($r.Reason)</Message></ErrorInfo></Output></UnitTestResult>"
            }
        }
        @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
$($rows -join "`n")
  </Results>
</TestRun>
"@ | Set-Content -LiteralPath (Join-Path $Dir 'r.trx')
    }
    function Assert-Arm {
        param([string]$Name, [object[]]$Results, [bool]$ShouldPass)
        $dir = Join-Path $root ([guid]::NewGuid().ToString('N'))
        New-Trx -Dir $dir -Results $Results
        $code = Invoke-Assertion -Directory $dir 6> $null
        if (($code -eq 0) -ne $ShouldPass) {
            Write-Host "SELF-TEST FAIL -- ${Name}: exit $code, expected $(if ($ShouldPass) { 'PASS' } else { 'REFUSE' })"
            exit 1
        }
        $script:arms++
        Write-Host "SELF-TEST: PASS -- $Name"
    }
    try {
        Assert-Arm 'a declared capability skip alongside a pass is ACCEPTED' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null },
            @{ Name = 'B'; Outcome = 'NotExecuted'; Reason = "$MARKER this transport advertises no filtering" }) $true

        Assert-Arm 'an UNDECLARED skip is REFUSED' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null },
            @{ Name = 'B'; Outcome = 'NotExecuted'; Reason = 'temporarily disabled' }) $false

        # The arm that matters most: absent infrastructure must never be excused as a capability fact.
        Assert-Arm 'a [transport-unavailable] skip is REFUSED, not excused' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null },
            @{ Name = 'B'; Outcome = 'NotExecuted'; Reason = '[transport-unavailable] broker unreachable' }) $false

        Assert-Arm 'a skip with NO reason recorded is REFUSED' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null },
            @{ Name = 'B'; Outcome = 'NotExecuted'; Reason = $null }) $false

        Assert-Arm 'zero executed is REFUSED even when every skip is declared' @(
            @{ Name = 'B'; Outcome = 'NotExecuted'; Reason = "$MARKER not applicable" }) $false

        Assert-Arm 'a failed test is REFUSED' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null },
            @{ Name = 'B'; Outcome = 'Failed'; Reason = 'assertion failed' }) $false

        Assert-Arm 'an all-passing run with no skips is ACCEPTED' @(
            @{ Name = 'A'; Outcome = 'Passed'; Reason = $null }) $true

        # A results directory with no TRX must refuse rather than read as a clean empty run.
        $empty = Join-Path $root 'no-trx'
        [void][IO.Directory]::CreateDirectory($empty)
        if ((Invoke-Assertion -Directory $empty 6> $null) -eq 0) {
            Write-Host 'SELF-TEST FAIL -- a directory with no TRX did not REFUSE'; exit 1
        }
        $script:arms++
        Write-Host 'SELF-TEST: PASS -- a results directory with no .trx is REFUSED, not read as clean'

        if ((Invoke-Assertion -Directory (Join-Path $root 'absent') 6> $null) -eq 0) {
            Write-Host 'SELF-TEST FAIL -- an absent directory did not REFUSE'; exit 1
        }
        $script:arms++
        Write-Host 'SELF-TEST: PASS -- an absent results directory is REFUSED'

        Write-Host "SELF-TEST: $script:arms arms, 7 of which MUST refuse and did. The gate is non-vacuous."
        return 0
    } finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($SelfTest) { exit (Invoke-SelfTest) }
if (-not $TrxDir) { Write-Host 'REFUSE (declared-skips-only): -TrxDir is required.'; exit $REFUSE }
exit (Invoke-Assertion -Directory $TrxDir)
