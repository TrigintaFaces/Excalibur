<#
.SYNOPSIS
    Collects compliance evidence from CI/CD artifacts and system state.

.DESCRIPTION
    This script collects evidence from GitHub Actions workflow runs, including:
    - Test results (JUnit XML, coverage reports)
    - Security scan results (SAST, DAST, container scans, secrets)
    - SBOM artifacts (CycloneDX JSON/XML)
    - Audit log samples
    - Requirements Traceability Matrix (RTM)

    Evidence is organized by compliance framework (FedRAMP, GDPR, SOC 2, HIPAA).

.PARAMETER OutputPath
    Directory where evidence will be collected. Default: ./compliance-evidence

.PARAMETER RunId
    GitHub Actions run ID to collect evidence from. If not specified, uses the latest successful run.

.PARAMETER Frameworks
    Comma-separated list of frameworks to collect evidence for.
    Valid values: FedRAMP, GDPR, SOC2, HIPAA, All
    Default: All

.PARAMETER IncludeAuditLogs
    Include sample audit logs (anonymized). Default: $true

.PARAMETER MaxAuditSamples
    Maximum number of audit log samples to include. Default: 100

.EXAMPLE
    .\collect-evidence.ps1
    Collects all evidence from the latest CI run to ./compliance-evidence

.EXAMPLE
    .\collect-evidence.ps1 -OutputPath "C:\Evidence" -Frameworks "FedRAMP,SOC2"
    Collects FedRAMP and SOC 2 evidence to C:\Evidence

.EXAMPLE
    .\collect-evidence.ps1 -RunId 123456789 -MaxAuditSamples 50
    Collects evidence from specific run ID with 50 audit samples

.NOTES
    Requires GitHub CLI (gh) to be installed and authenticated.
    Run: gh auth login
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath = ".\compliance-evidence",

    [Parameter()]
    [string]$RunId,

    [Parameter()]
    [ValidateSet("FedRAMP", "GDPR", "SOC2", "HIPAA", "All")]
    [string[]]$Frameworks = @("All"),

    [Parameter()]
    [bool]$IncludeAuditLogs = $true,

    [Parameter()]
    [int]$MaxAuditSamples = 100
)

$ErrorActionPreference = "Stop"

# Check prerequisites
function Test-Prerequisites {
    Write-Host "Checking prerequisites..." -ForegroundColor Cyan

    # Check GitHub CLI
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) is not installed. Install from: https://cli.github.com/"
    }

    # Check authentication
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not authenticated. Run: gh auth login"
    }

    Write-Host "✓ Prerequisites satisfied" -ForegroundColor Green
}

# Get latest successful workflow run
function Get-LatestWorkflowRun {
    Write-Host "Finding latest successful CI workflow run..." -ForegroundColor Cyan

    $runs = gh run list --workflow=ci.yml --status=success --limit=1 --json databaseId,conclusion,createdAt | ConvertFrom-Json

    if ($runs.Count -eq 0) {
        throw "No successful workflow runs found"
    }

    $run = $runs[0]
    Write-Host "✓ Found run: $($run.databaseId) ($(Get-Date $run.createdAt -Format 'yyyy-MM-dd HH:mm'))" -ForegroundColor Green

    return $run.databaseId
}

# Create evidence directory structure
function Initialize-EvidenceDirectory {
    param([string]$Path)

    Write-Host "Creating evidence directory structure..." -ForegroundColor Cyan

    $structure = @(
        "$Path",
        "$Path\test-results",
        "$Path\security-scans",
        "$Path\security-scans\sast",
        "$Path\security-scans\dast",
        "$Path\security-scans\container",
        "$Path\security-scans\secrets",
        "$Path\sbom",
        "$Path\audit-logs",
        "$Path\rtm",
        "$Path\metadata"
    )

    foreach ($dir in $structure) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Write-Host "✓ Directory structure created" -ForegroundColor Green
}

