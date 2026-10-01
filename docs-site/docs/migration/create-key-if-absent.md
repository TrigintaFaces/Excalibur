---
title: Provisioning a key is no longer a rotation
sidebar_label: Provisioning is not rotation
description: IKeyManagementProvider gained CreateKeyIfAbsentAsync. Custom providers must implement it. Two concurrent first writes for one data subject no longer retire each other's key.
---

# Provisioning a key is no longer a rotation

`IKeyManagementProvider` gained one member:

```csharp
Task<KeyMetadata> CreateKeyIfAbsentAsync(
    string keyId,
    EncryptionAlgorithm algorithm,
    string? purpose,
    CancellationToken cancellationToken);
```

**If you use the providers we ship, there is nothing to change.** The in-memory, AWS KMS, Azure Key Vault
and HashiCorp Vault providers all implement it, and no method you call has a different signature.

**If you implement `IKeyManagementProvider` yourself, your provider no longer compiles** until you add the
member. That is the whole of the upgrade, and the contract below is what your implementation owes.

## Why the member exists

Crypto-shredding gives each data subject their own key, so minting that key is on the ordinary write path:
the first time a subject's personal data is encrypted, the key has to come into being. There was no
create-if-absent operation, so that path read the key and called `RotateKeyAsync` when the read said
"absent".

`RotateKeyAsync` is create-**or**-rotate. Two steps with no atomicity between them, and a second step that
rotates whenever the key turns out to exist, is enough for two ordinary concurrent first writes for one new
subject to harm each other: the writer that arrives second reads "absent", finds the key present by the
time it writes, takes the rotate branch, and retires the version the first writer just created. Nobody
requested a rotation. No unusual configuration is involved.

What you would have observed is a subject the design gives one key holding a version per racing write, with
the earlier ones marked `DecryptOnly` — and on a backend that rotates by minting a whole new key, one such
key apiece, each of which an erasure then has to locate and destroy. Writes did not fail and no data became
unreadable; the cost was keys nobody asked for and an invariant that no longer held.

## The contract your implementation owes

- **If no key exists at `keyId`, create one** with a single version usable for encryption, and return its
  metadata.
- **If a key already exists, change nothing.** Add no version, alter no version's status, alter no attribute
  of the key. Return what `GetKeyAsync` returns for it.
- **A lost creation race is a no-op that yields the winner's key** — not an error. Two callers that both
  found the key absent must both come away with the same key.
- **Never demote.** This is the one obligation that holds on every backend, and it is the harm the member
  exists to prevent.
- **Throw on failure. Never return `null`.** Failure is not reported through the returned value.

### Atomicity is yours to state, and it varies by backend

A store with a conditional insert creates at most one key, and ours do where they can — the in-memory
provider uses a conditional insert directly, and AWS KMS uses the key alias as the guard, since
`CreateAlias` rejects a name that is taken.

A store whose only create operation *also* adds a version cannot promise that. Azure Key Vault's key-create
endpoint adds a version when the name is taken, has no conditional form, and offers no create-only flag, so
two genuinely concurrent callers there can leave the key with two versions. **That is permitted.** Both
versions remain usable, so no write is lost and no ciphertext names a fenced version; the cost is a wasted
key. What is not permitted, on any backend, is demoting a version that was already there.

If your backend cannot create without adding a version, say so in your provider's documentation rather than
pretending otherwise — and expect the conformance arm that races concurrent provisionings to be the one you
mark skipped, with that reason.

### Returned metadata may not match your arguments

An existing key keeps the algorithm and purpose it was created with, so `algorithm` and `purpose` describe
the key to create and are not applied to one that is already there. A caller that requires a particular
algorithm must check the returned metadata rather than assume.

## A minimal implementation

For a store with a conditional insert, the whole implementation is the insert plus a read of the winner:

```csharp
public Task<KeyMetadata> CreateKeyIfAbsentAsync(
    string keyId,
    EncryptionAlgorithm algorithm,
    string? purpose,
    CancellationToken cancellationToken)
{
    ArgumentException.ThrowIfNullOrEmpty(keyId);

    var candidate = BuildFirstVersion(keyId, algorithm, purpose);

    if (_keys.TryAdd(keyId, candidate))
    {
        return Task.FromResult(DescribeCurrentVersion(candidate));
    }

    // Lost the race: change nothing and describe the key that is actually there.
    return Task.FromResult(DescribeCurrentVersion(_keys[keyId]));
}
```

If your store has no conditional insert, read, then create, and treat a create that fails because the key
appeared in between as the no-op it is — re-read and return the winner's key. Do not let that surface as an
error, and do not fall back to a rotation.

## Verifying your implementation

`KeyManagementProviderConformanceTestKit` gained three arms for this member. Deriving from the kit and
wiring them is how you establish your implementation honours the contract, including under concurrency —
see [Conformance kits gained arms](conformance-kit-arms-added.md) for the wrappers to add and what each arm
holds you to.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is minted, used and destroyed
- [Consumer conformance toolkit](../testing/conformance-toolkit.md) — deriving a suite from a shipped kit
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
