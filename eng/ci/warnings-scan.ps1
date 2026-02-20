param(
  [string]$OutDir = "WarningsReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$log = Join-Path $OutDir 'build-warnings.log'
dotnet build --configuration Release --no-restore 2>&1 | Tee-Object -FilePath $log | Out-Null

# Extract warnings lines
$warnings = Select-String -Path $log -Pattern "^.*warning\s+[A-Z]{2,}\d+" -SimpleMatch:$false
$warnings | ForEach-Object { $_.Line } | Out-File -FilePath (Join-Path $OutDir 'warnings.txt') -Encoding UTF8

Write-Host "Warnings report generated in $OutDir"

