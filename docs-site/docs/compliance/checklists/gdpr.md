# GDPR Certification Readiness Checklist

**Framework:** Excalibur.Dispatch
**Standard:** GDPR (General Data Protection Regulation)
**Implementation:** Cryptographic erasure + Records of Processing Activities (RoPA)
**Status:** Comprehensive compliance capabilities implemented
**Last Updated:** 2026-01-01

---

## Overview

This checklist provides step-by-step guidance for GDPR compliance using the Excalibur framework. The framework implements GDPR requirements through **cryptographic erasure** (Article 17 "Right to be Forgotten") and **Records of Processing Activities** (Article 30), plus comprehensive audit logging for accountability.

**GDPR Scope:** Personal data processing (EU residents)
**Compliance Approach:** Framework-provided capabilities + consumer configuration
**Key Innovation:** Cryptographic erasure - O(1) deletion regardless of data volume

---

## Control Mapping Table

| Article | Title | Framework Status | Consumer Action | Evidence Location |
|---------|-------|------------------|-----------------|-------------------|
| **Article 5** | Lawfulness, Fairness, Transparency | ⚠️ PARTIAL | Document legal basis, privacy policy | N/A (business process) |
| **Article 6** | Lawfulness of Processing | ⚠️ PARTIAL | Obtain consent/legal basis | N/A (business process) |
| **Article 13-14** | Information to Data Subjects | ⚠️ PARTIAL | Provide privacy notices | N/A (business process) |
| **Article 17** | Right to Erasure | ✅ SATISFIED | Inherit `IErasureService` | `docs/security/gdpr-compliance.md:120-243` |
| **Article 17(3)** | Erasure Exceptions | ✅ SATISFIED | Inherit `ILegalHoldService` | `docs/security/gdpr-compliance.md:245-340` |
| **Article 25** | Data Protection by Design | ✅ SATISFIED | Inherit `[PersonalData]` encryption | `docs/advanced/security.md:352-395` |
| **Article 30** | Records of Processing Activities | ✅ SATISFIED | Inherit `IDataInventoryService` | `docs/security/gdpr-compliance.md:342-397` |
| **Article 32** | Security of Processing | ✅ SATISFIED | Inherit encryption + audit | `docs/advanced/security.md` |
| **Article 33-34** | Breach Notification | ⚠️ PARTIAL | Implement incident response | N/A (business process) |

**Legend:**
- ✅ SATISFIED: Framework provides technical implementation
- ⚠️ PARTIAL: Framework provides tools, consumer configures business processes

---

## Implementation Checklist

### Phase 1: Prerequisites (Week 1)

#### 1.1 Understand GDPR Scope

- [ ] Identify personal data processed by your application
- [ ] Determine if you process EU resident data (territorial scope)
- [ ] Document data processing purposes
- [ ] Classify data by sensitivity (identity, contact, financial, health, etc.)

**Reference:** GDPR Articles 4(1), 3

#### 1.2 Install Framework Packages

- [ ] Install GDPR compliance packages

**Command:**
```bash
dotnet add package Excalibur.Compliance
dotnet add package Excalibur.Compliance.SqlServer  # Production
dotnet add package Excalibur.Domain  # For [PersonalData] attribute
```

#### 1.3 Review Framework Capabilities

- [ ] Read `docs/security/gdpr-compliance.md` (1,000+ lines)
- [ ] Understand cryptographic erasure approach
- [ ] Review conformance test kits, and note that their arms are opt-in — you wrap what you run

---

### Phase 2: Article 17 - Right to Erasure (Week 2)

**Control Requirement:**
Data subjects have the right to obtain erasure of personal data without undue delay (≤30 days).

**Framework Implementation:**
- `IErasureService` for erasure request processing
- Cryptographic erasure (key deletion = data irrecoverable)
- Grace period (default 72 hours) to prevent accidental deletion
- Erasure certificates for compliance proof

#### 2.1 Configure Erasure Service

**Development Setup:**

```csharp
using Excalibur.Compliance;

var builder = WebApplication.CreateBuilder(args);

// Add GDPR erasure services
builder.Services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod = TimeSpan.FromHours(72);
    options.RequireVerification = true;
    options.NotifyOnCompletion = true;
});

// Add in-memory stores for development
builder.Services.AddInMemoryErasureStore();
builder.Services.AddInMemoryLegalHoldStore();
builder.Services.AddInMemoryDataInventoryStore();

// Add supporting services
builder.Services.AddLegalHoldService();
builder.Services.AddDataInventoryService();
builder.Services.AddErasureVerificationService();

// Add background scheduler for automatic execution
builder.Services.AddErasureScheduler();
```

