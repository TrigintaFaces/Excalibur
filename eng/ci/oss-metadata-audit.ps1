param(
  [string]$OutDir = "OssMetadataReport",
  # EMPTY BY DEFAULT, and that is the fix rather than an omission. This used to default to
  # "eng/governance/package-ids.yaml", a file that does not exist and that nothing in this repository
  # generates, so every run emitted a WARNING and then silently took the discovery path anyway. A
  # permanent warning on a passing gate is how a real warning stops being read.
  #
  # Discovery is also the STRONGER source, not a degraded one: Discover-CatalogEntries recurses all
  # of src for *.csproj, so it cannot omit a project. A hand-maintained catalog can, and it cannot
  # detect its own omission, because the list IS the definition. That is a failure this repository
  # has already measured and corrected elsewhere. Worse here than elsewhere: this gate audits OSS
  # metadata, so a partial catalog silently NARROWS a compliance audit while still reporting pass.
  #
  # Pass -Catalog explicitly to audit a specific subset. A path that is given and missing is still a
  # warning, because then it IS a mistake.
  [string]$Catalog = ""
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-XmlNodeText {
  param(
    [xml]$Xml,
    [string]$XPath
  )
  $node = $Xml.SelectSingleNode($XPath)
  if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
    return $node.InnerText.Trim()
  }
  return ''
}

function Get-GlobalDefaults {
  param([string]$RepoRoot)
  $defaults = @{
    PackageReadmeFile = ''
    PackageLicenseExpression = ''
    PackageLicenseFile = ''
    RepositoryUrl = ''
    PackageProjectUrl = ''
  }

  $propsPath = Join-Path $RepoRoot 'Directory.Build.props'
  if (-not (Test-Path $propsPath)) {
    return $defaults
  }

  $xml = [xml](Get-Content -Raw -- $propsPath)
  $defaults.PackageReadmeFile = Get-XmlNodeText -Xml $xml -XPath '//PackageReadmeFile'
  $defaults.PackageLicenseExpression = Get-XmlNodeText -Xml $xml -XPath '//PackageLicenseExpression'
  $defaults.PackageLicenseFile = Get-XmlNodeText -Xml $xml -XPath '//PackageLicenseFile'
  $defaults.RepositoryUrl = Get-XmlNodeText -Xml $xml -XPath '//RepositoryUrl'
  $defaults.PackageProjectUrl = Get-XmlNodeText -Xml $xml -XPath '//PackageProjectUrl'
  return $defaults
}

function Parse-Catalog {
  param([string]$Path)
  if (-not (Test-Path $Path)) {
    Write-Warning "Catalog '$Path' was requested but does not exist. Falling back to recursive discovery of src/**/*.csproj."
    return @()
  }

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

function Discover-CatalogEntries {
  param([string]$RepoRoot)
  $entries = @()
  $srcPath = Join-Path $RepoRoot 'src'
  $projects = Get-ChildItem -Path $srcPath -Recurse -File -Filter *.csproj

  foreach ($proj in $projects) {
    $xml = [xml](Get-Content -Raw -- $proj.FullName)
    $isPackable = Get-XmlNodeText -Xml $xml -XPath '//IsPackable'
    if ($isPackable -eq 'false') {
      continue
    }

    $packageId = Get-XmlNodeText -Xml $xml -XPath '//PackageId'
    if (-not $packageId) {
      $packageId = [System.IO.Path]::GetFileNameWithoutExtension($proj.Name)
    }

    $entries += @{
      id = $packageId
      path = $proj.FullName
    }
  }

  return $entries
}

function Audit-Csproj {
  param(
    [string]$Path,
    [hashtable]$Defaults
  )
  $result = [ordered]@{ Path = $Path; PackageId = ''; Readme = $false; LicenseOk = $false; RepoUrl = $false; ProjectUrl = $false }
  $xml = [xml](Get-Content -Raw -- $Path)

  $packageId = Get-XmlNodeText -Xml $xml -XPath '//PackageId'
  if (-not $packageId) {
    $packageId = [System.IO.Path]::GetFileNameWithoutExtension((Split-Path -Leaf $Path))
  }
  $result.PackageId = $packageId

  $readme = Get-XmlNodeText -Xml $xml -XPath '//PackageReadmeFile'
  if (-not $readme) { $readme = $Defaults.PackageReadmeFile }
  $result.Readme = [bool]$readme

  $licExpr = Get-XmlNodeText -Xml $xml -XPath '//PackageLicenseExpression'
  if (-not $licExpr) { $licExpr = $Defaults.PackageLicenseExpression }
  $licFile = Get-XmlNodeText -Xml $xml -XPath '//PackageLicenseFile'
  if (-not $licFile) { $licFile = $Defaults.PackageLicenseFile }
  $result.LicenseOk = [bool]($licExpr -or $licFile)

  $repo = Get-XmlNodeText -Xml $xml -XPath '//RepositoryUrl'
  if (-not $repo) { $repo = $Defaults.RepositoryUrl }
  $proj = Get-XmlNodeText -Xml $xml -XPath '//PackageProjectUrl'
  if (-not $proj) { $proj = $Defaults.PackageProjectUrl }
  $result.RepoUrl = [bool]$repo
  $result.ProjectUrl = [bool]$proj
  return [pscustomobject]$result
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$defaults = Get-GlobalDefaults -RepoRoot $repoRoot
# Only consult a catalog when one was explicitly asked for. Test-Path on an empty string is a
# parameter-binding error, so this guard is load-bearing and not defensive decoration.
$entries = if ([string]::IsNullOrWhiteSpace($Catalog)) { @() } else { @(Parse-Catalog -Path $Catalog) }
if (@($entries).Count -eq 0) {
  $entries = Discover-CatalogEntries -RepoRoot $repoRoot
}

$report = @()
$fail = $false
foreach ($e in $entries) {
  $p = $e.path.Trim('"')
  if (-not [System.IO.Path]::IsPathRooted($p)) {
    $p = Join-Path $repoRoot $p
  }

  if (!(Test-Path $p)) { $report += [pscustomobject]@{ Path=$p; Error='missing' }; $fail=$true; continue }
  $r = Audit-Csproj -Path $p -Defaults $defaults
  $report += $r
  if (-not $r.PackageId) { $fail = $true }
  if (-not $r.Readme) { $fail = $true }
  if (-not $r.LicenseOk) { $fail = $true }
  if (-not $r.RepoUrl -or -not $r.ProjectUrl) { $fail = $true }
}

$json = $report | ConvertTo-Json -Depth 5
$json | Out-File -FilePath (Join-Path $OutDir 'oss-metadata.json') -Encoding UTF8
if ($fail) { Write-Error 'OSS metadata audit failed; see oss-metadata.json' } else { Write-Host 'OSS metadata audit passed' }

