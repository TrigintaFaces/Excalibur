<#
.SYNOPSIS
    Analyzes all shipping projects for AOT/trim compatibility at build time.

.DESCRIPTION
    Runs `dotnet build` with trim analyzers enabled on all shipping projects
    and scans source files for untyped JsonSerializer usage (missing
    JsonSerializerContext). Produces JSON and Markdown reports.

    This script consolidates the former trim-aot-audit.ps1 and
    validate-aot-trim.ps1 into one canonical build-time analysis tool.

    Exit codes:
      0 = No IL warnings and no untyped JSON usage
      1 = IL warnings or untyped JSON usage detected
      2 = Script error

.PARAMETER OutputPath
    Directory for analysis results (default: aot-build-analysis).

.PARAMETER SkipBuild
    Skip the dotnet build analysis (only run JSON context scan).

.EXAMPLE
    ./Invoke-AotBuildAnalysis.ps1 -OutputPath ./aot-build-analysis
#>
[CmdletBinding()]
param(
    [string]$OutputPath = 'aot-build-analysis',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$reportJsonFile = Join-Path $OutputPath 'aot-build-analysis.json'
$reportMdFile = Join-Path $OutputPath 'aot-build-analysis.md'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# ============================================================================
# Find shipping projects
# ============================================================================

Write-Host "Searching for shipping projects..." -ForegroundColor Cyan

$srcDirs = @(
    (Join-Path $repoRoot 'src' 'Dispatch'),
    (Join-Path $repoRoot 'src' 'Excalibur')
)

$projects = @()
foreach ($srcDir in $srcDirs) {
    if (Test-Path $srcDir) {
        $found = Get-ChildItem -Path $srcDir -Filter '*.csproj' -Recurse |
            Where-Object { $_.FullName -notmatch '(Tests|Benchmarks|Examples|Sample)' }
        $projects += $found
    }
}

Write-Host "Found $($projects.Count) shipping projects" -ForegroundColor Green

# ============================================================================
# Build-time trim analysis
# ============================================================================

$projectResults = @()

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Running build-time trim analysis (this may take several minutes)..." -ForegroundColor Cyan

    foreach ($project in $projects) {
        $projectName = $project.BaseName
        Write-Host "  Analyzing: $projectName" -ForegroundColor Gray

        $buildArgs = @(
            'build', $project.FullName,
            '-c', 'Release',
            '--no-restore',
            '-p:EnableTrimAnalyzer=true',
            '-p:IsTrimmable=true',
            '-p:TrimMode=full',
            '-p:SuppressTrimAnalysisWarnings=false',
            '--nologo',
            '--verbosity', 'normal'
        )

        $buildLog = Join-Path $OutputPath "$projectName.log"
        $buildOutput = & dotnet @buildArgs 2>&1 | Out-String
        $buildOutput | Out-File -FilePath $buildLog -Encoding utf8
        $buildSuccess = ($LASTEXITCODE -eq 0)

        # Parse IL warnings
        $ilWarnings = @()
        foreach ($line in ($buildOutput -split "`n")) {
            if ($line -match '(warning|error)\s+(IL\d{4})\s*:\s*(.+)') {
                $ilWarnings += @{
                    Severity = $matches[1]
                    Code     = $matches[2]
                    Message  = $matches[3].Trim()
                }
            }
        }

        # Read .csproj for AOT declaration (use SelectSingleNode for strict-mode safety)
        [xml]$projXml = Get-Content $project.FullName
        $isAotCompatible = $null
        foreach ($pg in $projXml.Project.PropertyGroup) {
            $aotNode = $pg.SelectSingleNode('IsAotCompatible')
            if ($null -ne $aotNode) {
                $isAotCompatible = ($aotNode.InnerText -eq 'true')
            }
        }

        $projectResults += @{
            Name            = $projectName
            Path            = $project.FullName
            BuildSuccess    = $buildSuccess
            IsAotCompatible = $isAotCompatible
            ILWarnings      = $ilWarnings
            WarningCount    = $ilWarnings.Count
        }

        $statusIcon = if ($ilWarnings.Count -eq 0) { 'OK' } else { "$($ilWarnings.Count) warnings" }
        Write-Host "    $projectName : $statusIcon" -ForegroundColor $(if ($ilWarnings.Count -eq 0) { 'Green' } else { 'Yellow' })
    }
}
else {
    Write-Host "Skipping build analysis (-SkipBuild)" -ForegroundColor Yellow
}

# ============================================================================
# JSON context scan -- find untyped JsonSerializer usage
# ============================================================================

Write-Host ""
Write-Host "Scanning for untyped JsonSerializer usage..." -ForegroundColor Cyan

$jsonViolations = @()
$srcFiles = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch '(Tests|Benchmarks|obj|bin)' }

foreach ($file in $srcFiles) {
    $content = Get-Content -Raw -ErrorAction SilentlyContinue -- $file.FullName
    if ($null -eq $content) { continue }

    # Detect JsonSerializer.Serialize/Deserialize with JsonSerializerOptions but no context/TypeInfo
    if ($content -match 'JsonSerializer\.(Serialize|Deserialize)\s*(<[^>]+>)?\s*\([^)]*JsonSerializerOptions' -and
        $content -notmatch 'JsonSerializer\.(Serialize|Deserialize)\s*(<[^>]+>)?\s*\([^)]*JsonSerializerContext') {
        $relativePath = $file.FullName.Replace($repoRoot, '').TrimStart('\/')
        $jsonViolations += $relativePath
    }
}

