# Compliance Quick Start Guide

**Framework:** Excalibur.Dispatch
**Audience:** First-time users implementing compliance features
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Security
  ```
- Familiarity with [security overview](../security/index.md) and [audit logging](./audit-logging.md)

## Overview

This guide walks you through implementing baseline compliance capabilities using the Excalibur framework in under an hour. You'll:

1. Choose your compliance framework(s)
2. Install required packages
3. Configure core security capabilities
4. Verify installation with conformance tests
5. Understand next steps for full certification

**After this guide:** You'll have working encryption, audit logging, and access control - the foundation for FedRAMP, GDPR, SOC 2, or HIPAA compliance. For full certification, follow the detailed checklists.

---

## Prerequisites

**Required:**
- .NET 9.0 SDK or later
- Visual Studio 2022, VS Code, or Rider
- Basic C# knowledge
- NuGet package management

**Optional but Recommended:**
- Azure Key Vault or AWS KMS account (for production encryption)
- SQL Server or Postgres (for audit log persistence)
- GitHub account (for evidence collection from CI/CD)

---

## Step 1: Choose Your Framework

### Decision Tree

**Question 1: What data do you process?**

```
Are you selling to US federal government?
├─ YES → FedRAMP (Required for government contracts)
└─ NO  → Continue

Do you process EU resident data?
├─ YES → GDPR (Legally required for EU data)
└─ NO  → Continue

Do you handle healthcare data (PHI)?
├─ YES → HIPAA (Legally required for healthcare)
└─ NO  → Continue

Are you a B2B SaaS/cloud provider?
├─ YES → SOC 2 (Customer requirement)
└─ NO  → Consider SOC 2 for trust/credibility
```

### Framework Comparison

| Framework | Primary Audience | Mandatory? | Key Benefit | Certification Time |
|-----------|------------------|------------|-------------|-------------------|
| **FedRAMP** | Government contractors | Yes (for fed contracts) | Access to federal market | 6-12 months |
| **GDPR** | EU data processors | Yes (EU residents) | Legal compliance, avoid fines | 3-6 months |
| **SOC 2** | B2B SaaS providers | No (but expected) | Customer trust, RFP requirement | 3-18 months |
| **HIPAA** | Healthcare sector | Yes (PHI processing) | Legal compliance, patient trust | 6-12 months |

**Can you pursue multiple frameworks?** Yes! The framework capabilities overlap significantly:
- FedRAMP + SOC 2: Common for government SaaS
- GDPR + SOC 2: Common for EU SaaS
- HIPAA + SOC 2: Common for healthcare SaaS

**Recommendation:** Start with GDPR or SOC 2 as a baseline, then add others as needed.

---

## Step 2: Install Packages

### Core Packages

All frameworks require the core security packages:

```bash
# Core domain capabilities (required for all)
dotnet add package Excalibur.Domain

# Compliance features (GDPR erasure, SOC 2 validation)
dotnet add package Excalibur.Dispatch.Compliance

# Event sourcing (optional, for audit trail + domain events)
dotnet add package Excalibur.EventSourcing
```

### Framework-Specific Packages

**For Production (SQL Server persistence):**
```bash
# Audit log persistence
dotnet add package Excalibur.Dispatch.AuditLogging.SqlServer

# Erasure requests, legal holds persistence
dotnet add package Excalibur.Compliance.SqlServer

# Event sourcing + outbox (optional)
dotnet add package Excalibur.EventSourcing.SqlServer
```

**For Audit Logging:**
```bash
# Core audit logging (includes in-memory store)
dotnet add package Excalibur.Dispatch.AuditLogging
```

**For Testing:**
```bash
# Conformance test kits
dotnet add package Excalibur.Testing
```

---

## Step 3: Configure Core Capabilities

### 3.1 Access Control (All Frameworks)

**Required for:** FedRAMP (AC-3, AC-6), GDPR (Art 32), SOC 2 (CC5), HIPAA (§164.312(a))

**Configuration:**

```csharp
// Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Add ASP.NET Core authorization with policy-based access control
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ViewPatients", policy =>
        policy.RequireClaim("permission", "patients.read"));

    options.AddPolicy("EditPatients", policy =>
        policy.RequireClaim("permission", "patients.write"));
});
```

:::note Access Control
The `[RequirePermission]` attribute is provided by `Excalibur.A3.Authorization`, a separate authorization package. For basic access control, use ASP.NET Core's built-in `[Authorize]` policies as shown above.
:::

### 3.2 Encryption (FedRAMP, GDPR, SOC 2, HIPAA)

**Required for:** FedRAMP (SC-28), GDPR (Art 25, 32), SOC 2 (C2), HIPAA (§164.312(a)(2)(iv))

**Development Configuration (In-Memory):**

```csharp
// Development only — keys are NOT persisted!
builder.Services.AddDevEncryption();
```

**Production Configuration (Fluent Builder):**

```csharp
builder.Services.AddEncryption(encryption => encryption
    .UseKeyManagement<AesGcmEncryptionProvider>("aes-gcm-primary")
    .ConfigureOptions(options => options.DefaultPurpose = "field-encryption"));
