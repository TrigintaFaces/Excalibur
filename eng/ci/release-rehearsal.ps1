#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Release rehearsal -- validates the full shipping pipeline matches release.yml behavior.

.DESCRIPTION
    Executes the canonical release validation pipeline using ShippingOnly.slnf as the single
    shipping graph. This is the "can we ship?" check that mirrors the actual release workflow.

    Steps:
    1. Restore (ShippingOnly.slnf)
    2. Build (Release, ShippingOnly.slnf)
    3. Pack (local feed)
    4. Validate package composition
    5. Validate NuSpec dependencies
    6. Public API baseline audit
    7. Validate governance stack

    Sprint 639 C.1 (bd-bvc8e).

.PARAMETER OutDir
    Output directory for rehearsal artifacts and report. Defaults to ReleaseRehearsalReport.

.PARAMETER NoBuild
    Skip restore and build steps (use if already built in Release configuration).

.PARAMETER StopOnFirstFailure
    Stop execution at the first failing step instead of running all steps.

.EXAMPLE
    .\release-rehearsal.ps1

.EXAMPLE
    .\release-rehearsal.ps1 -NoBuild

.EXAMPLE
    .\release-rehearsal.ps1 -StopOnFirstFailure
#>
param(
    [string]$OutDir = 'ReleaseRehearsalReport',
    [switch]$NoBuild,
    [switch]$StopOnFirstFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ShippingSlnf = Join-Path $RepoRoot 'eng/ci/shards/ShippingOnly.slnf'
$StartTime = [DateTime]::UtcNow

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

if (-not (Test-Path $ShippingSlnf)) {
    throw "ShippingOnly.slnf not found at: $ShippingSlnf"
}

# --- Step tracking ---
$steps = @()
$failureCount = 0

function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    $stepStart = [DateTime]::UtcNow
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  STEP: $Name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $result = [pscustomobject]@{
        Name = $Name
        Status = 'skipped'
        Duration = [TimeSpan]::Zero
        Error = $null
    }

    try {
        & $Action
        $result.Status = 'passed'
        Write-Host "  PASSED: $Name" -ForegroundColor Green
    }
    catch {
        $result.Status = 'failed'
        $result.Error = $_.Exception.Message
        $script:failureCount++
        Write-Host "  FAILED: $Name -- $($_.Exception.Message)" -ForegroundColor Red

        if ($StopOnFirstFailure) {
            $result.Duration = ([DateTime]::UtcNow - $stepStart)
            $script:steps += $result
            throw "Release rehearsal stopped at step '$Name': $($_.Exception.Message)"
        }
    }

    $result.Duration = ([DateTime]::UtcNow - $stepStart)
    $script:steps += $result
}

# --- Step 1: Restore ---
if (-not $NoBuild) {
    Run-Step 'Restore (ShippingOnly.slnf)' {
        $output = dotnet restore $ShippingSlnf --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed (exit code $LASTEXITCODE): $($output | Select-Object -Last 5 | Out-String)"
        }
    }

    # --- Step 2: Build ---
    Run-Step 'Build (Release, ShippingOnly.slnf)' {
        $output = dotnet build $ShippingSlnf --configuration Release --no-restore --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed (exit code $LASTEXITCODE): $($output | Select-Object -Last 10 | Out-String)"
        }
    }
}
else {
    Write-Host "Skipping restore/build steps (-NoBuild specified)." -ForegroundColor Yellow
}

