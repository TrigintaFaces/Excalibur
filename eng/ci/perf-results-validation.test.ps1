param(
    [string]$Gate = "$PSScriptRoot/perf-regression-check.ps1",
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
New-Item -ItemType Directory $Output -ErrorAction Stop | Out-Null
$Output = (Resolve-Path $Output).Path
$results = [Collections.Generic.List[object]]::new()
function New-Report {
    @{ Benchmarks = @(@{
        FullName = 'Fixture.Measure'; Method = 'Measure'
        Statistics = @{ N = 2; Mean = 10.0 }
        Memory = @{ BytesAllocatedPerOperation = 0 }
        Measurements = @(
            @{ IterationMode = 'Workload'; IterationStage = 'Actual'; Operations = 10; Nanoseconds = 120 },
            @{ IterationMode = 'Workload'; IterationStage = 'Actual'; Operations = 10; Nanoseconds = 125 },
            @{ IterationMode = 'Workload'; IterationStage = 'Actual'; Operations = 10; Nanoseconds = 130 },
            @{ IterationMode = 'Workload'; IterationStage = 'Result'; Operations = 10; Nanoseconds = 90 },
            @{ IterationMode = 'Workload'; IterationStage = 'Result'; Operations = 10; Nanoseconds = 110 }
        )
    }) }
}
function Check([string]$Name, [int]$Expected, [scriptblock]$Arrange, [string[]]$ExtraArguments = @()) {
    $dir = Join-Path $Output $Name
    New-Item -ItemType Directory "$dir/nested" | Out-Null
    $report = New-Report
    & $Arrange $report $dir
    & pwsh -NoProfile -File $Gate -ResultsPath $dir -RequireResultsOnly @ExtraArguments *> "$dir/gate.log"
    $actual = $LASTEXITCODE
    $results.Add(@{ name=$Name; expected=$Expected; actual=$actual; passed=($actual -eq $Expected) })
}
function Save($Report, $Directory) {
    $Report | ConvertTo-Json -Depth 12 -Compress | Set-Content "$Directory/nested/fixture-report-full-compressed.json"
}
Check 'healthy-zero-allocation' 0 { param($r,$d) Save $r $d }
Check 'empty-directory' 2 { param($r,$d) }
Check 'malformed' 2 { param($r,$d) '{' | Set-Content "$d/bad-report-full.json" }
Check 'healthy-and-malformed' 2 { param($r,$d) Save $r $d; '{' | Set-Content "$d/bad-report-full.json" }
Check 'empty-cases' 2 { param($r,$d) $r.Benchmarks=@(); Save $r $d }
Check 'missing-cases' 2 { param($r,$d) $r.Remove('Benchmarks'); Save $r $d }
Check 'non-array-cases' 2 { param($r,$d) $r.Benchmarks=$r.Benchmarks[0]; Save $r $d }
Check 'failed-case' 2 { param($r,$d) $r.Benchmarks[0].Statistics=$null; Save $r $d }
Check 'healthy-and-failed-case' 2 { param($r,$d) $r.Benchmarks += @{FullName='Fixture.Failed';Statistics=$null}; Save $r $d }
Check 'summary-only' 2 { param($r,$d) $r.Benchmarks[0].Remove('Measurements'); Save $r $d }
Check 'missing-identity' 2 { param($r,$d) $r.Benchmarks[0].Remove('FullName'); $r.Benchmarks[0].Remove('Method'); Save $r $d }
foreach ($v in @($null, '2', $true, 0, -1, 1.5, 3)) {
    $value=$v
    Check "invalid-n-$($results.Count)" 2 { param($r,$d) $r.Benchmarks[0].Statistics.N=$value; Save $r $d }
}
foreach ($v in @(0, -1, 'NaN', 'Infinity', '10', $true, $null, 999)) {
    $value=$v
    Check "invalid-mean-$($results.Count)" 2 { param($r,$d) $r.Benchmarks[0].Statistics.Mean=$value; Save $r $d }
}
foreach ($field in @('Operations','Nanoseconds')) {
    foreach ($v in @(0, -1, 'NaN', $null, $true)) {
        $value=$v
        Check "invalid-$field-$($results.Count)" 2 { param($r,$d) $r.Benchmarks[0].Measurements[3][$field]=$value; Save $r $d }
    }
}
Check 'fractional-operations' 2 { param($r,$d) $r.Benchmarks[0].Measurements[3].Operations=1.5; Save $r $d }
Check 'missing-actual' 2 { param($r,$d) $r.Benchmarks[0].Measurements=@($r.Benchmarks[0].Measurements|Where-Object IterationStage -EQ Result); Save $r $d }
Check 'missing-result' 2 { param($r,$d) $r.Benchmarks[0].Measurements=@($r.Benchmarks[0].Measurements|Where-Object IterationStage -EQ Actual); Save $r $d }
Check 'all-zero-noop' 2 { param($r,$d) $r.Benchmarks[0].Statistics.Mean=0; $r.Benchmarks[0].Measurements|ForEach-Object {$_.Nanoseconds=0}; Save $r $d }
Check 'invalid-with-allow-empty' 2 { param($r,$d) $r.Benchmarks[0].Statistics=$null; Save $r $d } @('-AllowEmpty')
Check 'healthy-full' 0 { param($r,$d) $r|ConvertTo-Json -Depth 12|Set-Content "$d/fixture-report-full.json" }
Check 'multiple-launches' 0 { param($r,$d) $r.Benchmarks[0].Measurements[3].LaunchIndex=1; $r.Benchmarks[0].Measurements[4].LaunchIndex=2; Save $r $d }
Check 'invalid-actual' 2 { param($r,$d) $r.Benchmarks[0].Measurements[0].Operations=0; Save $r $d }
Check 'extreme-mean-mismatch' 2 { param($r,$d) $r.Benchmarks[0].Measurements|ForEach-Object {$_.Nanoseconds=1e308; $_.Operations=1}; Save $r $d }
$results | ConvertTo-Json -Depth 5 | Set-Content "$Output/results.json"
$failed=@($results|Where-Object {-not $_.passed})
Write-Output "$($results.Count) controls; $($failed.Count) failed"
$failed | ForEach-Object { Write-Output "$($_.name): expected $($_.expected), actual $($_.actual)" }
if ($failed.Count) { exit 1 }
