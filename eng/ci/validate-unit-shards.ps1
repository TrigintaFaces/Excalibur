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
    "tests/unit/Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures/Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures.csproj"
  ),
  [string]$OutDir = "UnitShardReport",
  [bool]$Enforce = $true,
  [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


function Get-RelativeRepoPath([string]$fullPath) {
  return [IO.Path]::GetRelativePath((Get-Location).Path, $fullPath).Replace([char]92, '/')
}

function Read-FilterProjects([string]$filterPath) {
  if (-not (Test-Path -LiteralPath $filterPath -PathType Leaf)) { throw "Declared shard filter not found: $filterPath" }
  $filter = Get-Item -LiteralPath $filterPath
  $content = Get-Content -LiteralPath $filter.FullName -Raw | ConvertFrom-Json -NoEnumerate
  if ($content -isnot [pscustomobject] -or $null -eq $content.PSObject.Properties['solution'] -or
      $content.solution -isnot [pscustomobject] -or $null -eq $content.solution.PSObject.Properties['path'] -or
      $content.solution.path -isnot [string] -or $null -eq $content.solution.PSObject.Properties['projects'] -or
      $content.solution.projects -isnot [array] -or @($content.solution.projects | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count) {
    throw "Invalid filter shape: $filterPath"
  }
  $solution = [IO.Path]::GetFullPath((Join-Path $filter.DirectoryName $content.solution.path.Replace([char]92, '/')))
  if ($solution -cne (Join-Path (Get-Location).Path 'Excalibur.sln')) { throw "Shard references another solution: $filterPath" }
  $solutionProjects = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach ($line in Get-Content -LiteralPath $solution) {
    if ($line -match '^Project\("[^"]+"\) = "[^"]+", "([^"]+\.csproj)", "\{[^}]+\}"$') {
      [void]$solutionProjects.Add($Matches[1].Replace([char]92, '/'))
    }
  }
  $members = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach ($member in $content.solution.projects) {
    $path = $member.Replace([char]92, '/')
    if (@($path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -or $path.Contains(':') -or
        -not $members.Add($path) -or -not $solutionProjects.Contains($path)) {
      throw "Invalid, duplicate or absent solution project in $filterPath : $path"
    }
  }
  if ($members.Count -eq 0) { throw "Empty shard filter: $filterPath" }
  return @($members)
}

function Get-UnitProjectsFromSlnf([string]$slnfPath) {
  $unitRootPath = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $UnitTestsRoot))
  $prefix = (Get-RelativeRepoPath $unitRootPath).TrimEnd('/') + '/'
  return @(Read-FilterProjects $slnfPath | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) })
}

