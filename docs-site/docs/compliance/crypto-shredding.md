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
- Crypto-shredding builds on the compliance encryption and data-subject-hashing subsystems. The following must already be registered (typically by your compliance-encryption and GDPR-erasure setup):
  - `IKeyManagementProvider` and `IKeyManagementAdmin` — the key-management subsystem that mints and destroys key material
  - `IDataSubjectHasher` — pseudonymizes raw subject identifiers before they reach the key store
- Familiarity with [GDPR Erasure](./gdpr-erasure.md)

## Registration

Register the crypto-shredding services with `AddCryptoShredding`:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddCryptoShredding();
```

`AddCryptoShredding()` registers three scoped services (via `TryAdd`, so a consumer registration wins):

| Service | Role |
|---------|------|
| `ISubjectKeyManager` | Resolves the key handle protecting a subject's data in a given retention scope, minting one if it does not exist. It does **not** destroy keys — destruction is an erasure operation, and `IErasureService` owns it so legal holds are honoured first. |
| `IFieldEncryptor` | Encrypts a single value under the key selected for a subject and a retention scope; decrypts an envelope. |
| `SubjectFieldCryptor` | Encrypts/decrypts all `[PersonalData]` fields of a record under the key selected for the record's data subject and the aggregate type it is stored inside. |

Because the key-management provider, key-management admin, and data-subject hasher are dependencies (not registered here), call `AddCryptoShredding()` alongside your compliance-encryption and data-subject-hashing setup.

## Marking Personal Data

Crypto-shredding is annotation-driven. Two attributes (both in `Excalibur.Compliance`) declare what to protect and whose key protects it:

- **`[DataSubjectId]`** marks the property whose value identifies the data subject the record belongs to. Exactly one property per record should carry it; if more than one does, the first property discovered is used and the others are ignored.
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
- **`DecryptFieldsAsync`** reverses the process. A field whose subject key the key provider **states is destroyed** decrypts to `null` (a tombstone), leaving the rest of the record intact so an aggregate still loads with its non-personal fields. A key that merely cannot be found is not a tombstone — see [the read path](#fail-closed-field-protection) below.
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
- **Read path (`DecryptAsync`) degrades open only for a genuinely shredded subject:** it returns `null` only when the key-management provider **states**, through `IKeyDestructionStatusProvider`, that the material behind that envelope's key version is irrecoverable. Every other failure throws.

:::danger A key that cannot be found is not an erasure

A `null` asserts that the data was lawfully crypto-shredded, so it is produced from an affirmative statement of destruction and never from the absence of a key. The two are indistinguishable at the read: a backend with a recovery window reports a deleted-but-fully-restorable key exactly as it reports one that never existed. **Azure Key Vault answers a soft-deleted key with 404 for its entire retention period — 90 days by default — while a single `recover` call brings it back.** Reading absence as erasure would tell you an erasure request was discharged over data that is still there.

The question is asked of the **key version** the envelope names, not the key handle, because a rotation can leave a handle holding one retired version and one live one, and only an all-versions-destroyed handle is a destroyed handle.

Three failures therefore throw rather than returning a tombstone:

- **No registered provider supports the envelope's algorithm** — a configuration fault, never a lawful erasure.
- **The envelope's key version cannot be used, but the provider reports its material as recoverable** — `EncryptionErrorCode.KeyNotFound`. The data is still there; recover the key rather than working around the error.
- **The key provider does not implement `IKeyDestructionStatusProvider`** — it has no way to state destruction, so nothing may be concluded from the failed read. The exception names the capability. All five key providers in the box implement it; a custom provider must, for an erased subject's aggregate to load.
:::

## Per-Subject Keys and Erasure

`ISubjectKeyManager` owns the per-subject key lifecycle:

- **`GetOrCreateKeyAsync(subjectId, retentionScope, ct)`** returns the key handle protecting that subject's data in that scope, minting a new cryptographically-random key (via the key-management provider) if it does not yet exist.

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
