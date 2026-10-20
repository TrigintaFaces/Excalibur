---
title: Decryption requires the caller's tenant
description: IFieldEncryptor.DecryptAsync and SubjectFieldCryptor.DecryptFieldsAsync now take the owning tenant from the caller, because an envelope cannot establish who is allowed to read it.
---

# Decryption requires the caller's tenant

**This is a compile error on upgrade, and that is deliberate.** An overload keeping the old shape would
have let every existing call site keep compiling and keep reading across tenants.

## What changed

| Member | Before | After |
|---|---|---|
| `IFieldEncryptor.DecryptAsync` | `(EncryptedData envelope, CancellationToken)` | `(EncryptedData envelope, string expectedTenantId, CancellationToken)` |
| `SubjectFieldCryptor.DecryptFieldsAsync` | `(object record, CancellationToken)` | `(object record, string? expectedTenantId, CancellationToken)` |

## Why the envelope could not answer it

The read path used to take the envelope as self-describing: it names the key, so decrypt it. But the
envelope is **input**, and on a read path it is frequently input the caller did not author — a document
fetched by id, a row joined in, a projection replayed. A component that answers *may I read this* from a
field inside the thing being read answers *yes* to anything shaped correctly.

So ownership now comes from outside the ciphertext: the authorized request, or trusted stored-record
metadata. **Do not pass `envelope.TenantId`.** That reinstates the defect with extra steps and the
comparison will always succeed.

## What the check does

In `FieldEncryptor.DecryptAsync`, before any provider lookup and before the destruction ledger is
consulted:

- a blank `expectedTenantId` is refused;
- a blank `envelope.TenantId` is refused;
- a mismatch is refused, compared with `StringComparison.Ordinal` — **no trimming, no case folding**.

The refusal is a `CryptographicException`. It is raised **ahead of the ledger** on purpose: were it
raised after, an erased envelope belonging to another tenant would come back as a lawful `null`
tombstone, and the caller would record someone else's erasure as its own.

On `SubjectFieldCryptor.DecryptFieldsAsync` the parameter is nullable, which is narrower than it looks:
**`null` is tolerated only while no encrypted personal-data field is encountered.** It is not a bypass —
it fails at the first field that actually needs a decision.

## What you change

```csharp
// Before
await cryptor.DecryptFieldsAsync(record, ct);

// After — the tenant comes from the authorized request
await cryptor.DecryptFieldsAsync(record, currentRequest.TenantId, ct);
```

A deployment that is not multi-tenant passes the untenanted sentinel rather than a blank, since a blank
is refused:

```csharp
using Excalibur.Dispatch;

await cryptor.DecryptFieldsAsync(record, KeyedTenantPartition.Untenanted.TenantId, ct);
```

Hosts that only register through `AddCryptoShredding()` and never call the cryptor or implement
`IFieldEncryptor` themselves have nothing to change.

## Data written before 2026-10-01 will not decrypt

Envelopes carry a tenant only because the write path began stamping one in the release that fixed
cross-tenant erasure. **An envelope written before that carries no `TenantId`, and a blank envelope
tenant is refused** — so those fields now raise `CryptographicException` instead of returning plaintext.

This is a **refusal, not data loss**: the ciphertext and the key are both untouched, and the failure is
loud. But it is the one part of this change that a recompile will not surface, so before upgrading a
deployment that holds crypto-shredded data written by `10.0.0-alpha.14` or earlier, confirm whether any
stored envelope has a null or empty tenant. If it does, that data needs its envelopes re-stamped with
their owning tenant, which is a backfill against your own store — the framework cannot infer an owner
it was never told.

## See also

- [Crypto-shredding](../compliance/crypto-shredding.md) — the current read and write contract
- [A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md) — the other
  change to this read path
