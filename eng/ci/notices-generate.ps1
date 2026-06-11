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

# Build a lookup of central package versions
$centralVersions = @{}
foreach ($p in $central) { $centralVersions[$p.Id] = $p.Version }

# Only include packages actually referenced in src/ projects.
# Resolve versions from explicit csproj Version attributes, falling back to central management.
$all = @{}
foreach ($r in $projRefs) {
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
$lines += "This file lists third-party packages referenced by this repository."
$lines += "It is generated from project files; licenses remain with their respective owners."
$lines += ""
$lines += "| Package | Version |"
$lines += "|---------|---------|"
$sortedKeys = [string[]]$all.Keys
[System.Array]::Sort($sortedKeys, [System.StringComparer]::Ordinal)
foreach ($k in $sortedKeys) {
  $v = $all[$k]
  $lines += "| $k | $v |"
}

# Use explicit UTF-8 without BOM for stable cross-platform diffs in CI.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Output), (($lines -join "`n") + "`n"), $utf8NoBom)
Write-Host "Wrote $Output with $($all.Count) entries"