Write-Host "  Found $($jsonViolations.Count) file(s) with untyped JsonSerializer usage" -ForegroundColor $(if ($jsonViolations.Count -eq 0) { 'Green' } else { 'Yellow' })

# ============================================================================
# Check IsAotCompatible declarations
# ============================================================================

$missingDeclaration = @($projectResults | Where-Object { $null -eq $_.IsAotCompatible })
$claimsAot = @($projectResults | Where-Object { $true -eq $_.IsAotCompatible })
$claimsNotAot = @($projectResults | Where-Object { $false -eq $_.IsAotCompatible })
$dishonestClaims = @($claimsAot | Where-Object { $_.WarningCount -gt 0 })

# ============================================================================
# Build JSON report
# ============================================================================

$totalWarnings = ($projectResults | ForEach-Object { $_.WarningCount } | Measure-Object -Sum).Sum

$report = @{
    Timestamp            = (Get-Date -Format 'o')
    TotalProjects        = $projects.Count
    TotalILWarnings      = $totalWarnings
    UntypedJsonFiles     = $jsonViolations.Count
    MissingDeclaration   = $missingDeclaration.Count
    DishonestClaims      = $dishonestClaims.Count
    ClaimsAotCompatible  = $claimsAot.Count
    ClaimsNotAotCompatible = $claimsNotAot.Count
    Projects             = @($projectResults | ForEach-Object {
        @{
            Name            = $_.Name
            BuildSuccess    = $_.BuildSuccess
            IsAotCompatible = $_.IsAotCompatible
            WarningCount    = $_.WarningCount
            ILWarnings      = @($_.ILWarnings)
        }
    })
    UntypedJsonViolations = @($jsonViolations)
}

$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportJsonFile -Encoding utf8

# ============================================================================
# Build Markdown report
# ============================================================================

$md = @"
# AOT/Trim Build Analysis Report
**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## Summary

| Metric | Count |
|--------|-------|
| Total Projects | $($projects.Count) |
| IsAotCompatible=true | $($claimsAot.Count) |
| IsAotCompatible=false | $($claimsNotAot.Count) |
| Missing Declaration | $($missingDeclaration.Count) |
| Dishonest Claims (true + warnings) | $($dishonestClaims.Count) |
| Total IL Warnings | $totalWarnings |
| Untyped JsonSerializer Files | $($jsonViolations.Count) |

"@

if ($dishonestClaims.Count -gt 0) {
    $md += "## Dishonest AOT Claims`n`n"
    $md += "These projects claim IsAotCompatible=true but have IL warnings:`n`n"
    foreach ($p in $dishonestClaims) {
        $md += "- ``$($p.Name)`` ($($p.WarningCount) warnings)`n"
    }
    $md += "`n"
}

if ($missingDeclaration.Count -gt 0) {
    $md += "## Missing IsAotCompatible Declaration`n`n"
    foreach ($p in $missingDeclaration) {
        $md += "- ``$($p.Name)```n"
    }
    $md += "`n"
}

$projectsWithWarnings = @($projectResults | Where-Object { $_.WarningCount -gt 0 })
if ($projectsWithWarnings.Count -gt 0) {
    $md += "## Projects with IL Warnings`n`n"
    $md += "| Project | Warnings |`n|---------|----------|`n"
    foreach ($p in $projectsWithWarnings | Sort-Object { $_.WarningCount } -Descending) {
        $md += "| ``$($p.Name)`` | $($p.WarningCount) |`n"
    }
    $md += "`n"
}

if ($jsonViolations.Count -gt 0) {
    $md += "## Untyped JsonSerializer Usage`n`n"
    $md += "These files use JsonSerializer with JsonSerializerOptions instead of source-generated context:`n`n"
    foreach ($f in $jsonViolations) {
        $md += "- ``$f```n"
    }
    $md += "`n"
}

$md | Out-File -FilePath $reportMdFile -Encoding utf8

# ============================================================================
# Summary
# ============================================================================

Write-Host ""
Write-Host "========================================"
Write-Host "  AOT Build Analysis Summary"
Write-Host "========================================"
Write-Host "  Projects:           $($projects.Count)"
Write-Host "  IL Warnings:        $totalWarnings"
Write-Host "  Untyped JSON:       $($jsonViolations.Count) files"
Write-Host "  Missing AOT decl:   $($missingDeclaration.Count)"
Write-Host "  Dishonest claims:   $($dishonestClaims.Count)"
Write-Host "========================================"
Write-Host ""

Write-Host "Reports: $reportJsonFile, $reportMdFile" -ForegroundColor Cyan

# Determine exit code
if ($totalWarnings -gt 0 -or $jsonViolations.Count -gt 0) {
    Write-Host "AOT build analysis found issues." -ForegroundColor Yellow
    exit 1
}

Write-Host "AOT build analysis clean." -ForegroundColor Green
exit 0
