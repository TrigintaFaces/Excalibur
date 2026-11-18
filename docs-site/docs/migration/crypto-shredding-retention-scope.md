---
sidebar_position: 16
title: Crypto-Shredding Takes a Retention Scope
description: Current tenant-qualified subject-key and retention-scope contracts for crypto-shredding consumers and implementors.
---

# Crypto-Shredding Takes a Retention Scope

A deployment can now declare that an aggregate type must survive an erasure, because the law requires the
data it holds to be kept — see
[GDPR Erasure](../compliance/gdpr-erasure.md#when-the-law-requires-you-to-keep-the-record-declare-a-retention).
Keeping a record readable after its data subject is erased means its personal fields cannot share the key
the erasure destroys, so **the key lookup now takes the scope the value sits in.**

**This page matters to you if you call or implement these contracts directly.** With
`AddCryptoShredding()` and `AddGdprErasure(...)`, the framework supplies the registered subject-key
manager and carries the actual aggregate scope on your behalf. Configure a resolved tenant and the
required key provider and mint/destruction ledgers; registration is not a substitute for durable stores.
The signatures below reflect the current tenant-qualified, generation-bearing contract, not only the
original retention-scope change.

## What changed

These members changed from the earlier retention-free surface. **Signature changes require consumer
updates**, while the ownership and key-lifecycle obligations below also need behavioral verification.

| Member | Before | After |
|---|---|---|
| `ISubjectKeyManager.GetOrCreateKeyAsync` | `(string subjectId, CancellationToken)` | `(TenantId tenant, string subjectId, RetentionScope retentionScope, CancellationToken)` returning `ValueTask<SubjectKey>` |
| `IFieldEncryptor.EncryptAsync` | `(string subjectId, ReadOnlyMemory<byte> plaintext, CancellationToken)` | `(string subjectId, RetentionScope retentionScope, ReadOnlyMemory<byte> plaintext, CancellationToken)` |
| `SubjectFieldCryptor.EncryptFieldsAsync` | `(object record, CancellationToken)` | `(object record, string? aggregateType, CancellationToken)` |
| `EventStoreErasureContributor` | two constructors, the shorter with defaulted parameters | one constructor, ending in a required `IErasureRetentionRegistry?` |

:::danger Superseded — the read path DID change, later

This page previously said: *"`IFieldEncryptor.DecryptAsync` and `SubjectFieldCryptor.DecryptFieldsAsync`
are **unchanged**. An envelope records which key protects it, so the read path needs nothing from the
caller."* That was true of **this** change and is **false now**. Both members gained a required
`expectedTenantId`, and the stated reason — that the envelope carries everything the read path needs —
is the exact premise the later fix reverses: an envelope is attacker-supplied input, so its own tenant
field cannot establish who is allowed to read it.

See **[Decryption requires the caller's tenant](decryption-requires-the-callers-tenant.md)**.

:::

## `RetentionScope` is a value, and never an absence

```csharp
using Excalibur.Compliance;

// The value is not stored inside an aggregate. This is also `default`, so a scope
// that arrives unset resolves to the subject's own key — the key an erasure
// destroys — rather than to one it would not reach.
RetentionScope.NotInAnAggregate

// The value is stored inside this aggregate type, named exactly as the event
// store records it. Matching is ordinal.
RetentionScope.For("SalesRecord")
```

It is a struct rather than a `string?` for two reasons, and both are about the direction a mistake
travels:

- **A nullable string carried three meanings that resolved identically** — *not in an aggregate*, *the
  caller did not know*, and *nobody set it* — and resolved toward destruction, which is the direction
  that cannot be walked back.
- **The subject identifier travelling beside it is also a string.** Passed as bare strings, transposing
  the two compiles, runs, and encrypts every subject's data under one shared handle. No diagnostic at any
  tier reports that. A `RetentionScope` cannot be passed where an identifier is expected.

## If you call `SubjectFieldCryptor`

Add the aggregate type the record is stored inside, or `null` when it is not inside an aggregate:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Compliance.CryptoShredding;

public static class FieldEncryptionExamples
{
    // Alternative A: the record is NOT stored inside an aggregate.
    public static ValueTask EncryptDetachedAsync(SubjectFieldCryptor cryptor, object record, CancellationToken ct) =>
        cryptor.EncryptFieldsAsync(record, aggregateType: null, ct);

    // Alternative B: this record is stored inside the actual recorded SalesRecord aggregate.
    // Choose the matching alternative once; do not encrypt the same fields twice.
    public static ValueTask EncryptSalesRecordAsync(SubjectFieldCryptor cryptor, object record, CancellationToken ct) =>
        cryptor.EncryptFieldsAsync(record, aggregateType: "SalesRecord", ct);
}
```

Pass `null` only when the record is not inside an aggregate. For aggregate data, always pass its actual
recorded aggregate type, even when no retention is currently declared. Current writes give every
aggregate obligation class its own key; the erasure, not the write, decides which declared obligations
must be retained. Omitting the actual scope can put retained data under a key the erasure destroys.

## If you call or implement `ISubjectKeyManager` or `IFieldEncryptor`

Prefer the framework's registered `ISubjectKeyManager` rather than reimplementing its key derivation.
Pass the owner captured from an authorized request or trusted stored-record metadata, not from a supplied
ciphertext envelope. The same owner and subject must identify the same key namespace on write and erasure;
two tenants with the same subject string must remain independent.

This example calls the public contract and returns its complete result unchanged:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Compliance;
using Excalibur.Dispatch;

public static class SubjectKeyExample
{
    public static ValueTask<SubjectKey> ResolveAsync(
        ISubjectKeyManager keyManager,
        TenantId capturedOwner,
        string subjectId,
        CancellationToken cancellationToken) =>
        keyManager.GetOrCreateKeyAsync(
            capturedOwner, subjectId, RetentionScope.For("SalesRecord"), cancellationToken);
}
```

`SubjectKey` carries both `KeyId` and the material's `Generation`. Do not return only a handle or look up
the generation in a later operation: the material can change between calls. A missing generation is not
proof that a subject was erased; callers needing crypto-shred tombstones must fail closed. The framework
field encryptor applies these checks and binds the captured tenant into the ciphertext envelope.

Keep the deployment's keyed `IDataSubjectHasher` and secret pepper stable across services, restarts and
restores. Replacing that identity function or rotating its pepper does not re-key existing records; it
makes their persisted identities and handles unreachable. Do not replace the framework's qualified
handle with `HashDataSubjectId(subjectId)` or concatenate tenant/scope strings yourself.

A custom implementation must honor the same resolved-owner, obligation-class, write-ahead mint-ledger
and key/generation contract. Record mint intent durably before creating key material, and preserve the
handle/generation pair in destruction evidence and envelope reads. The erasure uses its persisted request
owner; it must not substitute whatever tenant happens to be ambient in a background worker.

:::danger Ignoring ownership or scope compiles, but destroys the isolation guarantee

Accepting `tenant` and `retentionScope` while ignoring either can still satisfy the method signature.
Verify both provisioning orders for two tenants sharing one subject: the survivor's fields must decrypt
after the other's erasure, and the erasing tenant's own fields must tombstone. Also verify retained and
non-retained obligation classes independently. Distinct handles alone do not prove an erasure reached
the right keys. These obligations apply to custom implementations as well as framework composition.
:::

## If you construct `EventStoreErasureContributor` by hand

Two constructors are gone. Both are replaced by one:

```csharp
new EventStoreErasureContributor(
    eventStoreErasure,
    mapping,
    logger,
    snapshotStore,        // null leaves snapshots untouched
    serviceProvider,      // null leaves projections untouched
    retentions);          // IErasureRetentionRegistry? — null declares none
```

**The registry parameter is required rather than defaulted, deliberately.** The contributor's action is to
tombstone every event of an aggregate. One built without a registry cannot know which aggregate types are
retained, so it tombstones them — the contributor fails *open*, toward destruction. A defaulted parameter
lets a call site acquire that behaviour by saying nothing; a required one makes the deployment state which
case it is in. Pass `null` if you genuinely declare no retentions, or resolve
`IErasureRetentionRegistry` from your container.

If you register through `AddGdprErasure(...)` and the event-sourcing erasure extensions, dependency
injection supplies all six arguments and you have nothing to change.

## After upgrading

Current writes partition key handles by owner, subject and obligation class, whether or not a retention
is already declared. With no applicable retention, erasure can destroy every recorded class for that
owner; it must preserve another owner's keys and classes covered by that owner's legal obligations.

Supply the correct scope when encrypting and follow the [retention declaration guidance](../compliance/gdpr-erasure.md#when-the-law-requires-you-to-keep-the-record-declare-a-retention)
before erasure. A declaration cannot repair fields written under an incorrect scope or restore material
already destroyed. This page promises no migration or backfill for unsupported earlier envelope formats.
