param(
  [string]$OutDir = "TrimAotAuditReport"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$csprojs = Get-ChildItem -Recurse -File -Include *.csproj | Where-Object { $_.FullName -notmatch '\\tests\\' }
$violations = @()
foreach ($proj in $csprojs) {
  $name = [IO.Path]::GetFileNameWithoutExtension($proj.Name)
  $log = Join-Path $OutDir ("${name}.log")
  Write-Host "Auditing trim for $($proj.FullName)"
  dotnet publish $proj.FullName -c Release -p:PublishTrimmed=true -p:TrimmerDefaultAction=link 2>&1 | Tee-Object -FilePath $log | Out-Null

  # Scan for common trim/AOT warnings (IL*), RequiresUnreferencedCode, DynamicDependency notes
  $content = Get-Content -Raw -ErrorAction SilentlyContinue -- $log
  if ($null -ne $content -and ($content -match '(?im)\bwarning\s+IL\d+' -or $content -match '(?im)RequiresUnreferencedCode' -or $content -match '(?im)Trim(mer|)\s+warning')) {
    $violations += $name
  }
}

# Check for missing JsonSerializerContext usage in shipping projects
$jsonViolations = @()
$srcFiles = Get-ChildItem -Recurse -File -Include *.cs -Path src | Where-Object { $_.FullName -notmatch '\\(Tests|Benchmarks|obj|bin)\\' }
foreach ($file in $srcFiles) {
  $content = Get-Content -Raw -ErrorAction SilentlyContinue -- $file.FullName
  if ($null -eq $content) { continue }

  # Detect JsonSerializer.Serialize/Deserialize without a context parameter
  # Pattern: JsonSerializer.(Serialize|Deserialize) NOT followed by a JsonSerializerContext or TypeInfo parameter
  if ($content -match 'JsonSerializer\.(Serialize|Deserialize)\s*(<[^>]+>)?\s*\([^)]*JsonSerializerOptions' -and
      $content -notmatch 'JsonSerializer\.(Serialize|Deserialize)\s*(<[^>]+>)?\s*\([^)]*JsonSerializerContext') {
    $relativePath = $file.FullName.Replace((Get-Location).Path, '').TrimStart('\/')
    $jsonViolations += $relativePath
  }
}

# Write a simple summary
$summary = Join-Path $OutDir 'summary.md'
"# Trim/AOT Audit Summary`n" | Out-File $summary -Encoding UTF8
if ($violations.Count -eq 0) {
  "No trim/AOT warnings detected across scanned projects." | Out-File $summary -Append -Encoding UTF8
} else {
  "Warnings detected for:" | Out-File $summary -Append -Encoding UTF8
  foreach ($n in $violations) { "- $n" | Out-File $summary -Append -Encoding UTF8 }
}

if ($jsonViolations.Count -gt 0) {
  "`n## Missing JsonSerializerContext Usage`n" | Out-File $summary -Append -Encoding UTF8
  "The following files use JsonSerializer with JsonSerializerOptions instead of a source-generated context:" | Out-File $summary -Append -Encoding UTF8
  foreach ($f in $jsonViolations) { "- $f" | Out-File $summary -Append -Encoding UTF8 }
} else {
  "`nNo missing JsonSerializerContext usages detected." | Out-File $summary -Append -Encoding UTF8
}

$enforce = ($env:TRIM_ENFORCE -and $env:TRIM_ENFORCE.ToString().ToLowerInvariant() -eq 'true')
if ($enforce -and $violations.Count -gt 0) {
  Write-Error "TRIM_ENFORCE=true and trim/AOT warnings detected for: $($violations -join ', ')"
  exit 1
}
elseif ($violations.Count -gt 0) {
  Write-Warning "Trim/AOT warnings detected (report-only). Set TRIM_ENFORCE=true to fail."
}

Write-Host "Trim/AOT audit logs under $OutDir"
exit 0
