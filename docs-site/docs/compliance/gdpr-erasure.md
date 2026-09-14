---
sidebar_position: 3
title: GDPR Erasure
description: Cryptographic erasure for Right to be Forgotten compliance
---

# GDPR Erasure

GDPR Article 17 ("Right to be Forgotten") requires organizations to delete personal data upon request. Dispatch implements this through cryptographic erasure (crypto-shredding), which renders data irrecoverable by deleting encryption keys.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Compliance
  # plus Excalibur.Compliance.SqlServer or Excalibur.Compliance.Postgres for the stores below
  ```
- Familiarity with [encryption architecture](../security/encryption-architecture.md) and [data masking](./data-masking.md)

## Overview

```mermaid
sequenceDiagram
    participant DS as Data Subject
    participant API as Erasure API
    participant ES as ErasureService
    participant LH as LegalHoldService
    participant KMS as Key Management

    DS->>API: Request Erasure
    API->>ES: RequestErasureAsync()
    ES->>LH: Check Legal Holds
    LH-->>ES: No Holds
    ES-->>API: Scheduled (Grace Period)

    Note over ES: 72 hours grace period

    ES->>KMS: Delete Encryption Keys
    KMS-->>ES: Keys Deleted
    ES-->>API: Certificate Generated
```

## Quick Start

### Configuration

```csharp
services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod = TimeSpan.FromHours(72);
    options.RequireVerification = true;
});

// Development (in-memory stores)
services.AddInMemoryErasureStore();
services.AddInMemoryLegalHoldStore();
services.AddLegalHoldService();
services.AddErasureScheduler();

// Development only — see the two startup gates described below.
services.Configure<KeyDurabilityOptions>(o => o.AllowVolatileKeyProvider = true);
```

:::tip Minimal wiring

`AddGdprErasure(...)` `TryAdd`-registers a default `IKeyManagementAdmin` (the in-memory `InMemoryKeyManagementProvider`). When you need a real KMS provider, call `AddComplianceEncryption(...)` — an explicit registration takes precedence over the `TryAdd` default.

**The block above is not yet a host that starts.** Two startup gates refuse it, both deliberately:

- **No discovery source.** Erasure refuses to start with neither an `IDataInventoryService` nor an explicit
  opt-in, because a completion certificate would otherwise be issued over coverage nobody verified. Register
  `AddDataInventoryService()`, or set `options.KeyShredOnlyErasure = true` to accept key-destruction-only
  erasure (certificates then report a coverage basis of key destruction rather than store-verified coverage).
- **A volatile key provider.** Erasure works by destroying keys, so a durability gate refuses the in-memory
  provider unless the host says so explicitly: `services.Configure<KeyDurabilityOptions>(o =>
  o.AllowVolatileKeyProvider = true);`. A production host registers a durable provider instead.

The in-memory provider holds keys in process memory and does not persist them. **It is not suitable for production**, where a restart would lose the keys required to read crypto-shredded data.
:::

```csharp

// Production (SQL Server storage)
// Package: Excalibur.Compliance.SqlServer
services.AddSqlServerErasureStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "compliance";
});
services.AddSqlServerLegalHoldStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "compliance";
});
services.AddLegalHoldService();
```

:::caution Erasure refuses to start without a legal-hold service

Registering an erasure store on its own is **not** a working configuration. Erasure consults legal holds
through an optional dependency and skips the check when no legal-hold service is registered — correct for a
deployment that has no holds, and irreversible for one that does. Because the absence cannot be distinguished
from a misconfiguration, it must be declared rather than inferred, so the host **fails at startup** when
erasure is registered with no legal-hold service.

Register one, as the block above does.

A deployment that genuinely operates no legal holds declares that explicitly instead, with
`ErasureOptions.OperatesNoLegalHolds` — see [Legal Holds](#legal-holds) for that declaration and for which
released versions carry it.

The `AddExcaliburPostgres` and `AddExcaliburSqlServer` metapackages register the full compliance set,
including the legal-hold service, so a host composed through either of those needs neither the registration
above nor the flag.

:::

:::info The compliance stores require a tenant context — the registrations supply it

`SqlServerErasureStore`, `SqlServerLegalHoldStore` and their PostgreSQL counterparts take `ITenantContext`
as a **required** constructor parameter. It used to be optional and default to `null`, and a store built
without one partitioned by whatever an absent context resolved to — not a decision a compliance store should
make silently. The in-memory erasure and legal-hold stores changed the same way, but they are internal types
reachable only through `AddInMemoryErasureStore()` / `AddInMemoryLegalHoldStore()`, so nothing is different
from where you sit.

**If you register through the extensions shown on this page, nothing changes for you.** Each
`Add*ErasureStore` / `Add*LegalHoldStore` extension registers a fail-closed single-tenant `ITenantContext`
default on your behalf, so the store resolves one whether or not your host calls
[`AddMultiTenancy`](../multi-tenancy.md). You only need to act if you **construct one of these stores
directly** — pass an `ITenantContext`, or the constructor throws `ArgumentNullException`.

Registering a tenant context does not by itself make a deployment multi-tenant: the store reads the
deployment mode from `TenantContextOptions.RequireTenant`, which `AddMultiTenancy(...)` sets. A single-tenant
host resolves the reserved `__untenanted__` partition, exactly as before.
:::

:::note The in-memory stores now store the untenanted sentinel, matching the SQL providers

The SQL stores fold an absent tenant to the reserved `__untenanted__` sentinel before storing it. The
in-memory stores assigned the raw value, so a `null` reached storage and the same input produced a different
stored term depending on which provider you used. Both now fold identically, and the in-memory legal-hold
read no longer carries a second spelling of *absent*.

If you asserted on an in-memory store returning a `null` tenant — in a test, most likely — it returns
`__untenanted__` instead. See [Untenanted is a value, not an absence](../multi-tenancy.md#2-untenanted-is-a-value-not-an-absence).
:::

### Submit Erasure Request

```csharp
public class ErasureController : ControllerBase
{
    private readonly IErasureService _erasureService;

