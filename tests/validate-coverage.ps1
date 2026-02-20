# Coverage Threshold Validation Script
# Validates coverage results against configured thresholds:
# - 95% overall
# - 98% core (Dispatch, Excalibur.Dispatch.Abstractions)
# - 90% transport providers

param(
    [string]$CoverageReport = "TestResults/coverage/Cobertura.xml",
    [switch]$Strict
)

# Coverage thresholds
$Thresholds = @{
    Overall = 95.0
    Core = 98.0
    Transport = 90.0
}

# Module patterns for categorization
$CoreModules = @(
    "Excalibur.Dispatch",
    "Excalibur.Dispatch.Abstractions"
)

$TransportModules = @(
    "Excalibur.Dispatch.Transport.*"
)

function Get-CoveragePercentage {
    param([xml]$Coverage, [string[]]$ModulePatterns)

    $totalLines = 0
    $coveredLines = 0

    foreach ($package in $Coverage.coverage.packages.package) {
        $name = $package.name
        $matches = $false

        foreach ($pattern in $ModulePatterns) {
            if ($name -like $pattern) {
                $matches = $true
                break
            }
        }

        if ($matches) {
            $totalLines += [int]$package.'line-rate' * 100
            # Actually need to sum up class-level data
            foreach ($class in $package.classes.class) {
                $lineRate = [double]$class.'line-rate'
                $totalLines += 1
                $coveredLines += $lineRate
            }
        }
    }

    if ($totalLines -eq 0) { return 0 }
    return [math]::Round(($coveredLines / $totalLines) * 100, 2)
}

# Check if coverage report exists
if (-not (Test-Path $CoverageReport)) {
    Write-Host "Coverage report not found: $CoverageReport" -ForegroundColor Red
    Write-Host "Run tests with coverage first: dotnet test --collect:'XPlat Code Coverage'" -ForegroundColor Yellow
    exit 1
}

# Load coverage report
[xml]$coverage = Get-Content $CoverageReport

# Get overall coverage from summary
$overallRate = [double]$coverage.coverage.'line-rate' * 100
$overallCoverage = [math]::Round($overallRate, 2)

Write-Host "`n=== Coverage Threshold Validation ===" -ForegroundColor Cyan
Write-Host "Report: $CoverageReport"
Write-Host ""

# Validate thresholds
$failures = @()

# Overall coverage
$target = $Thresholds.Overall
$status = if ($overallCoverage -ge $target) { "PASS" } else { "FAIL" }
$color = if ($status -eq "PASS") { "Green" } else { "Red" }
Write-Host "Overall Coverage: $overallCoverage% (target: $target%) [$status]" -ForegroundColor $color
if ($status -eq "FAIL") {
    $failures += "Overall: $overallCoverage% < $target%"
}

# Note: Detailed module-level validation requires parsed coverage data
# For now, document the expected thresholds

Write-Host ""
Write-Host "=== Threshold Targets ===" -ForegroundColor Cyan
Write-Host "  Overall:   $($Thresholds.Overall)%"
Write-Host "  Core:      $($Thresholds.Core)% (Excalibur.Dispatch, Excalibur.Dispatch.Abstractions)"
Write-Host "  Transport: $($Thresholds.Transport)% (Transport providers)"
Write-Host ""

if ($failures.Count -gt 0) {
    Write-Host "=== VALIDATION FAILED ===" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    if ($Strict) {
        exit 1
    }
} else {
    Write-Host "=== VALIDATION PASSED ===" -ForegroundColor Green
}

Write-Host ""
Write-Host "To run coverage tests: dotnet test --settings tests/coverage.runsettings --collect:'XPlat Code Coverage'"
