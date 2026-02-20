#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Hot-path restriction enforcement script per Article III
.DESCRIPTION
    Validates banned API compliance to enforce Article III (Strict Time & Scheduling Policy)
    and hot-path performance restrictions for the Excalibur framework.
.PARAMETER AnalyzeSource
    Perform static source code analysis in addition to analyzer validation
.PARAMETER CheckHotPaths
    Identify potential hot-path violations through pattern analysis
.PARAMETER ExportReport
    Export detailed report to management/reports/
.PARAMETER DetailedOutput
    Enable detailed output with violation-level analysis
.EXAMPLE
    .\eng\verify-banned-apis.ps1 -AnalyzeSource -CheckHotPaths -ExportReport -DetailedOutput
#>
[CmdletBinding()]
param(
    [switch]$AnalyzeSource,
    [switch]$CheckHotPaths,
    [switch]$ExportReport,
    [switch]$DetailedOutput
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot | Split-Path
$srcDir = Join-Path $repoRoot "src"
$bannedSymbolsPath = Join-Path $repoRoot "eng/banned/BannedSymbols.txt"

Write-Host "🚫 Banned API Compliance Validation (Article III)" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host ""

$violations = @()
$warnings = @()
$hotPathViolations = @()
$analysisResults = @{}

# Load banned symbols configuration
if (-not (Test-Path $bannedSymbolsPath)) {
    Write-Error "❌ BannedSymbols.txt not found at: $bannedSymbolsPath"
    exit 1
}

Write-Host "📋 Loading banned symbols configuration..." -ForegroundColor Yellow
$bannedRules = @{}
$currentCategory = "General"

foreach ($rawLine in Get-Content $bannedSymbolsPath) {
    $line = $rawLine.Trim()
    if (-not $line) { continue }

    if ($line.StartsWith("#")) {
        if ($line.Contains(":")) {
            # Category header: # Article III: Strict Time & Scheduling Policy
            $currentCategory = $line.Substring(1).Trim()
        }
        continue
    }

    if ($line -match "^(?<kind>T|M|N):(?<symbol>[^;]+?)(?:;(?<reason>.+))?$") {
        $kind = $Matches['kind']
        $symbol = $Matches['symbol'].Trim()
        $reason = if ($Matches['reason']) { $Matches['reason'].Trim() } else { 'Banned by Article III policy' }
        $type = switch ($kind) {
            'T' { 'Type' }
            'M' { 'Method' }
            'N' { 'Namespace' }
            default { 'Unknown' }
        }

        $bannedRules[$symbol] = @{
            Type = $type
            Reason = $reason
            Category = $currentCategory
        }
    }
}

Write-Host "Loaded $($bannedRules.Count) banned API rules" -ForegroundColor Gray
Write-Host ""

# Hot-path patterns that indicate performance-critical code
$hotPathPatterns = @(
    @{ Pattern = "while\s*\(.*\)"; Description = "While loops (potential hot path)" }
    @{ Pattern = "for\s*\(.*\)"; Description = "For loops (potential hot path)" }
    @{ Pattern = "foreach\s*\(.*\)"; Description = "Foreach loops (potential hot path)" }
    @{ Pattern = "\.Where\(|\.Select\(|\.OrderBy\("; Description = "LINQ operations (banned in hot paths)" }
    @{ Pattern = "Task\.Run|ThreadPool\.QueueUserWorkItem"; Description = "Thread spawning (check if appropriate)" }
    @{ Pattern = "new\s+\w+\["; Description = "Array allocations (check if pooled)" }
    @{ Pattern = 'string\.Concat|string\.Format|\$"'; Description = "String operations (check for StringBuilder)" }
)

# Performance-critical project patterns
$hotPathProjectPatterns = @(
    "*Dispatch*",
    "*Core*", 
    "*Messaging*",
    "*Pipeline*",
    "*Router*"
)

function Test-WildcardMatch {
    param($Pattern, $Text)
    $regexPattern = "^" + $Pattern.Replace("*", ".*").Replace("?", ".") + "$"
    return $Text -match $regexPattern
}

function Test-IsHotPathProject {
    param($ProjectName)
    foreach ($pattern in $hotPathProjectPatterns) {
        if (Test-WildcardMatch -Pattern $pattern -Text $ProjectName) {
            return $true
        }
    }
    return $false
}

function Analyze-SourceFile {
    param($FilePath, $ProjectName)
    
    if (-not (Test-Path $FilePath)) {
        return @()
    }
    
    $violations = @()
    $content = Get-Content $FilePath -Raw
    $lines = Get-Content $FilePath
    $isHotPathProject = Test-IsHotPathProject -ProjectName $ProjectName
    
    # Check for banned API usage
    foreach ($bannedApi in $bannedRules.Keys) {
        $rule = $bannedRules[$bannedApi]
        $escapedApi = [regex]::Escape($bannedApi)
        
        switch ($rule.Type) {
            "Type" {
                $shortType = ($bannedApi -split "\.")[-1] -replace "`\d+$", ""
                $escapedShortType = [regex]::Escape($shortType)
                if ($content -match "\b$escapedApi\b" -or $content -match "\b$escapedShortType\b") {
                    $violations += @{
                        Type = "BannedType"
                        API = $bannedApi
                        Reason = $rule.Reason
                        Category = $rule.Category
                        File = $FilePath
                        Severity = "Error"
                    }
                }
            }
            "Method" {
                # Match by method name regardless of signature overload details
                $methodPattern = (($bannedApi -split "\(")[0] -split "\.")[-1]
                $escapedMethodPattern = [regex]::Escape($methodPattern)
                if ($content -match "\b$escapedMethodPattern\s*\(") {
                    $violations += @{
                        Type = "BannedMethod"
                        API = $bannedApi
                        Reason = $rule.Reason
                        Category = $rule.Category
                        File = $FilePath
                        Severity = "Error"
                    }
                }
            }
            "Namespace" {
                if ($content -match "using\s+$escapedApi" -or $content -match "\b$escapedApi\.") {
                    $violations += @{
                        Type = "BannedNamespace"
                        API = $bannedApi
                        Reason = $rule.Reason
                        Category = $rule.Category
                        File = $FilePath
                        Severity = "Error"
                    }
                }
            }
        }
    }
    
    # Check hot-path patterns if in performance-critical project
    if ($CheckHotPaths -and $isHotPathProject) {
        foreach ($pattern in $hotPathPatterns) {
            if ($content -match $pattern.Pattern) {
                $matches = [regex]::Matches($content, $pattern.Pattern)
                foreach ($match in $matches) {
                    # Find line number
                    $lineNum = 1
                    $pos = 0
                    foreach ($line in $lines) {
                        if ($pos + $line.Length -ge $match.Index) {
                            break
                        }
                        $pos += $line.Length + 1
                        $lineNum++
                    }
                    
                    $violations += @{
                        Type = "HotPathPattern"
                        Pattern = $pattern.Pattern
                        Description = $pattern.Description
                        File = $FilePath
                        LineNumber = $lineNum
                        Severity = "Warning"
                    }
                }
            }
        }
    }
    
    return $violations
}

# Scan all C# source files
Write-Host "🔍 Scanning source files for banned API usage..." -ForegroundColor Yellow

if (-not (Test-Path $srcDir)) {
    Write-Error "❌ src/ directory not found"
    exit 1
}

$sourceFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($sourceFiles.Count) C# source files" -ForegroundColor Gray

$projectFiles = Get-ChildItem -Path $srcDir -Filter "*.csproj" -Recurse
foreach ($projectFile in $projectFiles) {
    $projectName = $projectFile.BaseName
    $projectDir = $projectFile.Directory.FullName
    
    Write-Host "📦 Analyzing project: $projectName" -ForegroundColor Cyan
    
    $projectViolations = @()
    $projectHotPathViolations = @()
    
    # Find source files in this project
    $projectSourceFiles = $sourceFiles | Where-Object { 
        $_.FullName.StartsWith($projectDir)
    }
    
    if ($AnalyzeSource) {
        foreach ($sourceFile in $projectSourceFiles) {
            $fileViolations = Analyze-SourceFile -FilePath $sourceFile.FullName -ProjectName $projectName
            
            foreach ($violation in $fileViolations) {
                if ($violation.Type -eq "HotPathPattern") {
                    $projectHotPathViolations += $violation
                    $hotPathViolations += $violation
                } else {
                    $projectViolations += $violation
                    $violations += $violation
                }
                
                if ($DetailedOutput) {
                    $relativeFile = $violation.File.Substring($repoRoot.Length + 1)
                    switch ($violation.Severity) {
                        "Error" {
                            Write-Host "   ❌ $($violation.Type): $($violation.API)" -ForegroundColor Red
                            Write-Host "      File: $relativeFile" -ForegroundColor Red
                            Write-Host "      Reason: $($violation.Reason)" -ForegroundColor Red
                        }
                        "Warning" {
                            Write-Host "   ⚠️  $($violation.Description)" -ForegroundColor Yellow
                            Write-Host "      File: ${relativeFile}:$($violation.LineNumber)" -ForegroundColor Yellow
                        }
                    }
                }
            }
        }
    }
    
    # Check if project has BannedApiAnalyzers configured
    [xml]$projectContent = Get-Content $projectFile.FullName
    $hasAnalyzer = $false
    $packageRefs = $projectContent.Project.ItemGroup.PackageReference
    if ($packageRefs) {
        foreach ($packageRef in $packageRefs) {
            if ($packageRef.Include -eq "Microsoft.CodeAnalysis.BannedApiAnalyzers") {
                $hasAnalyzer = $true
                break
            }
        }
    }
    
    if (-not $hasAnalyzer) {
        $warnings += "Project $projectName does not reference Microsoft.CodeAnalysis.BannedApiAnalyzers"
        Write-Host "   ⚠️  Missing BannedApiAnalyzers reference" -ForegroundColor Yellow
    } else {
        Write-Host "   ✅ BannedApiAnalyzers configured" -ForegroundColor Green
    }
    
    $analysisResults[$projectName] = @{
        ProjectPath = $projectFile.FullName
        SourceFiles = $projectSourceFiles.Count
        Violations = $projectViolations.Count
        HotPathViolations = $projectHotPathViolations.Count
        HasAnalyzer = $hasAnalyzer
        Details = $projectViolations + $projectHotPathViolations
    }
    
    if ($DetailedOutput) {
        Write-Host "   📁 Source files: $($projectSourceFiles.Count)" -ForegroundColor Gray
        Write-Host "   ❌ Violations: $($projectViolations.Count)" -ForegroundColor Gray
        Write-Host "   ⚠️  Hot-path warnings: $($projectHotPathViolations.Count)" -ForegroundColor Gray
    }
    Write-Host ""
}

# Check if banned symbols file is referenced in build files
Write-Host "🔍 Validating BannedSymbols.txt integration..." -ForegroundColor Yellow
$directoryBuildProps = Join-Path $repoRoot "Directory.Build.props"
$hasBannedSymbolsReference = $false

if (Test-Path $directoryBuildProps) {
    $content = Get-Content $directoryBuildProps -Raw
    if ($content -match "BannedSymbols\.txt") {
        $hasBannedSymbolsReference = $true
        Write-Host "✅ BannedSymbols.txt referenced in Directory.Build.props" -ForegroundColor Green
    } else {
        $warnings += "BannedSymbols.txt not referenced in Directory.Build.props"
        Write-Host "⚠️  BannedSymbols.txt not integrated in build" -ForegroundColor Yellow
    }
} else {
    $warnings += "Directory.Build.props not found"
}

# Generate summary report
if ($ExportReport) {
    Write-Host "📊 Generating banned API compliance report..." -ForegroundColor Yellow
    $reportDir = Join-Path $repoRoot "management/reports"
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    
    $report = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
        Summary = @{
            TotalProjects = $projectFiles.Count
            TotalSourceFiles = $sourceFiles.Count
            BannedApiViolations = $violations.Count
            HotPathWarnings = $hotPathViolations.Count
            TotalWarnings = $warnings.Count
            Status = if ($violations.Count -eq 0) { "PASS" } else { "FAIL" }
        }
        BannedRules = $bannedRules
        ProjectAnalysis = $analysisResults
        Violations = $violations
        HotPathViolations = $hotPathViolations
        Warnings = $warnings
    }
    
    $reportPath = Join-Path $reportDir "banned-api-compliance-report.json"
    $report | ConvertTo-Json -Depth 10 | Set-Content $reportPath
    Write-Host "✅ Report saved to: $reportPath" -ForegroundColor Green
}