- [ ] Configure erasure service with appropriate grace period
- [ ] Add in-memory stores for development testing
- [ ] Add background scheduler for automatic execution

**Production Setup:**

```csharp
builder.Services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod = TimeSpan.FromHours(72);
    options.RequireVerification = true;
    options.CertificateRetentionPeriod = TimeSpan.FromDays(365 * 7); // 7 years
    options.SigningKeyId = "erasure-cert-signing-key";
});

// Use SQL Server for production persistence
builder.Services.AddSqlServerErasureStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Compliance");
    options.SchemaName = "compliance";
    options.AutoMigrate = true;
});

builder.Services.AddLegalHoldService();
builder.Services.AddDataInventoryService();
builder.Services.AddErasureVerificationService();
builder.Services.AddErasureScheduler();
```

- [ ] Configure SQL Server erasure store
- [ ] Set certificate retention period (7 years recommended)
- [ ] Configure signing key for certificate signatures

:::warning Shipped test evidence for the production compliance stores is uneven — verify against your own database

`✅ SATISFIED` in the table above means **the framework provides a technical implementation**. It does not
mean every provider of that implementation carries shipped test evidence, and for the compliance stores it
does not. Measured in this framework's own test suite:

| Store | Shipped test files |
|---|---|
| `SqlServerAuditStore` / `PostgresAuditStore` | 7 / 7 |
| `SqlServerErasureStore` / `PostgresErasureStore` | 2 / **0** |
| `SqlServerLegalHoldStore` / `PostgresLegalHoldStore` | **0** / **0** |
| `SqlServerDataInventoryStore` / `PostgresDataInventoryStore` | 1 / **0** |

The audit store is well covered. **The erasure, legal-hold and data-inventory SQL stores are not**, and the
conformance kits for those three are exercised against the in-memory store only — so neither route gives you
evidence about the provider you actually deploy.

This does not mean those stores are broken; it means **we have not demonstrated they are correct on your
provider, so you should not present our artifacts as if we had.** Before relying on Article 17, 17(3) or 30
behaviour in production, exercise it against your own database and keep that result as your evidence.

:::

#### 2.2 Implement Erasure API

**Create Erasure Endpoint:**

```csharp
public class ErasureController : ControllerBase
{
    private readonly IErasureService _erasureService;

    [HttpPost("erasure-requests")]
    public async Task<IActionResult> RequestErasure(
        [FromBody] ErasureRequestDto dto, CancellationToken ct)
    {
        var request = new ErasureRequest
        {
            DataSubjectId = dto.UserId,
            IdType = DataSubjectIdType.UserId,
            TenantId = dto.TenantId,
            Scope = ErasureScope.User,
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            RequestedBy = User.Identity?.Name ?? "anonymous"
        };

        var result = await _erasureService.RequestErasureAsync(request, ct);

        return Ok(new
        {
            RequestId = result.RequestId,
            Status = result.Status,
            ScheduledExecutionTime = result.ScheduledExecutionTime,
            Message = $"Erasure scheduled for {result.ScheduledExecutionTime:O}"
        });
    }

    [HttpGet("erasure-requests/{requestId}")]
    public async Task<IActionResult> GetStatus(Guid requestId, CancellationToken ct)
    {
        var status = await _erasureService.GetStatusAsync(requestId, ct);
        if (status is null) return NotFound();

        return Ok(new
        {
            RequestId = status.RequestId,
            Status = status.Status.ToString(),
            RequestedAt = status.RequestedAt,
            ScheduledExecutionAt = status.ScheduledExecutionAt,
            CompletedAt = status.CompletedAt,
            KeysDeleted = status.KeysDeleted,
            RecordsAffected = status.RecordsAffected
        });
    }

    [HttpPost("erasure-requests/{requestId}/cancel")]
    public async Task<IActionResult> CancelErasure(
        Guid requestId, [FromBody] CancelDto dto, CancellationToken ct)
    {
        try
        {
            var cancelled = await _erasureService.CancelErasureAsync(
                requestId,
                dto.Reason,
                User.Identity?.Name ?? "system",
                ct);

            if (!cancelled)
                return NotFound("Request not found or already executed");

            return Ok("Erasure request cancelled");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("erasure-requests/{requestId}/certificate")]
    public async Task<IActionResult> GetCertificate(Guid requestId, CancellationToken ct)
    {
        try
        {
            var certificate = await _erasureService.GenerateCertificateAsync(
                requestId, ct);

            return Ok(new
            {
                CertificateId = certificate.CertificateId,
                RequestId = certificate.RequestId,
                IssuedAt = certificate.IssuedAt,
                DataSubjectIdHash = certificate.DataSubjectIdHash,
                KeysDeleted = certificate.KeysDeleted,
                VerificationMethods = certificate.VerificationMethods,
                Signature = certificate.Signature
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message); // Request not completed
        }
    }
}
```

