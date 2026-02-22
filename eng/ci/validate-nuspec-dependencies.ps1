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
#>
param(
    [string]$SolutionFilter = "eng/ci/shards/ShippingOnly.slnf",
    [string]$OutDir = "management/reports/PackageDependencyReport",
    [string]$Version = "0.1.0-ci-validation",
    [switch]$Enforce = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path $SolutionFilter)) {
    throw "Solution filter not found: $SolutionFilter"
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
        -p:PackageVersion=$Version `
        -p:Version=$Version `
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

foreach ($path in $projectPaths) {
    if (-not (Test-Path $path)) {
        continue
    }

    [xml]$csproj = Get-Content -Raw $path
    $isPackableNode = $csproj.SelectSingleNode("//Project/PropertyGroup/IsPackable")
    $isPackable = $true
    if ($isPackableNode -and $isPackableNode.InnerText.Trim().ToLowerInvariant() -eq "false") {
        $isPackable = $false
    }
    if (-not $isPackable) {
        continue
    }

    $packableProjectPaths += $path.Replace('\', '/')

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($path)
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

    $packageId = if ($candidateId -match '^\$\(.+\)$') { $projectName } else { $candidateId }

    $expectedPackageIds[$packageId] = $path.Replace('\', '/')
}

$nupkgs = @(Get-ChildItem -Path $packagesDir -Filter "*.nupkg" -File | Where-Object { $_.Name -notlike "*.symbols.nupkg" })

$issues = @()
$reports = @()
$actualPackageIds = @{}

function Test-IsDispatchFamily {
    param([string]$Id)
    return $Id -eq "Excalibur.Dispatch" -or $Id.StartsWith("Excalibur.Dispatch.", [System.StringComparison]::Ordinal)
}

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
            }
        }

        $packageIssues = @()
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

            if (Test-IsDispatchFamily -Id $packageId) {
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
