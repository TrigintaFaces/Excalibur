# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SampleAssembly,
    [Parameter(Mandatory)][string]$ResultsDirectory
)
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $SampleAssembly).Path
$results = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $results -Force | Out-Null
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$assemblyHash = (Get-FileHash -LiteralPath $assembly).Hash
$bundle = @(Get-ChildItem -LiteralPath (Split-Path $assembly) -File |
    Where-Object { $_.Extension -in '.dll','.json' } |
    Sort-Object Name | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$observations = [Collections.Generic.List[object]]::new()
function Assert-BundleUnchanged {
    $current = @(Get-ChildItem -LiteralPath (Split-Path $assembly) -File |
        Where-Object { $_.Extension -in '.dll','.json' } |
        Sort-Object Name | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    if (($current | ConvertTo-Json -Compress) -cne ($bundle | ConvertTo-Json -Compress)) {
        throw 'Sample dependency bundle changed during verification.'
    }
}
# These are rejection controls against local, unreachable infrastructure. This is not emulator acceptance.
$testKey = [Convert]::ToBase64String([byte[]]::new(64))
$cases = @(
    @{name='illustrative-before-config';arguments=@('--illustrative');config='{invalid-json';connection='invalid';accepted=$true;required='Illustrative mode: no database operations are performed.'},
    @{name='missing-real-config';arguments=@('--real');config='{}';connection='';accepted=$false;required='Real mode requires CosmosDb:ConnectionString'},
    @{name='default-is-real';arguments=@();config='{}';connection='';accepted=$false;required='Real mode requires CosmosDb:ConnectionString'},
    @{name='conflicting-modes';arguments=@('--illustrative','--real');config='{}';connection='';accepted=$false;required='Usage: dotnet run'},
    @{name='unreachable-real-endpoint';arguments=@('--real');config='{}';connection="AccountEndpoint=https://127.0.0.1:1/;AccountKey=$testKey;";accepted=$false;required='Failed to connect to Cosmos DB:'}
)
foreach ($case in $cases) {
    Assert-BundleUnchanged
    if ((Get-FileHash -LiteralPath $assembly).Hash -cne $assemblyHash) { throw 'Sample assembly changed during verification.' }
    $directory = Join-Path $results $case.name
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'appsettings.json'),$case.config)
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.WorkingDirectory = $directory
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.ArgumentList.Add($assembly)
    foreach ($argument in $case.arguments) { $process.StartInfo.ArgumentList.Add($argument) }
    $process.StartInfo.Environment['CosmosDb__ConnectionString'] = $case.connection
    $process.StartInfo.Environment['CosmosDb__DatabaseName'] = 'sample-verification'
    $process.StartInfo.Environment['CosmosDb__ContainerName'] = 'sample-verification'
    $timedOut = $false
    $started = $false
    try {
        if (-not $process.Start()) { throw 'Sample process failed to start.' }
        $started = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $timedOut = $true
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'Owned timed-out sample could not be stopped.' }
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdoutTask,$stderrTask),5000)) {
            throw 'Sample output streams did not close.'
        }
        $stdout = $stdoutTask.Result
        $stderr = $stderrTask.Result
        [IO.File]::WriteAllText((Join-Path $directory 'stdout.log'),$stdout)
        [IO.File]::WriteAllText((Join-Path $directory 'stderr.log'),$stderr)
        $accepted = $process.ExitCode -eq 0
        $combined = $stdout + $stderr
        $valid = -not $timedOut -and $accepted -eq $case.accepted -and $combined.Contains($case.required)
        if ($case.accepted) {
            $valid = $valid -and $stdout.Contains('Demo Complete!') -and -not $stdout.Contains('Initializing Cosmos DB')
        } else {
            $valid = $valid -and -not $combined.Contains('Demo Complete!') -and -not $combined.Contains('Sample Complete!')
            $valid = $valid -and -not $combined.Contains('Running in demo mode with simulated output')
            if ($case.name -eq 'unreachable-real-endpoint') {
                $valid = $valid -and $combined -match 'SocketException|HttpRequestException|OperationCanceledException|TaskCanceledException'
            }
        }
        $observation = @{case=$case.name;exitCode=$process.ExitCode;timedOut=$timedOut;expectedAcceptance=$case.accepted;status=$(if($valid){'passed-control'}else{'failed-control'})}
        $observations.Add($observation)
        $observation | ConvertTo-Json | Set-Content (Join-Path $directory 'receipt.json')
        if (-not $valid) { throw "Sample mode control failed: $($case.name)" }
    } finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'Owned sample process remained active during cleanup.' }
        }
        $process.Dispose()
        @{scope='CLI mode and rejection controls only; real emulator persistence unverified';assembly=$assembly;assemblySha256=$assemblyHash;bundle=$bundle;cases=@($observations.ToArray())} |
            ConvertTo-Json -Depth 8 | Set-Content (Join-Path $results 'verification.json')
    }
}
Assert-BundleUnchanged
if ((Get-FileHash -LiteralPath $assembly).Hash -cne $assemblyHash) { throw 'Sample assembly changed during verification.' }
Write-Output "PASS: $($observations.Count) native sample mode controls; emulator persistence remains unverified."
