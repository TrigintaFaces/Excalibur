# Copyright (c) 2026 The Excalibur Project

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [string]$ArtifactsPath = "benchmarks/runs/BenchmarkDotNet.Artifacts",

    [Parameter(Mandatory = $false)]
    [switch]$NoBuild,

    [Parameter(Mandatory = $false)]
    [switch]$NoRestore,

    [Parameter(Mandatory = $false)]
    [switch]$ComparativeOnly,

    [Parameter(Mandatory = $false)]
    [switch]$DiagnosticsOnly,

    [Parameter(Mandatory = $false)]
    [switch]$CiSmoke,

    [Parameter(Mandatory = $false)]
    [switch]$ContinueOnError,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseFrameworkLogs,

    [Parameter(Mandatory = $false)]
    [string[]]$Classes,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfile,

    [Parameter(Mandatory = $false)]
    [string]$RuntimeProfilesPath = "eng/runtime-profiles.json",

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 20)]
    [int]$RepeatCount = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SafeCommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $false)]
        [string[]]$Arguments
    )

    try {
        $output = & $FilePath @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        if ($output -is [System.Array]) {
            return ($output -join [Environment]::NewLine).Trim()
        }

        return "$output".Trim()
    }
    catch {
        return $null
    }
}

function Get-BenchmarkEnvironmentMetadata {
    param(
        [Parameter(Mandatory = $false)]
        [string]$RuntimeProfileName,

        [Parameter(Mandatory = $false)]
        [hashtable]$RuntimeProfileVariables
    )

    $commitSha = Get-SafeCommandOutput -FilePath "git" -Arguments @("rev-parse", "HEAD")
    $branchName = Get-SafeCommandOutput -FilePath "git" -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
    $dotnetVersion = Get-SafeCommandOutput -FilePath "dotnet" -Arguments @("--version")

    return [pscustomobject]@{
        machineName             = [Environment]::MachineName
        osDescription           = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        osArchitecture          = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture     = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        frameworkDescription    = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
        processorCount          = [Environment]::ProcessorCount
        isServerGc              = [System.Runtime.GCSettings]::IsServerGC
        gcLatencyMode           = [System.Runtime.GCSettings]::LatencyMode.ToString()
        dotnetVersion           = $dotnetVersion
        commitSha               = $commitSha
        branch                  = $branchName
        isCi                    = [string]::Equals($env:GITHUB_ACTIONS, "true", [System.StringComparison]::OrdinalIgnoreCase)
        dotnetTieredPgo         = $env:DOTNET_TieredPGO
        dotnetReadyToRun        = $env:DOTNET_ReadyToRun
        dotnetGcServer          = $env:DOTNET_gcServer
        dotnetTcQuickJit        = $env:DOTNET_TC_QuickJit
        dotnetTcQuickJitForLoops= $env:DOTNET_TC_QuickJitForLoops
        comPlusTieredPgo        = $env:COMPlus_TieredPGO
        comPlusReadyToRun       = $env:COMPlus_ReadyToRun
        comPlusGcServer         = $env:COMPlus_gcServer
        runtimeProfile          = if ([string]::IsNullOrWhiteSpace($RuntimeProfileName)) { "none" } else { $RuntimeProfileName }
        runtimeProfileVariables = if ($RuntimeProfileVariables) { $RuntimeProfileVariables } else { @{} }
    }
}

function Get-RuntimeProfileVariables {
    param(
        [Parameter(Mandatory = $false)]
        [string]$ProfileName,

        [Parameter(Mandatory = $true)]
        [string]$ProfilesFilePath
    )

    if ([string]::IsNullOrWhiteSpace($ProfileName)) {
        return @{}
    }

    if (-not (Test-Path $ProfilesFilePath)) {
        throw "Runtime profiles file not found: $ProfilesFilePath"
    }

    $profilesRoot = Get-Content -Path $ProfilesFilePath -Raw | ConvertFrom-Json -AsHashtable
    if (-not $profilesRoot.ContainsKey("profiles")) {
        throw "Runtime profiles file is missing the top-level 'profiles' object: $ProfilesFilePath"
    }

    $profiles = $profilesRoot["profiles"]
    if (-not $profiles.ContainsKey($ProfileName)) {
        $availableProfiles = @($profiles.Keys) -join ", "
        throw "Runtime profile '$ProfileName' was not found in $ProfilesFilePath. Available profiles: $availableProfiles"
    }

    $selectedProfile = $profiles[$ProfileName]
    if ($selectedProfile -isnot [hashtable]) {
        throw "Runtime profile '$ProfileName' is not a valid key/value map in $ProfilesFilePath"
    }

    return $selectedProfile
}

