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
5. **`AddErasureRetention(...)`** - keeping a record the law requires you to keep, through an erasure

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
    options.DefaultGracePeriod  = TimeSpan.FromHours(72);
    options.EnableAutoDiscovery = true;

    // Certificates are signed with HMAC-SHA256 and an unsigned one is never written, so a host with
    // no key configured completes the erasure and produces no evidence of it. Source this from your
    // secret manager in production.
    options.Retention.SigningKey = signingKeyFromSecretManager;
});

// Data-subject identifiers are pseudonymized with a keyed HMAC that needs a secret pepper; the framework
// fails closed at startup if one is not configured. In production, source it from your secret manager / KMS
// (stored apart from the erasure data) — never a literal in source.
builder.Services.Configure<DataSubjectHashingOptions>(o =>
    o.Pepper = builder.Configuration["Gdpr:DataSubjectPepper"] ?? "sample-demo-pepper-not-a-secret-change-me-0123456789");

builder.Services.AddInMemoryErasureStore();       // swap for SQL Server in prod

// Erasure consults legal holds before destroying anything and refuses to start without a hold
// service -- a hold that is never checked is a hold that does not exist.
builder.Services.AddInMemoryLegalHoldStore();
builder.Services.AddLegalHoldService();

// The discovery source. Without it (or an explicit ErasureOptions.KeyShredOnlyErasure opt-in) the
// host refuses to start, because a completion certificate would attest coverage nothing verified.
builder.Services.AddInMemoryDataInventoryStore();
builder.Services.AddDataInventoryService();

// Carries out scheduled erasures once the grace period elapses. IErasureService files the request;
// nothing executes it without this.
builder.Services.AddErasureScheduler();

