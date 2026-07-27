<#
.SYNOPSIS
    Reaps orphaned test hosts left behind by a wedged shard, so they cannot poison later shards.

.DESCRIPTION
    Under xUnit v3 + MTP the test host is a NATIVE EXE NAMED AFTER THE ASSEMBLY
    (e.g. Some.Thing.Tests.exe) -- not testhost.exe and not dotnet.exe. A process hunt filtered on
    'testhost*' or 'dotnet*' returns a confident zero that could never have matched the subject.

    A wedged host SPINS rather than idling, and it SURVIVES killing the vstest/dotnet chain above it.
    While alive it keeps an exclusive handle on the test project's output DLLs, so the next build of
    that project dies with MSB3021 "being used by another process" and exits 1 -- a build break
    wearing a test-failure exit code, surfacing in a LATER shard than the one that leaked it. The
    symptom mutates each time, which is why this was re-diagnosed from scratch more than once.

    Prose in a runbook telling people to reap between shards is not a control; it has no exit code.
    This script is the control. Run it between shards.

    ORPHAN, precisely: a *.Tests.exe whose PARENT PROCESS NO LONGER EXISTS. A host still owned by a
    live dotnet/vstest parent is a RUNNING TEST, and killing it would corrupt the very run this
    script protects. That distinction is the whole safety property.

    Exit codes:
      0 = nothing to reap, or every orphan was killed
      1 = at least one orphan resisted termination (its DLL handles are still held)
      2 = cannot evaluate (process enumeration unavailable)

.PARAMETER MinAgeSeconds
    Ignore hosts younger than this. A host started moments ago may simply be mid-startup with a
    parent that has not been observed yet. Default 60.

.PARAMETER WhatIf
    Report what would be reaped without killing anything.

.PARAMETER SelfTest
    Runs the orphan-classification self-test and exits without touching any process.

.EXAMPLE
    ./Reap-OrphanTestHosts.ps1
    ./Reap-OrphanTestHosts.ps1 -WhatIf