if ($ComparativeOnly -and $DiagnosticsOnly) {
    throw "Use only one of -ComparativeOnly or -DiagnosticsOnly."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj"
$artifactsFullPath = Join-Path $repoRoot $ArtifactsPath
$resultsPath = Join-Path $artifactsFullPath "results"

if (-not (Test-Path $projectPath)) {
    throw "Benchmark project not found: $projectPath"
}

if (-not (Test-Path $artifactsFullPath)) {
    New-Item -ItemType Directory -Force -Path $artifactsFullPath | Out-Null
}

if (-not (Test-Path $resultsPath)) {
    New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
}

$comparativeClasses = @(
    "MediatRComparisonBenchmarks",
    "WolverineInProcessComparisonBenchmarks",
    "MassTransitMediatorComparisonBenchmarks",
    "TransportQueueParityComparisonBenchmarks",
    "WolverineComparisonBenchmarks",
    "MassTransitComparisonBenchmarks",
    "PipelineComparisonBenchmarks",
    "StartupComparisonBenchmarks",
    "RoutingFirstParityBenchmarks",
    "DispatchThroughputBenchmarks"
)

$diagnosticClasses = @(
    "DispatchHotPathBreakdownBenchmarks",
    "MiddlewareCostCurveBenchmarks",
    "MetricsLoggingOverheadBenchmarks",
    "HandlerResolutionBenchmarks",
    "HandlerFanOutBenchmarks",
    "TransportAdapterPhaseBenchmarks",
    "FailurePathBenchmarks",
    "LongRunAllocationGcBenchmarks",
    "CancellationCostBenchmarks",
    "RetryPolicyMicroBenchmarks",
    "DispatchContextCostBenchmarks",
    "AllocationHotspotBenchmarks",
    "ConcurrencyContentionBenchmarks",
    "FanOutColdDecompositionBenchmarks",
    "FanOutBehaviorMatrixBenchmarks",
    "TransportConcurrencyBreakdownBenchmarks",
    "ActivationStrategyBenchmarks",
    "HandlerInvokerPathBenchmarks"
)

$ciSmokeClasses = @(
    "MediatRComparisonBenchmarks",
    "WolverineInProcessComparisonBenchmarks",
    "MassTransitMediatorComparisonBenchmarks",
    "TransportQueueParityComparisonBenchmarks",
    "RoutingFirstParityBenchmarks",
    "DispatchHotPathBreakdownBenchmarks",
    "HandlerInvokerPathBenchmarks"
)

$selectedClasses = if ($Classes -and @($Classes).Count -gt 0) {
    $Classes
}
elseif ($CiSmoke) {
    $ciSmokeClasses
}
elseif ($ComparativeOnly) {
    $comparativeClasses
}
elseif ($DiagnosticsOnly) {
    $diagnosticClasses
}
else {
    $comparativeClasses + $diagnosticClasses
}

$normalizedSelectedClasses = @()
foreach ($classSelection in @($selectedClasses)) {
    foreach ($className in "$classSelection".Split(",")) {
        $trimmed = $className.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            $normalizedSelectedClasses += $trimmed
        }
    }
}

$selectedClasses = $normalizedSelectedClasses

$exporters = @("csv", "markdown", "html", "json")
$matrixStartUtc = [DateTimeOffset]::UtcNow
$matrixStart = Get-Date
$summary = @()
$failures = @()

$runtimeProfilesFullPath = if ([System.IO.Path]::IsPathRooted($RuntimeProfilesPath)) {
    $RuntimeProfilesPath
}
else {
    Join-Path $repoRoot $RuntimeProfilesPath
}
$runtimeProfileVariables = Get-RuntimeProfileVariables -ProfileName $RuntimeProfile -ProfilesFilePath $runtimeProfilesFullPath
$runtimeProfileOriginalEnv = @{}
foreach ($envVar in $runtimeProfileVariables.Keys) {
    $runtimeProfileOriginalEnv[$envVar] = [Environment]::GetEnvironmentVariable($envVar)
    [Environment]::SetEnvironmentVariable($envVar, "$($runtimeProfileVariables[$envVar])")
}

$quietLogEnvVars = @(
    "Logging__LogLevel__Default",
    "Logging__LogLevel__Microsoft",
    "Logging__LogLevel__Wolverine",
    "Logging__LogLevel__MassTransit",
    "Logging__LogLevel__Excalibur"
)

