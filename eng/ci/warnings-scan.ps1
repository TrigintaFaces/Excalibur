param(
  [string]$OutDir = "WarningsReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$enforce = ($env:WARNINGS_ENFORCE -and $env:WARNINGS_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
$log = Join-Path $OutDir 'build-warnings.log'
dotnet build --configuration Release --no-restore 2>&1 | Tee-Object -FilePath $log | Out-Null
$buildExitCode = $LASTEXITCODE

# Extract warnings lines
$warnings = Select-String -Path $log -Pattern "^.*warning\s+[A-Z]{2,}\d+" -SimpleMatch:$false
$warnings | ForEach-Object { $_.Line } | Out-File -FilePath (Join-Path $OutDir 'warnings.txt') -Encoding UTF8

Write-Host "Warnings report generated in $OutDir"

if ($buildExitCode -ne 0) {
  Write-Warning "dotnet build exited with code $buildExitCode while generating warnings report."
}

if ($enforce -and @($warnings).Count -gt 0) {
  Write-Error "WARNINGS_ENFORCE=true and warnings were detected."
  exit 1
}

exit 0

