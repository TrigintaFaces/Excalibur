param(
  [string]$Output = "THIRD-PARTY-NOTICES.md"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
  $xml = [xml](Get-Content -Raw -- $Path)
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

$central = Get-PackageVersionsFromDirectoryProps (Join-Path (Get-Location) 'Directory.Packages.props')
$projRefs = Get-ChildItem -Recurse -File -Include *.csproj | ForEach-Object { Get-PackageReferencesFromCsproj $_.FullName }

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

Set-Content -Path $Output -Value ($lines -join "`n") -Encoding UTF8
Write-Host "Wrote $Output with $($all.Count) entries"