    [HttpPost("erasure")]
    public async Task<IActionResult> RequestErasure(
        [FromBody] ErasureRequestDto dto,
        CancellationToken ct)
    {
        var request = new ErasureRequest
        {
            DataSubjectId = dto.SubjectId,
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            RequestedBy = User.Identity?.Name ?? "anonymous",
            TenantId = dto.TenantId,
            Scope = ErasureScope.User
        };

        var result = await _erasureService.RequestErasureAsync(request, ct);

        return Ok(new
        {
            RequestId = result.RequestId,
            Status = result.Status,
            ScheduledFor = result.ScheduledExecutionTime
        });
    }
}
```

## Erasure Workflow

### 1. Request Submission

```csharp
var request = new ErasureRequest
{
    DataSubjectId = "user-12345",
    IdType = DataSubjectIdType.UserId,
    LegalBasis = ErasureLegalBasis.DataSubjectRequest,
    RequestedBy = "compliance@company.com",
    TenantId = "tenant-abc",
    Scope = ErasureScope.User
};

var result = await _erasureService.RequestErasureAsync(request, ct);
```

### 2. Grace Period

Requests enter a configurable grace period (default 72 hours) before execution:

```csharp
services.AddGdprErasure(options =>
{
    // Default grace period (minimum recommended 72 hours for production)
    options.DefaultGracePeriod = TimeSpan.FromHours(72);

    // Configure min/max bounds
    options.MinimumGracePeriod = TimeSpan.FromHours(24);
    options.MaximumGracePeriod = TimeSpan.FromDays(30);
});
```

### 3. Cancellation (During Grace Period)

```csharp
var cancelled = await _erasureService.CancelErasureAsync(
    requestId: result.RequestId,
    reason: "Request withdrawn by data subject",
    cancelledBy: "support@company.com",
    ct);

if (!cancelled)
{
    // Request already executed or not found
}
```

### 4. Execution (Crypto-Shredding)

Erasure execution is handled automatically by the background scheduler after the grace period expires. Consumers do not call execution directly — monitor status via `GetStatusAsync`:

```csharp
// Poll for completion after grace period
var status = await _erasureService.GetStatusAsync(requestId, ct);

