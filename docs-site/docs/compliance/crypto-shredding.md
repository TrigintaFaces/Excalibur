---
sidebar_position: 5
title: Crypto-Shredding
description: Per-subject field-level crypto-shredding — encrypt each data subject's personal data under a dedicated key, then erase the key to render it unrecoverable.
---

# Crypto-Shredding

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
| `ISubjectKeyManager` | Binds each data subject to a dedicated key over the key-management subsystem; creates and destroys per-subject keys. |
| `IFieldEncryptor` | Encrypts/decrypts a single value under a subject's key. |
| `SubjectFieldCryptor` | Encrypts/decrypts all `[PersonalData]` fields of a record under the record's data subject's key. |

Because the key-management provider, key-management admin, and data-subject hasher are dependencies (not registered here), call `AddCryptoShredding()` alongside your compliance-encryption and data-subject-hashing setup.

## Marking Personal Data

Crypto-shredding is annotation-driven. Two attributes (both in `Excalibur.Compliance`) declare what to protect and whose key protects it:

- **`[DataSubjectId]`** marks the property whose value identifies the data subject the record belongs to. At most one property per record may carry it.
- **`[PersonalData]`** marks each property that holds personal data to be encrypted under that subject's key. `[PersonalData]` also carries policy metadata (`Category`, `IsSensitive`, `Purpose`, `LegalBasis`, `RetentionDays`, `MaskInLogs`, `ExcludeFromErrors`).

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
        await cryptor.EncryptFieldsAsync(record, ct);
        // ... persist record ...
    }

    public async Task RevealAsync(CustomerRecord record, CancellationToken ct)
    {
        // ... load record ...
        await cryptor.DecryptFieldsAsync(record, ct);
    }
}
```

- **`EncryptFieldsAsync`** resolves the subject id from the `[DataSubjectId]` property, obtains (or mints) that subject's key via `ISubjectKeyManager`, and replaces each `[PersonalData]` field value with a subject-bound ciphertext envelope. `string` and `byte[]` personal-data properties are supported.
- **`DecryptFieldsAsync`** reverses the process. A field whose subject key has been **destroyed** decrypts to `null` (a tombstone), leaving the rest of the record intact so an aggregate still loads with its non-personal fields.
- A record with **no resolvable `[DataSubjectId]` value** is left untouched — per-subject protection is additive over any existing at-rest encryption.

Under the hood, `IFieldEncryptor` mints per-subject keys as **AES-256-GCM** keys. Key material is always produced by the key-management provider's cryptographically-secure RNG — never from `Guid` or `Random`. Raw subject identifiers are pseudonymized through `IDataSubjectHasher` before they are used as key handles, so they never reach the key store.

## Fail-Closed Field Protection

:::warning The field cryptor fails closed — it never silently persists plaintext

A type that carries `[DataSubjectId]` has **declared that it holds personal data**. If `SubjectFieldCryptor` resolves **zero** `[PersonalData]` properties for such a type, `EncryptFieldsAsync` **throws** an `EncryptionException` rather than encrypting nothing and persisting the record in plaintext. A zero-field plan on a data-subject type means the classification annotations were lost (for example, trimmed away) — and quietly writing unencrypted personal data would be a breach. The write path refuses.

This fail-closed guarantee holds under trimming/AOT. The type-plan lookup roots the record's public properties with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]`, so the `[PersonalData]`/`[DataSubjectId]` annotations survive trimming; the throw-on-empty-plan check is the defense-in-depth backstop behind that rooting.
:::

The read path is deliberately asymmetric:

- **Write path (`EncryptAsync`) fails closed:** any encryption failure throws; it never returns plaintext or a partially-protected value.
- **Read path (`DecryptAsync`) degrades open only for a genuinely shredded subject:** when the envelope's key has been destroyed, it returns `null`. Every other failure still throws. In particular, if the subject's key is still present but **no registered provider supports the envelope's algorithm**, decryption throws an `EncryptionException` — a configuration fault is never misreported as a lawful erasure (it refuses to fabricate an erasure tombstone for data that is actually still recoverable).

## Per-Subject Keys and Erasure

`ISubjectKeyManager` owns the per-subject key lifecycle:

- **`GetOrCreateKeyAsync(subjectId, ct)`** returns the subject's key handle, minting a new cryptographically-random key (via the key-management provider) if the subject does not yet have one.

**Destroying a subject key is an erasure operation, and the erasure service owns it.** There is deliberately no direct "destroy this subject's key" call on the key manager: key destruction is irreversible, so it must not be reachable on a path that bypasses the legal-hold check. Route every crypto-shred through `IErasureService`, which evaluates legal holds *before* any key is destroyed and records the destruction on the completion certificate.

```csharp
// Registration. The key-shred-only opt-in is REQUIRED when no data-inventory
// discovery source is registered — without it the erasure registration is
// rejected at startup, because a completion certificate must never be issued
// over coverage that was never verified.
builder.Services.AddInMemoryErasureStore();
builder.Services.AddGdprErasure(options => options.KeyShredOnlyErasure = true);
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

`RequestErasureAsync` files the request and returns immediately. The returned `ErasureResult` carries `ScheduledExecutionTime` (when the configured grace period elapses) and, if the subject is under a legal hold, the `BlockingHold` that is holding the erasure. The hosted scheduler registered by `AddGdprErasure` executes the request when the grace period expires and no hold applies; destroying the subject's key is part of that execution. The subject's own key handle is destroyed **even when the data inventory does not enumerate it**, so the crypto-shred does not depend on inventory coverage.

Because erasure is a single key destruction, every field and record encrypted under that subject's key becomes undecryptable at once — no per-record mutation is required for the encrypted values. Once destroyed, all data encrypted under that key is permanently unrecoverable.

## What's Next

- [GDPR Erasure](./gdpr-erasure.md) — Right-to-be-forgotten orchestration: grace period, coverage model, contributors, and compliance certificates
- [Compliance Overview](./index.md) — Compliance framework capabilities
