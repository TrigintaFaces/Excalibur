[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $output) { throw 'Control evidence directory must be new.' }
[void][IO.Directory]::CreateDirectory($output)
'<Project />' | Set-Content (Join-Path $output 'Directory.Build.props')
'<Project><PropertyGroup><IsTestingPlatformApplication>false</IsTestingPlatformApplication></PropertyGroup></Project>' |
    Set-Content (Join-Path $output 'Directory.Build.targets')
$project = Join-Path $output 'RoutingFixture.csproj'
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
'@ | Set-Content $project
@'
using Xunit;
namespace Excalibur.RoutingFixture;
[Trait("Category", "Integration")]
public abstract class IntegrationBase { }
public sealed class InheritedIntegrationCase : IntegrationBase
{
    [Fact] public void IsDiscovered() { Assert.Equal(2, System.Math.Abs(-2)); }
}
'@ | Set-Content (Join-Path $output 'Cases.cs')
& dotnet restore $project *> (Join-Path $output 'restore.log')
if ($LASTEXITCODE -ne 0) { throw 'Routing fixture restore failed.' }
& dotnet build $project -c Release --no-restore -warnaserror *> (Join-Path $output 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Routing fixture build failed.' }
$hostPath = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$link = [IO.File]::ResolveLinkTarget($hostPath, $true)
if ($null -ne $link) { $hostPath = $link.FullName }
$sdkVersion = (Get-Content (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
$sdk = Join-Path ([IO.Path]::GetDirectoryName($hostPath)) "sdk/$sdkVersion"
$platform = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$context = @{
    candidateSha = (& git -C $repo rev-parse HEAD).Trim(); runId = 'local-control'; runAttempt = '1'
    job = 'routing-control'; shard = 'fixture'; provider = 'none'; targetFramework = 'net10.0'
    targetPlatform = $platform
    os = $(if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'macOS' })
}
$contextPath = Join-Path $output 'context.json'
$context | ConvertTo-Json | Set-Content $contextPath
foreach ($label in @('required', 'excluded')) {
    $expression = if ($label -eq 'required') { '(Category=Integration|Category=EndToEnd)' } else { 'Category=Unit' }
    $settings = '<RunSettings><RunConfiguration><TargetPlatform>' + $platform + '</TargetPlatform><TestCaseFilter>' +
        $expression + '</TestCaseFilter></RunConfiguration><xUnit><PreEnumerateTheories>true</PreEnumerateTheories></xUnit></RunSettings>'
    $settingsPath = Join-Path $output "$label.runsettings"
    $settings | Set-Content $settingsPath
    & (Join-Path $PSScriptRoot 'discover-required-tests.ps1') -AssemblyPath (Join-Path $output 'bin/Release/net10.0/RoutingFixture.dll') `
        -EffectiveRunSettings $settingsPath -SdkDirectory $sdk -ContextPath $contextPath -OutputPath (Join-Path $output "$label.json")
}
$required = Join-Path $output 'required.json'
$excluded = Join-Path $output 'excluded.json'
if ((Get-Content $required -Raw | ConvertFrom-Json).discoveredCount -ne 1) { throw 'Inherited Integration trait was not discovered.' }
$checker = Join-Path $PSScriptRoot 'integration-route-audit.py'
& python $checker --required $required --admitted $required *> (Join-Path $output 'valid.log')
if ($LASTEXITCODE -ne 0) { throw 'Valid route was refused.' }
& python $checker --required $required --admitted *> (Join-Path $output 'missing-route.log')
if ($LASTEXITCODE -ne 1) { throw 'Missing route was not rejected.' }
& python $checker --required $required --admitted $excluded *> (Join-Path $output 'excluded-case.log')
if ($LASTEXITCODE -ne 1) { throw 'Route excluding inherited Integration case was not rejected.' }
Write-Host 'PASS: compiled inherited Integration trait; valid route accepted; missing and excluding routes rejected.'
