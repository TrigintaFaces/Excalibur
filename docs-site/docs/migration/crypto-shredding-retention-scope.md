---
sidebar_position: 16
title: Crypto-Shredding Takes a Retention Scope
description: Four compile errors for anyone who implements ISubjectKeyManager or IFieldEncryptor, calls SubjectFieldCryptor, or constructs EventStoreErasureContributor by hand.
---

# Crypto-Shredding Takes a Retention Scope

A deployment can now declare that an aggregate type must survive an erasure, because the law requires the
data it holds to be kept — see
[GDPR Erasure](../compliance/gdpr-erasure.md#when-the-law-requires-you-to-keep-the-record-declare-a-retention).
Keeping a record readable after its data subject is erased means its personal fields cannot share the key
the erasure destroys, so **the key lookup now takes the scope the value sits in.**

**This page matters to you only if you write code against those types.** If you register through
`AddCryptoShredding()` and `AddGdprErasure(...)` and let the framework do the encrypting, nothing here
affects you: the framework passes the scope on your behalf, and a deployment that declares no retention
behaves exactly as before.

## What changed

Four members. **Each is a compile error, not a silent behaviour change** — your build tells you, which is
the failure mode we would choose for something whose wrong answer is destroyed data.

| Member | Before | After |
|---|---|---|
| `ISubjectKeyManager.GetOrCreateKeyAsync` | `(string subjectId, CancellationToken)` | `(string subjectId, RetentionScope retentionScope, CancellationToken)` |
| `IFieldEncryptor.EncryptAsync` | `(string subjectId, ReadOnlyMemory<byte> plaintext, CancellationToken)` | `(string subjectId, RetentionScope retentionScope, ReadOnlyMemory<byte> plaintext, CancellationToken)` |
| `SubjectFieldCryptor.EncryptFieldsAsync` | `(object record, CancellationToken)` | `(object record, string? aggregateType, CancellationToken)` |
| `EventStoreErasureContributor` | two constructors, the shorter with defaulted parameters | one constructor, ending in a required `IErasureRetentionRegistry?` |

`IFieldEncryptor.DecryptAsync` and `SubjectFieldCryptor.DecryptFieldsAsync` are **unchanged**. An envelope
records which key protects it, so the read path needs nothing from the caller.

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
// Not stored inside an aggregate
await cryptor.EncryptFieldsAsync(record, aggregateType: null, ct);

// Stored inside an aggregate — required if "SalesRecord" is under a declared retention
await cryptor.EncryptFieldsAsync(record, aggregateType: "SalesRecord", ct);
```

Passing `null` reproduces the old behaviour exactly. **It is the wrong answer only where the aggregate
type is under a declared retention**, and there it is the answer that destroys the record: the fields go
under the subject's own key, the erasure destroys that key, and the retained record survives with its
personal fields no longer decryptable.

## If you implement `ISubjectKeyManager` or `IFieldEncryptor`

Accept the scope and **use it to select the key**. A conforming implementation resolves a *distinct*
handle per `(subject, scope)` pair, so that destroying the subject's own handle leaves data written under
a retention scope readable.

```csharp
public async ValueTask<string> GetOrCreateKeyAsync(
    string subjectId,
    RetentionScope retentionScope,
    CancellationToken cancellationToken)
{
    var subjectHandle = _hasher.HashDataSubjectId(subjectId);

    // A scope naming an aggregate gets its own handle; everything else uses the
    // subject's own handle, which is the one an erasure destroys.
    var handle = retentionScope.IsInAnAggregate
        ? $"{subjectHandle}:{retentionScope.AggregateType}"
        : subjectHandle;

    return await _keys.GetOrCreateAsync(handle, cancellationToken);
}
```

:::danger Ignoring the parameter compiles, and it is the failure this change exists to prevent

An implementation that accepts `retentionScope` and resolves the same handle regardless satisfies the
compiler and passes any test that does not erase. The defect appears only at the erasure: the retained
record's key is destroyed along with the subject's, and a record the law required you to keep survives as
ciphertext nobody can open. Where you compose the handle from strings, compose it **injectively** — a
subject identifier containing your separator must not be able to produce another pair's handle.
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

A deployment that declares no retention behaves exactly as it did: one key handle per subject, and every
aggregate the mapping returns is tombstoned. The scope parameter changes nothing until an aggregate type
is declared retained.

**Where you do declare one, declare it before the data is written.** The scope is applied at encryption
time, so fields already written under the subject's own handle are destroyed with it. There is no backfill
and none is possible once the key is gone.