# Download artifacts from GitHub Actions
function Get-WorkflowArtifacts {
    param(
        [string]$RunId,
        [string]$OutputPath
    )

    Write-Host "Downloading artifacts from run $RunId..." -ForegroundColor Cyan

    # Get list of artifacts
    $artifacts = gh run view $RunId --json artifacts | ConvertFrom-Json

    if ($artifacts.artifacts.Count -eq 0) {
        Write-Warning "No artifacts found for run $RunId"
        return
    }

    Write-Host "Found $($artifacts.artifacts.Count) artifacts" -ForegroundColor Yellow

    # Download each artifact
    foreach ($artifact in $artifacts.artifacts) {
        $artifactName = $artifact.name
        Write-Host "  Downloading: $artifactName" -ForegroundColor Gray

        try {
            # Determine target directory based on artifact type
            $targetDir = switch -Regex ($artifactName) {
                "test-results|coverage" { "$OutputPath\test-results" }
                "sarif|codeql" { "$OutputPath\security-scans\sast" }
                "zap|dast" { "$OutputPath\security-scans\dast" }
                "trivy|container" { "$OutputPath\security-scans\container" }
                "gitleaks|secrets" { "$OutputPath\security-scans\secrets" }
                "sbom|cyclonedx" { "$OutputPath\sbom" }
                default { "$OutputPath\metadata" }
            }

            # Download artifact
            gh run download $RunId -n $artifactName -D $targetDir
            Write-Host "    ✓ Downloaded to: $targetDir" -ForegroundColor Green
        }
        catch {
            Write-Warning "  Failed to download ${artifactName}: $_"
        }
    }
}

# Export audit log samples (simulated - would connect to actual audit store)
function Export-AuditLogSamples {
    param(
        [string]$OutputPath,
        [int]$MaxSamples
    )

    if (-not $IncludeAuditLogs) {
        Write-Host "Skipping audit log samples (disabled)" -ForegroundColor Yellow
        return
    }

    Write-Host "Exporting audit log samples..." -ForegroundColor Cyan

    # NOTE: In production, this would connect to your IAuditStore implementation
    # For now, create a sample template showing the expected format

    $sampleAuditLog = @{
        Metadata = @{
            ExportedAt = Get-Date -Format "o"
            SampleCount = $MaxSamples
            Anonymized = $true
            Note = "Replace with actual audit log query: SELECT TOP $MaxSamples * FROM AuditLog ORDER BY Timestamp DESC"
        }
        Samples = @(
            @{
                EventId = "00000000-0000-0000-0000-000000000001"
                EventType = "PHIAccessed"
                UserId = "[REDACTED]"
                Timestamp = Get-Date -Format "o"
                Outcome = "Success"
                CorrelationId = "cor-123"
                Metadata = @{
                    Action = "Read"
                    Resource = "PatientRecord"
                }
            },
            @{
                EventId = "00000000-0000-0000-0000-000000000002"
                EventType = "DataExported"
                UserId = "[REDACTED]"
                Timestamp = Get-Date -Format "o"
                Outcome = "Success"
                CorrelationId = "cor-124"
                Metadata = @{
                    Action = "Export"
                    Format = "PDF"
                }
            }
        )
        Instructions = "To include real audit logs, implement IDataInventoryService and query your audit store. Ensure data is anonymized before export."
    }

    $samplePath = "$OutputPath\audit-logs\sample-audit-logs.json"
    $sampleAuditLog | ConvertTo-Json -Depth 10 | Out-File -FilePath $samplePath -Encoding UTF8

    Write-Host "✓ Sample audit log template created: $samplePath" -ForegroundColor Green
    Write-Host "  NOTE: Replace with actual audit log queries in production" -ForegroundColor Yellow
}

# -- Derived compliance figures ---------------------------------------------------------------------
#
# Every control figure this collector reports is derived from the evidence actually collected into the
# package, using eng/compliance/control-evidence-map.tsv as the control inventory. Nothing here is a
# literal. The figures used to be baked into the document template (14 of 14 FedRAMP controls, 80 GDPR
# conformance tests, 17 SOC 2 controls, 12 HIPAA technical controls) with no input that could make them
# print anything else, so a run that downloaded no artifacts still asserted complete control
# documentation. The audience for that document is an auditor, and this script is published, so a
# consumer could generate it against their own repository and be handed our numbers as if they were
# theirs. This mirrors the derivation already implemented in the sibling collect-evidence.sh.
#
# Exit codes: 0 = every reported figure was derived; 2 = REFUSE (the package was written, but at least
# one figure could not be derived and is reported null/REFUSED rather than 0).

