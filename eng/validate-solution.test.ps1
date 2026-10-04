#!/usr/bin/env pwsh
param([string]$EvidenceDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$fixture = Get-Content (Join-Path $repo 'tests/architecture/Boundary.Tests/Fixtures/SolutionGovernance.json') -Raw | ConvertFrom-Json -AsHashtable
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$scratch = Join-Path $temporaryRoot ('solution-controls-' + [guid]::NewGuid().ToString('N'))
$results = [Collections.Generic.List[object]]::new()
$helperChecks = 0
function Invoke-InventoryTool([string]$Root, [string]$Script, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.WorkingDirectory = $Root; $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($arg in (@('-NoProfile','-File',(Join-Path $repo $Script)) + $Arguments)) { $start.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) { $process.Kill($true); throw "Timed out: $Script" }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Inventory tool failed: $Script : $output" }
        return $output
    }
    finally { $process.Dispose() }
}
try {
    foreach ($case in $fixture.cases) {
        $root = Join-Path $scratch $case.name
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $files = @{}
        foreach ($file in $fixture.files.GetEnumerator()) { $files[$file.Key] = $file.Value }
        if ($case.ContainsKey('edits')) {
            foreach ($edit in $case.edits) {
                if (-not $files[$edit.path].Contains($edit.before)) { throw "Fixture edit did not apply: $($case.name)" }
                $files[$edit.path] = $files[$edit.path].Replace($edit.before, $edit.after)
            }
        }
        if ($case.ContainsKey('delete')) { foreach ($path in $case.delete) { $files.Remove($path) } }
        if ($case.ContainsKey('add')) { foreach ($file in $case.add.GetEnumerator()) { $files[$file.Key] = $file.Value } }
        foreach ($file in $files.GetEnumerator()) {
            $path = [IO.Path]::GetFullPath((Join-Path $root $file.Key))
            if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) { throw 'Fixture escapes its directory' }
            New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
            [IO.File]::WriteAllText($path, $file.Value)
        }
        $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
        $start.WorkingDirectory = $root
        $start.UseShellExecute = $false
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        foreach ($arg in @('-NoProfile','-File',(Join-Path $repo 'eng/validate-solution.ps1'))) { $start.ArgumentList.Add($arg) }
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(60000)) { $process.Kill($true); throw "Timed out: $($case.name)" }
            $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
            $passed = ($process.ExitCode -eq 0)
            $results.Add(@{name=$case.name;expectedPass=$case.pass;exitCode=$process.ExitCode;output=$output})
            if ($passed -ne $case.pass) { throw "Control failed: $($case.name): $output" }
            if (-not $case.pass -and ($process.ExitCode -ne 1 -or $output -notmatch $case.error)) { throw "Wrong rejection for $($case.name): $output" }
            Write-Host "PASS $($case.name)"
        }
        finally { $process.Dispose() }
    }
    foreach ($check in @(
        @{name='valid-exact-paths-duplicate-display-names-and-platform-remap';missing=0},
        @{name='generated-project-does-not-enter-inventory';missing=0},
        @{name='indented-project-declarations';missing=0},
        @{name='omitted-same-basename-project';missing=1;path='src/Dispatch/Widget/Widget.csproj'},
        @{name='unlisted-project-on-disk';missing=1;path='src/Dispatch/New/New.csproj'},
        @{name='case-distinct-bin-is-not-excluded';missing=1;path='src/Dispatch/Bin/Probe.csproj'}
    )) {
        $output = Invoke-InventoryTool (Join-Path $scratch $check.name) 'eng/add-missing-to-solution.ps1' @('-DryRun')
        if (-not $output.Contains("Found $($check.missing) projects to add to solution")) { throw "Repair helper count mismatch: $output" }
        if ($check.missing -gt 0 -and -not $output.Contains("[DRY RUN] Would add: $($check.path)")) { throw "Repair helper identity mismatch: $output" }
        Write-Host "PASS repair helper $($check.name)"
        $helperChecks++
    }
    $baseRoot = Join-Path $scratch 'valid-exact-paths-duplicate-display-names-and-platform-remap'
    $toolsParent = Join-Path $scratch 'tools'
    New-Item -ItemType Directory -Path $toolsParent | Out-Null
    Copy-Item -LiteralPath $baseRoot -Destination $toolsParent -Recurse
    $baseRoot = Join-Path $toolsParent 'valid-exact-paths-duplicate-display-names-and-platform-remap'
    $null = Invoke-InventoryTool $baseRoot 'eng/inventory-projects.ps1' @('-ManifestPath','eng/governance/regenerated.yaml','-Strict')
    $null = Invoke-InventoryTool $baseRoot 'eng/validate-solution.ps1' @('-ManifestPath','eng/governance/regenerated.yaml')
    Write-Host 'PASS regenerated manifest round-trip under tools parent'
    $helperChecks++
    Write-Host "$($results.Count) solution governance controls and $helperChecks inventory-helper checks passed."
}
finally {
    if ($EvidenceDirectory) {
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
        $results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'solution-controls.json')
    }
    $resolved = [IO.Path]::GetFullPath($scratch)
    if (-not $resolved.StartsWith($temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal) -or
        [IO.Path]::GetFileName($resolved) -notlike 'solution-controls-*') { throw 'Refusing unsafe fixture cleanup' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
