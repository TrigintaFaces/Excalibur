# FedRAMP Compliance Documentation

> **Disclaimer:** Excalibur is a software framework that provides **technical controls** to assist with FedRAMP compliance. Using this framework does **not** make your application FedRAMP authorized. FedRAMP authorization requires a complete System Security Plan (SSP), third-party assessment (3PAO), and PMO review specific to your deployment. The control statuses below indicate that the framework provides the **technical capability** — your organization must still implement organizational policies, processes, and infrastructure controls to achieve authorization.

**Framework:** Excalibur.Dispatch
**Standard:** NIST 800-53 Rev 5
**Epic:** i43v - FedRAMP Government Compliance
**Status:** 14/14 technical controls implemented (framework level)

---

## Overview

This directory contains compliance documentation for NIST 800-53 Rev 5 controls implemented in the Excalibur framework to support **FedRAMP (Federal Risk and Authorization Management Program)** authorization.

**FedRAMP Impact Level:** Moderate (baseline)
**Authorization Boundary:** Excalibur framework (NuGet packages)
**Implementation Approach:** Secure-by-default framework capabilities

---

## Control Status (Epic i43v)

| Control | Title | Status | Implementation |
|---------|-------|--------|----------------|
| **AC-3** | Access Enforcement | ✅ SATISFIED | Declarative authorization (`[RequirePermission]`) |
| **AC-6** | Least Privilege | ✅ SATISFIED | Role-based access control (RBAC) |
| **AU-2** | Audit Events | ✅ SATISFIED | `IAuditLogger` interface, structured logging |
| **AU-3** | Content of Audit Records | ✅ SATISFIED | Comprehensive audit log schema |
| **AU-9** | Protection of Audit Information | ✅ SATISFIED | Immutable audit logs, tamper detection |
| **IA-5** | Authenticator Management | ✅ SATISFIED | Argon2id password hashing, key rotation |
| **SC-8** | Transmission Confidentiality | ✅ SATISFIED | TLS 1.2+ enforcement, encryption pipeline |
| **SC-13** | Cryptographic Protection | ✅ SATISFIED | `IEncryptionProvider`, AES-256-GCM |
| **SC-28** | Protection of Information at Rest | ✅ SATISFIED | Field-level encryption (`[PersonalData]`) |
| **SI-4** | System Monitoring | ✅ SATISFIED | OpenTelemetry integration, health checks |
| **SI-7** | Software Integrity | ✅ SATISFIED | SBOM hash verification, dependency vulnerability scanning |
| **PM-11** | Mission/Business Process Definition | ✅ SATISFIED | Requirements traceability matrix (RTM) |
| **SA-15** | Development Process | ✅ SATISFIED | CI/CD pipeline, automated quality gates |
| **CM-8** | Component Inventory | ✅ SATISFIED | SBOM generation (CycloneDX) |

**Epic i43v Status:** 14/14 controls (100% complete) - **READY FOR CLOSURE**

---

## Control Documentation

### Detailed Control Mappings

- **[CM-8-SBOM.md](./CM-8-SBOM.md)** - Software Bill of Materials (SBOM) implementation

---

## Implementation Highlights

### Security Controls

**Encryption (SC-8, SC-13, SC-28):**
- `IEncryptionProvider` abstraction for pluggable encryption
- AES-256-GCM for data at rest
- TLS 1.2+ for data in transit
- Field-level encryption via `[PersonalData]` attribute

**Audit Logging (AU-2, AU-3, AU-9):**
- `IAuditLogger` interface with `IAuditStore` persistence
- Structured audit logs with correlation IDs
- Immutable append-only audit trails
- Tamper detection via cryptographic hashing

**Access Control (AC-3, AC-6):**
- Declarative authorization (`[RequirePermission]`)
- Role-based access control (RBAC)
- Least privilege enforcement

### Development Process (SA-15)

**CI/CD Pipeline:**
- Multi-platform builds (Ubuntu, Windows, macOS)
- Unit, integration, and functional tests
- Requirements traceability validation (RTM)
- Security scanning (SAST, DAST, container scan)
- SBOM generation (CycloneDX)
- Code coverage enforcement (60% threshold)
- Dependency vulnerability scanning

**Quality Gates:**
- Serialization policy validation (R0.14)
- Dead code detection (R1.15, R1.17)
- API compatibility checks
- Transitive dependency bloat detection
- Banned APIs scanning
- Secrets scanning (Gitleaks)

### Component Inventory (CM-8)

**SBOM Generation:**
- Automated CycloneDX SBOM for all packages
- 90-day artifact retention
- Package-level granularity with dependency graph
- GitHub Security tab integration
- Validation on every CI build

See [CM-8-SBOM.md](./CM-8-SBOM.md) for detailed implementation.

