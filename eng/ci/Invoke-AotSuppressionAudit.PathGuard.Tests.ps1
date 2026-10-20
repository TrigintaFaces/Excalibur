# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Runs isolated real-script path controls; preserves all receipts. Never regenerates the real repository baseline.
param(
    [string]$CandidateScript = (Join-Path $PSScriptRoot 'Invoke-AotSuppressionAudit.ps1')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'eng/package-composition.functions.ps1')
$candidate = (Resolve-Path -LiteralPath $CandidateScript).Path
$candidateHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$attempt = Join-Path (Join-Path $workspace 'artifacts/aot-audit-path-guard') ('attempt-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $attempt -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source

function Invoke-Case {
    param([string]$Name, [string]$Mode, [int]$ExpectedExit, [bool]$Generate = $true)
    $caseRoot = Join-Path $attempt $Name
    $repo = Join-Path $caseRoot 'repo'
    $ci = Join-Path $repo 'eng/ci'
    $source = Join-Path $repo 'src'
    $external = Join-Path $caseRoot 'external-src'
    $output = Join-Path $caseRoot 'audit-output'
    foreach ($directory in @($ci, $source, $external, (Join-Path $source 'subdir'))) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $copy = Join-Path $ci 'Invoke-AotSuppressionAudit.ps1'
    Copy-Item -LiteralPath $candidate -Destination $copy
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $candidateHash) { throw 'Candidate copy hash mismatch.' }
    foreach ($directory in @($source, $external)) {
        $justification = if ($directory -eq $source) { 'repository source sentinel' } else { 'external source sentinel' }
        "using System.Diagnostics.CodeAnalysis;`n[UnconditionalSuppressMessage(`"Trimming`", `"IL2026`", Justification = `"$justification`")]`npublic class EmptyFixture { }" |
            Set-Content -LiteralPath (Join-Path $directory 'EmptyFixture.cs')
    }
    $baseline = Join-Path $ci 'aot-suppression-baseline.json'
    '{"description":"preserve-this-sentinel","suppressions":[]}' | Set-Content -LiteralPath $baseline
    $before = (Get-FileHash -LiteralPath $baseline).Hash
    $target = $baseline
    $arguments = [Collections.Generic.List[string]]::new()
    foreach ($argument in @('-NoProfile', '-File', $copy, '-OutputPath', $output)) { $arguments.Add($argument) }
    if ($Generate) { $arguments.Add('-GenerateBaseline') }
    switch ($Mode) {
        'default' { $arguments.Add('-SrcPath'); $arguments.Add($external) }
        'explicit' { $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add($baseline) }
        'alias-target' { $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add((Join-Path $ci '../ci/aot-suppression-baseline.json')) }
        'partial' { $arguments.Add('-SrcPath'); $arguments.Add((Join-Path $source 'subdir')); $arguments.Add('-BaselinePath'); $arguments.Add($baseline) }
        'full-alias' { $arguments.Add('-SrcPath'); $arguments.Add((Join-Path $repo './src')); $arguments.Add('-BaselinePath'); $arguments.Add($baseline) }
        'external-target' { $target = Join-Path $caseRoot 'scratch-baseline.json'; $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add($target) }
        'sibling-prefix' { $sibling = Join-Path $caseRoot 'repo-other'; New-Item -ItemType Directory -Path $sibling | Out-Null; $target = Join-Path $sibling 'aot-suppression-baseline.json'; $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add($target) }
        'baseline-junction' {
            $parent = Join-Path $caseRoot 'baseline-link'
            New-Item -ItemType Junction -Path $parent -Target $ci | Out-Null
            $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add((Join-Path $parent 'aot-suppression-baseline.json'))
        }
        'source-junction' {
            $link = Join-Path $caseRoot 'source-link'
            New-Item -ItemType Junction -Path $link -Target $source | Out-Null
            $target = Join-Path $caseRoot 'scratch-baseline.json'
            $arguments.Add('-SrcPath'); $arguments.Add($link); $arguments.Add('-BaselinePath'); $arguments.Add($target)
        }
        'dangling-parent-junction' {
            $emptyTarget = Join-Path $repo 'new-baselines'
            New-Item -ItemType Directory -Path $emptyTarget | Out-Null
            $link = Join-Path $caseRoot 'dangling-parent'
            New-Item -ItemType Junction -Path $link -Target $emptyTarget | Out-Null
            if (-not [IO.Path]::GetFullPath($emptyTarget).StartsWith([IO.Path]::GetFullPath($repo) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe synthetic target.' }
            Remove-Item -LiteralPath $emptyTarget
            $target = Join-Path $link 'new-baseline.json'
            $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add($target)
        }
        'file-symlink' {
            $link = Join-Path $caseRoot 'baseline-link.json'
            try { New-Item -ItemType SymbolicLink -Path $link -Target $baseline | Out-Null }
            catch {
                $results.Add([ordered]@{ name=$Name; status='unsupported'; reason=$_.Exception.Message; candidateSha256=$candidateHash })
                return
            }
            $arguments.Add('-SrcPath'); $arguments.Add($external); $arguments.Add('-BaselinePath'); $arguments.Add($link)
        }
        default { throw "Unknown mode $Mode" }
    }
    $targetExisted = Test-Path -LiteralPath $target
    $log = Join-Path $caseRoot 'native'
    $commandError = $null
    try { Invoke-PackageCommand -Executable $pwsh -Arguments $arguments.ToArray() -Log $log -WorkingDirectory $repo -TimeoutSeconds 30 }
    catch { $commandError = $_.Exception.Message }
    $receipt = Get-Content -LiteralPath "$log.receipt.json" -Raw | ConvertFrom-Json
    $after = (Get-FileHash -LiteralPath $baseline).Hash
    $outputExists = Test-Path -LiteralPath $output
    $passed = -not $receipt.timedOut -and $receipt.exitCode -eq $ExpectedExit
    if ($ExpectedExit -eq 2) {
        $passed = $passed -and $before -eq $after -and -not $outputExists -and $null -ne $commandError
        if (-not $targetExisted) { $passed = $passed -and -not (Test-Path -LiteralPath $target) }
    }
    elseif ($Generate) {
        $generated = Get-Content -LiteralPath $target -Raw | ConvertFrom-Json
        $rows = @($generated.suppressions)
        $expectedJustification = if ($Mode -eq 'full-alias') { 'repository source sentinel' } else { 'external source sentinel' }
        $passed = $passed -and $null -eq $commandError -and $outputExists -and $rows.Count -eq 1 -and
            $rows[0].warningId -eq 'IL2026' -and $rows[0].justification -eq $expectedJustification -and
            $rows[0].file.EndsWith('/EmptyFixture.cs', [StringComparison]::Ordinal)
        if ($target -ne $baseline) { $passed = $passed -and $before -eq $after }
    }
    else { $passed = $passed -and $null -ne $commandError -and $before -eq $after -and $outputExists }
    $results.Add([ordered]@{ name=$Name; status=$(if($passed){'passed'}else{'failed'}); expectedExit=$ExpectedExit; actualExit=$receipt.exitCode; candidateSha256=$candidateHash; baselineBefore=$before; baselineAfter=$after; outputExists=$outputExists; receipt="$log.receipt.json"; commandError=$commandError })
}

try {
    Invoke-Case 'external-default' 'default' 2
    Invoke-Case 'external-explicit-repository' 'explicit' 2
    Invoke-Case 'external-alias-repository' 'alias-target' 2
    Invoke-Case 'partial-repository' 'partial' 2
    Invoke-Case 'full-source-alias' 'full-alias' 0
    Invoke-Case 'external-scratch-baseline' 'external-target' 0
    Invoke-Case 'sibling-repository-prefix' 'sibling-prefix' 0
    Invoke-Case 'read-only-external-audit' 'explicit' 1 -Generate $false
    Invoke-Case 'baseline-parent-junction' 'baseline-junction' 2
    Invoke-Case 'source-junction' 'source-junction' 2
    Invoke-Case 'dangling-baseline-parent-junction' 'dangling-parent-junction' 2
    Invoke-Case 'baseline-file-symlink' 'file-symlink' 2
}
finally {
    [ordered]@{ candidate=$candidate; candidateSha256=$candidateHash; attempt=$attempt; cases=$results.ToArray(); completedCount=$results.Count } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $attempt 'report.json')
}
if (@($results | Where-Object status -eq 'failed').Count -gt 0) { throw "Path-guard controls failed; retained evidence: $attempt" }
if ($results.Count -ne 12) { throw "Incomplete control census; retained evidence: $attempt" }
Write-Output "Path-guard controls completed; optional unsupported cases remain explicit: $attempt"
