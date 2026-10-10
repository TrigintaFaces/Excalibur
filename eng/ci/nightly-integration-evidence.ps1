# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
<#
.SYNOPSIS
  Run or independently verify a nightly integration shard using the shared required-test gates.
.DESCRIPTION
  Expected context comes from the workflow and checked-out shard, never the execution plan.
  Provider identifies the mixed integration infrastructure profile; Cosmos reachability is a
  separate workflow gate. Cancellation without a receipt is incomplete, never successful.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Run','Verify')][string]$Mode,
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Shard,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [string]$RunnerOutcome = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = [IO.Path]::GetFullPath($ResultsDirectory)
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
foreach ($name in @('GITHUB_SHA','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','GITHUB_JOB','RUNNER_OS')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { throw "Missing workflow context: $name" }
}
$head = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -cne $env:GITHUB_SHA) { throw 'Checkout differs from expected nightly candidate.' }
if ($Shard -notmatch '^[a-z0-9-]+$') { throw 'Invalid shard identity.' }
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$context = @{
    candidateSha = $env:GITHUB_SHA; runId = $env:GITHUB_RUN_ID; runAttempt = $env:GITHUB_RUN_ATTEMPT
    job = $env:GITHUB_JOB; shard = $Shard; os = $env:RUNNER_OS; provider = 'mixed-integration'
    source = [IO.Path]::GetRelativePath($repo,$sourcePath).Replace('\','/')
    sourceSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    # RequiresCredentials is excluded, and the parentheses are load-bearing: without them the trailing
    # AND binds only to the EndToEnd term and the Integration tests come through unfiltered.
    #
    # Those tests need a real cloud key vault, so on a hosted runner they reach their availability guard
    # and report NotExecuted. This filter is shared by discovery and by the run -- the same string builds
    # the required set and invokes it -- so a test that can never execute here was being DISCOVERED AS
    # REQUIRED and then skipped, and the evidence gate refused the whole lane with "Required test did not
    # pass" while every test that ran passed. Measured on the dispatch lane: 2018 passed, 11 NotExecuted,
    # all eleven from the one class, against four tests in that same class that need no credentials and
    # pass. So the exclusion is per-test rather than per-class -- a class-level trait would have dropped
    # those four as well.
    #
    # The absence is now visible as an excluded category instead of a skip inside a required set, which is
    # the honest shape: a test that cannot run in an environment is not required to pass there. The
    # availability guards stay, because they are what lets the same tests run for someone who has
    # credentials configured.
    filter = '(Category=Integration|Category=EndToEnd)&Category!=RequiresCredentials'
}
$prefix = "nightly-integration-$Shard"
$evidence = Join-Path $root "$prefix.required"
$receiptPath = Join-Path $root "$prefix.runner.json"
[void][IO.Directory]::CreateDirectory($root)
$contextPath = Join-Path $root "$prefix.$($Mode.ToLowerInvariant()).context.json"
ConvertTo-Json -InputObject $context | Set-Content -LiteralPath $contextPath -Encoding utf8NoBOM
if ($Mode -eq 'Run') {
    if ((Test-Path -LiteralPath $receiptPath) -or (Test-Path -LiteralPath $evidence)) { throw 'Nightly evidence must be fresh.' }
    # 100 minutes. The session bound must leave a HEALTHY run room to finish, or it converts slow into
    # aborted and the abort is indistinguishable from a real failure: the excalibur shard reached 3851 of
    # 3937 identities in 75 minutes under the previous 4500000, so every arm after that point went
    # unexamined while 0 tests had failed. A genuine hang is still caught by -BlameTimeout, which names the
    # test; this bound only has to sit under the 120-minute job cap with room for checkout and build.
    $arguments = @('-NoProfile','-File',(Join-Path $repo 'eng/build.ps1'),'-Test','-NoRestore','-NoBuild',
        '-Project',$sourcePath,'-TestFilter',$context.filter,'-ResultsDirectory',$root,'-ResultsPrefix',$prefix,
        '-MaxCpuCount','1','-BlameTimeout','10m','-TestSessionTimeout','6000000','-RequiredTestContext',$contextPath)
    $started = [DateTimeOffset]::UtcNow.ToString('O')
    & $shell @arguments 2>&1 | Tee-Object -FilePath (Join-Path $root "$prefix.runner.log")
    $runnerExit = $LASTEXITCODE
    @{
        schemaVersion = 1; context = $context; exitCode = $runnerExit
        startedUtc = $started; completedUtc = [DateTimeOffset]::UtcNow.ToString('O'); command = @($shell) + $arguments
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
    exit $runnerExit
}

# Each verifier runs even when another refused. The producer's outcome remains an independent
# requirement: surviving passing rows cannot override a crash in its final cleanup or validation.
$failed = $RunnerOutcome -cne 'success'
if ($failed) { Write-Host "REFUSE: integration step outcome was '$RunnerOutcome'." }
try {
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    if ($receipt.schemaVersion -ne 1 -or ($receipt.exitCode -isnot [long] -and $receipt.exitCode -isnot [int]) -or $receipt.exitCode -ne 0) {
        throw 'Nightly runner did not complete successfully.'
    }
    foreach ($key in $context.Keys) {
        if ($receipt.context[$key] -cne $context[$key]) { throw "Nightly runner context mismatch: $key" }
    }
} catch { Write-Host "REFUSE: $($_.Exception.Message)"; $failed = $true }
& python (Join-Path $PSScriptRoot 'required-test-evidence.py') --plan (Join-Path $evidence 'plan.json') --expected-context $contextPath --repo-root $repo
if ($LASTEXITCODE -ne 0) { $failed = $true }
& $shell -NoProfile -File (Join-Path $PSScriptRoot 'validate-shard-results.ps1') -TrxDir $evidence `
    -ExpectedAssembliesFile (Join-Path $evidence 'expected-assemblies.json')
if ($LASTEXITCODE -ne 0) { $failed = $true }
if ($failed) { throw 'Nightly integration evidence is incomplete or unsuccessful.' }
Write-Host 'Nightly integration runner and all independently required test identities passed.'
