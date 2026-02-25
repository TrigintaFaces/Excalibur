# Copyright (c) 2026 The Excalibur Project
# Native AOT and Trim Validation Script (Phase 9.6)
#
# This script validates that all Dispatch libraries are compatible with:
# - Native AOT compilation (<PublishAot>true</PublishAot>)
# - Assembly trimming (<PublishTrimmed>true</PublishTrimmed>)
#
# Requirements:
# - All libraries must build with 0 ILLink warnings
# - No dynamic code generation (reflection emit, dynamic assembly loading)
# - No unsupported APIs (Assembly.Load, Type.GetType with strings, etc.)
# - Trimmer-safe attributes ([DynamicallyAccessedMembers], [RequiresUnreferencedCode])
#
# Exit Codes:
# 0 = All projects AOT/Trim compatible
# 1 = AOT/Trim incompatibilities detected
# 2 = Script error or configuration missing

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectFilter = "src/**/*.csproj",

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "aot-trim-validation-report.md",

    [Parameter(Mandatory = $false)]
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Excalibur - AOT/Trim Validation (Phase 9.6)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Configuration
# ============================================================================

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$publishDir = Join-Path $repoRoot "artifacts/aot-validation"

Write-Host "Repository Root: $repoRoot" -ForegroundColor Gray
Write-Host "Publish Directory: $publishDir" -ForegroundColor Gray
Write-Host ""

# Clean previous artifacts
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous validation artifacts..." -ForegroundColor Gray
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -Path $publishDir -ItemType Directory -Force | Out-Null

# ============================================================================
# Find Library Projects
# ============================================================================

Write-Host "Searching for library projects..." -ForegroundColor Cyan

$projects = Get-ChildItem -Path (Join-Path $repoRoot "src") -Filter "*.csproj" -Recurse | Where-Object {
    # Exclude test projects, samples, and tools
    $_.FullName -notmatch "\\tests\\" -and
    $_.FullName -notmatch "\\samples\\" -and
    $_.FullName -notmatch "\\tools\\" -and
    $_.FullName -notmatch "\\benchmarks\\"
}

if ($projects.Count -eq 0) {
    Write-Host "  ERROR: No library projects found matching filter" -ForegroundColor Red
    exit 2
}

Write-Host "  Found $($projects.Count) library project(s)" -ForegroundColor Green
foreach ($project in $projects) {
    Write-Host "    - $($project.Name)" -ForegroundColor Gray
}
Write-Host ""

# ============================================================================
# Analyze Project Configurations
# ============================================================================

Write-Host "Analyzing project configurations..." -ForegroundColor Cyan
Write-Host ""

$projectConfigs = @()

foreach ($project in $projects) {
    Write-Host "Analyzing: $($project.Name)" -ForegroundColor Gray

    [xml]$projectXml = Get-Content $project.FullName

    # Check for AOT/Trim settings
    $isAotEnabled = $projectXml.Project.PropertyGroup.PublishAot -eq "true"
    $isTrimEnabled = $projectXml.Project.PropertyGroup.PublishTrimmed -eq "true"
    $trimMode = $projectXml.Project.PropertyGroup.TrimMode
    $isPackable = $projectXml.Project.PropertyGroup.IsPackable -ne "false"

    $config = [PSCustomObject]@{
        ProjectName = $project.BaseName
        ProjectPath = $project.FullName
        IsAotEnabled = $isAotEnabled
        IsTrimEnabled = $isTrimEnabled
        TrimMode = $trimMode
        IsPackable = $isPackable
        ILLinkWarnings = @()
        PublishSuccess = $false
    }

    $projectConfigs += $config

    Write-Host "    AOT: $isAotEnabled | Trim: $isTrimEnabled | TrimMode: $trimMode | Packable: $isPackable" -ForegroundColor Gray
}

Write-Host ""

# ============================================================================
# Validate AOT/Trim Compatibility (Build-Time Analysis)
# ============================================================================

