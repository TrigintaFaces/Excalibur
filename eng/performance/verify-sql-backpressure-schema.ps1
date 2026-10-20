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
$source = Join-Path $PSScriptRoot 'SqlBackpressureControls.cs'
$copiedSource = Join-Path $output 'Program.cs'
[IO.File]::WriteAllBytes($copiedSource, [IO.File]::ReadAllBytes($source))
$project = Join-Path $output 'SqlBackpressureControls.csproj'
$projectText = @'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><RunAnalyzers>false</RunAnalyzers><NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
 <ItemGroup><Compile Include="Program.cs"/><FrameworkReference Include="Microsoft.AspNetCore.App"/></ItemGroup>
</Project>
'@
$sqlClientPath = Join-Path $bundle 'Microsoft.Data.SqlClient.dll'
if (-not (Test-Path -LiteralPath $sqlClientPath -PathType Leaf)) { throw 'Actual bundled Microsoft.Data.SqlClient required.' }
$escapedSqlClient = [Security.SecurityElement]::Escape($sqlClientPath)
$projectText = $projectText.Replace('</Project>', ('<ItemGroup><Reference Include="Microsoft.Data.SqlClient"><HintPath>' + $escapedSqlClient + '</HintPath><Private>false</Private></Reference></ItemGroup></Project>'))
$outboxSql = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql'))
$eventSql = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql'))
Assert-NoReparse $outboxSql; Assert-NoReparse $eventSql
$outboxSqlSha = (Get-FileHash -LiteralPath $outboxSql).Hash
$eventSqlSha = (Get-FileHash -LiteralPath $eventSql).Hash
if ($outboxSqlSha -cne '105A02547E19A9805546E04E7920AFDA3BD2B3436B78329CC4F92EC178E02224') { throw 'Reviewed canonical outbox SQL changed.' }
$ownerToken = [guid]::NewGuid().ToString('N')
[IO.File]::WriteAllText($project, $projectText, $utf8)
function Invoke-Bounded([string] $Name, [string[]] $Arguments, [string] $Executable = 'dotnet', [ValidateRange(1,600)][int] $DeadlineSeconds = 600) {
    $stdout = Join-Path $output ($Name + '.stdout.log')
    $stderr = Join-Path $output ($Name + '.stderr.log')
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
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
    $drainCancellation = [Threading.CancellationTokenSource]::new(($DeadlineSeconds * 1000))
    try {
        $outStream = [IO.File]::Open($stdout, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $errStream = [IO.File]::Open($stderr, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $process = [Diagnostics.Process]::Start($start)
        $outTask = $process.StandardOutput.BaseStream.CopyToAsync($outStream, 81920, $drainCancellation.Token)
        $errTask = $process.StandardError.BaseStream.CopyToAsync($errStream, 81920, $drainCancellation.Token)
        # Root exit is insufficient: a descendant can retain either inherited pipe.
        # Root exit and BOTH drains share one deadline, including failure cleanup.
        while (-not ($process.HasExited -and $outTask.IsCompleted -and $errTask.IsCompleted)) {
            $remaining = [Math]::Max(0, ($DeadlineSeconds * 1000) - $timer.ElapsedMilliseconds)
            if ($remaining -eq 0) { $refusal = 'Shared root-exit/pipe-drain deadline exceeded.'; break }
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
            $remaining = [Math]::Max(0, ($DeadlineSeconds * 1000) - $timer.ElapsedMilliseconds)
            if ($remaining -gt 0) { $null = $process.WaitForExit([int]$remaining) }
        }
        while (($null -ne $outTask -and -not $outTask.IsCompleted) -or ($null -ne $errTask -and -not $errTask.IsCompleted)) {
            $remaining = [Math]::Max(0, ($DeadlineSeconds * 1000) - $timer.ElapsedMilliseconds)
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
            sampledLimits=$true; deadlineSeconds=$DeadlineSeconds; rootPrivateLimitBytes=512MB; rootWorkingSetLimitBytes=512MB; outputLimitBytes=64MB;
            descendantMemoryBoundEstablished=$false; descendantTerminationEstablished=$false; timerCallbackQuiescenceVerified=$false; performanceAdmission=$false }
        [IO.File]::WriteAllText((Join-Path $output ($Name + '-process-receipt.json')), ($receipt | ConvertTo-Json -Depth 6), $utf8)
    }
    if ($refusal) { throw $refusal }
    if ($process.ExitCode -ne 0) { throw "$Name failed; see $stderr" }
}
function Select-SqlBackpressureReport([string]$Text, [string]$ExpectedOwnerToken) {
    $candidates=@($Text -split '\r?\n' | Where-Object {$_.TrimStart().StartsWith('{',[StringComparison]::Ordinal)})
    if ($candidates.Count -ne 1) { throw 'Exactly one standalone JSON report line required; missing or ambiguous output refused.' }
    try { $report=$candidates[0] | ConvertFrom-Json -ErrorAction Stop }
    catch { throw 'Standalone SQL control report JSON is malformed.' }
    if ($report.schema -cne 'sql-backpressure-schema-controls-r1' -or $report.ownerToken -cne $ExpectedOwnerToken -or
        $report.passed -isnot [bool] -or -not $report.passed) { throw 'SQL control report schema, owner token or success flag differs.' }
    return $report
}
function Test-SqlBackpressureReportParser {
    $token='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
    $valid=@{schema='sql-backpressure-schema-controls-r1';ownerToken=$token;passed=$true}|ConvertTo-Json -Compress
    $wrongSchema=@{schema='other';ownerToken=$token;passed=$true}|ConvertTo-Json -Compress
    $wrongOwner=@{schema='sql-backpressure-schema-controls-r1';ownerToken='bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';passed=$true}|ConvertTo-Json -Compress
    $failed=@{schema='sql-backpressure-schema-controls-r1';ownerToken=$token;passed=$false}|ConvertTo-Json -Compress
    $cases=@(
        @{name='AcceptLoggedReport';text=("[testcontainers] bounded log`n"+$valid);accept=$true},
        @{name='AcceptWhitespaceReport';text=("`n[log] text { not a report`n  "+$valid+"`n");accept=$true},
        @{name='RejectMissing';text='[log] no report';accept=$false},
        @{name='RejectMultiple';text=($valid+"`n"+$valid);accept=$false},
        @{name='RejectMalformed';text='{broken';accept=$false},
        @{name='RejectWrongSchema';text=$wrongSchema;accept=$false},
        @{name='RejectWrongOwner';text=$wrongOwner;accept=$false},
        @{name='RejectFailedReport';text=$failed;accept=$false})
    $results=@(foreach($case in $cases){$accepted=$true;try{$null=Select-SqlBackpressureReport $case.text $token}catch{$accepted=$false}
        if ($accepted -ne $case.accept) { throw ('Pure report parser control failed: '+$case.name) }
        @{name=$case.name;expectedAccept=$case.accept;actualAccept=$accepted;passed=$true}})
    return @{schema='sql-backpressure-report-parser-controls-r1';passed=$true;controls=$results;positiveControls=2;rejectingControls=6;databaseExecuted=$false}
}
$parserControls=Test-SqlBackpressureReportParser
[IO.File]::WriteAllText((Join-Path $output 'report-parser-controls-r1.json'), ($parserControls|ConvertTo-Json -Depth 5), $utf8)
$launch = [ordered]@{ schema='sql-backpressure-schema-launch-r1'; utc=[DateTimeOffset]::UtcNow.ToString('o'); bundle=$bundle;
    sourcePath=$source; sourceSha256=(Get-FileHash -LiteralPath $source).Hash; copiedSourceSha256=(Get-FileHash -LiteralPath $copiedSource).Hash;
    wrapperSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash; projectSha256=(Get-FileHash -LiteralPath $project).Hash;
    outboxSqlSha256=$outboxSqlSha; eventSqlSha256=$eventSqlSha; ownerToken=$ownerToken; expectedConfigurations=4;
    expectedVerifiedMethodCalls=24; expectedRejectingControls=40; nativeAotVerified=$false; timedBenchmark=$false; existingDatabaseTargetSupplied=$false }
[IO.File]::WriteAllText((Join-Path $output 'launch.json'), ($launch | ConvertTo-Json -Depth 6), $utf8)
Invoke-Bounded 'build' @('build', $project, '-c', 'Release', '-m:1', '-nr:false', '-p:BuildExamplesAndTests=true', '-p:UseSharedCompilation=false') -DeadlineSeconds 120
$dll = Join-Path $output 'bin/Release/net10.0/SqlBackpressureControls.dll'
$controlsCompleted=$false
$cleanup=[ordered]@{schema='sql-backpressure-owned-cleanup-r1';ownerToken=$ownerToken;normalFixtureDisposalReported=$false;fallbackRequested=$false;knownOwnedContainerAttempts=@();allContainersRemovedVerified=$false;unknownOwnershipContainersTouched=$false;hostServicesChanged=$false;error=$null}
try {
    Invoke-Bounded 'controls' @($dll, $bundle, $outboxSql, $eventSql, $ownerToken) -DeadlineSeconds 600
    $result = Select-SqlBackpressureReport ([IO.File]::ReadAllText((Join-Path $output 'controls.stdout.log'))) $ownerToken
    if (-not $result.passed -or $result.schema -cne 'sql-backpressure-schema-controls-r1' -or $result.configurations -ne 4 -or
        $result.scenarios.Count -ne 4 -or $result.verifiedMethodCalls -ne 24 -or $result.rejectingNegativeControls -ne 40 -or
        -not $result.runtimeBundleUnchanged -or $result.nativeAotVerified -or $result.timingOrThroughputMeasured -or
        $result.ownerToken -cne $ownerToken -or $result.ownedContainers.Count -ne 4 -or
        @($result.ownedContainers | Where-Object {-not $_.disposalCompleted}).Count -ne 0) { throw 'Incomplete real-SQL control population.' }
    if ((Get-FileHash -LiteralPath $source).Hash -cne $launch.sourceSha256 -or
        (Get-FileHash -LiteralPath $copiedSource).Hash -cne $launch.copiedSourceSha256 -or
        (Get-FileHash -LiteralPath $PSCommandPath).Hash -cne $launch.wrapperSha256 -or
        (Get-FileHash -LiteralPath $outboxSql).Hash -cne $outboxSqlSha -or
        (Get-FileHash -LiteralPath $eventSql).Hash -cne $eventSqlSha) { throw 'Reviewed sources changed during verification.' }
    $controlsCompleted=$true; $cleanup.normalFixtureDisposalReported=$true
} finally {
    if (-not $controlsCompleted) {
        $cleanup.fallbackRequested=$true
        try {
            $ownedPath=Join-Path $output 'owned-containers.json'; Assert-NoReparse $ownedPath
            if (Test-Path -LiteralPath $ownedPath) {
                if ((Get-Item -LiteralPath $ownedPath).Length -gt 65536) { throw 'Ownership receipt exceeds64 KiB.' }
                $owned=Get-Content -LiteralPath $ownedPath -Raw|ConvertFrom-Json
                if ($owned.schema -cne 'sql-backpressure-owned-containers-r1' -or $owned.ownerToken -cne $ownerToken -or $owned.containers.Count -gt 4 -or
                    @($owned.containers.id|Sort-Object -Unique).Count -ne $owned.containers.Count) { throw 'Ownership receipt mismatch; no cleanup by name or scan.' }
                foreach ($container in $owned.containers) {
                    if ($container.id -cnotmatch '^[a-f0-9]{64}$') { throw 'Invalid owned container identity; no cleanup attempted.' }
                    $attempt=[ordered]@{id=$container.id;command='docker rm --force exact captured container ID';completed=$false;error=$null}
                    try { Invoke-Bounded ('owned-cleanup-'+$container.id.Substring(0,12)) @('rm','--force',$container.id) -Executable 'docker' -DeadlineSeconds 30; $attempt.completed=$true }
                    catch { $attempt.error=$_.Exception.Message }
                    $cleanup.knownOwnedContainerAttempts+=@($attempt)
                }
            }
        } catch { $cleanup.error=$_.Exception.Message }
    }
    [IO.File]::WriteAllText((Join-Path $output 'owned-cleanup-receipt.json'), ($cleanup|ConvertTo-Json -Depth 6), $utf8)
}
$completed = [ordered]@{ schema='sql-backpressure-schema-verification-r1'; passed=$true; configurations=4; verifiedMethodCalls=24; rejectingNegativeControls=40;
    sourceSha256=$launch.sourceSha256; wrapperSha256=$launch.wrapperSha256; helperSha256=(Get-FileHash -LiteralPath $dll).Hash;
    helperPdbSha256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($dll,'.pdb'))).Hash;
    controlsSha256=(Get-FileHash -LiteralPath (Join-Path $output 'controls.stdout.log')).Hash;
    buildReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'build-process-receipt.json')).Hash;
    controlsReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'controls-process-receipt.json')).Hash;
    ownedCleanupReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $output 'owned-cleanup-receipt.json')).Hash;
    outboxSqlSha256=$outboxSqlSha; eventSqlSha256=$eventSqlSha; runtimeBundleUnchanged=$true; nativeAotVerified=$false;
    performanceAdmission=$false; existingDatabaseTargetSupplied=$false; allContainersRemovedVerified=$false }
[IO.File]::WriteAllText((Join-Path $output 'verification.json'), ($completed | ConvertTo-Json -Depth 6), $utf8)
Write-Output "PASS: Four fresh owned SQL configurations, 24 original staging calls and 40 rejecting controls. Evidence: $output"