- [ ] Implement POST /erasure-requests endpoint
- [ ] Implement GET /erasure-requests/\{requestId\} for status checking
- [ ] Implement POST /erasure-requests/\{requestId\}/cancel for grace period cancellation
- [ ] Implement GET /erasure-requests/\{requestId\}/certificate for compliance proof

#### 2.3 Test Erasure Workflow

**Unit Test:**

```csharp
[Fact]
public async Task RequestErasure_WithValidRequest_SchedulesErasure()
{
    var services = new ServiceCollection();
    services.AddGdprErasure();
    services.AddInMemoryErasureStore();
    services.AddInMemoryLegalHoldStore();
    services.AddLegalHoldService();

    var provider = services.BuildServiceProvider();
    var erasureService = provider.GetRequiredService<IErasureService>();

    var result = await erasureService.RequestErasureAsync(new ErasureRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        LegalBasis = ErasureLegalBasis.DataSubjectRequest,
        RequestedBy = "test"
    }, CancellationToken.None);

    result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
    result.ScheduledExecutionTime.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
}
```

- [ ] Write unit tests for erasure request submission
- [ ] Test grace period cancellation
- [ ] Test certificate generation
- [ ] Wrap and run the `ErasureStoreConformanceTestKit` arms (24 available)

**Evidence:**
- `docs/security/gdpr-compliance.md:642-687` - Testing guide
- Conformance results from the arms you wrapped (`ErasureStoreConformanceTestKit` — in-memory store only; does not evidence SQL Server or PostgreSQL behaviour)

---

### Phase 3: Article 17(3) - Legal Hold Exceptions (Week 3)

**Control Requirement:**
Erasure rights do not apply when processing is necessary for legal compliance, legal claims, public interest, etc.

**Framework Implementation:**
- `ILegalHoldService` for managing erasure exceptions
- Automatic blocking of erasure when holds exist
- Legal hold bases aligned with Article 17(3)

#### 3.1 Implement Legal Hold Management

**Create Legal Hold Endpoint:**

```csharp
public class LegalHoldController : ControllerBase
{
    private readonly ILegalHoldService _legalHoldService;

    [HttpPost("legal-holds")]
    public async Task<IActionResult> CreateHold(
        [FromBody] LegalHoldDto dto, CancellationToken ct)
    {
        var request = new LegalHoldRequest
        {
            DataSubjectId = dto.UserId,
            IdType = DataSubjectIdType.UserId,
            TenantId = dto.TenantId,
            Basis = dto.Basis, // e.g., LegalHoldBasis.LitigationHold
            CaseReference = dto.CaseReference, // e.g., "CASE-2024-001"
            Description = dto.Description,
            ExpiresAt = dto.ExpiresAt, // null = indefinite
            CreatedBy = User.Identity?.Name ?? "legal-team"
        };

        var hold = await _legalHoldService.CreateHoldAsync(request, ct);

        return Ok(new
        {
            HoldId = hold.HoldId,
            Basis = hold.Basis.ToString(),
            CaseReference = hold.CaseReference,
            IsActive = hold.IsActive,
            CreatedAt = hold.CreatedAt
        });
    }

    [HttpPost("legal-holds/{holdId}/release")]
    public async Task<IActionResult> ReleaseHold(
        Guid holdId, [FromBody] ReleaseDto dto, CancellationToken ct)
    {
        await _legalHoldService.ReleaseHoldAsync(
            holdId,
            dto.Reason, // "Case settled"
            User.Identity?.Name ?? "legal-team",
            ct);

        return Ok("Legal hold released");
    }

    [HttpGet("legal-holds/active")]
    public async Task<IActionResult> ListActiveHolds(
        [FromQuery] string? tenantId, CancellationToken ct)
    {
        var holds = await _legalHoldService.ListActiveHoldsAsync(tenantId, ct);

        return Ok(holds.Select(h => new
        {
            h.HoldId,
            Basis = h.Basis.ToString(),
            h.CaseReference,
            h.Description,
            h.CreatedAt,
            h.ExpiresAt
        }));
    }
}
```

