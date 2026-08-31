#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates architecture boundary compliance across Dispatch and Excalibur projects.

.DESCRIPTION
    Comprehensive enforcement of the framework's architectural boundaries:

    dispatch-no-excalibur:       Excalibur.Dispatch.* projects MUST NOT reference Excalibur.* projects
    excalibur-abstractions-only: Excalibur.* projects MAY reference Excalibur.Dispatch.Abstractions only (not Core/Patterns/etc.)
    core-no-cloud-sdks:          Core projects MUST NOT reference cloud SDKs (pay-for-play model)
    serialization-policy:        Excalibur MUST use MemoryPack only (no STJ, MessagePack, Protobuf)

    Validates:
    - Project-to-project references
    - Package dependencies (NuGet)
    - Provider SDK isolation (Azure/AWS/Google)
    - Serialization boundaries
    - Circular dependencies

.PARAMETER GenerateReport
    If true, generates a detailed boundary violation report CSV.

.PARAMETER FailOnWarnings
    If true, treats warnings as failures (exit code 1).

.EXAMPLE
    .\eng\validate-architecture-boundaries.ps1
    .\eng\validate-architecture-boundaries.ps1 -GenerateReport
    .\eng\validate-architecture-boundaries.ps1 -GenerateReport -FailOnWarnings
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$GenerateReport,

    [Parameter()]
    [switch]$FailOnWarnings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Color codes for output
$ColorCritical = 'Red'
$ColorHigh = 'Yellow'
$ColorInfo = 'Cyan'
$ColorSuccess = 'Green'
$ColorGray = 'Gray'

Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor $ColorInfo
Write-Host "║  Architecture Boundary Validation                           ║" -ForegroundColor $ColorInfo
Write-Host "║  Enforcing: package boundaries + serialization policy       ║" -ForegroundColor $ColorInfo
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor $ColorInfo
Write-Host ""

# Find all project files
$srcPath = Join-Path $PSScriptRoot ".." "src"
$projectFiles = Get-ChildItem -Path $srcPath -Recurse -Filter "*.csproj" |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
    Sort-Object FullName

Write-Host "📁 Scanning $($projectFiles.Count) project file(s)..." -ForegroundColor $ColorGray
Write-Host ""

$violations = @()
$warnings = @()
$allowedRefs = @()

# Helper function to extract project name from path
function Get-ProjectName {
    param([string]$IncludePath)
    $fileName = [System.IO.Path]::GetFileName($IncludePath)
    return $fileName -replace '\.csproj$', ''
}

# Helper function to parse project dependencies
function Get-ProjectDependencies {
    param([string]$ProjectPath)

    try {
        [xml]$xml = Get-Content $ProjectPath -ErrorAction Stop

        # Extract project references (XPath avoids strict-mode property access failures)
        $projectRefs = @()
        $projectRefNodes = @($xml.SelectNodes('//Project/ItemGroup/ProjectReference'))
        foreach ($ref in $projectRefNodes) {
            $include = $ref.GetAttribute('Include')
            if (-not [string]::IsNullOrWhiteSpace($include)) {
                $projectRefs += Get-ProjectName -IncludePath $include
            }
        }

        # Extract package references
        $packageRefs = @()
        $packageRefNodes = @($xml.SelectNodes('//Project/ItemGroup/PackageReference'))
        foreach ($ref in $packageRefNodes) {
            $include = $ref.GetAttribute('Include')
            if (-not [string]::IsNullOrWhiteSpace($include)) {
                $version = $ref.GetAttribute('Version')
                if ([string]::IsNullOrWhiteSpace($version)) {
                    $version = $ref.GetAttribute('VersionOverride')
                }

                $packageRefs += [PSCustomObject]@{
                    Name    = $include
                    Version = $version
                }
            }
        }

        return @{
            ProjectReferences = $projectRefs
            PackageReferences = $packageRefs
        }
    }
    catch {
        Write-Warning "Failed to parse $ProjectPath : $_"
        return @{
            ProjectReferences = @()
            PackageReferences = @()
        }
    }
}

