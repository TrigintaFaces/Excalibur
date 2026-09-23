# GDPR Certification Readiness Checklist

**Framework:** Excalibur
**Standard:** GDPR (General Data Protection Regulation)
**Implementation:** Cryptographic erasure + Records of Processing Activities (RoPA)
**Status:** Comprehensive compliance capabilities implemented
**Last Updated:** 2026-09-12

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
| **Article 17** | Right to Erasure | ✅ SATISFIED | Inherit `IErasureService` | [Erasure workflow](../gdpr-erasure.md#erasure-workflow) |
| **Article 17(3)** | Erasure Exceptions | ✅ SATISFIED | Inherit `ILegalHoldService` | [Legal holds](../gdpr-erasure.md#legal-holds) |
| **Article 25** | Data Protection by Design | ⚠️ PARTIAL | Inherit `[PersonalData]` encryption — but register encryption, and annotate a `[DataSubjectId]` member on each record: a record that declares no data subject is left in plaintext, and only `string` and `byte[]` properties are covered. Data minimisation and purpose limitation remain yours | [Field-level encryption](../../security/encryption-architecture.md#personaldata-attribute) |
| **Article 30** | Records of Processing Activities | ✅ SATISFIED | Inherit `IDataInventoryService` | [Data inventory](../gdpr-erasure.md#data-inventory) |
| **Article 32** | Security of Processing | ⚠️ PARTIAL | Inherit encryption + the `IAuditLogger` API and its event taxonomy. **Nothing is audited automatically** — you choose the events and call `IAuditLogger` at each point. Availability, resilience and the regular testing Article 32(1)(d) requires are yours | [Encryption architecture](../../security/encryption-architecture.md), [audit logging](../../security/audit-logging.md) |
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
```

#### 1.3 Review Framework Capabilities

- [ ] Read [GDPR erasure](../gdpr-erasure.md)
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
- Erasure certificates as a compliance **record**, not proof of disposal. The `2.0` signature covers the payload in full; the framework ships no verifier for it, and certificates written under format `1.0` signed only three identity fields — see [Known issues](../../known-issues.md)

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
    options.Retention.CertificateRetentionPeriod = TimeSpan.FromDays(365 * 7); // 7 years
    options.Retention.SigningKeyId = "erasure-cert-signing-key";
});

// Use SQL Server for production persistence. Swap AddSqlServer* for AddPostgres* to run on
// PostgreSQL; the options are the same and the packages are peers.
builder.Services.AddSqlServerErasureStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Compliance");
    options.SchemaName = "compliance";
    // AutoCreateSchema defaults to false: you provision the tables and the store verifies at
    // startup that they exist. Set it to true to have the store create them on first use.
});

// Each contract needs its own store. Registering the service without one leaves the service with
// nowhere to persist -- these are three separate registrations, not one.
builder.Services.AddLegalHoldService();
builder.Services.AddSqlServerLegalHoldStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Compliance");
    options.SchemaName = "compliance";
});

builder.Services.AddDataInventoryService();
builder.Services.AddSqlServerDataInventoryStore(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Compliance");
    options.SchemaName = "compliance";
});

builder.Services.AddErasureVerificationService();
builder.Services.AddErasureScheduler();
```

- [ ] Configure the erasure store
- [ ] Configure the legal-hold store
- [ ] Configure the data-inventory store
- [ ] Provision the compliance tables, or set `AutoCreateSchema = true` on each store (see [tenant scoping of registrations](#43-generate-ropa-report))
- [ ] Set certificate retention period (7 years recommended)
- [ ] Configure signing key for certificate signatures

:::warning Shipped test evidence differs by provider and by what you count — verify against your own database

`✅ SATISFIED` in the table above means **the framework provides a technical implementation**. It does not
mean every provider of that implementation carries identical shipped test evidence. For the compliance
stores the answer depends on which evidence you count, and the two counts point different ways — so count
the one that bears on your control rather than the one that is easiest to grep.

**Conformance arms — the measure that bears on provider behaviour.** Each store's conformance suite is
bound against real SQL Server and real Postgres, not the in-memory store alone, and an arm runs on a
provider only if that provider's suite declares a wrapper for it. Compare the two suites directly:

```bash
d=tests/integration/Excalibur.Dispatch.Integration.Tests/Compliance
for s in Audit LegalHold Erasure DataInventory; do
  printf '%-14s sqlserver=%s postgres=%s\n' "$s" \
    "$(grep -cE '^[[:space:]]*\[(Fact|SkippableFact)' "$d/SqlServer/SqlServer${s}StoreConformanceTests.cs")" \
    "$(grep -cE '^[[:space:]]*\[(Fact|SkippableFact)' "$d/Postgres/Postgres${s}StoreConformanceTests.cs")"
done
```

**Total test files naming the store type — a weaker proxy, and the one that differs.** It sweeps in unit
tests, DI-registration tests and helpers, so a difference here does **not** by itself mean a provider's
behaviour is less covered:

```bash
for s in AuditStore LegalHoldStore ErasureStore DataInventoryStore; do
  printf '%-20s sqlserver=%s postgres=%s\n' "$s" \
    "$(grep -rlw "SqlServer$s" tests/ --include=*.cs | wc -l)" \
    "$(grep -rlw "Postgres$s"  tests/ --include=*.cs | wc -l)"
done
```

**No counts are published here**, deliberately. A number typed into this page is ours at a past moment, it
goes stale on the next commit, and it is not what evidences your control in any case — neither figure says
anything about your database, your schema, or your workload. Run the commands above against the
[framework repository](https://github.com/TrigintaFaces/Excalibur) if you want the current shape, and read
the two results against each other rather than either one alone.

**A declared wrapper is not an executed test.** What belongs in an evidence package is the arms *your*
run executed and passed against *your* database — our bindings evidence our schema and our
configuration, on a disposable container.

The legal-hold tenant predicate additionally has a never-skipped suite that migrates the shipped schema and
asserts a global hold stays visible to a scoped tenant.

Read both results narrowly. Our provider suites run against a disposable container, on our schema and
our configuration — **that is not your database, so you should not present our artifacts as if it were.**
Collation, and whether you have actually run the tenant-totality migration, both change the answers above on
a real deployment. Before relying on Article 17, 17(3) or 30 behaviour in production, exercise it against
your own database and keep that result as your evidence.

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
        catch (InvalidOperationException)
        {
            return Conflict("The erasure request has already been executed.");
        }
    }

    [HttpGet("erasure-requests/{requestId}/certificate")]
    public async Task<IActionResult> GetCertificate(Guid requestId, CancellationToken ct)
    {
        try
        {
            var certificate = await _erasureService.GenerateCertificateAsync(
                requestId, ct);

            // Return the certificate as issued. Re-shaping the payload would separate the
            // signature from the content it was computed over.
            return Ok(certificate);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            return Conflict("The erasure request has not completed.");
        }
    }
}
```

- [ ] Implement POST /erasure-requests endpoint
- [ ] Implement GET /erasure-requests/\{requestId\} for status checking
- [ ] Implement POST /erasure-requests/\{requestId\}/cancel for grace period cancellation
- [ ] Implement GET /erasure-requests/\{requestId\}/certificate to retrieve the compliance **record**

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
- [ ] Wrap and run the `ErasureStoreConformanceTestKit` arms

**Evidence:**
- [Conformance toolkit](../../testing/conformance-toolkit.md) - Testing guide
- Conformance results from the arms you wrapped (`ErasureStoreConformanceTestKit` — bound against real SQL Server and real PostgreSQL in our suite; that evidences OUR schema and configuration, not your deployment)

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
- [ ] Wrap and run the `LegalHoldStoreConformanceTestKit` arms

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

:::info How data-location registrations are scoped by tenant

**Registrations belong to a tenant.** Each record carries the owning tenant as a value of its own, and that value is part of the record's key — `(TableName, FieldName, TenantId)`, enforced as the primary key on PostgreSQL and as a uniqueness constraint on SQL Server (whose clustered key is a surrogate, because the natural key exceeds the clustered-index size limit). Two tenants can register `("CRM_Contacts", "PersonalInfo")` independently and hold two distinct records. Neither overwrites the other.

**`AutoCreateSchema = true` and the shipped migration scripts now produce the same shape** — a surrogate clustered key with the natural key enforced as a uniqueness constraint, on both data-inventory tables. Either provisioning path is safe on SQL Server.

**The tenant term comes from the ambient tenant context, not from an argument.** `RegisterDataLocationAsync` takes no tenant parameter; the store resolves the current tenant once and binds the same term into every statement it issues. A host with no tenant context registered is not multi-tenant — its rows belong to the reserved untenanted partition rather than to an absent tenant. A registration that genuinely belongs to no tenant is stored under that sentinel, never as `NULL`, so "global" and "the caller forgot" stay distinguishable.

**What a scoped caller sees.** A read returns that tenant's own registrations **plus** untenanted ones, so estate-wide locations can be registered once by an unscoped operator and inherited by every tenant, while a tenant's own registrations stay private to it. Removal is scoped strictly: a tenant can remove only its own registration, never an untenanted one.

**`TenantIdColumn` is still a column *name*, not a tenant *value*.** It records which column in *your* table holds the tenant identifier, so the erasure path knows where to look. It does not associate the registration with a tenant and plays no part in scoping — use the registration's tenant for that. The two are easy to confuse.

**Upgrading an existing database.** A compliance database created before registrations carried a tenant term needs the shipped data-inventory migration script. It adds the column, backfills existing rows to the untenanted partition, and puts the term into the key. Apply it before relying on per-tenant registrations — until it runs, the older schema's reads remain estate-wide.

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

:::warning The `tenantId` argument is inert — scope comes from the ambient tenant context

**Supplying a `tenantId` to `DiscoverAsync` or `GetDataMapAsync` does not select that tenant.** The
parameter is accepted and deliberately never consulted. Passing `"tenant-a"` and passing `"tenant-b"`
return **identical results**, and so does passing nothing at all.

**What you actually get is correct, which is why this is a warning and not a data-isolation defect.**
The read is scoped by the **ambient tenant context**, and returns your tenant's registrations plus any
registered as untenanted — the shared, estate-wide entries. It does not return another tenant's rows.
The SQL Server, PostgreSQL and in-memory stores all behave this way; they agree.

**So to scope a read, set the ambient tenant, not the argument:**

```csharp
// The argument does nothing. This does.
using var scope = tenantContext.BeginScope("tenant-a");
var map = await dataInventory.GetDataMapAsync(tenantId: null, ct);
```

**For Article 30:** a RoPA report generated under a tenant's ambient scope contains that tenant's
processing records and the shared ones — not another controller's. If you generate reports by varying
the `tenantId` argument alone, every report will be identical and will reflect whichever tenant was
ambient, so vary the scope instead.

**A green conformance run does not demonstrate tenant isolation in *your* deployment.** The
data-inventory suite is derived three ways — in-memory, real SQL Server and real PostgreSQL — so the
scoping behaviour is exercised against both SQL providers, but against **our** schema, **our**
configuration and a disposable container. That is evidence about the store, not about your database.
Test scoping directly against your real database before relying on it for a regulatory report.

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
- [Data inventory](../gdpr-erasure.md#data-inventory) - Data inventory conformance
- RoPA report export
- Conformance test results (`DataInventoryStoreConformanceTestKit` — bound against in-memory, real SQL Server and real PostgreSQL)
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
- [ ] Verify `[PersonalData]` fields are encrypted **in your own database** — annotation alone does not encrypt; the record must also carry `[DataSubjectId]` and crypto-shredding must be registered
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
- [Field-level encryption](../../security/encryption-architecture.md#field-level-encryption) - Data at rest encryption guide
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
- [Audit logging](../../security/audit-logging.md) - Audit logging guide
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
- [Authorization](../../advanced/security.md#authorization) - Authorization and RBAC guide

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

**Reference:** [SIEM integration](../../security/audit-logging.md#siem-integration)

#### 7.2 Document Breach Response Plan

- [ ] Define roles and responsibilities (incident commander, legal, PR)
- [ ] Create breach notification templates (supervisory authority + data subjects)
- [ ] Test breach response procedures (tabletop exercises)

**Template:**

```markdown
# GDPR Breach Notification Template

:::warning Not legal advice

This page describes technical features that can **support** your compliance work. It is not legal
advice, and it does not establish that any system is compliant with any law, regulation or standard.
You remain responsible for your own compliance assessment, independent testing and validation, and
review by qualified legal and compliance professionals. See the [Compliance Disclaimer](../../legal/compliance-disclaimer.md).
:::

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
- Conformance test kits (the count that evidences a control is the one your own run executed, not one printed here)
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

- [ ] Run `ErasureStoreConformanceTestKit` *(shipped evidence: in-memory, plus real SQL Server and PostgreSQL bindings)*
- [ ] Run `LegalHoldStoreConformanceTestKit` *(shipped evidence: in-memory, plus real SQL Server and PostgreSQL bindings)*
- [ ] Run `DataInventoryStoreConformanceTestKit` *(shipped evidence: in-memory, plus real SQL Server and PostgreSQL bindings)*
- [ ] Run `AuditStoreConformanceTestKit` *(also exercised against SQL Server and PostgreSQL)*
- [ ] **Record your executed and passed counts in your own evidence pack**
- [ ] **Attach `ConformanceArmLedger.Describe()` output to that evidence pack.** The kits record every
  arm that ran its body and every arm that did not — with the capability it needed and why it was
  unavailable. An arm gated on an optional capability used to return early, which a test runner reports
  identically to a pass; the ledger is what separates *verified* from *not verified* in your evidence.
  Call `ConformanceArmLedger.Reset()` before the run, since it is process-wide and additive. See the
  [conformance toolkit guide](../../testing/conformance-toolkit.md#what-a-green-run-actually-covered).

:::warning The executed count is authored by you, not by this framework

**The conformance kits deliberately carry no test attributes.** Every arm is a `public virtual`
method — so **nothing is discovered or executed until you declare an attributed wrapper in your own
derived class.** Conformance is opt-in per arm.

This is intentional: it is what lets you run the suite against *your* store, with your fixtures and your
provider, rather than against ours. But it has a consequence for your evidence pack:

**Do not record a passed count you did not produce.** An arm you have not wrapped has not run, and a kit
you have merely referenced has asserted nothing. No arm counts are published on this page for that
reason: the only number that evidences your control is the one your own run executed.

**And check *which store* the shipped evidence covers.** All four kits are bound against real SQL Server
and real PostgreSQL, not the in-memory store alone. That is still not evidence about *your* deployment:
our suites run against a disposable container on our schema and our configuration. Run the arms against
your real database before treating them as Article 17, 17(3) or 30 evidence.

The VSTest form of the commands below carries `RunConfiguration.TreatNoTestsAsError=true` for exactly
this reason — without it, a filter that matches nothing exits successfully and reads as a pass. **The
Microsoft.Testing.Platform form needs its own guard, not none:** it rejects that setting, and its
`--zero-tests-policy` defaults to `allow-skipped`, so a run in which every arm skipped still succeeds.
Pass `--minimum-expected-tests 1`, as the commands below do. The paragraph after them explains why.

:::

**Commands:**

```bash
# Run all GDPR conformance tests

# VSTest (default)
dotnet test --filter "FullyQualifiedName~ErasureStoreConformance" --blame-hang-timeout 5m -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~LegalHoldStoreConformance" --blame-hang-timeout 5m -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~DataInventoryStoreConformance" --blame-hang-timeout 5m -- RunConfiguration.TreatNoTestsAsError=true
dotnet test --filter "FullyQualifiedName~AuditStoreConformance" --blame-hang-timeout 5m -- RunConfiguration.TreatNoTestsAsError=true

# Microsoft.Testing.Platform
dotnet test --filter "FullyQualifiedName~ErasureStoreConformance" --blame-hang-timeout 10m -- --timeout 5m --minimum-expected-tests 1
dotnet test --filter "FullyQualifiedName~LegalHoldStoreConformance" --blame-hang-timeout 10m -- --timeout 5m --minimum-expected-tests 1
dotnet test --filter "FullyQualifiedName~DataInventoryStoreConformance" --blame-hang-timeout 10m -- --timeout 5m --minimum-expected-tests 1
dotnet test --filter "FullyQualifiedName~AuditStoreConformance" --blame-hang-timeout 10m -- --timeout 5m --minimum-expected-tests 1
```

Which of the two forms you need depends on the test runner your project uses, and picking the wrong
one fails in a way that does not name the cause:

- **VSTest** (the default). `RunConfiguration.TreatNoTestsAsError=true` is required — without it a
  filter that matches nothing exits `0` and reads as a pass.
- **Microsoft.Testing.Platform** (`<UseMicrosoftTestingPlatform>true</UseMicrosoftTestingPlatform>`).
  Do **not** pass the setting above: the native test host does not recognise it, prints its help text
  and exits non-zero on every run, whether or not the filter matched. **Pass
  `--minimum-expected-tests 1` instead** — a run that executes fewer tests than the minimum, including
  zero, exits with code `9`.
  **This matters more here than the filter case, because conformance arms skip themselves.** An arm
  whose capability your provider does not implement reports *skipped*, not *failed*. MTP's
  `--zero-tests-policy` defaults to `allow-skipped`, so a run in which **every** arm skipped
  **succeeds** — the checklist item would read as passed on a run that verified nothing. Either pass a
  minimum as above, or pass `--zero-tests-policy strict` (available from MTP 2.3.0), which fails an
  all-skipped run with exit code `8`. An explicit minimum supersedes the policy.

Both forms below also carry a hang bound, so a wedged test host ends the run with evidence instead of
occupying your pipeline until it is killed.


> **A conformance run that executes nothing is not a pass.** The VSTest form carries
> `RunConfiguration.TreatNoTestsAsError=true` deliberately: without it, `dotnet test --filter` **exits `0`
> when the filter matches nothing**, so if the conformance package is not referenced or a type has been
> renamed, the command prints `No test matches the given testcase filter` and **succeeds** — and this
> checklist item would be ticked on a run that verified nothing. With the setting, a filter matching no
> tests fails the command. **Confirm each run reports a non-zero `Total`;** an exit code alone is not
> evidence that a check ran.
>
> **Then read the process exit code, and take it as the verdict.** A non-zero `Total` tells you a check
> ran; only the exit code tells you whether it passed. **Do not tick this item from the summary line** —
> through the `dotnet test` bridge an assembly-level cleanup failure is reported outside the failure
> count, so the console can print `Passed!` with `Failed: 0` on a run that exited non-zero. Capture
> `$LASTEXITCODE` (PowerShell) or `$?` (POSIX shell) as the evidence for this item, not the console text.

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
  - Key-management service records of key deletion (the evidence of disposal)
  - Erasure certificates (sample), as internal records of the requests
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
- [GDPR erasure](../gdpr-erasure.md) - Cryptographic erasure, legal holds, and data inventory
- [Security guide](../../advanced/security.md) - Security capabilities (encryption, audit, access control)
- [Conformance toolkit](../../testing/conformance-toolkit.md) - Conformance test kits (shipped as the `Excalibur.Testing.Conformance` package)

**Conformance kits available to wrap** (your evidence pack records what *your* run executed):
- `ErasureStoreConformanceTestKit` - Article 17
- `LegalHoldStoreConformanceTestKit` - Article 17(3)
- `DataInventoryStoreConformanceTestKit` - Article 30
- `AuditStoreConformanceTestKit` - SOC 2 / Article 32

**No arm count is published here, deliberately.** Any number we print is ours at some past moment, and
the number that evidences your control is the one your own run executed. To see what a kit ships today,
enumerate it from the assembly you installed:

```csharp
using System.Reflection;

const BindingFlags Declared =
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

var arms = typeof(ErasureStoreConformanceTestKit)   // or any other kit
    .GetMethods(Declared)
    .Where(m => (m.ReturnType == typeof(Task) || m.ReturnType == typeof(void))
                && m.GetParameters().Length == 0
                && m.IsVirtual && !m.IsFinal && !m.IsSpecialName
                && m.Name != "ConformanceSuite_ShouldWireEveryArm")
    .Select(m => m.Name)
    .ToList();

Console.WriteLine(arms.Count);
```

If a kit inherits arms from an abstract base kit, repeat the same enumeration for each base type up to
`ConformanceTestKit`, which is what the shipped wiring check does.

**SQL Server Schema:**
- `compliance.ErasureRequests` - Erasure request tracking
- `compliance.LegalHolds` - Legal hold management
- `compliance.ErasureCertificates` - Erasure certificates, as records of each request (7-year retention)
- `compliance.DataInventoryRegistrations` - registered personal-data locations (RoPA); `compliance.DiscoveredDataLocations` - auto-discovered locations

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
- Erasure events are **not** written to the audit log by the framework. If your assessor expects them
  in the tamper-evident audit trail, record them there yourself
- Legal hold check (automatic blocking if holds exist)
- Verification performed (KMS key deletion and, when enabled, a decryption-failure test). The audit-log
  method finds nothing unless you have recorded erasure events yourself
- Certificate issued (a signed **record** of the erasure — not proof of disposal. The `2.0` signature covers the payload in full, but the framework ships no verifier for it; see [Known issues](../../known-issues.md))

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

| Kit | Article | Purpose |
|-----|---------|---------|
| **AuditStoreConformanceTestKit** | Article 32 | Tamper-evident audit logging |
| **ErasureStoreConformanceTestKit** | Article 17 | "Right to be Forgotten" |
| **LegalHoldStoreConformanceTestKit** | Article 17(3) | Legal hold exceptions |
| **DataInventoryStoreConformanceTestKit** | Article 30 | Records of Processing Activities (RoPA) |

:::caution What a kit *can* run is not what anyone ran
Each kit defines "arm" executably rather than by convention: its `ConformanceSuite_ShouldWireEveryArm`
enumerates them by reflection as the public, parameterless, virtual methods on the kit returning `Task`
or `void`, minus the wiring check itself. The `CleanupAsync` lifecycle helper is excluded because it is
`protected`, not because it is named. **This page publishes no arm counts**, because a number printed here
is ours at some past moment and goes stale the next time a kit gains an arm. Derive the current figure
from the kit you installed instead:

```csharp
using System.Reflection;

const BindingFlags Declared =
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

var arms = typeof(ErasureStoreConformanceTestKit)   // or any other kit
    .GetMethods(Declared)
    .Where(m => (m.ReturnType == typeof(Task) || m.ReturnType == typeof(void))
                && m.GetParameters().Length == 0
                && m.IsVirtual && !m.IsFinal && !m.IsSpecialName
                && m.Name != "ConformanceSuite_ShouldWireEveryArm")
    .Select(m => m.Name)
    .ToList();

Console.WriteLine(arms.Count);
```

If a kit inherits arms from an abstract base kit, repeat the same enumeration for each base type up to
`ConformanceTestKit`, which is what the shipped wiring check does.
 The kits carry no test attributes, so an arm executes only once
you declare an attributed wrapper for it in your own derived class. **The number that belongs in an
evidence package is the executed and passed count from your own run**, together with its output —
not the figure above. An assessor who is shown "92" has been shown the size of a menu.
:::

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

**Last Updated:** 2026-09-12
**Document status:** every GDPR article in scope is walked through below. **This describes the document, not your compliance posture** — 5 of 9 articles are satisfied by framework controls you can inherit; 4 are business process and are yours to evidence.