- [ ] Implement POST /legal-holds for creating holds
- [ ] Implement POST /legal-holds/\{holdId\}/release for releasing holds
- [ ] Implement GET /legal-holds/active for listing active holds
- [ ] Document legal hold procedures for legal team

#### 3.2 Legal Hold Bases

**Article 17(3) Exception Mapping:**

| Legal Basis | Article 17(3) Reference | Use Case |
|-------------|------------------------|----------|
| `LitigationHold` | (e) Legal claims | Pending lawsuit, active litigation |
| `RegulatoryInvestigation` | (b) Legal obligation | SEC, FTC, GDPR investigation |
| `LegalObligation` | (b) Legal obligation | Tax retention, employment law |
| `PublicInterest` | (d) Public interest | Medical research, public health |
| `LegalClaims` | (e) Legal claims | Contract disputes, warranty claims |

- [ ] Map your legal hold scenarios to Article 17(3) bases
- [ ] Document legal hold approval process
- [ ] Configure legal team access control

#### 3.3 Test Legal Hold Integration

**Integration Test:**

```csharp
[Fact]
public async Task RequestErasure_WithActiveLegalHold_BlocksErasure()
{
    // Arrange: Create legal hold
    await _legalHoldService.CreateHoldAsync(new LegalHoldRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        Basis = LegalHoldBasis.LitigationHold,
        CaseReference = "CASE-2024-001",
        Description = "Active litigation hold for case CASE-2024-001",
        CreatedBy = "legal-team"
    }, CancellationToken.None);

    // Act: Attempt erasure
    var result = await _erasureService.RequestErasureAsync(new ErasureRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        LegalBasis = ErasureLegalBasis.DataSubjectRequest,
        RequestedBy = "user"
    }, CancellationToken.None);

    // Assert: Erasure blocked
    result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
    result.BlockingHold.ShouldNotBeNull();
}
```

- [ ] Test erasure blocking with active holds
- [ ] Test hold release and subsequent erasure
- [ ] Test expired hold cleanup
- [ ] Wrap and run the `LegalHoldStoreConformanceTestKit` arms (19 available)

**Evidence:**
- Conformance test results (LegalHoldStoreConformanceTestKit)

---

### Phase 4: Article 30 - Records of Processing Activities (Week 4)

**Control Requirement:**
Maintain records of processing activities (RoPA) documenting personal data categories, purposes, storage locations, etc.

**Framework Implementation:**
- `IDataInventoryService` for tracking personal data locations
- `[PersonalData]` attribute for automatic discovery
- Manual registration for external systems

#### 4.1 Annotate Personal Data Fields

**Automatic Discovery:**

```csharp
using Excalibur.Compliance;

public class UserProfile
{
    public Guid Id { get; set; }

    [PersonalData(Category = PersonalDataCategory.Identity)]
    public string FirstName { get; set; }

    [PersonalData(Category = PersonalDataCategory.Identity)]
    public string LastName { get; set; }

    [PersonalData(Category = PersonalDataCategory.ContactInfo, IsSensitive = true)]
    public string Email { get; set; }

    [PersonalData(Category = PersonalDataCategory.ContactInfo)]
    public string PhoneNumber { get; set; }

    [PersonalData(Category = PersonalDataCategory.Financial, IsSensitive = true)]
    public string BankAccount { get; set; }
}
```

- [ ] Annotate all personal data fields with `[PersonalData]`
- [ ] Classify data by category (Identity, Contact, Financial, Health, etc.)
- [ ] Mark sensitive fields (email, SSN, health data, financial data)

#### 4.2 Register External Data Locations

:::danger Data-location registrations are not tenant-scoped

**In a multi-tenant deployment, do not treat the data inventory as tenant-isolated.** Two limitations apply to the SQL Server and PostgreSQL stores today.

**Registrations are keyed by table and field only.** The registration record's primary key is `(TableName, FieldName)`, which carries no tenant term, and `RegisterDataLocationAsync` takes no tenant argument. Registering `("CRM_Contacts", "PersonalInfo")` for one tenant therefore **overwrites** any registration another tenant holds for the same table and field — including its `DataCategory`, `KeyIdColumn`, and `Description`. The last writer wins silently; no error is raised.

**`TenantIdColumn` is a column *name*, not a tenant *value*.** It records which column in *your* table holds the tenant identifier. It does not associate the registration itself with a tenant, and it is not used to restrict which registrations a caller can read (see §4.3).

**What to do until this is resolved:**

- Use a **separate database or schema per tenant** for the compliance store if your registrations differ by tenant.
- Otherwise, treat the registration set as **estate-wide**: agree table/field registrations centrally, and do not let individual tenants register overlapping locations.
- Namespacing `TableName` per tenant works, but the resulting registrations are still readable by every tenant.

