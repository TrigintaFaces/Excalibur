param(
  [string]$Output = "management/reports/dependency-inventory.md"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null

function Write-Section($title) {
  "`n## $title`n" | Out-File -FilePath $Output -Encoding UTF8 -Append
}

function Get-RelativePath {
  param(
    [string]$BasePath,
    [string]$TargetPath
  )
  $base = [System.IO.Path]::GetFullPath($BasePath)
  if (-not $base.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
    $base += [System.IO.Path]::DirectorySeparatorChar
  }
  $baseUri = [System.Uri]$base
  $targetUri = [System.Uri]([System.IO.Path]::GetFullPath($TargetPath))
  return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-ProjectTargetFrameworks {
  param([string]$ProjectPath)
  $xml = [xml](Get-Content -Raw -- $ProjectPath)
  $values = @()

  foreach ($node in @($xml.SelectNodes('//PropertyGroup/TargetFrameworks'))) {
    if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
      $values += $node.InnerText.Trim()
    }
  }
  foreach ($node in @($xml.SelectNodes('//PropertyGroup/TargetFramework'))) {
    if ($node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
      $values += $node.InnerText.Trim()
    }
  }

  $values = @($values | Where-Object { $_ } | Sort-Object -Unique)
  if (@($values).Count -eq 0) {
    return '(unspecified)'
  }
  return ($values -join ';')
}

"# Repository Inventory and Dependency Audit`nGenerated: $(Get-Date -Format o)" | Out-File -FilePath $Output -Encoding UTF8

Write-Section "Solutions and Projects"
Get-ChildItem -Recurse -File -Include *.sln,*.csproj |
  ForEach-Object { "- ``$(Get-RelativePath -BasePath (Get-Location).Path -TargetPath $_.FullName)``" } |
  Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "Target Frameworks"
Get-ChildItem -Recurse -File -Include *.csproj |
  ForEach-Object {
    $tfm = Get-ProjectTargetFrameworks -ProjectPath $_.FullName
    "- ``$($_.FullName)`` → $tfm"
  } | Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "Dependency Trees (transitive)"
try {
  $csprojs = Get-ChildItem -Recurse -File -Include *.csproj
  foreach ($proj in $csprojs) {
    "### $($proj.FullName)" | Out-File -FilePath $Output -Encoding UTF8 -Append
    dotnet list $proj.FullName package --include-transitive |
      Out-File -FilePath $Output -Encoding UTF8 -Append
  }
} catch {
  "- dotnet list package failed: $($_.Exception.Message)" | Out-File -FilePath $Output -Encoding UTF8 -Append
}

Write-Section "LOC Summary (.cs)"
$files = Get-ChildItem -Recurse -File -Include *.cs
$total = 0
foreach ($f in $files) {
  $lines = (Get-Content -ReadCount 0 -- $f.FullName).Length
  $total += $lines
}
"- Files: $($files.Count)" | Out-File -FilePath $Output -Encoding UTF8 -Append
"- Total LOC: $total" | Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "End of Report"