```

:::note Key Management
The `AddEncryption()` API uses a fluent builder pattern. Call `.UseKeyManagement<TProvider>(name)` to register your provider, then `.ConfigureOptions()` for settings. For production, implement `IEncryptionProvider` with your KMS (Azure Key Vault, AWS KMS, etc.).
:::

**Usage (Data Classification Attributes):**

```csharp
using Excalibur.Dispatch.Compliance;

public class Patient
{
    public Guid Id { get; set; }

    [PersonalData]  // Marks field as personal data
    public string FirstName { get; set; }

    [PersonalData]
    public string LastName { get; set; }

    [PersonalData]
    [Sensitive]  // Extra protection flag
    public string SSN { get; set; }

    public DateTime DateOfBirth { get; set; }  // NOT marked
}
```

### 3.3 Audit Logging (All Frameworks)

**Required for:** FedRAMP (AU-2, AU-3, AU-9), GDPR (Art 32), SOC 2 (CC4), HIPAA (§164.312(b))

**Development Configuration (In-Memory):**

```csharp
// Package: Excalibur.Dispatch.AuditLogging
// Default in-memory store for development/testing
builder.Services.AddAuditLogging();
```

**Production Configuration (SQL Server):**

```csharp
// Package: Excalibur.Dispatch.AuditLogging.SqlServer
builder.Services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance")!;
    options.SchemaName = "compliance";
    options.EnableHashChain = true;
});
```

**Usage:**

```csharp
using Excalibur.Dispatch.Compliance;

public class PatientService
{
    private readonly IAuditStore _auditStore;

    public async Task<Patient> GetPatientAsync(Guid patientId, CancellationToken ct)
    {
        var patient = await _repository.GetAsync(patientId, ct);

        // Audit access
        await _auditStore.StoreAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.DataAccess,
            Action = "Patient.Read",
            ActorId = _currentUser.Id,
            ResourceId = patientId.ToString(),
            ResourceType = "Patient",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow
        }, ct);

        return patient;
    }
}
```

### 3.4 GDPR Erasure (GDPR + Optional for Others)

**Required for:** GDPR (Art 17)
**Recommended for:** SOC 2 (C3), HIPAA (Secure disposal)

**Configuration:**

```csharp
using Excalibur.Dispatch.Compliance;

builder.Services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod = TimeSpan.FromHours(72);
    options.RequireVerification = true;
});

// Development (in-memory stores)
builder.Services.AddInMemoryErasureStore();
builder.Services.AddInMemoryLegalHoldStore();
builder.Services.AddInMemoryDataInventoryStore();

// Supporting services
builder.Services.AddLegalHoldService();
builder.Services.AddDataInventoryService();
builder.Services.AddErasureVerificationService();
builder.Services.AddErasureScheduler();
```

**Usage:**

```csharp
using Excalibur.Dispatch.Compliance;

public class UserController : ControllerBase
{
    private readonly IErasureService _erasureService;

    [HttpPost("erasure-requests")]
    public async Task<IActionResult> RequestErasure(
        [FromBody] ErasureRequestDto dto,
        CancellationToken ct)
    {
        var result = await _erasureService.RequestErasureAsync(new ErasureRequest
        {
            DataSubjectId = dto.UserId,
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            RequestedBy = User.Identity?.Name ?? "anonymous"
        }, ct);

        return Ok(new { result.RequestId, result.Status, result.ScheduledExecutionTime });
    }
}
```

### 3.5 SOC 2 Validation (SOC 2 Only)

**Configuration:**

```csharp
using Excalibur.Dispatch.Compliance;

builder.Services.AddSoc2ComplianceWithBuiltInValidators(options =>
{
    options.EnabledCategories =
    [
        TrustServicesCategory.Security,
        TrustServicesCategory.Availability,
        TrustServicesCategory.Confidentiality
    ];
});

// Development
builder.Services.AddInMemorySoc2ReportStore();

// Optional: Continuous monitoring
builder.Services.AddSoc2ComplianceWithMonitoring(options =>
{
    options.MonitoringInterval = TimeSpan.FromHours(1);
    options.AlertThreshold = GapSeverity.Medium;
});
```

---

## Step 4: Verify Installation

### 4.1 Run Conformance Tests

The framework includes 80 conformance tests to verify your implementation:

```bash
# Run all GDPR conformance tests
dotnet test --filter "FullyQualifiedName~ErasureStoreConformance"
dotnet test --filter "FullyQualifiedName~LegalHoldStoreConformance"
dotnet test --filter "FullyQualifiedName~DataInventoryStoreConformance"
dotnet test --filter "FullyQualifiedName~AuditStoreConformance"

