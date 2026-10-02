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
6. **Crypto-shredding** - personal fields encrypted at rest under a per-subject key, read back as `null`
   once that key is destroyed

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

### 5. Crypto-shredding: erasing by destroying the key, not by overwriting the row

**Two different erasure mechanisms are in this sample, and they must not be confused.**

| | `/erase`, `/tombstone` | `/crypto-shred/walkthrough` |
|---|---|---|
| What is stored | the plaintext, until it is cleared | ciphertext, from the first write |
| What erasure does | overwrites each `[PersonalData]` field | destroys one key; the stored bytes are untouched |
| What a read returns after | the cleared value | `null`, the erasure tombstone |
| Cost of erasing | one write per copy, in every store | one key destruction, however many copies exist |
| Reaches a store you forgot to register | no | yes, if the data was encrypted under that key |

The second is `Excalibur.Compliance`'s crypto-shredding. `POST /crypto-shred/walkthrough` demonstrates it
on `SupportNote` (`Domain/SupportNote.cs`), a record stored outside the event store.

#### The guarantee, in one sentence

**A personal field decrypts to `null` only where a durable destruction ledger holds a row for the key
generation that field's envelope names.**

Not "the key is missing". Absence cannot be the test, and the reason is concrete: a key vault with a
recovery window reports a soft-deleted key as absent for the whole window, while a single recover call
brings it back. A tombstone derived from absence would tell a data subject their data was erased while it
was one API call from being readable -- and nothing downstream could tell. So the read asks a ledger
instead, and a ledger row is written **only after** an irreversible destruction this deployment performed
has already completed. **The row's existence *is* the destruction statement.**

A key that merely cannot be reached makes the read **throw**. "We lost it" surfaces as an error; it never
surfaces as "we erased it".

#### What the walkthrough prints

```jsonc
{
  // Both values are truncated here on purpose. At full length they are 64 and 32 hexadecimal
  // characters, which is indistinguishable from key material to a secret scanner -- and ours
  // flagged this block when it carried them whole. Yours will differ on every run: the handle is
  // a keyed digest of your tenant and data subject, and the generation is minted at random.
  "keyHandle": "F4883C01...DC88A5",       // 64 hex characters in the real response
  "keyGeneration": "e807755c...c36264",   // 32 hex characters, randomly minted

  "storedAtRest": {                   // what the database holds
    "marker": "EXCR1:",               // EncryptedFieldBinding.StringEnvelopePrefix
    "fullName": "EXCR1:RVhDUnsiQ2lwaGVy... (494 chars total)",
    "subject": "Replacement wing mirror"            // not annotated, so plaintext
  },

  "beforeErasure": {                  // the same stored bytes, decrypted
    "ledgerSaysGenerationDestroyed": false,
    "fullName": "Erin Vance"
  },

  "erasure": { "status": "Completed" },

  "afterErasure": {                   // the SAME stored bytes, read again
    "ledgerSaysGenerationDestroyed": true,
    "ledgerSaysUnrelatedGenerationDestroyed": false, // the control
    "fullName": null,                                // the erasure tombstone
    "subject": "Replacement wing mirror"             // still loads
  }
}
```

Read it as four claims, each checkable from the response rather than taken on trust:

- `storedAtRest` is ciphertext. The marker is what distinguishes a stored envelope from a value that was
  never encrypted; bare Base64 cannot express that difference.
- `beforeErasure` decrypts to the plaintext, so the `null` later is not a record that was never readable.
- `afterErasure.ledgerSaysGenerationDestroyed` is the **reason** for the `null`, printed beside it.
- `ledgerSaysUnrelatedGenerationDestroyed` is the control: a key generation minted during the request and
  never destroyed answers `false` through the same call, so the `true` above is a lookup, not a constant.

#### Marking a record

Two attributes, and both are needed. `[DataSubjectId]` names the property identifying *whose* key
protects the record; `[PersonalData]` names each field encrypted under it.

```csharp
public sealed record SupportNote
{
    [DataSubjectId]
    public string CustomerId { get; set; } = string.Empty;

    [PersonalData(Category = PersonalDataCategory.Identity)]
    public string? FullName { get; set; }

    [PersonalData(Category = PersonalDataCategory.ContactInfo)]
    public string? EmailAddress { get; set; }

    // Not annotated -- stays plaintext, still reads after erasure.
    public string Subject { get; set; } = string.Empty;
}
```

A type carrying `[DataSubjectId]` but no `[PersonalData]` field is **refused**, as is one whose declared
identifier is null or blank: both states mean personal data with no key to protect it, so proceeding would
persist plaintext. A type with no `[DataSubjectId]` is left untouched -- per-subject protection is
additive over whatever at-rest encryption already applies.

#### Registration

Three calls, each a separate decision:

