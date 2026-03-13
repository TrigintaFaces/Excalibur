param(
  [string[]]$ShardFilters = @(
    "eng/ci/shards/UnitTests-Core.slnf",
    "eng/ci/shards/UnitTests-Messaging.slnf",
    "eng/ci/shards/UnitTests-Transport.slnf",
    "eng/ci/shards/UnitTests-Middleware.slnf",
    "eng/ci/shards/UnitTests-Observability.slnf",
    "eng/ci/shards/UnitTests-Excalibur.slnf"
  ),
  [string[]]$BlockingTierShards = @(
    "eng/ci/shards/UnitTests-Core.slnf",
    "eng/ci/shards/UnitTests-Transport.slnf",
    "eng/ci/shards/UnitTests-Middleware.slnf",
    "eng/ci/shards/UnitTests-Excalibur.slnf",
    "eng/ci/shards/UnitTests-Observability.slnf"
  ),
  [string[]]$AdvisoryTierShards = @(
    "eng/ci/shards/UnitTests-Messaging.slnf"
  ),
  [string]$DeterministicSlnf = "eng/ci/shards/UnitTests-Deterministic.slnf",
  [string]$AsyncRiskSlnf = "eng/ci/shards/UnitTests-AsyncRisk.slnf",
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

# --- Tier validation: verify Deterministic and AsyncRisk slnf files ---
$tierIssues = @()

function Get-UnitProjectsFromSlnf([string]$slnfPath) {
  if (-not (Test-Path $slnfPath)) {
    return @()
  }
  $content = Get-Content $slnfPath -Raw | ConvertFrom-Json
  $projects = @($content.solution.projects)
  return @($projects | Where-Object { $_ -like "tests\unit\*" })
}

$detProjects = Get-UnitProjectsFromSlnf $DeterministicSlnf
$arProjects = Get-UnitProjectsFromSlnf $AsyncRiskSlnf

# Verify tier shard files exist
if (-not (Test-Path $DeterministicSlnf)) {
  $tierIssues += "Missing tier shard file: $DeterministicSlnf"
}
if (-not (Test-Path $AsyncRiskSlnf)) {
  $tierIssues += "Missing tier shard file: $AsyncRiskSlnf"
}

# Verify no overlap between tiers
$tierOverlap = @($detProjects | Where-Object { $arProjects -contains $_ })
if ($tierOverlap.Count -gt 0) {
  foreach ($proj in $tierOverlap) {
    $tierIssues += "Project in BOTH tiers: $proj"
  }
}

# Verify Deterministic tier contains all blocking shard unit projects
foreach ($blockingShard in $BlockingTierShards) {
  if (-not (Test-Path $blockingShard)) { continue }
  $blockingProjects = Get-UnitProjectsFromSlnf $blockingShard
  foreach ($proj in $blockingProjects) {
    if (-not ($detProjects -contains $proj)) {
      $tierIssues += "Blocking shard project missing from Deterministic tier: $proj (from $blockingShard)"
    }
  }
}

# Verify AsyncRisk tier contains all advisory shard unit projects
foreach ($advisoryShard in $AdvisoryTierShards) {
  if (-not (Test-Path $advisoryShard)) { continue }
  $advisoryProjects = Get-UnitProjectsFromSlnf $advisoryShard
  foreach ($proj in $advisoryProjects) {
    if (-not ($arProjects -contains $proj)) {
      $tierIssues += "Advisory shard project missing from AsyncRisk tier: $proj (from $advisoryShard)"
    }
  }
}

# Verify all unit projects are in exactly one tier
$allTierProjects = @($detProjects) + @($arProjects)
foreach ($project in $unitProjects) {
  $relativePath = $project.FullName.Replace("$repoRoot\", "").Replace("\", "\")
  # Normalize to match slnf paths
  $slnfRelative = $relativePath.Replace("/", "\")
  $inTier = $allTierProjects | Where-Object { $_ -eq $slnfRelative }
  if (-not $inTier) {
    $tierIssues += "Unit project not in any tier: $slnfRelative"
  }
}

$tierIssueCount = $tierIssues.Count

# --- Write report ---
$summaryPath = Join-Path $OutDir "summary.md"
"# Unit Test Shard Coverage Audit" | Out-File -FilePath $summaryPath -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Unit test projects scanned: $projectCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Shard filters scanned: $($ShardFilters.Count)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Missing assignments: $missingCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Multi-shard assignments: $duplicateCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"## Tier Summary" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Deterministic (blocking) unit projects: $($detProjects.Count)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- AsyncRisk (advisory) unit projects: $($arProjects.Count)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Tier overlap: $($tierOverlap.Count)" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
"- Tier issues: $tierIssueCount" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
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

if ($tierIssueCount -gt 0) {
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "## Tier Issues" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  foreach ($issue in $tierIssues) {
    "- $issue" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  }
}

if ($missingCount -eq 0 -and $duplicateCount -eq 0 -and $tierIssueCount -eq 0) {
  "## Result" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "" | Out-File -FilePath $summaryPath -Append -Encoding UTF8
  "All unit test projects are assigned to exactly one shard and one tier." | Out-File -FilePath $summaryPath -Append -Encoding UTF8
}

$jsonPath = Join-Path $OutDir "unit-shard-map.json"
$coverageMap.GetEnumerator() |
  Sort-Object Key |
  ForEach-Object {
    $relPath = $_.Key.Replace("$repoRoot\", "")
    $slnfRelative = $relPath.Replace("/", "\")
    $tier = if ($detProjects -contains $slnfRelative) { "blocking" }
            elseif ($arProjects -contains $slnfRelative) { "advisory" }
            else { "unassigned" }
    [pscustomobject]@{
      Project = $relPath
      Shards = $_.Value
      Tier = $tier
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

if ($tierIssueCount -gt 0) {
  Write-Warning "Tier validation issues detected: $tierIssueCount"
}

if ($Enforce -and ($missingCount -gt 0 -or $duplicateCount -gt 0 -or $tierIssueCount -gt 0)) {
  throw "Unit shard coverage audit failed."
}

Write-Host "Unit shard coverage audit completed. Report: $summaryPath"
