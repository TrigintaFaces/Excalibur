---
sidebar_position: 5
title: Crypto-Shredding
description: Per-subject field-level crypto-shredding — encrypt each data subject's personal data under a dedicated key, then erase the key to render it unrecoverable.
---

# Crypto-Shredding

:::warning Not legal advice

This page describes technical features that can **support** your compliance work. It is not legal
advice, and it does not establish that any system is compliant with any law, regulation or standard.
You remain responsible for your own compliance assessment, independent testing and validation, and
review by qualified legal and compliance professionals. See the [Compliance Disclaimer](../legal/compliance-disclaimer.md).
:::

Crypto-shredding erases personal data by destroying the key it was encrypted with rather than by overwriting every stored record. `Excalibur.Compliance` implements this at the **field level, per data subject**: each subject's `[PersonalData]` fields are encrypted under a dedicated per-subject key, and destroying that one key renders **every field encrypted under that key** permanently unrecoverable — while every other subject's data stays intact.

This is the encryption/key layer that makes the right-to-erasure cheap: you do not have to visit and mutate every store that holds a subject's annotated fields. You erase the subject's key once, and every ciphertext produced under that key becomes undecryptable.

:::info Scope of this capability
This capability provides **per-subject field-level crypto-shredding**: the per-subject key lifecycle, the field encryptor, and the record-level field cryptor. Applying erasure across every persistence store your application uses (the store-application layer) is a separate concern — see [GDPR Erasure](./gdpr-erasure.md) for the orchestration, grace period, coverage model, and certificates.
:::

:::warning Key destruction does not reach message payloads at rest
The guarantee is bounded by **what was encrypted under the subject's key**. The inbox/outbox at-rest encryption decorators (`AddInboxEncryption()` / `AddOutboxEncryption()`) are **not** keyed per data subject: they encrypt an already-serialized payload under a single context built from the configured purpose and tenant, which carries no data-subject term. Crypto-shredding is therefore the **wrong mechanism** on those surfaces — destroying one subject's key leaves that subject's inbox and outbox payloads recoverable.

Where an erasure guarantee must extend to messages in flight, bound **retention** on those surfaces so the payloads age out, or register an `IErasureContributor` that covers the store kind. See [GDPR Erasure](./gdpr-erasure.md) for the coverage model, which reports such a location as *Uncovered* rather than claiming success over it.
:::

## Before You Start

