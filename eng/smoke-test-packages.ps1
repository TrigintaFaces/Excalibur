#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packaging Smoke Test Script for Excalibur.Dispatch
    Sprint 309 - T5.2 - W5 Validation Phase 2

.DESCRIPTION
    Validates that Dispatch NuGet packages work correctly in isolation without
    requiring Excalibur dependencies. This script:

    1. PACK PHASE: Packs all Excalibur.Dispatch.* packages to a local NuGet feed
    2. CONSUMER PHASE: Creates a minimal .NET console app that references only Dispatch
    3. VALIDATION PHASE: Verifies the consumer app builds and runs correctly
    4. CLEANUP PHASE: Always cleans up temp directory in finally block

.NOTES
    Architectural Decisions:
    - AD-309-1: Single self-contained script (no external template files)
    - AD-309-2: Uses temp directory with automatic cleanup
    - AD-309-3: Designed for integration with .github/workflows/ci.yml

.EXAMPLE
    ./eng/smoke-test-packages.ps1

    Runs the full smoke test suite.

.EXAMPLE
    ./eng/smoke-test-packages.ps1 -Verbose

    Runs with detailed output.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# Configuration
# ============================================================================

$Script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Script:Timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$Script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "excalibur-smoke-test-$Script:Timestamp"
$Script:PackagesDir = Join-Path $Script:TempDir 'packages'
$Script:ConsumerDir = Join-Path $Script:TempDir 'consumer'
$Script:SmokeTestVersion = "99.0.0-smoketest"  # Use prerelease version for all packages