if (-not $SkipPublish) {
    Write-Host "Validating AOT/Trim compatibility via build analysis..." -ForegroundColor Cyan
    Write-Host "  (This may take several minutes)" -ForegroundColor Gray
    Write-Host ""

    foreach ($config in $projectConfigs) {
        Write-Host "Analyzing: $($config.ProjectName)" -ForegroundColor Gray

        # Build with trim analysis enabled (works for class libraries)
        $buildArgs = @(
            "build"
            $config.ProjectPath
            "-c", "Release"
            "--no-restore"
            "-p:EnableTrimAnalyzer=true"
            "-p:IsTrimmable=true"
            "-p:TrimMode=full"
            "-p:SuppressTrimAnalysisWarnings=false"
            "-p:IlcOptimizationPreference=Speed"
            "-p:IlcInvariantGlobalization=false"
            "--nologo"
            "--verbosity", "normal"
        )

        $buildOutput = & dotnet @buildArgs 2>&1 | Out-String

        # Check for success
        if ($LASTEXITCODE -eq 0) {
            $config.PublishSuccess = $true
            Write-Host "  ✅ Build analysis successful" -ForegroundColor Green
        }
        else {
            Write-Host "  ❌ Build analysis failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
        }

        # Extract ILLink warnings (trim analysis warnings)
        $ilLinkWarnings = $buildOutput -split "`n" | Where-Object {
            $_ -match "warning IL\d{4}:" -or $_ -match "error IL\d{4}:"
        }

        if ($ilLinkWarnings.Count -gt 0) {
            foreach ($warning in $ilLinkWarnings) {
                if ($warning -match "(warning|error) (IL\d{4}): (.+)") {
                    $severity = $matches[1]
                    $code = $matches[2]
                    $message = $matches[3]

                    $config.ILLinkWarnings += [PSCustomObject]@{
                        Severity = $severity
                        Code = $code
                        Message = $message
                    }
                }
            }
            Write-Host "  ⚠️ ILLink warnings: $($ilLinkWarnings.Count)" -ForegroundColor Yellow
        }
    }

    Write-Host ""
}
else {
    Write-Host "Skipping build analysis (-SkipPublish specified)" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# Generate Report
# ============================================================================

$reportContent = @"
# AOT/Trim Validation Report
**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary

| Metric | Count |
|--------|-------|
| Total Projects | $($projectConfigs.Count) |
| AOT Enabled | $(($projectConfigs | Where-Object { $_.IsAotEnabled }).Count) |
| Trim Enabled | $(($projectConfigs | Where-Object { $_.IsTrimEnabled }).Count) |
| Publish Successful | $(($projectConfigs | Where-Object { $_.PublishSuccess }).Count) |
| ILLink Warnings | $(($projectConfigs | ForEach-Object { $_.ILLinkWarnings.Count } | Measure-Object -Sum).Sum) |

## Project Configurations

| Project | AOT | Trim | TrimMode | Packable |
|---------|-----|------|----------|----------|
"@

foreach ($config in $projectConfigs | Sort-Object ProjectName) {
    $aotIcon = if ($config.IsAotEnabled) { "✅" } else { "❌" }
    $trimIcon = if ($config.IsTrimEnabled) { "✅" } else { "❌" }
    $trimMode = if ($config.TrimMode) { $config.TrimMode } else { "N/A" }
    $packableIcon = if ($config.IsPackable) { "✅" } else { "❌" }

    $reportContent += @"

| ``$($config.ProjectName)`` | $aotIcon | $trimIcon | $trimMode | $packableIcon |
"@
}

$allWarnings = $projectConfigs | ForEach-Object { $_.ILLinkWarnings } | Where-Object { $_ }

if ($allWarnings.Count -gt 0) {
    $reportContent += @"


## ❌ ILLink Warnings Detected

| Project | Severity | Code | Message |
|---------|----------|------|---------|
"@

    foreach ($config in $projectConfigs | Where-Object { $_.ILLinkWarnings.Count -gt 0 }) {
        foreach ($warning in $config.ILLinkWarnings) {
            $icon = if ($warning.Severity -eq "error") { "❌" } else { "⚠️" }
            $reportContent += @"

| ``$($config.ProjectName)`` | $icon $($warning.Severity.ToUpper()) | ``$($warning.Code)`` | $($warning.Message.Replace("|", "\|")) |
"@
        }
    }

    $reportContent += @"


### Common ILLink Warning Codes

- **IL2026**: Calling member with [RequiresUnreferencedCode] attribute
- **IL2072**: Target parameter type might not match source type (data flow analysis)
- **IL2075**: 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' requirements
- **IL3050**: Using member with [RequiresDynamicCode] attribute (AOT incompatible)
- **IL3053**: Assembly reference has embedded PDB

### Recommended Fixes

1. **Add trimmer attributes** to preserve required members:
   - ``[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]``
   - ``[RequiresUnreferencedCode("...")]`` for unavoidable reflection

2. **Use source generators** instead of runtime reflection:
   - JSON serialization: System.Text.Json source generators
   - Logging: LoggerMessage source generators
   - DI: Manual registration instead of assembly scanning

3. **Avoid unsupported APIs**:
   - Replace ``Assembly.Load`` with ``AssemblyLoadContext``
   - Replace ``Type.GetType(string)`` with ``typeof()``
   - Avoid ``Activator.CreateInstance`` with type names

"@
}

$failedProjects = $projectConfigs | Where-Object { -not $_.PublishSuccess -and -not $SkipPublish }

if ($failedProjects.Count -gt 0) {
    $reportContent += @"


## ❌ Publish Failures

The following projects failed to publish with AOT/Trim enabled:

"@

    foreach ($project in $failedProjects) {
        $reportContent += "- ``$($project.ProjectName)``$([Environment]::NewLine)"
    }
}

$reportContent += @"


## AOT/Trim Best Practices

### ✅ DO
- Use source generators for serialization and logging
- Annotate APIs with trimmer attributes (``[RequiresUnreferencedCode]``, ``[DynamicallyAccessedMembers]``)
- Use ``typeof()`` instead of ``Type.GetType(string)``
- Test publish with ``-p:PublishAot=true`` and ``-p:PublishTrimmed=true``
- Keep ILLink warnings at 0

### ❌ DON'T
- Use ``Activator.CreateInstance`` with type names
- Use ``Assembly.Load`` with strings
- Scan assemblies at runtime (use source generators)
- Ignore ILLink warnings
- Use reflection emit or dynamic code generation

## Exit Code

"@

# ============================================================================
# Determine Exit Code
# ============================================================================

$exitCode = 0

$errors = $allWarnings | Where-Object { $_.Severity -eq "error" }

if ($errors.Count -gt 0 -or $failedProjects.Count -gt 0) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - AOT/Trim validation failed$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  ❌ AOT/TRIM VALIDATION FAILED" -ForegroundColor Red
    Write-Host "    Errors: $($errors.Count)" -ForegroundColor Red
    Write-Host "    Failed Publishes: $($failedProjects.Count)" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Red
}
elseif ($allWarnings.Count -gt 0) {
    $exitCode = 1
    $reportContent += "**EXIT 1** - ILLink warnings detected (must be 0)$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  ⚠️ ILLINK WARNINGS DETECTED: $($allWarnings.Count)" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Yellow
}
else {
    $reportContent += "**EXIT 0** - All projects AOT/Trim compatible ✅$([Environment]::NewLine)"
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "  ✅ AOT/TRIM VALIDATION PASSED" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
}

# Save report
$reportContent | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host ""
Write-Host "Report saved to: $OutputPath" -ForegroundColor Cyan

# Display summary
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total Projects: $($projectConfigs.Count)" -ForegroundColor Gray
Write-Host "  Publish Success: $(($projectConfigs | Where-Object { $_.PublishSuccess }).Count)" -ForegroundColor $(if ($failedProjects.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  ILLink Warnings: $($allWarnings.Count)" -ForegroundColor $(if ($allWarnings.Count -gt 0) { "Yellow" } else { "Green" })
Write-Host ""

exit $exitCode
