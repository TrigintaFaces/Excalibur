param(
  [string]$Root = "src",
  [switch]$Fix
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$headerPath = (Resolve-Path (Join-Path $PSScriptRoot '..\license\HEADER.txt')).Path
if (!(Test-Path $headerPath)) { throw "HEADER.txt not found at $headerPath" }
$header = [string](Get-Content -Raw -- $headerPath)
if ([string]::IsNullOrWhiteSpace($header)) { throw "HEADER.txt is empty at $headerPath" }

$files = Get-ChildItem -Recurse -File -Include *.cs -Path $Root |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|BenchmarkDotNet\.Artifacts)\\' }

$missing = @()
foreach ($f in $files) {
  $content = ''
  try { $content = Get-Content -Raw -- $f.FullName } catch { $content = '' }
  $content = [string]$content
  if ([string]::IsNullOrEmpty($content) -or -not $content.StartsWith($header)) {
    $missing += $f.FullName
    if ($Fix) {
      $new = $header + "`n" + $content
      Set-Content -NoNewline -Path $f.FullName -Value $new
    }
  }
}

if ($missing.Count -gt 0 -and -not $Fix) {
  Write-Host "::error::Missing license headers in $($missing.Count) file(s)"
  $missing | ForEach-Object { Write-Host "::warning::$_" }
  exit 1
}
Write-Host "License headers verification complete. Missing: $($missing.Count)"