if ($SelfTest) {
  $auditScript = $PSCommandPath
  $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
  $scratch = Join-Path $tempRoot ('unit-shard-controls-' + [guid]::NewGuid().ToString('N'))
  $cases = @('valid','forward-slashes','unassigned','missing-shard','missing-blocking','missing-advisory','duplicate-assignment','case-substitution','same-name-exemption','scalar-projects','array-root')
  try {
    foreach ($case in $cases) {
      $root = Join-Path $scratch $case
      New-Item -ItemType Directory -Path $root -Force | Out-Null
      $projects = @('tests/unit/Assigned/Assigned.csproj','tests/unit/Advisory/Advisory.csproj','tests/unit/Fixture/Fixture.csproj')
      if ($case -eq 'unassigned') { $projects += 'tests/unit/New/New.csproj' }
      if ($case -eq 'same-name-exemption') { $projects += 'tests/unit/Other/Fixture.csproj' }
      $solution = @()
      foreach ($path in $projects) {
        New-Item -ItemType Directory -Path (Split-Path (Join-Path $root $path)) -Force | Out-Null
        '<Project/>' | Set-Content -LiteralPath (Join-Path $root $path)
        $solution += 'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Test", "' + $path + '", "{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}"'
      }
      $solution | Set-Content (Join-Path $root 'Excalibur.sln')
      $filters = @{
        'blocking.slnf'=@($projects[0]); 'advisory.slnf'=@($projects[1])
        'deterministic.slnf'=@($projects[0]); 'risk.slnf'=@($projects[1])
      }
      if ($case -eq 'duplicate-assignment') { $filters['advisory.slnf'] += $projects[0] }
      if ($case -eq 'case-substitution') { $filters['blocking.slnf'] = @($projects[0].Replace('Assigned','assigned')) }
      foreach ($filter in $filters.GetEnumerator()) {
        $members = if ($case -eq 'forward-slashes') { $filter.Value } else { @($filter.Value | ForEach-Object { $_.Replace('/', '\') }) }
        @{solution=@{path='Excalibur.sln';projects=@($members)}} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root $filter.Key)
      }
      if ($case -eq 'scalar-projects') {
        @{solution=@{path='Excalibur.sln';projects=$projects[0]}} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'blocking.slnf')
      }
      if ($case -eq 'array-root') {
        $filterPath = Join-Path $root 'blocking.slnf'
        ('[' + (Get-Content -LiteralPath $filterPath -Raw) + ']') | Set-Content -LiteralPath $filterPath
      }
      $parameters = @{
        ShardFilters=@('blocking.slnf','advisory.slnf'); BlockingTierShards=@('blocking.slnf')
        AdvisoryTierShards=@('advisory.slnf'); DeterministicSlnf='deterministic.slnf'; AsyncRiskSlnf='risk.slnf'
        NonTestProjects=@('tests/unit/Fixture/Fixture.csproj'); UnitTestsRoot='tests/unit'; OutDir='report'; Enforce=$true
      }
      if ($case -eq 'missing-shard') { $parameters.ShardFilters += 'absent.slnf' }
      if ($case -eq 'missing-blocking') { $parameters.BlockingTierShards += 'absent.slnf' }
      if ($case -eq 'missing-advisory') { $parameters.AdvisoryTierShards += 'absent.slnf' }
      Push-Location $root
      try {
        $accepted = $false
        $failure = ''
        try { & $auditScript @parameters *> $null; $accepted = $true } catch { $failure = $_.Exception.Message }
        $expected = $case -in @('valid','forward-slashes')
        if ($accepted -ne $expected) { throw "Unit shard control failed: $case (accepted=$accepted expected=$expected): $failure" }
        if (-not $expected) {
          $reason = if ($case.StartsWith('missing-')) { 'Declared shard filter not found' }
            elseif ($case -in @('scalar-projects','array-root')) { 'Invalid filter shape' }
            elseif ($case -eq 'case-substitution') { 'absent solution project' }
            else { 'Unit shard coverage audit failed' }
          if (-not $failure.Contains($reason, [StringComparison]::Ordinal)) { throw "Wrong rejection for $case : $failure" }
        }
      }
      finally { Pop-Location }
      Write-Host "PASS $case"
    }
    Write-Host "$($cases.Count) unit shard controls passed without editing repository projects."
  }
  finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    if (-not $resolved.StartsWith($tempRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal) -or
        [IO.Path]::GetFileName($resolved) -notlike 'unit-shard-controls-*') { throw 'Unsafe fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
  }
  exit 0
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$repoRoot = (Get-Location).Path
$unitProjects = @(Get-ChildItem -Path $UnitTestsRoot -Recurse -Filter "*.csproj" -File |
  Where-Object {
    $relative = Get-RelativeRepoPath $_.FullName
    $relative -cnotin $NonTestProjects -and -not @($relative.Split('/') | Where-Object { $_.StartsWith('.') -or $_ -cin @('bin','obj','BenchmarkDotNet.Artifacts') }).Count
  })

foreach ($excluded in $NonTestProjects) {
  Write-Host "Excluded from shard coverage (declares no tests): $excluded"
}

if ($unitProjects.Count -eq 0) {
  throw "No unit test projects found under '$UnitTestsRoot'."
}

$coverageMap = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($project in $unitProjects) {
  $coverageMap[(Get-RelativeRepoPath $project.FullName)] = @()
}

foreach ($filter in $ShardFilters) {
  foreach ($path in Read-FilterProjects $filter) {
    if ($coverageMap.ContainsKey($path)) { $coverageMap[$path] += $filter }
  }
}

$missing = @($coverageMap.GetEnumerator() | Where-Object { $_.Value.Count -eq 0 } | Sort-Object Key)
$duplicateAssignments = @($coverageMap.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } | Sort-Object Key)
$projectCount = $unitProjects.Count
$missingCount = $missing.Count
$duplicateCount = $duplicateAssignments.Count

# --- Tier validation: verify Deterministic and AsyncRisk slnf files ---
$tierIssues = @()

$detProjects = @(Get-UnitProjectsFromSlnf $DeterministicSlnf)
$arProjects = @(Get-UnitProjectsFromSlnf $AsyncRiskSlnf)

# Verify no overlap between tiers
$tierOverlap = @($detProjects | Where-Object { $arProjects -ccontains $_ })
if ($tierOverlap.Count -gt 0) {
  foreach ($proj in $tierOverlap) {
    $tierIssues += "Project in BOTH tiers: $proj"
  }
}

# Verify Deterministic tier contains all blocking shard unit projects
foreach ($blockingShard in $BlockingTierShards) {
  $blockingProjects = Get-UnitProjectsFromSlnf $blockingShard
  foreach ($proj in $blockingProjects) {
    if (-not ($detProjects -ccontains $proj)) {
      $tierIssues += "Blocking shard project missing from Deterministic tier: $proj (from $blockingShard)"
    }
  }
}

# Verify AsyncRisk tier contains all advisory shard unit projects
foreach ($advisoryShard in $AdvisoryTierShards) {
  $advisoryProjects = Get-UnitProjectsFromSlnf $advisoryShard
  foreach ($proj in $advisoryProjects) {
    if (-not ($arProjects -ccontains $proj)) {
      $tierIssues += "Advisory shard project missing from AsyncRisk tier: $proj (from $advisoryShard)"
    }
  }
}

# Verify all unit projects are in exactly one tier
$allTierProjects = @($detProjects) + @($arProjects)
foreach ($project in $unitProjects) {
  $slnfRelative = Get-RelativeRepoPath $project.FullName
  $inTier = $allTierProjects | Where-Object { $_ -ceq $slnfRelative }
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
    $relPath = $_.Key
    $slnfRelative = $relPath
    $tier = if ($detProjects -ccontains $slnfRelative) { "blocking" }
            elseif ($arProjects -ccontains $slnfRelative) { "advisory" }
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