switch (status?.Status)
{
    case ErasureRequestStatus.Completed:
        _logger.LogInformation("Erasure complete for {RequestId}", requestId);
        break;
    case ErasureRequestStatus.PartiallyCompleted:
        _logger.LogWarning("Partial erasure for {RequestId}", requestId);
        break;
    case ErasureRequestStatus.Scheduled:
        _logger.LogInformation("Awaiting grace period for {RequestId}", requestId);
        break;
    case ErasureRequestStatus.Pending:
    case ErasureRequestStatus.InProgress:
        _logger.LogInformation("Erasure in flight for {RequestId}", requestId);
        break;
}
```

### Status values

`GetStatusAsync` can return any of the following. Handle `Pending` and `InProgress` explicitly if you poll — a request occupies them briefly but genuinely, and a `switch` that omits them falls through silently while work is still in flight.

| value | meaning |
|---|---|
| `Pending` | Request received, validation in progress. |
| `Scheduled` | In grace period, awaiting execution. |
| `InProgress` | Erasure currently executing. |
| `Completed` | Erasure completed successfully. |
| `BlockedByLegalHold` | Blocked by a legal hold (Article 17(3)). |
| `Cancelled` | Cancelled during the grace period. |
| `Failed` | Erasure failed and requires investigation. |
| `PartiallyCompleted` | Some data retained under a documented exception. |

### Statutory deadline

`ErasureStatus.DaysUntilDeadline(asOf)` is a **method**, not a property: it reports the days remaining against the one-month response deadline in Article 12(3), measured from `RequestedAt` as of the instant you pass, and floored at zero. Use it to surface requests approaching the limit:

```csharp
// GetStatusAsync is on IErasureStore. IErasureQueryStore carries only the two
// list-shaped members (GetScheduledRequestsAsync, ListRequestsAsync).
var status = await _erasureStore.GetStatusAsync(requestId, ct);
var remaining = status?.DaysUntilDeadline(DateTimeOffset.UtcNow);

if (status is { IsExecuted: false } && remaining <= 5)
{
    _logger.LogWarning(
        "Erasure {RequestId} has {Days} days remaining against the statutory deadline",
        requestId, remaining);
}
```

The framework reports the deadline. It does not enforce it — a request blocked by a legal hold, or one whose grace period has not elapsed, will pass the deadline without the framework intervening. Monitoring it is the controller's obligation.

### Certificate retention

Erasure certificates are your evidence that a request was honoured, and **the framework deletes them on a schedule.** Each certificate is stamped with a `RetainUntil` of completion plus `Retention.CertificateRetentionPeriod` — **7 years by default** — and `CleanupExpiredCertificatesAsync` permanently deletes every certificate past that date.

```csharp
services.Configure<ErasureOptions>(o =>
    o.Retention.CertificateRetentionPeriod = TimeSpan.FromDays(365 * 10)); // extend to 10 years
```

If your retention obligation is longer than the configured period, raise it before certificates begin ageing out, or export them to your own archive. Deletion is permanent and is not announced.

:::warning Partial Completion Is Structural, Not Just On Failure

An erasure reaches `Completed` **only** when every discovered personal-data location is *covered* **and** no contributor reported an error. **Three** distinct conditions prevent it:

1. **A contributor erasure fails** (an error is reported), or
2. **A discovered location is left _uncovered_** — its store holds personal data but no mechanism erases it (no crypto-shred key, no covering `IErasureContributor`, no declared exemption), or
3. **A `[PersonalData]` category matches no discovered location at all** — annotated personal data the inventory never located. A `Completed` certificate over silently-skipped annotated data is deliberately made inexpressible.

Any of the three forces a non-`Completed` outcome **even when nothing threw** — the framework will not report `Completed` over a store it never erased.

**Which non-`Completed` status you get depends on whether anything succeeded.** If at least one key was deleted or at least one record was affected, the request is `PartiallyCompleted`. **If nothing succeeded at all — a pure coverage gap with zero deletions — the request is `Failed`, not `PartiallyCompleted`.** Handle both; a `switch` that treats coverage problems as always-partial will fall through on the pure-gap case.

See [Erasure Coverage Model](#erasure-coverage-model) below. Monitor the `ErasurePartiallyCompleted` event (ID 92729) and investigate uncovered stores and failed contributors.
:::

### 5. Compliance Certificate

Generate cryptographic proof of erasure. **A certificate can only be produced for a request whose status is
exactly `Completed`:** `GenerateCertificateAsync` throws `InvalidOperationException` for any other status —
including the `PartiallyCompleted` and `Failed` outcomes described directly above — and `KeyNotFoundException`
for a request id it does not know. So the cases most in need of documentation are the ones that cannot be
certified; evidence them from the status and its error summary instead.

```csharp
var certificate = await _erasureService.GenerateCertificateAsync(requestId, ct);

