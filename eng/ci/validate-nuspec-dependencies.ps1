#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates internal package dependencies from generated nuspec metadata.
.DESCRIPTION
    Packs eng/ci/shards/ShippingOnly.slnf, inspects generated .nupkg nuspec files, and enforces:
      - Dispatch packages may only depend on Dispatch internal packages
      - Excalibur packages may depend on Excalibur/Dispatch internal packages
      - Internal dependency versions must be explicit and non-floating
      - Expected packable projects produce packages
      - No shipped package declares a development-only dependency (see the block below)
.PARAMETER SelfTest
    Runs the development-only classifier against planted defects and controls, without packing,
    and exits non-zero if it fails to name a planted defect or flags a legitimate dependency.
#>
param(
    [string]$SolutionFilter = "eng/ci/shards/ShippingOnly.slnf",
    [string]$OutDir = "management/reports/PackageDependencyReport",
    [string]$Version = "0.0.0-ci-validation",
    [switch]$Enforce = $true,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------------------
# Development-only dependency classes.
#
# A packed nuspec is the list a consumer's restore acts on: every id named there is downloaded
# into their application. These classes are never correct on that list. A benchmarking harness, a
# test framework, an analyzer, the compiler platform and our own build tooling belong to OUR
# build, not to a consumer's runtime.
#
# Nobody has to write the dependency down for it to appear. With central transitive pinning on, a
# centrally pinned package that reaches ANY point of a project's transitive graph is promoted onto
# that project's nuspec as a DIRECT dependency -- so a single reference missing PrivateAssets, in
# one project, is declared by every package downstream of it. Two published releases carried
# exactly that shape: an object-relational mapper behind a health-checks package, and a
# benchmarking harness declared by more than half the published set. Neither was visible in a lock
# file, because a lock file records the restore graph rather than what the package declares.
#
# This is deliberately a denylist of categories that can never be right, not an allowlist of every
# expected dependency. An expected-set baseline over ~200 packages would be edited by whoever broke
# it, which is how a ratchet loosens. A category that is never correct has a membership rule any
# reader can check, and no reason to be relaxed.
#
# The remedy for a hit is PrivateAssets="all" on the reference. That keeps the version floor for
# our own restore -- which is why transitive pinning is on in the first place, and how the
# metapackage directory keeps its security floors while pinning is off there -- without emitting
# the package onto a consumer's list. An exemption below is the last resort, not the first.
# ---------------------------------------------------------------------------------------------
$developmentOnlyDependencyPrefixes = [ordered]@{
    'BenchmarkDotNet'               = 'benchmarking harness'
    'Microsoft.CodeAnalysis.'       = 'compiler platform'
    'StyleCop.Analyzers'            = 'analyzer'
    'Roslynator.'                   = 'analyzer'
    'Meziantou.Analyzer'            = 'analyzer'
    'SonarAnalyzer.'                = 'analyzer'
    'Microsoft.SourceLink.'         = 'build tooling'
    'MinVer'                        = 'build tooling'
    'Nerdbank.GitVersioning'        = 'build tooling'
    'coverlet.'                     = 'coverage tooling'
    'Microsoft.NET.Test.Sdk'        = 'test framework'
    'xunit'                         = 'test framework'
    'NUnit'                         = 'test framework'
    'MSTest'                        = 'test framework'
    'Moq'                           = 'mocking library'
    'FakeItEasy'                    = 'mocking library'
    'NSubstitute'                   = 'mocking library'
    'Shouldly'                      = 'assertion library'
    'AutoFixture'                   = 'test-data library'
    'Testcontainers'                = 'test infrastructure'
    'Microsoft.EntityFrameworkCore' = 'object-relational mapper (this framework accesses data through Dapper and raw ADO.NET, so shipping an ORM to a consumer contradicts its stated data-access constraint)'
}

# Test-support PRODUCTS, where the dependency IS what the consumer buys. Excalibur.Testing.Containers
# hands a consumer container-backed fixtures to write xUnit tests against; Excalibur.Dispatch.Testing.Shouldly
# ships assertion extensions over Shouldly's own types. Making these private would publish a package whose
# public surface a consumer cannot compile against.
#
# Keyed by package id on purpose: an exemption is never global. The same dependency appearing on any
# other package is still a defect, which is the case that would otherwise slip through.
$developmentOnlyDependencyExemptions = @{
    'Excalibur.Testing.Containers'        = @('xunit', 'Testcontainers')
    'Excalibur.Dispatch.Testing.Shouldly' = @('Shouldly')
}

# Returns a human-readable issue string when the dependency is development-only and unexempted for
# this package; returns $null otherwise. Pure -- no I/O -- so the self-test can drive it directly.
function Get-DevelopmentOnlyDependencyIssue {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$DependencyId
    )

    $exempt = @()
    if ($developmentOnlyDependencyExemptions.ContainsKey($PackageId)) {
        $exempt = $developmentOnlyDependencyExemptions[$PackageId]
    }

    foreach ($prefix in $developmentOnlyDependencyPrefixes.Keys) {
        if (-not $DependencyId.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if ($exempt -contains $prefix) {
            return $null
        }

        $category = $developmentOnlyDependencyPrefixes[$prefix]
        return "Development-only dependency '$DependencyId' ($category) is declared on the packed manifest, so a consumer restoring this package downloads it. Add PrivateAssets=all to the reference that introduces it."
    }

    return $null
}

if ($SelfTest) {
    # Safety arm: each planted shape must be named. The first two are the shapes that actually shipped;
    # the last two prove an exemption is scoped to its own package rather than to the dependency id.
    $planted = @(
        @{ Pkg = 'Excalibur.Dispatch.Patterns';    Dep = 'BenchmarkDotNet' }
        @{ Pkg = 'Excalibur.Hosting.HealthChecks'; Dep = 'Microsoft.EntityFrameworkCore.Relational' }
        @{ Pkg = 'Excalibur.Dispatch';             Dep = 'Microsoft.CodeAnalysis.CSharp' }
        @{ Pkg = 'Excalibur.Dispatch';             Dep = 'Testcontainers.MsSql' }
        @{ Pkg = 'Excalibur.Dispatch';             Dep = 'Shouldly' }
    )

    # Liveness arm: a gate that flags everything is as useless as one that flags nothing. Ordinary
    # dependencies must pass, and so must the two exemptions on the packages that own them.
    $controls = @(
        @{ Pkg = 'Excalibur.Dispatch';                  Dep = 'Microsoft.Extensions.DependencyInjection.Abstractions' }
        @{ Pkg = 'Excalibur.Dispatch';                  Dep = 'System.Text.Json' }
        @{ Pkg = 'Excalibur.Dispatch';                  Dep = 'Excalibur.Dispatch.Abstractions' }
        @{ Pkg = 'Excalibur.Testing.Containers';        Dep = 'Testcontainers.MsSql' }
        @{ Pkg = 'Excalibur.Testing.Containers';        Dep = 'xunit.v3.extensibility.core' }
        @{ Pkg = 'Excalibur.Dispatch.Testing.Shouldly'; Dep = 'Shouldly' }
    )

    $selfTestFailures = @()
    foreach ($case in $planted) {
        if (-not (Get-DevelopmentOnlyDependencyIssue -PackageId $case.Pkg -DependencyId $case.Dep)) {
            $selfTestFailures += "MISSED planted defect: $($case.Pkg) -> $($case.Dep)"
        }
    }
    foreach ($case in $controls) {
        if (Get-DevelopmentOnlyDependencyIssue -PackageId $case.Pkg -DependencyId $case.Dep) {
            $selfTestFailures += "FALSE POSITIVE on a legitimate dependency: $($case.Pkg) -> $($case.Dep)"
        }
    }

    if ($selfTestFailures.Count -gt 0) {
        Write-Host "Self-test FAILED:" -ForegroundColor Red
        foreach ($f in $selfTestFailures) { Write-Host " - $f" }
        exit 3
    }

    Write-Host "Self-test passed: $($planted.Count) planted defects named, $($controls.Count) legitimate dependencies not flagged."
    exit 0
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path $SolutionFilter)) {
    throw "Solution filter not found: $SolutionFilter"
}

