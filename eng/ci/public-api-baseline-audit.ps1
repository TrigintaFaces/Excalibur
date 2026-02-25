param(
  [string]$SolutionFilter = "eng/ci/shards/ShippingOnly.slnf",
  [string]$OutDir = "PublicApiBaselineReport",
  [bool]$Enforce = $true
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
  Write-Warning "Non-empty PublicAPI.Unshipped.txt detected in shipping projects: $pendingCount"
}

if (($missingCount -gt 0 -or $pendingCount -gt 0) -and $Enforce) {
  throw "Public API baseline audit failed."
}

Write-Host "Public API baseline audit completed. Report: $summaryPath"