// Certificate contains:
// - Request details (RequestId, anonymized DataSubjectReference)
// - Execution timestamp (CompletedAt) and Method (e.g. CryptographicErasure)
// - Summary.KeysDeleted / RecordsAffected
// - Summary.DataCategories is present on the type but is NOT populated: both paths that build a
//   certificate summary set it to an empty list, so do not rely on it
// - Verification.Verified + Verification.DeletedKeyIds (the specific key IDs proven gone)
// - Exceptions: stores deliberately retained under Article 17(3) (e.g. the audit store), with legal Basis
// - Signature: an HMAC-SHA256 over the certificate data, keyed with your configured signing key
//   (a keyed MAC, not a bare digest — an unkeyed SHA-256 would attest nothing about origin)
```

The verification summary records the **specific** deleted key IDs (`Verification.DeletedKeyIds`) and is non-vacuous: if the summary claims `KeysDeleted > 0` but no deleted key can be confirmed gone — or a discovered location was left uncovered — `Verification.Verified` is `false` rather than a blanket `true`.

## Erasure Coverage Model

Erasure breadth is governed by a **three-state coverage gate**. Every personal-data [location](#data-inventory) discovered for the data subject is classified as one of:

| State | Meaning | Effect on status |
|-------|---------|------------------|
| **Covered** | A mechanism erases this location: its per-subject encryption key was deleted (crypto-shred), **or** a registered `IErasureContributor` declares its store kind. | Does not block `Completed`. |
| **Exempt** | A declared, documented retention exemption with a legal basis (e.g. the audit/security store). | Enumerated on the certificate (`Exceptions`), but **non-blocking**. |
| **Uncovered** | Neither covered nor exempt — a genuine gap. | **Forces a non-`Completed` outcome** — `PartiallyCompleted` if anything succeeded, `Failed` if nothing did — naming the uncovered store. |

`Completed` is reachable **only** when there are zero uncovered locations and zero errors. This is enforced structurally — the framework cannot report `Completed` over a store it never erased.

### Store kinds and contributor coverage

Each `DataLocation` carries a `StoreKind` (`Excalibur.Compliance.DataStoreKind`). A contributor declares which kinds it erases via `CoveredStoreKinds`:

```csharp
using Excalibur.Compliance;

public sealed class OutboxErasureContributor : IErasureContributor
{
    public string Name => "Outbox";

    // The coverage gate marks an Outbox-kind location as Covered when this contributor is registered.
    public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } =
        new HashSet<DataStoreKind> { DataStoreKind.Outbox };

    public Task<ErasureContributorResult> EraseAsync(
        ErasureContributorContext context,
        CancellationToken cancellationToken)
    {
        // Delete/tombstone rows for context.DataSubjectIdHash, then:
        return Task.FromResult(ErasureContributorResult.Succeeded(recordsAffected: 0));
    }
}
```

`DataStoreKind` is an **extensible**, string-backed kind (the Microsoft "names" pattern), not a closed enum — consumers may have custom stores holding personal data. Use the well-known members (`DataStoreKind.EventStore`, `.Snapshot`, `.Outbox`, `.Inbox`, `.Projection`, `.Saga`, `.Audit`, `.Cache`) for first-party stores and `DataStoreKind.Create("MyCustomStore")` for your own. The default/unclassified kind (`DataStoreKind.Unknown`) is **never coverable** — an unclassified location always blocks `Completed`, so a store can never silently pass as erased.

### Audit/security store: exempt by default

The audit/security store kind (`DataStoreKind.Audit`) is treated as **`Exempt` by default**, on the legal basis of **GDPR Article 17(3)(b)** (processing necessary for compliance with a legal obligation — security audit-trail retention) and **Article 17(3)(e)** (establishment, exercise, or defence of legal claims — security-incident investigation). The exemption is recorded explicitly on the certificate's `Exceptions` list with its basis — it is never a silent skip and is never falsely counted as covered.

If your compliance posture requires the audit store to be erased (no legal-retention basis, or post-retention-window erasure), **override the default** by registering an `IErasureContributor` whose `CoveredStoreKinds` includes `DataStoreKind.Audit` (contributor coverage wins over the default exemption).

:::warning Compliance assistance, not a compliance guarantee

The default audit-store exemption is a **sensible documented default**, not a legal determination. Excalibur is a framework, not your application — it cannot make your organization's final legal call. Your Data Protection Officer owns the decision of whether the audit store is in scope for a given erasure. See the [Compliance Disclaimer](../legal/compliance-disclaimer.md).
:::

## Legal Holds

Article 17(3) exceptions prevent erasure for:
- Legal claims
- Litigation holds
- Regulatory investigations
- Legal obligations

:::info Not in any published version yet
The startup refusal and `ErasureOptions.OperatesNoLegalHolds` described in this section are **not present
in `10.0.0-alpha.10` or any earlier release** — verified against the published assembly. On a released
package, erasure starts with no legal-hold service registered and **silently skips the hold check**. Until
a release carries this, register `AddLegalHoldService()` yourself and do not rely on a startup error to
tell you it is missing. When a release does carry it, this note will name that version.
:::

Erasure **refuses to start** unless a legal-hold service is registered, because the hold check is
skipped when the service is absent and erasure is irreversible. Registering only an `ILegalHoldStore`
is rejected as well — that is the configuration most easily mistaken for protection, since holds would
be written and readable but never enforced. Register `AddLegalHoldService()` alongside the store.

If a deployment genuinely operates no legal holds, declare it rather than leaving it to be inferred:

```csharp
services.AddNoLegalHolds();
```

`AddNoLegalHolds()` is one call that does both halves: it registers the service that reports no holds, and it records the declaration. **Setting `ErasureOptions.OperatesNoLegalHolds` on its own fails at startup** — the wiring validator has no early return for it, and refuses with a message naming this call. Declaring no holds accepts that no erasure request will ever be blocked by one. The
absence of the check has to be a decision, so there is no configuration that silently skips it.

### Register a Legal Hold Store

Holds are persisted through `ILegalHoldStore`, which is a separate registration from the erasure store —
registering the erasure store alone gives you no hold storage.

```csharp
// SQL Server — package: Excalibur.Compliance.SqlServer
services.AddSqlServerLegalHoldStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.SchemaName = "compliance";     // default
    // options.TableName = "LegalHolds";      // default
    // options.AutoCreateSchema = true;       // opt in to have the store create its own table
});

