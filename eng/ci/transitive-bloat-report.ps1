#!/usr/bin/env pwsh
param(
    [string]$SrcDir = "src",
    [string]$PackageMap = "management/package-map.yaml",
    [string]$OutputDir = "TransitiveBloatReport"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "🔎 Transitive bloat report (provider SDKs in Core/Abstractions)"

if (-not (Test-Path $PackageMap)) {
    Write-Warning "Package map not found at '$PackageMap'. Using fallback category heuristics (Core/Abstractions only)."
}

# Quick YAML parse: id -> category
$categories = @{}
if (Test-Path $PackageMap) {
    $id = $null
    foreach ($line in Get-Content $PackageMap) {
        if ($line -match "^- id:\s*(.+)$") { $id = $Matches[1].Trim() }
        elseif ($line -match "^\s*category:\s*(.+)$" -and $id) {
            $categories[$id] = $Matches[1].Trim()
            $id = $null
        }
    }
}

$prohibitedPrefixes = @(
    'Azure.', 'AWSSDK', 'Amazon.', 'Google.Cloud', 'Google.Apis', 'Confluent.Kafka', 'RabbitMQ.Client', 'MongoDB.Driver', 'StackExchange.Redis', 'Elastic.Clients', 'Microsoft.Data.SqlClient', 'Npgsql'
)

$findings = @()
$projects = Get-ChildItem -Path $SrcDir -Filter "*.csproj" -Recurse
foreach ($proj in $projects) {
    # Map project folder name to package id from package-map (best effort)
    $pkgId = $proj.Directory.Name
    $cat = if ($categories.ContainsKey($pkgId)) { $categories[$pkgId] } else { '' }

    if (-not $cat) {
        if ($pkgId -match '\.Abstractions($|\.)') {
            $cat = 'Abstractions'
        }
        elseif ($pkgId -in @(
            'Excalibur.Dispatch',
            'Excalibur',
            'Excalibur.Data',
            'Excalibur.EventSourcing',
            'Excalibur.Outbox',
            'Excalibur.Saga',
            'Excalibur.Jobs',
            'Excalibur.LeaderElection'
        )) {
            $cat = 'Core'
        }
    }

    if ($cat -notin @('Core','Abstractions')) { continue }

    Write-Host "Scanning packages for $($proj.FullName) [$cat]"
    $jsonFile = Join-Path $OutputDir ("{0}.packages.json" -f $proj.BaseName)
    dotnet list $proj.FullName package --include-transitive --format json | Out-File -FilePath $jsonFile -Encoding UTF8
    try {
        $json = Get-Content $jsonFile -Raw | ConvertFrom-Json
        $all = @()
        foreach ($tfm in $json.projects.frameworks) {
            $topLevel = @($tfm.topLevelPackages)
            $transitive = @()
            if ($tfm.PSObject.Properties.Name -contains 'transitivePackages') {
                $transitive = @($tfm.transitivePackages)
            }
            foreach ($dep in ($topLevel + $transitive)) { $all += $dep.id }
        }
        $matches = $all | Where-Object { $p = $_; $prohibitedPrefixes | Where-Object { $p.StartsWith($_) } }
        $matches = $matches | Sort-Object -Unique
        foreach ($m in $matches) { $findings += [pscustomobject]@{ Project=$proj.FullName; Category=$cat; Package=$m } }
    }
    catch { Write-Warning "Failed to parse package list for $($proj.FullName): $_" }
}

$report = Join-Path $OutputDir "summary.md"
"# Transitive Bloat Report`n" | Out-File $report -Encoding UTF8
if ($findings.Count -eq 0) {
    "No prohibited provider SDKs detected in Core/Abstractions." | Out-File $report -Append -Encoding UTF8
}
else {
    "Prohibited provider SDKs detected in Core/Abstractions:" | Out-File $report -Append -Encoding UTF8
    foreach ($f in $findings) {
        "- [$($f.Category)] $($f.Project): $($f.Package)" | Out-File $report -Append -Encoding UTF8
    }
}

$enforce = ($env:PACK_ENFORCE -and $env:PACK_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
if ($enforce -and $findings.Count -gt 0) {
    Write-Error "PACK_ENFORCE=true and prohibited provider packages were detected."
    exit 1
}
exit 0

