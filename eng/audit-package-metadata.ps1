<#
.SYNOPSIS
    Audits shipping projects for required NuGet package metadata per AD-326-1.

.DESCRIPTION
    Scans all src/**/*.csproj files and checks for required packaging metadata:
    - Description (required)
    - PackageTags (required)
    - PackageReadmeFile and README.md existence (required)

    Outputs a report of missing metadata and optionally generates a fix plan.

.PARAMETER Path
    Root path to scan. Defaults to current directory.

.PARAMETER OutputFormat
    Output format: 'Console', 'Csv', or 'Json'. Default: Console.

.PARAMETER GenerateFixPlan
    If specified, generates a YAML fix plan for missing metadata.

.EXAMPLE
    .\audit-package-metadata.ps1

.EXAMPLE
    .\audit-package-metadata.ps1 -OutputFormat Json -GenerateFixPlan
#>

[CmdletBinding()]
param(
    [string]$Path = (Get-Location),
    [ValidateSet('Console', 'Csv', 'Json')]
    [string]$OutputFormat = 'Console',
    [switch]$GenerateFixPlan
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Get all shipping projects (src/**)
$srcPath = Join-Path $Path "src"
$projects = @(Get-ChildItem -Path $srcPath -Filter "*.csproj" -Recurse)

$results = @()
$missingCount = 0
$totalCount = @($projects).Count

foreach ($project in $projects) {
    $csprojPath = $project.FullName
    $projectDir = $project.DirectoryName
    $projectName = $project.BaseName

    # Parse csproj XML
    [xml]$csproj = Get-Content $csprojPath

    # Extract metadata
    $description = $csproj.SelectSingleNode("//Description")?.InnerText
    $packageTags = $csproj.SelectSingleNode("//PackageTags")?.InnerText
    $packageReadmeFile = $csproj.SelectSingleNode("//PackageReadmeFile")?.InnerText

    # Check for README.md
    $readmePath = Join-Path $projectDir "README.md"
    $hasReadme = Test-Path $readmePath

    # Determine issues
    $issues = @()
    if ([string]::IsNullOrWhiteSpace($description)) {
        $issues += "Missing <Description>"
    }
    if ([string]::IsNullOrWhiteSpace($packageTags)) {
        $issues += "Missing <PackageTags>"
    }
    if ([string]::IsNullOrWhiteSpace($packageReadmeFile)) {
        $issues += "Missing <PackageReadmeFile>"
    }
    if (-not $hasReadme) {
        $issues += "Missing README.md file"
    }

    $relativePath = $csprojPath.Replace($Path, "").TrimStart("\", "/")

    $result = [PSCustomObject]@{
        Project = $projectName
        Path = $relativePath
        HasDescription = -not [string]::IsNullOrWhiteSpace($description)
        HasPackageTags = -not [string]::IsNullOrWhiteSpace($packageTags)
        HasPackageReadmeFile = -not [string]::IsNullOrWhiteSpace($packageReadmeFile)
        HasReadmeMd = $hasReadme
        Issues = ($issues -join "; ")
        IssueCount = $issues.Count
    }

    $results += $result

    if ($issues.Count -gt 0) {
        $missingCount++
    }
}

# Output results based on format
switch ($OutputFormat) {
    'Console' {
        Write-Host "`n========================================" -ForegroundColor Cyan
        Write-Host "Package Metadata Audit Report (AD-326-1)" -ForegroundColor Cyan
        Write-Host "========================================`n" -ForegroundColor Cyan

        Write-Host "Total shipping projects: $totalCount" -ForegroundColor White
        Write-Host "Projects with issues: $missingCount" -ForegroundColor $(if ($missingCount -gt 0) { 'Yellow' } else { 'Green' })
        Write-Host "Projects complete: $($totalCount - $missingCount)" -ForegroundColor Green
        Write-Host ""

        if ($missingCount -gt 0) {
            Write-Host "Projects with Missing Metadata:" -ForegroundColor Yellow
            Write-Host "-------------------------------" -ForegroundColor Yellow

            $results | Where-Object { $_.IssueCount -gt 0 } | ForEach-Object {
                Write-Host "`n  $($_.Project)" -ForegroundColor White
                Write-Host "    Path: $($_.Path)" -ForegroundColor Gray
                Write-Host "    Issues: $($_.Issues)" -ForegroundColor Yellow
            }
        }

        Write-Host "`n`nSummary by Issue Type:" -ForegroundColor Cyan
        Write-Host "----------------------" -ForegroundColor Cyan
        $missingDescription = @($results | Where-Object { -not $_.HasDescription }).Count
        $missingTags = @($results | Where-Object { -not $_.HasPackageTags }).Count
        $missingReadmeFile = @($results | Where-Object { -not $_.HasPackageReadmeFile }).Count
        $missingReadmeMd = @($results | Where-Object { -not $_.HasReadmeMd }).Count

        Write-Host "  Missing <Description>: $missingDescription" -ForegroundColor $(if ($missingDescription -gt 0) { 'Yellow' } else { 'Green' })
        Write-Host "  Missing <PackageTags>: $missingTags" -ForegroundColor $(if ($missingTags -gt 0) { 'Yellow' } else { 'Green' })
        Write-Host "  Missing <PackageReadmeFile>: $missingReadmeFile" -ForegroundColor $(if ($missingReadmeFile -gt 0) { 'Yellow' } else { 'Green' })
        Write-Host "  Missing README.md file: $missingReadmeMd" -ForegroundColor $(if ($missingReadmeMd -gt 0) { 'Yellow' } else { 'Green' })
    }
    'Csv' {
        $results | Export-Csv -Path (Join-Path $Path "package-metadata-audit.csv") -NoTypeInformation
        Write-Host "Report written to package-metadata-audit.csv"
    }
    'Json' {
        $report = @{
            timestamp = Get-Date -Format "o"
            totalProjects = $totalCount
            projectsWithIssues = $missingCount
            projectsComplete = $totalCount - $missingCount
            summary = @{
                missingDescription = @($results | Where-Object { -not $_.HasDescription }).Count
                missingPackageTags = @($results | Where-Object { -not $_.HasPackageTags }).Count
                missingPackageReadmeFile = @($results | Where-Object { -not $_.HasPackageReadmeFile }).Count
                missingReadmeMd = @($results | Where-Object { -not $_.HasReadmeMd }).Count
            }
            projects = $results
        }
        $report | ConvertTo-Json -Depth 5
    }
}

# Generate fix plan if requested
if ($GenerateFixPlan) {
    $fixPlan = @"
# Package Metadata Fix Plan
# Generated: $(Get-Date -Format "o")
# Sprint 326 - T2.1 Packaging (AD-326-1)

total_issues: $missingCount

projects:
"@

    $results | Where-Object { $_.IssueCount -gt 0 } | ForEach-Object {
        $fixPlan += @"

  - project: $($_.Project)
    path: $($_.Path)
    needs:
"@
        if (-not $_.HasDescription) {
            $fixPlan += "`n      - description: true"
        }
        if (-not $_.HasPackageTags) {
            $fixPlan += "`n      - packageTags: true"
        }
        if (-not $_.HasPackageReadmeFile) {
            $fixPlan += "`n      - packageReadmeFile: true"
        }
        if (-not $_.HasReadmeMd) {
            $fixPlan += "`n      - readmeMd: true"
        }
    }

    $fixPlanPath = Join-Path $Path "management/governance/package-metadata-fixplan.yaml"
    $fixPlan | Out-File -FilePath $fixPlanPath -Encoding utf8
    Write-Host "`nFix plan written to: $fixPlanPath" -ForegroundColor Green
}

# Exit with error code if issues found
if ($missingCount -gt 0) {
    exit 1
}