---

## Evidence Package

### Primary Evidence

**Control Implementation:**
- Source code in `src/` (framework capabilities)

**Process Evidence:**
- CI/CD pipeline (`.github/workflows/ci.yml`)
- GitHub Actions workflow runs (audit trail)
- Test coverage reports (≥60% enforced)
- Security scan reports (SAST, DAST, container, secrets)

**Artifact Evidence:**
- SBOM artifacts (CycloneDX JSON/XML)
- NuGet packages (hash-verified)
- Docker images (Trivy-scanned)
- RTM reports (requirements traceability)

### Evidence Generation

Evidence package generation includes:
- Certification readiness checklists (HIPAA, FedRAMP, SOC 2, GDPR, PCI-DSS)
- Automated evidence package generation tooling

---

## Continuous Compliance

### Automated Monitoring

**Every CI Build:**
- SBOM generation (component inventory)
- Dependency vulnerability scanning
- Security policy enforcement (CRITICAL vulnerabilities block)
- Requirements traceability validation
- Code coverage measurement

**Pull Request Gates:**
- Coverage diff enforcement (≤1% drop on touched files)
- API compatibility checks
- Architecture boundary validation
- Transitive dependency bloat detection

### Review Cadence

**Quarterly Reviews:**
- Control effectiveness assessment
- Evidence package updates
- Compliance documentation refresh
- Security posture evaluation

**On-Demand:**
- Pre-release compliance verification
- Audit preparation support
- Incident response documentation

---

## Auditor Access

### Evidence Retrieval

**GitHub Actions:**
```bash
# Download SBOM artifacts
gh run download <run-id> -n cyclonedx-sbom

# Download security scan reports
gh run download <run-id> -n zap-dast-report
gh run download <run-id> -n trivy-container-scan
```

**Audit Trail:**
- GitHub commit history (Git SHA traceability)
- GitHub Actions workflow runs (90-day retention)
- Pull request reviews (approval workflow)

---

## Framework Consumer Guidance

### FedRAMP Authorization Inheritance

**Excalibur Provides:**
- Compliant security capabilities (encryption, audit, access control)
- SBOM for supply chain transparency
- Secure development process evidence
- Continuous compliance monitoring

**Consumer Responsibilities:**
- System Security Plan (SSP) development
- Control implementation statements (inheriting framework capabilities)
- Continuous monitoring plan
- Incident response procedures

### Control Inheritance

Consumers can **inherit** framework controls:
- **AC-3, AC-6:** Use framework authorization (`[RequirePermission]`)
- **AU-2, AU-3, AU-9:** Use framework audit logging (`IAuditLogger`)
- **SC-8, SC-13, SC-28:** Use framework encryption (`IEncryptionProvider`)
- **SI-4:** Use framework telemetry (OpenTelemetry integration)
- **CM-8:** Reference framework SBOM in SSP

**Example Inheritance Statement:**
> "The application inherits SC-13 (Cryptographic Protection) from the Excalibur framework, which implements AES-256-GCM encryption via the `IEncryptionProvider` abstraction. Framework compliance evidence includes NIST FIPS 140-2 validated algorithms and continuous vulnerability scanning."

---

## Contact

**Questions:**
- Product Manager: Requirements clarification, control scope
- Software Architect: Technical implementation, architecture decisions
- Project Manager: Evidence packages, sprint planning, audit coordination

**Escalation:**
- Security incidents: See `docs/security/incident-response.md`
- Compliance gaps: Create GitHub issue with `compliance` label
- Audit requests: Contact Project Manager for evidence package

---

## References

**Standards:**
- [FedRAMP Program](https://www.fedramp.gov/)
- [NIST SP 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [FedRAMP Moderate Baseline](https://www.fedramp.gov/assets/resources/documents/FedRAMP_Security_Controls_Baseline.xlsx)

**Framework Documentation:**
- [Security Guide](../../advanced/security.md) - Comprehensive security capabilities
- [Deployment Guide](../../advanced/deployment.md) - Cloud-native deployment patterns
- [Testing Guide](../../advanced/testing.md) - Conformance testing

**Related Compliance:**
- GDPR: See `docs/compliance/checklists/gdpr.md`
- SOC 2: See security guides in `docs/security/`
- HIPAA: See `docs/compliance/checklists/hipaa.md`

---

## See Also

- [CM-8 SBOM Implementation](./CM-8-SBOM.md) - Software component inventory details
- [Compliance Checklists](../checklists/fedramp.md) - FedRAMP checklist
- [Security](../../security/index.md) - Security implementation guides

---

**Last Updated:** 2026-01-01
**Next Review:** 2026-04-01
**Status:** 14/14 controls SATISFIED
