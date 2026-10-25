# Audits the PublicAPI analyzer baselines across the shipping graph. It reports two findings, and
# they are deliberately enforceable SEPARATELY, because they are not the same kind of defect.
#
#   missing baseline PAIR   A packable project with no PublicAPI.Shipped.txt/Unshipped.txt is outside
#                           the analyzer's reach entirely -- RS0016 cannot fire, so its public surface
#                           is unguarded. That is wrong on every commit and -Enforce keeps failing it.
#
#   PENDING Unshipped rows  New public API lands in Unshipped first; that is the analyzer's design and
#                           the convention the BCL repositories follow. Rows accumulate through
#                           development and are promoted into Shipped before a release. Failing a
#                           routine commit for them punishes the correct workflow, so -EnforcePending-
#                           Unshipped is turned OFF per-commit and left ON for the tag that publishes.
#
# The promotion itself is performed in the development repository before the release is cut; this
# script only VERIFIES that it happened. It never edits a baseline.
param(
  [string]$SolutionFilter = "eng/ci/shards/ShippingOnly.slnf",
  [string]$OutDir = "PublicApiBaselineReport",
  [bool]$Enforce = $true,
  [bool]$EnforcePendingUnshipped = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Get-ProjectPaths {
  param([string]$FilterPath)

  if (Test-Path $FilterPath) {
    $slnf = Get-Content -Raw $FilterPath | ConvertFrom-Json
    return @($slnf.solution.projects | Where-Object { Test-Path $_ })
  }

  return @((Get-ChildItem -Path "src" -Recurse -Filter "*.csproj" -File).FullName)
}

function Get-ApiEntryCount {
  param([string]$Path)

  if (-not (Test-Path $Path)) {
    return 0
  }

  $count = 0
  foreach ($line in Get-Content $Path) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
      continue
    }

    # Support lightweight comments if present in baseline files.
    if ($trimmed.StartsWith("#", [System.StringComparison]::Ordinal)) {
      continue
    }

    $count++
  }

  return $count
}

$projectPaths = @(Get-ProjectPaths -FilterPath $SolutionFilter)
$results = @()

foreach ($projectPath in $projectPaths) {
  $project = Get-Item $projectPath
  $projectDir = $project.DirectoryName

  [xml]$projectXml = Get-Content -Raw $projectPath
  $isPackableNode = $projectXml.SelectSingleNode("//Project/PropertyGroup/IsPackable")
  $isPackable = $true
  if ($isPackableNode -and $isPackableNode.InnerText.Trim().ToLowerInvariant() -eq "false") {
    $isPackable = $false
  }
  if (-not $isPackable) {
    continue
  }

  $shippedPath = Join-Path $projectDir "PublicAPI.Shipped.txt"
  $unshippedPath = Join-Path $projectDir "PublicAPI.Unshipped.txt"
  $hasUnshipped = Test-Path $unshippedPath
  $unshippedEntryCount = if ($hasUnshipped) { Get-ApiEntryCount -Path $unshippedPath } else { 0 }

  $results += [pscustomobject]@{
    Project = $project.FullName.Replace((Get-Location).Path + "\", "")
    HasShipped = Test-Path $shippedPath
    HasUnshipped = $hasUnshipped
    UnshippedEntryCount = $unshippedEntryCount
    HasPendingUnshipped = $unshippedEntryCount -gt 0
  }
}

$missing = @($results | Where-Object { -not $_.HasShipped -or -not $_.HasUnshipped })
$pendingUnshipped = @($results | Where-Object { $_.HasPendingUnshipped })
$projectCount = $results.Count
$missingCount = $missing.Count
$pendingCount = $pendingUnshipped.Count

$summaryPath = Join-Path $OutDir "summary.md"
"# Public API Baseline Audit" | Out-File -FilePath $summaryPath -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Projects scanned: $projectCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Missing baseline pairs: $missingCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Projects with non-empty PublicAPI.Unshipped.txt: $pendingCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8

if ($missingCount -gt 0) {
  "## Missing PublicAPI Baselines" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  foreach ($item in $missing) {
    "- $($item.Project) (Shipped=$($item.HasShipped), Unshipped=$($item.HasUnshipped))" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
}

if ($pendingCount -gt 0) {
  "## Projects with Pending Public API Entries" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  foreach ($item in $pendingUnshipped | Sort-Object Project) {
    "- $($item.Project) ($($item.UnshippedEntryCount) entries)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
}

if ($missingCount -eq 0 -and $pendingCount -eq 0) {
  "## Result" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "All shipping projects contain baseline pairs and have no pending PublicAPI.Unshipped entries." | Out-File -FilePath $summaryPath -Append -Encoding UTF8
}

$jsonPath = Join-Path $OutDir "public-api-baselines.json"
$results | ConvertTo-Json -Depth 4 | Out-File -FilePath $jsonPath -Encoding UTF8

if ($missingCount -gt 0) {
  Write-Warning "Missing PublicAPI baseline files detected: $missingCount"
}

if ($pendingCount -gt 0) {
  if ($EnforcePendingUnshipped) {
    Write-Warning "Non-empty PublicAPI.Unshipped.txt detected in shipping projects: $pendingCount"
  }
  else {
    Write-Host "Pending PublicAPI.Unshipped entries in $pendingCount project(s). Not enforced here; promote them before the release tag."
  }
}

$failures = @()
if ($missingCount -gt 0 -and $Enforce) {
  $failures += "$missingCount project(s) are missing a PublicAPI baseline pair"
}
if ($pendingCount -gt 0 -and $EnforcePendingUnshipped) {
  $failures += "$pendingCount project(s) have unpromoted PublicAPI.Unshipped entries. Move each entry into that project's PublicAPI.Shipped.txt (and drop the signature a *REMOVED* line names), commit, then re-tag"
}
if ($failures.Count -gt 0) {
  throw "Public API baseline audit failed: $($failures -join '; ')."
}

Write-Host "Public API baseline audit completed. Report: $summaryPath"