$script:EvidenceCategories = @('test-results', 'security-scans', 'sbom', 'audit-logs', 'rtm')
$script:ControlMap = if ($env:CONTROL_EVIDENCE_MAP) { $env:CONTROL_EVIDENCE_MAP } else { Join-Path $PSScriptRoot 'control-evidence-map.tsv' }
$script:RefusalReasons = @()
$script:CoverageSummary = @()

# Record a reason a figure could not be derived. A refusal never stops the package being written; it
# changes what the package is allowed to claim, and it changes the exit code.
function Add-RefusalReason {
    param([string]$Reason)
    $script:RefusalReasons += $Reason
    Write-Warning "REFUSED: $Reason"
}

# Files collected into one evidence category, excluding *.template.json placeholders: a blank form this
# script writes on every run is not evidence and must not count toward a control.
# Returns $null when the directory is absent -- "never looked" is not "nothing found".
function Get-EvidenceFileCount {
    param([string]$PackageRoot, [string]$Category)

    $dir = Join-Path $PackageRoot $Category
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return $null }

    return @(Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*.template.json' }).Count
}

# The map rows for one framework, as objects with ControlId and Categories.
# Returns $null when the map is unreadable or names no control for the framework: an empty control set
# would otherwise score as "0 of 0 controls", which reads as a clean bill of health for a framework we
# never assessed.
function Read-ControlMap {
    param([string]$Framework)

    if (-not (Test-Path -LiteralPath $script:ControlMap -PathType Leaf)) { return $null }

    $rows = @(Get-Content -LiteralPath $script:ControlMap |
        Where-Object { $_ -notmatch '^\s*#' -and $_ -notmatch '^\s*$' } |
        ForEach-Object {
            $f = $_ -split "`t"
            if ($f.Count -ge 3 -and $f[0].Trim() -eq $Framework) {
                [pscustomobject]@{ ControlId = $f[1].Trim(); Categories = $f[2].Trim() }
            }
        })

    if ($rows.Count -eq 0) { return $null }
    return $rows
}

# The frameworks to report on. "All" expands to every framework the map defines, so adding one to the
# map adds it here without editing this script.
function Get-RequestedFrameworks {
    if ($Frameworks -contains 'All') {
        if (-not (Test-Path -LiteralPath $script:ControlMap -PathType Leaf)) { return @() }
        return @(Get-Content -LiteralPath $script:ControlMap |
            Where-Object { $_ -notmatch '^\s*#' -and $_ -notmatch '^\s*$' } |
            ForEach-Object { ($_ -split "`t")[0].Trim() } |
            Where-Object { $_ } |
            Select-Object -Unique)
    }
    return @($Frameworks)
}

