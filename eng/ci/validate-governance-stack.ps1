#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the full governance validation stack with consolidated reporting.
.DESCRIPTION
    Consolidates the governance checks used by CI and release workflows:
      - Solution and project graph validation
      - ShippingOnly filter completeness
      - Canonical repository links validation
      - Framework governance matrix validation
      - Shipping package metadata audit
#>
param(
    [string]$SolutionFilter = 'eng/ci/shards/ShippingOnly.slnf',
    [string]$MatrixPath = 'management/governance/framework-governance.json',
    [string]$ReportsRoot = 'management/reports',
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 20
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }

    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

$solutionReportDir = Join-Path $ReportsRoot 'SolutionGovernanceReport'
$frameworkReportDir = Join-Path $ReportsRoot 'FrameworkGovernanceReport'
New-Item -ItemType Directory -Force -Path $solutionReportDir | Out-Null
New-Item -ItemType Directory -Force -Path $frameworkReportDir | Out-Null

Write-Host '1/5 Validate solution graph and manifest'
./eng/validate-solution.ps1
if ($LASTEXITCODE -ne 0) {
    throw "validate-solution.ps1 failed (exit code $LASTEXITCODE)."
}

Write-Host '2/5 Validate ShippingOnly filter parity'
./eng/ci/validate-shipping-filter.ps1 `
    -SolutionFilter $SolutionFilter `
    -OutDir $solutionReportDir `
    -Enforce:$Enforce
if ($LASTEXITCODE -ne 0) {
    throw "validate-shipping-filter.ps1 failed (exit code $LASTEXITCODE)."
}

Write-Host '3/5 Validate canonical repository links'
./eng/ci/validate-repository-links.ps1 `
    -OutDir $solutionReportDir `
    -Enforce:$Enforce
if ($LASTEXITCODE -ne 0) {
    throw "validate-repository-links.ps1 failed (exit code $LASTEXITCODE)."
}

Write-Host '4/5 Validate framework governance matrix'
./eng/ci/validate-framework-governance.ps1 `
    -Mode Governance `
    -MatrixPath $MatrixPath `
    -OutDir $frameworkReportDir `
    -Enforce:$Enforce
if ($LASTEXITCODE -ne 0) {
    throw "validate-framework-governance.ps1 failed (exit code $LASTEXITCODE)."
}

Write-Host '5/5 Audit shipping package metadata'
$metadataJsonPath = Join-Path $solutionReportDir 'package-metadata.json'
$metadataSummaryPath = Join-Path $solutionReportDir 'package-metadata-summary.md'
$metadataJson = & pwsh -NoProfile -File eng/audit-package-metadata.ps1 -OutputFormat Json
$metadataExitCode = $LASTEXITCODE
$metadataJson | Out-File -FilePath $metadataJsonPath -Encoding UTF8

$metadata = ConvertFrom-JsonCompat -Json $metadataJson -Depth 20
$summary = @(
    '# Package Metadata Audit',
    '',
    "- Total shipping projects: $($metadata.totalProjects)",
    "- Projects with issues: $($metadata.projectsWithIssues)",
    "- Projects complete: $($metadata.projectsComplete)",
    ''
)

if ($metadata.projectsWithIssues -gt 0) {
    $summary += '## Issue Summary'
    $summary += "- Missing <Description>: $($metadata.summary.missingDescription)"
    $summary += "- Missing <PackageTags>: $($metadata.summary.missingPackageTags)"
    $summary += "- Missing <PackageReadmeFile>: $($metadata.summary.missingPackageReadmeFile)"
    $summary += "- Missing README.md: $($metadata.summary.missingReadmeMd)"
    $summary += ''
}
else {
    $summary += '## Result'
    $summary += 'All shipping projects contain required package metadata.'
    $summary += ''
}

$summary | Out-File -FilePath $metadataSummaryPath -Encoding UTF8
Write-Host "Wrote summary: $metadataSummaryPath"
Write-Host "Wrote report: $metadataJsonPath"

if ($Enforce -and $metadataExitCode -ne 0) {
    throw "Package metadata audit failed (exit code $metadataExitCode)."
}

Write-Host 'Governance stack validation passed.'
