param(
    [Parameter(Mandatory)][string]$ResultsPath,
    [Parameter(Mandatory)][string]$Output,
    [Parameter(Mandatory)][string]$SourceRevision,
    [ValidateSet('success','failure','cancelled','skipped')][string]$HotPathStatus = 'failure',
    [ValidateSet('success','failure','cancelled','skipped')][string]$MemoryStatus = 'skipped',
    [ValidateSet('success','failure','cancelled','skipped')][string]$ThroughputStatus = 'skipped'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/perf-results-validation.ps1"
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$errors = [Collections.Generic.List[string]]::new()
$rows = [Collections.Generic.List[object]]::new()
$artifacts = [Collections.Generic.List[object]]::new()
$mapping = [ordered]@{
    'hot-path-results-ubuntu-latest' = @('HotPath','Linux',$HotPathStatus)
    'hot-path-results-windows-latest' = @('HotPath','Windows',$HotPathStatus)
    'hot-path-results-macos-latest' = @('HotPath','macOS',$HotPathStatus)
    'memory-benchmark-results' = @('Memory','Linux',$MemoryStatus)
    'throughput-benchmark-results' = @('Throughput','Linux',$ThroughputStatus)
}
foreach ($name in $mapping.Keys) {
    $suite,$platform,$status = $mapping[$name]
    $directory = Join-Path $ResultsPath $name
    $artifactErrors = [Collections.Generic.List[string]]::new()
    $files=@(); $logs=@(); $traces=@(); $diagnosticFiles=@()
    if (Test-Path -LiteralPath $directory -PathType Container) {
        try {
            $files = @(Get-ChildItem -LiteralPath $directory -Filter '*-report*.json' -Recurse -File -ErrorAction Stop)
            $logs = @(Get-ChildItem -LiteralPath $directory -Filter '*.log' -Recurse -File -ErrorAction Stop)
            $traces = @(Get-ChildItem -LiteralPath $directory -Filter '*.etl' -Recurse -File -ErrorAction Stop | Where-Object Length -GT 0)
            $diagnosticFiles = @(@($logs)+@($traces) | ForEach-Object {
                @{path=[IO.Path]::GetRelativePath($directory,$_.FullName); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
            })
        } catch { $artifactErrors.Add("Artifact enumeration or hashing failed: $($_.Exception.Message)") }
    }
    if ($status -ne 'success' -and !($status -eq 'skipped' -and $suite -ne 'HotPath')) {
        $artifactErrors.Add("Aggregate upstream suite status: $status")
    }
    if ($status -eq 'success' -and !$files.Count) { $artifactErrors.Add('No benchmark JSON reports') }
    if ($status -eq 'skipped' -and $files.Count) { $artifactErrors.Add('Reports present for skipped suite') }
    # A requested profiler must not silently degrade to ordinary timing.
    if ($platform -eq 'Windows' -and $files.Count) {
        if (!$traces.Count) { $artifactErrors.Add('Requested ETW trace unavailable; numeric rows do not verify profiling') }
    }
    foreach ($log in $logs) {
        if (Select-String -LiteralPath $log.FullName -Quiet -Pattern 'UnresolvedDiagnoser|Unable to resolve IProfiler|Must be elevated \(Admin\)|EtwProfiler are not supported') {
            $artifactErrors.Add("Profiler prerequisite failure in $($log.Name)")
        }
    }
    foreach ($file in $files) {
        try {
            $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            $null = Assert-BenchmarkReportMeasurements $report
            foreach ($case in $report.Benchmarks) {
                $rows.Add([ordered]@{
                    artifact=$name; suite=$suite; platform=$platform; upstreamSuiteStatus=$status
                    fullName=$case.FullName; parameters=$case.Parameters; job=$case.Job
                    statistics=$case.Statistics; memory=$case.Memory; meanUnit='ns/op'
                    hostEnvironment=$report.HostEnvironmentInfo
                    report=[IO.Path]::GetRelativePath((Resolve-Path $ResultsPath).Path,$file.FullName)
                    reportSha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                })
            }
        } catch { $artifactErrors.Add("$($file.Name): $($_.Exception.Message)") }
    }
    foreach ($problem in $artifactErrors) { $errors.Add("${name}: $problem") }
    $artifacts.Add(@{name=$name; suite=$suite; platform=$platform; upstreamSuiteStatus=$status; reportFiles=$files.Count; diagnosticFiles=$diagnosticFiles; errors=@($artifactErrors)})
}
# Unrecognised result artifacts cannot acquire an invented platform or suite.
try {
$directories = if (Test-Path -LiteralPath $ResultsPath -PathType Container) { @(Get-ChildItem -LiteralPath $ResultsPath -Directory -ErrorAction Stop) } else { @() }
foreach ($directory in $directories) {
    if (!$mapping.Contains($directory.Name) -and @(Get-ChildItem -LiteralPath $directory.FullName -Filter '*-report*.json' -Recurse -File).Count) {
        $errors.Add("Unknown benchmark artifact: $($directory.Name)")
    }
}
} catch { $errors.Add("Input enumeration failed: $($_.Exception.Message)") }
if (!$rows.Count) { $errors.Add('No raw-measurement-validated report rows') }
$summary = [ordered]@{
    generatedUtc=[DateTimeOffset]::UtcNow.ToString('O'); workflowSourceRevision=$SourceRevision
    evidenceValid=($errors.Count -eq 0); reportRows=$rows.Count; artifacts=@($artifacts); errors=@($errors); cases=@($rows)
    scope='Per-report raw measurement validation only. HotPath status is aggregate matrix status; individual platform execution status is unknown. Nonempty trace presence is not trace validity. Rows may repeat across exporters. No expected-population, functional, profiling-completeness, regression, improvement or NativeAOT certification. Statistics retain original within-run uncertainty; no pooled average or matched comparison is inferred.'
}
$summary | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath "$Output/performance-summary.json" -Encoding utf8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Performance evidence report')
$lines.Add('')
$lines.Add("Supplied workflow revision (not independently verified binary provenance): $SourceRevision")
$lines.Add("Raw-measurement-validated report rows: $($rows.Count). This is not a unique executed-case count.")
$lines.Add('')
foreach ($artifact in $artifacts) { $lines.Add("- $($artifact.name): aggregate suite status $($artifact.upstreamSuiteStatus); $($artifact.reportFiles) report files; $($artifact.errors.Count) evidence errors.") }
$lines.Add('')
foreach ($problem in $errors) { $lines.Add("- Evidence refused: $problem") }
$lines.Add('')
$lines.Add($summary.scope)
$lines.Add('Per-case jobs, parameters, nanosecond statistics, allocations, host environment and report hashes are retained in performance-summary.json. Missing uncertainty is not invented. No optimization percentage or competitive advantage is asserted.')
$lines | Set-Content -LiteralPath "$Output/performance-report.md" -Encoding utf8
if ($errors.Count) { exit 1 }