function Convert-ToPlatformPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $PathValue
    }

    $separator = [System.IO.Path]::DirectorySeparatorChar
    return $PathValue.Replace('\', $separator).Replace('/', $separator)
}

function Convert-ToRepoPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $PathValue
    }

    return $PathValue.Replace('\', '/')
}

$packagesDir = Join-Path $OutDir "packages"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $packagesDir | Out-Null
Get-ChildItem -Path $packagesDir -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Packing shipping projects from $SolutionFilter ..."
$packAttempts = 0
$maxPackAttempts = 3
$packSucceeded = $false
while ($packAttempts -lt $maxPackAttempts -and -not $packSucceeded) {
    $packAttempts++
    if ($packAttempts -gt 1) {
        Write-Warning "dotnet pack attempt $packAttempts/$maxPackAttempts ..."
    }

    dotnet pack $SolutionFilter `
        --configuration Release `
        --verbosity minimal `
        --output $packagesDir `
        -p:MinVerVersionOverride=$Version `
        -p:PackageVersion=$Version `
        -p:DispatchPackageVersion=$Version `
        -p:ExcaliburPackageVersion=$Version `
        -p:RestoreDisableParallel=true

    if ($LASTEXITCODE -eq 0) {
        $packSucceeded = $true
    }
    elseif ($packAttempts -lt $maxPackAttempts) {
        Start-Sleep -Seconds 2
    }
}

