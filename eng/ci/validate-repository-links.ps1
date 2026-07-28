#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates legacy repository URLs are not used in tracked files.
.DESCRIPTION
    Blocks known historical repository URL variants and enforces canonical
    references to https://github.com/TrigintaFaces/Excalibur for this project.
#>
param(
    [string]$OutDir = "management/reports/SolutionGovernanceReport",
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rules = @(
    [PSCustomObject]@{ Name = "legacy-org-repo"; Pattern = "github\.com/excalibur/dispatch" },
    [PSCustomObject]@{ Name = "legacy-kebab-repo"; Pattern = "github\.com/excalibur-dispatch/Excalibur\.Dispatch" },
    [PSCustomObject]@{ Name = "legacy-owner-kebab"; Pattern = "github\.com/TrigintaFaces/excalibur-dispatch" },
    [PSCustomObject]@{ Name = "legacy-owner-dotted"; Pattern = "github\.com/TrigintaFaces/Excalibur\.Dispatch" }
)

$trackedFiles = @(git ls-files)
$trackedFiles = @($trackedFiles | Where-Object { $_ -notlike ".beads/*" -and $_ -notlike ".git/*" })

$violations = @()
foreach ($file in $trackedFiles) {
    if (-not (Test-Path $file -PathType Leaf)) {
        continue
    }

    foreach ($rule in $rules) {
        $matches = @(Select-String -Path $file -Pattern $rule.Pattern -SimpleMatch:$false)
        foreach ($match in $matches) {
            $violations += [PSCustomObject]@{
                file = $file.Replace('\\', '/')
                line = $match.LineNumber
                rule = $rule.Name
                matchedText = $match.Matches.Value
            }
        }
    }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$summaryPath = Join-Path $OutDir "repository-links-validation.md"
$jsonPath = Join-Path $OutDir "repository-links-validation.json"

$report = [PSCustomObject]@{
    canonicalRepository = "https://github.com/TrigintaFaces/Excalibur"
    scannedFileCount = $trackedFiles.Count
    violationCount = $violations.Count
    violations = $violations
}

$report | ConvertTo-Json -Depth 6 | Out-File -FilePath $jsonPath -Encoding UTF8

$lines = @()
$lines += "# Repository Link Validation"
$lines += ""
$lines += "- Canonical repository: https://github.com/TrigintaFaces/Excalibur"
$lines += "- Files scanned: $($trackedFiles.Count)"
$lines += "- Violations: $($violations.Count)"
$lines += ""

if ($violations.Count -gt 0) {
    $lines += "## Violations"
    foreach ($v in ($violations | Sort-Object file, line, rule)) {
        $lines += "- $($v.file):$($v.line) [$($v.rule)]"
    }
    $lines += ""
}
else {
    $lines += "## Result"
    $lines += "No legacy repository URL patterns detected."
    $lines += ""
}

$lines | Out-File -FilePath $summaryPath -Encoding UTF8

Write-Host "Wrote summary: $summaryPath"
Write-Host "Wrote report: $jsonPath"

if ($Enforce -and $violations.Count -gt 0) {
    throw "Repository link validation failed with $($violations.Count) violation(s)."
}

Write-Host "Repository link validation passed."
