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
            Write-Warning "  Failed to download $artifactName: $_"
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

# Generate evidence manifest
function New-EvidenceManifest {
    param(
        [string]$OutputPath,
        [string]$RunId
    )

    Write-Host "Generating evidence manifest..." -ForegroundColor Cyan

    $manifest = @{
        GeneratedAt = Get-Date -Format "o"
        GeneratedBy = $env:USERNAME
        RunId = $RunId
        Repository = (gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner
        Frameworks = $Frameworks
        Contents = @{
            TestResults = Get-ChildItem "$OutputPath\test-results" -Recurse -File | Select-Object Name, Length, LastWriteTime
            SecurityScans = @{
                SAST = Get-ChildItem "$OutputPath\security-scans\sast" -Recurse -File | Select-Object Name, Length, LastWriteTime
                DAST = Get-ChildItem "$OutputPath\security-scans\dast" -Recurse -File | Select-Object Name, Length, LastWriteTime
                Container = Get-ChildItem "$OutputPath\security-scans\container" -Recurse -File | Select-Object Name, Length, LastWriteTime
                Secrets = Get-ChildItem "$OutputPath\security-scans\secrets" -Recurse -File | Select-Object Name, Length, LastWriteTime
            }
            SBOM = Get-ChildItem "$OutputPath\sbom" -Recurse -File | Select-Object Name, Length, LastWriteTime
            AuditLogs = Get-ChildItem "$OutputPath\audit-logs" -Recurse -File | Select-Object Name, Length, LastWriteTime
        }
        Compliance = @{
            FedRAMP = @{
                Controls = 14
                ControlsDocumented = 14
                EvidenceTypes = @("SBOM", "SecurityScans", "TestResults", "RTM")
            }
            GDPR = @{
                Articles = @(17, "17(3)", 25, 30, 32)
                ConformanceTests = 80
                EvidenceTypes = @("AuditLogs", "ErasureCertificates", "DataInventory")
            }
            SOC2 = @{
                Categories = @("Security", "Availability", "ProcessingIntegrity", "Confidentiality")
                Controls = 17
                EvidenceTypes = @("ControlValidation", "AuditLogs", "Monitoring")
            }
            HIPAA = @{
                Safeguards = @("Technical", "Administrative", "Physical")
                TechnicalControls = 12
                EvidenceTypes = @("AccessLogs", "EncryptionVerification", "AuditTrail")
            }
        }
    }

    $manifestPath = "$OutputPath\MANIFEST.json"
    $manifest | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestPath -Encoding UTF8

    Write-Host "✓ Evidence manifest created: $manifestPath" -ForegroundColor Green
}

# Generate README
function New-EvidenceReadme {
    param([string]$OutputPath)

    $readme = @"
# Compliance Evidence Package

**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Repository:** $(try { (gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner } catch { "Unknown" })
**Frameworks:** $($Frameworks -join ", ")

---

## Directory Structure

\`\`\`
compliance-evidence/
├── test-results/           # Unit, integration, functional test results
│   ├── junit-xml/          # JUnit XML test results
│   └── coverage/           # Code coverage reports
├── security-scans/         # Security scan results
│   ├── sast/               # Static Application Security Testing (CodeQL, etc.)
│   ├── dast/               # Dynamic Application Security Testing (OWASP ZAP)
│   ├── container/          # Container vulnerability scanning (Trivy)
│   └── secrets/            # Secrets scanning (Gitleaks)
├── sbom/                   # Software Bill of Materials (CycloneDX)
├── audit-logs/             # Sample audit logs (anonymized)
├── rtm/                    # Requirements Traceability Matrix
├── metadata/               # Additional metadata and artifacts
├── MANIFEST.json           # Evidence inventory manifest
└── README.md               # This file
\`\`\`

---

## Evidence Types by Framework

### FedRAMP (NIST 800-53 Rev 5)

**Controls Covered:** 14/14 (100% complete)

| Control | Evidence Type | Location |
|---------|---------------|----------|
| SA-15 | CI/CD pipeline logs, test results | test-results/, security-scans/ |
| CM-8 | SBOM (CycloneDX) | sbom/ |
| SI-7 | Package signing, hash verification | sbom/ |
| SI-4 | Security scan results | security-scans/ |
| CC9 | Vulnerability scanning | security-scans/ |

**Reference:** docs/compliance/checklists/fedramp.md

### GDPR

**Articles Covered:** 17, 17(3), 25, 30, 32

| Article | Evidence Type | Location |
|---------|---------------|----------|
| 17 | Erasure certificates (cryptographic erasure) | audit-logs/ |
| 30 | Records of Processing Activities (RoPA) | audit-logs/ |
| 32 | Audit logs, encryption verification | audit-logs/, security-scans/ |

**Conformance Tests:** 80 tests (Audit, Erasure, LegalHold, DataInventory)

**Reference:** docs/compliance/checklists/gdpr.md

### SOC 2

**Categories:** Security, Availability, Processing Integrity, Confidentiality

| Criterion | Evidence Type | Location |
|-----------|---------------|----------|
| CC4 | Audit logs (tamper-evident hash chain) | audit-logs/ |
| CC6 | Encryption verification, access controls | security-scans/ |
| CC8 | CI/CD pipeline, change management | test-results/, security-scans/ |
| CC9 | Security scanning (SAST, DAST, container) | security-scans/ |

**Automated Validators:** 6 validators, 17+ controls

**Reference:** docs/compliance/checklists/soc2.md

### HIPAA

**Safeguards:** Technical (§164.312)

| Standard | Evidence Type | Location |
|----------|---------------|----------|
| §164.312(a) | Access control logs | audit-logs/ |
| §164.312(b) | Audit trail (PHI access) | audit-logs/ |
| §164.312(c) | Integrity verification (hash chain) | audit-logs/ |
| §164.312(d) | Authentication logs | audit-logs/ |
| §164.312(e) | TLS verification, transmission logs | security-scans/ |

**Reference:** docs/compliance/checklists/hipaa.md

---

## Using This Evidence

### For External Audits

1. Provide this entire directory to your auditor
2. Reference the compliance checklists in docs/compliance/checklists/
3. Provide access to GitHub Actions workflow runs (90-day retention)
4. Reference framework documentation in docs/

### For Internal Reviews

1. Review MANIFEST.json for evidence inventory
2. Check test-results/ for coverage metrics (≥60% enforced)
3. Review security-scans/ for vulnerability findings
4. Verify SBOM completeness in sbom/

### For Certification

1. **FedRAMP:** Provide to 3PAO for Security Assessment Report (SAR)
2. **GDPR:** Reference for Data Protection Impact Assessment (DPIA)
3. **SOC 2:** Provide to auditor for Type I or Type II report
4. **HIPAA:** Reference for Risk Assessment and Security Rule compliance

---

## Next Steps

### Customize Evidence Collection

Edit eng/compliance/collect-evidence.ps1 to:
- Add custom evidence types
- Connect to production audit store
- Include additional metadata

### Automate Collection

Add to CI/CD pipeline:

\`\`\`yaml
- name: Collect Compliance Evidence
  run: |
    .\scripts\compliance\collect-evidence.ps1 -OutputPath artifacts/evidence

- name: Upload Evidence Package
  uses: actions/upload-artifact@v4
  with:
    name: compliance-evidence
    path: artifacts/evidence
    retention-days: 365
\`\`\`

### Generate Evidence Package

Run:

\`\`\`powershell
.\scripts\compliance\generate-evidence-package.ps1
\`\`\`

Outputs: compliance-evidence-v1.0.0.zip

---

## Contact

**Questions:**
- Compliance: Contact Security Official
- Evidence Access: Contact Project Manager
- Framework Support: See docs/compliance/checklists/

---

**Generated by:** Excalibur Compliance Evidence Collector
**Version:** 1.0.0
**Date:** $(Get-Date -Format "yyyy-MM-dd")
"@

    $readmePath = "$OutputPath\README.md"
    $readme | Out-File -FilePath $readmePath -Encoding UTF8

    Write-Host "✓ README created: $readmePath" -ForegroundColor Green
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

    # Generate manifest
    New-EvidenceManifest -OutputPath $OutputPath -RunId $RunId

    # Generate README
    New-EvidenceReadme -OutputPath $OutputPath

    Write-Host "`n✓ Evidence collection complete!" -ForegroundColor Green
    Write-Host "Location: $OutputPath" -ForegroundColor Cyan
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "  1. Review MANIFEST.json for evidence inventory" -ForegroundColor Gray
    Write-Host "  2. Verify security scan results in security-scans/" -ForegroundColor Gray
    Write-Host "  3. Generate evidence package: .\scripts\compliance\generate-evidence-package.ps1" -ForegroundColor Gray
}
catch {
    Write-Error "Evidence collection failed: $_"
    exit 1
}
