# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
<#
.SYNOPSIS
  Capture test identities independently, before execution, using the pinned VSTest adapter.
.DESCRIPTION
  The caller must use EffectiveRunSettings for execution too, including its TestCaseFilter.
  Completion, source coverage, diagnostics, and identity uniqueness are mandatory. Deferred
  theories are refused: their discovery identity does not establish their executed child set.
  This produces evidence, not a passing test verdict. The result gate must compare this census
  with this invocation's raw results and separately bind the invocation to the expected CI job.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AssemblyPath,
    [Parameter(Mandatory)][string]$EffectiveRunSettings,
    [Parameter(Mandatory)][string]$SdkDirectory,
    [Parameter(Mandatory)][string]$ContextPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(1,3600)][int]$TimeoutSeconds = 120,
    [switch]$SourcePresenceOnly,
    [string]$StartSignal,
    [switch]$Worker
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not ('Excalibur.CI.DiscoveryProcessJob' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'discover-required-tests.process.cs')
}
if ($Worker) {
    if (-not $StartSignal) { throw 'Discovery worker requires its parent containment handshake.' }
    $handshake = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $StartSignal)) {
        if ($handshake.Elapsed.TotalSeconds -gt 30) { throw 'Discovery parent did not authorize process start.' }
        Start-Sleep -Milliseconds 25
    }
}

# A separate process bounds discovery plus termination/output grace periods. Windows jobs
# contain descendants; on Unix, framework tree termination is best effort (orphaned,
# inaccessible or detached descendants can escape). Failure to drain output
# within the grace period refuses evidence instead of waiting indefinitely or signaling a
# numeric process group that may no longer belong to this invocation.
if (-not $Worker) {
    $finalOutput = [IO.Path]::GetFullPath($OutputPath)
    if (Test-Path -LiteralPath $finalOutput) { throw 'Refusing to overwrite prior discovery evidence.' }
    $stagedOutput = $finalOutput + '.pending-' + [guid]::NewGuid().ToString('N')
    $signal = $stagedOutput + '.start'
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($signal))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$PSCommandPath,'-Worker',
        '-AssemblyPath',$AssemblyPath,'-EffectiveRunSettings',$EffectiveRunSettings,
        '-SdkDirectory',$SdkDirectory,'-ContextPath',$ContextPath,'-OutputPath',$stagedOutput,'-StartSignal',$signal)) {
        $start.ArgumentList.Add($argument)
    }
    if ($SourcePresenceOnly) { $start.ArgumentList.Add('-SourcePresenceOnly') }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $job = $null
    $startedProcess = $false
    try {
        if ($IsWindows) { $job = [Excalibur.CI.DiscoveryProcessJob]::new() }
        [void]$process.Start()
        $startedProcess = $true
        if ($null -ne $job) { $job.Assign($process.Handle) }
        [IO.File]::WriteAllText($signal, 'start')
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            if ($null -ne $job) { $job.Dispose(); $job = $null }
            if (-not $process.HasExited) { $process.Kill($true) }
            if (-not $process.WaitForExit(5000)) { throw 'Discovery process failed to terminate after deadline.' }
            throw 'Discovery exceeded its deadline; owned-process cleanup was requested. No census is published.'
        }
        # Close descendant handles before reading to EOF; a detached test process can otherwise
        # keep these streams open indefinitely after the controller exits.
        if ($null -ne $job) { $job.Dispose(); $job = $null }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),5000)) {
            throw 'Discovery output streams did not close after process containment ended.'
        }
        Write-Host $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Discovery failed (exit $($process.ExitCode)): $errorOutput" }
        if ($errorOutput) { throw "Unexpected discovery stderr: $errorOutput" }
        $staged = Get-Content -LiteralPath $stagedOutput -Raw | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -cne $staged.assemblySha256 -or
            (Get-FileHash -LiteralPath $EffectiveRunSettings -Algorithm SHA256).Hash -cne $staged.runSettingsSha256) {
            throw 'Discovery inputs changed before evidence publication.'
        }
        $assemblyDirectory = [IO.Path]::GetDirectoryName((Resolve-Path -LiteralPath $AssemblyPath).Path)
        $currentBundle = @(Get-ChildItem -LiteralPath $assemblyDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath($assemblyDirectory,$_.FullName).Replace('\','/')
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
        if (($currentBundle | ConvertTo-Json -Compress -Depth 5) -cne ($staged.inputBundle | ConvertTo-Json -Compress -Depth 5)) {
            throw 'Discovery dependency/configuration/data bundle changed before publication.'
        }
    } finally {
        if ($null -ne $job) { $job.Dispose() }
        if ($startedProcess -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'Discovery worker cleanup exceeded its grace period.' }
        }
        $process.Dispose()
        if (Test-Path -LiteralPath $signal) { Remove-Item -LiteralPath $signal -Force }
    }
    # Publication is last, after cleanup. Same-directory, non-overwriting rename prevents a
    # timeout or teardown failure from leaving an authoritative-looking discovery manifest.
    if (Test-Path -LiteralPath "$stagedOutput.raw.jsonl") {
        [IO.File]::Move("$stagedOutput.raw.jsonl", "$finalOutput.raw.jsonl", $false)
    }
    [IO.File]::Move($stagedOutput, $finalOutput, $false)
    return
}

