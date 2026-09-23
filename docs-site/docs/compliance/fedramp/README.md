---
description: NIST 800-53 Rev 5 technical controls the Excalibur framework provides to support FedRAMP authorization.
---

# FedRAMP Compliance Documentation

:::warning Not legal advice

This page describes technical features that can **support** your compliance work. It is not legal
advice, and it does not establish that any system is compliant with any law, regulation or standard.
You remain responsible for your own compliance assessment, independent testing and validation, and
review by qualified legal and compliance professionals. See the [Compliance Disclaimer](../../legal/compliance-disclaimer.md).
:::

> **Disclaimer:** Excalibur is a software framework that provides **technical controls** to assist with FedRAMP compliance. Using this framework does **not** make your application FedRAMP authorized. FedRAMP authorization requires a complete System Security Plan (SSP), third-party assessment (3PAO), and PMO review specific to your deployment. The control statuses below indicate that the framework provides the **technical capability** — your organization must still implement organizational policies, processes, and infrastructure controls to achieve authorization.

**Framework:** Excalibur
**Standard:** NIST 800-53 Rev 5
**Status:** see the [FedRAMP checklist](../checklists/fedramp.md#control-mapping-table), which is the single place this project asserts control status

---

## Overview

This directory contains compliance documentation for NIST 800-53 Rev 5 controls implemented in the Excalibur framework to support **FedRAMP (Federal Risk and Authorization Management Program)** authorization.

**FedRAMP Impact Level:** Moderate (baseline)
**Authorization Boundary:** Excalibur framework (NuGet packages)
**Implementation Approach:** Secure-by-default framework capabilities

---

## Control Status

**The control status table lives in [the FedRAMP checklist](../checklists/fedramp.md), which is the single
place this project asserts control status.** It carries the same fourteen controls, each with the consumer
action it requires and the evidence supporting it.

This page previously repeated that table without the evidence column. A control marked satisfied with no
evidence behind it is not something we can ask an assessor to accept, and two documents asserting the same
controls to different standards is a way for one of them to drift unnoticed. The claims now have one owner;
this page is orientation and supporting detail, and the sections below are among the evidence the checklist
points at.

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
- Append-only writes. **Records are not made immutable by the framework** — store them where the
  application cannot update or delete them, and restrict that access yourself
- Tamper *evidence* via a cryptographic hash chain: modification after the fact is detectable, not prevented

**Access Control (AC-3, AC-6):**
- Declarative authorization (`[RequirePermission]`)
- Role-based access control (RBAC)
- Least privilege enforcement

### Development Process (SA-15)

**CI/CD Pipeline:**
- Multi-platform builds (Ubuntu, Windows, macOS)
- Unit, integration, and functional tests
- Requirements traceability validation (RTM)
- Security scanning (SAST via CodeQL, secrets via Gitleaks, dependency vulnerabilities). No DAST and no container scanning run in this pipeline.
- SBOM generation (CycloneDX)
- Code coverage enforcement (44% regression floor; CI fails below it)
- Dependency vulnerability scanning

**Quality Gates:**
- Serialization policy validation
- Dead code detection
- API compatibility checks
- Transitive dependency bloat detection
- Banned APIs scanning
- Secrets scanning (Gitleaks)

### Component Inventory (CM-8)

**SBOM Generation:**
- Automated CycloneDX SBOM (JSON, spec version 1.7) covering the framework's own packages
- 90-day artifact retention
- Package-level granularity with dependency graph
- One validation check: the job fails if no SBOM file was produced. Schema validity, per-package
  coverage and license completeness are **not** asserted — validate the artifact yourself

See [CM-8-SBOM.md](./CM-8-SBOM.md) for detailed implementation.

---

## Evidence Package

### Primary Evidence

**Control Implementation:**
- Framework source, in the [framework repository](https://github.com/TrigintaFaces/Excalibur)

**Process Evidence:**
- CI/CD pipeline — GitHub Actions, in the [framework repository](https://github.com/TrigintaFaces/Excalibur)
- GitHub Actions workflow runs (audit trail)
- Test coverage reports (enforced regression floor of 44%)
- Security scan reports (CodeQL SAST, Gitleaks secrets, dependency scanning)

**Artifact Evidence:**
- SBOM artifact (CycloneDX JSON — the pipeline generates JSON only; no XML BOM is produced)
- NuGet packages (hash-verifiable; published with **no author signature** — do not inherit an author-signing control)
- Docker images (**not scanned by this pipeline** — no container scanning runs here)
- RTM reports (requirements traceability)

### Evidence Generation

Evidence package generation includes:
- Certification readiness checklists (FedRAMP, GDPR, SOC 2, HIPAA)
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
gh run download <run-id> -n codeql-results
# No DAST and no container-scan artifacts are produced by this pipeline,
# so there is no zap-dast-report or container-scan artifact to download.
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

A consumer inherits a **mechanism**, and for most of these controls a mechanism is not the control.
Only the four marked satisfied in the checklist are inherited whole; for the rest the checklist's
**Consumer Action** column states what you must still do, and your SSP must describe both halves.

- **AC-3, AC-6:** framework authorization (`[RequirePermission]`, RBAC) — satisfied
- **AU-3:** the audit record schema — satisfied
- **SC-13:** `IEncryptionProvider` (AES-256-GCM) — satisfied
- **AU-2, AU-9, IA-5, SC-8, SC-28, SI-4, SI-7, SA-15, CM-8, PM-11:** partial. The framework supplies
  `IAuditLogger` and its event taxonomy, the hash chain, Argon2id password hashing, TLS-required
  transports, `[PersonalData]` field encryption in the stores that honour it, OpenTelemetry signals,
  dependency scanning and an SBOM of its own packages — event selection, storage immutability,
  credential issuance and MFA, host TLS, everything outside those stores, detection and alerting,
  your own development process, the rest of your component inventory, and your mission and business
  processes all remain yours

**Example Inheritance Statement:**
> "The application inherits SC-13 (Cryptographic Protection) from the Excalibur framework, which implements AES-256-GCM encryption via the `IEncryptionProvider` abstraction. Framework compliance evidence includes AES-256-GCM through the platform's cryptographic provider (FIPS 140-3 validation is a property of the host platform's cryptographic module, not of the framework — assert it only where your deployment runs a validated module in FIPS mode) and continuous vulnerability scanning."

---

## Contact

**Questions:**
- Product Manager: Requirements clarification, control scope
- Software Architect: Technical implementation, architecture decisions
- Project Manager: Evidence packages, audit coordination

**Escalation:**
- Security incidents: follow your organisation's incident response plan. The framework records
  security-relevant events through the audit trail; it does not detect, triage, or escalate incidents.
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
- GDPR: See the [GDPR checklist](../checklists/gdpr.md)
- SOC 2: See the [SOC 2 checklist](../checklists/soc2.md) and the [security guides](../../security/index.md)
- HIPAA: See the [HIPAA checklist](../checklists/hipaa.md)

---

## See Also

- [CM-8 SBOM Implementation](./CM-8-SBOM.md) - Software component inventory details
- [Compliance Checklists](../checklists/fedramp.md) - FedRAMP checklist
- [Security](../../security/index.md) - Security implementation guides

---

**Last Updated:** 2026-09-22
**Status:** asserted only in the [FedRAMP checklist](../checklists/fedramp.md#control-mapping-table)