if (-not $packSucceeded) {
    throw "dotnet pack failed after $maxPackAttempts attempt(s)."
}

# Parse expected project/package identities from slnf
$slnf = Get-Content -Raw $SolutionFilter | ConvertFrom-Json
$projectPaths = @($slnf.solution.projects)
$expectedPackageIds = @{}
$packableProjectPaths = @()

foreach ($rawPath in $projectPaths) {
    $platformPath = Convert-ToPlatformPath -PathValue $rawPath
    if (-not (Test-Path $platformPath)) {
        continue
    }

    [xml]$csproj = Get-Content -Raw $platformPath
    $isPackableNode = $csproj.SelectSingleNode("//Project/PropertyGroup/IsPackable")
    $isPackable = $true
    if ($isPackableNode -and $isPackableNode.InnerText.Trim().ToLowerInvariant() -eq "false") {
        $isPackable = $false
    }
    if (-not $isPackable) {
        continue
    }

    $repoPath = Convert-ToRepoPath -PathValue $rawPath
    $packableProjectPaths += $repoPath

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($platformPath)
    $packageIdNode = $csproj.SelectSingleNode("//Project/PropertyGroup/PackageId")
    $assemblyNameNode = $csproj.SelectSingleNode("//Project/PropertyGroup/AssemblyName")
    $candidateId = if ($packageIdNode -and -not [string]::IsNullOrWhiteSpace($packageIdNode.InnerText)) {
        $packageIdNode.InnerText.Trim()
    }
    elseif ($assemblyNameNode -and -not [string]::IsNullOrWhiteSpace($assemblyNameNode.InnerText)) {
        $assemblyNameNode.InnerText.Trim()
    }
    else {
        $projectName
    }

    # If PackageId/AssemblyName still contains unresolved MSBuild properties, fall back to project name.
    $packageId = if ($candidateId -match '\$\(.+\)') { $projectName } else { $candidateId }

    $expectedPackageIds[$packageId] = $repoPath
}

$nupkgs = @(Get-ChildItem -Path $packagesDir -Filter "*.nupkg" -File | Where-Object { $_.Name -notlike "*.symbols.nupkg" })

$issues = @()
$reports = @()
$actualPackageIds = @{}

function Test-IsDispatchFamily {
    param([string]$Id)
    return $Id -eq "Excalibur.Dispatch" -or $Id.StartsWith("Excalibur.Dispatch.", [System.StringComparison]::Ordinal)
}

