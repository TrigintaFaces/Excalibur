#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Solution = "eng/ci/shards/ShippingOnly.slnf",
    [string]$Configuration = "Release",
    [string]$BaselinePath = "eng/banned/RS0030.baseline.json",
    [string]$OutDir = "BannedApisReport",
    [bool]$Enforce = $true,
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SymbolFromMessage {
    param([string]$Message)

    if ($Message -match "'(?<symbol>[^']+)'") {
        return $Matches['symbol']
    }

    if ($Message -match '"(?<symbol>[^"]+)"') {
        return $Matches['symbol']
    }

    return "(unknown)"
}

function Convert-ToCountMap {
    param([array]$Entries)

    $map = @{}
    foreach ($entry in $Entries) {
        if ($null -eq $entry) {
            continue
        }

        $key = [string]$entry.key
        if ([string]::IsNullOrWhiteSpace($key)) {
            continue
        }

        $map[$key] = [int]$entry.count
    }

    return $map
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$buildLogPath = Join-Path $OutDir "build-rs0030.log"
$summaryPath = Join-Path $OutDir "summary.md"
$reportPath = Join-Path $OutDir "report.json"

Write-Host "🚫 Banned API Gate (RS0030 baseline diff)"
Write-Host "Solution: $Solution"
Write-Host "Configuration: $Configuration"
Write-Host "Baseline: $BaselinePath"
Write-Host "Update baseline: $UpdateBaseline"
Write-Host "Enforce: $Enforce"
Write-Host ""

$buildArgs = @(
    "build", $Solution,
    "--configuration", $Configuration,
    "--nologo",
    "--verbosity", "minimal",
    "-p:EnableBannedApiAnalyzers=true",
    "-p:BannedApiStrict=false",
    "-p:RunAnalyzers=true",
    "-p:RunAnalyzersDuringBuild=true",
    "-p:TreatWarningsAsErrors=false",
    "-p:WarningsAsErrors="
)

Write-Host "Building with Roslyn banned API analyzers..."
$buildOutput = & dotnet @buildArgs 2>&1
$buildOutput | Tee-Object -FilePath $buildLogPath | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed before banned API comparison."
    exit 1
}

$diagnosticRegex = '^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s*warning\s+RS0030:\s*(?<message>.+?)\s+\[(?<project>.+?)\]\s*$'
$records = @()

foreach ($entry in $buildOutput) {
    $line = [string]$entry
    if ($line -match $diagnosticRegex) {
        $projectPath = $Matches['project']
        $projectName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
        $message = $Matches['message']
        $symbol = Get-SymbolFromMessage -Message $message

        $records += [pscustomobject]@{
            project = $projectName
            symbol = $symbol
            file = $Matches['file']
            line = [int]$Matches['line']
            column = [int]$Matches['column']
            key = "$projectName|$symbol"
        }
    }
}

$currentCounts = @(
    $records |
        Group-Object key |
        ForEach-Object {
            $first = $_.Group[0]
            [pscustomobject]@{
                key = $_.Name
                project = $first.project
                symbol = $first.symbol
                count = $_.Count
            }
        } |
        Sort-Object -Property project, symbol
)

$bySymbol = @(
    $records |
        Group-Object symbol |
        ForEach-Object {
            [pscustomobject]@{
                symbol = $_.Name
                count = $_.Count
            }
        } |
        Sort-Object -Property @{ Expression = 'count'; Descending = $true }, @{ Expression = 'symbol'; Descending = $false }
)

$baselineFound = Test-Path $BaselinePath
$baselineData = $null
$baselineCounts = @{}

if ($baselineFound) {
    $baselineData = Get-Content $BaselinePath -Raw | ConvertFrom-Json
    $baselineCounts = Convert-ToCountMap -Entries @($baselineData.countByProjectAndSymbol)
}

$increased = @()
$resolved = @()

if ($baselineCounts.Count -gt 0) {
    foreach ($item in $currentCounts) {
        $baseCount = if ($baselineCounts.ContainsKey($item.key)) { [int]$baselineCounts[$item.key] } else { 0 }
        $delta = [int]$item.count - $baseCount
        if ($delta -gt 0) {
            $increased += [pscustomobject]@{
                key = $item.key
                project = $item.project
                symbol = $item.symbol
                baseline = $baseCount
                current = [int]$item.count
                delta = $delta
            }
        }
    }

    $currentMap = Convert-ToCountMap -Entries $currentCounts
    foreach ($key in $baselineCounts.Keys) {
        $currentValue = if ($currentMap.ContainsKey($key)) { [int]$currentMap[$key] } else { 0 }
        $delta = [int]$baselineCounts[$key] - $currentValue
        if ($delta -gt 0) {
            $parts = $key.Split("|", 2)
            $resolved += [pscustomobject]@{
                key = $key
                project = $parts[0]
                symbol = $parts[1]
                baseline = [int]$baselineCounts[$key]
                current = $currentValue
                delta = $delta
            }
        }
    }
}