$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$settingsPath = (Resolve-Path -LiteralPath $EffectiveRunSettings).Path
$sdk = (Resolve-Path -LiteralPath $SdkDirectory).Path
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($field in @('candidateSha','runId','runAttempt','job','shard','provider','targetFramework','targetPlatform','os')) {
    if (-not $context.ContainsKey($field) -or [string]::IsNullOrWhiteSpace([string]$context[$field])) {
        throw "Missing invocation context '$field'."
    }
}
if ($context.candidateSha -cnotmatch '^[0-9a-f]{40}$') { throw 'candidateSha must be a full commit SHA.' }
$actualOS = if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } elseif ($IsMacOS) { 'macOS' } else { throw 'Unsupported runner OS.' }
if ($context.os -cne $actualOS) { throw 'Invocation OS differs from this runner.' }
if (Test-Path -LiteralPath $OutputPath) { throw "Refusing to overwrite prior discovery evidence: $OutputPath" }
$output = [IO.Path]::GetFullPath($OutputPath)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))

$xmlSettings = [Xml.XmlReaderSettings]::new()
$xmlSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$xmlSettings.XmlResolver = $null
$settingsBytes = [IO.File]::ReadAllBytes($settingsPath)
$settingsHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($settingsBytes))
$settingsStream = [IO.MemoryStream]::new($settingsBytes)
$reader = [Xml.XmlReader]::Create($settingsStream, $xmlSettings)
try {
    $settings = [Xml.XmlDocument]::new()
    $settings.XmlResolver = $null
    $settings.Load($reader)
} finally { $reader.Dispose(); $settingsStream.Dispose() }
if ($settings.DocumentElement.Name -cne 'RunSettings') { throw 'Expected RunSettings root.' }
if ($settings.SelectNodes('/RunSettings/xUnit/PreEnumerateTheories').Count -ne 1 -or
    $settings.SelectSingleNode('/RunSettings/xUnit/PreEnumerateTheories').InnerText -cne 'true') {
    throw 'The effective execution settings must explicitly pre-enumerate theories.'
}
if ($settings.SelectNodes('/RunSettings/RunConfiguration/TestCaseFilter').Count -gt 1) {
    throw 'Multiple effective TestCaseFilter values are ambiguous.'
}
$filterNode = $settings.SelectSingleNode('/RunSettings/RunConfiguration/TestCaseFilter')
$filter = if ($null -eq $filterNode) { '' } else { $filterNode.InnerText }
if ($SourcePresenceOnly -and $filter) { throw 'Source presence requires unfiltered discovery.' }
if ($settings.SelectNodes('/RunSettings/RunConfiguration/TargetPlatform').Count -ne 1 -or
    $settings.SelectSingleNode('/RunSettings/RunConfiguration/TargetPlatform').InnerText -cne $context.targetPlatform) {
    throw 'Effective execution settings must bind the planned target platform.'
}
function Get-InputBundle {
    # Include dependencies, adapter, runtime config, xUnit JSON and copied data fixtures.
    @(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($assembly)) -File -Recurse |
        Sort-Object FullName | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath([IO.Path]::GetDirectoryName($assembly), $_.FullName).Replace('\','/')
               sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
}
$inputBundle = Get-InputBundle
$runtimeConfig = [IO.Path]::ChangeExtension($assembly, '.runtimeconfig.json')
$runtime = Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json
if ($runtime.runtimeOptions.tfm -cne $context.targetFramework) { throw 'Assembly TFM differs from expected invocation.' }

# Use precisely the SDK selected by the caller; no global SDK fallback or package download.
$sdkRoot = [IO.Path]::GetFullPath((Join-Path $sdk '../..'))
$dotnet = Join-Path $sdkRoot $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'SDK host is missing.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pinnedSdk = (Get-Content (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
Push-Location $repoRoot
try { $actualSdk = & $dotnet --version; $sdkExit = $LASTEXITCODE } finally { Pop-Location }
if ($sdkExit -ne 0 -or $actualSdk -cne $pinnedSdk -or [IO.Path]::GetFileName($sdk) -cne $pinnedSdk) {
    throw 'Discovery must use the exact SDK pinned in global.json.'
}
$oldRoot = $env:DOTNET_ROOT
$oldPath = $env:PATH
$wrapper = $null
try {
    $env:DOTNET_ROOT = $sdkRoot
    $env:PATH = "$sdkRoot$([IO.Path]::PathSeparator)$oldPath"
    foreach ($file in @('Microsoft.VisualStudio.TestPlatform.ObjectModel.dll',
        'Microsoft.TestPlatform.CoreUtilities.dll','Microsoft.TestPlatform.PlatformAbstractions.dll',
        'Microsoft.TestPlatform.Utilities.dll','Microsoft.VisualStudio.TestPlatform.Common.dll',
        'Microsoft.TestPlatform.CommunicationUtilities.dll','Microsoft.TestPlatform.CrossPlatEngine.dll',
        'Microsoft.TestPlatform.VsTestConsole.TranslationLayer.dll','Newtonsoft.Json.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk $file))
    }
    if (-not ('Excalibur.CI.DiscoveryCensus' -as [type])) {
        # SDK TestPlatform targets net8; PowerShell supplies its net10 reference assemblies.
        # CS1701 is the framework-reference unification warning, not a test/build warning.
        Add-Type -CompilerOptions /nowarn:1701 -ReferencedAssemblies @(
            "$sdk/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll",
            "$sdk/Microsoft.TestPlatform.VsTestConsole.TranslationLayer.dll",
            "$PSHOME/ref/System.Runtime.dll", "$PSHOME/ref/System.Collections.dll") -TypeDefinition @'
using System;
using System.Collections.Generic;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
namespace Excalibur.CI {
    public sealed class DiscoveryCensus : ITestDiscoveryEventsHandler2 {
        public readonly List<TestCase> Tests = new List<TestCase>();
        public readonly List<string> Diagnostics = new List<string>();
        public readonly List<string> RawMessages = new List<string>();
        public DiscoveryCompleteEventArgs Completion;
        public int CompletionCount;
        public void HandleDiscoveredTests(IEnumerable<TestCase> tests) {
            if (tests != null) Tests.AddRange(tests);
        }
        public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs args, IEnumerable<TestCase> tests) {
            Completion = args; CompletionCount++; HandleDiscoveredTests(tests);
        }
        public void HandleRawMessage(string message) { RawMessages.Add(message); }
        public void HandleLogMessage(TestMessageLevel level, string message) {
            if (level != TestMessageLevel.Informational) Diagnostics.Add(level + ": " + message);
        }
    }
}
'@
    }
    $census = [Excalibur.CI.DiscoveryCensus]::new()
    $wrapper = [Microsoft.TestPlatform.VsTestConsole.TranslationLayer.VsTestConsoleWrapper]::new("$sdk/vstest.console.dll")
    $options = [Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.TestPlatformOptions]::new()
    # The pinned SDK ignores options.TestCaseFilter during discovery. The adapter honors the
    # effective RunConfiguration/TestCaseFilter XML (verified against adapter 4.0.0).
    $options.TestCaseFilter = $filter
    $assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    $started = [DateTimeOffset]::UtcNow
    try {
        $wrapper.DiscoverTests([string[]]@($assembly), $settings.OuterXml, $options, $census)
    } finally {
        [IO.File]::WriteAllLines("$output.raw.jsonl", $census.RawMessages)
    }
    $complete = $census.Completion
    if ($census.CompletionCount -ne 1 -or $null -eq $complete -or $complete.IsAborted) {
        throw 'Discovery did not complete exactly once without timeout or abort.'
    }
    foreach ($diagnostic in $census.Diagnostics) {
        $zeroMatch = 'Warning: No test matches the given testcase filter `' + $filter + '` in ' + $assembly
        if (-not ($filter -and $complete.TotalCount -eq 0 -and $diagnostic.Trim() -ceq $zeroMatch)) {
            throw "Discovery diagnostics: $($census.Diagnostics -join '; ')"
        }
    }
    if ($complete.TotalCount -ne $census.Tests.Count) { throw 'Discovery count and received identity set disagree.' }
    if (@($complete.FullyDiscoveredSources).Count -ne 1 -or
        [IO.Path]::GetFullPath($complete.FullyDiscoveredSources[0]) -ne $assembly -or
        @($complete.PartiallyDiscoveredSources).Count -gt 0 -or
        @($complete.SkippedDiscoveredSources).Count -gt 0 -or
        @($complete.NotDiscoveredSources).Count -gt 0) { throw 'Expected assembly was not fully discovered.' }
    if ($assemblyHash -ne (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -or
        $settingsHash -ne (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash) {
        throw 'Discovery inputs changed during enumeration.'
    }
    if (($inputBundle | ConvertTo-Json -Depth 5 -Compress) -cne ((Get-InputBundle) | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'Assembly input bundle changed during discovery.'
    }
    $ids = [Collections.Generic.HashSet[guid]]::new()
    $rows = @(foreach ($case in $census.Tests) {
        if (-not $ids.Add($case.Id) -or $case.Id -eq [guid]::Empty) { throw 'Duplicate or empty discovered test identity.' }
        if ([IO.Path]::GetFullPath($case.Source) -ne $assembly) { throw 'Discovered test belongs to unexpected source.' }
        $properties = @{}
        foreach ($property in $case.Properties) { $properties[$property.Id] = $case.GetPropertyValue($property) }
        if (-not $properties.ContainsKey('XunitTestCaseSerialization') -or
            -not $properties.ContainsKey('XunitTestCaseUniqueID')) { throw 'Unsupported adapter identity shape.' }
        $serialization = [string]$properties.XunitTestCaseSerialization
        $segments = $serialization.Split(':')
        if ($segments.Length -lt 3 -or $segments[0] -cne '-3') { throw 'Unsupported xUnit serialization format.' }
        $caseType = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($segments[1]))
        if (-not $SourcePresenceOnly -and $caseType -cne 'Xunit.v3.XunitTestCase,xunit.v3.core') {
            throw "Unsupported deferred or custom discovered case type '$caseType': $($case.DisplayName)."
        }
        @{
            id = $case.Id.ToString(); name = $case.DisplayName; fullyQualifiedName = $case.FullyQualifiedName
            xunitUniqueId = [string]$properties.XunitTestCaseUniqueID
            traits = @($case.Traits | ForEach-Object { @{ name = $_.Name; value = $_.Value } })
        }
    })
    $adapter = Join-Path ([IO.Path]::GetDirectoryName($assembly)) 'xunit.runner.visualstudio.testadapter.dll'
    $evidence = [ordered]@{
        schemaVersion = 1; purpose = $(if ($SourcePresenceOnly) { 'source-presence' } else { 'execution' })
        context = $context; startedUtc = $started.ToString('O'); completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        orchestratorArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        assembly = [IO.Path]::GetFileName($assembly); assemblySha256 = $assemblyHash
        adapterVersion = [Reflection.AssemblyName]::GetAssemblyName($adapter).Version.ToString()
        adapterSha256 = (Get-FileHash -LiteralPath $adapter -Algorithm SHA256).Hash
        sdk = [IO.Path]::GetFileName($sdk); runSettingsSha256 = $settingsHash; filter = $filter
        testPlatformSha256 = (Get-FileHash -LiteralPath "$sdk/Microsoft.VisualStudio.TestPlatform.ObjectModel.dll" -Algorithm SHA256).Hash
        inputBundle = $inputBundle
        diagnostics = $census.Diagnostics.ToArray()
        fullyDiscoveredSources = @($complete.FullyDiscoveredSources)
        discoveredCount = $complete.TotalCount; tests = $rows
    }
    $evidence | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $output -Encoding utf8NoBOM
    Write-Host "Discovered $($rows.Count) identities for $($evidence.assembly) ($($context.targetFramework))."
} finally {
    if ($null -ne $wrapper) { $wrapper.EndSession() }
    $env:DOTNET_ROOT = $oldRoot
    $env:PATH = $oldPath
}