# Bridge metapackages live in src/Excalibur/ but are named Excalibur.Dispatch.*.
# They intentionally depend on both Dispatch and Excalibur packages.
# All metapackages in src/metapackages/ intentionally depend on both Dispatch and Excalibur packages.
$bridgeMetapackages = @(
    "Excalibur.Dispatch.SqlServer",
    "Excalibur.Dispatch.Postgres",
    "Excalibur.Dispatch.RabbitMQ",
    "Excalibur.Dispatch.Kafka",
    "Excalibur.Dispatch.Azure",
    "Excalibur.Dispatch.Aws"
)

foreach ($pkg in $nupkgs) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($pkg.FullName)
    try {
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        if (-not $nuspecEntry) {
            $issues += "Package '$($pkg.Name)' does not contain a nuspec file"
            continue
        }

        $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
        try {
            $nuspecContent = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        [xml]$nuspec = $nuspecContent
        $namespaceUri = $nuspec.DocumentElement.NamespaceURI
        $packageId = $null
        $dependencyNodes = @()
        if ([string]::IsNullOrWhiteSpace($namespaceUri)) {
            $packageId = $nuspec.SelectSingleNode("//package/metadata/id")?.InnerText
            $dependencyNodes = @($nuspec.SelectNodes("//package/metadata/dependencies/group/dependency"))
            $dependencyNodes += @($nuspec.SelectNodes("//package/metadata/dependencies/dependency"))
        }
        else {
            $ns = New-Object System.Xml.XmlNamespaceManager($nuspec.NameTable)
            $ns.AddNamespace("n", $namespaceUri)
            $packageId = $nuspec.SelectSingleNode("//n:package/n:metadata/n:id", $ns)?.InnerText
            $dependencyNodes = @($nuspec.SelectNodes("//n:package/n:metadata/n:dependencies/n:group/n:dependency", $ns))
            $dependencyNodes += @($nuspec.SelectNodes("//n:package/n:metadata/n:dependencies/n:dependency", $ns))
        }

        if ([string]::IsNullOrWhiteSpace($packageId)) {
            $issues += "Package '$($pkg.Name)' nuspec is missing metadata/id"
            continue
        }

        $packageId = $packageId.Trim()
        $actualPackageIds[$packageId] = $true

        $internalDeps = @()
        $developmentOnlyIssues = @()
        foreach ($dep in $dependencyNodes) {
            $depId = $dep.id
            $depVersion = $dep.version
            if ([string]::IsNullOrWhiteSpace($depId)) {
                continue
            }

            if ($depId.StartsWith("Excalibur.", [System.StringComparison]::Ordinal)) {
                $internalDeps += [PSCustomObject]@{
                    Id = $depId
                    Version = $depVersion
                }
                continue
            }

            # Third-party ids used to be discarded at this point, which left the whole
            # development-only class invisible to a gate that was already reading the one artifact
            # that shows it.
            $devIssue = Get-DevelopmentOnlyDependencyIssue -PackageId $packageId -DependencyId $depId
            if ($devIssue) {
                $developmentOnlyIssues += $devIssue
            }
        }

        $packageIssues = @()
        $packageIssues += $developmentOnlyIssues
        foreach ($dep in $internalDeps) {
            if ($dep.Id -eq $packageId) {
                $packageIssues += "Self dependency: $($dep.Id)"
            }

            if ([string]::IsNullOrWhiteSpace($dep.Version)) {
                $packageIssues += "Internal dependency '$($dep.Id)' has empty version"
            }
            elseif ($dep.Version.Contains("*")) {
                $packageIssues += "Internal dependency '$($dep.Id)' has floating version '$($dep.Version)'"
            }

            if ((Test-IsDispatchFamily -Id $packageId) -and ($packageId -notin $bridgeMetapackages)) {
                if (-not (Test-IsDispatchFamily -Id $dep.Id)) {
                    $packageIssues += "Dispatch package '$packageId' depends on non-Dispatch internal package '$($dep.Id)'"
                }
            }

            if ($packageId.StartsWith("Excalibur.", [System.StringComparison]::Ordinal) -and
                -not (Test-IsDispatchFamily -Id $packageId)) {
                if (-not ($dep.Id.StartsWith("Excalibur.", [System.StringComparison]::Ordinal) -or
                          (Test-IsDispatchFamily -Id $dep.Id))) {
                    $packageIssues += "Excalibur package '$packageId' has unexpected internal dependency '$($dep.Id)'"
                }
            }
        }

        $packageIssues = @($packageIssues | Sort-Object -Unique)

        if ($packageIssues.Count -gt 0) {
            foreach ($pi in $packageIssues) {
                $issues += "${packageId}: $pi"
            }
        }

        $reports += [PSCustomObject]@{
            PackageId = $packageId
            PackageFile = $pkg.Name
            InternalDependencyCount = $internalDeps.Count
            InternalDependencies = @($internalDeps | ForEach-Object { "$($_.Id) @ $($_.Version)" })
            DevelopmentOnlyDependencyCount = $developmentOnlyIssues.Count
            Issues = @($packageIssues)
        }
    }
    finally {
        $zip.Dispose()
    }
}

