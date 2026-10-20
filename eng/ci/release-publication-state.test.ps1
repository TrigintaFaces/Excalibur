# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/release-publication-state.functions.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('release-publication-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot
$script:cases = 0
function Assert-Case([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:cases++
    Write-Host "PASS $Name"
}
function Assert-Throws([scriptblock]$Action) {
    $thrown = $false
    try { $null = & $Action } catch { $thrown = $true }
    if (-not $thrown) { throw 'Expected rejection.' }
}
function New-Feed([string]$Name, [string[]]$Ids = @('Excalibur.Test.A','Excalibur.Test.B'), [string]$Version = '10.0.0-alpha.99') {
    $feed = Join-Path $testRoot $Name
    $null = New-Item -ItemType Directory -Path $feed
    for ($index = 0; $index -lt $Ids.Count; $index++) {
        # Deliberately renamed archives: identity must come from the nuspec.
        $zip = [IO.Compression.ZipFile]::Open((Join-Path $feed "renamed-$index.nupkg"), [IO.Compression.ZipArchiveMode]::Create)
        try {
            $entry = $zip.CreateEntry('package.nuspec')
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write("<package><metadata><id>$($Ids[$index])</id><version>$Version</version></metadata></package>") } finally { $writer.Dispose() }
        } finally { $zip.Dispose() }
    }
    return $feed
}
function Assert-State($State, [string]$Status, [string]$Mode, [int]$Published, [int]$Unknown) {
    if ($State.status -ne $Status -or $State.mode -ne $Mode -or $State.published -ne $Published -or $State.unknown -ne $Unknown) {
        throw "Unexpected publication state: $($State | ConvertTo-Json -Depth 5 -Compress)"
    }
}
try {
    $feed = New-Feed 'normal'
    Assert-Case 'all absent, renamed packages' {
        $state = Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { param($Uri) 404 }
        Assert-State $state passed fresh 0 0
        if ($state.packages.Count -ne 2 -or $state.packages[0].id -ne 'Excalibur.Test.A') { throw 'Wrong package identity.' }
    }
    Assert-Case 'all published' { Assert-State (Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { 200 }) passed resumed 2 0 }
    Assert-Case 'mixed known' { Assert-State (Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { param($Uri) if ($Uri -like '*/excalibur.test.a/*') { 200 } else { 404 } }) passed partial 1 0 }
    Assert-Case 'unknown is never fresh' { Assert-State (Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { 503 }) not_checked inconclusive 0 2 }
    Assert-Case 'request exception is unknown' { Assert-State (Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { throw 'network unavailable' }) not_checked inconclusive 0 2 }
    Assert-Case 'polluted probe rejected' { Assert-Throws { Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe { 'noise'; 404 } } }
    Assert-Case 'empty probe rejected' { Assert-Throws { Get-ReleasePublicationState $feed '10.0.0-alpha.99' -Probe {} } }
    Assert-Case 'placeholder does not query HTTP' {
        $local = New-Feed 'local' -Version '0.0.0-local'
        $state = Get-ReleasePublicationState $local '0.0.0-local' -Probe { throw 'HTTP must not run' }
        Assert-State $state not_checked inconclusive 0 0
        if ($state.packages.Count -ne 0) { throw 'Placeholder unexpectedly queried HTTP.' }
    }
    Assert-Case 'empty feed rejected' { Assert-Throws { Get-ReleasePublicationState (New-Feed 'empty' -Ids @()) '10.0.0-alpha.99' } }
    Assert-Case 'missing feed rejected' { Assert-Throws { Get-ReleasePublicationState (Join-Path $testRoot missing) '10.0.0-alpha.99' } }
    Assert-Case 'case-insensitive duplicate identities rejected' { Assert-Throws { Get-ReleasePublicationState (New-Feed 'duplicate' -Ids @('Excalibur.Test','excalibur.test')) '10.0.0-alpha.99' } }
    Assert-Case 'mismatched version rejected before placeholder bypass' { Assert-Throws { Get-ReleasePublicationState $feed '0.0.0-local' } }
    Assert-Case 'invalid package ID rejected' { Assert-Throws { Get-ReleasePublicationState (New-Feed 'bad-id' -Ids @('not/a/package')) '10.0.0-alpha.99' } }
    Assert-Case 'invalid version rejected' { Assert-Throws { Get-ReleasePublicationState $feed 'not-a-version' } }
    Assert-Case 'mixed feed versions rejected' {
        $mixed = New-Feed 'mixed-version' -Ids @('Excalibur.Test.A')
        $other = New-Feed 'other-version' -Ids @('Excalibur.Test.B') -Version '10.0.0-alpha.98'
        Copy-Item -LiteralPath (Join-Path $other 'renamed-0.nupkg') -Destination (Join-Path $mixed 'other.nupkg')
        Assert-Throws { Get-ReleasePublicationState $mixed '10.0.0-alpha.99' }
    }
    Assert-Case 'malformed package rejected' {
        $malformed = New-Feed 'malformed' -Ids @()
        [IO.File]::WriteAllText((Join-Path $malformed 'broken.nupkg'), 'not a zip archive')
        Assert-Throws { Get-ReleasePublicationState $malformed '10.0.0-alpha.99' }
    }
    Assert-Case 'NuGet normalization, metadata and fourth zero' {
        $normalizedFeed = New-Feed 'normalized' -Version '10.0.0.0-alpha.99+build.123'
        $state = Get-ReleasePublicationState $normalizedFeed '10.0.0.0-alpha.99+build.123' -Probe {
            param($Uri)
            if ($Uri -notlike '*/10.0.0-alpha.99/*.10.0.0-alpha.99.nupkg') { throw "Wrong normalized URI: $Uri" }
            404
        }
        Assert-State $state passed fresh 0 0
    }
    # Execute the actual Run-Step and report source in isolated children, without packaging/builds.
    $source = Get-Content -LiteralPath "$PSScriptRoot/release-rehearsal.ps1" -Raw
    $tokens = $null; $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw 'Rehearsal failed to parse.' }
    $runStep = $ast.Find({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq 'Run-Step' }, $true).Extent.Text
    $buildBranch = $ast.Find({ param($Node) $Node -is [Management.Automation.Language.IfStatementAst] -and $Node.Extent.Text.StartsWith('if (-not $NoBuild)') }, $true)
    $skipBuildSource = $buildBranch.ElseClause.Extent.Text
    $reportSource = $source.Substring($source.IndexOf('# --- Report ---'))
    $shell = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    foreach ($scenario in @(
        @{name='placeholder-incomplete'; version='0.0.0-local'; actions="Run-Step 'lookup' { param(`$step) `$step.Status='not_checked' }"; status='INCOMPLETE'; code=0; skipped=0},
        @{name='real-version-incomplete'; version='10.0.0-alpha.99'; actions="Run-Step 'lookup' { param(`$step) `$step.Status='not_checked' }"; status='INCOMPLETE'; code=2; skipped=0},
        @{name='fully-checked-local'; version='10.0.0-alpha.99'; actions="Run-Step 'checked' { }"; status='PASSED'; code=0; skipped=0},
        @{name='step-output-is-not-status'; version='10.0.0-alpha.99'; actions="Run-Step 'noisy' { 'native output'; [pscustomobject]@{Status='failed'} }"; status='PASSED'; code=0; skipped=0},
        @{name='invalid-step-status-fails'; version='10.0.0-alpha.99'; actions="Run-Step 'bad status' { param(`$step) `$step.Status='invented' }"; status='FAILED'; code=1; skipped=0},
        @{name='first-failure-report'; version='10.0.0-alpha.99'; actions="Run-Step 'failed' { throw 'planted failure' }; Run-Step 'must-not-run' { Set-Content (Join-Path `$OutDir 'unexpected') 'ran' }"; status='FAILED'; code=1; skipped=1},
        @{name='no-build-incomplete'; version='10.0.0-alpha.99'; actions=". $skipBuildSource; Run-Step 'independent composition' { }"; status='INCOMPLETE'; code=2; skipped=1}
    )) {
        Assert-Case $scenario.name {
            $directory = Join-Path $testRoot $scenario.name
            $null = New-Item -ItemType Directory -Path $directory
            $escaped = $directory.Replace("'", "''")
            $prefix = "`$ErrorActionPreference='Stop'; `$OutDir='$escaped'; `$Version='$($scenario.version)'; `$NoBuild=`$false; `$StopOnFirstFailure=`$true; `$StartTime=[DateTime]::UtcNow; `$steps=@(); `$failureCount=0;"
            $scriptPath = Join-Path $directory 'report-control.ps1'
            [IO.File]::WriteAllText($scriptPath, ($prefix + "`n" + $runStep + "`n" + $scenario.actions + "`n" + $reportSource))
            $null = & $shell -NoProfile -File $scriptPath 2>&1
            if ($LASTEXITCODE -ne $scenario.code) { throw "Unexpected exit code $LASTEXITCODE for $($scenario.name)" }
            $report = Get-Content -LiteralPath (Join-Path $directory 'release-rehearsal-report.json') -Raw | ConvertFrom-Json
            if ($report.status -ne $scenario.status -or $report.releaseReady -ne $false -or
                @($report.steps | Where-Object Status -eq skipped).Count -ne $scenario.skipped -or
                (Test-Path -LiteralPath (Join-Path $directory unexpected))) { throw "Wrong report for $($scenario.name)" }
        }
    }
    Write-Host "Publication and rehearsal report controls: $script:cases passed. No network requests or package builds."
} finally {
    # Only the exact fresh test directory created above is removed.
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
# The last child deliberately exits 2. Do not leak that into the successful control-suite result.
exit 0
