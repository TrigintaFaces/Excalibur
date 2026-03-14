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
$projectPaths = Get-SolutionProjectPaths $SolutionPath
if ($projectPaths.Count -eq 0) {
  $projectPaths = Get-ChildItem -Path (Join-Path $RepoRoot 'src') -Recurse -File -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin|templates)[\\/]' } |
    Select-Object -ExpandProperty FullName
}

$projRefs = $projectPaths | ForEach-Object { Get-PackageReferencesFromCsproj $_ }

# Merge by Id, prefer explicit versions then central
$all = @{}
foreach ($p in $central) { $all[$p.Id] = $p.Version }
foreach ($r in $projRefs) { if ($r.Version) { $all[$r.Id] = $r.Version } elseif ($all.ContainsKey($r.Id)) { } else { $all[$r.Id] = '' } }

$lines = @()
$lines += "# THIRD-PARTY NOTICES"
$lines += ""
$lines += "This file lists third-party packages referenced by this repository."
$lines += "It is generated from project files; licenses remain with their respective owners."
$lines += ""
$lines += "| Package | Version |"
$lines += "|---------|---------|"
foreach ($k in ($all.Keys | Sort-Object)) {
  $v = $all[$k]
  $lines += "| $k | $v |"
}

# Use explicit UTF-8 without BOM for stable cross-platform diffs in CI.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Output), (($lines -join "`n") + "`n"), $utf8NoBom)
Write-Host "Wrote $Output with $($all.Count) entries"