- **.NET 10.0**
- Crypto-shredding builds on the compliance encryption subsystem. The following must be registered **by you**, and `AddCryptoShredding()` does not register either:
  - `IKeyManagementProvider` and `IKeyManagementAdmin` — the key-management subsystem that mints and destroys key material, from your compliance-encryption setup
  - `IKeyDestructionLedger` — the durable record of destroyed key generations. An erasure store supplies it; a deployment that never erases declares that instead. See [Registration](#registration).
- `IDataSubjectHasher` **is** registered by `AddCryptoShredding()`, so you do not add it. You still supply the hashing pepper, from a secret manager, via `Configure<DataSubjectHashingOptions>`.
- Familiarity with [GDPR Erasure](./gdpr-erasure.md)

## Registration

Register the crypto-shredding services with `AddCryptoShredding`:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddCryptoShredding();
```

Every registration below uses `TryAdd`, so a consumer registration wins.

| Service | Role |
|---------|------|
| `ISubjectKeyManager` | Scoped. Resolves the key handle protecting a subject's data in a given retention scope, minting one if it does not exist. It does **not** destroy keys — destruction is an erasure operation, and `IErasureService` owns it so legal holds are honoured first. |
| `IFieldEncryptor` | Scoped. Encrypts a single value under the key selected for a subject and a retention scope; decrypts an envelope. |
| `SubjectFieldCryptor` | Scoped. Encrypts/decrypts all `[PersonalData]` fields of a record under the key selected for the record's data subject and the aggregate type it is stored inside. |
| `IDataSubjectHasher` | Pseudonymizes raw subject identifiers before they reach the key store. **Registered by this call** — you supply the pepper through `Configure<DataSubjectHashingOptions>`. |
| `IErasureRetentionRegistry` | The declared erasure retentions, which decide whether a value resolves to the subject's own key or to a retained record's key. Registered empty if you declare none, because an *absent* registry would be a second answer to the same question rather than a value meaning "none declared". |

The key-management provider and admin remain your dependencies, so call `AddCryptoShredding()` alongside your compliance-encryption setup.

### A key-destruction ledger is required, and this call does not register one

Reading a crypto-shredded field reports it as erased only on the strength of a durable destruction record, so `IFieldEncryptor` requires `IKeyDestructionLedger`. **A composition without one refuses to start**, rather than failing on the first request that reads an encrypted field.

An erasure store supplies the ledger from the same instance it registers the store from, so a deployment that erases adds nothing extra:

```csharp
services.AddCryptoShredding();

// One of these. Each also registers IKeyDestructionLedger.
services.AddInMemoryErasureStore();     // development
services.AddPostgresErasureStore(/* ... */);
services.AddSqlServerErasureStore(/* ... */);
```

A deployment that encrypts personal data at rest and **never destroys a subject key** — an encrypted event store, inbox or outbox with no erasure subsystem — names that instead of registering an erasure store:

```csharp
// INSTEAD of AddCryptoShredding(), and never alongside an erasure store.
services.AddCryptoShreddingWithoutErasure();
```

That registers a ledger holding no rows, so every generation reports as not destroyed. In a deployment that destroys nothing that is the true answer rather than a stand-in: reads decrypt normally and no tombstone is ever produced. On SQL Server and PostgreSQL, the ledger is two tables the erasure store verifies at startup — see [Erasure destroyed-key record](../migration/erasure-destroyed-key-record.md).

:::danger Two start-up refusals, and the second one is why the opt-out is a separate call

**No ledger at all** — `InvalidOperationException` naming `IKeyDestructionLedger`, the three erasure-store calls, and `AddCryptoShreddingWithoutErasure()` as the alternative.

**`AddCryptoShreddingWithoutErasure()` together with an erasure store** — also refused. The two answer the same question differently, and every ledger registration is a `TryAdd`, so the winner would be whichever call ran first. In one of the two orders an always-false ledger stands in front of a real erasure store: the deployment performs erasures, never tombstones anything, and no component reports a problem. Order-dependence on this question is refused rather than documented.

The opt-out is never a default for the same reason. A ledger that always answers "not destroyed" cannot fabricate an erasure, which is the safe direction — and that is exactly why defaulting to one would be wrong: a deployment that *does* erase, but whose erasure store was never registered, would silently never tombstone anything and read its own erased subjects back in the clear.

A third refusal covers the instrument rather than the wiring: if the container supplies no `IServiceProviderIsService`, registration cannot be probed, so the question was not answered and start-up is refused rather than assumed. The check runs as both an `IHostedService` and an `IStartupPrerequisiteValidator`, so it fires for a host that starts normally and for a consumer who builds a provider and calls `ValidateStartupGates`.
:::

## Marking Personal Data

Crypto-shredding is annotation-driven. Two attributes (both in `Excalibur.Compliance`) declare what to protect and whose key protects it:

- **`[DataSubjectId]`** marks the property whose value identifies the data subject the record belongs to. Exactly one property per record should carry it; if more than one does, the first property discovered is used and the others are ignored. **The value must be unique across your whole deployment, not merely within a tenant.** In every published version the per-subject key is named from this value alone, so two tenants holding one value share one key and either tenant's erasure destroys it for both — embed your tenant identifier in the value. See [the known issue](../known-issues.md#one-tenants-erasure-destroys-another-tenants-data-when-they-share-a-data-subject-id-and-the-victims-read-reports-it-as-lawfully-erased).
- **`[PersonalData]`** marks each property that holds personal data to be encrypted under that subject's key. `[PersonalData]` also carries policy metadata: `Category`, `Purpose` and `LegalBasis` drive erasure, `RetentionDays` drives retention for the types you declare with `AddRetentionPolicies<T>()` (see [GDPR Erasure](./gdpr-erasure.md#retention-enforcement-retentiondays)), and `MaskInLogs` drives masking. **`IsSensitive` is inert — nothing reads it.** It records your own classification; it does not cause the framework to treat the property differently, so do not rely on it as a control.

```csharp
using Excalibur.Compliance;

public class CustomerRecord
{
    [DataSubjectId]
    public string CustomerId { get; set; } = default!;

    [PersonalData(Category = PersonalDataCategory.ContactInfo)]
    public string? Email { get; set; }

    [PersonalData(Category = PersonalDataCategory.Identity)]
    public string? SocialSecurityNumber { get; set; }

    // Not annotated — left as plaintext, still loads after erasure.
    public DateTimeOffset CreatedAt { get; set; }
}
```

### How encryption and decryption flow

`SubjectFieldCryptor` operates on a record **in place**:

```csharp
public sealed class CustomerWriter(SubjectFieldCryptor cryptor)
{
    public async Task ProtectAsync(CustomerRecord record, CancellationToken ct)
    {
        // Encrypts every [PersonalData] field under the key for record.CustomerId.
        // The second argument is the aggregate type this record is stored inside,
        // or null when it is not stored inside an aggregate.
        await cryptor.EncryptFieldsAsync(record, aggregateType: null, ct);
        // ... persist record ...
    }

    public async Task ProtectSalesRecordAsync(SalesRecord record, CancellationToken ct)
    {
        // Naming the aggregate type is what routes the fields to the retained
        // record's own key when "SalesRecord" is under a declared erasure retention.
        await cryptor.EncryptFieldsAsync(record, aggregateType: "SalesRecord", ct);
        // ... persist record ...
    }

    public async Task RevealAsync(CustomerRecord record, CancellationToken ct)
    {
        // ... load record ...
        await cryptor.DecryptFieldsAsync(record, ct);
    }
}
```

- **`EncryptFieldsAsync`** resolves the subject id from the `[DataSubjectId]` property, obtains (or mints) the key for that subject in the given scope via `ISubjectKeyManager`, and replaces each `[PersonalData]` field value with a subject-bound ciphertext envelope. `string` and `byte[]` personal-data properties are supported. **Pass the aggregate type the record is stored inside**, exactly as the event store records it; pass `null` when it is not inside an aggregate. The argument selects nothing but the key, and it matters only where that aggregate type is under a declared erasure retention — see [Per-Subject Keys and Erasure](#per-subject-keys-and-erasure) below.
- **`DecryptFieldsAsync`** takes no scope. An envelope records which key protects it, so the read path needs nothing from the caller.
- **`DecryptFieldsAsync`** reverses the process. A field whose key generation the **destruction ledger holds a record for** decrypts to `null` (a tombstone), leaving the rest of the record intact so an aggregate still loads with its non-personal fields. A key that merely cannot be found is not a tombstone — see [the read path](#fail-closed-field-protection) below.
- **`EncryptFieldsAsync` is idempotent, so retrying a failed write is safe.** It mutates the record in place, so a write that fails *after* it returns leaves you holding a record whose fields are already envelopes. Calling it again on that same instance re-encrypts nothing: a field already carrying an envelope is left as it is, and the stored value still decrypts in a single pass to the original data. You do not have to reload the record or track whether encryption already ran before retrying a transient persistence fault. A field that carries **no** envelope is always encrypted, so a record written before you adopted this capability is protected on its next write rather than skipped.
- A record whose type carries **no `[DataSubjectId]` property** is left untouched — per-subject protection is additive over any existing at-rest encryption. A record that **declares** a data subject whose identifier is null or blank is **rejected with an `EncryptionException`**: it has `[PersonalData]` fields and no key under which to protect them, so proceeding would persist plaintext personal data.

Under the hood, `ISubjectKeyManager` mints per-subject keys as **AES-256-GCM** keys. Key material is always produced by the key-management provider's cryptographically-secure RNG — never from `Guid` or `Random`. Raw subject identifiers are pseudonymized through `IDataSubjectHasher` before they are used as key handles, so they never reach the key store.

## Fail-Closed Field Protection

:::warning The field cryptor fails closed — it never silently persists plaintext

A type that carries `[DataSubjectId]` has **declared that it holds personal data**. If `SubjectFieldCryptor` resolves **zero** `[PersonalData]` properties for such a type, `EncryptFieldsAsync` **throws** an `EncryptionException` rather than encrypting nothing and persisting the record in plaintext. A zero-field plan on a data-subject type means the classification annotations were lost (for example, trimmed away) — and quietly writing unencrypted personal data would be a breach. The write path refuses.

This fail-closed guarantee holds under trimming/AOT. The type-plan lookup roots the record's public properties with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]`, so the `[PersonalData]`/`[DataSubjectId]` annotations survive trimming; the throw-on-empty-plan check is the defense-in-depth backstop behind that rooting.
:::

The read path is deliberately asymmetric:

- **Write path (`EncryptAsync`) fails closed:** any encryption failure throws; it never returns plaintext or a partially-protected value.
- **Read path (`DecryptAsync`) degrades open only for a genuinely shredded subject:** it returns `null` only when `IKeyDestructionLedger` holds a record that the **key generation** this envelope names was destroyed. Every other failure throws.

:::danger A key that cannot be found is not an erasure

A `null` asserts that the data was lawfully crypto-shredded, so it is produced only from a durable record of a destruction this deployment performed — never from the absence of a key. The two are indistinguishable at the read: a backend with a recovery window reports a deleted-but-fully-restorable key exactly as it reports one that never existed. **Azure Key Vault answers a soft-deleted key with 404 for its entire retention period — 90 days by default — while a single `recover` call brings it back.** Reading absence as erasure would tell you an erasure request was discharged over data that is still there.

**The key backend is not asked, at any granularity.** It retains nothing about material it never held, so "we destroyed this" and "this was never here" are the same observation there — permanently and by construction. Narrowing the question from the handle to a version does not fix that; it only moves it. The information that separates the two states existed at one instant and belonged to one actor, the destroyer, so the answer comes from a record that actor wrote.

The record is keyed on the **key generation** the envelope names, not on the handle and not on a version ordinal. A subject's handle is derived from the subject, so an ordinary write after an erasure re-mints material at the same handle and restarts the ordinal — and both then report "not destroyed", truthfully, about material the reader is not holding. A generation is minted once from a CSPRNG and never reused.

**A row's existence is the statement.** There is no status column and no timestamp to compare: a row is written only after an irreversible destruction completed. So a `true` is monotone and may be cached indefinitely; a `false` means "no row yet"; and **failing to read the ledger throws** rather than answering `false`, because a `false` from an outage is indistinguishable in logs and metrics from "asked, and there is no row".

These failures throw rather than returning a tombstone:

- **No registered provider supports the envelope's algorithm** — a configuration fault, never a lawful erasure.
- **The envelope declares an earlier format version, or carries no key generation** — `EncryptionErrorCode.InvalidCiphertext`. The material behind it cannot be identified, so a destroyed subject cannot be told from one whose key was provisioned again.
- **The generation has no ledger row and the decrypt then fails** — including `EncryptionErrorCode.KeyNotFound`. The ledger has already said this generation was not destroyed here, so the read fails loudly and the data may still be recoverable: recover a soft-deleted key that should be live rather than working around the error.
- **The ledger could not be read** — reported as the implementation's own exception, naming the cause, and never as either answer.

A tombstone's claim is scoped to the **envelope**, not the subject. A `true` says this ciphertext's plaintext is unrecoverable; it does not say the subject is erased, because a plaintext copy held elsewhere, or a second ciphertext under a different key, leaves it correct and an erasure claim false.

`IKeyDestructionStatusProvider` still exists and still matters — for a different question, asked by **erasure verification** about a **handle**, once, at erasure completion. A read never consults it. All five key providers in the box implement it, and an erasure that destroys keys on a provider that does not is never certified.
:::

## Per-Subject Keys and Erasure

`ISubjectKeyManager` owns the per-subject key lifecycle:

- **`GetOrCreateKeyAsync(tenant, subjectId, retentionScope, ct)`** returns the key handle protecting that subject's data in that tenant and scope, minting a new cryptographically-random key (via the key-management provider) if it does not yet exist.

The **tenant is part of the key's identity**, not a filter applied around it. A data-subject identifier comes from your own entity, so customer numbers, employee identifiers and e-mail addresses repeat across tenants; a handle that did not carry the tenant would give two such tenants one key, and either tenant's erasure would destroy it for both. A handle is a name in a key store with no row beside it to carry a tenant column, so the name itself carries the tenant.

:::danger This is true of our source and of no published version — check which version you installed
The paragraph above describes the **corrected** behaviour. **In every published version —
`10.0.0-alpha.4` through `10.0.0-alpha.13` — the handle carries no tenant.** Two tenants whose
`[DataSubjectId]` values collide therefore share one key, and either tenant's erasure destroys it for
both. The other tenant then reads `null` over data nobody asked to erase — which the read path on this
page defines as the assertion of a lawful erasure — and the key is gone, so that data is unrecoverable.
It takes no race and no fault: two ordinary writes and one ordinary erasure. The authenticated data
carries no tenant on this path either, so the read produces the tombstone rather than failing loudly.

**Until a release carries the fix, make the `[DataSubjectId]` value globally unique yourself** by
embedding your tenant identifier in it, at **both** the write path and the erasure-request path. It
protects only data written afterwards, repairs nothing already written, and cannot recover a collision
that has already been erased. Changing those values also makes your existing legal holds and registered
data locations unmatchable until you re-place them. See [the full entry in Known
issues](../known-issues.md#one-tenants-erasure-destroys-another-tenants-data-when-they-share-a-data-subject-id-and-the-victims-read-reports-it-as-lawfully-erased).
:::

Single-tenant hosts pass `TenantDefaults.DefaultTenantId` and need no further configuration — `IFieldEncryptor` resolves the ambient tenant for you, and an unconfigured host resolves that same identity. In a multi-tenant host, establish the tenant scope before the write (tenant middleware or `TenantContextHolder.BeginScope`) and **file each erasure request for the tenant whose data it erases** — an erasure for one tenant destroys only that tenant's key.

`RetentionScope` is a value, and it is exactly one of two things — never an absence:

```csharp
using Excalibur.Compliance;

// A value that is not stored inside an aggregate. This is the default, so a value
// that reaches the key manager without a deliberate scope resolves to the subject's
// own key — the key an erasure destroys — rather than to one it would not reach.
var unscoped = RetentionScope.NotInAnAggregate;

// A value stored inside an aggregate, named exactly as the event store records it.
var inSalesRecord = RetentionScope.For("SalesRecord");
```

It is a distinct type rather than a string because the subject identifier travelling beside it is also a
string: passed as bare strings, transposing the two compiles, runs, and encrypts every subject's data
under one shared handle — a mistake no diagnostic would report.

### A subject has one handle, plus one per declared retention their data touches

The "one subject, one key" model holds for every value except one, and the exception is the point of it.

An aggregate type the deployment has declared it must keep through an erasure — see
[erasure retention](./gdpr-erasure.md#when-the-law-requires-you-to-keep-the-record-declare-a-retention) —
resolves to **a handle of its own**. Destroying the subject's handle then leaves the retained record
readable, rather than surviving the erasure as ciphertext nobody can open.

So an erasure destroys the subject's own handle and, **by design, not the retention handles**. A
deployment that declares no retention has exactly one handle per subject, and nothing about it changes.

:::caution The scope you pass at write time is the one that binds

Where the fields were encrypted is what determines whether they survive. Personal fields written under
the subject's own handle **before** a retention was declared are destroyed with that handle, so the
retained record keeps them as unreadable ciphertext. Declare the retention before the data is written.
:::

**Destroying a subject key is an erasure operation, and the erasure service owns it.** There is deliberately no direct "destroy this subject's key" call on the key manager: key destruction is irreversible, so it must not be reachable on a path that bypasses the legal-hold check. Route every crypto-shred through `IErasureService`, which evaluates legal holds *before* any key is destroyed and records the destruction on the completion certificate.

```csharp
// Registration. The key-shred-only opt-in is REQUIRED when no data-inventory
// discovery source is registered — without it the erasure registration is
// rejected at startup, because a completion certificate must never be issued
// over coverage that was never verified.
builder.Services.AddInMemoryErasureStore();
// Erasure also refuses to start with no legal-hold service, so that the check is never
// skipped by accident — see the GDPR erasure guide.
builder.Services.AddInMemoryLegalHoldStore();
builder.Services.AddLegalHoldService();
builder.Services.AddGdprErasure(options => options.KeyShredOnlyErasure = true);

// Erasure works by destroying keys, so a startup gate refuses a volatile key provider
// unless the host says so explicitly. A production host registers a durable provider
// (Azure Key Vault, AWS KMS, HashiCorp Vault) instead of this line.
builder.Services.Configure<KeyDurabilityOptions>(o => o.AllowVolatileKeyProvider = true);

// Filing a request is not executing it. Without the scheduler, requests persist and never run.
builder.Services.AddErasureScheduler();
```

```csharp
public sealed class SubjectEraser(IErasureService erasure)
{
    // Crypto-shred a data subject: files the erasure request that destroys the subject's key.
    public async Task<ErasureResult> EraseAsync(string subjectId, string requestedBy, CancellationToken ct)
    {
        var request = new ErasureRequest
        {
            DataSubjectId = subjectId,
            IdType = DataSubjectIdType.UserId,
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            RequestedBy = requestedBy,
        };

        return await erasure.RequestErasureAsync(request, ct).ConfigureAwait(false);
    }
}
```

`RequestErasureAsync` files the request and returns immediately. The returned `ErasureResult` carries `ScheduledExecutionTime` (when the configured grace period elapses) and, if the subject is under a legal hold, the `BlockingHold` that is holding the erasure. Execution is performed by the erasure scheduler, which is a **separate registration**: call `AddErasureScheduler()` alongside `AddGdprErasure(...)`. Without it, requests are persisted and scheduled and **never execute**. The scheduler runs the request once the grace period expires and no hold applies; destroying the subject's key is part of that execution. The subject's own key handle is destroyed **even when the data inventory does not enumerate it**, so the crypto-shred does not depend on inventory coverage.

Because erasure is a single key destruction, every field and record encrypted under that subject's key becomes undecryptable at once — no per-record mutation is required for the encrypted values. Once destroyed, all data encrypted under that key is permanently unrecoverable. Fields written under a **declared retention's** handle are the exception, and they stay readable for the declared period; the erasure record names each retention that narrowed it, with its legal basis, justification and period.

## What's Next

- [GDPR Erasure](./gdpr-erasure.md) — Right-to-be-forgotten orchestration: grace period, coverage model, contributors, and erasure certificates
- [Compliance Overview](./index.md) — Compliance framework capabilities
