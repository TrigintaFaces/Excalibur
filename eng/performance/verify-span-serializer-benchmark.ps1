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
if ($output.StartsWith($bundle.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be outside the immutable benchmark bundle.' }
New-Item -ItemType Directory -Path $output | Out-Null
function Get-AssetFence {
    @(Get-ChildItem -LiteralPath $bundle -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{path=[IO.Path]::GetRelativePath($bundle,$_.FullName);length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$beforeFence = Get-AssetFence
$beforeFence | ConvertTo-Json -Depth 5 | Set-Content "$output/bundle-before.json"
[pscustomobject]@{utc=[DateTimeOffset]::UtcNow.ToString('o');bundle=$bundle;
    benchmarkAssemblySha256=(Get-FileHash "$bundle/Excalibur.Dispatch.Benchmarks.dll").Hash;
    controlSourceSha256=(Get-FileHash "$PSScriptRoot/SpanSerializerBenchmarkControls.cs").Hash;
    wrapperSha256=(Get-FileHash $PSCommandPath).Hash;
    scope='Full-field serializer fixture; string/null metadata only; no measured latency or NativeAOT certification'} |
    ConvertTo-Json -Depth 5 | Set-Content "$output/identity.json"
Copy-Item -LiteralPath "$PSScriptRoot/SpanSerializerBenchmarkControls.cs" -Destination "$output/Program.cs"
@'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><RunAnalyzers>false</RunAnalyzers><NuGetAudit>false</NuGetAudit></PropertyGroup>
 <ItemGroup><Compile Include="Program.cs"/><FrameworkReference Include="Microsoft.AspNetCore.App"/></ItemGroup>
</Project>
'@ | Set-Content "$output/SpanSerializerIsolation.csproj"
[xml]$projectXml = Get-Content "$output/SpanSerializerIsolation.csproj"
$references = $projectXml.CreateElement('ItemGroup')
foreach ($name in @('MemoryPack.Core','Excalibur.Dispatch.Abstractions')) {
    $reference = $projectXml.CreateElement('Reference')
    $reference.SetAttribute('Include',$name)
    $hint = $projectXml.CreateElement('HintPath')
    $hint.InnerText = Join-Path $bundle "$name.dll"
    $null = $reference.AppendChild($hint)
    $null = $references.AppendChild($reference)
}
$null = $projectXml.Project.AppendChild($references)
$projectXml.Save("$output/SpanSerializerIsolation.csproj")
& dotnet build "$output/SpanSerializerIsolation.csproj" -c Release -m:1 -nr:false -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false *> "$output/build.log"
if ($LASTEXITCODE -ne 0) { throw "Control build failed; see $output/build.log" }
$dll = "$output/bin/Release/net10.0/SpanSerializerIsolation.dll"
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
$afterFence = Get-AssetFence
$afterFence | ConvertTo-Json -Depth 5 | Set-Content "$output/bundle-after.json"
$fenceEqual = (ConvertTo-Json -InputObject $beforeFence -Depth 5 -Compress) -ceq (ConvertTo-Json -InputObject $afterFence -Depth 5 -Compress)
[pscustomobject]@{matched=$fenceEqual;beforeCount=$beforeFence.Count;afterCount=$afterFence.Count} |
    ConvertTo-Json | Set-Content "$output/integrity-receipt.json"
if (-not $fenceEqual) { throw 'Benchmark bundle changed during diagnostic execution.' }
if ($refusal) { throw $refusal }
if ($process.ExitCode -ne 0) { throw "Controls failed; see $stderr" }
$result = Get-Content $stdout -Raw | ConvertFrom-Json
if (-not $result.passed -or $result.cycles -ne 128 -or $result.uniqueCases -ne 16 -or $result.repeats -ne 8 -or $result.negativeControls -ne 10 -or $result.memberChecks -ne 3 -or -not $result.negativeVerifierSelfCheck) { throw 'Incomplete control population.' }
Write-Output "PASS: 128 calls across sixteen exact serializer methods; all fields, metadata, malformed inputs, byte counts and cleanup verified. Evidence: $output"



