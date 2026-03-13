#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate changelog from git commits and Beads task IDs.

.DESCRIPTION
    Parses git log for commits since the last tag (or a specified range) and groups them
    by category (feat/fix/chore/refactor/test/docs/ci) using conventional commit prefixes.
    Extracts Beads task IDs (bd-XXXXX) from commit messages for cross-referencing.

    Sprint 640 B.1 (bd-oio69).

.PARAMETER OutDir
    Output directory for changelog artifacts. Defaults to ChangelogReport.

.PARAMETER SinceTag
    Generate changelog since this tag. If not specified, uses the most recent tag.

.PARAMETER SinceCommit
    Generate changelog since this commit SHA. Overrides SinceTag.

.PARAMETER SprintNumber
    Optional sprint number to include in the changelog header.

.EXAMPLE
    ./generate-changelog.ps1

.EXAMPLE
    ./generate-changelog.ps1 -SinceTag v1.0.0 -SprintNumber 640
#>
param(
    [string]$OutDir = 'ChangelogReport',
    [string]$SinceTag,
    [string]$SinceCommit,
    [int]$SprintNumber = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

# Determine the range
$range = ''
if ($SinceCommit) {
    $range = "$SinceCommit..HEAD"
}
elseif ($SinceTag) {
    $range = "$SinceTag..HEAD"
}
else {
    # Find the most recent tag
    $latestTag = git describe --tags --abbrev=0 2>$null
    if ($latestTag) {
        $range = "$latestTag..HEAD"
        Write-Host "Generating changelog since tag: $latestTag"
    }
    else {
        # No tags -- use all commits (limit to last 200)
        $range = ''
        Write-Host "No tags found. Generating changelog from recent commits."
    }
}

# Get commits
$gitArgs = @('log', '--pretty=format:%H|%s|%an|%aI')
if ($range) {
    $gitArgs += $range
}
else {
    $gitArgs += '-n'
    $gitArgs += '200'
}

$rawLog = & git @gitArgs 2>$null
if (-not $rawLog) {
    Write-Host "No commits found in range."
    "# Changelog`n`nNo changes detected." | Out-File -FilePath (Join-Path $OutDir 'CHANGELOG.md') -Encoding UTF8
    exit 0
}

$commits = $rawLog -split "`n" | Where-Object { $_ -match '\S' } | ForEach-Object {
    $parts = $_ -split '\|', 4
    if ($parts.Count -ge 4) {
        [pscustomobject]@{
            Hash    = $parts[0].Substring(0, [Math]::Min(10, $parts[0].Length))
            Subject = $parts[1]
            Author  = $parts[2]
            Date    = $parts[3]
        }
    }
}

if (-not $commits -or $commits.Count -eq 0) {
    Write-Host "No parseable commits found."
    "# Changelog`n`nNo changes detected." | Out-File -FilePath (Join-Path $OutDir 'CHANGELOG.md') -Encoding UTF8
    exit 0
}

# Categorize commits using conventional commit prefixes
$categories = @{
    'Features'       = [System.Collections.Generic.List[object]]::new()
    'Bug Fixes'      = [System.Collections.Generic.List[object]]::new()
    'Refactoring'    = [System.Collections.Generic.List[object]]::new()
    'Tests'          = [System.Collections.Generic.List[object]]::new()
    'Documentation'  = [System.Collections.Generic.List[object]]::new()
    'CI/CD'          = [System.Collections.Generic.List[object]]::new()
    'Other'          = [System.Collections.Generic.List[object]]::new()
}

# Pattern: conventional commit prefix (optional scope)
$conventionalPattern = '^(feat|fix|refactor|test|docs|ci|chore|perf|build|style)(\([^)]+\))?[!]?:\s*(.+)$'
# Pattern: Beads task ID
$beadsPattern = '\b(bd-[a-z0-9]{5})\b'

foreach ($commit in $commits) {
    $subject = $commit.Subject
    $beadIds = @()
    $matches_ = [regex]::Matches($subject, $beadsPattern)
    if ($matches_.Count -gt 0) {
        $beadIds = $matches_ | ForEach-Object { $_.Groups[1].Value }
    }

    $category = 'Other'
    $description = $subject

    if ($subject -match $conventionalPattern) {
        $prefix = $Matches[1]
        $scope = if ($Matches[2]) { $Matches[2].Trim('(', ')') } else { $null }
        $description = $Matches[3]

        $category = switch ($prefix) {
            'feat'     { 'Features' }
            'fix'      { 'Bug Fixes' }
            'refactor' { 'Refactoring' }
            'test'     { 'Tests' }
            'docs'     { 'Documentation' }
            'ci'       { 'CI/CD' }
            'build'    { 'CI/CD' }
            'chore'    { 'Other' }
            'perf'     { 'Features' }
            'style'    { 'Other' }
            default    { 'Other' }
        }

        if ($scope) {
            $description = "**$scope**: $description"
        }
    }

    $entry = [pscustomobject]@{
        Hash        = $commit.Hash
        Description = $description
        Author      = $commit.Author
        Date        = $commit.Date
        BeadIds     = $beadIds
        Category    = $category
    }

    $categories[$category].Add($entry)
}

# Generate markdown changelog
$md = [System.Collections.Generic.List[string]]::new()
$md.Add('# Changelog')
$md.Add('')

if ($SprintNumber -gt 0) {
    $md.Add("## Sprint $SprintNumber")
}
else {
    $md.Add("## $(Get-Date -Format 'yyyy-MM-dd')")
}
$md.Add('')

$totalCount = ($commits | Measure-Object).Count
$md.Add("**$totalCount commits**")
$md.Add('')

# Ordered category output
$categoryOrder = @('Features', 'Bug Fixes', 'Refactoring', 'Tests', 'Documentation', 'CI/CD', 'Other')

foreach ($cat in $categoryOrder) {
    $entries = $categories[$cat]
    if ($entries.Count -eq 0) { continue }

    $md.Add("### $cat")
    $md.Add('')

    foreach ($entry in $entries) {
        $beadSuffix = ''
        if ($entry.BeadIds -and $entry.BeadIds.Count -gt 0) {
            $beadSuffix = " [$($entry.BeadIds -join ', ')]"
        }
        $md.Add("- $($entry.Description) ($($entry.Hash))$beadSuffix")
    }
    $md.Add('')
}

# Write Beads task cross-reference
$allBeadIds = $commits | ForEach-Object {
    $matches_ = [regex]::Matches($_.Subject, $beadsPattern)
    $matches_ | ForEach-Object { $_.Groups[1].Value }
} | Sort-Object -Unique

if ($allBeadIds.Count -gt 0) {
    $md.Add('### Beads Tasks Referenced')
    $md.Add('')
    foreach ($id in $allBeadIds) {
        $md.Add("- ``$id``")
    }
    $md.Add('')
}

$changelogPath = Join-Path $OutDir 'CHANGELOG.md'
$md -join "`n" | Out-File -FilePath $changelogPath -Encoding UTF8

# Write JSON report
$jsonReport = [pscustomobject]@{
    generatedAt  = (Get-Date -Format 'o')
    range        = if ($range) { $range } else { 'last-200' }
    sprintNumber = $SprintNumber
    totalCommits = $totalCount
    categories   = @{}
    beadTaskIds  = @($allBeadIds)
}

foreach ($cat in $categoryOrder) {
    $entries = $categories[$cat]
    $jsonReport.categories[$cat] = $entries.Count
}

$jsonPath = Join-Path $OutDir 'changelog-report.json'
$jsonReport | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host ""
Write-Host "Changelog generated: $changelogPath"
Write-Host "  Commits: $totalCount"
Write-Host "  Beads tasks: $($allBeadIds.Count)"
Write-Host "  Categories: $(($categoryOrder | Where-Object { $categories[$_].Count -gt 0 }) -join ', ')"