// PostgreSQL — package: Excalibur.Compliance.Postgres
services.AddPostgresLegalHoldStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.SchemaName = "compliance";     // default
    // options.TableName = "LegalHolds";      // default
    // options.AutoCreateSchema = true;       // opt in to have the store create its own table
});
```

Both providers also expose an `…FromConfiguration` overload that binds the options from a configuration
section instead of a lambda.

`AutoCreateSchema` has the same meaning here as for the erasure store — same default of `false`, same
create-versus-verify semantics — but **not the same timing.** Only the erasure store registers a startup
validator. The legal-hold store (and the data-inventory store below) verify their schema **lazily, on first
use**, so a missing or wrong table surfaces on the first erasure-related operation rather than at startup.
See [Database Schema](#database-schema).

### Check for Holds

```csharp
public class LegalHoldAwareErasure
{
    private readonly ILegalHoldService _holdService;
    private readonly IErasureService _erasureService;

    public async Task<ErasureResult> SafeErasure(
        ErasureRequest request,
        CancellationToken ct)
    {
        // Check for active holds (requires DataSubjectIdType)
        var checkResult = await _holdService.CheckHoldsAsync(
            request.DataSubjectId,
            request.IdType,
            request.TenantId,
            ct);

        if (checkResult.HasActiveHolds)
        {
            throw new ErasureOperationException(
                $"Cannot erase: {checkResult.ActiveHolds.Count} active legal hold(s)");
        }

        return await _erasureService.RequestErasureAsync(request, ct);
    }
}
```

### Create Legal Hold

```csharp
var hold = await _holdService.CreateHoldAsync(new LegalHoldRequest
{
    DataSubjectId = "user-12345",
    IdType = DataSubjectIdType.UserId,
    TenantId = "tenant-abc",
    Basis = LegalHoldBasis.LitigationHold,
    CaseReference = "Case #2024-001",
    Description = "Pending lawsuit - Case #2024-001",
    CreatedBy = "legal@company.com",
    ExpiresAt = DateTimeOffset.UtcNow.AddYears(2)
}, ct);
```

### Release Hold

```csharp
await _holdService.ReleaseHoldAsync(
    holdId: hold.HoldId,
    reason: "Litigation concluded",
    releasedBy: "legal@company.com",
    ct);
```

## Erasure Scopes

Control what data is erased:

```csharp
public enum ErasureScope
{
    User = 0,       // Erase all data for a specific user
    Tenant = 1,     // Erase all data for an entire tenant
    Selective = 2   // Erase specific data categories only
}

// Selective erasure with data categories
var request = new ErasureRequest
{
    DataSubjectId = "user-12345",
    IdType = DataSubjectIdType.UserId,
    LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
    RequestedBy = "compliance@company.com",
    Scope = ErasureScope.Selective,
    DataCategories = ["marketing", "analytics"]
};
```

## Data Inventory

Track where personal data is stored. Register the data inventory service via DI:

```csharp
// Register data inventory services
services.AddDataInventoryService();

// Development / tests
services.AddInMemoryDataInventoryStore();

