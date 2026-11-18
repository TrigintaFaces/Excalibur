param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory $Output -ErrorAction Stop | Out-Null
$Output = (Resolve-Path $Output).Path
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name,[int]$Expected,[scriptblock]$Arrange,[string]$MemoryStatus='skipped',[string]$HotPathStatus='success') {
    $dir = Join-Path $Output $Name
    New-Item -ItemType Directory "$dir/input" | Out-Null
    foreach ($platform in @('ubuntu','windows','macos')) {
        $target = "$dir/input/hot-path-results-$platform-latest"
        New-Item -ItemType Directory "$target/results" | Out-Null
        $report = @{HostEnvironmentInfo=@{RuntimeVersion='fixture'}; Benchmarks=@(@{
            FullName='Fixture.Measure'; Parameters='Size=10'; Job=@{Id='Fixture'}
            Statistics=@{N=2;Mean=10.0;StandardDeviation=1.414;ConfidenceInterval=@{Lower=1;Upper=19}}
            Memory=@{BytesAllocatedPerOperation=0}
            Measurements=@(
                @{IterationMode='Workload';IterationStage='Actual';Operations=1;Nanoseconds=10},
                @{IterationMode='Workload';IterationStage='Result';Operations=1;Nanoseconds=9},
                @{IterationMode='Workload';IterationStage='Result';Operations=1;Nanoseconds=11}
            )
        })}
        $report | ConvertTo-Json -Depth 15 | Set-Content "$target/results/fixture-report-full.json"
        if ($platform -eq 'windows') { 'fixture trace, presence only' | Set-Content "$target/fixture.etl" }
    }
    & $Arrange $dir
    $inputPath = if ($Name -eq 'absent-input-root') { "$dir/absent" } else { "$dir/input" }
    & pwsh -NoProfile -File "$PSScriptRoot/perf-report.ps1" -ResultsPath $inputPath -Output "$dir/report" -SourceRevision fixture -HotPathStatus $HotPathStatus -MemoryStatus $MemoryStatus *> "$dir/run.log"
    $exit = $LASTEXITCODE
    $summary = Get-Content "$dir/report/performance-summary.json" -Raw | ConvertFrom-Json
    $text = Get-Content "$dir/report/performance-report.md" -Raw
    $passed = $exit -eq $Expected -and $summary.evidenceValid -eq ($Expected -eq 0) -and
        $text -notmatch 'All benchmark suites completed successfully|5-8%|Average Performance'
    if ($Name -eq 'healthy-skipped-optional') {
        $passed = $passed -and @($summary.cases | Where-Object platform -CEQ 'macOS').Count -eq 1 -and
            @($summary.artifacts | Where-Object upstreamSuiteStatus -CEQ 'skipped').Count -eq 2
    }
    $results.Add(@{name=$Name;expected=$Expected;actual=$exit;passed=$passed})
}
Check 'aggregate-matrix-failure' 1 {} 'skipped' 'failure'
Check 'absent-input-root' 1 {}
Check 'healthy-skipped-optional' 0 {}
Check 'malformed-sibling' 1 {param($d) '{' | Set-Content "$d/input/hot-path-results-ubuntu-latest/results/bad-report.json"}
Check 'missing-statistics' 1 {param($d)
    $p="$d/input/hot-path-results-ubuntu-latest/results/fixture-report-full.json"
    $r=Get-Content $p -Raw|ConvertFrom-Json; $r.Benchmarks[0].PSObject.Properties.Remove('Statistics'); $r|ConvertTo-Json -Depth 15|Set-Content $p
}
Check 'empty-required-artifact' 1 {param($d) Remove-Item -LiteralPath "$d/input/hot-path-results-ubuntu-latest/results/fixture-report-full.json"}
Check 'unknown-artifact' 1 {param($d)
    New-Item -ItemType Directory "$d/input/unknown"|Out-Null
    Copy-Item "$d/input/hot-path-results-ubuntu-latest/results/fixture-report-full.json" "$d/input/unknown/fixture-report.json"
}
Check 'profiler-failure-with-numeric-results' 1 {param($d) 'Unable to resolve IProfiler'|Set-Content "$d/input/hot-path-results-windows-latest/benchmark.log"}
Check 'missing-etw-trace' 1 {param($d) Remove-Item -LiteralPath "$d/input/hot-path-results-windows-latest/fixture.etl"}
Check 'failed-optional-suite' 1 {} 'failure'
Check 'successful-optional-missing-results' 1 {} 'success'
$results|ConvertTo-Json|Set-Content "$Output/results.json"
$results|Format-Table
if (@($results|Where-Object { !$_.passed }).Count) { throw 'Reporting regression controls failed' }
