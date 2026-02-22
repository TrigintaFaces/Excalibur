param(
  [string]$OutDir = "SerializationBoundaryReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$violations = @()
$enforce = [bool]::Parse([string]::IsNullOrWhiteSpace($env:R014_ENFORCE) ? 'False' : $env:R014_ENFORCE)

function Test-DirectoryUsesSystemTextJson {
  param([Parameter(Mandatory = $true)][string]$DirectoryPath)

  $rg = Get-Command rg -ErrorAction SilentlyContinue
  if ($rg) {
    $matches = rg --no-heading -n "^\s*using\s+System\.Text\.Json" $DirectoryPath 2>$null
    return -not [string]::IsNullOrWhiteSpace(($matches -join [Environment]::NewLine))
  }

  $csFiles = Get-ChildItem -Path $DirectoryPath -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[/\\](obj|bin)[/\\]' }
  if (-not $csFiles) {
    return $false
  }

  $matches = $csFiles | Select-String -Pattern '^\s*using\s+System\.Text\.Json' -ErrorAction SilentlyContinue
  return @($matches).Count -gt 0
}

# Policy: Excalibur.Dispatch MUST NOT reference System.Text.Json; public boundary projects SHOULD use STJ.
$coreCsprojs = Get-ChildItem -Recurse -File -Include Excalibur.Dispatch.csproj
foreach ($proj in $coreCsprojs) {
  $xml = [xml](Get-Content -Raw -- $proj.FullName)

  $packageRefs = @($xml.SelectNodes('/Project/ItemGroup/PackageReference'))
  $hasStj = @($packageRefs | Where-Object { [string]$_.Attributes['Include']?.Value -like 'System.Text.Json*' }).Count -gt 0
  if ($hasStj) {
    $violations += [pscustomobject]@{ Project = $proj.FullName; Issue = 'Excalibur.Dispatch references System.Text.Json (forbidden)'}
  }
  if (Test-DirectoryUsesSystemTextJson -DirectoryPath (Split-Path $proj.FullName -Parent)) {
    $violations += [pscustomobject]@{ Project = $proj.FullName; Issue = 'Excalibur.Dispatch has using System.Text.Json (forbidden)'}
  }
}

$json = $violations | ConvertTo-Json -Depth 5
if (-not $json) { $json = '[]' }
$outJson = Join-Path $OutDir 'serialization-boundary-violations.json'
$json | Out-File -FilePath $outJson -Encoding UTF8
Write-Host "Wrote $outJson"

if ($violations.Count -gt 0 -and $enforce) { exit 1 } else { exit 0 }