# --- Step 3: Pack ---
Run-Step 'Pack (local feed)' {
    $packScript = Join-Path $RepoRoot 'eng/pack-local.ps1'
    if (-not (Test-Path $packScript)) {
        throw "pack-local.ps1 not found at: $packScript"
    }
    $packArgs = @()
    if ($NoBuild) { $packArgs += '-NoBuild' }
    & $packScript @packArgs
    if ($LASTEXITCODE -ne 0) {
        throw "pack-local.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 4: Validate package composition ---
Run-Step 'Validate package composition' {
    $compScript = Join-Path $RepoRoot 'eng/validate-package-composition.ps1'
    if (-not (Test-Path $compScript)) {
        throw "validate-package-composition.ps1 not found at: $compScript"
    }
    & $compScript -SkipBuild
    if ($LASTEXITCODE -ne 0) {
        throw "validate-package-composition.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 5: Validate NuSpec dependencies ---
Run-Step 'Validate NuSpec dependencies' {
    $nuspecScript = Join-Path $RepoRoot 'eng/ci/validate-nuspec-dependencies.ps1'
    if (-not (Test-Path $nuspecScript)) {
        throw "validate-nuspec-dependencies.ps1 not found at: $nuspecScript"
    }
    & $nuspecScript
    if ($LASTEXITCODE -ne 0) {
        throw "validate-nuspec-dependencies.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 6: Public API baseline audit ---
Run-Step 'Public API baseline audit' {
    $apiScript = Join-Path $RepoRoot 'eng/ci/public-api-baseline-audit.ps1'
    if (-not (Test-Path $apiScript)) {
        throw "public-api-baseline-audit.ps1 not found at: $apiScript"
    }
    & $apiScript
    if ($LASTEXITCODE -ne 0) {
        throw "public-api-baseline-audit.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Step 7: Validate governance stack ---
# A rehearsal that packs but never looks at the RELEASE PIPELINE rehearses half the release. The
# defect this catches shipped for months: create-release published the GitHub release with
# draft: false while publish-nuget depended on it, so the public announcement preceded the packages
# existing and a publish failure left a release pointing at packages that were never uploaded.
#
# Nothing about that is observable from packing. It is a property of the job graph, and a property
# is exactly what a rehearsal can check for free -- no tag, no publish, no side effect.
Run-Step 'Release ordering: draft precedes publish' {
    $releaseYml = Join-Path $RepoRoot '.github/workflows/release.yml'
    if (-not (Test-Path $releaseYml)) { throw "release.yml not found at: $releaseYml" }

    # Parsed as YAML rather than grepped: `draft: true` appearing SOMEWHERE in the file says
    # nothing about which job it belongs to, and this assertion is about the graph.
    $py = @'
import sys, io, yaml
d = yaml.safe_load(io.open(sys.argv[1], encoding="utf-8").read())
jobs = d.get("jobs") or {}
problems = []

cr = jobs.get("create-release")
if not cr:
    problems.append("create-release job is missing")
else:
    rel = [s for s in (cr.get("steps") or []) if "action-gh-release" in str(s.get("uses", ""))]
    if not rel:
        problems.append("create-release has no action-gh-release step")
    else:
        draft = (rel[0].get("with") or {}).get("draft")
        if draft is not True:
            problems.append(
                f"create-release publishes with draft={draft!r}; it must be True so the release is "
                "not announced before publish-nuget has put the packages on the feed")

fin = jobs.get("finalize-release")
if not fin:
    problems.append("finalize-release job is missing; nothing would ever publish the draft")
else:
    needs = fin.get("needs") or []
    if "publish-nuget" not in needs:
        problems.append(f"finalize-release does not depend on publish-nuget (needs={needs})")
    cond = str(fin.get("if") or "")
    if "publish-nuget.result" not in cond or "success" not in cond:
        problems.append(
            f"finalize-release condition {cond!r} does not require publish-nuget to have SUCCEEDED; "
            "it could publish a release whose packages failed to upload")
    if "always()" in cond:
        problems.append("finalize-release uses always(); a failed publish must leave the release a draft")

# Public publishing must be GATED on staged validation, not merely preceded by it. Publication to
# NuGet.org cannot be undone, so the packages have to be proven installable from a real remote feed
# while they are still a candidate. A `needs` edge is the whole enforcement, and a `needs` edge is
# one careless edit from gone -- which is why it is asserted here rather than trusted.
pn = jobs.get("publish-nuget")
if not pn:
    problems.append("publish-nuget job is missing")
else:
    needs = pn.get("needs") or []
    if isinstance(needs, str):
        needs = [needs]
    if "staging-validation" not in needs:
        problems.append(
            f"publish-nuget does not depend on staging-validation (needs={needs}); packages could "
            "reach NuGet.org without ever being proven to install from a remote feed, and that "
            "cannot be undone")
    # always() would let publication proceed THROUGH a failed or skipped validation, which is the
    # same as having no gate while still appearing to have one.
    cond = str(pn.get("if") or "")
    if "always()" in cond:
        problems.append(
            f"publish-nuget condition {cond!r} uses always(); it would publish even when staged "
            "validation failed or was skipped")

sv = jobs.get("staging-validation")
if not sv:
    problems.append("staging-validation job is missing; publish-nuget's gate would not exist")
else:
    # The gate must consume the ONE canonical package set (Item 5.1). Validating a different
    # artifact than the one published proves nothing about what ships.
    dl = [s for s in (sv.get("steps") or []) if "download-artifact" in str(s.get("uses", ""))]
    names = [str((s.get("with") or {}).get("name") or "") for s in dl]
    if "packages" not in names:
        problems.append(
            f"staging-validation does not download the canonical 'packages' artifact (found {names}); "
            "it would validate something other than what publish-nuget uploads")

if problems:
    for p in problems:
        print("FAIL: " + p)
    sys.exit(1)
print("create-release drafts; finalize-release publishes only after publish-nuget succeeds")
'@
    $pyFile = Join-Path ([System.IO.Path]::GetTempPath()) 'rehearsal-order.py'
    $py | Out-File -FilePath $pyFile -Encoding UTF8
    $out = & python3 $pyFile $releaseYml 2>&1
    $out | ForEach-Object { Write-Host "  $_" }
    if ($LASTEXITCODE -ne 0) { throw 'Release ordering invariant violated -- see above.' }
}

# Report-only. Answers "if we released from THIS commit, would the version collide?" before anyone
# tags, using the same classification the release itself performs. It never fails the rehearsal on a
# collision -- a rehearsal is not a release, and a version being published is not an error here --
# but it turns a surprise at tag time into a line in a report.
Run-Step 'Publication state of the version this commit would ship' {
    $pkgs = Get-ChildItem -Path (Join-Path $RepoRoot 'artifacts') -Filter '*.nupkg' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' }
    if (-not $pkgs) {
        Write-Host "  no packed .nupkg found; skipping (pack step is the source of the version)"
        return
    }

    # Version is whatever Pack actually produced -- read from the artifact, not recomputed, so this
    # cannot disagree with the thing that would be published.
    $sample = $pkgs[0].BaseName
    if ($sample -notmatch '^(?<id>.+?)\.(?<ver>\d+\.\d+\.\d+.*)$') {
        Write-Host "  could not parse a version from '$sample'; skipping"
        return
    }
    $version = $Matches['ver']
    Write-Host "  version this commit would ship: $version"

    $published = 0; $total = 0; $unknown = 0
    foreach ($p in $pkgs) {
        if ($p.BaseName -notmatch '^(?<id>.+?)\.(?<ver>\d+\.\d+\.\d+.*)$') { continue }
        $total++
        $id = $Matches['id'].ToLowerInvariant()
        $url = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
        try {
            $resp = Invoke-WebRequest -Uri $url -Method Head -SkipHttpErrorCheck -TimeoutSec 20
            switch ($resp.StatusCode) {
                200 { $published++ }
                404 { }
                default { $unknown++ }
            }
        } catch { $unknown++ }
    }

    $mode = if ($published -eq 0) { 'fresh' } elseif ($published -eq $total) { 'resumed' } else { 'partial' }
    Write-Host "  publication state: $mode ($published of $total already on NuGet; $unknown unanswered)"
    if ($mode -ne 'fresh') {
        Write-Host "  NOTE: releasing this version would be a re-run, not a first publish." -ForegroundColor Yellow
    }
    if ($unknown -gt 0) {
        Write-Host "  NOTE: $unknown package(s) could not be checked; treat this report as incomplete." -ForegroundColor Yellow
    }
}

Run-Step 'Validate governance stack' {
    $govScript = Join-Path $RepoRoot 'eng/ci/validate-governance-stack.ps1'
    if (-not (Test-Path $govScript)) {
        throw "validate-governance-stack.ps1 not found at: $govScript"
    }
    & $govScript
    if ($LASTEXITCODE -ne 0) {
        throw "validate-governance-stack.ps1 failed (exit code $LASTEXITCODE)"
    }
}

# --- Report ---
$totalDuration = ([DateTime]::UtcNow - $StartTime)
$passedCount = @($steps | Where-Object { $_.Status -eq 'passed' }).Count
$failedCount = @($steps | Where-Object { $_.Status -eq 'failed' }).Count
$skippedCount = @($steps | Where-Object { $_.Status -eq 'skipped' }).Count
$overallStatus = if ($failedCount -eq 0) { 'PASSED' } else { 'FAILED' }

$summaryPath = Join-Path $OutDir 'summary.md'
$jsonPath = Join-Path $OutDir 'release-rehearsal-report.json'

$summary = @(
    '# Release Rehearsal Report',
    '',
    "- **Status:** $overallStatus",
    "- **Date:** $($StartTime.ToString('yyyy-MM-dd HH:mm:ss')) UTC",
    "- **Duration:** $($totalDuration.ToString('hh\:mm\:ss'))",
    "- **Shipping graph:** ShippingOnly.slnf",
    "- **Steps:** $($steps.Count) total ($passedCount passed, $failedCount failed, $skippedCount skipped)",
    '',
    '## Step Results',
    '',
    '| Step | Status | Duration |',
    '|------|--------|----------|'
)

foreach ($step in $steps) {
    $icon = switch ($step.Status) {
        'passed' { 'PASS' }
        'failed' { 'FAIL' }
        'skipped' { 'SKIP' }
    }
    $summary += "| $($step.Name) | $icon | $($step.Duration.ToString('mm\:ss')) |"
}

if ($failedCount -gt 0) {
    $summary += ''
    $summary += '## Failures'
    $summary += ''
    foreach ($step in ($steps | Where-Object { $_.Status -eq 'failed' })) {
        $summary += "### $($step.Name)"
        $summary += ''
        $summary += "``$($step.Error)``"
        $summary += ''
    }
}

$summary | Out-File -FilePath $summaryPath -Encoding UTF8

$report = [pscustomobject]@{
    status = $overallStatus
    date = $StartTime.ToString('o')
    durationSeconds = [int]$totalDuration.TotalSeconds
    shippingGraph = 'eng/ci/shards/ShippingOnly.slnf'
    steps = $steps
}
$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  RELEASE REHEARSAL: $overallStatus" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  Duration: $($totalDuration.ToString('hh\:mm\:ss'))" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "  Report: $summaryPath" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })
Write-Host "========================================" -ForegroundColor $(if ($overallStatus -eq 'PASSED') { 'Green' } else { 'Red' })

if ($failedCount -gt 0) {
    exit 1
}
