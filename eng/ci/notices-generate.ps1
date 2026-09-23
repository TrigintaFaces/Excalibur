param(
  [string]$Output = "THIRD-PARTY-NOTICES.md"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SolutionPath = Join-Path $RepoRoot 'Excalibur.sln'

function Get-PackageVersionsFromDirectoryProps {
  param([string]$Path)
  if (!(Test-Path $Path)) { return @() }
  $xml = [xml](Get-Content -Raw -- $Path)
  $nodes = @($xml.Project.ItemGroup.PackageVersion)
  foreach ($n in $nodes) {
    if ($n.Include -and $n.Version) {
      [pscustomobject]@{ Id = [string]$n.Include; Version = [string]$n.Version }
    }
  }
}

function Get-PackageReferencesFromCsproj {
  param([string]$Path)
  try {
    $xml = [xml](Get-Content -Raw -- $Path)
  }
  catch {
    Write-Warning "Skipping non-project or malformed XML file: $Path"
    return @()
  }

  if ($null -eq $xml.DocumentElement -or $xml.DocumentElement.Name -ne 'Project') {
    Write-Warning "Skipping file without a valid <Project> root: $Path"
    return @()
  }

  $nodes = $xml.SelectNodes('//PackageReference')
  foreach ($n in $nodes) {
    $id = ''
    $ver = ''
    $idAttr = $n.Attributes.GetNamedItem('Include')
    if ($idAttr) { $id = [string]$idAttr.Value }
    $verAttr = $n.Attributes.GetNamedItem('Version')
    if ($verAttr) { $ver = [string]$verAttr.Value }
    else {
      $verNode = $n.SelectSingleNode('Version')
      if ($verNode -and $verNode.InnerText) { $ver = [string]$verNode.InnerText }
    }

    # Skip build-time-only / non-redistributed dependencies (PrivateAssets="all").
    # Their assets do not flow to consumers (analyzers, source generators, build
    # tooling), so they do not belong in a redistribution NOTICES file.
    $priv = ''
    $privAttr = $n.Attributes.GetNamedItem('PrivateAssets')
    if ($privAttr) { $priv = [string]$privAttr.Value }
    else {
      $privNode = $n.SelectSingleNode('PrivateAssets')
      if ($privNode -and $privNode.InnerText) { $priv = [string]$privNode.InnerText }
    }
    if ($priv.Trim().ToLowerInvariant() -eq 'all') { continue }

    if ($id) { [pscustomobject]@{ Id = $id; Version = $ver } }
  }
}

function Get-CentralTransitiveFromLock {
  # A packed nuspec declares MORE than the project file references. With
  # CentralPackageTransitivePinningEnabled=true (Directory.Packages.props), NuGet promotes every
  # centrally-pinned TRANSITIVE dependency to a pinned direct reference, and pack writes those into
  # the nuspec's dependency group. A consumer's restore therefore resolves them, and a procurement
  # scan of the published package finds them -- so the inventory must list them.
  #
  # The committed packages.lock.json records exactly that promotion as type "CentralTransitive"
  # (plain "Transitive" entries are NOT promoted and are NOT declared, so they are excluded here).
  # The lock files are committed and version-controlled, so this stays deterministic and
  # machine-independent: no restore, no package cache, no pack run.
  param([string]$Path)

  if (!(Test-Path $Path)) { return @() }
  try {
    $json = Get-Content -Raw -- $Path | ConvertFrom-Json
  }
  catch {
    Write-Warning "Skipping malformed lock file: $Path"
    return @()
  }

  $depsProp = $json.PSObject.Properties['dependencies']
  if (!$depsProp) { return @() }

  foreach ($tfm in $depsProp.Value.PSObject.Properties) {
    foreach ($pkg in $tfm.Value.PSObject.Properties) {
      $typeProp = $pkg.Value.PSObject.Properties['type']
      if (!$typeProp -or [string]$typeProp.Value -ne 'CentralTransitive') { continue }
      $ver = ''
      $resolvedProp = $pkg.Value.PSObject.Properties['resolved']
      if ($resolvedProp) { $ver = [string]$resolvedProp.Value }
      [pscustomobject]@{ Id = [string]$pkg.Name; Version = $ver }
    }
  }
}

function Get-SolutionProjectPaths {
  param([string]$Path)

  if (!(Test-Path $Path)) { return @() }

  $projectRegex = 'Project\(".*?"\)\s*=\s*".*?",\s*"(.*?\.csproj)"'
  $solutionDir = Split-Path -Parent $Path

  return Get-Content -- $Path |
    Select-String -Pattern $projectRegex |
    ForEach-Object {
      $relativePath = $_.Matches[0].Groups[1].Value -replace '\\', [IO.Path]::DirectorySeparatorChar
      Join-Path $solutionDir $relativePath
    } |
    Where-Object { Test-Path $_ }
}

$central = Get-PackageVersionsFromDirectoryProps (Join-Path $RepoRoot 'Directory.Packages.props')
# Only scan src/ projects — these are the shipping packages consumers receive via NuGet.
# Test, benchmark, and load-test dependencies are not redistributed and should not appear.
$projectPaths = Get-ChildItem -Path (Join-Path $RepoRoot 'src') -Recurse -File -Filter *.csproj |
  Where-Object { $_.FullName -notmatch '[\\/](obj|bin|templates)[\\/]' } |
  Select-Object -ExpandProperty FullName

$projRefs = $projectPaths | ForEach-Object { Get-PackageReferencesFromCsproj $_ }

# ...plus the transitive dependencies that central transitive pinning promotes into each packed
# nuspec. Read from the lock file sitting beside each scanned project, so the set of lock files is
# bound to the same project set as the references above.
$lockRefs = $projectPaths |
  ForEach-Object { Join-Path (Split-Path -Parent $_) 'packages.lock.json' } |
  ForEach-Object { Get-CentralTransitiveFromLock $_ }

# Licences come from a committed map rather than from the restored package cache. A cache read would
# make this file's content depend on what a given machine happened to have restored, and the CI gate
# diffs the generated file byte-for-byte against the committed one -- so an unrestored package would
# fail the gate on some machines and not others. The map also makes adding a dependency an explicit
# licence decision: a package with no entry fails this script rather than being quietly listed with
# an empty licence cell.
$licenseMapPath = Join-Path $PSScriptRoot 'package-licenses.json'
if (!(Test-Path $licenseMapPath)) {
  Write-Error "Licence map not found at $licenseMapPath. Cannot state a licence for any package."
  exit 1
}
$licenseMap = Get-Content -Raw -- $licenseMapPath | ConvertFrom-Json

# Build a lookup of central package versions
$centralVersions = @{}
foreach ($p in $central) { $centralVersions[$p.Id] = $p.Version }

# Only include packages actually referenced in src/ projects.
# Resolve versions from explicit csproj Version attributes, falling back to central management.
$all = @{}
foreach ($r in @($projRefs) + @($lockRefs)) {
  # First-party packages are not third-party notices. Metapackages reference their siblings by
  # PackageReference, which is why 22 Excalibur.* rows were listed here carrying a blank version --
  # they are not centrally pinned, because their version is the build's.
  if ($r.Id -like 'Excalibur.*') { continue }

  # Resolve the effective version for this reference (explicit, else central pin).
  $ver = if ($r.Version) { $r.Version }
         elseif ($centralVersions.ContainsKey($r.Id)) { $centralVersions[$r.Id] }
         else { '' }

  if (-not $all.ContainsKey($r.Id)) {
    $all[$r.Id] = $ver
  }
  elseif ($all[$r.Id] -ne $ver) {
    # Deterministic conflict resolution (avoids OS-dependent enumeration order):
    # the centrally managed pin is the repository's canonical version, so it wins;
    # otherwise keep the higher ordinal version string as a stable tie-break.
    if ($centralVersions.ContainsKey($r.Id)) { $all[$r.Id] = $centralVersions[$r.Id] }
    elseif ([string]::CompareOrdinal($ver, $all[$r.Id]) -gt 0) { $all[$r.Id] = $ver }
  }
}

$lines = @()
$lines += "# THIRD-PARTY NOTICES"
$lines += ""
$lines += "This file lists every third-party package that the shipping projects in this repository"
$lines += "declare: the packages their project files reference, plus the transitive dependencies that"
$lines += "central package pinning promotes into each published package's own dependency list. It is"
$lines += "generated from those project files and their committed lock files, so it matches what a"
$lines += "consumer's restore resolves. Licenses remain with their respective owners."
$lines += ""
$lines += "Licenses are recorded per package id in ``eng/ci/package-licenses.json``. Most are SPDX"
$lines += "expressions taken from the package's own metadata. Where a package ships its terms as a"
$lines += "license file or a URL instead of an SPDX expression, the entry states those terms in words,"
$lines += "and is marked PROPRIETARY when they are not an OSI-approved open-source license."
$lines += ""
$lines += "| Package | Version | License |"
$lines += "|---------|---------|---------|"
$sortedKeys = [string[]]$all.Keys
[System.Array]::Sort($sortedKeys, [System.StringComparer]::Ordinal)
$unlicensed = @()
foreach ($k in $sortedKeys) {
  $v = $all[$k]
  $licenseProp = $licenseMap.PSObject.Properties[$k]
  if ($null -eq $licenseProp -or [string]::IsNullOrWhiteSpace([string]$licenseProp.Value)) {
    $unlicensed += $k
    continue
  }
  $lines += "| $k | $v | $([string]$licenseProp.Value) |"
}

# Fail loud rather than emit a row with an empty licence cell. A notices file that lists a package
# without stating its terms is the defect this column exists to close.
if ($unlicensed.Count -gt 0) {
  Write-Error ("No licence recorded for: " + ($unlicensed -join ', ') + ". Add each to eng/ci/package-licenses.json, reading the licence the package actually ships rather than the vendor's usual one.")
  exit 1
}

# Native assets reach a consumer through a package that is resolved transitively rather than declared,
# so it cannot appear in the table above -- but a procurement scan of the consumer's restore graph will
# find it, and it carries no SPDX expression of its own. Emitted only when the managed client that pulls
# it is actually in the set.
#
# MAINTAINER NOTE: the component list below was read from librdkafka's LICENSES.txt at the tag matching
# the pinned Confluent.Kafka version, and Confluent.Kafka's nuspec declares librdkafka.redist at that
# same version. When the Confluent.Kafka pin moves, re-read LICENSES.txt at the new tag and update the
# component list; do not assume it is unchanged.
if ($all.ContainsKey('Confluent.Kafka')) {
  $kafkaVersion = $all['Confluent.Kafka']
  $lines += ""
  $lines += "## Native components reached through Confluent.Kafka"
  $lines += ""
  $lines += "The ``Confluent.Kafka`` package contains managed assemblies only. The native Kafka client it"
  $lines += "calls arrives as a separate NuGet package, ``librdkafka.redist``, which your restore resolves"
  $lines += "transitively -- no package in this repository declares it, which is why it has no row above."
  $lines += ""
  $lines += "``librdkafka.redist`` ships prebuilt native binaries and states no SPDX license expression."
  $lines += "Its terms are published as a single ``LICENSES.txt`` covering librdkafka itself, which is"
  $lines += "BSD-2-Clause, together with the third-party components built into those binaries. At the"
  $lines += "version this repository resolves, that file names fourteen: cjson, crc32c, fnv1a,"
  $lines += "hdrhistogram, lz4, murmur2, nanopb, opentelemetry, pycrc, queue, regexp, snappy, tinycthread"
  $lines += "and wingetopt. Read it at the version your build resolves, not at the package's own license"
  $lines += "link, which points at a moving branch:"
  $lines += ""
  $lines += "    https://github.com/confluentinc/librdkafka/blob/v$kafkaVersion/LICENSES.txt"
}

# Use explicit UTF-8 without BOM for stable cross-platform diffs in CI.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Output), (($lines -join "`n") + "`n"), $utf8NoBom)
Write-Host "Wrote $Output with $($all.Count) entries"
