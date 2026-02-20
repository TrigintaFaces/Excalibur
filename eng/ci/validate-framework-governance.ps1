#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates consolidated framework governance contracts.
.DESCRIPTION
    Enforces governance rules from management/governance/framework-governance.json:
      - Capability ownership matrix integrity
      - Critical package test matrix completeness
      - Full shipping package -> test mapping rule coverage
      - Sample fitness classification + smoke profile completeness
      - Docs parity (generated capability matrix docs)
      - Postgres/Postgres naming policy in consumer docs
      - Transport parity conformance class coverage
#>
param(
    [ValidateSet('Governance','TransportParity')]
    [string]$Mode = 'Governance',
    [string]$MatrixPath = 'management/governance/framework-governance.json',
    [string]$OutDir = 'management/reports/FrameworkGovernanceReport',
    [switch]$Enforce = $true,
    [switch]$FixGeneratedDocs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertFrom-JsonCompat {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [int]$Depth = 50
    )

    $jsonText = if ($Json -is [string]) { $Json } else { ($Json -join [Environment]::NewLine) }

    $convertFromJsonCommand = Get-Command ConvertFrom-Json -ErrorAction Stop
    if ($convertFromJsonCommand.Parameters.ContainsKey('Depth')) {
        return ($jsonText | ConvertFrom-Json -Depth $Depth)
    }

    return ($jsonText | ConvertFrom-Json)
}

if (-not (Test-Path $MatrixPath)) {
    throw "Governance matrix not found: $MatrixPath"
}

$repoRoot = (Get-Location).Path
$matrix = ConvertFrom-JsonCompat -Json (Get-Content -Raw $MatrixPath) -Depth 50
$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Normalize-RepoPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $PathValue
    }

    return $PathValue.Replace('\', '/').Trim()
}

