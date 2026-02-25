#!/usr/bin/env pwsh
param(
    [string]$BaselinesDir = "eng/api",
    [string]$SrcDir = "src",
    [string]$OutputDir = "ApiCompatReport"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "🔎 Public API compatibility check (report-only unless API_ENFORCE=true)"

# Map baseline file name to source project current shipped file path
$baselineFiles = Get-ChildItem -Path $BaselinesDir -Filter "*.PublicAPI.Shipped.txt" -File -ErrorAction SilentlyContinue
if (-not $baselineFiles) {
    Write-Warning "No baseline files found in '$BaselinesDir'. Skipping."
    exit 0
}

$diffs = @()
foreach ($bl in $baselineFiles) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($bl.Name) # e.g., Excalibur.Dispatch.Core.PublicAPI.Shipped
    # Assembly name is the portion before .PublicAPI.Shipped
    $assembly = $name -replace "\.PublicAPI\.Shipped$", ''

    $srcShippedPath = Join-Path $SrcDir $assembly | Join-Path -ChildPath "PublicAPI.Shipped.txt"
    if (-not (Test-Path $srcShippedPath)) {
        # Try nested folder matching src/<Assembly>/<Assembly>.csproj pattern
        $candidates = Get-ChildItem -Path $SrcDir -Recurse -Filter "PublicAPI.Shipped.txt" -File | Where-Object { $_.FullName -match "\\$assembly\\" }
        if ($candidates) { $srcShippedPath = $candidates[0].FullName }
    }

    if (-not (Test-Path $srcShippedPath)) {
        Write-Warning "No current shipped baseline found for assembly '$assembly' under src/. Skipping compare."
        continue
    }

    Write-Host "Comparing baseline for '$assembly'"
    $baseline = Get-Content $bl.FullName -Raw
    $current = Get-Content $srcShippedPath -Raw

    $tmpBase = Join-Path $OutputDir "$assembly.baseline.txt"
    $tmpCurr = Join-Path $OutputDir "$assembly.current.txt"
    $diffOut = Join-Path $OutputDir "$assembly.diff.txt"
    Set-Content -Path $tmpBase -Value $baseline -NoNewline
    Set-Content -Path $tmpCurr -Value $current -NoNewline

    # Use built-in fc on Windows or diff on *nix; fallback to PowerShell Compare-Object
    try {
        if ($IsWindows) {
            $proc = Start-Process -FilePath "cmd.exe" -ArgumentList "/c","fc","/n",$tmpBase,$tmpCurr -NoNewWindow -PassThru -Wait -RedirectStandardOutput $diffOut
        }
        else {
            $proc = Start-Process -FilePath "bash" -ArgumentList "-lc","diff -u --label baseline $tmpBase --label current $tmpCurr" -NoNewWindow -PassThru -Wait -RedirectStandardOutput $diffOut
        }
    }
    catch {
        $diff = Compare-Object -ReferenceObject ($baseline -split "`n") -DifferenceObject ($current -split "`n") -IncludeEqual:$false | Out-String
        Set-Content -Path $diffOut -Value $diff
    }

    $diffContent = Get-Content $diffOut -Raw
    if ($diffContent.Trim().Length -gt 0) {
        $diffs += $assembly
    }
}

$summary = Join-Path $OutputDir "summary.md"
"# API Compat Report`n" | Out-File $summary -Encoding UTF8
if ($diffs.Count -eq 0) {
    "All checked assemblies match their baselines." | Out-File $summary -Append -Encoding UTF8
}
else {
    "The following assemblies differ from their baselines:" | Out-File $summary -Append -Encoding UTF8
    foreach ($asm in $diffs) { "- $asm" | Out-File $summary -Append -Encoding UTF8 }
}

$enforce = ($env:API_ENFORCE -and $env:API_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
if ($enforce -and $diffs.Count -gt 0) {
    Write-Error "API_ENFORCE=true and differences detected: $($diffs -join ', ')"
    exit 1
}
else {
    if ($diffs.Count -gt 0) { Write-Warning "Differences detected (report-only). Set API_ENFORCE=true to fail." }
    exit 0
}