:::

**Manual Registration:**

```csharp
// Register external data locations not discoverable via attributes
await _dataInventoryService.RegisterDataLocationAsync(new DataLocationRegistration
{
    TableName = "CRM_Contacts",
    FieldName = "PersonalInfo",
    DataCategory = "Identity",
    DataSubjectIdColumn = "UserId",
    IdType = DataSubjectIdType.UserId,
    KeyIdColumn = "EncryptionKeyId",
    TenantIdColumn = "TenantId",
    Description = "External CRM contact records"
}, cancellationToken);
```

- [ ] Identify external systems storing personal data (CRM, analytics, etc.)
- [ ] Register external data locations
- [ ] Document encryption key IDs for external systems

#### 4.3 Generate RoPA Report

:::danger The `tenantId` argument does not restrict results

**A scoped read returns the whole estate's registrations, not the querying tenant's.** This affects `DiscoverAsync` and `GetDataMapAsync` on the SQL Server and PostgreSQL stores.

Supplying a `tenantId` does **not** filter to that tenant. The value is accepted and then never compared against anything; the query it produces selects every registration that merely *has* a tenant column configured, regardless of which tenant owns it. Passing `"tenant-a"` and passing `"tenant-b"` return **identical results**. Passing a tenant that owns nothing at all still returns other tenants' rows.

The in-memory store behaves differently again — it compares your `tenantId` against the configured column *name*, so it generally returns nothing rather than everything. **Do not use in-memory behaviour to predict SQL behaviour here.**

**Why this matters for Article 30:** a RoPA report generated per tenant from these calls will contain other controllers' processing records. If you export or hand that report to a tenant, an auditor, or a data subject, you are disclosing another tenant's data map.

**What to do until this is resolved:**

- **Do not generate per-tenant RoPA reports from a shared compliance store.** Use a separate store per tenant, or generate one estate-wide report and filter it yourself against your own record of tenant ownership.
- If you must filter in application code, do it on data *you* control — the framework's returned rows carry no trustworthy owner field to filter on.
- Single-tenant deployments are unaffected: there is only one tenant's data to return.

**Verification is not covered by the shipped conformance kit.** The data-inventory conformance suite runs against the in-memory store only; neither SQL store is exercised by it. A green conformance run therefore does **not** demonstrate tenant isolation in your deployment. Test scoping directly against your real database before relying on it.

:::

**Data Inventory Query:**

```csharp
// NOTE: on the SQL Server and PostgreSQL stores, tenantId does NOT restrict
// the result to that tenant -- see the warning above. In a multi-tenant
// deployment the returned locations may belong to other tenants.
var inventory = await _dataInventoryService.DiscoverAsync(
    userId,
    DataSubjectIdType.UserId,
    tenantId,
    cancellationToken);

// List discovered data locations
foreach (var location in inventory.Locations)
{
    Console.WriteLine($"  Table: {location.TableName}");
    Console.WriteLine($"  Field: {location.FieldName}");
    Console.WriteLine($"  Category: {location.DataCategory}");
    Console.WriteLine($"  Key ID: {location.KeyId}");
    Console.WriteLine($"  Auto-discovered: {location.IsAutoDiscovered}");
}

// Generate RoPA data map report
var dataMap = await _dataInventoryService.GetDataMapAsync(tenantId, cancellationToken);
foreach (var entry in dataMap.Entries)
{
    Console.WriteLine($"  Table: {entry.TableName}");
    Console.WriteLine($"  Field: {entry.FieldName}");
    Console.WriteLine($"  Category: {entry.DataCategory}");
    Console.WriteLine($"  Records: {entry.RecordCount}");
    Console.WriteLine($"  Auto-discovered: {entry.IsAutoDiscovered}");
}
```

- [ ] Query data inventory for sample users
- [ ] Verify all personal data locations are tracked
- [ ] Generate RoPA report (CSV or JSON format)
- [ ] Run conformance test kit
- [ ] **Multi-tenant deployments:** confirm your own tenant-isolation test against your real database — query as a tenant that owns no registrations and assert the result is empty. The shipped conformance kit does not cover this (see the warning above).

**Evidence:**
- `docs/security/gdpr-compliance.md:859-941` - Data inventory conformance
- RoPA report export
- Conformance test results (DataInventoryStoreConformanceTestKit — in-memory store only; does not evidence SQL Server or PostgreSQL behaviour)
- Multi-tenant deployments: your own cross-tenant isolation test result against the real database

