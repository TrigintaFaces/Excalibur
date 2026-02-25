param(
  [string[]]$ShardFilters = @(
    "eng/ci/shards/UnitTests-Core.slnf",
    "eng/ci/shards/UnitTests-Messaging.slnf",
    "eng/ci/shards/UnitTests-Transport.slnf",
    "eng/ci/shards/UnitTests-Middleware.slnf",
    "eng/ci/shards/UnitTests-Observability.slnf",
    "eng/ci/shards/UnitTests-Excalibur.slnf"
  ),
  [string]$UnitTestsRoot = "tests/unit",
  [string]$OutDir = "UnitShardReport",
  [bool]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$repoRoot = (Get-Location).Path
$unitProjects = @(Get-ChildItem -Path $UnitTestsRoot -Recurse -Filter "*.csproj" -File)

if ($unitProjects.Count -eq 0) {
  throw "No unit test projects found under '$UnitTestsRoot'."
}

$coverageMap = @{}
foreach ($project in $unitProjects) {
  $coverageMap[$project.FullName] = @()
}

foreach ($filter in $ShardFilters) {
  if (-not (Test-Path $filter)) {
    throw "Shard filter not found: $filter"
  }

  $filterContent = Get-Content $filter -Raw | ConvertFrom-Json
  $filterProjects = @($filterContent.solution.projects)

  foreach ($relativeProjectPath in $filterProjects) {
    $fullPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $relativeProjectPath))
    if ($coverageMap.ContainsKey($fullPath)) {
      $coverageMap[$fullPath] += $filter
    }
  }
}

$missing = @($coverageMap.GetEnumerator() | Where-Object { $_.Value.Count -eq 0 } | Sort-Object Key)
$duplicateAssignments = @($coverageMap.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } | Sort-Object Key)
$projectCount = $unitProjects.Count
$missingCount = $missing.Count
$duplicateCount = $duplicateAssignments.Count

$summaryPath = Join-Path $OutDir "summary.md"
"# Unit Test Shard Coverage Audit" | Out-File -FilePath $summaryPath -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Unit test projects scanned: $projectCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Shard filters scanned: $($ShardFilters.Count)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Missing assignments: $missingCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Multi-shard assignments: $duplicateCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8

if ($missingCount -gt 0) {
  "## Missing Unit Projects" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  foreach ($item in $missing) {
    $path = $item.Key.Replace("$repoRoot\", "")
    "- $path" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
}

if ($duplicateCount -gt 0) {
  if ($missingCount -gt 0) {
    "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
  "## Multi-Shard Assignments" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  foreach ($item in $duplicateAssignments) {
    $path = $item.Key.Replace("$repoRoot\", "")
    "- $path -> $($item.Value -join ", ")" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
}

if ($missingCount -eq 0 -and $duplicateCount -eq 0) {
  "## Result" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "All unit test projects are assigned to exactly one shard." | Out-File -FilePath $summaryPath -Append -Encoding UTF8
}

$jsonPath = Join-Path $OutDir "unit-shard-map.json"
$coverageMap.GetEnumerator() |
  Sort-Object Key |
  ForEach-Object {
    [pscustomobject]@{
      Project = $_.Key.Replace("$repoRoot\", "")
      Shards = $_.Value
    }
  } |
  ConvertTo-Json -Depth 4 |
  Out-File -FilePath $jsonPath -Encoding UTF8

if ($missingCount -gt 0) {
  Write-Warning "Missing shard assignments detected: $missingCount"
}

if ($duplicateCount -gt 0) {
  Write-Warning "Projects assigned to multiple shards: $duplicateCount"
}

if ($Enforce -and ($missingCount -gt 0 -or $duplicateCount -gt 0)) {
  throw "Unit shard coverage audit failed."
}

Write-Host "Unit shard coverage audit completed. Report: $summaryPath"