$missingExpected = @()
foreach ($expectedId in $expectedPackageIds.Keys) {
    if (-not $actualPackageIds.ContainsKey($expectedId)) {
        $missingExpected += "$expectedId (from $($expectedPackageIds[$expectedId]))"
    }
}

if ($missingExpected.Count -gt 0) {
    foreach ($m in $missingExpected) {
        $issues += "Missing package output: $m"
    }
}

$reportJsonPath = Join-Path $OutDir "report.json"
$summaryPath = Join-Path $OutDir "summary.md"

$reportObject = [PSCustomObject]@{
    solutionFilter = $SolutionFilter
    packableProjectCount = $packableProjectPaths.Count
    generatedPackageCount = $nupkgs.Count
    expectedPackageIds = @($expectedPackageIds.Keys | Sort-Object)
    missingExpectedPackages = @($missingExpected | Sort-Object)
    issues = @($issues | Sort-Object)
    packages = $reports | Sort-Object PackageId
}

$reportObject | ConvertTo-Json -Depth 6 | Out-File -FilePath $reportJsonPath -Encoding UTF8

$lines = @()
$lines += "# Package Dependency Validation"
$lines += ""
$lines += "- Solution filter: $SolutionFilter"
$lines += "- Packable projects expected: $($packableProjectPaths.Count)"
$lines += "- Generated packages: $($nupkgs.Count)"
$lines += "- Issues: $($issues.Count)"
$lines += ""

if ($missingExpected.Count -gt 0) {
    $lines += "## Missing Expected Packages"
    foreach ($item in ($missingExpected | Sort-Object)) {
        $lines += "- $item"
    }
    $lines += ""
}

if ($issues.Count -gt 0) {
    $lines += "## Issues"
    foreach ($item in ($issues | Sort-Object)) {
        $lines += "- $item"
    }
    $lines += ""
}
else {
    $lines += "## Result"
    $lines += "No dependency-graph issues detected."
    $lines += ""
}

$lines += "## Package Internal Dependencies"
foreach ($pkgReport in ($reports | Sort-Object PackageId)) {
    $lines += "- $($pkgReport.PackageId): $($pkgReport.InternalDependencyCount) internal deps"
}

$lines | Out-File -FilePath $summaryPath -Encoding UTF8

Write-Host "Wrote report: $reportJsonPath"
Write-Host "Wrote summary: $summaryPath"

if ($Enforce -and $issues.Count -gt 0) {
    Write-Host "Dependency graph issues detected:" -ForegroundColor Red
    foreach ($issue in ($issues | Sort-Object -Unique)) {
        Write-Host " - $issue"
    }
    Write-Error "Dependency graph validation failed with $($issues.Count) issue(s)."
    exit 1
}

Write-Host "Dependency graph validation passed."
exit 0