$originalEnv = @{}
if (-not $VerboseFrameworkLogs) {
    foreach ($envVar in $quietLogEnvVars) {
        $originalEnv[$envVar] = [Environment]::GetEnvironmentVariable($envVar)
        [Environment]::SetEnvironmentVariable($envVar, "Warning")
    }
}

$environmentMetadata = Get-BenchmarkEnvironmentMetadata -RuntimeProfileName $RuntimeProfile -RuntimeProfileVariables $runtimeProfileVariables

Write-Host "Benchmark matrix root: $repoRoot" -ForegroundColor Cyan
Write-Host "Benchmark project: $projectPath" -ForegroundColor Cyan
Write-Host "Artifacts path: $artifactsFullPath" -ForegroundColor Cyan
Write-Host "Selected classes ($(@($selectedClasses).Count)): $($selectedClasses -join ', ')" -ForegroundColor Cyan
Write-Host "Repeat count: $RepeatCount" -ForegroundColor Cyan
Write-Host "Quiet framework logs: $(-not $VerboseFrameworkLogs)" -ForegroundColor Cyan
Write-Host "Runtime profile: $(if ([string]::IsNullOrWhiteSpace($RuntimeProfile)) { 'none' } else { $RuntimeProfile })" -ForegroundColor Cyan
if ($runtimeProfileVariables.Count -gt 0) {
    Write-Host "Runtime profile variables: $(($runtimeProfileVariables.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }) -join ', ')" -ForegroundColor Cyan
}

Push-Location $repoRoot
try {
    foreach ($className in $selectedClasses) {
        for ($repeatIndex = 1; $repeatIndex -le $RepeatCount; $repeatIndex++) {
            $classStart = Get-Date
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $logFile = Join-Path $resultsPath ("run-{0}-r{1}-{2}.log" -f $className, $repeatIndex, $timestamp)

            Write-Host ""
            Write-Host "=== $className (run $repeatIndex/$RepeatCount) ===" -ForegroundColor Yellow

            $arguments = @(
                "run",
                "--project", $projectPath,
                "--configuration", $Configuration
            )

            if ($NoBuild) {
                $arguments += "--no-build"
            }

            if ($NoRestore) {
                $arguments += "--no-restore"
            }

            $arguments += @(
                "--",
                "--filter", "*$className*",
                "--exporters"
            )
            $arguments += $exporters
            $arguments += @("--artifacts", $artifactsFullPath)

            & dotnet @arguments *> $logFile
            $exitCode = $LASTEXITCODE
            $classDuration = (Get-Date) - $classStart

            $githubReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report-github.md" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1
            $csvReport = Get-ChildItem $resultsPath -Filter "Excalibur.Dispatch.Benchmarks*.$className-report.csv" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1

            $rowCount = 0
            if ($csvReport) {
                $rowCount = @(Import-Csv $csvReport.FullName).Count
            }

            $reportFound = $null -ne $githubReport
            $reportPath = if ($githubReport) { $githubReport.FullName } else { "" }
            $csvReportPath = if ($csvReport) { $csvReport.FullName } else { "" }

            # Snapshot reports per repeat so variance can be computed across runs.
            if ($csvReport) {
                $csvSnapshotPath = Join-Path $resultsPath ("{0}.run{1}.{2}.csv" -f [System.IO.Path]::GetFileNameWithoutExtension($csvReport.Name), $repeatIndex, $timestamp)
                Copy-Item -Path $csvReport.FullName -Destination $csvSnapshotPath -Force
                $csvReportPath = $csvSnapshotPath
            }

            if ($githubReport) {
                $githubSnapshotPath = Join-Path $resultsPath ("{0}.run{1}.{2}.md" -f [System.IO.Path]::GetFileNameWithoutExtension($githubReport.Name), $repeatIndex, $timestamp)
                Copy-Item -Path $githubReport.FullName -Destination $githubSnapshotPath -Force
                $reportPath = $githubSnapshotPath
            }

            $summary += [pscustomobject]@{
                ClassName       = $className
                Run             = $repeatIndex
                ExitCode        = $exitCode
                DurationSeconds = [math]::Round($classDuration.TotalSeconds, 1)
                BenchmarkRows   = $rowCount
                ReportFound     = $reportFound
                ReportPath      = $reportPath
                CsvReportPath   = $csvReportPath
                LogPath         = $logFile
            }

            if ($exitCode -ne 0 -or -not $reportFound) {
                $failures += "$className#run$repeatIndex"
                Write-Host "FAILED $className (run $repeatIndex/$RepeatCount, exit=$exitCode, reportFound=$reportFound)" -ForegroundColor Red
                Write-Host "Log: $logFile" -ForegroundColor Red
                if (-not $ContinueOnError) {
                    break
                }
            }
            else {
                Write-Host "OK $className (run $repeatIndex/$RepeatCount, $rowCount rows, $([math]::Round($classDuration.TotalSeconds, 1))s)" -ForegroundColor Green
            }
        }

        if (@($failures).Count -gt 0 -and -not $ContinueOnError) {
            break
        }
    }
}
finally {
    Pop-Location

    if (-not $VerboseFrameworkLogs) {
        foreach ($envVar in $quietLogEnvVars) {
            [Environment]::SetEnvironmentVariable($envVar, $originalEnv[$envVar])
        }
    }

    foreach ($envVar in $runtimeProfileVariables.Keys) {
        [Environment]::SetEnvironmentVariable($envVar, $runtimeProfileOriginalEnv[$envVar])
    }
}

