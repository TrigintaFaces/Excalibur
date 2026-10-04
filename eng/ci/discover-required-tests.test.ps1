# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DotnetPath,
    [Parameter(Mandatory)][string]$SdkDirectory,
    [string]$PackageCache = $env:NUGET_PACKAGES
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Join-Path $repo ('artifacts/tools/required-discovery-control-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$dotnet = (Resolve-Path -LiteralPath $DotnetPath).Path
$sdk = (Resolve-Path -LiteralPath $SdkDirectory).Path
$shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
if (-not $PackageCache) { $PackageCache = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
'<Project />' | Set-Content (Join-Path $root 'Directory.Build.props')
# Match the repository's VSTest choice in Directory.Build.targets after package imports.
'<Project><PropertyGroup><IsTestingPlatformApplication>false</IsTestingPlatformApplication></PropertyGroup></Project>' | Set-Content (Join-Path $root 'Directory.Build.targets')
$project = Join-Path $root 'CensusFixture.csproj'
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <NuGetAudit>false</NuGetAudit><RunAnalyzers>false</RunAnalyzers>
    <RunSettingsFilePath>$(MSBuildThisFileDirectory)fixture.runsettings</RunSettingsFilePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
'@ | Set-Content $project
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'discover-required-tests.fixture.cs.txt') -Destination (Join-Path $root 'Cases.cs')
$baseSettings = '<RunSettings><RunConfiguration><TargetPlatform>' +
    [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant() +
    '</TargetPlatform></RunConfiguration><xUnit><PreEnumerateTheories>true</PreEnumerateTheories></xUnit></RunSettings>'
$baseSettings | Set-Content (Join-Path $root 'fixture.runsettings')
& $dotnet restore $project --configfile (Join-Path $repo 'NuGet.Config') --packages $PackageCache --source $PackageCache -p:NuGetAudit=false *> (Join-Path $root 'restore.log')
if ($LASTEXITCODE -ne 0) { throw "Fixture restore failed; see $root/restore.log" }
& $dotnet build $project -c Release --no-restore -p:BuildExamplesAndTests=true -warnaserror *> (Join-Path $root 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Fixture build failed; see $root/build.log" }
$assembly = Join-Path $root 'bin/Release/net10.0/CensusFixture.dll'
$context = @{
    candidateSha = (git -C $repo rev-parse HEAD); runId = 'local-control'; runAttempt = '1'
    job = 'adapter-control'; shard = 'fixture'; provider = 'none'; targetFramework = 'net10.0'
    targetPlatform = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    os = $(if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'macOS' })
    source = [IO.Path]::GetRelativePath($repo,$project).Replace('\','/'); sourceSha256 = (Get-FileHash $project).Hash
}
$oldFault = $env:CI02_DISCOVERY_FAULT
$oldMutation = $env:CI02_MUTATE_FILE
$caseCount = 0
function Invoke-Control([string]$Name, [string]$Filter, [int]$Count, [bool]$Pass = $true, [string]$Fault = '') {
    $script:caseCount++
    $directory = Join-Path $root $Name
    [void][IO.Directory]::CreateDirectory($directory)
    [xml]$settings = $baseSettings
    $node = $settings.CreateElement('TestCaseFilter'); $node.InnerText = $Filter
    [void]$settings.RunSettings.RunConfiguration.AppendChild($node)
    $settingsPath = Join-Path $directory 'effective.runsettings'
    $settings.Save($settingsPath)
    $context.filter = $Filter
    $contextPath = Join-Path $directory 'context.json'
    $context | ConvertTo-Json | Set-Content $contextPath
    $censusPath = Join-Path $directory 'discovery.json'
    $env:CI02_DISCOVERY_FAULT = $Fault
    $env:CI02_MUTATE_FILE = $settingsPath
    $timeout = if ($Fault -eq 'hang') { 5 } else { 60 }
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    & $shell -NoProfile -File (Join-Path $PSScriptRoot 'discover-required-tests.ps1') -AssemblyPath $assembly `
        -EffectiveRunSettings $settingsPath -SdkDirectory $sdk -ContextPath $contextPath -OutputPath $censusPath `
        -TimeoutSeconds $timeout *> (Join-Path $directory 'discovery.log')
    $exit = $LASTEXITCODE
    if ($Fault -eq 'hang' -and $elapsed.Elapsed.TotalSeconds -gt 15) { throw 'Discovery timeout did not enforce a hard bound.' }
    if (($exit -eq 0) -ne $Pass) { throw "${Name}: expected success=$Pass, got exit=$exit; see $directory/discovery.log" }
    if (-not $Pass -and (Test-Path -LiteralPath $censusPath)) { throw "${Name}: refused discovery published authoritative evidence." }
    if ($Pass) {
        $census = Get-Content $censusPath -Raw | ConvertFrom-Json
        if ($census.discoveredCount -ne $Count) { throw "${Name}: expected $Count identities, got $($census.discoveredCount)." }
        if ($Count -gt 0) {
            & $dotnet test $project -c Release --no-build --no-restore --settings $settingsPath `
                --logger 'trx;LogFileName=execution.trx' --results-directory $directory *> (Join-Path $directory 'execution.log')
            if ($LASTEXITCODE -ne 0) { throw "${Name}: execution failed." }
            [xml]$trx = Get-Content (Join-Path $directory 'execution.trx') -Raw
            $actual = @($trx.TestRun.Results.UnitTestResult)
            $difference = @(Compare-Object @($census.tests.id | Sort-Object) @($actual.testId | Sort-Object))
            if ($difference.Count -gt 0 -or @($actual | Where-Object outcome -ne 'Passed').Count -gt 0) {
                throw "${Name}: independent IDs and passing execution disagree."
            }
        }
    }
    Write-Host "${Name}: verified (exit=$exit)."
    return $directory
}
try {
    $null = Invoke-Control 'serializable-and-duplicate-display' 'Group=valid' 4
    $null = Invoke-Control 'compound-and-negative' '(Group=valid|Group=never)&Group!=never&FullyQualifiedName!~Unstable' 4
    $null = Invoke-Control 'multi-valued-trait' 'Group=shared' 1
    $null = Invoke-Control 'zero-match' 'FullyQualifiedName=NoSuchTest' 0
    $null = Invoke-Control 'identical-inline-data' 'FullyQualifiedName~Duplicated' 1
    $null = Invoke-Control 'deferred-nonserializable' 'FullyQualifiedName~Deferred' 0 $false
    $null = Invoke-Control 'invalid-filter' 'Group=' 0 $false
    $null = Invoke-Control 'discovery-error' 'FullyQualifiedName~DiscoveryFault' 0 $false 'throw'
    $null = Invoke-Control 'discovery-timeout' 'FullyQualifiedName~DiscoveryFault' 0 $false 'hang'
    $null = Invoke-Control 'input-mutation' 'FullyQualifiedName~DiscoveryFault' 0 $false 'mutate'
    $env:CI02_DISCOVERY_FAULT = ''
    $context.filter = 'FullyQualifiedName~Unstable'
    $contextPath = Join-Path $root 'unstable-context.json'
    $context | ConvertTo-Json | Set-Content $contextPath
    $commandArgs = @('test',$project,'--configuration','Release','--no-build','--nologo','--filter',$context.filter,
        '--logger','trx;LogFilePrefix=unstable')
    try {
        & (Join-Path $PSScriptRoot 'invoke-required-tests.ps1') -TestArguments $commandArgs -ContextPath $contextPath `
            -EvidenceDirectory (Join-Path $root 'unstable') -DotnetPath $dotnet -SdkDirectory $sdk *> (Join-Path $root 'unstable.log')
        throw 'Unstable row identity was accepted.'
    } catch {
        if ($_ -notmatch 'Required test invocation evidence failed') { throw }
        if ((Get-Content (Join-Path $root 'unstable.log') -Raw) -notmatch 'Independent test identity mismatch') {
            throw 'Unstable-row control failed for an unrelated reason.'
        }
    }
    $caseCount++
    Write-Host "PASS: $caseCount real adapter controls. Evidence: $root"
} finally { $env:CI02_DISCOVERY_FAULT = $oldFault; $env:CI02_MUTATE_FILE = $oldMutation }

# EXIT EXPLICITLY. Every must-fail control above invokes a native command that legitimately exits
# non-zero, and a PowerShell script inherits the LAST native exit code -- GitHub's pwsh wrapper then
# ends the step with `exit $LASTEXITCODE`. So this suite printed
# "PASS: 11 real adapter controls" and failed its step anyway, which took out NINE unit shards
# across three operating systems on a self-test that had actually passed.
#
# The leaked value is not even stable: measured 1 in CI and 2 locally, from whichever control ran
# last. On this gate family 2 means REFUSE, so the leak could present as a three-state verdict this
# script never rendered.
#
# A real failure still exits non-zero: every control throws, and $ErrorActionPreference='Stop'
# makes a throw terminate the script before this line is reached.
exit 0
