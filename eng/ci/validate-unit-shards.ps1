param(
  [string[]]$ShardFilters = @(
    "eng/ci/shards/UnitTests-Core.slnf",
    "eng/ci/shards/UnitTests-Messaging.slnf",
    "eng/ci/shards/UnitTests-Transport.slnf",
    "eng/ci/shards/UnitTests-Middleware.slnf",
    "eng/ci/shards/UnitTests-Observability.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Data.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Platform.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Messaging.slnf"
  ),
  [string[]]$BlockingTierShards = @(
    "eng/ci/shards/UnitTests-Core.slnf",
    "eng/ci/shards/UnitTests-Transport.slnf",
    "eng/ci/shards/UnitTests-Middleware.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Data.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Platform.slnf",
    "eng/ci/shards/UnitTests-Excalibur-Messaging.slnf"
  ),
  [string[]]$AdvisoryTierShards = @(
    "eng/ci/shards/UnitTests-Messaging.slnf",
    "eng/ci/shards/UnitTests-Observability.slnf"
  ),
  [string]$DeterministicSlnf = "eng/ci/shards/UnitTests-Deterministic.slnf",
  [string]$AsyncRiskSlnf = "eng/ci/shards/UnitTests-AsyncRisk.slnf",
  [string]$UnitTestsRoot = "tests/unit",
  # Projects under tests/unit that declare NO tests and therefore cannot belong to a shard. Shard
  # membership is an assertion that an assembly produces test results, so listing a project that
  # declares none makes the aggregation gate demand results from something built to have none.
  #
  # DupFixtures supplies duplicate handler types to the MediatR compatibility suite, which pulls it
  # in by project reference. Its own source calls it a marker to resolve the fixture assembly. It has
  # zero test methods.
  #
  # Keep this list SHORT and justified. A real test project landing here would be silently exempt
  # from shard coverage -- the exact hole this audit exists to close.
  [string[]]$NonTestProjects = @(
    "Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures"
  ),
  [string]$OutDir = "UnitShardReport",
  [bool]$Enforce = $true,
  [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------------------
# Proves this audit is non-vacuous. A coverage audit that cannot fail is indistinguishable from
# one with nothing to report, and both print the same reassuring line.
#
# Two arms, because either alone is satisfied by a broken gate: one that refuses everything is
# "safe" and useless, one that refuses nothing is quiet and useless. The audit must refuse a
# project belonging to no shard and no tier, AND pass an unmodified tree.
# ---------------------------------------------------------------------------------------------
if ($SelfTest) {
  $auditScript = $PSCommandPath
  $probeName   = "Excalibur.ShardAuditSelfTest.Tests"
  $probeDir    = Join-Path $UnitTestsRoot $probeName
  $scratch     = Join-Path ([System.IO.Path]::GetTempPath()) ("shard-selftest-" + [guid]::NewGuid().ToString("N"))
  $failures    = @()

  function Test-AuditAccepts([string]$reportDir) {
    try {
      & $auditScript -OutDir $reportDir -Enforce $true *> $null
      return $true
    }
    catch {
      return $false
    }
  }

  try {
    if (-not (Test-AuditAccepts (Join-Path $scratch "unmodified"))) {
      $failures += "LIVENESS: the audit refused an unmodified tree. A gate that is red on a clean " +
                   "checkout carries no information -- it is read once and ignored thereafter."
    }

    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    $probeProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
"@
    Set-Content -Path (Join-Path $probeDir ($probeName + ".csproj")) -Value $probeProject -Encoding UTF8

    if (Test-AuditAccepts (Join-Path $scratch "planted")) {
      $failures += "SAFETY: a unit test project belonging to no shard and no tier was ACCEPTED. " +
                   "Tests in no shard are never executed by any lane, and their absence reads as " +
                   "green, which is the hole this audit exists to close."
    }
  }
  finally {
    if (Test-Path $probeDir) { Remove-Item -Recurse -Force $probeDir }
    if (Test-Path $scratch)  { Remove-Item -Recurse -Force $scratch }
  }

  if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Error $failure -ErrorAction Continue }
    throw "Unit shard coverage audit self-test FAILED: the audit does not detect what it reports on."
  }

  Write-Host "Unit shard coverage audit self-test passed: refuses an untiered project, accepts a clean tree."
  exit 0
}


New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$repoRoot = (Get-Location).Path
$unitProjects = @(Get-ChildItem -Path $UnitTestsRoot -Recurse -Filter "*.csproj" -File |
  Where-Object { $NonTestProjects -notcontains [IO.Path]::GetFileNameWithoutExtension($_.Name) })

foreach ($excluded in $NonTestProjects) {
  Write-Host "Excluded from shard coverage (declares no tests): $excluded"
}

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

function Get-RelativeRepoPath([string]$fullPath) {
  $relative = [IO.Path]::GetRelativePath($repoRoot, $fullPath)
  return $relative.Replace('/', '\')
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
  $slnfRelative = Get-RelativeRepoPath $project.FullName
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
    $relPath = Get-RelativeRepoPath $_.Key
    $slnfRelative = $relPath
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
