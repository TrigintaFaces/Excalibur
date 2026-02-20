param(
  [string]$OutDir = "OssMetadataReport",
  [string]$Catalog = "management/package-ids.yaml"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Parse-Catalog {
  param([string]$Path)
  $text = Get-Content -Raw -- $Path
  $entries = @()
  $current = @{}
  foreach ($line in ($text -split "`n")) {
    $l = $line.Trim()
    if ($l -like '- id:*') { $current = @{ id = $l.Split(':',2)[1].Trim(); path = '' } }
    elseif ($l -like 'path:*') { $current.path = $l.Split(':',2)[1].Trim(); if ($current.id -and $current.path) { $entries += $current; $current = $null } }
  }
  return $entries
}

function Audit-Csproj {
  param([string]$Path)
  $result = [ordered]@{ Path = $Path; PackageId = ''; Readme = $false; LicenseOk = $false; RepoUrl = $false; ProjectUrl = $false }
  $xml = [xml](Get-Content -Raw -- $Path)
  $pg = $xml.SelectSingleNode('//PropertyGroup')
  $pid = $xml.SelectSingleNode('//PackageId')
  $result.PackageId = if ($pid) { $pid.InnerText } else { '' }
  $readme = $xml.SelectSingleNode('//PackageReadmeFile')
  $result.Readme = [bool]($readme -and $readme.InnerText)
  $licExpr = $xml.SelectSingleNode('//PackageLicenseExpression')
  $licFile = $xml.SelectSingleNode('//PackageLicenseFile')
  $result.LicenseOk = [bool]($licExpr -or $licFile)
  $repo = $xml.SelectSingleNode('//RepositoryUrl')
  $proj = $xml.SelectSingleNode('//PackageProjectUrl')
  $result.RepoUrl = [bool]($repo -and $repo.InnerText)
  $result.ProjectUrl = [bool]($proj -and $proj.InnerText)
  return [pscustomobject]$result
}

$entries = Parse-Catalog -Path $Catalog
$report = @()
$fail = $false
foreach ($e in $entries) {
  $p = $e.path.Trim('"')
  if (!(Test-Path $p)) { $report += [pscustomobject]@{ Path=$p; Error='missing' }; $fail=$true; continue }
  $r = Audit-Csproj -Path $p
  $report += $r
  if (-not $r.PackageId) { $fail = $true }
  if (-not $r.Readme) { $fail = $true }
  if (-not $r.LicenseOk) { $fail = $true }
  if (-not $r.RepoUrl -or -not $r.ProjectUrl) { $fail = $true }
}

$json = $report | ConvertTo-Json -Depth 5
$json | Out-File -FilePath (Join-Path $OutDir 'oss-metadata.json') -Encoding UTF8
if ($fail) { Write-Error 'OSS metadata audit failed; see oss-metadata.json' } else { Write-Host 'OSS metadata audit passed' }

