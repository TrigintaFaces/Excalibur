#!/usr/bin/env pwsh
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param([string]$EvidenceDirectory = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/package-composition.functions.ps1"
$repository = Split-Path -Parent $PSScriptRoot
if (-not $EvidenceDirectory) {
    $EvidenceDirectory = Join-Path $repository ('artifacts/test-warning-policy/' + [guid]::NewGuid().ToString('N'))
}
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$failures = [Collections.Generic.List[string]]::new()
$controls = [Collections.Generic.List[object]]::new()
$evaluations = [Collections.Generic.List[object]]::new()
$runCompleted = $false
$expectedEvaluations = 0
$props = Join-Path $repository 'tests/Directory.Build.props'
$propsHash = (Get-FileHash -LiteralPath $props -Algorithm SHA256).Hash

function Get-WarningPolicyInputs {
    $selectors = @('tests/*.csproj', 'tests/**/*.csproj', 'tests/*.props', 'tests/**/*.props',
        'tests/*.targets', 'tests/**/*.targets', 'Directory.Build.props', 'Directory.Build.targets',
        'Directory.Packages.props', 'global.json', 'NuGet.config', 'nuget.config',
        'eng/ci/quality-policy.json', 'eng/test-warning-policy.ps1', 'eng/package-composition.functions.ps1')
    $paths = @(& git -C $repository ls-files --cached --others --exclude-standard -- @selectors | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) { throw 'Cannot enumerate warning-policy source inputs.' }
    $rows = @($paths | ForEach-Object {
        $inputPath = Join-Path $repository $_
        @{ path=$_; sha256=$(if (Test-Path -LiteralPath $inputPath -PathType Leaf) {
            (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
        } else { 'deleted' }) }
    })
    $text = ($rows | ForEach-Object { "$($_.path)`t$($_.sha256)" }) -join "`n"
    return @{ inputs=$rows; sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))) }
}
$sourceBefore = Get-WarningPolicyInputs
$sourceBefore | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'source-before.json') -Encoding utf8
$policy = Get-Content (Join-Path $repository 'eng/ci/quality-policy.json') -Raw | ConvertFrom-Json
if ($policy.build.warningsAsErrors -ne $true) { throw 'The declared build policy must treat compiler warnings as errors.' }
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('xlb-warning-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$project = Join-Path $sandbox 'WarningPolicyProbe.csproj'
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath $project -Encoding utf8
@'
#if WARNING_CONTROL
#warning UNSUPPRESSED_TEST_POLICY_PROBE
#endif
public static class WarningPolicyProbe { public static int Value => 1; }
'@ | Set-Content -LiteralPath (Join-Path $sandbox 'Probe.cs') -Encoding utf8
Copy-Item -LiteralPath $project, (Join-Path $sandbox 'Probe.cs'), $props -Destination $evidence

function Test-NativeControl([string]$Name, [string[]]$ExtraArguments, [bool]$ShouldFail,
    [string]$FailureCode = 'CS1030', [string]$FailureMarker = 'UNSUPPRESSED_TEST_POLICY_PROBE') {
    $log = Join-Path $evidence $Name
    # Each control changes compiler policy; compile it afresh rather than trusting prior outputs.
    $arguments = @('build', $project, '-c', 'Release', '--no-incremental', '--nologo', '-v:minimal',
        "-p:DirectoryBuildPropsPath=$props", '-p:IsTestProject=false', '-p:EnableBannedApiAnalyzers=false') + $ExtraArguments
    try { Invoke-PackageCommand $dotnet $arguments $log $repository -TimeoutSeconds 180 }
    catch {
        if (-not (Test-Path -LiteralPath "$log.receipt.json")) { throw }
    }
    $receipt = Get-Content -LiteralPath "$log.receipt.json" -Raw | ConvertFrom-Json
    $output = (Get-Content -LiteralPath "$log.stdout.log" -Raw) + (Get-Content -LiteralPath "$log.stderr.log" -Raw)
    $passed = if ($ShouldFail) {
        -not $receipt.timedOut -and $null -ne $receipt.exitCode -and $receipt.exitCode -ne 0 -and
        $output -match "error $FailureCode" -and $output -match [regex]::Escape($FailureMarker)
    } else { -not $receipt.timedOut -and $receipt.exitCode -eq 0 }
    if ($Name -eq 'explicit-cli-override') {
        $passed = $passed -and $output -match 'warning CS1030' -and $output -match 'UNSUPPRESSED_TEST_POLICY_PROBE'
    }
    if ($Name -eq 'existing-documentation-suppression') {
        $documentation = Join-Path $sandbox 'bin/Release/net10.0/WarningPolicyProbe.xml'
        $passed = $passed -and (Test-Path -LiteralPath $documentation) -and
            (Get-Content -LiteralPath $documentation -Raw) -match '<name>WarningPolicyProbe</name>'
    }
    $controls.Add(@{ name=$Name; passed=$passed; nativeExit=$receipt.exitCode; expectedFailure=$ShouldFail })
    if (-not $passed) { $failures.Add("Native control $Name did not produce its expected compiler outcome.") }
    Write-Host "$Name : passed=$passed nativeExit=$($receipt.exitCode)"
}

try {
    Test-NativeControl 'healthy-default' @() $false
    Test-NativeControl 'unsuppressed-warning-refused' @('-p:DefineConstants=WARNING_CONTROL') $true
    Test-NativeControl 'explicit-cli-override' @('-p:DefineConstants=WARNING_CONTROL', '-p:TreatWarningsAsErrors=false') $false
    Test-NativeControl 'existing-documentation-suppression' @('-p:GenerateDocumentationFile=true') $false
    Test-NativeControl 'documentation-warning-unmasked' @('-p:GenerateDocumentationFile=true', '-p:NoWarn=CS1030') $true 'CS1591' 'WarningPolicyProbe'

    $projects = @(& git -C $repository ls-files --cached --others --exclude-standard -- 'tests/*.csproj' 'tests/**/*.csproj' | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0 -or $projects.Count -eq 0) { throw 'Cannot enumerate the current test-project roster.' }
    $expectedEvaluations = 2 * $projects.Count
    $projects | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'project-roster.json') -Encoding utf8
    foreach ($configuration in @('Debug', 'Release')) {
        foreach ($relative in $projects) {
            $path = Join-Path $repository $relative
            $name = ($relative -replace '[^A-Za-z0-9_.-]', '_') + '-' + $configuration
            $log = Join-Path $evidence $name
            Invoke-PackageCommand $dotnet @('msbuild', $path, '-nologo',
                '-getProperty:IsTestProject,TreatWarningsAsErrors,NoWarn,EnforceCodeStyleInBuild',
                "-p:Configuration=$configuration") $log $repository -TimeoutSeconds 90
            $properties = (Get-Content -LiteralPath "$log.stdout.log" -Raw | ConvertFrom-Json).Properties
            $passed = $properties.TreatWarningsAsErrors -eq 'true'
            $evaluations.Add(@{ project=$relative; configuration=$configuration; properties=$properties;
                projectSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; passed=$passed })
            if (-not $passed) { $failures.Add("$relative ($configuration) evaluates TreatWarningsAsErrors=$($properties.TreatWarningsAsErrors).") }
        }
    }
    if ((Get-FileHash -LiteralPath $props -Algorithm SHA256).Hash -ne $propsHash) {
        $failures.Add('The test warning policy changed during verification.')
    }
    $sourceAfter = Get-WarningPolicyInputs
    $sourceAfter | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'source-after.json') -Encoding utf8
    if ($sourceAfter.sha256 -ne $sourceBefore.sha256) { $failures.Add('Warning-policy selected source inputs or roster changed during verification.') }
    $runCompleted = $true
}
finally {
    $complete = $runCompleted -and $failures.Count -eq 0 -and $evaluations.Count -eq $expectedEvaluations -and
        $expectedEvaluations -gt 0 -and $controls.Count -eq 5 -and @($controls | Where-Object { -not $_.passed }).Count -eq 0
    if (-not $complete -and $failures.Count -eq 0) { $failures.Add('Warning-policy verification did not finish every required control and project/configuration evaluation.') }
    @{ status=$(if ($complete) { 'passed' } else { 'incomplete' });
        runCompleted=$runCompleted; expectedEvaluations=$expectedEvaluations; sourceSha256=$sourceBefore.sha256;
        testPropsSha256=$propsHash; controls=@($controls); evaluations=@($evaluations); failures=@($failures) } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'verdict.json') -Encoding utf8
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedSandbox = [IO.Path]::GetFullPath($sandbox)
    if (-not $resolvedSandbox.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Get-Item -LiteralPath $resolvedSandbox).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing warning-probe cleanup outside the temporary root or through a junction.'
    }
    Remove-Item -LiteralPath $resolvedSandbox -Recurse -Force
}
if ($failures.Count) { throw ($failures -join "`n") }
Write-Host "PASS: $($controls.Count) native controls and $($evaluations.Count) effective project/configuration evaluations. Evidence: $evidence"