# Expected: All tests PASS
```

**Test Breakdown:**
- **AuditStoreConformanceTestKit:** 18 tests (audit logging)
- **ErasureStoreConformanceTestKit:** 24 tests (GDPR erasure)
- **LegalHoldStoreConformanceTestKit:** 19 tests (GDPR exceptions)
- **DataInventoryStoreConformanceTestKit:** 19 tests (RoPA)
- **Total:** 80 tests

### 4.2 Manual Verification

**Test Encryption:**
```csharp
// Create a patient with encrypted fields
var patient = new Patient
{
    Id = Guid.NewGuid(),
    FirstName = "John",
    LastName = "Doe",
    SSN = "123-45-6789"
};

await _repository.SaveAsync(patient);

// Inspect database - should see encrypted values
// SELECT FirstName FROM Patients WHERE Id = '...'
// Result: "AQIDBAUGBwgJCgsMDQ4PEA==" (Base64 ciphertext)
```

**Test Audit Logging:**
```csharp
// Access a patient
var patient = await _patientService.GetPatientAsync(patientId, ct);

// Query audit logs via IAuditStore
var events = await _auditStore.QueryAsync(new AuditQuery
{
    Action = "Patient.Read",
    StartDate = DateTimeOffset.UtcNow.AddMinutes(-5),
    EndDate = DateTimeOffset.UtcNow
}, ct);

// Verify: Event logged with correct ActorId, Timestamp, ResourceId
```

**Test Authorization:**
```csharp
// Try to access patient without permission
// Expected: UnauthorizedAccessException

// Grant permission, retry
// Expected: Success
```

---

## Step 5: Next Steps

### For Full Certification

Now that you have baseline compliance capabilities, follow the detailed checklists:

**FedRAMP (6-12 months):**
1. Follow [checklists/fedramp.md](checklists/fedramp.md)
2. Weeks 1-9: Implement all 14 NIST 800-53 controls
3. Week 10-12: Internal audit, penetration testing
4. Engage FedRAMP-approved 3PAO
5. Submit authorization package to PMO

**GDPR (3-6 months):**
1. Follow [checklists/gdpr.md](checklists/gdpr.md)
2. Weeks 1-7: Implement Articles 17, 17(3), 25, 30, 32
3. Week 8-10: Conformance testing, policy development
4. Conduct Data Protection Impact Assessment (DPIA)
5. Schedule external audit (optional but recommended)

**SOC 2 (3-18 months):**
1. Follow [checklists/soc2.md](checklists/soc2.md)
2. Weeks 1-7: Implement Security + optional categories
3. Week 8-9: Run automated validators, collect evidence
4. Schedule SOC 2 Type I audit (CPA firm)
5. Operate for 6-12 months, then SOC 2 Type II

**HIPAA (6-12 months):**
1. Follow [checklists/hipaa.md](checklists/hipaa.md)
2. Engage HIPAA compliance specialist (REQUIRED)
3. Weeks 1-7: Implement Technical Safeguards (§164.312)
4. Weeks 8-12: Develop policies, train workforce
5. Conduct Risk Assessment, schedule external audit

### Evidence Collection

**Automate Evidence Collection:**

Use the provided scripts to collect evidence from your CI/CD pipeline:

```bash
# Windows
.\eng\compliance\collect-evidence.ps1 -Frameworks "FedRAMP,SOC2"

# Linux/macOS
./eng/compliance/collect-evidence.sh -f GDPR,HIPAA
```

**Monthly Evidence Collection:**

Set up automated monthly evidence collection (see [Evidence Automation](index.md#evidence-automation) for GitHub Actions workflow).

### Training and Policies

**All Frameworks Require:**
- Security awareness training (annual)
- Privacy policy and notices
- Incident response procedures
- Business Associate Agreements (HIPAA, GDPR processors)
- Risk assessments (annual or when changes occur)

**Framework-Specific:**
- **FedRAMP:** System Security Plan (SSP), Security Assessment Report (SAR)
- **GDPR:** Records of Processing Activities (RoPA), Data Protection Impact Assessment (DPIA)
- **SOC 2:** System description, management assertion, control design documentation
- **HIPAA:** HIPAA Security Officer designation, Business Associate Agreements (BAAs)

---

## Common Pitfalls

### 1. Using In-Memory Stores in Production

**Problem:** In-memory stores (audit, erasure, legal hold) are lost on restart.

**Solution:** Always use SQL Server stores for production:
```csharp
// ❌ WRONG (Production) - uses in-memory storage
builder.Services.AddAuditLogging();

