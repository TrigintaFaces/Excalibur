#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates exact filesystem, manifest, solution, build configurations and CI filters.
.DESCRIPTION
    Run from the repository root. Inputs are never regenerated during validation.
    Manifest v2 accepts block collections and plain or JSON-quoted string scalars only.
    Unsupported syntax fails rather than silently dropping an entry.
#>
param([string]$ManifestPath = 'eng/governance/project-manifest.yaml')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Get-Location).Path
$requiredRoots = @('src', 'tests', 'samples', 'benchmarks', 'load-tests')
$templatePackage = 'templates/Excalibur.Dispatch.Templates.csproj'

function New-Set { return ,([Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)) }
function New-Map { return ,([Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)) }
function Convert-RepoPath([string]$Path) {
    $path = $Path.Replace([char]92, '/')
    if ([string]::IsNullOrWhiteSpace($path) -or $path.StartsWith('/') -or $path.Contains(':') -or
        @($path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count) {
        throw "Non-canonical repository path: '$Path'"
    }
    return $path
}
function Get-Projects([string]$Directory) {
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { return }
    Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter '*.csproj' | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace([char]92, '/')
        $excluded = @($relative.Split('/') | Where-Object {
            $_.StartsWith('.') -or $_ -cin @('bin','obj','node_modules','labs','tools','BenchmarkDotNet.Artifacts')
        }).Count -gt 0
        if (-not $excluded) { $relative }
    }
}
function Read-Scalar([string]$Value) {
    if ($Value.StartsWith('"')) {
        $json = [System.Text.Json.JsonDocument]::Parse($Value)
        try { return $json.RootElement.GetString() } finally { $json.Dispose() }
    }
    if ($Value -notmatch '^[A-Za-z0-9_./*\\-]+$') { throw "Unsupported manifest scalar: $Value" }
    return $Value
}
function Add-Field($Map, [string]$Key, [string]$Value) {
    if ($Map.ContainsKey($Key)) { throw "Duplicate manifest key: $Key at $($Map['path'])" }
    if ($Key -ceq 'in_solution' -and $Value -cnotin @('true','false')) { throw "Expected literal manifest boolean: $Value" }
    $Map.Add($Key, (Read-Scalar $Value))
}
function Assert-Fields($Map, [string[]]$Required, [string[]]$Allowed) {
    foreach ($key in $Required) { if (-not $Map.ContainsKey($key)) { throw "Missing manifest key: $key at $($Map['path'])" } }
    foreach ($key in $Map.Keys) { if ($key -cnotin $Allowed) { throw "Unexpected manifest key: $key" } }
}
function Assert-SameSet($Expected, $Actual, [string]$Label) {
    $expectedSet = New-Set
    foreach ($path in $Expected) { [void]$expectedSet.Add($path) }
    foreach ($path in $Expected) { if (-not $Actual.Contains($path)) { throw "$Label missing: $path" } }
    foreach ($path in $Actual) { if (-not $expectedSet.Contains($path)) { throw "$Label unexpected: $path" } }
}

function Assert-SolutionStructure([string[]]$Lines) {
    $header = 0
    while ($header -lt $Lines.Count -and [string]::IsNullOrWhiteSpace($Lines[$header])) { $header++ }
    if ($header -ge $Lines.Count -or $Lines[$header] -cne 'Microsoft Visual Studio Solution File, Format Version 12.00') { throw 'Invalid solution structure: header' }
    $block = ''; $section = ''; $globalSeen = $false; $globalClosed = $false
    $globalSections = New-Set
    foreach ($line in $Lines | Select-Object -Skip ($header + 1)) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
        if ($trimmed.StartsWith('Project(')) {
            if ($block -ne '' -or $globalSeen -or $trimmed -notmatch '^Project\("(\{[^}]+\})"\)') { throw 'Invalid solution structure: Project' }
            if ($Matches[1].ToUpperInvariant() -cnotin @('{2150E333-8FDC-42A3-9474-1A3956D46DE8}','{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}','{9A19103F-16F7-4668-BE54-9A1E7A4F7556}')) { throw 'Invalid solution structure: project type GUID' }
            $block = 'Project'; continue
        }
        if ($trimmed -ceq 'Global') {
            if ($block -ne '' -or $globalSeen) { throw 'Invalid solution structure: Global' }
            $block = 'Global'; $globalSeen = $true; continue
        }
        if ($trimmed -cin @('EndProject','EndGlobal')) {
            if ($section -ne '' -or $trimmed -cne ('End' + $block)) { throw 'Invalid solution structure: block terminator' }
            if ($block -ceq 'Global') { $globalClosed = $true }
            $block = ''; continue
        }
        if ($trimmed -match '^(Project|Global)Section\(([^)]+)\) = (preProject|postProject|preSolution|postSolution)$') {
            if ($section -ne '' -or $block -cne $Matches[1]) { throw 'Invalid solution structure: section' }
            $section = $Matches[1]
            if ($section -ceq 'Global' -and -not $globalSections.Add($Matches[2])) { throw 'Invalid solution structure: duplicate global section' }
            continue
        }
        if ($trimmed -cin @('EndProjectSection','EndGlobalSection')) {
            if ($section -eq '' -or $trimmed -cne ('End' + $section + 'Section')) { throw 'Invalid solution structure: section terminator' }
            $section = ''; continue
        }
        if ($section -eq '' -and ($block -ne '' -or $globalSeen -or $trimmed -notmatch '^(VisualStudioVersion|MinimumVisualStudioVersion) = [0-9.]+$')) { throw "Invalid solution structure: $trimmed" }
    }
    if ($block -ne '' -or $section -ne '' -or -not $globalClosed) { throw 'Invalid solution structure: missing terminator' }
}