$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    solution = $Solution
    configuration = $Configuration
    baselinePath = $BaselinePath
    totalWarnings = $records.Count
    distinctProjectSymbolPairs = $currentCounts.Count
    bySymbol = $bySymbol
    countByProjectAndSymbol = $currentCounts
    increasedVsBaseline = $increased
    resolvedVsBaseline = $resolved
}

$report | ConvertTo-Json -Depth 8 | Out-File -FilePath $reportPath -Encoding utf8

$lines = @()
$lines += "# RS0030 Banned API Gate"
$lines += ""
$lines += "- Solution: $Solution"
$lines += "- Configuration: $Configuration"
$lines += "- Total RS0030 warnings: $($records.Count)"
$lines += "- Distinct project/symbol pairs: $($currentCounts.Count)"
$lines += "- Baseline file: $BaselinePath"

if ($UpdateBaseline) {
    $baselinePayload = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        solution = $Solution
        configuration = $Configuration
        totalWarnings = $records.Count
        countByProjectAndSymbol = $currentCounts
    }

    $baselineDir = Split-Path -Parent $BaselinePath
    if ($baselineDir -and -not (Test-Path $baselineDir)) {
        New-Item -ItemType Directory -Path $baselineDir -Force | Out-Null
    }

    $baselinePayload | ConvertTo-Json -Depth 8 | Out-File -FilePath $BaselinePath -Encoding utf8
    $lines += "- Baseline updated: yes"
    $lines += ""
    $lines += "Baseline updated from current diagnostics."
    $lines | Out-File -FilePath $summaryPath -Encoding utf8
    Write-Host "✅ Baseline updated at $BaselinePath"
    exit 0
}

if (-not $baselineFound) {
    $lines += "- Baseline loaded: no"
    $lines += ""
    $lines += "No baseline was found."
    $lines | Out-File -FilePath $summaryPath -Encoding utf8

    if ($Enforce) {
        Write-Error "Baseline file missing. Run with -UpdateBaseline to initialize $BaselinePath."
        exit 1
    }

    Write-Warning "Baseline file missing. Report generated without enforcement."
    exit 0
}

$lines += "- Baseline loaded: yes"
$lines += "- Increased project/symbol pairs: $($increased.Count)"
$lines += "- Resolved project/symbol pairs: $($resolved.Count)"
$lines += ""

if ($increased.Count -gt 0) {
    $lines += "## Increased vs Baseline"
    $lines += ""
    foreach ($row in ($increased | Sort-Object -Property @{ Expression = 'delta'; Descending = $true }, @{ Expression = 'project'; Descending = $false }, @{ Expression = 'symbol'; Descending = $false } | Select-Object -First 50)) {
        $lines += "- [$($row.project)] $($row.symbol): baseline $($row.baseline), current $($row.current), delta +$($row.delta)"
    }
    if ($increased.Count -gt 50) {
        $lines += "- ... and $($increased.Count - 50) more"
    }
    $lines += ""
}

if ($resolved.Count -gt 0) {
    $lines += "## Resolved vs Baseline"
    $lines += ""
    foreach ($row in ($resolved | Sort-Object -Property @{ Expression = 'delta'; Descending = $true }, @{ Expression = 'project'; Descending = $false }, @{ Expression = 'symbol'; Descending = $false } | Select-Object -First 50)) {
        $lines += "- [$($row.project)] $($row.symbol): baseline $($row.baseline), current $($row.current), resolved $($row.delta)"
    }
    if ($resolved.Count -gt 50) {
        $lines += "- ... and $($resolved.Count - 50) more"
    }
    $lines += ""
}

$lines | Out-File -FilePath $summaryPath -Encoding utf8

if ($Enforce -and $increased.Count -gt 0) {
    Write-Error "RS0030 gate failed: $($increased.Count) project/symbol pair(s) increased versus baseline."
    exit 1
}

Write-Host "✅ RS0030 gate passed."
exit 0