builder.Services.AddComplianceMonitoring();
```

Each of those refusals is deliberate. Erasure is irreversible and its certificate is relied on as a
compliance record, so every gate fails the host at startup rather than issuing an unprovable
certificate at runtime.

### 3. Erase-in-place vs Tombstone

| Pattern | When to use | Effect |
|---------|-------------|--------|
| Erase-in-place | You want to preserve the aggregate row | Every `[PersonalData]` field is nulled out, identity is preserved |
| Tombstone | You want downstream systems to see "user deleted" | Row is replaced with a marker record: same ID, all PII fields `<erased>` |

Both paths go through `IErasureService.RequestErasureAsync(...)` to produce an
audit-log entry, a unique tracking ID, and a scheduled execution window
(respecting the grace period).

### 4. When the law requires you to keep the record: a declared retention

Erasure tombstones whole aggregates. Where personal data is unavoidably embedded in a transaction
record -- the buyer's name on a vehicle sale -- destroying the aggregate destroys the transaction, and
tax, warranty, recall and AML obligations attach to that record. Article 17(3) withholds the right to
erasure to the extent processing is necessary to comply with a legal obligation.

`AddErasureRetention` declares the aggregate types this deployment must keep. An erasure then skips
them: the record survives whole and readable, and the certificate names the retention, its basis, its
justification and its period.

```csharp
builder.Services.AddErasureRetention(new ErasureRetention
{
    AggregateType   = nameof(SalesRecord),          // matched ordinally against the stored type name
    TenantId        = TenantScope.UntenantedSentinel, // this deployment is not multi-tenant
    Basis           = LegalHoldBasis.LegalObligation,
    Justification   = "Vehicle sales records are kept for six years under the tax code's "
                    + "record-keeping requirement and for product-recall traceability.",
    RetentionPeriod = TimeSpan.FromDays(365 * 6),
});
```

**The retention unit is the aggregate type, whole.** There is no field-level erasure inside a retained
record, and that limit is deliberate: an obligation to keep a record attaches to the *record*, and a
partly-erased record is a mutated record with no evidentiary value -- which is the entire reason it was
being kept. So every data subject named inside a retained type is covered by it while the obligation is
in force.

Three things this asks of you in return:

- **Declare it before the data is written.** Naming a type here changes which key its personal fields
  are encrypted under, and that is what keeps the record readable afterwards. Events written earlier
  were encrypted under the subject's own key, which the erasure destroys.
- **The retained record must carry its own copy of what it needs.** `SalesRecord` holds the buyer's
  name and address on its own events. A record that reached its buyer through a reference into
  `CustomerProfile` would break the moment that profile was erased -- the reference survives and
  resolves to a tombstone. A retention protects the types you name and nothing they point at.
- **Write the justification about the record, not about a person.** Every data subject named on a
  retained record is shown that text, including people the statute was not written with in mind.

Re-linking is yours. If the customer returns, a new `CustomerProfile` is created; matching it to the
surviving sales record is a business decision on the identifiers you hold. The framework does not
attempt it and keeps no hidden link, because a hidden link would be the re-identification the erasure
was supposed to remove.

`POST /retention/walkthrough` runs one erasure for a subject whose data spans both aggregate types and
reports the outcome of each:

```jsonc
{
  "status": "Completed",
  "erasable": {                       // nothing declares a retention for CustomerProfile
    "aggregateType": "CustomerProfile",
    "survived": false,
    "fullName": "", "emailAddress": ""
  },
  "retained": {                       // SalesRecord is declared, so it is whole and readable
    "aggregateType": "SalesRecord",
    "survived": true,
    "buyerName": "Dana Okoro",
    "buyerAddress": "14 Kingsway, Leeds",
    "vehicleIdentificationNumber": "VIN-4Y1SL65848Z",
    "salePrice": 24500
  },
  "certificate": [{                   // the retention is named, with its basis and period
    "dataCategory": "SalesRecord",
    "basis": "LegalObligation",
    "reason": "Personal data of this data subject held in 'SalesRecord' was not erased and lawfully
               persists there for the period stated. Vehicle sales records are kept for six years ...",
    "retentionPeriod": "2190.00:00:00"
  }]
}
```

Note that `SalesRetentionMapping` returns **both** aggregates, including the retained one. That
interface answers "where is this person's data?", which is a question of fact; whether a type may be
destroyed is a separate, legal question, answered by the declaration. Withholding the sales record from
the mapping instead would produce the same surviving record *silently* -- the erasure would never learn
the data was there, so the certificate would not name it and the data subject would never be told their
personal data lawfully persists.

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

# 6. Run the declared-retention walkthrough: one erasure across an erasable aggregate and a
#    retained one, reporting what is left of each and what the certificate says
curl -X POST http://localhost:5000/retention/walkthrough
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
├── Retention/
│   ├── RetentionEvents.cs             // CustomerProfileRegistered, VehicleSold
│   ├── RetentionAggregates.cs         // CustomerProfile (erasable), SalesRecord (retained)
│   └── SalesRetentionMapping.cs       // IAggregateDataSubjectMapping + its subject index
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
| `AddErasureScheduler(...)` | `Excalibur.Compliance` | Carries out scheduled erasures |
| `ErasureRetention` / `LegalHoldBasis` | `Excalibur.Compliance.Abstractions` | Declared retention + its Article 17(3) ground |
| `AddErasureRetention(...)` | `Excalibur.Compliance` | Declares the aggregate types an erasure must not destroy |
| `ErasureCertificate` / `ErasureException` | `Excalibur.Compliance.Abstractions` | Signed erasure record; retentions named on it |
| `IAggregateDataSubjectMapping` | `Excalibur.EventSourcing` | Maps a data subject to their aggregates |
| `UseEventStoreErasure<T>()` | `Excalibur.EventSourcing` | Opts the event store into erasure |

## Production notes

- Swap `AddInMemoryErasureStore` for `AddSqlServerErasureStore(...)` in production.
- For encrypted-at-rest PII, also register `IEncryptionProvider` + an active key
  in your KMS provider of choice.
- `RetentionDays` does not delete anything by itself. Declare the types whose retention is
  enforced with `AddRetentionPolicies<T>()`, then `AddRetentionEnforcement()` runs a periodic pass
  that hands those types' retention periods -- and no others -- to your registered
  `IRetentionContributor` implementations, which perform the deletion. Enabling enforcement with
  nothing declared fails at startup.