try {
    $top = New-Map; $governance = New-Map; $roots = New-Set
    $entries = [Collections.Generic.List[object]]::new()
    $exclusions = [Collections.Generic.List[object]]::new()
    $section = ''; $entry = $null
    foreach ($line in Get-Content -LiteralPath $ManifestPath) {
        if ($line -match '^\s*(#.*)?$') { continue }
        if ($line -match '^([a-z_]+):(?: (.+))?$') {
            $section = $Matches[1]; $entry = $null
            if ($top.ContainsKey($section)) { throw "Duplicate manifest section: $section" }
            $value = if ($Matches.ContainsKey(2)) { Read-Scalar $Matches[2] } else { '' }
            $top.Add($section, $value)
            continue
        }
        if ($section -ceq 'governance' -and $line -match '^  ([a-z_]+): (.+)$') { Add-Field $governance $Matches[1] $Matches[2]; continue }
        if ($section -ceq 'governed_directories' -and $line -match '^  - ([a-z-]+)/\*\*$') {
            if (-not $roots.Add($Matches[1])) { throw 'Duplicate governed directory' }; continue
        }
        if ($section -cin @('projects','exclusions')) {
            if ($line -match '^  - path: (.+)$') {
                $entry = New-Map; Add-Field $entry 'path' $Matches[1]
                if ($section -ceq 'projects') { $entries.Add($entry) } else { $exclusions.Add($entry) }
                continue
            }
            if ($null -ne $entry -and $line -match '^    ([a-z_]+): (.+)$') { Add-Field $entry $Matches[1] $Matches[2]; continue }
        }
        throw "Unsupported manifest syntax: $line"
    }
    $fields = @('version','generated_at','governance','governed_directories','exclusions','projects')
    Assert-Fields $top $fields $fields
    if ($top['version'] -cne '2.0') { throw 'Unsupported manifest version' }
    foreach ($name in @('governance','governed_directories','exclusions','projects')) {
        if ($top[$name] -cne '') { throw "Expected block section: $name" }
    }
    Assert-Fields $governance @('solution_file') @('solution_file')
    if ($governance['solution_file'] -cne 'Excalibur.sln') { throw 'Manifest must govern Excalibur.sln' }
    Assert-SameSet $requiredRoots $roots 'Governed directories'
    $excludedPaths = New-Set
    foreach ($item in $exclusions) {
        Assert-Fields $item @('path','reason') @('path','reason')
        $path = Convert-RepoPath $item['path']
        if (-not $excludedPaths.Add($path)) { throw "Duplicate exclusion: $path" }
        if ($path.Split('/')[0] -cin $requiredRoots) { throw "Cannot exclude governed project tree: $path" }
    }

    $disk = New-Set
    foreach ($root in $requiredRoots) { foreach ($path in Get-Projects $root) { [void]$disk.Add($path) } }
    if (-not (Test-Path -LiteralPath $templatePackage -PathType Leaf)) { throw "Missing template package: $templatePackage" }
    [void]$disk.Add($templatePackage)
    $manifest = New-Set
    foreach ($item in $entries) {
        Assert-Fields $item @('path','classification','in_solution') @('path','classification','in_solution','framework_owner','tier','category','variant','notes','reason')
        $path = Convert-RepoPath $item['path']
        if (-not $manifest.Add($path)) { throw "Duplicate manifest project: $path" }
        if ($item['in_solution'] -cne 'true') { throw "Governed project must have literal in_solution: true: $path" }
        $classification = if ($path -ceq $templatePackage) { 'Template' }
            elseif ($path.StartsWith('src/', [StringComparison]::Ordinal)) { 'Shipping' }
            elseif ($path.StartsWith('samples/', [StringComparison]::Ordinal)) { 'Sample' }
            elseif ($path.StartsWith('benchmarks/', [StringComparison]::Ordinal) -or $path.StartsWith('tests/benchmarks/', [StringComparison]::Ordinal)) { 'Benchmark' }
            else { 'Test' }
        if ($item['classification'] -cne $classification) { throw "Incorrect classification: $path" }
    }
    Assert-SameSet $disk $manifest 'Manifest'

    $projects = New-Map; $guids = New-Set; $configs = New-Set; $mappings = New-Map; $items = New-Set
    $names = New-Map; $folders = New-Set; $parents = New-Map
    $section = ''; $isFolder = $false
    $solutionLines = @(Get-Content -LiteralPath 'Excalibur.sln')
    Assert-SolutionStructure $solutionLines
    foreach ($sourceLine in $solutionLines) {
        $line = $sourceLine.Trim()
        if ($line -match '^Project\("\{[^}]+\}"\) = "([^"]+)", "([^"]+)", "(\{[^}]+\})"$') {
            $name = $Matches[1]; $path = $Matches[2]; $guid = $Matches[3].ToUpperInvariant()
            $parsedGuid = [guid]::Empty
            if (-not [guid]::TryParse($guid, [ref]$parsedGuid)) { throw "Invalid project GUID: $guid" }
            if (-not $guids.Add($guid)) { throw "Duplicate solution GUID: $guid" }
            $isFolder = $line.StartsWith('Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}")', [StringComparison]::OrdinalIgnoreCase)
            $effectiveName = if ($isFolder) { $name } else { [IO.Path]::GetFileNameWithoutExtension($path.Replace([char]92, '/')) }
            $names.Add($guid, $effectiveName)
            if ($isFolder) { [void]$folders.Add($guid) }
            if (-not $isFolder) {
                $path = Convert-RepoPath $path
                if (-not $path.EndsWith('.csproj', [StringComparison]::Ordinal)) { throw "Unsupported solution project: $path" }
                if ($projects.ContainsKey($path)) { throw "Duplicate solution path: $path" }
                $projects.Add($path, $guid)
            }
        }
        elseif ($line.StartsWith('Project(')) { throw "Malformed solution project: $line" }
        elseif ($line -match '^\s*GlobalSection\((SolutionConfigurationPlatforms|ProjectConfigurationPlatforms|NestedProjects)\)') { $section = $Matches[1] }
        elseif ($line -match '^\s*ProjectSection\(SolutionItems\)') {
            if (-not $isFolder) { throw 'SolutionItems must belong to a solution folder' }; $section = 'SolutionItems'
        }
        elseif ($line -match '^\s*End(?:Global|Project)Section') { $section = '' }
        elseif ($section -ceq 'SolutionConfigurationPlatforms') {
            if ($line -notmatch '^\s*([^=]+?)\s*=\s*(.+?)\s*$' -or $Matches[1] -cne $Matches[2] -or -not $configs.Add($Matches[1])) { throw "Invalid or duplicate solution configuration: $line" }
        }
        elseif ($section -ceq 'ProjectConfigurationPlatforms') {
            if ($line -notmatch '^\s*(\{[^}]+\})\.(.+)\.(ActiveCfg|Build\.0) = (.+?)\s*$') { throw "Invalid project configuration: $line" }
            $key = $Matches[1].ToUpperInvariant() + '.' + $Matches[2] + '.' + $Matches[3]
            if ($mappings.ContainsKey($key)) { throw "Duplicate project configuration: $key" }
            $mappings.Add($key, $Matches[4])
        }
        elseif ($section -ceq 'NestedProjects') {
            if ($line -notmatch '^\s*(\{[^}]+\}) = (\{[^}]+\})\s*$') { throw 'Invalid solution nesting' }
            $child = $Matches[1].ToUpperInvariant(); $parent = $Matches[2].ToUpperInvariant()
            if ($parents.ContainsKey($child)) { throw 'Duplicate solution nesting' }
            $parents.Add($child, $parent)
        }
        elseif ($section -ceq 'SolutionItems') {
            if ($line -notmatch '^\s*(.+?) = (.+?)\s*$') { throw "Invalid solution item: $line" }
            $left = Convert-RepoPath $Matches[1]; $right = Convert-RepoPath $Matches[2]
            if ($left -cne $right) { throw "Solution item path mismatch: $line" }
            if ($left.EndsWith('.csproj', [StringComparison]::Ordinal) -and -not $items.Add($left)) { throw "Duplicate project solution item: $left" }
        }
    }
    foreach ($parent in $parents.GetEnumerator()) {
        if (-not $guids.Contains($parent.Key) -or -not $folders.Contains($parent.Value)) { throw 'Invalid solution folder reference' }
        $visited = New-Set; $node = $parent.Key
        while ($parents.ContainsKey($node)) {
            if (-not $visited.Add($node)) { throw 'Cyclic solution folder nesting' }
            $node = $parents[$node]
        }
    }
    $scopedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $names.GetEnumerator()) {
        $parent = if ($parents.ContainsKey($name.Key)) { $parents[$name.Key] } else { '<root>' }
        $kind = if ($folders.Contains($name.Key)) { 'folder/' } else { 'project/' }
        if (-not $scopedNames.Add($kind + $parent + '/' + $name.Value)) { throw "Duplicate solution display name in one folder: $($name.Value)" }
    }
    Assert-SameSet $disk $projects.Keys 'Solution'
    $requiredConfigs = foreach ($configuration in @('Debug','Release')) { foreach ($platform in @('Any CPU','x64','x86')) { "$configuration|$platform" } }
    Assert-SameSet $requiredConfigs $configs 'Solution configurations'
    foreach ($project in $projects.GetEnumerator()) {
        foreach ($config in $configs) {
            $key = $project.Value + '.' + $config
            if (-not $mappings.ContainsKey("$key.ActiveCfg") -or -not $mappings.ContainsKey("$key.Build.0")) { throw "Missing ActiveCfg/Build.0: $($project.Key) $config" }
            if ($mappings["$key.ActiveCfg"] -cne $mappings["$key.Build.0"]) { throw "Mismatched ActiveCfg/Build.0: $($project.Key) $config" }
            if ($mappings["$key.ActiveCfg"] -cnotmatch ('^' + [regex]::Escape($config.Split('|')[0]) + '\|[^|]+$')) { throw "Invalid configuration target: $($project.Key) $config" }
        }
    }
    if ($mappings.Count -ne $projects.Count * $configs.Count * 2) { throw 'Unexpected project configuration entries' }
    $payloads = New-Set
    foreach ($path in Get-Projects 'templates') { if ($path -cne $templatePackage) { [void]$payloads.Add($path) } }
    Assert-SameSet $payloads $items 'Template solution items'

    $filterRoot = Join-Path $repoRoot 'eng/ci/shards'
    $filters = @(Get-ChildItem -LiteralPath $filterRoot -Filter '*.slnf' -File -Recurse)
    if ($filters.Count -eq 0) { throw 'No CI solution filters found' }
    $filterSets = New-Map
    foreach ($filter in $filters) {
        $content = Get-Content -LiteralPath $filter.FullName -Raw | ConvertFrom-Json -NoEnumerate
        if ($content -isnot [pscustomobject] -or $null -eq $content.PSObject.Properties['solution'] -or
            $content.solution -isnot [pscustomobject] -or $null -eq $content.solution.PSObject.Properties['path'] -or
            $content.solution.path -isnot [string] -or $null -eq $content.solution.PSObject.Properties['projects'] -or
            $content.solution.projects -isnot [array] -or @($content.solution.projects | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count) {
            throw "Invalid filter shape: $($filter.Name)"
        }
        $solutionPath = $content.solution.path.Replace([char]92, [IO.Path]::DirectorySeparatorChar)
        $solution = [IO.Path]::GetFullPath((Join-Path $filter.DirectoryName $solutionPath))
        if ($solution -cne (Join-Path $repoRoot 'Excalibur.sln')) { throw "Filter references another solution: $($filter.Name)" }
        $members = New-Set
        foreach ($member in $content.solution.projects) {
            $path = Convert-RepoPath $member
            if (-not $members.Add($path)) { throw "Duplicate filter project: $($filter.Name) $path" }
            if (-not $projects.ContainsKey($path)) { throw "Filter project absent from solution: $($filter.Name) $path" }
        }
        if ($members.Count -eq 0) { throw "Empty solution filter: $($filter.Name)" }
        $filterSets.Add([IO.Path]::GetRelativePath($filterRoot, $filter.FullName).Replace([char]92, '/'), $members)
    }
    # ShippingOnly intentionally lists package roots; bundled analyzer projects build transitively.
    # The package producer validates that packability-based population. SamplesOnly is exhaustive.
    if (-not $filterSets.ContainsKey('SamplesOnly.slnf')) { throw 'Missing complete filter: SamplesOnly.slnf' }
    $expectedSamples = @($disk | Where-Object { $_.StartsWith('samples/', [StringComparison]::Ordinal) })
    Assert-SameSet $expectedSamples $filterSets['SamplesOnly.slnf'] 'SamplesOnly.slnf'
    Write-Host "PASSED: $($disk.Count) exact project paths, $($configs.Count) build configurations, $($payloads.Count) template items, $($filters.Count) CI filters."
    exit 0
}
catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