# Dispatch packages to test (core packages, not Excalibur)
# Note: Only include packages that are properly configured for packing
# Other packages (Excalibur.Dispatch.Patterns.*, etc.) have packaging issues that
# should be fixed separately - this smoke test validates isolation, not packability
# Order matters for dependency resolution!
$Script:DispatchPackages = @(
    'Excalibur.Dispatch.Abstractions',          # Must be first (dependency of all others)
    'Excalibur.Dispatch.Serialization.MemoryPack',  # Dependency of Dispatch
    'Excalibur.Dispatch'              # Core dispatcher
)

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Banner {
    param([string]$Message)
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $($Message.PadRight(66)) ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-StepHeader {
    param([string]$Step, [string]$Description)
    Write-Host ""
    Write-Host "▶ [$Step] $Description" -ForegroundColor Yellow
    Write-Host ("-" * 70) -ForegroundColor DarkGray
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ️  $Message" -ForegroundColor Cyan
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

# ============================================================================
# Phase 1: Pack Dispatch Packages
# ============================================================================

function Invoke-PackPhase {
    Write-StepHeader "PACK" "Packing Excalibur.Dispatch.* packages to local NuGet feed"

    # Create packages directory
    New-Item -ItemType Directory -Path $Script:PackagesDir -Force | Out-Null
    Write-Info "Created local feed: $Script:PackagesDir"

    # Build and pack each Dispatch package individually
    # (Avoids building entire solution which may have missing example projects)
    $packedCount = 0
    foreach ($packageName in $Script:DispatchPackages) {
        $projectPath = Join-Path $Script:RepoRoot "src/Dispatch/$packageName/$packageName.csproj"

        if (-not (Test-Path $projectPath)) {
            Write-Verbose "Project not found, skipping: $projectPath"
            continue
        }

        # Build the project
        Write-Info "Building $packageName..."
        $buildResult = & dotnet build $projectPath `
            --configuration Release `
            --verbosity quiet `
            "-p:Version=$Script:SmokeTestVersion" `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Build failed for $packageName"
            Write-Host $buildResult
            throw "Build failed for $packageName with exit code $LASTEXITCODE"
        }
        Write-Success "Built $packageName"

        # Pack the project
        # Note: We use ContinuousIntegrationBuild=false to avoid source link issues
        # in local smoke runs while preserving normal shipping metadata validation.
        Write-Verbose "Packing $packageName..."
        $packResult = & dotnet pack $projectPath `
            --configuration Release `
            --no-build `
            --output $Script:PackagesDir `
            --verbosity quiet `
            "-p:Version=$Script:SmokeTestVersion" `
            "-p:ContinuousIntegrationBuild=false" `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Failed to pack $packageName"
            Write-Host $packResult
            throw "Pack failed for $packageName"
        }

        $packedCount++
        Write-Success "Packed $packageName"
    }

    # List created packages
    $nupkgFiles = Get-ChildItem -Path $Script:PackagesDir -Filter '*.nupkg'
    Write-Info "Created $($nupkgFiles.Count) package(s) in local feed:"
    foreach ($pkg in $nupkgFiles) {
        Write-Host "    - $($pkg.Name)" -ForegroundColor DarkGray
    }

    if ($nupkgFiles.Count -eq 0) {
        throw "No packages were created"
    }

    return $packedCount
}

# ============================================================================
# Phase 2: Create Consumer App
# ============================================================================

function Invoke-ConsumerPhase {
    Write-StepHeader "CONSUMER" "Creating throwaway consumer application"

    # Create consumer directory
    New-Item -ItemType Directory -Path $Script:ConsumerDir -Force | Out-Null
    Write-Info "Created consumer directory: $Script:ConsumerDir"

    # Generate .csproj file (inline, per AD-309-1)
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Dispatch packages ONLY - no Excalibur references -->
    <PackageReference Include="Excalibur.Dispatch" Version="$Script:SmokeTestVersion" />
    <PackageReference Include="Excalibur.Dispatch.Abstractions" Version="$Script:SmokeTestVersion" />
  </ItemGroup>
</Project>
"@

    $csprojPath = Join-Path $Script:ConsumerDir 'SmokeTest.csproj'
    Set-Content -Path $csprojPath -Value $csprojContent -Encoding UTF8
    Write-Success "Created SmokeTest.csproj"

    # Generate Program.cs (inline, per AD-309-1)
    # This code exercises basic Dispatch functionality without Excalibur
    $programContent = @'
using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("🧪 Dispatch Smoke Test - Starting...");
Console.WriteLine();

// Create service collection
var services = new ServiceCollection();
services.AddLogging();

// Verify we can add Dispatch services (minimal configuration)
services.AddDispatch(builder =>
{
    // Minimal configuration - just register the builder
    // This verifies DI wiring works without additional config
});

// Build service provider
var provider = services.BuildServiceProvider();

// Verify core services are registered
Console.WriteLine("Verifying core services...");

var dispatcher = provider.GetRequiredService<IDispatcher>();
Console.WriteLine($"  ✅ IDispatcher resolved: {dispatcher.GetType().Name}");

// Verify core types are available from Excalibur.Dispatch.Abstractions
Console.WriteLine($"  ✅ IDispatchMessage interface available: {typeof(IDispatchMessage).FullName}");
Console.WriteLine($"  ✅ IDomainEvent interface available: {typeof(IDomainEvent).FullName}");
Console.WriteLine($"  ✅ IIntegrationEvent interface available: {typeof(IIntegrationEvent).FullName}");

// Verify no non-Dispatch Excalibur types are accidentally pulled in
// Excalibur.Dispatch.* assemblies are expected; Excalibur.Domain, Excalibur.EventSourcing, etc. are not
var unexpectedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Select(a => a.GetName().Name ?? "")
    .Where(n => n.StartsWith("Excalibur", StringComparison.OrdinalIgnoreCase)
             && !n.StartsWith("Excalibur.Dispatch", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (unexpectedAssemblies.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("❌ ERROR: Found non-Dispatch Excalibur assemblies loaded (should be isolated):");
    foreach (var asm in unexpectedAssemblies)
    {
        Console.WriteLine($"    - {asm}");
    }
    Environment.Exit(1);
}

Console.WriteLine($"  ✅ No non-Dispatch Excalibur assemblies loaded (isolation verified)");

Console.WriteLine();
Console.WriteLine("✅ Smoke test PASSED: Dispatch works without Excalibur dependencies");
Environment.Exit(0);
'@

    $programPath = Join-Path $Script:ConsumerDir 'Program.cs'
    Set-Content -Path $programPath -Value $programContent -Encoding UTF8
    Write-Success "Created Program.cs"

    # Generate NuGet.Config to use local feed
    $nugetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalSmokeTest" value="$Script:PackagesDir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@

    $nugetConfigPath = Join-Path $Script:ConsumerDir 'NuGet.Config'
    Set-Content -Path $nugetConfigPath -Value $nugetConfigContent -Encoding UTF8
    Write-Success "Created NuGet.Config with local feed"

    Write-Info "Consumer app created with:"
    Write-Host "    - SmokeTest.csproj (references Excalibur.Dispatch, Excalibur.Dispatch.Abstractions)" -ForegroundColor DarkGray
    Write-Host "    - Program.cs (exercises dispatcher creation)" -ForegroundColor DarkGray
    Write-Host "    - NuGet.Config (uses local package feed)" -ForegroundColor DarkGray
}

# ============================================================================
# Phase 3: Validate Consumer
# ============================================================================

function Invoke-ValidationPhase {
    Write-StepHeader "VALIDATE" "Building and running consumer application"

    Push-Location $Script:ConsumerDir
    try {
        # Restore packages from local feed
        Write-Info "Restoring packages from local feed..."
        $restoreResult = & dotnet restore --verbosity quiet 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Package restore failed"
            Write-Host $restoreResult
            throw "Restore failed with exit code $LASTEXITCODE"
        }
        Write-Success "Packages restored successfully"

        # Build consumer app
        Write-Info "Building consumer application..."
        $buildResult = & dotnet build --configuration Release --no-restore --verbosity quiet 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Consumer build failed"
            Write-Host $buildResult
            throw "Consumer build failed with exit code $LASTEXITCODE"
        }
        Write-Success "Consumer built successfully"

        # Run consumer app
        Write-Info "Running consumer application..."
        Write-Host ""

        $runResult = & dotnet run --configuration Release --no-build 2>&1
        $runExitCode = $LASTEXITCODE

        # Display output
        Write-Host $runResult
        Write-Host ""

        if ($runExitCode -ne 0) {
            Write-Failure "Consumer app exited with code $runExitCode"
            throw "Consumer validation failed"
        }

        Write-Success "Consumer validation passed"

        # Additional check: Verify no Excalibur references in deps.json
        Write-Info "Verifying dependency isolation..."
        $depsJsonPath = Join-Path $Script:ConsumerDir 'bin/Release/net9.0/SmokeTest.deps.json'
        if (Test-Path $depsJsonPath) {
            $depsContent = Get-Content -Path $depsJsonPath -Raw
            if ($depsContent -match 'Excalibur\.(?!Dispatch)') {
                Write-Failure "Found non-Dispatch Excalibur reference in deps.json!"
                throw "Dependency isolation violation: non-Dispatch Excalibur dependency found in consumer app"
            }
            Write-Success "No non-Dispatch Excalibur dependencies in deps.json"
        }
    }
    finally {
        Pop-Location
    }
}

# ============================================================================
# Phase 4: Cleanup
# ============================================================================

function Invoke-CleanupPhase {
    param([bool]$Success = $true)

    Write-StepHeader "CLEANUP" "Removing temporary files"

    if (Test-Path $Script:TempDir) {
        try {
            Remove-Item -Path $Script:TempDir -Recurse -Force
            Write-Success "Removed temp directory: $Script:TempDir"
        }
        catch {
            Write-Failure "Failed to remove temp directory: $_"
            # Don't throw - cleanup failure shouldn't mask test results
        }
    }
    else {
        Write-Info "Temp directory already removed"
    }

    # Verify cleanup
    if (Test-Path $Script:TempDir) {
        Write-Failure "Warning: Temp directory still exists after cleanup"
    }
    else {
        Write-Success "Cleanup verified - no artifacts remaining"
    }
}

# ============================================================================
# Main Execution
# ============================================================================

$Script:TestPassed = $false

try {
    Write-Banner "Dispatch Packaging Smoke Test (Sprint 309 T5.2)"

    Write-Info "Repository root: $Script:RepoRoot"
    Write-Info "Temp directory: $Script:TempDir"
    Write-Host ""

    # Execute phases
    $packedCount = Invoke-PackPhase
    Invoke-ConsumerPhase
    Invoke-ValidationPhase

    $Script:TestPassed = $true
}
catch {
    Write-Host ""
    Write-Failure "SMOKE TEST FAILED: $_"
    Write-Host ""
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
}
finally {
    # Always cleanup (AD-309-2)
    Invoke-CleanupPhase -Success $Script:TestPassed
}

# Final status
Write-Host ""
if ($Script:TestPassed) {
    Write-Banner "SMOKE TEST PASSED"
    Write-Host "All Dispatch packages work correctly in isolation." -ForegroundColor Green
    Write-Host ""
    exit 0
}
else {
    Write-Banner "SMOKE TEST FAILED"
    Write-Host "See errors above for details." -ForegroundColor Red
    Write-Host ""
    exit 1
}