---

### Phase 5: Article 25 - Data Protection by Design (Week 5)

**Control Requirement:**
Implement appropriate technical and organizational measures to ensure data protection by design and by default.

**Framework Implementation:**
- Field-level encryption via `[PersonalData]` attribute
- Encryption at rest (AES-256-GCM)
- Encryption in transit (TLS 1.2+)

#### 5.1 Configure Encryption

**Encryption Setup:**

```csharp
using Excalibur.Compliance;

services.AddEncryption(encryption => encryption
    .UseKeyManagement<AesGcmEncryptionProvider>("aes-gcm-primary")
    .ConfigureOptions(options => options.DefaultPurpose = "field-encryption"));
```

- [ ] Configure encryption provider (Azure Key Vault, AWS KMS, etc.)
- [ ] Verify `[PersonalData]` fields are automatically encrypted
- [ ] Test decryption with integration tests

**Database Verification:**

```sql
-- Verify encryption at rest
SELECT Email FROM UserProfiles WHERE UserId = '...';
-- Result: "AQIDBAUGBwgJCgsMDQ4PEA=="  (Base64-encoded ciphertext)
```

- [ ] Inspect database to verify encrypted values
- [ ] Test tamper detection (modify ciphertext, verify decryption fails)

**Evidence:**
- `docs/advanced/security.md:352-395` - Data at rest encryption guide
- Encryption verification tests

---

### Phase 6: Article 32 - Security of Processing (Week 6)

**Control Requirement:**
Implement appropriate technical and organizational measures to ensure a level of security appropriate to the risk.

**Framework Implementation:**
- Encryption (at rest + in transit)
- Audit logging (tamper-evident hash chain)
- Access control (RBAC with `[RequirePermission]`)

#### 6.1 Configure Audit Logging

**Audit Setup:**

```csharp
using Excalibur.Compliance;

public class UserService
{
    private readonly IAuditLogger _auditLogger;

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct)
    {
        // Perform deletion
        await _repository.DeleteAsync(userId, ct);

        // Audit the action
        await _auditLogger.LogAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.DataModification,
            Action = "User.Delete",
            ActorId = _currentUser.Id,
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ResourceId = userId.ToString(),
            ResourceType = "User"
        }, ct);
    }
}
```

- [ ] Inject `IAuditLogger` into services
- [ ] Audit all security-relevant events (access, modification, deletion)
- [ ] Implement `IAuditStore` persistence layer

**Evidence:**
- `docs/advanced/security.md:215-260` - Audit logging guide
- Audit log samples (anonymized)

#### 6.2 Configure Access Control

**Authorization Setup:**

```csharp
using Excalibur.A3.Authorization;

[RequirePermission("users.delete")]
public class DeleteUserCommand : IDispatchAction
{
    public Guid UserId { get; set; }
}
```

- [ ] Apply `[RequirePermission]` to protected operations
- [ ] Define role-to-permission mappings
- [ ] Test authorization enforcement

**Evidence:**
- `docs/advanced/security.md:15-78` - Authorization and RBAC guide

---

### Phase 7: Breach Notification (Week 7)

**Control Requirement:**
Notify supervisory authority and data subjects of personal data breaches within 72 hours.

**Consumer Responsibilities:**
- Implement incident detection and response procedures
- Configure breach notification workflows
- Document breach response plan

#### 7.1 Configure Incident Detection

- [ ] Set up security monitoring (SIEM integration)
- [ ] Configure alerting for suspicious activity
- [ ] Define breach severity levels

**Reference:** `docs/security/siem-integration.md`

#### 7.2 Document Breach Response Plan

- [ ] Define roles and responsibilities (incident commander, legal, PR)
- [ ] Create breach notification templates (supervisory authority + data subjects)
- [ ] Test breach response procedures (tabletop exercises)

**Template:**

```markdown
# GDPR Breach Notification Template

**Breach ID:** [AUTO-GENERATED]
**Detected:** [TIMESTAMP]
**Severity:** [LOW/MEDIUM/HIGH/CRITICAL]

## Breach Details
- Nature of breach: [Unauthorized access, data loss, ransomware, etc.]
- Personal data affected: [Categories and approximate number of data subjects]
- Root cause: [Technical failure, human error, malicious attack, etc.]

## Containment Actions
- [Action 1]: [Timestamp completed]
- [Action 2]: [Timestamp completed]

## Notification Timeline
- [ ] Supervisory authority notified (within 72 hours)
- [ ] Data subjects notified (if high risk)
- [ ] Documentation completed

## Remediation
- [Short-term fixes]
- [Long-term improvements]
```

