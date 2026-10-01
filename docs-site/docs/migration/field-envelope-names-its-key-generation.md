---
title: Field ciphertext now names the generation of key material it was written under
sidebar_label: Field envelope names its key generation
description: The field-encryption envelope gained a key generation and a format version. Ciphertext written by an earlier prerelease is refused rather than read. Decrypt it before you upgrade.
---

# Field ciphertext now names the generation of key material it was written under

**Read this before upgrading if you have field-encrypted personal data at rest.** The envelope format
changed, and ciphertext written by an earlier prerelease is **refused** by the read path. Decrypt it on your
current version first, then upgrade and re-encrypt.

This affects **crypto-shredded field encryption only** — values written through `IFieldEncryptor` or
`SubjectFieldCryptor`. Encrypted audit logs, and the outbox, inbox and store encryption decorators, are
untouched: their envelopes never carried a generation, and the associated data still binds nothing where no
generation is supplied.

## What changed, and why it could not be done compatibly

Crypto-shredding gives each data subject their own key and erases the subject by destroying it. A read of an
erased subject's field returns `null` — a tombstone — rather than throwing, so the surrounding aggregate
still loads. That tombstone is a statement that data was lawfully erased, so it is produced only when the
key-management provider **states** the material is destroyed.

The key handle is derived from the subject, which makes it **stable, and re-mintable**. Destroy a subject's
key, let one ordinary write provision another at the same handle, and the version ordinal restarts from the
beginning. The identifiers the envelope carried were the handle and that ordinal — so after a re-mint, both
of them designate live material, and both answer "not destroyed" **truthfully, about material the reader is
not holding.** The erased subject's older fields then read as live, the decryption fails its authentication
tag, and the failure surfaces as corrupted data rather than as the erasure it is.

No value already in the envelope distinguishes the two cases, which is why the envelope had to change. The
fix is an identifier minted per provisioning, from a cryptographic random source, that a later provisioning
at the same handle cannot reproduce:

```csharp
public sealed record EncryptedData
{
    public const int CurrentFormatVersion = 2;

    public string? KeyGeneration { get; init; }   // new
    public int FormatVersion { get; init; }       // new, defaults to CurrentFormatVersion
    // ...
}
```

The generation is bound into the AES-GCM associated data, so it cannot be rewritten by a holder of the
ciphertext, and the destruction question now keys on it.

## Existing field ciphertext is refused, not read

An envelope written before this change declares no format version and carries no generation. The field read
path refuses it:

```
This field envelope declares format version 1, and this build writes and reads version 2.
The material behind an envelope of the earlier layout cannot be identified, so a destroyed
subject cannot be distinguished from one whose key was provisioned again afterwards.
The envelope is refused rather than read.
```

The failure is an `EncryptionException` with `ErrorCode` `InvalidCiphertext`. **It is deliberately loud.**
Reading such an envelope was the only alternative, and it would mean reading it under associated data that
binds no generation — which authenticates, and so would leave the original confusion in place for every
value written before the upgrade, silently.

There is **no read path for the earlier layout and none is planned.** The generation it needs was never
written down, so no amount of reading recovers it.

### Migrating data you need to keep

Do this **before** you upgrade, while your current version can still read the ciphertext:

1. On your current package version, decrypt every field-encrypted value whose subject has **not** been
   erased, and hold the plaintext as you would any other personal data in flight.
2. Upgrade.
3. Re-encrypt. The new envelope is written automatically; no API call differs.

Values belonging to **already-erased** subjects need nothing. Their keys are destroyed, so the plaintext is
already unrecoverable and the refusal replaces a tombstone with a loud read failure for data that is gone
either way.

If you cannot take an outage for step 1, run it as a background pass that reads and rewrites each value on
the old version, and upgrade once it drains — the two layouts never need to coexist in a single process.

## API changes

Three contracts changed. Each is a compile error, not a silent behaviour change.

### `ISubjectKeyManager.GetOrCreateKeyAsync` returns the key and its generation

```csharp
// before
ValueTask<string> GetOrCreateKeyAsync(string subjectId, RetentionScope retentionScope, CancellationToken cancellationToken);

// after
ValueTask<SubjectKey> GetOrCreateKeyAsync(string subjectId, RetentionScope retentionScope, CancellationToken cancellationToken);

public readonly record struct SubjectKey(string KeyId, string? Generation);
```

A caller that only wanted the handle reads `.KeyId`. The generation comes from the same call deliberately:
the provisioning that mints the material is the only place that knows which generation it is, and reading it
back separately would be a second lookup that a concurrent re-mint can invalidate.

### `IKeyDestructionStatusProvider` gained a generation-scoped member

```csharp
Task<bool> IsKeyDestroyedAsync(string keyId, string generation, CancellationToken cancellationToken);
```

The handle-scoped and version-scoped members are unchanged and still used — a handle holding one destroyed
version and one live version is not a destroyed handle, and the version-scoped question remains the right
one where the caller holds a version rather than an envelope.

**Only hosts that implement this interface themselves are affected.** Every provider we ship implements the
member. What your implementation owes:

- **Return `true` when the handle holds a generation other than the one asked about.** This is the case the
  member exists for, and the one that is easy to get wrong: the handle is live and looks perfectly healthy.
- **Return `true` when the handle is absent**, consistent with the existing overloads — "destroyed, or never
  existed".
- **Return `false` only for the generation whose material is currently live** at that handle.
- **Throw `ArgumentException` for a null or empty `keyId` or `generation`.** Answering a malformed
  identifier at all risks answering it "destroyed", which the caller reads as a lawful erasure.
- **Destroy whatever records the generation when you destroy the key.** If your provider keeps the
  generation in a sidecar record, that record must die with the key — otherwise the next provisioning at the
  same handle reads it back and hands new material the destroyed generation's identifier, which reintroduces
  exactly the confusion this change removes.

### `KeyMetadata` gained `Generation`

```csharp
public string? Generation { get; init; }
```

Supply a value that is **unique to each provisioning** of a key and **stable for its lifetime**. What that is
depends on the backend, and every shipped provider uses something the backend already guarantees rather than
inventing a parallel record where it can:

| Provider | Generation |
|---|---|
| In-memory | a cryptographically random value minted with the key |
| AWS KMS | the CMK identifier, which is new for each provisioning |
| Azure Key Vault | the key's opaque version string |
| HashiCorp Vault | a cryptographically random value in a sidecar document, deleted with the key |
| Multi-region | forwarded from the region that answered |

`Generation` is nullable, so a provider that cannot supply one still compiles. It will not work for
field encryption: the read path refuses an envelope naming no generation, so values written through such a
provider cannot be read back. If that is your situation, say so in your provider's documentation rather than
leaving it to be discovered at the first read.

## Verifying your implementation

`KeyManagementProviderConformanceTestKit` gained three arms for the new member. The load-bearing one asks
about a generation the backend has never held **at a live key handle** and requires `true` — a provider that
answers the handle instead of the generation fails it. See
[Conformance kits gained arms](conformance-kit-arms-added.md) for the wrappers to add.

Azure Key Vault's generation mapping is **not covered by a conformance run** — there is no suite for that
provider, so its behaviour here is reasoned from the backend's documented versioning and is unverified by us.
Treat it accordingly if you depend on it.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is minted, used and destroyed
- [Key destruction is stated, not inferred](key-destruction-is-stated-not-inferred.md) — why absence is never read as erasure
- [Provisioning a key is no longer a rotation](create-key-if-absent.md) — the member that mints a subject's key
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
