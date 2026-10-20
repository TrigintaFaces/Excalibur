# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
<# .SYNOPSIS
Verify evaluated sample warning policy and real trim/dynamic-code diagnostics from planted calls.
This does not certify native execution or the AOT compatibility of any dependency graph.
#>
[CmdletBinding()]
param([string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'eng/package-composition.functions.ps1')
if (-not $RunDirectory) { $RunDirectory = Join-Path $repo "artifacts/sample-aot-diagnostic-policy/attempt-$([Guid]::NewGuid().ToString('N'))" }
$run = [IO.Path]::GetFullPath($RunDirectory)
if (Test-Path -LiteralPath $run) { throw 'Refusing to overwrite existing verification evidence.' }
[void][IO.Directory]::CreateDirectory($run)
$native = Join-Path $repo 'samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj'
$ordinary = @(Get-ChildItem -LiteralPath (Join-Path $repo 'samples/01-getting-started/DispatchOnly') -Filter '*.csproj')
if ($ordinary.Count -ne 1) { throw 'Ordinary sample project selection is ambiguous.' }
$props = Join-Path $repo 'samples/Directory.Build.props'
$inputs = @($props, $native, $ordinary[0].FullName, $PSCommandPath)
$before = @($inputs | ForEach-Object { @{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash} })
$failures = [Collections.Generic.List[string]]::new()
$checks = @()
foreach ($entry in @(@{name='ordinary';path=$ordinary[0].FullName}, @{name='native';path=$native})) {
    $log = Join-Path $run $entry.name
    Invoke-PackageCommand dotnet @('msbuild',$entry.path,'-nologo','-getProperty:NoWarn') -Log $log -WorkingDirectory $repo -TimeoutSeconds 120
    $noWarn = (Get-Content -LiteralPath "$log.stdout.log" -Raw).Trim()
    $suppressed = @($noWarn.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })
    $visible = -not ($suppressed -contains 'IL2026' -or $suppressed -contains 'IL3050')
    $checks += @{name=$entry.name;project=$entry.path;noWarn=$noWarn;bothVisible=$visible}
    if (-not $visible) { $failures.Add("$($entry.name): trim or dynamic-code diagnostics are suppressed") }
}
$fixture = Join-Path $run 'fixture'
[void][IO.Directory]::CreateDirectory($fixture)
$import = [Security.SecurityElement]::Escape($props)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$import" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer><EnableAotAnalyzer>true</EnableAotAnalyzer>
    <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $fixture 'Policy.csproj') -Encoding utf8
@'
using System.Diagnostics.CodeAnalysis;
internal static class Program {
    static void Main() { TrimSensitive(); DynamicSensitive(); }
    [RequiresUnreferencedCode("Planted policy witness, not production functionality.")]
    static void TrimSensitive() { }
    [RequiresDynamicCode("Planted policy witness, not production functionality.")]
    static void DynamicSensitive() { }
}
'@ | Set-Content -LiteralPath (Join-Path $fixture 'Program.cs') -Encoding utf8
$build = Join-Path $run 'fixture-build'
Invoke-PackageCommand dotnet @('build',(Join-Path $fixture 'Policy.csproj'),'-c','Release','--disable-build-servers','-m:1',
    '-p:UseSharedCompilation=false','-p:ImportDirectoryBuildProps=false','-p:ImportDirectoryBuildTargets=false',
    '-p:BuildExamplesAndTests=true') -Log $build -WorkingDirectory $repo -TimeoutSeconds 300
$output = (Get-Content -LiteralPath "$build.stdout.log" -Raw) + (Get-Content -LiteralPath "$build.stderr.log" -Raw)
$seen = @('IL2026','IL3050') | Where-Object { $output -match "warning $_\b" }
foreach ($code in @('IL2026','IL3050')) { if ($seen -notcontains $code) { $failures.Add("Planted call did not emit $code") } }
$after = @($inputs | ForEach-Object { @{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash} })
if (($before | ConvertTo-Json -Compress) -cne ($after | ConvertTo-Json -Compress)) { $failures.Add('Verification inputs changed.') }
[ordered]@{status=$(if ($failures.Count) {'failed'} else {'passed'});inputs=$before;checks=$checks;diagnostics=$seen;failures=@($failures)} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
if ($failures.Count) { throw ($failures -join '; ') }
Write-Output 'PASS: both evaluated sample policies and both real analyzer diagnostic witnesses are visible.'