---

## Consumer Responsibilities

**Framework Provides:**
- Technical implementation of erasure, legal holds, data inventory
- Conformance test kits (80 tests total)
- Encryption and audit logging capabilities

**Consumer Must Implement:**
- Privacy policies and notices (Articles 13-14)
- Legal basis for data processing (Article 6)
- Consent management (Article 7)
- Data protection impact assessments (DPIA) - Article 35
- Data processor agreements (Article 28)
- Breach notification procedures (Articles 33-34)
- Data protection officer appointment (Article 37-39)
- Subject access request (SAR) handling (Article 15)

---

## Compliance Verification

### Pre-Certification Testing

**Week 8: Conformance Testing**

- [ ] Run `ErasureStoreConformanceTestKit` — 24 arms *(shipped evidence: in-memory store only)*
- [ ] Run `LegalHoldStoreConformanceTestKit` — 19 arms *(shipped evidence: in-memory store only)*
- [ ] Run `DataInventoryStoreConformanceTestKit` — 20 arms (`protected`) *(shipped evidence: in-memory store only)*
- [ ] Run `AuditStoreConformanceTestKit` — 27 arms *(also exercised against SQL Server and PostgreSQL)*
- [ ] **Record your executed and passed counts in your own evidence pack**

:::warning The executed count is authored by you, not by this framework

**The conformance kits deliberately carry no test attributes.** Every arm is `virtual` — and on
`DataInventoryStoreConformanceTestKit`, `protected` — so **nothing is discovered or executed until you
declare an attributed wrapper in your own derived class.** Conformance is opt-in per arm.

This is intentional: it is what lets you run the suite against *your* store, with your fixtures and your
provider, rather than against ours. But it has a consequence for your evidence pack:

**Do not record a passed count you did not produce.** An arm you have not wrapped has not run, and a kit
you have merely referenced has asserted nothing. The counts above are the arms **available** to wrap in
each kit; the number that actually executed is whatever your own test run reports.

**And check *which store* the shipped evidence covers.** Of the four kits above, only
`AuditStoreConformanceTestKit` is exercised against real SQL Server and PostgreSQL in this framework's own
test suite. The Erasure, Legal Hold and Data Inventory kits are exercised **against the in-memory store
only** — so a green run here does not evidence the behaviour of the SQL stores this checklist tells you to
deploy for production. Run those arms against your real database before treating them as Article 17,
17(3) or 30 evidence.

The `--RunConfiguration.TreatNoTestsAsError=true` flag in the commands below is there for exactly this
reason — without it, a filter that matches nothing exits successfully and reads as a pass.

:::

**Commands:**

```bash
# Run all GDPR conformance tests
dotnet test --filter "FullyQualifiedName~ErasureStoreConformance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~LegalHoldStoreConformance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~DataInventoryStoreConformance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~AuditStoreConformance" -- RunConfiguration.TreatNoTestsAsError=true
```

> **A conformance run that executes nothing is not a pass.** These commands carry
> `RunConfiguration.TreatNoTestsAsError=true` deliberately: without it, `dotnet test --filter` **exits `0`
> when the filter matches nothing**, so if the conformance package is not referenced or a type has been
> renamed, the command prints `No test matches the given testcase filter` and **succeeds** — and this
> checklist item would be ticked on a run that verified nothing. With the setting, a filter matching no
> tests fails the command. **Confirm each run reports a non-zero `Total`;** an exit code alone is not
> evidence that a check ran.

**Week 9: Integration Testing**

- [ ] Test full erasure workflow (request → grace period → execution → certificate)
- [ ] Test legal hold blocking and release
- [ ] Test data inventory discovery and RoPA generation
- [ ] Test encryption at rest and in transit
- [ ] Test breach notification procedures (dry run)

**Week 10: Documentation Review**

- [ ] Review privacy policy and notices
- [ ] Review RoPA (Records of Processing Activities)
- [ ] Review legal basis documentation
- [ ] Review data protection impact assessment (DPIA)
- [ ] Review breach response plan

### External Audit Preparation

- [ ] Compile evidence package:
  - Conformance test results
  - Erasure certificates (sample)
  - RoPA export
  - Audit log samples
  - Encryption verification
  - Privacy policy and notices
- [ ] Schedule data protection audit with external auditor
- [ ] Address audit findings and remediate gaps
- [ ] Obtain GDPR compliance certification (optional)

---

## Evidence References

### Primary Evidence

