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

# Set by the SURFACE phase; read by the coverage ratchet. Initialised here because StrictMode makes
# reading an unassigned variable a terminating error, and the ratchet must still be able to report
# when the surface phase threw before reaching its assignment.
$Script:SurfaceReferenced = @()
$Script:SurfaceTools = @()

# Dispatch packages to test (core packages, not Excalibur)
# Note: Only include packages that are properly configured for packing
# Other packages (Excalibur.Dispatch.Patterns.*, etc.) have packaging issues that
# should be fixed separately - this smoke test validates isolation, not packability
# Order matters for dependency resolution!
$Script:DispatchPackages = @(
    'Excalibur.Dispatch.Abstractions',              # Must be first (dependency of all others)
    'Excalibur.Compliance.Abstractions',            # Dependency of Dispatch (moved to src/Excalibur)
    'Excalibur.Dispatch.Serialization.MemoryPack',  # Dependency of Dispatch
    'Excalibur.Dispatch'                            # Core dispatcher
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
        # Search both src/Dispatch/ and src/Excalibur/ (some packages moved)
        $projectPath = Join-Path $Script:RepoRoot "src/Dispatch/$packageName/$packageName.csproj"
        if (-not (Test-Path $projectPath)) {
            $projectPath = Join-Path $Script:RepoRoot "src/Excalibur/$packageName/$packageName.csproj"
        }

        if (-not (Test-Path $projectPath)) {
            Write-Verbose "Project not found in src/Dispatch/ or src/Excalibur/, skipping: $packageName"
            continue
        }

        # Build the project
        Write-Info "Building $packageName..."
        $buildResult = & dotnet build $projectPath `
            --configuration Release `
            --verbosity quiet `
            "-p:MinVerVersionOverride=$Script:SmokeTestVersion" `
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
            "-p:MinVerVersionOverride=$Script:SmokeTestVersion" `
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

    # ------------------------------------------------------------------------------------------
    # THE WHOLE SHIPPING SURFACE, packed in one command.
    #
    # The loop above packs four named packages, and the comment justifying that said the rest "have
    # packaging issues that should be fixed separately". Measured 2026-08-07: that is stale. The
    # shipping filter packs COMPLETELY -- 195 of 195, zero errors, in 54 seconds, which is faster
    # than the four-package loop it sits beside.
    #
    # The four-package loop is deliberately kept rather than replaced. It feeds the isolation
    # consumer, whose deps.json assertion is that Excalibur.Dispatch does NOT drag in Excalibur.*,
    # and that assertion only means something while the consumer references the Dispatch core ALONE.
    # Widening that consumer to the whole surface would raise the coverage number by destroying the
    # architectural guard underneath it. Two consumers, two questions.
    Write-Info "Packing the whole shipping surface..."
    $shippingFilter = Join-Path $Script:RepoRoot 'eng/ci/shards/ShippingOnly.slnf'
    if (Test-Path $shippingFilter) {
        $surfaceResult = & dotnet pack $shippingFilter `
            --configuration Release `
            --output $Script:PackagesDir `
            --verbosity quiet `
            "-p:MinVerVersionOverride=$Script:SmokeTestVersion" `
            "-p:ContinuousIntegrationBuild=false" `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Failed to pack the shipping surface"
            Write-Host $surfaceResult
            throw "Pack failed for $shippingFilter"
        }
        Write-Success "Packed the shipping surface"
    }
    else {
        throw "Shipping filter not found at $shippingFilter -- the surface set is unknowable, and an unmeasured surface is not a covered one."
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
    <TargetFramework>net10.0</TargetFramework>
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
using Excalibur.Dispatch;
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

// Verify core types are available from Excalibur.Dispatch (namespace, package is still Excalibur.Dispatch.Abstractions)
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

    # A FRESH PACKAGE CACHE, because the smoke version never changes.
    #
    # Every run packs 99.0.0-smoketest, and NuGet caches by id AND version -- so once a copy of that
    # version is in the global cache, every later run resolves the CACHED bytes and never contacts
    # the feed the test just built. The test then validates whatever was packed the first time it
    # ever ran.
    #
    # Not hypothetical. Found 2026-08-07: the global cache held four 99.0.0-smoketest packages, one
    # of them named excalibur.dispatch.compliance.abstractions -- an id from before a rename. Locally
    # that surfaced as CS0246 on types the current assembly plainly has, because the assembly being
    # compiled against was months old. It passed in CI only because a fresh runner has no cache.
    #
    # The failure direction that matters is the other one: a stale copy that still compiles would
    # report a PASS about bytes nobody built. Redirecting the cache makes the run measure what it
    # packed, and the emptiness check below is the positive control that it did.
    $cacheDir = Join-Path $Script:TempDir 'consumer-cache'
    if (Test-Path $cacheDir) { Remove-Item -Path $cacheDir -Recurse -Force }
    $previousCache = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = $cacheDir

    Push-Location $Script:ConsumerDir
    try {
        # Restore packages from local feed
        Write-Info "Restoring packages from local feed (fresh cache)..."
        $restoreResult = & dotnet restore --verbosity quiet 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Package restore failed"
            Write-Host $restoreResult
            throw "Restore failed with exit code $LASTEXITCODE"
        }
        $restoredCount = @(Get-ChildItem -Path $cacheDir -Directory -ErrorAction SilentlyContinue).Count
        if ($restoredCount -eq 0) {
            throw "Restore reported success but the fresh cache is EMPTY, so nothing was resolved from the feed. A restore that left no trace did not happen."
        }
        Write-Success "Packages restored successfully ($restoredCount into a fresh cache)"

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
        $depsJsonPath = Join-Path $Script:ConsumerDir 'bin/Release/net10.0/SmokeTest.deps.json'
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
        $env:NUGET_PACKAGES = $previousCache
    }
}

# ============================================================================
# Phase 3b: The whole shipping surface, consumed from the feed
# ============================================================================

<#
.SYNOPSIS
    Proves every shipping package can actually be consumed, not merely produced.

.DESCRIPTION
    The isolation consumer answers "does Dispatch stay free of Excalibur". This answers a different
    question the suite never asked: can a consumer REFERENCE what we ship and compile against it.

    Producing a .nupkg proves almost nothing about that. A package can pack cleanly and still be
    unusable -- a dependency group naming a version that does not exist, a target framework nothing
    can consume, a missing lib/ folder. None of that is visible at pack time and all of it is fatal
    on the consumer's first restore, which is the worst place to discover it.

    dotnet tool packages are excluded, by package type rather than by name. A DotnetTool is installed
    with `dotnet tool install`, and NuGet refuses it as a PackageReference (NU1212) -- correctly. It
    is excluded because it is a different KIND of artifact, not because it is inconvenient.

    THE EMPTY CACHE IS THE POSITIVE CONTROL. With a warm cache a restore can succeed having never
    contacted the feed, so it would pass over a feed serving nothing. NUGET_PACKAGES is redirected to
    a fresh directory and the run fails if that directory is still empty afterwards: resolution that
    left no trace did not happen.

    Source mapping pins Excalibur.* to the local feed and everything else to nuget.org. Without it a
    published package of the same name could satisfy the restore and the test would prove nothing
    about the bytes just built. Third-party dependencies come from nuget.org because that is where a
    real consumer gets them.
#>
function Invoke-SurfacePhase {
    Write-StepHeader "SURFACE" "Consuming every shipping package from the local feed"

    $surfaceDir = Join-Path $Script:TempDir 'surface'
    $cacheDir = Join-Path $Script:TempDir 'surface-cache'
    New-Item -ItemType Directory -Path $surfaceDir -Force | Out-Null

    # Package id + version off the filename, with tool packages filtered out by nuspec package type.
    $referenced = @()
    $tools = @()
    foreach ($pkg in Get-ChildItem -Path $Script:PackagesDir -Filter '*.nupkg') {
        if ($pkg.Name -notmatch '^(.*?)\.(\d+\.\d+\.\d+.*)\.nupkg$') { continue }
        $id = $Matches[1]
        $version = $Matches[2]

        $isTool = $false
        try {
            $zip = [System.IO.Compression.ZipFile]::OpenRead($pkg.FullName)
            try {
                $nuspec = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
                if ($nuspec) {
                    $reader = New-Object System.IO.StreamReader($nuspec.Open())
                    try { $isTool = $reader.ReadToEnd() -match 'packageType\s+name="DotnetTool"' }
                    finally { $reader.Dispose() }
                }
            }
            finally { $zip.Dispose() }
        }
        catch {
            throw "Could not read $($pkg.Name) to determine its package type. REFUSING rather than guessing: a package silently treated as unreferenceable is a coverage hole that reports as coverage."
        }

        if ($isTool) { $tools += $id } else { $referenced += [pscustomobject]@{ Id = $id; Version = $version } }
    }

    if ($referenced.Count -eq 0) {
        throw "No referenceable packages found in the local feed -- nothing would be measured, and a run that measures nothing must not report a pass."
    }

    $refLines = ($referenced | Sort-Object Id | ForEach-Object {
        "    <PackageReference Include=""$($_.Id)"" Version=""$($_.Version)"" />"
    }) -join "`n"

    # A consumer inherits nothing from this repository. Validating against our own build props would
    # test the wrong thing: a consumer does not get our build.
    Set-Content -Path (Join-Path $surfaceDir 'Directory.Build.props') -Value '<Project />' -Encoding UTF8
    Set-Content -Path (Join-Path $surfaceDir 'Directory.Build.targets') -Value '<Project />' -Encoding UTF8
    Set-Content -Path (Join-Path $surfaceDir 'Directory.Packages.props') `
        -Value '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>' -Encoding UTF8

    Set-Content -Path (Join-Path $surfaceDir 'Surface.csproj') -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <NoWarn>`$(NoWarn);NU1701;NU1702;NU5104;CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
$refLines
  </ItemGroup>
</Project>
"@

    Set-Content -Path (Join-Path $surfaceDir 'nuget.config') -Encoding UTF8 -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$Script:PackagesDir" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Excalibur.*" /></packageSource>
    <packageSource key="nuget"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@

    Write-Info "Referencing $($referenced.Count) shipping package(s); $($tools.Count) tool package(s) excluded by package type."

    if (Test-Path $cacheDir) { Remove-Item -Path $cacheDir -Recurse -Force }
    $previousCache = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = $cacheDir
    Push-Location $surfaceDir
    try {
        $buildResult = & dotnet build --configuration Release --verbosity quiet --nologo 2>&1
        $buildExit = $LASTEXITCODE

        if ($buildExit -ne 0) {
            Write-Failure "The shipping surface does not restore and compile from the feed"
            Write-Host $buildResult
            throw "Surface consumption failed with exit code $buildExit"
        }

        $restored = @(Get-ChildItem -Path $cacheDir -Directory -ErrorAction SilentlyContinue).Count
        if ($restored -eq 0) {
            throw "The build succeeded but the package cache is EMPTY, so nothing was resolved from the feed. A restore that left no trace did not happen, and this pass would be about nothing."
        }

        Write-Success "$($referenced.Count) package(s) restored from the feed and compiled together ($restored cache entries)"
        # Published for the coverage ratchet below: what was REFERENCED AND COMPILED, so the number
        # it reports is derived from the run rather than restated beside it.
        $Script:SurfaceReferenced = @($referenced | ForEach-Object { $_.Id })
        $Script:SurfaceTools = @($tools)
    }
    finally {
        Pop-Location
        $env:NUGET_PACKAGES = $previousCache
    }

    return $referenced.Count
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
    $Script:SurfaceCovered = Invoke-SurfacePhase

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

# ============================================================================
# Coverage, stated out loud.
#
# This test consumes PACKED PACKAGES rather than project references, which is the property that
# makes it worth having -- and it does so for a hand-maintained list. A green result therefore says
# "the packages in $DispatchPackages work in isolation", not "the shipping surface works", and
# nothing in the output used to distinguish those. A pass reported wider than what was measured is
# how a 2%-coverage gate reads as release confidence.
#
# So the number is printed every run, and the uncovered count is RATCHETED against a committed
# baseline: it may shrink freely, and growing it fails. That makes adding a shipping package
# without smoke-testing it a deliberate act with a visible cost, instead of a silent one.
# ============================================================================
$shippingFilter = Join-Path $PSScriptRoot 'ci/shards/ShippingOnly.slnf'
$baselineFile   = Join-Path $PSScriptRoot 'ci/smoke-test-coverage-baseline.txt'
if (Test-Path $shippingFilter) {
    # Split on BOTH separators explicitly. The .slnf stores Windows paths, and
    # [System.IO.Path]::GetFileNameWithoutExtension does not treat a backslash as a separator on
    # Linux -- there the whole 'src\Dispatch\...\X.csproj' survives as one "filename", every name
    # fails to match the covered list, and the ratchet reports the full shipping count as uncovered.
    # It passed locally on Windows and failed on the runner, which is the only place it runs.
    $shipping = ([regex]::Matches((Get-Content $shippingFilter -Raw), '"([^"]*\.csproj)"') |
        ForEach-Object { ($_.Groups[1].Value -split '[\\/]')[-1] -replace '\.csproj$', '' } |
        Sort-Object -Unique)
    # COVERED is now what the SURFACE phase actually referenced and compiled, not a hand-maintained
    # list. That is the whole point of the change: the previous list was four names and a comment
    # asserting the rest could not be packed, which measurement disproved -- the shipping filter
    # packs completely.
    #
    # Derived from the feed rather than restated, so the number cannot drift from the thing it
    # describes. If the surface phase did not run (an early throw), this falls back to the isolation
    # list, which reports LESS coverage than was achieved -- the safe direction for a ratchet.
    $covered = if ($Script:SurfaceReferenced.Count -gt 0) { @($Script:SurfaceReferenced) } else { @($Script:DispatchPackages) }
    $uncovered = @($shipping | Where-Object { $covered -notcontains $_ })

    # CONTROL on the ratchet's own inputs. The count above is only meaningful if the two lists are
    # comparable at all; if the covered names match nothing in the shipping set, every package reads
    # as uncovered and the ratchet fails with a number that describes a parsing fault rather than
    # coverage. That is not hypothetical -- it is exactly what happened when the shipping names were
    # extracted with a path API that ignores backslashes on Linux: 197 uncovered instead of 193, a
    # confident number about nothing. A miscompare must say so instead of masquerading as a
    # regression.
    $unmatched = @($covered | Where-Object { $shipping -notcontains $_ })
    if ($unmatched.Count -gt 0) {
        Write-Host ""
        Write-Host "COVERAGE CONTROL FAILED: $($unmatched.Count) of $($covered.Count) smoke-tested name(s) do not appear in the shipping set:" -ForegroundColor Red
        $unmatched | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        Write-Host "The uncovered count below would be measuring a name-matching fault, not coverage." -ForegroundColor Red
        Write-Host "Check how shipping names are parsed from the solution filter before trusting any number here." -ForegroundColor Red
        $Script:TestPassed = $false
    }

    Write-Host ""
    Write-Host "Smoke-test coverage: $($covered.Count) of $($shipping.Count) shipping package(s) consumed as packages; $($uncovered.Count) uncovered." -ForegroundColor Cyan

    if (Test-Path $baselineFile) {
        $baseline = [int]((Get-Content $baselineFile -Raw) -replace '\D', '')
        if ($uncovered.Count -gt $baseline) {
            Write-Host ""
            Write-Host "SMOKE COVERAGE REGRESSED: $($uncovered.Count) uncovered, baseline is $baseline." -ForegroundColor Red
            Write-Host "A shipping package was added without smoke-testing it. Add it to `$Script:DispatchPackages," -ForegroundColor Red
            Write-Host "or raise the baseline deliberately and say why in the commit message." -ForegroundColor Red
            $Script:TestPassed = $false
        }
        elseif ($uncovered.Count -lt $baseline) {
            Write-Host "Coverage improved ($($uncovered.Count) < $baseline). Lower the baseline in $baselineFile to lock it in." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "No coverage baseline at $baselineFile; not ratcheting." -ForegroundColor Yellow
    }
}

# Final status
Write-Host ""
if ($Script:TestPassed) {
    Write-Banner "SMOKE TEST PASSED"
    Write-Host "The smoke-tested packages work correctly in isolation." -ForegroundColor Green
    Write-Host "This is NOT a statement about the packages it does not cover -- see the coverage line above." -ForegroundColor Yellow
    Write-Host ""
    exit 0
}
else {
    Write-Banner "SMOKE TEST FAILED"
    Write-Host "See errors above for details." -ForegroundColor Red
    Write-Host ""
    exit 1
}