// SQL Server — package: Excalibur.Compliance.SqlServer
services.AddSqlServerDataInventoryStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.SchemaName = "compliance";                             // default
    // options.RegistrationsTableName = "DataInventoryRegistrations"; // default
    // options.DiscoveredLocationsTableName = "DiscoveredDataLocations";
    // options.AutoCreateSchema = true;   // opt in to have the store create its own tables
});

// PostgreSQL — package: Excalibur.Compliance.Postgres
services.AddPostgresDataInventoryStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.SchemaName = "compliance";                             // default
    // options.RegistrationsTableName = "DataInventoryRegistrations"; // default
    // options.DiscoveredLocationsTableName = "DiscoveredDataLocations";
    // options.AutoCreateSchema = true;   // opt in to have the store create its own tables
});
```

The data inventory store keeps two tables rather than one — the registrations you declare and the
locations discovery finds — so it has a table-name option for each.

:::note

The `IDataInventoryService` provides registration and discovery of personal data locations across your system, enabling comprehensive erasure and Records of Processing Activities (RoPA) documentation.
:::

:::caution The in-memory data-inventory store is not tenant-capable
`IDataInventoryStore` is a tenant-owned contract. The **SQL Server and PostgreSQL** data-inventory stores
both attest a tenant capability, so they work alongside
[`AddMultiTenancy`](../multi-tenancy.md#first-class-persistence-isolation-addmultitenancy) with the
`RowDiscriminator` strategy.

The **in-memory** store shown above does not attest one, so a host that registers it *and* calls
`AddMultiTenancy(RowDiscriminator)` fails fast at startup. That is a startup refusal, not a silent leak —
you see it immediately in a local run, not in production. Register a durable data-inventory store, or keep
the in-memory one out of a container that configures row-discriminator tenancy.
:::

## Verification

### Check Erasure Status

```csharp
var status = await _erasureService.GetStatusAsync(requestId, ct);

switch (status?.Status)
{
    case ErasureRequestStatus.Scheduled:
        // In grace period
        break;
    case ErasureRequestStatus.Completed:
        // Successfully erased
        break;
    case ErasureRequestStatus.Failed:
        // Execution failed
        break;
    case ErasureRequestStatus.Cancelled:
        // Cancelled during grace period
        break;
}
```

### List Requests

```csharp
// Inject IErasureQueryStore — a separate, list-shaped contract. It does NOT inherit IErasureStore;
// request-addressed members such as GetStatusAsync live on IErasureStore.
var requests = await _erasureQueryStore.ListRequestsAsync(
    status: ErasureRequestStatus.Completed,
    tenantId: "tenant-abc",
    fromDate: DateTimeOffset.UtcNow.AddDays(-30),
    toDate: DateTimeOffset.UtcNow,
    pageNumber: 1,
    pageSize: 100,
    ct);
```

Results are paged. `pageNumber` is 1-based and `pageSize` accepts 1–1000; both are required, and values outside those ranges are rejected. Page through the result set rather than requesting an unbounded list — **nothing prunes the erasure-request table.** Certificate cleanup deletes from the certificates table only, so on a busy tenant the request table grows without bound and is yours to archive.

## Background Scheduler

Register the erasure scheduler to automatically execute requests after the grace period:

```csharp
// Register the scheduler service
services.AddErasureScheduler();
```

For serverless environments where background services are not available, register the erasure scheduler as a timer-triggered function:

```csharp
public class ErasureFunction
{
    private readonly IServiceProvider _serviceProvider;

    [Function("ProcessErasureRequests")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        // The scheduler handles execution internally when started
        // For serverless, use AddErasureScheduler() in DI and
        // let the hosted service process pending requests
        await using var scope = _serviceProvider.CreateAsyncScope();
        // Scheduler auto-processes pending requests on activation
    }
}
```

:::tip

For serverless deployments, `AddErasureScheduler()` registers the background service that automatically processes requests past their grace period. The execution logic is internal to the framework — consumers only need to submit requests and monitor status.

:::

## Database Schema

**By default the erasure store does not create its tables. You provision them, and the store verifies they exist at startup.**

Schema handling is controlled by `AutoCreateSchema`, which **defaults to `false`**: on startup the store verifies that the `compliance` schema and both of its tables — the erasure-request table and the erasure-certificate table — are present, and fails fast if they are missing rather than creating them. Set `AutoCreateSchema = true` to have the store create the schema and tables on first use if they do not already exist. Behavior is identical on SQL Server and PostgreSQL.

```csharp
// SQL Server — default: you provision the tables, the store verifies them at startup
services.AddSqlServerErasureStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.AutoCreateSchema = true;       // opt in to have the store create its own tables
});