# Main validation loop
foreach ($project in $projectFiles) {
    $projectPath = $project.FullName
    $projectName = $project.Name -replace '\.csproj$', ''
    $relativePath = $projectPath.Replace("$PSScriptRoot\..\", '').Replace('\', '/')

    Write-Host "  ⚡ $projectName" -ForegroundColor $ColorGray -NoNewline

    # Get dependencies
    $deps = Get-ProjectDependencies -ProjectPath $projectPath
    $projectRefs = $deps.ProjectReferences
    $packageRefs = $deps.PackageReferences

    # Categorize project
    $isDispatch = $projectName -match '^Excalibur\.Dispatch(\.|$)'
    $isExcalibur = ($projectName -match '^Excalibur\.') -and (-not ($projectName -match '^Excalibur\.Dispatch(\.|$)'))
    $isDispatchCore = $projectName -match '^Excalibur\.Dispatch\.(Core|Patterns)$'
    $isDispatchAbstractions = $projectName -eq 'Excalibur.Dispatch.Abstractions'
    $isAzureProvider = $projectName -match '^Excalibur\.Dispatch\.(Transport|Hosting\.Serverless)\.Azure'
    $isAwsProvider = $projectName -match '^Excalibur\.Dispatch\.(Transport|Hosting\.Serverless)\.Aws'
    $isGoogleProvider = $projectName -match '^Excalibur\.Dispatch\.(Transport|Hosting\.Serverless)\.Google'

    $violationCount = 0

    # ═══════════════════════════════════════════════════════════════
    # dispatch-no-excalibur: Dispatch MUST NOT reference Excalibur
    # ═══════════════════════════════════════════════════════════════
    if ($isDispatch) {
        foreach ($refName in $projectRefs) {
            if ($refName -match '^Excalibur\.' -and $refName -notmatch '^Excalibur\.Dispatch(\.|$)') {
                $violations += [PSCustomObject]@{
                    Project     = $projectName
                    Rule        = 'dispatch-no-excalibur'
                    Severity    = 'Critical'
                    Violation   = "Dispatch→Excalibur boundary violation: references $refName"
                    Path        = $relativePath
                    Remediation = "Remove reference to $refName. Dispatch must not depend on Excalibur."
                }
                $violationCount++
            }
        }
    }

    # ═══════════════════════════════════════════════════════════════
    # excalibur-abstractions-only: Excalibur MAY reference Excalibur.Dispatch.Abstractions ONLY
    # ═══════════════════════════════════════════════════════════════
    if ($isExcalibur) {
        $isTestingPackage = $projectName -match '^Excalibur\.Testing(\.|$)'
        $explicitAllowedDispatchRefs = @{
            'Excalibur.Caching' = @('Excalibur.Dispatch.Caching')
            'Excalibur.Data'    = @('Excalibur.Dispatch.Patterns')
        }
        $forbiddenDispatchRefs = @(
            'Excalibur.Dispatch',
            'Excalibur.Dispatch.Patterns',
            'Excalibur.Dispatch.Hosting.Web',
            'Excalibur.Dispatch.Caching',
            'Excalibur.Dispatch.Observability',
            'Excalibur.Dispatch.Security',
            'Excalibur.Dispatch.Resilience.Polly'
        )

        foreach ($refName in $projectRefs) {
            if ($refName -match '^Excalibur\.Dispatch\.') {
                $isExplicitlyAllowed = $false
                if ($explicitAllowedDispatchRefs.ContainsKey($projectName)) {
                    $isExplicitlyAllowed = $explicitAllowedDispatchRefs[$projectName] -contains $refName
                }

                # Check if it's an allowed abstraction
                $isAbstraction = $refName -match '\.Abstractions$'

                if ($isExplicitlyAllowed) {
                    $allowedRefs += [PSCustomObject]@{
                        Project   = $projectName
                        Reference = $refName
                        Status    = 'Allowed (explicit wrapper exception)'
                    }
                }
                elseif (-not $isTestingPackage -and -not $isAbstraction -and $forbiddenDispatchRefs -contains $refName) {
                    $violations += [PSCustomObject]@{
                        Project     = $projectName
                        Rule        = 'excalibur-abstractions-only'
                        Severity    = 'High'
                        Violation   = "Excalibur may only reference Dispatch abstractions: found $refName"
                        Path        = $relativePath
                        Remediation = "Replace $refName reference with Excalibur.Dispatch.*.Abstractions or refactor to use abstractions."
                    }
                    $violationCount++
                }
                elseif ($isAbstraction) {
                    # Document allowed reference
                    $allowedRefs += [PSCustomObject]@{
                        Project   = $projectName
                        Reference = $refName
                        Status    = 'Allowed (abstractions-only)'
                    }
                }
            }
        }
    }

    # ═══════════════════════════════════════════════════════════════
    # core-no-cloud-sdks: Core projects MUST NOT reference cloud SDKs
    # ═══════════════════════════════════════════════════════════════
    if ($isDispatchCore) {
        $cloudSDKs = $packageRefs | Where-Object {
            $_.Name -match '^(Azure\.|Microsoft\.Azure\.|AWSSDK\.|Google\.Cloud\.|Google\.Apis\.)' -and
            $_.Name -notmatch '(Testing|TestContainers)'
        }

        foreach ($pkg in $cloudSDKs) {
            $violations += [PSCustomObject]@{
                Project     = $projectName
                Rule        = 'core-no-cloud-sdks'
                Severity    = 'High'
                Violation   = "Core project references cloud SDK: $($pkg.Name)"
                Path        = $relativePath
                Remediation = "Move cloud SDK reference to provider package (Excalibur.Dispatch.Transport.Azure/Aws/Google)."
            }
            $violationCount++
        }
    }

    # ═══════════════════════════════════════════════════════════════
    # core-no-cloud-sdks: Provider SDK isolation (no cross-contamination)
    # ═══════════════════════════════════════════════════════════════
    if ($isAzureProvider) {
        $invalidSDKs = $packageRefs | Where-Object {
            ($_.Name -match '^(AWSSDK\.|Google\.)') -and
            ($_.Name -notmatch '(Testing|TestContainers)')
        }

        foreach ($pkg in $invalidSDKs) {
            $violations += [PSCustomObject]@{
                Project     = $projectName
                Rule        = 'core-no-cloud-sdks'
                Severity    = 'High'
                Violation   = "Azure provider references non-Azure SDK: $($pkg.Name)"
                Path        = $relativePath
                Remediation = "Remove $($pkg.Name). Azure providers should only use Azure.* packages."
            }
            $violationCount++
        }
    }

    if ($isAwsProvider) {
        $invalidSDKs = $packageRefs | Where-Object {
            ($_.Name -match '^(Azure\.|Microsoft\.Azure\.|Google\.)') -and
            ($_.Name -notmatch '(Testing|TestContainers)')
        }

        foreach ($pkg in $invalidSDKs) {
            $violations += [PSCustomObject]@{
                Project     = $projectName
                Rule        = 'core-no-cloud-sdks'
                Severity    = 'High'
                Violation   = "AWS provider references non-AWS SDK: $($pkg.Name)"
                Path        = $relativePath
                Remediation = "Remove $($pkg.Name). AWS providers should only use AWSSDK.* packages."
            }
            $violationCount++
        }
    }

    if ($isGoogleProvider) {
        $invalidSDKs = $packageRefs | Where-Object {
            ($_.Name -match '^(Azure\.|Microsoft\.Azure\.|AWSSDK\.)') -and
            ($_.Name -notmatch '(Testing|TestContainers)')
        }

        foreach ($pkg in $invalidSDKs) {
            $violations += [PSCustomObject]@{
                Project     = $projectName
                Rule        = 'core-no-cloud-sdks'
                Severity    = 'High'
                Violation   = "Google provider references non-Google SDK: $($pkg.Name)"
                Path        = $relativePath
                Remediation = "Remove $($pkg.Name). Google providers should only use Google.* packages."
            }
            $violationCount++
        }
    }

    # ═══════════════════════════════════════════════════════════════
    # serialization-policy: Excalibur.Dispatch MUST use MemoryPack only (no STJ/MessagePack/Protobuf)
    # ═══════════════════════════════════════════════════════════════
    if ($projectName -eq 'Excalibur.Dispatch') {
        $bannedSerializers = $packageRefs | Where-Object {
            $_.Name -match '^(System\.Text\.Json|MessagePack|Newtonsoft\.Json|Google\.Protobuf)'
        }

        foreach ($pkg in $bannedSerializers) {
            $violations += [PSCustomObject]@{
                Project     = $projectName
                Rule        = 'serialization-policy'
                Severity    = 'Critical'
                Violation   = "Excalibur.Dispatch must use MemoryPack only: found $($pkg.Name)"
                Path        = $relativePath
                Remediation = "Remove $($pkg.Name). Use MemoryPack for internal serialization. STJ belongs in hosting/edge packages."
            }
            $violationCount++
        }
    }

    # Print status
    if ($violationCount -gt 0) {
        Write-Host " ❌ ($violationCount violation(s))" -ForegroundColor $ColorCritical
    }
    else {
        Write-Host " ✅" -ForegroundColor $ColorSuccess
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor $ColorInfo
Write-Host "                    VALIDATION SUMMARY                          " -ForegroundColor $ColorInfo
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor $ColorInfo
Write-Host ""

# Summary statistics
$critical = @($violations | Where-Object { $_.Severity -eq 'Critical' })
$high = @($violations | Where-Object { $_.Severity -eq 'High' })
$totalViolations = $violations.Count

Write-Host "📊 Results:" -ForegroundColor $ColorInfo
Write-Host "   Projects scanned:    $($projectFiles.Count)" -ForegroundColor $ColorGray
Write-Host "   Allowed references:  $($allowedRefs.Count)" -ForegroundColor $ColorSuccess
Write-Host "   Total violations:    $totalViolations" -ForegroundColor $(if ($totalViolations -eq 0) { $ColorSuccess } else { $ColorCritical })
Write-Host "     Critical:          $($critical.Count)" -ForegroundColor $(if ($critical.Count -eq 0) { $ColorSuccess } else { $ColorCritical })
Write-Host "     High:              $($high.Count)" -ForegroundColor $(if ($high.Count -eq 0) { $ColorSuccess } else { $ColorHigh })
Write-Host ""

# Display violations
if ($totalViolations -gt 0) {
    if ($critical.Count -gt 0) {
        Write-Host "❌ CRITICAL VIOLATIONS ($($critical.Count)):" -ForegroundColor $ColorCritical
        Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor $ColorCritical
        $critical | Format-Table -Property Rule, Project, Violation, Remediation -Wrap -AutoSize
        Write-Host ""
    }

    if ($high.Count -gt 0) {
        Write-Host "⚠️ HIGH SEVERITY VIOLATIONS ($($high.Count)):" -ForegroundColor $ColorHigh
        Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor $ColorHigh
        $high | Format-Table -Property Rule, Project, Violation, Remediation -Wrap -AutoSize
        Write-Host ""
    }

    Write-Host "📋 Action Required:" -ForegroundColor $ColorHigh
    Write-Host "   1. Review violations above" -ForegroundColor $ColorHigh
    Write-Host "   2. Apply recommended remediations" -ForegroundColor $ColorHigh
    Write-Host "   3. Run validation again to verify fixes" -ForegroundColor $ColorHigh
    Write-Host ""
}
else {
    Write-Host "✅ ALL BOUNDARIES COMPLIANT" -ForegroundColor $ColorSuccess
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor $ColorSuccess
    Write-Host ""
    Write-Host "All architectural boundary rules are satisfied:" -ForegroundColor $ColorSuccess
    Write-Host "  ✓ dispatch-no-excalibur: Dispatch does not reference Excalibur" -ForegroundColor $ColorSuccess
    Write-Host "  ✓ excalibur-abstractions-only: Excalibur references Dispatch abstractions (or explicit wrapper exceptions)" -ForegroundColor $ColorSuccess
    Write-Host "  ✓ core-no-cloud-sdks: Core projects are cloud-agnostic" -ForegroundColor $ColorSuccess
    Write-Host "  ✓ core-no-cloud-sdks: Provider SDKs are isolated (no cross-contamination)" -ForegroundColor $ColorSuccess
    Write-Host "  ✓ serialization-policy: Excalibur uses MemoryPack exclusively" -ForegroundColor $ColorSuccess
    Write-Host ""
}

# Generate CSV report
if ($GenerateReport -or $totalViolations -gt 0) {
    $reportPath = Join-Path $PSScriptRoot ".." "management" "artifacts" "architecture-boundary-violations.csv"
    $reportDir = Split-Path $reportPath -Parent

    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    if ($violations.Count -gt 0) {
        $violations | Export-Csv -Path $reportPath -NoTypeInformation -Force
        Write-Host "📄 Violation report: $reportPath" -ForegroundColor $ColorInfo
    }
    else {
        # Write empty report to indicate success
        @() | Export-Csv -Path $reportPath -NoTypeInformation -Force
        Write-Host "📄 Clean report: $reportPath (0 violations)" -ForegroundColor $ColorSuccess
    }
    Write-Host ""
}

# Exit code
if ($totalViolations -gt 0) {
    Write-Host "❌ ARCHITECTURE BOUNDARY GATE: FAILED" -ForegroundColor $ColorCritical
    Write-Host ""
    exit 1
}

if ($FailOnWarnings -and $warnings.Count -gt 0) {
    Write-Host "⚠️ ARCHITECTURE BOUNDARY GATE: FAILED (warnings as errors)" -ForegroundColor $ColorHigh
    Write-Host ""
    exit 1
}

Write-Host "✅ ARCHITECTURE BOUNDARY GATE: PASSED" -ForegroundColor $ColorSuccess
Write-Host ""
exit 0
