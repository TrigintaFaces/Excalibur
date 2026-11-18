# Validates BDN full/full-compressed raw measurements, not expected population,
# functional behavior, process success, or statistical evidence of a regression.
function Assert-PositiveMeasurementNumber($Value, [string]$Label, [switch]$Integer) {
    if ($Value -isnot [int] -and $Value -isnot [long] -and $Value -isnot [double] -and
        $Value -isnot [decimal] -and $Value -isnot [float]) {
        throw "$Label must be a JSON number"
    }
    $number = [double]$Value
    if (-not [double]::IsFinite($number) -or $number -le 0 -or
        ($Integer -and [Math]::Truncate($number) -ne $number)) {
        throw "$Label must be finite and positive (integral for counts)"
    }
}

function Assert-BenchmarkReportMeasurements($Report) {
    if ($null -eq $Report -or -not ($Report.PSObject.Properties.Name -contains 'Benchmarks') -or
        $Report.Benchmarks -isnot [array] -or $Report.Benchmarks.Count -eq 0) {
        throw 'Report must contain a nonempty Benchmarks array'
    }
    foreach ($case in $Report.Benchmarks) {
        if ($null -eq $case -or -not ($case.PSObject.Properties.Name -contains 'FullName') -or
            $case.FullName -isnot [string] -or [string]::IsNullOrWhiteSpace($case.FullName)) {
            throw 'Benchmark case has no FullName identity'
        }
        # Missing properties also reject under the caller's StrictMode.
        Assert-PositiveMeasurementNumber $case.Statistics.N 'Statistics.N' -Integer
        Assert-PositiveMeasurementNumber $case.Statistics.Mean 'Statistics.Mean'
        if ($case.Measurements -isnot [array]) { throw 'Raw Measurements array required' }
        $actual = @($case.Measurements | Where-Object {
            $_.IterationMode -ceq 'Workload' -and $_.IterationStage -ceq 'Actual'
        })
        $result = @($case.Measurements | Where-Object {
            $_.IterationMode -ceq 'Workload' -and $_.IterationStage -ceq 'Result'
        })
        if ($actual.Count -eq 0 -or $result.Count -ne $case.Statistics.N) {
            throw 'Workload Actual required and Result count must equal Statistics.N'
        }
        foreach ($measurement in @($actual) + @($result)) {
            Assert-PositiveMeasurementNumber $measurement.Operations 'Operations' -Integer
            Assert-PositiveMeasurementNumber $measurement.Nanoseconds 'Nanoseconds'
        }
        $mean = ($result | ForEach-Object {
            [double]$_.Nanoseconds / [double]$_.Operations
        } | Measure-Object -Average).Average
        if (-not [double]::IsFinite($mean) -or $mean -le 0) {
            throw 'Recomputed mean must be finite and positive'
        }
        # Numerical serialization tolerance, not a performance regression threshold.
        $tolerance = [Math]::Max(1e-9, [Math]::Abs($mean) * 1e-9)
        $difference = [Math]::Abs($mean - [double]$case.Statistics.Mean)
        if (-not [double]::IsFinite($tolerance) -or -not [double]::IsFinite($difference) -or
            $difference -gt $tolerance) {
            throw 'Statistics.Mean does not reconcile to Workload Result measurements'
        }
    }
    return $Report.Benchmarks.Count
}