$matrixEnd = Get-Date
$matrixDuration = $matrixEnd - $matrixStart
$totalRows = ($summary | Measure-Object -Property BenchmarkRows -Sum).Sum
if ($null -eq $totalRows) {
    $totalRows = 0
}

$summaryTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryJsonPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.json" -f $summaryTimestamp)
$summaryMdPath = Join-Path $resultsPath ("benchmark-matrix-summary-{0}.md" -f $summaryTimestamp)

$summaryPayload = [pscustomobject]@{
    startedUtc       = $matrixStartUtc.ToString("O")
    endedUtc         = [DateTimeOffset]::UtcNow.ToString("O")
    duration         = [string]$matrixDuration
    configuration    = $Configuration
    repeatCount      = $RepeatCount
    artifactsPath    = $artifactsFullPath
    resultsPath      = $resultsPath
    environment      = $environmentMetadata
    selectedClasses  = $selectedClasses
    totalBenchmarks  = $totalRows
    failures         = $failures
    classResults     = $summary
}

$summaryPayload | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryJsonPath -Encoding UTF8

$markdown = @()
$markdown += "# Benchmark Matrix Summary"
$markdown += ""
$markdown += "- Started (UTC): $($summaryPayload.startedUtc)"
$markdown += "- Ended (UTC): $($summaryPayload.endedUtc)"
$markdown += "- Duration: $($summaryPayload.duration)"
$markdown += "- Configuration: $Configuration"
$markdown += "- Repeat count: $RepeatCount"
$markdown += "- Commit SHA: $($environmentMetadata.commitSha)"
$markdown += "- .NET: $($environmentMetadata.dotnetVersion)"
$markdown += "- OS: $($environmentMetadata.osDescription)"
$markdown += "- Server GC: $($environmentMetadata.isServerGc)"
$markdown += "- Runtime profile: $($environmentMetadata.runtimeProfile)"
$markdown += "- Total benchmark rows: $totalRows"
$markdown += "- Failures: $(if (@($failures).Count -eq 0) { 'none' } else { $failures -join ', ' })"
$markdown += ""
$markdown += "| Class | Run | Exit | Rows | Seconds | Report | Log |"
$markdown += "|-------|-----|------|------|---------|--------|-----|"
foreach ($result in $summary) {
    $reportCell = if ($result.ReportFound) { "yes" } else { "no" }
    $markdown += "| $($result.ClassName) | $($result.Run) | $($result.ExitCode) | $($result.BenchmarkRows) | $($result.DurationSeconds) | $reportCell | $($result.LogPath) |"
}
$markdown -join "`n" | Set-Content -Path $summaryMdPath -Encoding UTF8

Write-Host ""
Write-Host "Benchmark matrix complete." -ForegroundColor Cyan
Write-Host "Duration: $matrixDuration" -ForegroundColor Cyan
Write-Host "Total benchmark rows: $totalRows" -ForegroundColor Cyan
Write-Host "Summary JSON: $summaryJsonPath" -ForegroundColor Cyan
Write-Host "Summary Markdown: $summaryMdPath" -ForegroundColor Cyan

if (@($failures).Count -gt 0) {
    Write-Error "Benchmark matrix failed for class(es): $($failures -join ', ')"
    exit 2
}

exit 0
