# Compliance Documentation Navigation Guide

**Framework:** Excalibur.Dispatch
**Purpose:** Visual navigation for compliance documentation
**Last Updated:** 2026-01-01

---

## Overview

This guide provides visual diagrams to help you navigate the compliance documentation and understand the implementation workflow.

**Diagrams:**
1. [User Journey - Choosing Your Path](#1-user-journey---choosing-your-path)
2. [Documentation Structure](#2-documentation-structure)
3. [Implementation Workflow](#3-implementation-workflow)
4. [Evidence Collection Pipeline](#4-evidence-collection-pipeline)
5. [Certification Timeline](#5-certification-timeline)

---

## 1. User Journey - Choosing Your Path

```mermaid
flowchart TD
    Start([I need compliance]) --> Question1{Selling to<br/>US federal<br/>government?}

    Question1 -->|Yes| FedRAMP[FedRAMP Required]
    Question1 -->|No| Question2{Processing<br/>EU resident<br/>data?}

    Question2 -->|Yes| GDPR[GDPR Required]
    Question2 -->|No| Question3{Healthcare<br/>data PHI?}

    Question3 -->|Yes| HIPAA[HIPAA Required]
    Question3 -->|No| Question4{B2B SaaS/<br/>Cloud provider?}

    Question4 -->|Yes| SOC2[SOC 2 Recommended]
    Question4 -->|No| Optional[Consider SOC 2<br/>for trust]

    FedRAMP --> QuickStart
    GDPR --> QuickStart
    HIPAA --> QuickStart
    SOC2 --> QuickStart
    Optional --> QuickStart

    QuickStart[Read Quick Start Guide<br/>quick-start.md] --> Install[Install Packages<br/>Configure Baseline]

    Install --> ChooseChecklist{Which<br/>framework?}

    ChooseChecklist -->|FedRAMP| FedRAMPChecklist[checklists/fedramp.md<br/>9-week plan]
    ChooseChecklist -->|GDPR| GDPRChecklist[checklists/gdpr.md<br/>7-week plan]
    ChooseChecklist -->|SOC 2| SOC2Checklist[checklists/soc2.md<br/>7-week plan]
    ChooseChecklist -->|HIPAA| HIPAAChecklist[checklists/hipaa.md<br/>12-week plan]

    FedRAMPChecklist --> Evidence
    GDPRChecklist --> Evidence
    SOC2Checklist --> Evidence
    HIPAAChecklist --> Evidence

    Evidence[Collect Evidence<br/>eng/compliance/] --> Audit[External Audit]
    Audit --> Cert([Certification])

    style FedRAMP fill:#f9f,stroke:#333,stroke-width:2px
    style GDPR fill:#ff9,stroke:#333,stroke-width:2px
    style HIPAA fill:#9ff,stroke:#333,stroke-width:2px
    style SOC2 fill:#9f9,stroke:#333,stroke-width:2px
    style Cert fill:#6f6,stroke:#333,stroke-width:3px
```

---

## 2. Documentation Structure

```mermaid
graph TD
    README[index.md<br/>📋 Main Index] --> QuickStart[quick-start.md<br/>🚀 30-min Guide]
    README --> Checklists[checklists/<br/>📝 4 Frameworks]
    README --> Scripts[../../eng/compliance/<br/>🔧 Automation]
    README --> FedRAMPDocs[fedramp/<br/>📄 Detailed Docs]

    Checklists --> FedRAMP[fedramp.md<br/>FedRAMP Moderate]
    Checklists --> GDPR[gdpr.md<br/>GDPR Articles]
    Checklists --> SOC2[soc2.md<br/>Trust Services]
    Checklists --> HIPAA[hipaa.md<br/>Security + Privacy]

    Scripts --> CollectPS[collect-evidence.ps1<br/>Windows]
    Scripts --> CollectSH[collect-evidence.sh<br/>Linux/macOS]
    Scripts --> Package[generate-evidence-package.ps1<br/>ZIP creator]
    Scripts --> Audit[export-audit-samples.sh<br/>Audit logs]
    Scripts --> RoPA[generate-ropa-template.sh<br/>GDPR RoPA]

    FedRAMPDocs --> FedREADME[index.md<br/>14/14 Controls]
    FedRAMPDocs --> SBOM[CM-8-SBOM.md<br/>Component Inventory]

    FedRAMP -.->|References| FedREADME
    FedRAMP -.->|References| SBOM

    QuickStart -.->|Next step| FedRAMP
    QuickStart -.->|Next step| GDPR
    QuickStart -.->|Next step| SOC2
    QuickStart -.->|Next step| HIPAA

    style README fill:#6cf,stroke:#333,stroke-width:3px
    style QuickStart fill:#fc6,stroke:#333,stroke-width:2px
    style Checklists fill:#9f9,stroke:#333,stroke-width:2px
```

---

## 3. Implementation Workflow

```mermaid
flowchart LR
    subgraph Phase1[Phase 1: Setup]
        Install[Install Packages<br/>dotnet add package] --> Configure[Configure Services<br/>Program.cs]
        Configure --> Annotate[Annotate Models<br/>PersonalData]
    end

    subgraph Phase2[Phase 2: Core Capabilities]
        Annotate --> AccessControl[Access Control<br/>RequirePermission]
        AccessControl --> Encryption[Encryption<br/>IEncryptionProvider]
        Encryption --> Audit[Audit Logging<br/>IAuditLogger]
    end

    subgraph Phase3[Phase 3: Framework-Specific]
        Audit --> Decision{Which<br/>framework?}
        Decision -->|FedRAMP| FedRAMPImpl[SBOM Generation<br/>Security Scanning]
        Decision -->|GDPR| GDPRImpl[Erasure Service<br/>Legal Holds<br/>Data Inventory]
        Decision -->|SOC 2| SOC2Impl[Automated Validators<br/>Continuous Monitoring]
        Decision -->|HIPAA| HIPAAImpl[PHI Protection<br/>BAAs]
    end

    subgraph Phase4[Phase 4: Verification]
        FedRAMPImpl --> Tests
        GDPRImpl --> Tests
        SOC2Impl --> Tests
        HIPAAImpl --> Tests
        Tests[Run Conformance Tests<br/>80 tests] --> ManualTest[Manual Verification<br/>Database inspection]
    end

    subgraph Phase5[Phase 5: Certification]
        ManualTest --> CollectEv[Collect Evidence<br/>CI/CD artifacts]
        CollectEv --> Policies[Develop Policies<br/>Training]
        Policies --> Engage[Engage Auditor<br/>3PAO/CPA]
        Engage --> Remediate[Remediate Findings]
        Remediate --> Certify([Obtain Certification])
    end

    style Phase1 fill:#e1f5ff
    style Phase2 fill:#fff4e1
    style Phase3 fill:#e1ffe7
    style Phase4 fill:#ffe1e1
    style Phase5 fill:#f4e1ff
    style Certify fill:#6f6,stroke:#333,stroke-width:3px
```

---

## 4. Evidence Collection Pipeline

```mermaid
flowchart TD
    subgraph CI[CI/CD Pipeline<br/>.github/workflows/]
        Build[Build & Test<br/>dotnet build, test] --> SAST[Security Scanning<br/>CodeQL, Gitleaks]
        SAST --> DAST[DAST<br/>OWASP ZAP]
        DAST --> Container[Container Scan<br/>Trivy]
        Container --> SBOM[SBOM Generation<br/>CycloneDX]
    end

    subgraph Artifacts[GitHub Actions Artifacts<br/>90-day retention]
        SBOM --> ArtTest[Test Results<br/>JUnit XML, Coverage]
        ArtTest --> ArtSec[Security Reports<br/>SARIF, JSON]
        ArtSec --> ArtSBOM[SBOM Files<br/>JSON, XML]
    end

    subgraph Collection[Evidence Collection<br/>eng/compliance/]
        ArtTest --> Script1
        ArtSec --> Script1
        ArtSBOM --> Script1
        Script1[collect-evidence<br/>.ps1 or .sh] --> Download[Download Artifacts<br/>via gh CLI]
        Download --> Organize[Organize by Type<br/>test-results/, security-scans/]
    end

    subgraph Output[Evidence Package]
        Organize --> Manifest[MANIFEST.json<br/>metadata]
        Manifest --> README2[index.md<br/>instructions]
        README2 --> Package[ZIP Archive<br/>compliance-evidence-vX.zip]
    end

    subgraph Audit[Audit Preparation]
        Package --> Auditor[Provide to Auditor<br/>3PAO/CPA/DPO]
        Auditor --> Review[Review Evidence]
        Review --> Questions[Answer Questions]
        Questions --> Final[Final Report]
    end

    style CI fill:#e1f5ff
    style Artifacts fill:#fff4e1
    style Collection fill:#e1ffe7
    style Output fill:#ffe1e1
    style Audit fill:#f4e1ff
    style Final fill:#6f6,stroke:#333,stroke-width:3px
```

---

## 5. Certification Timeline

### FedRAMP Timeline (6-12 months)

```mermaid
gantt
    title FedRAMP Moderate Certification
    dateFormat YYYY-MM-DD
    section Preparation
    Risk Assessment           :2025-01-01, 60d
    SSP Development           :2025-03-01, 60d
    section Implementation
    Install Framework         :2025-05-01, 14d
    Configure Controls        :2025-05-15, 63d
    Evidence Collection       :2025-07-01, 30d
    section Assessment
    3PAO Engagement           :2025-08-01, 90d
    Remediation               :2025-11-01, 30d
    section Authorization
    PMO Review                :2025-12-01, 90d
    ATO Issuance              :milestone, 2026-03-01, 1d
```

### GDPR Timeline (3-6 months)

```mermaid
gantt
    title GDPR Compliance
    dateFormat YYYY-MM-DD
    section Preparation
    Scope Assessment          :2025-01-01, 30d
    Risk Assessment           :2025-02-01, 30d
    section Implementation
    Install Framework         :2025-03-01, 14d
    Configure Erasure         :2025-03-15, 21d
    Configure RoPA            :2025-04-05, 14d
    Policy Development        :2025-04-19, 30d
    section Verification
    Conformance Testing       :2025-05-19, 14d
    Training                  :2025-06-02, 30d
    section Audit
    External Audit (Optional) :2025-07-02, 30d
    Certification             :milestone, 2025-08-01, 1d
```

### SOC 2 Type I Timeline (3-6 months)

```mermaid
gantt
    title SOC 2 Type I Certification
    dateFormat YYYY-MM-DD
    section Preparation
    Scope Definition          :2025-01-01, 30d
    section Implementation
    Install Framework         :2025-02-01, 14d
    Configure Controls        :2025-02-15, 60d
    Automated Validators      :2025-04-16, 14d
    Readiness Assessment      :2025-04-30, 30d
    section Audit
    CPA Engagement            :2025-05-30, 60d
    Type I Report             :milestone, 2025-07-29, 1d
```

### SOC 2 Type II Timeline (12-18 months)

```mermaid
gantt
    title SOC 2 Type II Certification
    dateFormat YYYY-MM-DD
    section Type I
    Type I Certification      :2025-01-01, 180d
    section Observation Period
    6-Month Operation         :2025-07-01, 180d
    12-Month Operation        :2025-07-01, 365d
    section Type II Audit
    CPA Engagement            :2025-12-28, 60d
    Type II Report            :milestone, 2026-02-26, 1d
```

### HIPAA Timeline (6-12 months)

```mermaid
gantt
    title HIPAA Compliance
    dateFormat YYYY-MM-DD
    section Preparation
    Engage Specialist         :2025-01-01, 14d
    Risk Assessment           :2025-01-15, 60d
    section Policy Development
    Security Policies         :2025-03-16, 60d
    Privacy Policies          :2025-03-16, 60d
    section Implementation
    Install Framework         :2025-05-15, 14d
    Configure Tech Safeguards :2025-05-29, 60d
    Workforce Training        :2025-07-28, 60d
    section Verification
    Internal Audit            :2025-09-26, 60d
    External Audit            :2025-11-25, 60d
    Certification             :milestone, 2026-01-24, 1d
```

---

## Quick Reference Table

| **Step** | **FedRAMP** | **GDPR** | **SOC 2 Type I** | **SOC 2 Type II** | **HIPAA** |
|----------|-------------|----------|------------------|-------------------|-----------|
| **1. Preparation** | Risk assessment, scope | DPIA, scope | Scope definition | Type I complete | Engage specialist |
| **2. Install** | 2 weeks | 2 weeks | 2 weeks | N/A | 2 weeks |
| **3. Implement** | 9 weeks (14 controls) | 7 weeks (Articles 17, 30, 32) | 8 weeks (Security + optional) | N/A | 12 weeks (§164.312) |
| **4. Policies** | SSP, SAR | Privacy policy, RoPA | System description | N/A | Security + Privacy policies |
| **5. Tests** | 80 conformance tests | 80 conformance tests | Automated validators | N/A | 80 conformance tests |
| **6. Evidence** | SBOM, scans, audit logs | Erasure certs, RoPA | Reports, logs | Continuous | Audit logs, BAAs |
| **7. Audit** | 3PAO (3 months) | External DPO (1 month) | CPA (2 months) | CPA (2 months) | External (2 months) |
| **8. Timeline** | **6-12 months** | **3-6 months** | **3-6 months** | **12-18 months** | **6-12 months** |

---

## Navigation Shortcuts

### By Role

**Developers:**
1. Start: [Quick Start Guide](quick-start.md)
2. Reference: [Framework Capabilities](index.md#framework-capabilities)
3. Code Examples: Individual checklists (fedramp.md, gdpr.md, soc2.md, hipaa.md)

**Compliance Officers:**
1. Start: [Compliance Checklists](index.md#compliance-checklists)
2. Evidence: [Evidence Automation](index.md#evidence-automation)
3. Timeline: [Certification Roadmap](#5-certification-timeline)

**Auditors:**
1. Evidence: `eng/compliance/collect-evidence.*`
2. Conformance: 80 tests (Audit, Erasure, LegalHold, DataInventory)
3. Reports: Type I/II generation (SOC 2), SBOM artifacts (FedRAMP)

**Management:**
1. Overview: [index.md](index.md)
2. Timeline: [Certification Roadmap](index.md#certification-roadmap)
3. Costs: External audit fees + training + policies

### By Task

**"I need to get started"**
→ [Quick Start Guide](quick-start.md)

**"I need FedRAMP certification"**
→ [checklists/fedramp.md](checklists/fedramp.md)

**"I need GDPR compliance"**
→ [checklists/gdpr.md](checklists/gdpr.md)

**"I need SOC 2 certification"**
→ [checklists/soc2.md](checklists/soc2.md)

**"I need HIPAA compliance"**
→ [checklists/hipaa.md](checklists/hipaa.md)

**"I need to collect evidence"**
→ [Evidence Automation](index.md#evidence-automation)
→ `eng/compliance/collect-evidence.*`

**"I need to understand what the framework provides"**
→ [Framework Capabilities](index.md#framework-capabilities)

**"I need help choosing"**
→ [User Journey Diagram](#1-user-journey---choosing-your-path)

**"I need to know the timeline"**
→ [Certification Timeline](#5-certification-timeline)

---

## Related Documentation

**Within Compliance:**
- [index.md](index.md) - Main compliance index
- [quick-start.md](quick-start.md) - 30-minute getting started
- [checklists/](checklists/) - 4 detailed certification checklists
- [fedramp/](fedramp/) - FedRAMP-specific documentation

**Framework Documentation:**
- `../security/` - Security implementation guides
- `../advanced/` - Advanced topics (deployment, testing, performance)
- `../../eng/compliance/` - Evidence automation scripts

**External Standards:**
- [FedRAMP Program](https://www.fedramp.gov/)
- [NIST SP 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [GDPR Official Text](https://eur-lex.europa.eu/eli/reg/2016/679/oj)
- [AICPA Trust Services Criteria](https://www.aicpa.org/resources/download/trust-services-criteria)
- [HHS HIPAA](https://www.hhs.gov/hipaa/index.html)

---

**Last Updated:** 2026-01-01
**Next Review:** 2026-04-01
**Framework:** Excalibur 1.0.0

## See Also

- [Compliance Overview](./index.md) — Main compliance documentation index with framework capabilities and evidence automation
- [Quick Start Guide](./quick-start.md) — Get baseline compliance capabilities running in 30 minutes
- [Audit Logging](./audit-logging.md) — Configure and use audit logging for compliance evidence collection