# Summary
Write-Host "📋 Banned API Compliance Summary" -ForegroundColor Cyan
Write-Host "Projects analyzed: $($projectFiles.Count)" -ForegroundColor Gray
Write-Host "Source files analyzed: $($sourceFiles.Count)" -ForegroundColor Gray
Write-Host "Banned API violations: $($violations.Count)" -ForegroundColor $(if ($violations.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Hot-path warnings: $($hotPathViolations.Count)" -ForegroundColor $(if ($hotPathViolations.Count -gt 0) { "Yellow" } else { "Green" })
Write-Host "Configuration warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ BANNED API VIOLATIONS (must fix):" -ForegroundColor Red
    $groupedViolations = $violations | Group-Object { $_.API }
    foreach ($group in $groupedViolations) {
        Write-Host "  • $($group.Name): $($group.Count) occurrences" -ForegroundColor Red
        if ($DetailedOutput) {
            foreach ($violation in $group.Group) {
                $relativeFile = $violation.File.Substring($repoRoot.Length + 1)
                Write-Host "    - $relativeFile" -ForegroundColor Red
            }
        }
    }
}

if ($hotPathViolations.Count -gt 0 -and $CheckHotPaths) {
    Write-Host ""
    Write-Host "⚠️  HOT-PATH WARNINGS (review for performance):" -ForegroundColor Yellow
    $groupedHotPath = $hotPathViolations | Group-Object { $_.Description }
    foreach ($group in $groupedHotPath) {
        Write-Host "  • $($group.Name): $($group.Count) occurrences" -ForegroundColor Yellow
    }
}

if ($warnings.Count -gt 0 -and $DetailedOutput) {
    Write-Host ""
    Write-Host "⚠️  CONFIGURATION WARNINGS:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  • $warning" -ForegroundColor Yellow
    }
}

Write-Host ""
if ($violations.Count -eq 0) {
    Write-Host "✅ Banned API compliance validation PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Banned API compliance validation FAILED" -ForegroundColor Red
    Write-Host "💡 Review Article III requirements and replace banned APIs with approved alternatives" -ForegroundColor Cyan
    exit 1
}