// PostgreSQL — default: you provision the tables, the store verifies them at startup
services.AddPostgresErasureStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    // options.AutoCreateSchema = true;       // opt in to have the store create its own tables
});
```

:::warning Provision the tables from the store's own definition, not a copied schema

The column set — including the pseudonymized subject identifier described under [Data-subject hashing](#data-subject-hashing-idatasubjecthasher) — is an implementation detail that evolves with the framework.

Whether you provision the tables yourself (the default) or opt into `AutoCreateSchema = true`, a table built from a schema that does not match is not corrected: the store finds a table already present and leaves it as is. The mismatch surfaces later, at the moment a data subject exercises a right, rather than at startup. Provision from the store's own definition, not a copy transcribed into documentation.

:::

Because `AutoCreateSchema` defaults to `false`, DBA-managed environments get fail-fast verification without extra configuration: provision the tables to match the store's own definition and the store confirms they exist at startup. The schema and table names are configurable via `SchemaName`, `RequestsTableName`, and `CertificatesTableName`.

## Testing

### Unit Tests

```csharp
[Fact]
public async Task Should_Schedule_Erasure_With_Grace_Period()
{
    // Arrange
    var request = new ErasureRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        LegalBasis = ErasureLegalBasis.DataSubjectRequest,
        RequestedBy = "test@example.com",
        TenantId = "tenant-abc"
    };

    // Act
    var result = await _erasureService.RequestErasureAsync(request, CancellationToken.None);

    // Assert
    result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
    result.ScheduledExecutionTime.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
}

[Fact]
public async Task Should_Block_Erasure_With_Legal_Hold()
{
    // Arrange — create a legal hold first
    await _holdService.CreateHoldAsync(new LegalHoldRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        Basis = LegalHoldBasis.LitigationHold,
        CaseReference = "CASE-001",
        Description = "Test litigation hold",
        CreatedBy = "legal@example.com"
    }, CancellationToken.None);

    var request = new ErasureRequest
    {
        DataSubjectId = "user-123",
        IdType = DataSubjectIdType.UserId,
        LegalBasis = ErasureLegalBasis.DataSubjectRequest,
        RequestedBy = "test@example.com"
    };

    // Act & Assert — erasure should be blocked
    var result = await _erasureService.RequestErasureAsync(request, CancellationToken.None);
    result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
}
```

## Event Store Erasure

When using event sourcing, GDPR erasure must extend to event stores. The `IEventStoreErasure` interface (in `Excalibur.EventSourcing`) enables cryptographic erasure at the event store level.

### IEventStoreErasure Interface

```csharp
namespace Excalibur.EventSourcing;

public interface IEventStoreErasure
{
    /// <summary>
    /// Erases all event payloads for the specified aggregate, replacing them
    /// with a tombstone marker. The stream is retained for referential integrity.
    /// </summary>
    Task<int> EraseEventsAsync(
        string aggregateId,
        string aggregateType,
        Guid erasureRequestId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether erasure has been performed for the specified aggregate.
    /// </summary>
    Task<bool> IsErasedAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken);
}
```



:::info Tenant confinement

Both erasure operations are confined to the ambient tenant established for the store instance:
`EraseEventsAsync` tombstones only that tenant's stream for the given aggregate, and `IsErasedAsync`
answers only for it. The confinement is structural, and the mechanism differs by provider family.
The relational stores bind an unconditional tenant term on every erase and existence check, routed
through a partition type with no empty inhabitant, so a tenant-less erase cannot be constructed. The
document and in-memory stores address a key whose leading segment is the owning tenant, so another
tenant's stream is unaddressable rather than merely filtered out.

**Where a provider composes the tenant into a key, the composition is injective.** Erasure is
destructive and irreversible — a confinement failure here would destroy another tenant's history rather
than disclose it — so the tenant and the record identifier are encoded rather than concatenated: no pair
of identifiers can compose to another pair's key, whatever characters they contain. **Identifiers
containing the separator are supported and need no special handling by you.** A store presenting no
tenant capability marker is not confined by the framework at all.

Stated here because an assessor reading this page is entitled to know both what the framework
guarantees and what, if anything, it requires of you — and for these operations the answer to the
second is nothing.
:::

Event store providers that support GDPR erasure implement this interface. Ask the store for the
capability with `GetService(typeof(IEventStoreErasure))` — do **not** test its type. A store is
commonly reached through a decorator, whose own interface list is fixed when it is compiled while the
capabilities of the store it wraps are known only at run time, so `is IEventStoreErasure` reports the
decorator rather than the chain beneath it. A decorator answers this probe on behalf of the store it
wraps, and one that cannot honour erasure over its inner store answers `null` rather than claiming it:

```csharp
if (eventStore.GetService(typeof(IEventStoreErasure)) is IEventStoreErasure erasure)
{
    var count = await erasure.EraseEventsAsync(
        aggregateId: "user-12345",
        aggregateType: "UserProfile",
        erasureRequestId: requestId,
        cancellationToken);

    logger.LogInformation("Erased {Count} events for aggregate {AggregateId}", count, "user-12345");
}
```

### Data-subject hashing (`IDataSubjectHasher`)

GDPR components pseudonymize data-subject identifiers through the injected
`IDataSubjectHasher` service, so plain-text IDs are never stored in erasure request
tables or audit logs. The default implementation (`HmacDataSubjectHasher`) uses a
**keyed HMAC-SHA-256** with a required secret **pepper** — a keyed one-way hash, not a
plain SHA-256 digest. The pepper (held apart from the data) defeats offline
brute-forcing of low-entropy identifiers; because the scheme is one-way and
deterministic, the same ID always maps to the same token for match-and-erase, but the
token cannot be reversed to recover the ID.

The pepper is **required** and validated at startup: if it is missing or shorter than
`DataSubjectHashingOptions.MinimumPepperLength` (32 characters) the host **fails closed**
with an `OptionsValidationException` rather than pseudonymizing with a weak key. Supply
it from your secret manager / KMS — never a literal in source:

```csharp
using Excalibur.Compliance.Erasure;

