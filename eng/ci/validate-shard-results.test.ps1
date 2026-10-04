# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$gate = Join-Path $PSScriptRoot 'validate-shard-results.ps1'
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("shard-result-gate-" + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $testRoot)
$failures = [Collections.Generic.List[string]]::new()
$caseCount = 0

function New-Trx([string[]]$Outcomes = @('Passed'), [string[]]$Names = @('A.Test')) {
    $definitions = ''
    $results = ''
    for ($index = 0; $index -lt $Outcomes.Count; $index++) {
        $id = "test-$index"
        $name = $Names[[Math]::Min($index, $Names.Length - 1)]
        $definitions += "<UnitTest id='$id' storage='A.Tests.dll' name='$name'/>"
        $results += "<UnitTestResult testId='$id' executionId='execution-$index' testName='$name' outcome='$($Outcomes[$index])'/>"
    }
    $passed = @($Outcomes | Where-Object { $_ -eq 'Passed' }).Count
    $skipped = @($Outcomes | Where-Object { $_ -eq 'NotExecuted' }).Count
    $failed = $Outcomes.Count - $passed - $skipped
    $executed = $Outcomes.Count - $skipped
    return "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010' id='$([guid]::NewGuid())'><TestDefinitions>$definitions</TestDefinitions><Results>$results</Results><ResultSummary outcome='Completed'><Counters total='$($Outcomes.Count)' executed='$executed' passed='$passed' failed='$failed' notExecuted='$skipped'/></ResultSummary></TestRun>"
}

function Assert-Case([string]$Name, [string[]]$Documents, [bool]$Pass, [string]$Expected = 'A.Tests', [int]$Count = 0) {
    $script:caseCount++
    $directory = Join-Path $testRoot $Name
    [void](New-Item -ItemType Directory -Path $directory)
    for ($index = 0; $index -lt $Documents.Count; $index++) {
        [IO.File]::WriteAllText((Join-Path $directory "$index.trx"), $Documents[$index])
    }
    $report = Join-Path $directory 'report'
    $output = & $shell -NoProfile -File $gate -TrxDir $directory -ExpectedAssemblies $Expected -OutDir $report 2>&1
    $code = $LASTEXITCODE
    if (($code -eq 0) -ne $Pass) {
        $failures.Add("${Name}: expected pass=$Pass, exit=$code, output=$output")
    }
    elseif ($Pass -and $Count -gt 0) {
        $actual = Get-Content (Join-Path $report 'shard-results.json') -Raw | ConvertFrom-Json
        if ($actual.DistinctTests -ne $Count) { $failures.Add("${Name}: expected $Count distinct tests, got $($actual.DistinctTests)") }
    }
    Write-Host "${Name}: exit=$code"
}

try {
    $valid = New-Trx
    Assert-Case 'complete-pass' @($valid) $true -Count 1
    Assert-Case 'same-display-distinct-identities' @(New-Trx @('Passed','Passed') @('Same.Display','Same.Display')) $true -Count 2
    Assert-Case 'all-skipped' @(New-Trx @('NotExecuted')) $false
    Assert-Case 'capability-marker-cannot-excuse-required-skip' @((New-Trx @('NotExecuted')).Replace("outcome='NotExecuted'/>", "outcome='NotExecuted'><Output><ErrorInfo><Message>[capability-not-applicable] unavailable provider</Message></ErrorInfo></Output></UnitTestResult>")) $false
    Assert-Case 'mixed-skipped' @(New-Trx @('Passed','NotExecuted')) $false
    Assert-Case 'failed' @(New-Trx @('Failed')) $false
    Assert-Case 'unknown-outcome' @($valid.Replace("outcome='Passed'", "outcome='Mystery'")) $false
    Assert-Case 'missing-assembly' @($valid) $false -Expected 'Missing.Tests'
    Assert-Case 'absent-artifacts' @() $false
    Assert-Case 'malformed-xml' @('<TestRun>') $false
    Assert-Case 'missing-summary' @($valid -replace '<ResultSummary.*?</ResultSummary>','') $false
    Assert-Case 'aborted-run-with-pass' @($valid.Replace("outcome='Completed'", "outcome='Aborted'")) $false
    Assert-Case 'bad-counter' @($valid.Replace("total='1'", "total='not-a-count'")) $false
    Assert-Case 'counter-row-mismatch' @($valid.Replace("total='1'", "total='2'")) $false
    Assert-Case 'missing-result-row' @($valid -replace '<UnitTestResult[^>]+/>','') $false
    Assert-Case 'orphan-result' @($valid.Replace("<UnitTest id='test-0'", "<UnitTest id='other'")) $false
    $definition = [regex]::Match($valid, '<UnitTest [^>]+/>').Value
    Assert-Case 'duplicate-definitions' @($valid.Replace('</TestDefinitions>', "$definition</TestDefinitions>")) $false
    $duplicate = New-Trx @('Passed','Passed')
    Assert-Case 'duplicate-result-identity' @($duplicate.Replace("testId='test-1'", "testId='test-0'")) $false
    Assert-Case 'duplicate-execution-identity' @($duplicate.Replace("executionId='execution-1'", "executionId='execution-0'")) $false
    Assert-Case 'duplicate-artifact' @($valid, $valid) $false
    Assert-Case 'duplicate-run-identity' @($valid, $valid.Replace('A.Test', 'B.Test')) $false
    Assert-Case 'runner-error-after-pass' @($valid.Replace('</ResultSummary>', '<RunInfos><RunInfo outcome="Error">testhost crashed</RunInfo></RunInfos></ResultSummary>')) $false
    Assert-Case 'unexecuted-definition' @($valid.Replace('</TestDefinitions>', "<UnitTest id='never-ran' storage='A.Tests.dll'/></TestDefinitions>")) $false
    Assert-Case 'passing-sibling-cannot-hide-skip' @($valid, (New-Trx @('NotExecuted'))) $false
    $benignInfo = '<RunInfos><RunInfo outcome="Warning"><Text>Data collector ''Blame'' message: All tests finished running, Sequence file will not be generated.</Text></RunInfo></RunInfos>'
    Assert-Case 'completed-blame-warning' @($valid.Replace('</ResultSummary>', "$benignInfo</ResultSummary>")) $true -Count 1
    Assert-Case 'unknown-warning' @($valid.Replace('</ResultSummary>', '<RunInfos><RunInfo outcome="Warning"><Text>Adapter failed to load.</Text></RunInfo></RunInfos></ResultSummary>')) $false
    Assert-Case 'duplicate-summary' @($valid.Replace('</TestRun>', '<ResultSummary outcome="Failed"/></TestRun>')) $false
    Assert-Case 'duplicate-counters' @($valid.Replace('</ResultSummary>', '<Counters total="1" executed="1" passed="0" failed="1" notExecuted="0"/></ResultSummary>')) $false
    Assert-Case 'unknown-counter' @($valid.Replace("total='1'", "unexpected='0' total='1'")) $false
    Assert-Case 'wrong-namespace' @($valid.Replace('http://microsoft.com/schemas/VisualStudio/TeamTest/2010', 'urn:wrong')) $false
    Assert-Case 'nested-results' @($valid.Replace("outcome='Passed'/>", "outcome='Passed'><InnerResults><UnitTestResult outcome='Failed'/></InnerResults></UnitTestResult>")) $false
    if ($failures.Count -gt 0) { throw ($failures -join "`n") }
    Write-Host "PASS: $caseCount shard-result safety/liveness cases."
}
finally {
    # Delete only the exact temporary directory created by this invocation.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'shard-result-gate-*') { throw 'Refusing unsafe fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
