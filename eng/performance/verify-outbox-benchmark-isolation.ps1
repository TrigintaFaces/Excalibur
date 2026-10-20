# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BenchmarkBundle,
    [Parameter(Mandatory)][string] $ArtifactsPath
)
$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path -LiteralPath $BenchmarkBundle).Path
$output = [IO.Path]::GetFullPath($ArtifactsPath)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/OutboxBenchmarkIsolationControls.cs" -Destination "$output/Program.cs"
@'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><RunAnalyzers>false</RunAnalyzers><NuGetAudit>false</NuGetAudit></PropertyGroup>
 <ItemGroup><Compile Include="Program.cs"/><FrameworkReference Include="Microsoft.AspNetCore.App"/></ItemGroup>
</Project>
'@ | Set-Content "$output/OutboxIsolation.csproj"
& dotnet build "$output/OutboxIsolation.csproj" -c Release -m:1 -nr:false -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false *> "$output/build.log"
if ($LASTEXITCODE -ne 0) { throw "Control build failed; see $output/build.log" }
$dll = "$output/bin/Release/net10.0/OutboxIsolation.dll"
$stdout = "$output/controls.json"
$stderr = "$output/controls.stderr.log"
$start = [Diagnostics.ProcessStartInfo]::new('dotnet')
$start.ArgumentList.Add($dll)
$start.ArgumentList.Add($bundle)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = [Diagnostics.Process]::Start($start)
$outTask = $process.StandardOutput.ReadToEndAsync()
$errTask = $process.StandardError.ReadToEndAsync()
$timer = [Diagnostics.Stopwatch]::StartNew()
$peakPrivate = 0L
$refusal = $null
try {
    while (-not $process.WaitForExit(100)) {
        $process.Refresh()
        $peakPrivate = [Math]::Max($peakPrivate, $process.PrivateMemorySize64)
        if ($timer.Elapsed.TotalSeconds -gt 120 -or $peakPrivate -gt 512MB) {
            $refusal = 'Bounded controls exceeded 120 seconds or sampled 512MiB private memory. Heavy benchmarks must remain stopped.'
            $process.Kill($true)
            break
        }
    }
}
finally {
    if (-not $process.HasExited) { $process.Kill($true) }
    $process.WaitForExit()
    $outTask.GetAwaiter().GetResult() | Set-Content $stdout
    $errTask.GetAwaiter().GetResult() | Set-Content $stderr
    [pscustomobject]@{ exitCode=$process.ExitCode; refusal=$refusal; elapsedSeconds=$timer.Elapsed.TotalSeconds; peakObservedPrivateBytes=$peakPrivate; sampledLimit=$true; deadlineSeconds=120; privateLimitBytes=512MB } |
        ConvertTo-Json | Set-Content "$output/process-receipt.json"
}
if ($refusal) { throw $refusal }
if ($process.ExitCode -ne 0) { throw "Controls failed; see $stderr" }
$result = Get-Content $stdout -Raw | ConvertFrom-Json
if (-not $result.passed -or $result.cycles -ne 1280) { throw 'Incomplete control population.' }
Write-Output "PASS: 1,280 isolated workload cycles; bounded counts, payloads, statuses, cleanup and memory. Evidence: $output"
