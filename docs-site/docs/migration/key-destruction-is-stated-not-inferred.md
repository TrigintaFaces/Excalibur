---
sidebar_position: 17
title: A Destroyed Key Is Stated, Never Inferred
description: One new interface member for anyone who implements IKeyDestructionStatusProvider, and one behaviour change on the crypto-shredding read path — a key that merely cannot be found no longer reports as a lawful erasure.
---

# A Destroyed Key Is Stated, Never Inferred

A `null` from `IFieldEncryptor.DecryptAsync` is not "no data". It is a statement that the field was
lawfully crypto-shredded — that the key protecting it is gone and the ciphertext can never be opened
again. See [GDPR Erasure](../compliance/gdpr-erasure.md).

**The read path used to produce that statement from the absence of a key**, and absence is not
destruction. A key-management backend with a recovery window reports a deleted-but-fully-restorable key
exactly as it reports one that never existed. Azure Key Vault answers a soft-deleted key as *not found*
for its entire retention period — 90 days by default — while a single `recover` call brings it back. For
that whole window, every read of the affected subject's personal fields returned a tombstone claiming the
data was erased, over data that was not erased and that anyone with vault permissions could restore.

The fix is one sentence: **the tombstone is now produced only on an affirmative statement of destruction
from the key provider, asked of the exact key version the ciphertext names.**

## What changed

| | Before | After |
|---|---|---|
| `IKeyDestructionStatusProvider` | one member, key-scoped | a second member, **version-scoped** |
| `IFieldEncryptor.DecryptAsync`, key gone and provider says destroyed | `null` tombstone | `null` tombstone — unchanged |
| `IFieldEncryptor.DecryptAsync`, key unreachable but recoverable | `null` tombstone | **throws** `EncryptionException` |
| `IFieldEncryptor.DecryptAsync`, provider cannot state destruction | `null` tombstone | **throws** `EncryptionException` naming the capability |

The five key providers in the box — Azure Key Vault, AWS KMS, HashiCorp Vault, the multi-region provider,
and the in-memory provider — all implement the new member. **A host that registers one of them, and
registers crypto-shredding through `AddCryptoShredding()`, has nothing to change.**

## If you implement `IKeyDestructionStatusProvider`

Add the version-scoped overload. It is a compile error until you do.

```csharp
// The question a read asks: does this backend still hold any recoverable copy of the
// material behind ONE version of this key?
Task<bool> IsKeyDestroyedAsync(string keyId, int version, CancellationToken cancellationToken);
```

Return `true` **only** when no recoverable copy of that version's material remains. Return `false` when
it is live, and also when it is deleted but still inside a recovery window. Throw when you cannot obtain
an answer — a caller must never be able to mistake *could not ask* for *destroyed*.

### The key-scoped overload cannot stand in for it

A rotation leaves a key handle holding more than one version, and a backend that retires versions
individually can leave one version destroyed while a later one still decrypts. Such a handle is **not** a
destroyed handle, so the key-scoped overload answers `false` — correctly — while an envelope naming the
retired version has nothing left to decrypt with. That is why the read path asks the version-scoped
question: a guard must ask the same question, of the same object, as the operation it guards.

Backends differ, and each states its own answer:

- **HashiCorp Vault** trims key versions, removing their material permanently and leaving later versions
  live. The two overloads genuinely disagree.
- **AWS KMS**, as this framework uses it, gives each version its own CMK behind its own durable alias, so
  a version can be destroyed on its own. They genuinely disagree.
- **Azure Key Vault** has no per-version delete: a delete takes the key with every version it holds. The
  two overloads necessarily agree, and its version-scoped answer is its key-scoped answer.

If your backend has no recovery window at all, a version it does not hold is destroyed, and implementing
both overloads is straightforward. **State that as your backend's property — do not let a caller assume
it.**

:::danger Never compose the answer with an absence check

The defect this change fixes was exactly such a composition: *the key lookup returned nothing, therefore
the key is destroyed.* Filling a gap in a key-scoped answer by checking whether a version is *present* at
version granularity reintroduces it one level down. Answer from what the backend says about the
material's recoverability, or throw.
:::

## If your key provider does not implement the capability

Reads of a crypto-shredded subject's fields now **fail** on that provider instead of degrading open. The
exception names `IKeyDestructionStatusProvider` and says to implement it.

**This is deliberate, and it is the loud half of a real trade.** The alternative is a tombstone asserting
a lawful erasure that nothing confirmed — silent, undetectable from outside, and wrong in the one
direction that matters to a data controller answering for an erasure request. A failed read is loud,
diagnosable, and recoverable; a fabricated erasure certificate is none of those. Erasure verification
already refuses to certify a deletion on a provider that cannot confirm destruction, so this makes the
read path agree with the erasure path rather than contradict it.

Implement the capability on your provider to restore degrade-open behaviour.

## If you catch exceptions around field decryption

Two failures are new on this path:

- `EncryptionException` with `ErrorCode = EncryptionErrorCode.KeyNotFound` — the key version behind the
  envelope could not be used, and the provider states its material is still recoverable. **The data is
  still there.** Investigate the key backend: a soft-deleted key that should be live is recovered, not
  worked around.
- `EncryptionException` with no specific error code, whose message names
  `IKeyDestructionStatusProvider` — the provider has no way to answer. This is a configuration gap, not a
  data loss.

Neither replaces a tombstone you were relying on for a genuinely erased subject: that case still returns
`null`.

## After upgrading

Nothing about stored data changes, and no backfill is needed or possible. What changes is which reads
answer `null`: fewer of them, and only the ones a provider will vouch for.

**If you are running against Azure Key Vault with soft-delete enabled and have performed erasures, treat
any past read that returned a tombstone during a key's retention window as unverified.** The key may have
been recoverable at the time. Purge protection plus an elapsed retention window is what makes the
destruction irreversible, and the erasure-verification report is what attests it.
