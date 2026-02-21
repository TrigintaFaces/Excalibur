param(
  [string]$OutDir = "DependencyAuditReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$jsonPath = Join-Path $OutDir 'vulnerabilities.json'
$sarifPath = Join-Path $OutDir 'vulnerabilities.sarif'

$results = @()
$csprojs = Get-ChildItem -Recurse -File -Include *.csproj
foreach ($proj in $csprojs) {
  try {
    $output = dotnet list $proj.FullName package --vulnerable --include-transitive
    $results += [pscustomobject]@{ Project = $proj.FullName; Output = ($output | Out-String) }
  } catch {
    $results += [pscustomobject]@{ Project = $proj.FullName; Output = "FAILED: $($_.Exception.Message)" }
  }
}
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

# Emit minimal SARIF 2.1.0 with findings per line containing 'Vulnerable'
$runs = @()
foreach ($r in $results) {
  $lines = $r.Output -split "`n"
  $findings = @()
  $idx = 0
  foreach ($line in $lines) {
    if ($line -match '(?i)vulnerable') {
      $findings += @{ level = 'warning'; message = @{ text = $line.Trim() }; locations = @(@{ physicalLocation = @{ artifactLocation = @{ uri = $r.Project }; region = @{ startLine = $idx + 1 } } }) }
    }
    $idx++
  }
  $runs += @{ tool = @{ driver = @{ name = 'dotnet list package'; informationUri = 'https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package' } }; results = $findings }
}
$sarif = @{ version = '2.1.0'; '$schema' = 'https://json.schemastore.org/sarif-2.1.0.json'; runs = $runs }
$sarif | ConvertTo-Json -Depth 8 | Out-File -FilePath $sarifPath -Encoding UTF8
Write-Host "Wrote $jsonPath and $sarifPath"