```csharp
// 1. The encryption provider registry and an AES-256-GCM provider over your key management.
//    Crypto-shredding registers neither and will not start without them.
builder.Services.AddEncryption(encryption => encryption
    .UseInMemoryKeyManagement("sample-inmemory", options => options.AutoGenerateDefaultKey = true)
    .SetAsPrimary("sample-inmemory"));

// 2. An erasure store. It also supplies IKeyDestructionLedger, from the same instance.
builder.Services.AddInMemoryErasureStore();

// 3. Crypto-shredding itself -- AFTER the event store, because encryption is the innermost decorator.
builder.Services.AddEventSourcingCryptoShredding();
```

**On a host with an event store, use `AddEventSourcingCryptoShredding()`, not the bare
`AddCryptoShredding()`.** The bare call registers the services without wiring the store, and a start-up
gate refuses that combination by name: annotated event fields would persist as plaintext and destroying a
subject key would erase nothing. `AddCryptoShredding()` on its own is for a host that encrypts fields it
manages itself, with no event store.

**Neither call registers the key-destruction ledger, and that omission is deliberate.** The ledger comes
from whatever performs the destruction. Register no ledger at all and the host **refuses to start**,
naming `IKeyDestructionLedger` -- it does not fail later, on the first read of an encrypted field.

**A deployment that encrypts personal data at rest and never destroys a subject key** -- an encrypted
event store with no erasure subsystem -- calls **`AddCryptoShreddingWithoutErasure()`** instead of
registering an erasure store. That supplies a ledger holding no rows, so nothing ever tombstones; in such
a deployment that is the true answer rather than a stand-in. Registering both is also refused at start-up:
every ledger registration is a `TryAdd`, so in one of the two orders an always-false ledger would stand in
front of a real erasure store, and the deployment would erase while never reporting it.

#### Two boundaries worth knowing before you rely on this

- **The walkthrough also gives its subject an event-sourced `CustomerProfile`, and that is not
  decoration.** This host registers its data locations on the event store, and an erasure cannot reach
  `Completed` while a *registered* location goes undischarged. A subject with no event-store data has its
  key destroyed and its request still ends `Failed`, naming the locations nobody erased. That is the
  coverage model working, not a bug.
- **The `SupportNote` lives in a store this host never registered as a data location**, so no contributor
  visits it and the certificate makes no claim about it -- yet its fields become unreadable anyway,
  because the key is gone. That is the value of crypto-shredding and also its boundary: it reaches
  ciphertext written under the destroyed key wherever that ciphertext lives, and it is **not** a
  substitute for a data inventory.

### 6. Multi-tenancy: whose key is it?

`POST /crypto-shred/two-tenants` answers the question the single-tenant walkthrough cannot: **a tenant is
part of a subject key's identity, not a filter applied around it.**

Data-subject identifiers are yours, taken from your own entities -- an email address, a customer number,
an external account id -- so in an ordinary multi-tenant deployment **they repeat across tenants.** If a
key handle were constant in the tenant, two tenants holding a record for the same identifier would share
one key, and either one erasing that data subject would destroy it for both. The second tenant would then
read `null` over data nobody asked to erase, while this framework reported a lawful erasure of it.

The endpoint writes the same `[DataSubjectId]` value as three different tenants, prints all three key
handles, erases the data subject in one of them, and reads all three records back:

```jsonc
{
  "sharedDataSubjectId": "90a767d4-...",   // one identifier, held by all three
  "allKeyHandlesDiffer": true,             // the whole point
  "erasure": { "tenant": "untenanted", "status": "Completed" },
  "tenants": [
    { "tenant": "untenanted",      "thisTenantErased": true,
      "emailAddressBefore": "erin@untenanted.example",
      "emailAddressAfter":  null,                              // tombstoned
      "ledgerSaysGenerationDestroyed": true },
    { "tenant": "tenant-contoso",  "thisTenantErased": false,
      "emailAddressAfter":  "erin@tenant-contoso.example",     // untouched
      "ledgerSaysGenerationDestroyed": false },
    { "tenant": "tenant-fabrikam", "thisTenantErased": false,
      "emailAddressAfter":  "erin@tenant-fabrikam.example",    // untouched
      "ledgerSaysGenerationDestroyed": false }
  ]
}
```

Both halves are on that response and both are needed. The other tenants still decrypting is the **safety**
half; the erasing tenant's own field reading `null` -- with the ledger row that is the only thing entitling
it to -- is the **liveness** half. An erasure that had silently stopped working altogether would satisfy
the safety half on its own.

#### Three layers, three different answers to "which tenant?"

This is the part worth copying carefully, because the answers are deliberately not the same:

| Layer | Takes its tenant from | Why |
|---|---|---|
| **Write** (`SubjectFieldCryptor`, `IFieldEncryptor`) | the **ambient** tenant | the writer is operating as a tenant, so that is the authoritative one. Open it with `TenantContextHolder.BeginScope(tenantId)` |
| **Erasure** | the tenant **recorded against the request** | a background processor's ambient scope is no evidence of whose data a request was about |
| **Read** (decrypt) | the **stored envelope** | a projection rebuild or a background replay has no ambient tenant, and a reader's tenant must never be mistaken for the writer's |

Two consequences follow, and neither is obvious from the call signatures:

- **An erasure must be *filed* by the tenant that owns it**, inside that tenant's
  `TenantContextHolder.BeginScope`. The erasure store records a request against the **ambient** tenant --
  so that a caller cannot file an erasure against a tenant it is not operating as -- which makes
  `ErasureRequest.TenantId` a declaration rather than the control. File it outside a scope and the request
  is recorded against the untenanted partition, which derives a different key handle, destroys material no
  data was written under, and still reports `Completed`.
- **The status must be *polled* inside the same scope.** A tenant-scoped request is readable only by the
  tenant that owns it, so a status read taken from outside reports nothing and looks like a stall.

#### Host registration

A host that varies the ambient tenant needs a tenant context that observes it, which the single-tenant
default deliberately does not:

```csharp
builder.Services.AddTenantContext(o => o.RequireTenant = true);
builder.Services.AddSingleton<ITenantContext>(TenantContextHolder.AmbientContext);
```

Both lines earn their place. A resolving tenant context while `RequireTenant` is `false` is **refused at
start-up**, because that combination applies the single-tenant schema while routing two tenants through
the same keyed rows. And `TenantContextHolder.AmbientContext` is used rather than the context
`AddTenantContext()` installs on its own, because outside a scope it resolves the reserved **untenanted
partition** instead of leaving the tenant unresolved -- which matters here, since much of this host runs
outside any scope: the start-up seeding, the erase/tombstone endpoints, and the background erasure
scheduler. The untenanted partition is a partition this framework names, and reads and writes there are
confined to it exactly as a named tenant's are.

> **Scope of this walkthrough.** It erases in the **untenanted** partition, which is the one partition the
> background erasure scheduler can carry a request for: the scheduler establishes no ambient tenant, so a
> request filed by a *named* tenant is found by its scan and then reported `Request not found` by every
> tenant-scoped store call that follows it. Key *separation* -- the property this endpoint exists to show --
> is demonstrated for all three tenants, named ones included. Driving an erasure to completion from a named
> tenant needs the scheduler to establish the scope its own request recorded, and there is no public API to
> drive one by hand in the meantime.

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

# 7. Run the crypto-shredding walkthrough: a personal field encrypted at rest under its data
#    subject's own key, then read back as null once that key is destroyed
curl -X POST http://localhost:5000/crypto-shred/walkthrough

# 8. Run the multi-tenant walkthrough: three tenants holding a record for the SAME data-subject
#    id under three different keys, then one tenant's erasure -- which leaves the other two readable
curl -X POST http://localhost:5000/crypto-shred/two-tenants
```

### File layout

```
GdprCompliance/
├── Commands/
│   ├── ErasureCommands.cs             // EraseCustomerCommand, TombstoneCustomerCommand
│   └── ErasureHandlers.cs             // IActionHandler<T> for each command
├── CryptoShredding/
│   ├── CryptoShredWalkthroughEndpoint.cs  // POST /crypto-shred/walkthrough
│   └── TwoTenantCryptoShredEndpoint.cs    // POST /crypto-shred/two-tenants
├── Domain/
│   ├── Customer.cs                    // [PersonalData]-annotated entity
│   ├── SupportNote.cs                 // [DataSubjectId] + [PersonalData]: the crypto-shredded record
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
| `[DataSubjectId]` | `Excalibur.Compliance.Abstractions` | Names the property identifying whose key protects a record |
| `SubjectFieldCryptor` | `Excalibur.Compliance` | Encrypts/decrypts a record's `[PersonalData]` fields in place |
| `ISubjectKeyManager` / `SubjectKey` | `Excalibur.Compliance.Abstractions` | Resolves (and mints) a subject's key handle and generation |
| `IKeyDestructionLedger` | `Excalibur.Compliance.Abstractions` | The durable record of destroyed key generations -- the sole basis for a tombstone |
| `EncryptedFieldBinding` | `Excalibur.Compliance.Abstractions` | The stored-envelope marker distinguishing ciphertext from plaintext |
| `AddEncryption(...)` | `Excalibur.Compliance` | Encryption provider registry + the AES-256-GCM provider |
| `AddEventSourcingCryptoShredding()` | `Excalibur.EventSourcing` | Per-subject crypto-shredding, with the event store wired for at-rest field encryption |
| `AddCryptoShreddingWithoutErasure()` | `Excalibur.Compliance` | Declares a deployment that encrypts at rest and destroys no subject key |
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
