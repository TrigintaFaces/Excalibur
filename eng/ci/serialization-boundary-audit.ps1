param(
  [string]$OutDir = "SerializationBoundaryReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$violations = @()
$enforce = [bool]::Parse([string]::IsNullOrWhiteSpace($env:R014_ENFORCE) ? 'False' : $env:R014_ENFORCE)

# Policy: Excalibur.Dispatch MUST NOT reference System.Text.Json; public boundary projects SHOULD use STJ.
$coreCsprojs = Get-ChildItem -Recurse -File -Include Excalibur.Dispatch.csproj
foreach ($proj in $coreCsprojs) {
  $xml = [xml](Get-Content -Raw -- $proj.FullName)
  $hasStj = $xml.Project.ItemGroup.PackageReference | Where-Object { $_.Include -like 'System.Text.Json*' }
  if ($hasStj) {
    $violations += [pscustomobject]@{ Project = $proj.FullName; Issue = 'Excalibur.Dispatch references System.Text.Json (forbidden)'}
  }
  $usings = rg --no-heading -n "^\s*using\s+System\.Text\.Json" (Split-Path $proj.FullName -Parent) 2>$null
  if ($usings) {
    $violations += [pscustomobject]@{ Project = $proj.FullName; Issue = 'Excalibur.Dispatch has using System.Text.Json (forbidden)'}
  }
}

$json = $violations | ConvertTo-Json -Depth 5
if (-not $json) { $json = '[]' }
$outJson = Join-Path $OutDir 'serialization-boundary-violations.json'
$json | Out-File -FilePath $outJson -Encoding UTF8
Write-Host "Wrote $outJson"

if ($violations.Count -gt 0 -and $enforce) { exit 1 } else { exit 0 }