function Convert-ToRepoPath {
    param([string]$FullPath)

    $normalizedFull = [System.IO.Path]::GetFullPath($FullPath)
    $normalizedRoot = [System.IO.Path]::GetFullPath($repoRoot)
    $relative = $normalizedFull.Substring($normalizedRoot.Length).TrimStart('\', '/')
    return Normalize-RepoPath $relative
}

function Get-ContentNormalized {
    param([string]$PathToRead)

    $raw = Get-Content -Raw $PathToRead
    return $raw.Replace("`r`n", "`n").Trim()
}

function Test-PathExists {
    param([string]$PathToCheck)

    $normalized = Normalize-RepoPath $PathToCheck
    if (-not (Test-Path $normalized)) {
        $issues.Add("Missing path: $normalized")
        return $false
    }

    return $true
}

function New-Report {
    param(
        [string]$SummaryPath,
        [string[]]$Lines,
        [string]$JsonPath,
        [object]$Object
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $SummaryPath) | Out-Null
    $Lines | Out-File -FilePath $SummaryPath -Encoding UTF8
    $Object | ConvertTo-Json -Depth 50 | Out-File -FilePath $JsonPath -Encoding UTF8
    Write-Host "Wrote summary: $SummaryPath"
    Write-Host "Wrote report: $JsonPath"
}

function Get-IsPackable {
    param([string]$ProjectPath)

    [xml]$csproj = Get-Content -Raw $ProjectPath
    $nodes = @($csproj.SelectNodes('//Project/PropertyGroup/IsPackable'))
    foreach ($node in $nodes) {
        if ($null -eq $node) {
            continue
        }

        $value = $node.InnerText.Trim().ToLowerInvariant()
        if ($value -eq 'false') {
            return $false
        }
        if ($value -eq 'true') {
            return $true
        }
    }

    return $true
}

function Get-PackableProjects {
    param([string]$SourceRoot = 'src')

    $projects = @(Get-ChildItem -Path $SourceRoot -Recurse -Filter '*.csproj' -File | ForEach-Object {
        Convert-ToRepoPath -FullPath $_.FullName
    })

    $packable = @()
    foreach ($project in $projects) {
        if (Get-IsPackable -ProjectPath $project) {
            $packable += $project
        }
    }

    return @($packable | Sort-Object -Unique)
}

function Get-PackageId {
    param([string]$ProjectPath)

    [xml]$csproj = Get-Content -Raw $ProjectPath
    $packageIdNode = $csproj.SelectSingleNode('//Project/PropertyGroup/PackageId')
    if ($null -ne $packageIdNode -and -not [string]::IsNullOrWhiteSpace($packageIdNode.InnerText)) {
        $rawPackageId = $packageIdNode.InnerText.Trim()
        if ($rawPackageId -notmatch '\$\(') {
            return $rawPackageId
        }
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
}

function Get-CapabilityMatrixLines {
    param(
        [object]$Matrix,
        [switch]$ForDocsSite
    )

    $lines = @()

    if ($ForDocsSite) {
        $lines += '---'
        $lines += 'title: Capability Ownership Matrix'
        $lines += 'description: Canonical Dispatch vs Excalibur ownership matrix generated from governance source.'
        $lines += '---'
        $lines += ''
    }

    $lines += '# Capability Ownership Matrix'
    $lines += ''
    $lines += '> Auto-generated from `management/governance/framework-governance.json`.'
    $lines += ''
    $lines += '| Capability | Owner | Dispatch Packages | Excalibur Packages | Rationale |'
    $lines += '|---|---|---|---|---|'
    foreach ($entry in $Matrix.capabilityOwnership) {
        $dispatchPackages = @($entry.dispatchPackages) -join ', '
        $excaliburPackages = @($entry.excaliburPackages) -join ', '
        if ([string]::IsNullOrWhiteSpace($dispatchPackages)) { $dispatchPackages = '—' }
        if ([string]::IsNullOrWhiteSpace($excaliburPackages)) { $excaliburPackages = '—' }
        $lines += "| $($entry.capability) | $($entry.owner) | $dispatchPackages | $excaliburPackages | $($entry.rationale) |"
    }

    $lines += ''
    $lines += '## Provider Naming Policy'
    $lines += ''
    $lines += "| Policy | Value |"
    $lines += "|---|---|"
    $lines += "| Canonical Postgres package | $($Matrix.packageNamingPolicy.preferredPackage) |"
    $lines += "| Legacy compatibility package | $($Matrix.packageNamingPolicy.legacyCompatibilityPackage) |"
    $lines += "| Canonical name | $($Matrix.packageNamingPolicy.postgresCanonicalName) |"
    $lines += "| Deprecation window | $($Matrix.packageNamingPolicy.deprecationWindow) |"
    $lines += ''
    $lines += "$($Matrix.packageNamingPolicy.migrationGuidance)"
    $lines += ''

    return $lines
}

if ($Mode -eq 'Governance') {
    $owners = @('Dispatch','Excalibur')
    $capabilityNameMap = @{}
    $dispatchOwnedPackages = @{}
    $excaliburOwnedPackages = @{}
    foreach ($entry in $matrix.capabilityOwnership) {
        if ([string]::IsNullOrWhiteSpace($entry.capability)) {
            $issues.Add('Capability ownership entry has empty capability name.')
            continue
        }

        if ($owners -notcontains $entry.owner) {
            $issues.Add("Capability '$($entry.capability)' has invalid owner '$($entry.owner)'.")
        }

        $capabilityKey = $entry.capability.Trim().ToLowerInvariant()
        if ($capabilityNameMap.ContainsKey($capabilityKey)) {
            $issues.Add("Duplicate capability entry detected: '$($entry.capability)'.")
        }
        else {
            $capabilityNameMap[$capabilityKey] = $true
        }

        $dispatchPackages = @($entry.dispatchPackages | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        $excaliburPackages = @($entry.excaliburPackages | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })

        $entryOverlap = @($dispatchPackages | Where-Object { $_ -in $excaliburPackages })
        foreach ($package in $entryOverlap) {
            $issues.Add("Capability '$($entry.capability)' lists package '$package' in both dispatch and excalibur package sets.")
        }

        foreach ($package in $dispatchPackages) {
            if ($excaliburOwnedPackages.ContainsKey($package)) {
                $issues.Add("Package '$package' is assigned to both Dispatch and Excalibur capabilities ('$($entry.capability)' and '$($excaliburOwnedPackages[$package])').")
            }
            elseif (-not $dispatchOwnedPackages.ContainsKey($package)) {
                $dispatchOwnedPackages[$package] = $entry.capability
            }
        }

        foreach ($package in $excaliburPackages) {
            if ($dispatchOwnedPackages.ContainsKey($package)) {
                $issues.Add("Package '$package' is assigned to both Dispatch and Excalibur capabilities ('$($dispatchOwnedPackages[$package])' and '$($entry.capability)').")
            }
            elseif (-not $excaliburOwnedPackages.ContainsKey($package)) {
                $excaliburOwnedPackages[$package] = $entry.capability
            }
        }
    }

    $preferredPackage = $matrix.packageNamingPolicy.preferredPackage
    $legacyPackage = $matrix.packageNamingPolicy.legacyCompatibilityPackage

    if ([string]::IsNullOrWhiteSpace($preferredPackage)) {
        $issues.Add('packageNamingPolicy.preferredPackage is required.')
    }
    else {
        $preferredProject = "src/Excalibur/$preferredPackage/$preferredPackage.csproj"
        Test-PathExists -PathToCheck $preferredProject | Out-Null
    }

    $hasLegacyPackage = -not [string]::IsNullOrWhiteSpace($legacyPackage) -and $legacyPackage -ne 'n/a'
    if ($hasLegacyPackage) {
        $legacyProject = "src/Excalibur/$legacyPackage/$legacyPackage.csproj"
        Test-PathExists -PathToCheck $legacyProject | Out-Null
    }

    foreach ($pkg in $matrix.criticalPackageTestMatrix) {
        Test-PathExists -PathToCheck $pkg.project | Out-Null

        $suiteMembers = @($pkg.suites.PSObject.Properties)
        if ($suiteMembers.Count -eq 0) {
            $issues.Add("Critical package '$($pkg.package)' has no test suites mapped.")
            continue
        }

        foreach ($suite in $suiteMembers) {
            $paths = @($suite.Value)
            if ($paths.Count -eq 0) {
                $issues.Add("Critical package '$($pkg.package)' suite '$($suite.Name)' has no test projects.")
                continue
            }

            foreach ($testPath in $paths) {
                Test-PathExists -PathToCheck (Normalize-RepoPath $testPath) | Out-Null
            }
        }
    }

    $rules = @($matrix.packageTestMappingRules)
    if ($rules.Count -eq 0) {
        $issues.Add('packageTestMappingRules is required and must not be empty.')
    }
    else {
        foreach ($rule in $rules) {
            if ([string]::IsNullOrWhiteSpace($rule.name)) {
                $issues.Add('A packageTestMappingRules entry is missing name.')
            }
            if ([string]::IsNullOrWhiteSpace($rule.packagePattern)) {
                $issues.Add("packageTestMappingRules entry '$($rule.name)' is missing packagePattern.")
            }
            else {
                try {
                    [void][regex]::new($rule.packagePattern)
                }
                catch {
                    $issues.Add("Invalid regex in packageTestMappingRules '$($rule.name)': $($rule.packagePattern)")
                }
            }

            $suiteMembers = @($rule.suites.PSObject.Properties)
            if ($suiteMembers.Count -eq 0) {
                $issues.Add("packageTestMappingRules '$($rule.name)' has no suite paths.")
                continue
            }

            foreach ($suite in $suiteMembers) {
                foreach ($testPath in @($suite.Value)) {
                    Test-PathExists -PathToCheck (Normalize-RepoPath $testPath) | Out-Null
                }
            }
        }

        $packableProjects = Get-PackableProjects -SourceRoot 'src'
        $unmappedPackages = New-Object System.Collections.Generic.List[string]
        foreach ($projectPath in $packableProjects) {
            $packageId = Get-PackageId -ProjectPath $projectPath
            $matched = $false
            foreach ($rule in $rules) {
                if ($packageId -match $rule.packagePattern) {
                    $matched = $true
                    break
                }
            }

            if (-not $matched) {
                $unmappedPackages.Add("$packageId ($projectPath)")
            }
        }

        if ($unmappedPackages.Count -gt 0) {
            foreach ($entry in $unmappedPackages) {
                $issues.Add("Packable shipping package has no test mapping rule: $entry")
            }
        }

        foreach ($rule in $rules) {
            $ruleMatchCount = 0
            foreach ($projectPath in $packableProjects) {
                $packageId = Get-PackageId -ProjectPath $projectPath
                if ($packageId -match $rule.packagePattern) {
                    $ruleMatchCount++
                }
            }

            if ($ruleMatchCount -eq 0) {
                $warnings.Add("packageTestMappingRules '$($rule.name)' does not currently match any packable package.")
            }
        }
    }

    $allSampleProjects = @(Get-ChildItem samples -Recurse -Filter '*.csproj' -File | Where-Object {
        $_.FullName -notmatch '[\\/](obj|bin)[\\/]'
    } | ForEach-Object {
        Convert-ToRepoPath -FullPath $_.FullName
    } | Sort-Object -Unique)

    $certifiedRaw = @($matrix.sampleFitness.certified)
    $quarantinedRaw = @($matrix.sampleFitness.quarantined)
    $certified = @($certifiedRaw | ForEach-Object { Normalize-RepoPath $_ })
    $quarantined = @($quarantinedRaw | ForEach-Object { Normalize-RepoPath $_ })

    $duplicateCertified = @($certified | Group-Object | Where-Object { $_.Count -gt 1 } | Select-Object -ExpandProperty Name)
    $duplicateQuarantined = @($quarantined | Group-Object | Where-Object { $_.Count -gt 1 } | Select-Object -ExpandProperty Name)
    foreach ($dup in $duplicateCertified) {
        $issues.Add("Duplicate certified sample entry: $dup")
    }
    foreach ($dup in $duplicateQuarantined) {
        $issues.Add("Duplicate quarantined sample entry: $dup")
    }

    $certified = @($certified | Sort-Object -Unique)
    $quarantined = @($quarantined | Sort-Object -Unique)
    $classified = @($certified + $quarantined | Sort-Object -Unique)

    $overlap = @($certified | Where-Object { $_ -in $quarantined })
    foreach ($samplePath in $overlap) {
        $issues.Add("Sample cannot be both certified and quarantined: $samplePath")
    }

    foreach ($samplePath in $classified) {
        Test-PathExists -PathToCheck $samplePath | Out-Null
    }

    $unclassified = @($allSampleProjects | Where-Object { $_ -notin $classified })
    foreach ($samplePath in $unclassified) {
        $issues.Add("Sample project is not classified in sampleFitness matrix: $samplePath")
    }

    $smokeProfiles = @($matrix.sampleFitness.smokeProfiles)
    if ($smokeProfiles.Count -eq 0) {
        $issues.Add('sampleFitness.smokeProfiles must include entries for certified samples.')
    }

    $smokeByProject = @{}
    foreach ($profile in $smokeProfiles) {
        $project = Normalize-RepoPath $profile.project
        if ([string]::IsNullOrWhiteSpace($project)) {
            $issues.Add('sampleFitness.smokeProfiles contains entry with empty project.')
            continue
        }

        if ($smokeByProject.ContainsKey($project)) {
            $issues.Add("Duplicate smoke profile for sample project: $project")
            continue
        }

        if ($certified -notcontains $project) {
            $issues.Add("Smoke profile project must be in certified sample set: $project")
        }

        if (-not (Test-Path $project)) {
            $issues.Add("Smoke profile project path not found: $project")
        }

        $smokeMode = $profile.mode
        if ($smokeMode -notin @('build','run')) {
            $issues.Add("Smoke profile mode must be 'build' or 'run' for $project.")
        }

        if ($smokeMode -eq 'run') {
            $timeout = 0
            if ($null -ne $profile.timeoutSeconds) {
                [int]$timeout = $profile.timeoutSeconds
            }

            if ($timeout -le 0) {
                $issues.Add("Run smoke profile must have timeoutSeconds > 0 for $project.")
            }
        }

        $smokeByProject[$project] = $true
    }

    foreach ($samplePath in $certified) {
        if (-not $smokeByProject.ContainsKey($samplePath)) {
            $issues.Add("Certified sample missing smoke profile: $samplePath")
        }
    }

    $summaryPath = Join-Path $OutDir 'summary.md'
    $jsonPath = Join-Path $OutDir 'report.json'

    $summary = @(
        '# Framework Governance Validation',
        '',
        "- Mode: $Mode",
        "- Matrix: $MatrixPath",
        "- Capability entries: $($matrix.capabilityOwnership.Count)",
        "- Critical package entries: $($matrix.criticalPackageTestMatrix.Count)",
        "- Test mapping rules: $($rules.Count)",
        "- Certified samples: $($certified.Count)",
        "- Quarantined samples: $($quarantined.Count)",
        "- Issues: $($issues.Count)",
        "- Warnings: $($warnings.Count)",
        ''
    )

    if ($issues.Count -gt 0) {
        $summary += '## Issues'
        foreach ($issue in $issues) {
            $summary += "- $issue"
        }
        $summary += ''
    }
    else {
        $summary += '## Result'
        $summary += 'Governance matrix validation passed.'
        $summary += ''
    }

    if ($warnings.Count -gt 0) {
        $summary += '## Warnings'
        foreach ($warning in $warnings) {
            $summary += "- $warning"
        }
        $summary += ''
    }

    $report = [PSCustomObject]@{
        mode = $Mode
        matrixPath = $MatrixPath
        issues = @($issues)
        warnings = @($warnings)
        capabilityCount = $matrix.capabilityOwnership.Count
        criticalPackageCount = $matrix.criticalPackageTestMatrix.Count
        packageTestRuleCount = $rules.Count
        certifiedSampleCount = $certified.Count
        quarantinedSampleCount = $quarantined.Count
    }

    New-Report -SummaryPath $summaryPath -Lines $summary -JsonPath $jsonPath -Object $report
}
else {
    $conformanceFiles = @(Get-ChildItem 'tests/conformance/Excalibur.Dispatch.Tests.Conformance' -Recurse -Filter '*.cs' -File)

    $rows = @()
    foreach ($transport in $matrix.transportParityMatrix) {
        $packagePath = "src/Dispatch/$($transport.package)/$($transport.package).csproj"
        $packageExists = Test-Path $packagePath
        if (-not $packageExists) {
            $issues.Add("Missing transport package project: $packagePath")
        }

        $conformanceClassFound = $false
        $providerClassFound = $false

        foreach ($file in $conformanceFiles) {
            $content = Get-Content -Raw $file.FullName
            if ($content -match "class\s+$([regex]::Escape($transport.conformanceClass))\b") {
                $conformanceClassFound = $true
            }
            if ($content -match "class\s+$([regex]::Escape($transport.providerConformanceClass))\b") {
                $providerClassFound = $true
            }
        }

        if (-not $conformanceClassFound) {
            $issues.Add("Missing transport conformance class '$($transport.conformanceClass)' for $($transport.transport).")
        }

        if (-not $providerClassFound) {
            $issues.Add("Missing provider conformance class '$($transport.providerConformanceClass)' for $($transport.transport).")
        }

        $rows += [PSCustomObject]@{
            Transport = $transport.transport
            Package = $transport.package
            PackageExists = $packageExists
            TransportConformanceClass = $transport.conformanceClass
            TransportClassFound = $conformanceClassFound
            ProviderConformanceClass = $transport.providerConformanceClass
            ProviderClassFound = $providerClassFound
            Status = if ($packageExists -and $conformanceClassFound -and $providerClassFound) { 'PASS' } else { 'FAIL' }
        }
    }

    $summaryPath = Join-Path $OutDir 'transport-parity-summary.md'
    $jsonPath = Join-Path $OutDir 'transport-parity-report.json'

    $summary = @(
        '# Transport Parity Dashboard',
        '',
        "- Mode: $Mode",
        "- Transports: $($rows.Count)",
        "- Failures: $((@($rows | Where-Object { $_.Status -eq 'FAIL' })).Count)",
        ''
    )

    $summary += '| Transport | Package | Transport Class | Provider Class | Status |'
    $summary += '|---|---|---|---|---|'
    foreach ($row in $rows) {
        $packageCell = if ($row.PackageExists) { $row.Package } else { 'MISSING' }
        $transportClassIcon = if ($row.TransportClassFound) { 'OK' } else { 'MISSING' }
        $providerClassIcon = if ($row.ProviderClassFound) { 'OK' } else { 'MISSING' }
        $summary += "| $($row.Transport) | $packageCell | $transportClassIcon $($row.TransportConformanceClass) | $providerClassIcon $($row.ProviderConformanceClass) | $($row.Status) |"
    }
    $summary += ''

    if ($issues.Count -gt 0) {
        $summary += '## Issues'
        foreach ($issue in $issues) {
            $summary += "- $issue"
        }
        $summary += ''
    }

    $report = [PSCustomObject]@{
        mode = $Mode
        issues = @($issues)
        transports = $rows
    }

    New-Report -SummaryPath $summaryPath -Lines $summary -JsonPath $jsonPath -Object $report
}

if ($Enforce -and $issues.Count -gt 0) {
    throw "Framework governance validation failed with $($issues.Count) issue(s)."
}

Write-Host 'Framework governance validation passed.'
