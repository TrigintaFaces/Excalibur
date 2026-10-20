# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BenchmarkBundle,
    [Parameter(Mandatory)][string] $ArtifactsPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Caller holds the serialized build/test slots. One Release helper build, then
# untimed controls; no benchmark or production retention setting is changed.
$bundle = (Resolve-Path -LiteralPath $BenchmarkBundle).Path
$output = [IO.Path]::GetFullPath($ArtifactsPath)
if (-not (Test-Path -LiteralPath (Join-Path $bundle 'Excalibur.Dispatch.Benchmarks.dll') -PathType Leaf)) { throw 'Compiled runtime bundle required.' }
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory.' }
if ($output.StartsWith($bundle.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $bundle.StartsWith($output.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $output.Equals($bundle, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence and runtime must be separate trees.' }
function Assert-NoReparse([string] $Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse path refused.' }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
}
Assert-NoReparse $bundle
Assert-NoReparse $output
New-Item -ItemType Directory -Path $output | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$source = Join-Path $PSScriptRoot 'OutboxPollingControls.cs'
$copiedSource = Join-Path $output 'Program.cs'
[IO.File]::WriteAllBytes($copiedSource, [IO.File]::ReadAllBytes($source))
$project = Join-Path $output 'OutboxPollingControls.csproj'
$projectText = @'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><RunAnalyzers>false</RunAnalyzers><NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
 <ItemGroup><Compile Include="Program.cs"/><FrameworkReference Include="Microsoft.AspNetCore.App"/></ItemGroup>
</Project>
'@
$bdnPath = Join-Path $bundle 'BenchmarkDotNet.dll'
if (-not (Test-Path -LiteralPath $bdnPath -PathType Leaf)) { throw 'Actual bundled BenchmarkDotNet required.' }
$escapedBdn = [Security.SecurityElement]::Escape($bdnPath)
$projectText = $projectText.Replace('</Project>', ('<ItemGroup><Reference Include="BenchmarkDotNet"><HintPath>' + $escapedBdn + '</HintPath><Private>false</Private></Reference></ItemGroup></Project>'))
[IO.File]::WriteAllText($project, $projectText, $utf8)
function Invoke-Bounded([string] $Name, [string[]] $Arguments) {
    $stdout = Join-Path $output ($Name + '.stdout.log')
    $stderr = Join-Path $output ($Name + '.stderr.log')
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.WorkingDirectory = $output
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = $null; $outStream = $null; $errStream = $null
    $outTask = $null; $errTask = $null
    $peakPrivate = 0L; $peakRss = 0L; $refusal = $null
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $drainCancellation = [Threading.CancellationTokenSource]::new(120000)
    try {
        $outStream = [IO.File]::Open($stdout, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $errStream = [IO.File]::Open($stderr, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $process = [Diagnostics.Process]::Start($start)
        $outTask = $process.StandardOutput.BaseStream.CopyToAsync($outStream, 81920, $drainCancellation.Token)
        $errTask = $process.StandardError.BaseStream.CopyToAsync($errStream, 81920, $drainCancellation.Token)
        # Root exit is insufficient: a descendant can retain either inherited pipe.
        # Root exit and BOTH drains share one deadline, including failure cleanup.
        while (-not ($process.HasExited -and $outTask.IsCompleted -and $errTask.IsCompleted)) {
            $remaining = [Math]::Max(0, 120000 - $timer.ElapsedMilliseconds)
            if ($remaining -eq 0) { $refusal = 'Shared 120 second root-exit/pipe-drain deadline exceeded.'; break }
            if (-not $process.HasExited) {
                $process.Refresh()
                $peakPrivate = [Math]::Max($peakPrivate, $process.PrivateMemorySize64)
                $peakRss = [Math]::Max($peakRss, $process.WorkingSet64)
            }
            if ($peakPrivate -gt 512MB -or $peakRss -gt 512MB -or $outStream.Length + $errStream.Length -gt 64MB) {
                $refusal = 'Build/controls exceeded sampled 512 MiB root memory or 64 MiB output guard.'; break
            }
            $slice = [int][Math]::Min(100, $remaining)
            if (-not $process.HasExited) { $null = $process.WaitForExit($slice) }
            else { [Threading.Thread]::Sleep($slice) }
        }
        foreach ($task in @($outTask, $errTask)) {
            if ($task.IsFaulted -or $task.IsCanceled) { $refusal = 'Output drain faulted or was cancelled; diagnostics may be partial.' }
        }
    }
    catch { if ($null -eq $refusal) { $refusal = $_.Exception.Message } }
    finally {
        # No parameterless WaitForExit or blocking task-result retrieval, even after kill.
        if ($null -ne $process -and -not $process.HasExited) {
            try { $process.Kill($true) } catch { if ($null -eq $refusal) { $refusal = 'Best-effort termination failed: ' + $_.Exception.Message } }
            $remaining = [Math]::Max(0, 120000 - $timer.ElapsedMilliseconds)
            if ($remaining -gt 0) { $null = $process.WaitForExit([int]$remaining) }
        }
        while (($null -ne $outTask -and -not $outTask.IsCompleted) -or ($null -ne $errTask -and -not $errTask.IsCompleted)) {
            $remaining = [Math]::Max(0, 120000 - $timer.ElapsedMilliseconds)
            if ($remaining -eq 0) { break }
            [Threading.Thread]::Sleep([int][Math]::Min(50, $remaining))
        }
        $outComplete = $null -ne $outTask -and $outTask.Status -eq [Threading.Tasks.TaskStatus]::RanToCompletion
        $errComplete = $null -ne $errTask -and $errTask.Status -eq [Threading.Tasks.TaskStatus]::RanToCompletion
        $terminal = $null -ne $process -and $process.HasExited
        if ((-not $terminal -or -not $outComplete -or -not $errComplete) -and $null -eq $refusal) { $refusal = 'Root exit or output drain incomplete at shared deadline; partial diagnostics retained.' }
        $drainCancellation.Cancel()
        if ($null -ne $process) {
            $process.StandardOutput.Close()
            $process.StandardError.Close()
        }
        if ($null -ne $outStream) { $outStream.Dispose() }
        if ($null -ne $errStream) { $errStream.Dispose() }
        $drainCancellation.Dispose()
        $receipt = [ordered]@{ phase=$Name; terminal=$terminal; exitCode=$(if($terminal){$process.ExitCode}else{$null});
            refusal=$refusal; elapsedSeconds=$timer.Elapsed.TotalSeconds; peakObservedRootPrivateBytes=$peakPrivate; peakObservedRootWorkingSetBytes=$peakRss;
            stdoutDrainCompleted=$outComplete; stderrDrainCompleted=$errComplete; partialDiagnostics=(-not ($outComplete -and $errComplete));
            sampledLimits=$true; deadlineSeconds=120; rootPrivateLimitBytes=512MB; rootWorkingSetLimitBytes=512MB; outputLimitBytes=64MB;
            descendantMemoryBoundEstablished=$false; descendantTerminationEstablished=$false; timerCallbackQuiescenceVerified=$false; performanceAdmission=$false }
        [IO.File]::WriteAllText((Join-Path $output ($Name + '-process-receipt.json')), ($receipt | ConvertTo-Json -Depth 6), $utf8)
    }
    if ($refusal) { throw $refusal }
    if ($process.ExitCode -ne 0) { throw "$Name failed; see $stderr" }
}
$launch = [ordered]@{ schema='outbox-polling-isolation-launch-r1'; utc=[DateTimeOffset]::UtcNow.ToString('o'); bundle=$bundle;
    sourcePath=$source; sourceSha256=(Get-FileHash -LiteralPath $source).Hash; copiedSourceSha256=(Get-FileHash -LiteralPath $copiedSource).Hash;
    wrapperSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash; projectSha256=(Get-FileHash -LiteralPath $project).Hash;
    expectedScenarios=12; expectedVerifiedCycles=48; expectedWarmupCycles=12; expectedRejectingNegativeControls=42; nativeAotVerified=$false; timedBenchmark=$false }
[IO.File]::WriteAllText((Join-Path $output 'launch.json'), ($launch | ConvertTo-Json -Depth 6), $utf8)
Invoke-Bounded 'build' @('build', $project, '-c', 'Release', '-m:1', '-nr:false', '-p:BuildExamplesAndTests=true', '-p:UseSharedCompilation=false')
$dll = Join-Path $output 'bin/Release/net10.0/OutboxPollingControls.dll'
Invoke-Bounded 'controls' @($dll, $bundle)
$result = Get-Content -LiteralPath (Join-Path $output 'controls.stdout.log') -Raw | ConvertFrom-Json
if (-not $result.passed -or $result.schema -cne 'outbox-polling-isolation-controls-r1' -or $result.scenarioCount -ne 12 -or
    $result.scenarios.Count -ne 12 -or $result.verifiedCycles -ne 48 -or $result.warmupCycles -ne 12 -or
    $result.rejectingNegativeControls -ne 42 -or -not $result.runtimeBundleUnchanged -or $result.nativeAotVerified -or $result.timingOrThroughputMeasured -or
    $result.universalMemoryBoundVerified) { throw 'Incomplete or overclaimed control population.' }
if ((Get-FileHash -LiteralPath $source).Hash -cne $launch.sourceSha256 -or
    (Get-FileHash -LiteralPath $copiedSource).Hash -cne $launch.copiedSourceSha256 -or
    (Get-FileHash -LiteralPath $PSCommandPath).Hash -cne $launch.wrapperSha256) { throw 'Verification source/wrapper changed during execution.' }
$completed = [ordered]@{ schema='outbox-polling-isolation-verification-r1'; passed=$true; scenarios=12; verifiedCycles=48; rejectingNegativeControls=42;
    sourceSha256=$launch.sourceSha256; wrapperSha256=$launch.wrapperSha256; helperSha256=(Get-FileHash -LiteralPath $dll).Hash;
    helperPdbSha256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($dll,'.pdb'))).Hash;
    controlsSha256=(Get-FileHash -LiteralPath (Join-Path $output 'controls.stdout.log')).Hash;
    buildReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'build-process-receipt.json')).Hash;
    controlsReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'controls-process-receipt.json')).Hash;
    runtimeBundleUnchanged=$true; nativeAotVerified=$false; performanceAdmission=$false; timerCallbackQuiescenceVerified=$false; universalMemoryBoundVerified=$false }
[IO.File]::WriteAllText((Join-Path $output 'verification.json'), ($completed | ConvertTo-Json -Depth 6), $utf8)
Write-Output "PASS: 12 outbox polling cases, 48 post-warmup cycles and 42 rejecting controls. Evidence: $output"