#>
[CmdletBinding()]
param(
    [int]$MinAgeSeconds = 60,
    [switch]$WhatIf,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The classification is kept pure -- it takes a snapshot and returns a decision -- so the safety
# property (never kill a host with a live parent) can be exercised without spawning real processes.
function Get-OrphanTestHosts {
    param(
        # Each entry: @{ ProcessId; Name; ParentProcessId; CreationDate }
        [object[]]$Snapshot,
        [int[]]$LivePids,
        [datetime]$Now,
        [int]$MinAgeSeconds
    )

    $orphans = @()

    foreach ($proc in $Snapshot) {
        # Only MTP test hosts. The name IS the discriminator here -- see the note above about the
        # process hunt that filtered on testhost*/dotnet* and returned zero while three were running.
        if ($proc.Name -notlike '*.Tests.exe') { continue }

        # SAFETY: a live parent means this is a running test, not a leak. Never kill it.
        if ($LivePids -contains $proc.ParentProcessId) { continue }

        # A host that just started may not have been reconciled with its parent yet.
        if (($Now - $proc.CreationDate).TotalSeconds -lt $MinAgeSeconds) { continue }

        $orphans += $proc
    }

    return @($orphans)
}

if ($SelfTest) {
    $now = [datetime]'2026-01-01T12:00:00'
    $old = $now.AddMinutes(-30)

    $cases = @(
        @{
            Name     = 'SAFETY: a host with a LIVE parent is a running test and is never reaped'
            Snapshot = @(@{ ProcessId = 100; Name = 'Some.Thing.Tests.exe'; ParentProcessId = 50; CreationDate = $old })
            LivePids = @(50)
            Expect   = @()
        }
        @{
            Name     = 'SAFETY: a non-test process with a dead parent is never reaped'
            Snapshot = @(@{ ProcessId = 101; Name = 'devenv.exe'; ParentProcessId = 999; CreationDate = $old })
            LivePids = @()
            Expect   = @()
        }
        @{
            Name     = 'SAFETY: dotnet.exe/testhost.exe are not the MTP host and are not reaped by name'
            Snapshot = @(
                @{ ProcessId = 102; Name = 'dotnet.exe'; ParentProcessId = 999; CreationDate = $old }
                @{ ProcessId = 103; Name = 'testhost.exe'; ParentProcessId = 999; CreationDate = $old }
            )
            LivePids = @()
            Expect   = @()
        }
        @{
            Name     = 'SAFETY: a freshly started orphan is below the age floor and is left alone'
            Snapshot = @(@{ ProcessId = 104; Name = 'Some.Thing.Tests.exe'; ParentProcessId = 999; CreationDate = $now.AddSeconds(-5) })
            LivePids = @()
            Expect   = @()
        }
        @{
            Name     = 'LIVENESS: an aged host whose parent is GONE is reaped'
            Snapshot = @(@{ ProcessId = 105; Name = 'Some.Thing.Tests.exe'; ParentProcessId = 999; CreationDate = $old })
            LivePids = @(1, 2, 3)
            Expect   = @(105)
        }
        @{
            Name     = 'LIVENESS: several orphans are all reaped, and a sibling with a live parent survives'
            Snapshot = @(
                @{ ProcessId = 106; Name = 'A.Tests.exe'; ParentProcessId = 999; CreationDate = $old }
                @{ ProcessId = 107; Name = 'B.Tests.exe'; ParentProcessId = 998; CreationDate = $old }
                @{ ProcessId = 108; Name = 'C.Tests.exe'; ParentProcessId = 42; CreationDate = $old }
            )
            LivePids = @(42)
            Expect   = @(106, 107)
        }
    )

    $failed = 0
    foreach ($case in $cases) {
        $got = @(Get-OrphanTestHosts -Snapshot $case.Snapshot -LivePids $case.LivePids -Now $now -MinAgeSeconds 60 |
            ForEach-Object { $_.ProcessId })
        $expected = @($case.Expect)

        if (($got -join ',') -eq ($expected -join ',')) {
            Write-Host "  PASS  $($case.Name)"
        }
        else {
            Write-Host "  FAIL  $($case.Name): expected [$($expected -join ',')], got [$($got -join ',')]" -ForegroundColor Red
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

# Win32_Process is used deliberately: it is the only enumeration here that reports native processes
# AND their parent and start time. Get-Process cannot supply the parent, and a POSIX-style process
# listing on this platform cannot see native processes or their command lines at all.
try {
    $all = @(Get-CimInstance Win32_Process -ErrorAction Stop)
}
catch {
    Write-Host "[reap-orphan-test-hosts] CANNOT EVALUATE - process enumeration failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

$snapshot = $all | ForEach-Object {
    [pscustomobject]@{
        ProcessId       = [int]$_.ProcessId
        Name            = [string]$_.Name
        ParentProcessId = [int]$_.ParentProcessId
        CreationDate    = if ($_.CreationDate) { [datetime]$_.CreationDate } else { [datetime]::MinValue }
    }
}

$livePids = @($snapshot | ForEach-Object { $_.ProcessId })
$orphans = @(Get-OrphanTestHosts -Snapshot $snapshot -LivePids $livePids -Now (Get-Date) -MinAgeSeconds $MinAgeSeconds)

$hosts = @($snapshot | Where-Object { $_.Name -like '*.Tests.exe' })
Write-Host "[reap-orphan-test-hosts] test hosts alive: $($hosts.Count); orphaned: $($orphans.Count)"

if ($orphans.Count -eq 0) {
    Write-Host "[reap-orphan-test-hosts] nothing to reap."
    exit 0
}

$survivors = 0
foreach ($orphan in $orphans) {
    $age = [int]((Get-Date) - $orphan.CreationDate).TotalSeconds
    if ($WhatIf) {
        Write-Host "  WOULD REAP  pid $($orphan.ProcessId) $($orphan.Name) (parent $($orphan.ParentProcessId) gone, age ${age}s)" -ForegroundColor Yellow
        continue
    }

    Write-Host "  REAPING     pid $($orphan.ProcessId) $($orphan.Name) (parent $($orphan.ParentProcessId) gone, age ${age}s)" -ForegroundColor Yellow
    try {
        Stop-Process -Id $orphan.ProcessId -Force -ErrorAction Stop
    }
    catch {
        Write-Host "  SURVIVED    pid $($orphan.ProcessId): $($_.Exception.Message)" -ForegroundColor Red
        $survivors++
    }
}

if ($WhatIf) {
    Write-Host "[reap-orphan-test-hosts] -WhatIf: nothing was killed."
    exit 0
}

if ($survivors -gt 0) {
    # Reported, not swallowed: a surviving orphan still holds the output DLLs, so the next build of
    # that project will fail MSB3021 and the cause must not be invisible when it does.
    Write-Host "[reap-orphan-test-hosts] $survivors orphan(s) survived termination - later builds of their projects will fail MSB3021." -ForegroundColor Red
    exit 1
}

Write-Host "[reap-orphan-test-hosts] reaped $($orphans.Count) orphan(s)." -ForegroundColor Green
exit 0
