param(
  [string]$Root = "src",
  [switch]$Fix,
  [int]$MaxLogged = 200
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$headerPath = (Resolve-Path (Join-Path $PSScriptRoot '..\license\HEADER.txt')).Path
if (!(Test-Path $headerPath)) { throw "HEADER.txt not found at $headerPath" }
$header = [string](Get-Content -Raw -- $headerPath)
if ([string]::IsNullOrWhiteSpace($header)) { throw "HEADER.txt is empty at $headerPath" }

$enforce = ($env:LICENSE_ENFORCE -and $env:LICENSE_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
$headerLf = $header -replace "`r`n", "`n"

$files = Get-ChildItem -Recurse -File -Include *.cs -Path $Root |
  Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BenchmarkDotNet\.Artifacts)[\\/]' -and
    $_.Name -notmatch '\.(g|g\.i|generated|designer)\.cs$'
  }

$missing = @()
foreach ($f in $files) {
  $content = ''
  try { $content = Get-Content -Raw -- $f.FullName } catch { $content = '' }
  $content = [string]$content
  $contentLf = $content -replace "`r`n", "`n"
  $hasHeader = (-not [string]::IsNullOrEmpty($content)) -and ($content.StartsWith($header) -or $contentLf.StartsWith($headerLf))

  if (-not $hasHeader) {
    $missing += $f.FullName
    if ($Fix) {
      $new = $header + "`n" + $content
      Set-Content -NoNewline -Path $f.FullName -Value $new
    }
  }
}

if ($missing.Count -gt 0 -and -not $Fix) {
  $severity = if ($enforce) { 'error' } else { 'warning' }
  Write-Host "::$severity::Missing license headers in $($missing.Count) file(s)"
  $missing | Select-Object -First $MaxLogged | ForEach-Object { Write-Host "::warning::$_" }
  if ($missing.Count -gt $MaxLogged) {
    Write-Host "::warning::... and $($missing.Count - $MaxLogged) additional files not shown"
  }

  if ($enforce) {
    exit 1
  }
}
Write-Host "License headers verification complete. Missing: $($missing.Count)"
