# GDPR Compliance Sample

**Location:** `samples/06-security/GdprCompliance/`

> **Canonical dispatch pipeline :** The erasure endpoints now go
> through the same L3 command → handler → event → projection pipeline used by
> the other real-world samples:
>
> ```
> POST /customers/{id}/erase
>   -> IDispatcher.DispatchAsync(EraseCustomerCommand)
>   -> EraseCustomerHandler
>        -> IErasureService.RequestErasureAsync(...)
>        -> clear [PersonalData] fields on the customer row
>        -> IDispatcher.DispatchAsync(CustomerErasedEvent)
>   -> IEventHandler<CustomerErasedEvent> (CustomerPrivacyView projection)
>
> GET /customers/{id}/privacy-view  <- reads the projected CustomerPrivacyView
> ```
>
> The tombstone endpoint follows the same shape with `TombstoneCustomerCommand`
> + `CustomerTombstonedEvent`. The in-place mutation that previously lived in
> the Minimal-API lambda is now owned by the command handler.

End-to-end GDPR sample demonstrating:

1. **`[PersonalData]` attributes** on domain entities
2. **`IErasureService`** - the Data Subject Right-to-Erasure (Article 17) API
3. **`AddGdprErasure(options => ...)`** registration
4. **Erase-in-place** and **tombstone** patterns

## What it shows

### 1. `[PersonalData]` annotations (Domain/Customer.cs)

```csharp
public sealed class Customer
{
    public Guid Id { get; set; }                          // not PII

    [PersonalData(
        Category = PersonalDataCategory.ContactInfo,
        Purpose = "Transactional communication",
        LegalBasis = LegalBasis.Consent,
        RetentionDays = 1095)]
    public string Email { get; set; } = string.Empty;     // PII

    [PersonalData(
        Category = PersonalDataCategory.Identity,
        IsSensitive = true,
        Purpose = "KYC / regulatory compliance",
        LegalBasis = LegalBasis.LegalObligation,
        RetentionDays = 2555)]
    public string? NationalIdNumber { get; set; }         // sensitive PII
}
```

The framework's auto-discovery uses these markers for:

- Field-level encryption at rest
- Log/error-message masking via `IDataMasker`
- Automated cascade-erasure scope computation
- Retention policy enforcement

### 2. Erasure registration (Program.cs)

```csharp
builder.Services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod   = TimeSpan.FromHours(72);
    options.EnableAutoDiscovery  = true;
    options.RequireVerification  = true;
});

builder.Services.AddInMemoryErasureStore();       // swap for SQL Server in prod
builder.Services.AddComplianceMonitoring();
```

### 3. Erase-in-place vs Tombstone

| Pattern | When to use | Effect |
|---------|-------------|--------|
| Erase-in-place | You want to preserve the aggregate row | Every `[PersonalData]` field is nulled out, identity is preserved |
| Tombstone | You want downstream systems to see "user deleted" | Row is replaced with a marker record: same ID, all PII fields `<erased>` |

Both paths go through `IErasureService.RequestErasureAsync(...)` to produce an
audit-log entry, a unique tracking ID, and a scheduled execution window
(respecting the grace period).

## Run locally

```bash
dotnet run

# 1. Read a customer (PII is masked in the response)
curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111

# 2. Issue a right-to-erasure request (dispatches EraseCustomerCommand)
curl -X POST http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/erase

# 3. Re-read to verify the fields are erased
curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111

# 4. Read the projected privacy view (populated by IEventHandler<CustomerErasedEvent>)
curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/privacy-view

# 5. Or request a tombstone on the other customer
curl -X POST http://localhost:5000/customers/22222222-2222-2222-2222-222222222222/tombstone
curl http://localhost:5000/customers/22222222-2222-2222-2222-222222222222/privacy-view
```

### File layout

```
GdprCompliance/
├── Commands/
│   ├── ErasureCommands.cs             // EraseCustomerCommand, TombstoneCustomerCommand
│   └── ErasureHandlers.cs             // IActionHandler<T> for each command
├── Domain/
│   ├── Customer.cs                    // [PersonalData]-annotated entity
│   ├── ICustomerRepository.cs         // in-memory customer store
│   └── Events/
│       └── CustomerErasureEvents.cs   // CustomerErasedEvent / CustomerTombstonedEvent
├── Projections/
│   ├── CustomerPrivacyView.cs         // read model populated by the event handlers
│   ├── ICustomerPrivacyViewStore.cs   // in-memory projection store
│   └── CustomerPrivacyProjectionHandlers.cs  // IEventHandler<T> projections
└── Program.cs                         // DI wiring + endpoints
```

## Framework components used

| Component | Package | Purpose |
|-----------|---------|---------|
| `[PersonalData]` | `Excalibur.Compliance.Abstractions` | Per-field PII marker |
| `PersonalDataCategory` / `LegalBasis` | `Excalibur.Compliance.Abstractions` | GDPR classification enums |
| `IErasureService` | `Excalibur.Compliance.Abstractions` | Right-to-erasure (Article 17) API |
| `ErasureRequest` / `ErasureResult` | `Excalibur.Compliance.Abstractions` | Request / response DTOs |
| `AddGdprErasure(...)` | `Excalibur.Compliance` | DI entry point + options + validator |
| `AddInMemoryErasureStore()` | `Excalibur.Compliance` | In-memory erasure tracking (demo) |
| `AddComplianceMonitoring()` | `Excalibur.Compliance` | Audit log, metrics, alerts |

## Production notes

- Swap `AddInMemoryErasureStore` for `AddSqlServerErasureStore(...)` in production.
- For encrypted-at-rest PII, also register `IEncryptionProvider` + an active key
  in your KMS provider of choice.
- Retention enforcement (`RetentionDays`) is driven by
  `RetentionEnforcementBackgroundService` once registered.
