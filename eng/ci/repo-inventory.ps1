param(
  [string]$Output = "management/reports/dependency-inventory.md"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null

function Write-Section($title) {
  "`n## $title`n" | Out-File -FilePath $Output -Encoding UTF8 -Append
}

"# Repository Inventory and Dependency Audit`nGenerated: $(Get-Date -Format o)" | Out-File -FilePath $Output -Encoding UTF8

Write-Section "Solutions and Projects"
Get-ChildItem -Recurse -File -Include *.sln,*.csproj |
  ForEach-Object { "- ``$($_.FullName.Replace((Get-Location).Path + '\\',''))``" } |
  Out-File -FilePath $Output -Encoding UTF8 -Append

Write-Section "Target Frameworks"
Get-ChildItem -Recurse -File -Include *.csproj |
  ForEach-Object {
    $xml = [xml](Get-Content -Raw -- $_.FullName)
    $tfm = ($xml.Project.PropertyGroup.TargetFrameworks, $xml.Project.PropertyGroup.TargetFramework) -join ';' -replace ';$',''
    if (-not $tfm) { $tfm = '(unspecified)' }
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