**Framework Implementation:**
- `docs/security/gdpr-compliance.md` - Comprehensive GDPR guide (1,000+ lines)
- `docs/advanced/security.md` - Security capabilities (encryption, audit, access control)
- `src/Excalibur/Excalibur.Testing.Conformance/Conformance/` - Conformance test kits (shipped as the `Excalibur.Testing.Conformance` package)

**Conformance arms available to wrap** (your evidence pack records what *your* run executed):
- `ErasureStoreConformanceTestKit` — 24 arms - Article 17
- `LegalHoldStoreConformanceTestKit` — 19 arms - Article 17(3)
- `DataInventoryStoreConformanceTestKit` — 20 arms (`protected`) - Article 30
- `AuditStoreConformanceTestKit` — 27 arms - SOC 2 / Article 32

**SQL Server Schema:**
- `compliance.ErasureRequests` - Erasure request tracking
- `compliance.LegalHolds` - Legal hold management
- `compliance.ErasureCertificates` - Compliance certificates (7-year retention)
- `compliance.DataInventory` - Personal data locations (RoPA)

### Supporting Documentation

**GDPR Text:**
- [GDPR Official Text](https://eur-lex.europa.eu/eli/reg/2016/679/oj)
- [Article 17: Right to Erasure](https://gdpr-info.eu/art-17-gdpr/)
- [Article 30: Records of Processing Activities](https://gdpr-info.eu/art-30-gdpr/)

**Framework Documentation:**
- [Encryption Architecture](../../security/encryption-architecture.md)
- [Audit Logging Guide](../../security/audit-logging.md)

---

## Continuous Compliance

### Automated Monitoring

**Every Erasure Request:**
- Audit trail generated (request, execution, certificate)
- Legal hold check (automatic blocking if holds exist)
- Verification performed (KMS key deletion + audit log + decryption test)
- Certificate issued (cryptographic proof of erasure)

**Periodic Reviews:**
- Quarterly: RoPA update (new data locations, external systems)
- Quarterly: Privacy policy review (legal basis, processing purposes)
- Annually: DPIA refresh (new risks, mitigations)
- Annually: Breach response testing (tabletop exercises)

### On-Demand

- Subject access request (SAR) fulfillment (Article 15)
- Data portability (Article 20)
- Rectification (Article 16)
- Restriction of processing (Article 18)

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Erasure blocked unexpectedly | Active legal hold | Check `ILegalHoldService.ListActiveHoldsAsync()` |
| Certificate generation fails | Request not completed | Wait for `Completed` status |
| Data not discovered | Missing `[PersonalData]` attribute | Annotate fields or register manually |
| Verification fails | KMS or audit service unavailable | Check service health, retry |

### Logging

Enable detailed logging:

```csharp
builder.Logging.AddFilter("Excalibur.Compliance", LogLevel.Debug);
```

---

## Contact

**Questions:**
- Product Manager: Privacy policy, legal basis, GDPR scope
- Software Architect: Technical implementation, encryption, audit
- Project Manager: Compliance documentation, audit coordination

**Escalation:**
- GDPR breach: See breach notification procedures
- Compliance gaps: Create GitHub issue with `compliance` label
- Audit requests: Contact Project Manager for evidence package

---

## GDPR Compliance Portfolio

**Conformance Test Kits:**

| Kit | Article | Tests | Purpose |
|-----|---------|-------|---------|
| **AuditStoreConformanceTestKit** | Article 32 | 18 | Tamper-evident audit logging |
| **ErasureStoreConformanceTestKit** | Article 17 | 24 | "Right to be Forgotten" |
| **LegalHoldStoreConformanceTestKit** | Article 17(3) | 19 | Legal hold exceptions |
| **DataInventoryStoreConformanceTestKit** | Article 30 | 19 | Records of Processing Activities (RoPA) |
| **Total** | | **80** | Complete GDPR compliance verification |

Together, these four kits provide comprehensive verification of GDPR compliance infrastructure:
- **Audit**: Proves what happened (tamper-evident hash chain)
- **Erasure**: Implements data deletion rights (with grace periods)
- **LegalHold**: Implements legal exceptions to deletion (blocks erasure when required)
- **DataInventory**: Tracks where personal data is stored (RoPA compliance)

---

## See Also

- [GDPR Erasure](../gdpr-erasure.md) - Cryptographic data deletion for right to be forgotten
- [Compliance Checklists](index.md) - All compliance checklists overview
- [Data Masking](../data-masking.md) - PII/PHI protection in logs and outputs

---

**Last Updated:** 2026-01-01
**Next Review:** 2026-04-01
**Status:** GDPR checklist COMPLETE ✅
