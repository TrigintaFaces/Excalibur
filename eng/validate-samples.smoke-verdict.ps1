# validate-samples.smoke-verdict.ps1 — the run-mode smoke VERDICT, as one testable function.
#
# WHY THIS IS A SEPARATE FILE.
#   The verdict used to be an inline if/elseif chain inside validate-samples.ps1. That made it
#   untestable in practice: the only way to exercise a branch was to build and run a real sample,
#   so the branches that matter most — a sample that HANGS, and a sample that exits 0 without
#   printing what its profile says proves it worked — were never executed by anything. Five
#   branches, zero coverage, and the two dishonest-green branches among them.
#
#   Extracting it changes no behavior. It makes the predicate directly assertable, which is the
#   point: a check whose predicate IS the requirement beats a proxy that merely correlates with it.
#
#   SINGLE-SOURCED ON PURPOSE. validate-samples.ps1 dot-sources this file and calls this function;
#   it does not carry its own copy of the rule. If this function and the gate ever disagree, the
#   gate is not using it, and the lock that binds this function is measuring nothing. The lock
#   asserts that wiring explicitly.
#
# THE RULE IT ENCODES — a success is DECLARED, never inferred from the absence of a crash.
#   A run-mode sample either exits cleanly, or — when it is meant to stay up — prints a readiness
#   line its smoke profile NOMINATES IN ADVANCE ('successMarker'). "Still alive at the timeout" is
#   not evidence the scenario ran. Equally, timing out is NOT failure per se: for a long-running
#   host sample, staying up is the success shape, so a declared marker that was reached and then
#   outlived the clock is a PASS. What is refused is the case where nothing distinguishes
#   "stayed up as designed" from "hung", because that is the one the gate used to paint green.
#
# Dot-source to use:  . "$PSScriptRoot/validate-samples.smoke-verdict.ps1"
# This file defines a function and MUST have no top-level side effects — the lock dot-sources it.

Set-StrictMode -Version Latest

function Get-SampleSmokeVerdict {
    <#
    .SYNOPSIS
        Decides PASS/FAIL for one run-mode sample smoke execution.
    .OUTPUTS
        A PSCustomObject with:
          Status  'PASS' | 'FAIL'
          Kind    a stable token naming WHICH rule fired (see below) — for callers and tests
          Display the one-line console text
          Color   console colour for Display
          Message the durable explanation recorded on the result row
    #>
    [CmdletBinding()]
    [OutputType([psobject])]
    param(
        # Did the process outlive its timeout (and get killed)?
        [Parameter(Mandatory)]
        [bool] $TimedOut,

        # Exit code. Meaningless when $TimedOut (the process was killed), so it is only read
        # on the not-timed-out paths.
        [AllowNull()]
        [object] $ExitCode,

        # Everything the sample wrote to stdout.
        [AllowNull()]
        [string] $Stdout,

        # The readiness line the profile NOMINATED, or null/blank when the profile declares none.
        [AllowNull()]
        [string] $SuccessMarker,

        [Parameter(Mandatory)]
        [int] $TimeoutSeconds
    )

    # A blank or whitespace marker is the same as no marker: it would be satisfied by any output
    # at all, which is the vacuous-declaration case. Treat it as undeclared rather than as met.
    $marker = $null
    if (-not [string]::IsNullOrWhiteSpace($SuccessMarker)) {
        $marker = [string]$SuccessMarker
    }

    $markerSeen = ($null -ne $marker) -and
                  (-not [string]::IsNullOrEmpty($Stdout)) -and
                  $Stdout.Contains($marker, [System.StringComparison]::Ordinal)

    if ($TimedOut -and $markerSeen) {
        # Declared stays-up sample that reached its declared readiness signal before the clock.
        return [pscustomobject]@{
            Status  = 'PASS'
            Kind    = 'StayedUpReachedMarker'
            Display = 'OK (stayed up after reaching declared success marker)'
            Color   = 'Green'
            Message = "Run-mode smoke passed (reached declared success marker, then stayed up for ${TimeoutSeconds}s)"
        }
    }

    if ($TimedOut -and $null -eq $marker) {
        # THE DEFECT THIS FUNCTION EXISTS FOR. Previously PASS, printed green, and counted as
        # executed. Nothing here distinguishes a host staying up as designed from a hang.
        return [pscustomobject]@{
            Status  = 'FAIL'
            Kind    = 'TimedOutNoMarkerDeclared'
            Display = "FAIL (timed out after ${TimeoutSeconds}s with no declared success signal)"
            Color   = 'Red'
            Message = "Run-mode smoke FAILED: the sample did not exit within ${TimeoutSeconds}s and its smoke profile declares no successMarker, so nothing distinguishes 'stayed up as designed' from 'hung'. Add a successMarker to the profile if this sample is expected to stay up."
        }
    }

    if ($TimedOut) {
        # A marker WAS declared and never appeared: the sample is hung short of readiness.
        return [pscustomobject]@{
            Status  = 'FAIL'
            Kind    = 'TimedOutMarkerMissing'
            Display = "FAIL (timed out after ${TimeoutSeconds}s with no declared success signal)"
            Color   = 'Red'
            Message = "Run-mode smoke FAILED: the sample did not exit within ${TimeoutSeconds}s and never printed its declared successMarker ('$marker')."
        }
    }

    if (0 -eq $ExitCode -and ($null -eq $marker -or $markerSeen)) {
        # Exited cleanly, and either nothing extra was demanded of it or it delivered what was.
        return [pscustomobject]@{
            Status  = 'PASS'
            Kind    = 'Exited'
            Display = 'OK'
            Color   = 'Green'
            Message = 'Run-mode smoke passed'
        }
    }

    if (0 -eq $ExitCode) {
        # Exited cleanly but never printed what the profile says proves it did its work.
        return [pscustomobject]@{
            Status  = 'FAIL'
            Kind    = 'ExitedMarkerMissing'
            Display = 'FAIL (exit 0 but declared success marker never appeared)'
            Color   = 'Red'
            Message = "Run-mode smoke FAILED: the sample exited 0 but never printed its declared successMarker ('$marker'), so the scenario is not established."
        }
    }

    return [pscustomobject]@{
        Status  = 'FAIL'
        Kind    = 'ExitCodeFailure'
        Display = "FAIL (exit $ExitCode)"
        Color   = 'Red'
        Message = "Run-mode smoke failed with exit code $ExitCode"
    }
}