// Registered automatically by AddGdprErasure / AddLegalHoldService / AddDataInventoryService.
// You only need to configure the required pepper:
builder.Services.Configure<DataSubjectHashingOptions>(o =>
    o.Pepper = builder.Configuration["Gdpr:DataSubjectPepper"]); // high-entropy secret, ≥ 32 chars

// Resolve/inject IDataSubjectHasher where you need a stable pseudonym:
public sealed class MyService(IDataSubjectHasher hasher)
{
    public string Pseudonymize(string dataSubjectId) => hasher.HashDataSubjectId(dataSubjectId);
}
```

### Implementing Custom Event Store Erasure

If you have a custom event store, implement `IEventStoreErasure` alongside your `IEventStore`:

```csharp
public class MyEventStore : IEventStore, IEventStoreErasure
{
    public async Task<int> EraseEventsAsync(
        string aggregateId,
        string aggregateType,
        Guid erasureRequestId,
        CancellationToken cancellationToken)
    {
        // Replace event payloads with tombstone markers
        // Retain the stream and event metadata for referential integrity
        var count = await ReplacePayloadsWithTombstone(aggregateId, aggregateType, cancellationToken);

        // Log the erasure for audit
        await RecordErasureAudit(aggregateId, erasureRequestId, count, cancellationToken);

        return count;
    }

    public async Task<bool> IsErasedAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken)
    {
        return await CheckForTombstoneMarker(aggregateId, aggregateType, cancellationToken);
    }
}
```

:::tip Key Design Decision

Event store erasure uses **tombstoning** (replacing payloads) rather than **deletion** (removing events). This preserves the event sequence and version numbers for other aggregates that may reference these events, while making the personal data irrecoverable.
:::

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Grace period | Leave `DefaultGracePeriod` at 72 hours for production, and raise `MinimumGracePeriod` above its 1-hour default so no caller can request a shorter window |
| Legal holds | Always check before execution |
| Audit logging | Enable for compliance evidence |
| Key rotation | Use separate keys per data subject |
| Verification | Generate certificates for all completions |
| Data inventory | Maintain accurate data location registry |

## Compliance Mapping

| GDPR Article | Feature |
|--------------|---------|
| Article 17(1) | ErasureService.RequestErasureAsync() |
| Article 17(2) | Cascade to all data locations via DataInventory |
| Article 17(3)(b) | LegalHoldService for compliance obligations |
| Article 17(3)(e) | LegalHoldService for legal claims |

## Next Steps

- [Data Masking](data-masking.md) - PII/PHI protection
- [Audit Logging](audit-logging.md) - Compliance audit trails

## See Also

- [Data Masking](data-masking.md) - PII/PHI protection in logs and outputs
- [Compliance Overview](index.md) - Compliance framework capabilities
- [Audit Logging](audit-logging.md) - Tamper-evident audit logging with hash chain integrity