// ✅ CORRECT (Production)
builder.Services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Compliance")!;
    options.EnableHashChain = true;
});
```

### 2. Forgetting to Annotate Sensitive Fields

**Problem:** Personal data not encrypted because `[PersonalData]` attribute is missing.

**Solution:** Audit all domain models and annotate sensitive fields:
```csharp
// ❌ WRONG (No encryption)
public class Patient
{
    public string SSN { get; set; }  // Stored in plaintext!
}

// ✅ CORRECT (Encrypted at rest)
public class Patient
{
    [PersonalData]
    public string SSN { get; set; }
}
```

### 3. Not Running Conformance Tests

**Problem:** Implementation bugs discovered during external audit.

**Solution:** Run all 80 conformance tests before engaging auditor:
```bash
dotnet test --filter "FullyQualifiedName~Conformance"
```

### 4. Skipping Organizational Controls

**Problem:** Framework provides technical controls, but organizational policies are missing.

**Solution:** Remember the framework provides **technical capabilities**, you must provide:
- Policies and procedures
- Workforce training
- Risk assessments
- Business Associate Agreements
- Incident response plans

### 5. Not Engaging Compliance Specialists

**Problem:** Attempting full compliance without professional guidance.

**Solution:** For FedRAMP, GDPR, SOC 2, and HIPAA, **always engage a qualified compliance specialist or legal counsel**. The framework provides technical tools, but compliance is a holistic business process.

---

## Troubleshooting

### Encryption Not Working

**Symptom:** Data is not encrypted in database.

**Check:**
1. Is `[PersonalData]` attribute applied to field?
2. Is `IEncryptionProvider` registered in DI?
3. Is encryption middleware configured?
4. Check logs for encryption errors

### Audit Logs Not Persisting

**Symptom:** Audit events logged but not in database.

**Check:**
1. Is `IAuditStore` implementation registered?
2. Is connection string correct?
3. Did you run database migrations (`AutoMigrate = true`)?
4. Check SQL Server permissions (INSERT required)

### Conformance Tests Failing

**Symptom:** Some conformance tests fail.

**Check:**
1. Are you using correct store implementations (not in-memory for tests)?
2. Are all required services registered (e.g., `ILegalHoldService`)?
3. Check test output for specific error messages
4. Review conformance test documentation

---

## Resources

### Documentation

**This Repository:**
- [index.md](index.md) - Compliance documentation index
- [checklists/](checklists/) - Detailed certification checklists
- [../security/](../security/) - Security implementation guides
- [../advanced/](../advanced/) - Advanced topics

**External Standards:**
- [FedRAMP Program](https://www.fedramp.gov/)
- [NIST SP 800-53](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [GDPR Official Text](https://eur-lex.europa.eu/eli/reg/2016/679/oj)
- [AICPA Trust Services Criteria](https://www.aicpa.org/resources/download/trust-services-criteria)
- [HHS HIPAA](https://www.hhs.gov/hipaa/index.html)

### Support

**Questions:**
- Product Manager: Compliance scope, legal basis
- Software Architect: Technical implementation
- Project Manager: Evidence packages, audit coordination

**Issues:**
- GitHub Issues: `compliance` label
- Email: compliance@yourcompany.com (if applicable)

---

## Summary Checklist

Before proceeding to full certification, verify:

- [ ] Installed required NuGet packages
- [ ] Configured access control (ASP.NET Core authorization policies)
- [ ] Configured encryption (`AddEncryption()` or `AddDevEncryption()` + `[PersonalData]`)
- [ ] Configured audit logging (`IAuditStore` via `AddAuditLogging()` or `AddSqlServerAuditStore()`)
- [ ] Configured GDPR erasure (`IErasureService`) - if applicable
- [ ] Configured SOC 2 validation - if applicable
- [ ] Ran all 80 conformance tests (ALL PASS)
- [ ] Verified encryption in database (ciphertext visible)
- [ ] Verified audit logs persisting
- [ ] Verified authorization enforcement
- [ ] Reviewed detailed checklist for chosen framework(s)
- [ ] Engaged compliance specialist or legal counsel
- [ ] Documented system architecture and data flows
- [ ] Developed initial policies and procedures

**You're Ready!** Proceed to the detailed checklists for full certification.

---

**Last Updated:** 2026-02-09
**Next Review:** 2026-05-09
**Framework Version:** Excalibur 1.0.0

## See Also

- [Compliance Overview](./index.md) — Main compliance documentation index with framework capabilities and certification roadmap
- [Audit Logging](./audit-logging.md) — Detailed guide for configuring audit logging with SQL Server persistence
- [GDPR Erasure](./gdpr-erasure.md) — Implement right-to-erasure with legal holds and verification
- [Security Overview](../security/index.md) — Security implementation guides for encryption, access control, and key management
