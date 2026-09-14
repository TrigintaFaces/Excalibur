#!/usr/bin/env pwsh
# E2E Capability Gate
#
# Fails when an advertised capability (eng/governance/e2e-capability-manifest.yaml) has no end-to-end
# arm, AND fails when its arm SKIPS or FAILS -- a gate that treats "skipped" or "absent" as passing is
# the same defect this whole manifest exists to catch, one level up. See ConformanceLivenessGate.cs for
# the same principle already applied to transport conformance ("skips are not failures, so a run in
# which nothing was verified must not report the same result as a run that verified everything").
#
# Usage: pwsh eng/ci/e2e-capability-gate.ps1 [-Manifest <path>]
#
# EXIT  0 PASS    every advertised capability has an arm that ran and passed
#       1 FAIL    at least one capability has no arm, or its arm failed, skipped, or matched nothing
#       2 REFUSE  nothing was checked, or an arm could not be measured (build break / runner never
#                 started). A REFUSE is NOT a pass and NOT a statement about the capability -- it says
#                 this run does not know. Never map it onto either verdict.
# [CmdletBinding()] is load-bearing, not decoration. Without it, `pwsh -File script.ps1 -Typo`
# SILENTLY DISCARDS the unrecognised switch and runs as if called with no arguments -- so a
# workflow step named for the self-test can run the REAL gate and report a pass that proves
# nothing about non-vacuity. With it, an unknown parameter is a hard error.
[CmdletBinding()]
param(
    [string]$Manifest = "eng/governance/e2e-capability-manifest.yaml",
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------------------------------
# Non-vacuity self-test.
#
# WHY THIS EXISTS, AND WHY NOW. This gate's ability to fail is currently demonstrated by its own red:
# one advertised capability has no arm, so every CI run watches the gate report a real failure. That
# proof EXPIRES the moment the last arm lands -- from then on a green gate and a gate that cannot fail
# are indistinguishable from the outside, which is the exact defect the manifest exists to catch, one
# level up. This block replaces the expiring proof with a permanent one.
#
# It plants violations rather than deleting machinery: each fixture leaves the gate's code untouched and
# changes only its INPUT, so a green arm here means the gate read a real manifest and reached a real
# verdict -- not that some file was missing.
#
# All three verdicts are covered on purpose. A self-test that only proves FAIL is reachable would be
# satisfied by a gate that fails on everything, and REFUSE collapsing onto either neighbour is the
# specific conflation the main body is written to prevent.
# ---------------------------------------------------------------------------------------------------
if ($SelfTest) {
    $fixtureDir = Join-Path ([System.IO.Path]::GetTempPath()) "e2e-gate-selftest-$([guid]::NewGuid())"
    $null = New-Item -ItemType Directory -Path $fixtureDir

    # A real test that needs no container and no infrastructure, so the PASS arm stays cheap. If this is
    # ever renamed, this arm fails loudly rather than silently degrading -- which is the correct outcome:
    # a self-test whose own fixture has rotted is not evidence of anything.
    $liveProject = 'tests/conformance/Excalibur.Dispatch.Tests.Conformance/Excalibur.Dispatch.Tests.Conformance.csproj'
    $liveTest = 'Excalibur.Dispatch.Tests.Conformance.Providers.CrossTransport.CloudEventsWiringConformanceShould.DetectorFindsAGenuineBridgeConstructorParameter'

    $cases = @(
        @{
            name = 'FAIL on an advertised capability with no arm'
            expected = 1
            body = "version: 1`ncapabilities:`n  - name: planted-unarmed-capability`n    advertised_in: `"self-test fixture`"`n    project: `"$liveProject`"`n    e2e_test: `"`"`n"
        },
        @{
            name = 'FAIL when the declared arm matches no test (renamed or deleted)'
            expected = 1
            body = "version: 1`ncapabilities:`n  - name: planted-missing-arm`n    advertised_in: `"self-test fixture`"`n    project: `"$liveProject`"`n    e2e_test: `"Excalibur.NoSuchNamespace.NoSuchClass.NoSuchTest`"`n"
        },
        @{
            name = 'PASS when every capability has an arm that ran and passed'
            expected = 0
            body = "version: 1`ncapabilities:`n  - name: planted-armed-capability`n    advertised_in: `"self-test fixture`"`n    project: `"$liveProject`"`n    e2e_test: `"$liveTest`"`n"
        },
        @{
            name = 'REFUSE when the manifest parses to no capabilities at all'
            expected = 2
            body = "version: 1`ncapabilities: []`n"
        }
    )

    $selfTestFailures = @()
    foreach ($case in $cases) {
        $fixture = Join-Path $fixtureDir "$([guid]::NewGuid()).yaml"
        Set-Content -LiteralPath $fixture -Value $case.body -NoNewline

        $null = & $PSCommandPath -Manifest $fixture 2>&1
        $actual = $LASTEXITCODE

        if ($actual -eq $case.expected) {
            Write-Host "SELF-TEST PASS  exit $actual  $($case.name)"
        }
        else {
            Write-Host "SELF-TEST FAIL  expected exit $($case.expected), got $actual  --  $($case.name)"
            $selfTestFailures += $case.name
        }
    }

    Remove-Item -LiteralPath $fixtureDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    if ($selfTestFailures.Count -gt 0) {
        Write-Host "E2E CAPABILITY GATE SELF-TEST: FAILED -- $($selfTestFailures.Count) of $($cases.Count) planted cases did not produce the expected verdict:"
        foreach ($f in $selfTestFailures) { Write-Host "  - $f" }
        Write-Host "The gate cannot be trusted to report on the real manifest until this is green."
        exit 1
    }

    Write-Host "E2E CAPABILITY GATE SELF-TEST: PASSED -- all $($cases.Count) planted cases produced the expected verdict (FAIL, FAIL, PASS, REFUSE)."
    exit 0
}


# Quick YAML parse (same idiom as eng/ci/transitive-bloat-report.ps1's package-map read): a flat list of
# `- name:` records with three follow-on scalar fields. No YAML library needed for this shape.
$capabilities = @()
$current = $null
foreach ($line in Get-Content $Manifest) {
    if ($line -match '^\s*-\s*name:\s*(.+?)\s*$') {
        if ($current) { $capabilities += [pscustomobject]$current }
        $current = @{ name = $Matches[1]; advertised_in = ''; project = ''; e2e_test = '' }
    }
    elseif ($current -and $line -match '^\s*advertised_in:\s*"?(.*?)"?\s*$') { $current.advertised_in = $Matches[1] }
    elseif ($current -and $line -match '^\s*project:\s*"?(.*?)"?\s*$') { $current.project = $Matches[1] }
    elseif ($current -and $line -match '^\s*e2e_test:\s*"?(.*?)"?\s*$') { $current.e2e_test = $Matches[1] }
}
if ($current) { $capabilities += [pscustomobject]$current }

if ($capabilities.Count -eq 0) {
    # REFUSE, not FAIL: nothing was checked. A manifest that parses to nothing cannot report that every
    # capability is armed, and must not be reachable from the same exit code as a real verdict.
    # Write-Host, not Write-Error: $ErrorActionPreference is 'Stop', so Write-Error terminates the script
    # with exit 1 and the REFUSE code below is never reached -- which would collapse REFUSE back onto FAIL,
    # the exact conflation this branch exists to remove.
    Write-Host "E2E CAPABILITY GATE: REFUSED -- no capabilities parsed from '$Manifest'. Check the manifest exists and is well-formed. Nothing was checked."
    exit 2
}

$failures = @()
$refusals = @()
foreach ($cap in $capabilities) {
    if ([string]::IsNullOrWhiteSpace($cap.e2e_test)) {
        Write-Host "FAIL  $($cap.name) -- no e2e_test declared. Advertised: $($cap.advertised_in)"
        $failures += $cap.name
        continue
    }

    Write-Host "RUN   $($cap.name) -> $($cap.e2e_test)"
    $output = & dotnet test $cap.project --nologo -p:BuildExamplesAndTests=true `
        --filter "FullyQualifiedName=$($cap.e2e_test)" 2>&1 | Out-String

    # "The arm did not pass" and "the arm could not be measured" are DIFFERENT states and must not share
    # an exit code. A build break makes every arm unmeasurable, and reporting that as FAIL says the
    # capability is unarmed -- a claim about the product derived from a broken toolchain. So: if the run
    # produced no verdict counters at all, nothing was measured here, and that is a REFUSE.
    # A filter that matches nothing is NOT an unmeasurable run -- it is a measured absence, and the two
    # must not share a verdict. Observed: `dotnet test --filter` on a name that no longer exists prints
    # "No test matches the given testcase filter", emits NO result counters, and EXITS 0. So the check
    # below would see 'no counters' and call it REFUSE ('fix your toolchain'), when the truth is that a
    # declared arm was renamed or deleted -- a real evidence gap, and precisely what this gate exists to
    # report. It has to be tested BEFORE the counter check, because a zero-match run and a build break
    # are indistinguishable by counters alone.
    #
    # The exit code is no help here and is deliberately not consulted: a zero-match filter exits 0, so a
    # gate that trusted it would certify a deleted arm as a pass.
    if ($output -match 'No test matches the given testcase filter') {
        Write-Host "FAIL  $($cap.name) -- the declared arm matched no test. It was renamed or deleted, so nothing verifies this capability."
        $failures += $cap.name
        continue
    }

    $measured = $output -match 'Passed:\s*\d+' -or $output -match 'Failed:\s*\d+' -or $output -match 'Total:\s*\d+'
    if (-not $measured) {
        Write-Host "REFUSE $($cap.name) -- the arm could not be measured: the run produced no test-result counters (build failure, or the runner never started). This is NOT a report that the capability is unarmed."
        Write-Host ($output -split "`n" | Select-String -Pattern 'error |error CS|MSB|Unhandled' | Select-Object -First 10 | Out-String)
        $refusals += $cap.name
        continue
    }

    # Two remaining non-passing shapes, both treated as gate failure: the arm ran and failed, or it ran
    # and SKIPPED (the shape this gate exists
    # to catch that a bare exit-code check would miss: xUnit v3 does not fail the run on a skip).
    $passedOne = $output -match 'Passed:\s*1,'
    $failedZero = $output -match 'Failed:\s*0,'
    $skippedZero = $output -match 'Skipped:\s*0,'

    if ($passedOne -and $failedZero -and $skippedZero) {
        Write-Host "PASS  $($cap.name)"
    }
    else {
        Write-Host "FAIL  $($cap.name) -- arm did not report exactly 1 passed / 0 failed / 0 skipped"
        Write-Host ($output -split "`n" | Select-String -Pattern 'Passed|Failed|Skipped|Total|error CS' | Out-String)
        $failures += $cap.name
    }
}

Write-Host ""
# REFUSE outranks FAIL. An unmeasurable arm means this run does not know the state of that capability,
# so the run cannot be summarised as a verdict about the manifest -- and the operator has to fix the
# toolchain before either number means anything. Failures are still listed, so nothing is hidden.
if ($refusals.Count -gt 0) {
    Write-Host "E2E CAPABILITY GATE: REFUSED -- $($refusals.Count) of $($capabilities.Count) arms could not be measured:"
    foreach ($r in $refusals) { Write-Host "  - $r" }
    if ($failures.Count -gt 0) {
        Write-Host "  (also $($failures.Count) with no verified arm: $($failures -join ', '))"
    }
    Write-Host "This is not a pass and not a capability verdict. Fix the build/runner, then re-run."
    exit 2
}

if ($failures.Count -gt 0) {
    Write-Host "E2E CAPABILITY GATE: FAILED -- $($failures.Count) of $($capabilities.Count) advertised capabilities have no verified end-to-end arm:"
    foreach ($f in $failures) { Write-Host "  - $f" }
    exit 1
}

Write-Host "E2E CAPABILITY GATE: PASSED -- all $($capabilities.Count) advertised capabilities have a verified end-to-end arm."
exit 0