# Generate evidence manifest -- every compliance figure derived from the collected package.
function New-EvidenceManifest {
    param(
        [string]$OutputPath,
        [string]$RunId
    )

    Write-Host "Generating evidence manifest..." -ForegroundColor Cyan

    # Reset the derived state, so a second call reports this package rather than this package plus
    # whatever the last one refused.
    $script:RefusalReasons = @()
    $script:CoverageSummary = @()

    $repository = if ($env:EVIDENCE_REPOSITORY) {
        $env:EVIDENCE_REPOSITORY
    } else {
        try { (gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner } catch { "Unknown" }
    }

    # -- Evidence counts, per category, refusing on an absent directory ----------------------------
    # A $null entry means REFUSED. Every downstream consumer must treat it as unknown, never as zero.
    $counts = [ordered]@{}
    foreach ($category in $script:EvidenceCategories) {
        $count = Get-EvidenceFileCount -PackageRoot $OutputPath -Category $category
        if ($null -eq $count) {
            Add-RefusalReason "evidence directory missing: $OutputPath/$category - cannot distinguish 'no evidence collected' from 'never looked'"
        }
        $counts[$category] = $count
    }

    # -- Control coverage, per framework, derived from the map and the counts above ----------------
    $coverage = [ordered]@{
        Source = 'eng/compliance/control-evidence-map.tsv'
        Basis  = 'ControlsDocumented counts in-scope controls whose every mapped evidence category has at least one collected file in this package. Controls whose evidence is documentation or a business process are mapped none and are never counted as documented. A package with no collected evidence therefore reports zero.'
    }

    foreach ($framework in (Get-RequestedFrameworks)) {
        if (-not $framework) { continue }

        $rows = Read-ControlMap -Framework $framework
        if ($null -eq $rows) {
            Add-RefusalReason "no controls mapped for framework '$framework' in $($script:ControlMap) - cannot derive its coverage"
            $coverage[$framework] = [ordered]@{
                Status = 'REFUSED'
                Reason = 'no controls for this framework in the control map; no coverage figure can be derived'
            }
            continue
        }

        $inScope = 0
        $documentedIds = @()
        $mappedCategories = @()

        foreach ($row in $rows) {
            $inScope++

            # 'none' -- evidence lives outside the pipeline. Counted in scope, never documented.
            if ($row.Categories -eq 'none') { continue }

            # ALL mapped categories must be present. A control needing a test result and a scan is not
            # substantiated by whichever one happens to be there.
            $satisfied = $true
            foreach ($cat in ($row.Categories -split ',')) {
                $cat = $cat.Trim()
                if ($script:EvidenceCategories -notcontains $cat) {
                    Add-RefusalReason "control $framework/$($row.ControlId) maps to unknown evidence category '$cat' - scoring it unmet would under-report coverage while looking measured"
                    $satisfied = $false
                    continue
                }
                $mappedCategories += $cat
                # A $null count is REFUSED, not zero: an unmeasurable category cannot satisfy anything.
                if ($null -eq $counts[$cat] -or $counts[$cat] -eq 0) { $satisfied = $false }
            }

            if ($satisfied) { $documentedIds += $row.ControlId }
        }

        $coverage[$framework] = [ordered]@{
            ControlsInScope          = $inScope
            ControlsDocumented       = $documentedIds.Count
            ControlsDocumentedIds    = @($documentedIds)
            EvidenceCategoriesMapped = @($mappedCategories | Select-Object -Unique | Sort-Object)
        }

        $script:CoverageSummary += [pscustomobject]@{
            Framework  = $framework
            InScope    = $inScope
            Documented = $documentedIds.Count
        }
    }

    $manifest = [ordered]@{
        GeneratedAt        = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        GeneratedBy        = $env:USERNAME
        RunId              = $RunId
        Repository         = $repository
        Frameworks         = ($Frameworks -join ',')
        ManifestStatus     = $(if ($script:RefusalReasons.Count -eq 0) { 'COMPLETE' } else { 'REFUSED' })
        RefusalReasons     = @($script:RefusalReasons)
        EvidenceCounts     = $counts
        EvidenceCountBasis = 'Files collected into this package, excluding *.template.json placeholders. null means the category directory was absent and the count is unknown -- it does not mean zero.'
        ControlCoverage    = $coverage
    }

    $manifestPath = Join-Path $OutputPath 'MANIFEST.json'
    $manifest | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestPath -Encoding UTF8

    Write-Host "Evidence manifest created: $manifestPath" -ForegroundColor Green

    if ($script:RefusalReasons.Count -ne 0) { return 2 }
    return 0
}

# Render the coverage table the package README shows, from the figures the manifest derived.
# Emits an explicit "not derived" notice rather than a table of zeros when nothing was computed --
# a table of zeros looks like a measurement of a package with no evidence, which is a different claim.
function Format-CoverageTable {
    if ($script:CoverageSummary.Count -eq 0) {
        return 'No control coverage was derived for this package. See MANIFEST.json -> RefusalReasons.'
    }

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('| Framework | Controls in scope | Documented by evidence in this package |')
    [void]$sb.AppendLine('|---|---:|---:|')
    foreach ($row in $script:CoverageSummary) {
        [void]$sb.AppendLine("| $($row.Framework) | $($row.InScope) | $($row.Documented) |")
    }
    return $sb.ToString().TrimEnd()
}

# Generate README
function New-EvidenceReadme {
    param([string]$OutputPath)

    $repository = if ($env:EVIDENCE_REPOSITORY) {
        $env:EVIDENCE_REPOSITORY
    } else {
        try { (gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner } catch { "Unknown" }
    }

    $coverageTable = Format-CoverageTable
    $fence = [string][char]0x60 * 3

    $readme = @"
# Compliance Evidence Package

**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Repository:** $repository
**Frameworks:** $($Frameworks -join ", ")

---

## Directory Structure

$fence
compliance-evidence/
  test-results/           # Unit, integration, functional test results
    junit-xml/            # JUnit XML test results
    coverage/             # Code coverage reports
  security-scans/         # Security scan results
    sast/                 # Static Application Security Testing (CodeQL, etc.)
    dast/                 # Dynamic Application Security Testing (OWASP ZAP)
    container/            # Container vulnerability scanning (Trivy)
    secrets/              # Secrets scanning (Gitleaks)
  sbom/                   # Software Bill of Materials (CycloneDX)
  audit-logs/             # Sample audit logs (anonymized)
  rtm/                    # Requirements Traceability Matrix
  metadata/               # Additional metadata and artifacts
  MANIFEST.json           # Evidence inventory manifest
  README.md               # This file
$fence

---

## Control Coverage in This Package

Counted from the files actually collected here, not asserted. A control is **documented** when every
evidence category mapped to it in eng/compliance/control-evidence-map.tsv has at least one collected
file in this package.

$coverageTable

**What the remainder means.** A control that is in scope but not documented here is not thereby
non-compliant -- most are substantiated by documentation, configuration, or a business process that a
CI pipeline does not produce, and those are mapped so that they can never be counted as documented
however complete the download was. Read MANIFEST.json for the per-control identifiers and for any
figure the collector refused to derive.

---

## Using This Evidence

### For External Audits

1. Provide this entire directory to your auditor
2. Reference the compliance checklists in docs/compliance/checklists/
3. Provide access to GitHub Actions workflow runs (90-day retention)
4. Reference framework documentation in docs/

### For Internal Reviews

1. Review MANIFEST.json for evidence inventory
2. Check test-results/ for coverage metrics
3. Review security-scans/ for vulnerability findings
4. Verify SBOM completeness in sbom/

### For Certification

1. **FedRAMP:** Provide to 3PAO for Security Assessment Report (SAR)
2. **GDPR:** Reference for Data Protection Impact Assessment (DPIA)
3. **SOC 2:** Provide to auditor for Type I or Type II report
4. **HIPAA:** Reference for Risk Assessment and Security Rule compliance

---

## Contact

**Questions:**
- Compliance: Contact Security Official
- Evidence Access: Contact Project Manager
- Framework Support: See docs/compliance/checklists/

---

**Generated by:** Excalibur Compliance Evidence Collector
"@

    $readmePath = Join-Path $OutputPath 'README.md'
    $readme | Out-File -FilePath $readmePath -Encoding UTF8

    Write-Host "README created: $readmePath" -ForegroundColor Green
}

# Main execution
try {
    Write-Host "`n=== Excalibur Compliance Evidence Collector ===" -ForegroundColor Cyan
    Write-Host "Frameworks: $($Frameworks -join ', ')" -ForegroundColor Yellow
    Write-Host "Output: $OutputPath`n" -ForegroundColor Yellow

    # Check prerequisites
    Test-Prerequisites

    # Get run ID
    if (-not $RunId) {
        $RunId = Get-LatestWorkflowRun
    }

    # Create directory structure
    Initialize-EvidenceDirectory -Path $OutputPath

    # Download artifacts
    Get-WorkflowArtifacts -RunId $RunId -OutputPath $OutputPath

    # Export audit logs
    Export-AuditLogSamples -OutputPath $OutputPath -MaxSamples $MaxAuditSamples

    # Generate manifest. Its return value is the refusal state, captured on the very next statement:
    # anything in between would replace the status this exit code exists to report.
    $manifestStatus = New-EvidenceManifest -OutputPath $OutputPath -RunId $RunId

    # Generate README
    New-EvidenceReadme -OutputPath $OutputPath

    Write-Host "`n✓ Evidence collection complete!" -ForegroundColor Green
    Write-Host "Location: $OutputPath" -ForegroundColor Cyan
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "  1. Review MANIFEST.json for evidence inventory" -ForegroundColor Gray
    Write-Host "  2. Verify security scan results in security-scans/" -ForegroundColor Gray
    Write-Host "  3. Generate evidence package: .\eng\compliance\generate-evidence-package.ps1" -ForegroundColor Gray

    # REFUSE is not success. The package was written, but at least one figure could not be derived and
    # is reported null/REFUSED rather than 0 -- a caller that treats this as a pass ships a document
    # whose gaps look like measurements.
    if ($manifestStatus -eq 2) {
        Write-Warning "REFUSED: at least one figure could not be derived. The package was written and its MANIFEST.json records why."
        exit 2
    }
}
catch {
    Write-Error "Evidence collection failed: $_"
    exit 1
}
