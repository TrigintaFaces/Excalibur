param(
  [string]$OutDir = "PackReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ShippingSolutionFilter = Join-Path $RepoRoot 'eng/ci/shards/ShippingOnly.slnf'

# Pack
dotnet pack --configuration Release --no-build --output $OutDir | Tee-Object -FilePath (Join-Path $OutDir 'pack.log') | Out-Null

# Validate csproj metadata hints
$csprojs = @()
if (Test-Path $ShippingSolutionFilter) {
  $filter = Get-Content -Raw -- $ShippingSolutionFilter | ConvertFrom-Json
  $csprojs = foreach ($projPath in $filter.solution.projects) {
    $fullPath = Join-Path $RepoRoot $projPath
    if (Test-Path $fullPath) {
      Get-Item $fullPath
    }
  }
}
else {
  $csprojs = Get-ChildItem -Path (Join-Path $RepoRoot 'src') -Recurse -File -Include *.csproj
}

function Get-Prop([xml]$xml, [string]$name){
  $path = "//Project/PropertyGroup/$name"
  $nodes = $xml.SelectNodes($path)
  if ($nodes -and $nodes.Count -gt 0) { return [string]$nodes[0].InnerText }
  return $null
}

function Try-ReadProjectXml([string]$path) {
  try {
    return [xml](Get-Content -Raw -- $path)
  }
  catch {
    Write-Warning "Skipping non-project or malformed XML file: $path"
    return $null
  }
}

$meta = foreach ($proj in $csprojs) {
  $xml = Try-ReadProjectXml $proj.FullName
  if ($null -eq $xml -or $null -eq $xml.DocumentElement -or $xml.DocumentElement.Name -ne 'Project') {
    continue
  }
  $pubRepo = Get-Prop $xml 'PublishRepositoryUrl'
  $embedSrc = Get-Prop $xml 'EmbedUntrackedSources'
  $ciBuild = Get-Prop $xml 'ContinuousIntegrationBuild'
  $licExp  = Get-Prop $xml 'PackageLicenseExpression'
  $licFile = Get-Prop $xml 'PackageLicenseFile'
  [pscustomobject]@{
    Project = $proj.FullName
    PublishRepositoryUrl = [bool]([string]::IsNullOrWhiteSpace($pubRepo) -eq $false)
    EmbedUntrackedSources = [bool]([string]::IsNullOrWhiteSpace($embedSrc) -eq $false)
    ContinuousIntegrationBuild = [bool]([string]::IsNullOrWhiteSpace($ciBuild) -eq $false)
    PackageLicenseExpression = [string]$licExp
    PackageLicenseFile = [string]$licFile
  }
}
$meta | ConvertTo-Json -Depth 5 | Out-File -FilePath (Join-Path $OutDir 'csproj-metadata.json') -Encoding UTF8

# Summarize produced packages
Get-ChildItem -Path $OutDir -Filter *.nupkg | Select-Object FullName,Length,CreationTime | ConvertTo-Json | Out-File -FilePath (Join-Path $OutDir 'packages.json') -Encoding UTF8
Get-ChildItem -Path $OutDir -Filter *.snupkg | Select-Object FullName,Length,CreationTime | ConvertTo-Json | Out-File -FilePath (Join-Path $OutDir 'symbols.json') -Encoding UTF8

<# Simple validation and optional enforcement #>
$issues = @()
# Metadata expectations commonly required for SourceLink/CI builds
foreach ($m in $meta) {
  if (-not $m.PublishRepositoryUrl) { $issues += ("{0}: PublishRepositoryUrl=false" -f $m.Project) }
  if (-not $m.EmbedUntrackedSources) { $issues += ("{0}: EmbedUntrackedSources=false" -f $m.Project) }
  if (-not $m.ContinuousIntegrationBuild) { $issues += ("{0}: ContinuousIntegrationBuild=false" -f $m.Project) }
  if ([string]::IsNullOrWhiteSpace($m.PackageLicenseExpression) -and [string]::IsNullOrWhiteSpace($m.PackageLicenseFile)) { $issues += ("{0}: No license metadata (PackageLicenseExpression/File)" -f $m.Project) }
}

# Ensure symbol packages exist for produced nupkgs
$nupkgs = Get-ChildItem -Path $OutDir -Filter *.nupkg -ErrorAction SilentlyContinue
foreach ($pkg in $nupkgs) {
  $sym = [IO.Path]::ChangeExtension($pkg.FullName, '.snupkg')
  if (-not (Test-Path $sym)) { $issues += ("{0}: Missing matching .snupkg" -f $pkg.Name) }
}

# Write summary
$summary = Join-Path $OutDir 'summary.md'
"# Pack Validation Summary`n" | Out-File $summary -Encoding UTF8
if ($issues.Count -eq 0) {
  "No packaging issues detected." | Out-File $summary -Append -Encoding UTF8
} else {
  "Issues detected:" | Out-File $summary -Append -Encoding UTF8
  foreach ($i in $issues) { "- $i" | Out-File $summary -Append -Encoding UTF8 }
}

$enforce = ($env:PACK_ENFORCE -and $env:PACK_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
if ($enforce -and $issues.Count -gt 0) {
  Write-Error "PACK_ENFORCE=true and pack validation issues detected. See $summary"
  exit 1
}
elseif ($issues.Count -gt 0) {
  Write-Warning "Pack validation issues detected (report-only). Set PACK_ENFORCE=true to fail."
}

Write-Host "Pack report generated in $OutDir"
exit 0
