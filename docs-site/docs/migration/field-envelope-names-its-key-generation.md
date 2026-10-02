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
still loads. That tombstone is a statement that data was lawfully erased, so it is produced only where a
durable destruction record says the material behind *this envelope* was destroyed. See
[A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md).

The key handle is derived from the subject, which makes it **stable, and re-mintable**. Destroy a subject's
key, let one ordinary write provision another at the same handle, and the version ordinal restarts from the
beginning. The identifiers the envelope carried were the handle and that ordinal — so after a re-mint, both
of them designate live material, and both answer "not destroyed" **truthfully, about material the reader is
not holding.** The erased subject's older fields then read as live, the decryption fails its authentication
tag, and the failure surfaces as corrupted data rather than as the erasure it is.

No value already in the envelope distinguishes the two cases, which is why the envelope had to change. The
fix is an identifier minted per provisioning **lineage**, from a cryptographic random source, that a later
provisioning at the same handle cannot reproduce:

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
ciphertext, and the destruction record is keyed on it alone.

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

An envelope of the current format version that carries **no** generation is refused too, with the same error
code. That refusal is what makes binding-only-when-present safe: the associated data includes the generation
only when one is supplied, so an envelope with the property stripped would otherwise re-read as the
no-generation form and authenticate correctly — a downgrade anyone could perform by deleting one JSON field.

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

Each is a compile error, not a silent behaviour change, and each lands only on a host that implements the
contract itself.

### `KeyGeneration` is a type, not a `string`

```csharp
public readonly struct KeyGeneration : IEquatable<KeyGeneration>
{
    public bool HasValue { get; }

    public static KeyGeneration Mint();                                  // 128 bits from a CSPRNG
    public static KeyGeneration Parse(string value);                     // 32 hex characters, or throws
    public static bool TryParse(string? value, out KeyGeneration value); // for a read-back that may carry none
    public override string ToString();                                   // the characters that travel and are stored
}
```

**Why a type and not a `string`.** The destruction record is keyed on the generation alone, so a weak
generation is not a local problem: two subjects whose generations collide share one record, and erasing the
first reports the second's live personal data as lawfully erased. A `string` parameter accepts an ordinal, a
backend version identifier, a key ARN, or a value derived from the subject identifier — all of which collide
or are guessable, and none of which any compiler would question. `Mint` uses a CSPRNG and `Parse` rejects
anything that is not exactly 32 hexadecimal characters, so a weak generation stops being discouraged and
becomes unrepresentable.

**What the type cannot enforce, stated plainly because it is your obligation.** It constrains **shape, not
entropy**. A provider may hash a subject identifier into 32 hexadecimal characters and `Parse` will accept
it, because the result is indistinguishable from a mint — and it collides across subjects exactly as a weak
value does, with the consequence above. **Obtain generations from `Mint`, or from a CSPRNG of your own. Never
derive one from the handle, the subject, a counter, or a clock.**

It deliberately stops short of the wire: the envelope and the encryption context carry the generation as a
`string`, because the value is bound into the AES-GCM associated data and persisted inside serialized
envelopes. `ToString()` produces the exact characters that travel, so the round trip is byte-identical.
Compare for equality, never for order, and do not read the characters for meaning.

### `ISubjectKeyManager.GetOrCreateKeyAsync` returns the key and its generation

```csharp
// before
ValueTask<string> GetOrCreateKeyAsync(string subjectId, RetentionScope retentionScope, CancellationToken cancellationToken);

// after
ValueTask<SubjectKey> GetOrCreateKeyAsync(string subjectId, RetentionScope retentionScope, CancellationToken cancellationToken);

public readonly record struct SubjectKey(string KeyId, KeyGeneration? Generation);
```

A caller that only wanted the handle reads `.KeyId`. The generation comes from the same call deliberately:
the provisioning that mints the material is the only place that knows which generation it is, and reading it
back separately would be a second lookup that a concurrent re-mint can invalidate.

### `KeyMetadata` gained `Generation`

```csharp
public KeyGeneration? Generation { get; init; }
```

Supply a value that is **unique to each provisioning lineage** of a key and **stable for that lineage's
lifetime, across rotations**. What that is depends on the backend; every shipped provider mints one through
`KeyGeneration.Mint()` and stores it where the backend will carry it:

| Provider | Generation |
|---|---|
| In-memory | minted with the key, held alongside the key entry |
| AWS KMS | minted once onto an AWS tag, carried forward onto each rotation's new CMK |
| Azure Key Vault | minted once onto a key tag, re-applied after a rotation |
| HashiCorp Vault | minted once into a sidecar marker document, deleted with the key |
| Multi-region | forwarded from the region that answered |

:::danger A per-version backend identifier is **not** a generation
An opaque key-vault version string and a per-version key ARN both change on rotation while looking like
perfectly good identifiers. A rotation extends one material lineage, so the generation must be the **same**
before and after — otherwise every envelope written before that rotation names an identifier no destruction
record will ever hold, and its read fails permanently with no repair available, because the material is gone
and the identifier that would have recorded it is unreadable.
:::

`Generation` is nullable, so a provider that cannot supply one still compiles. It will not work for field
encryption: the read path refuses an envelope naming no generation, so values written through such a provider
cannot be read back. If that is your situation, say so in your provider's documentation rather than leaving
it to be discovered at the first read.

### What a key provider owes

- **Mint the generation from a CSPRNG** — `KeyGeneration.Mint()`, or your own. Never derive it.
- **Keep it stable across a rotation.** One lineage, one generation.
- **Change it when a handle is re-provisioned after a destruction.** If it stayed the same, the destroyed
  generation's record would match the **new** material, and every field written after the erasure would be
  reported as lawfully erased while being perfectly readable — a tombstone over live personal data.
- **Destroy whatever records the generation when you destroy the key.** If your provider keeps it in a
  sidecar record or a tag, that record must die with the key — otherwise the next provisioning at the same
  handle reads it back and hands new material the destroyed generation's identifier, which reintroduces
  exactly the confusion this change removes.

:::warning `IKeyDestructionStatusProvider` did **not** gain a generation-scoped member

An earlier revision of this page said it had, and that implementing one was required. **That is withdrawn.**
The interface declares one member — the handle-scoped
`IsKeyDestroyedAsync(string keyId, CancellationToken)` — and the read path does not call it at all. If you
added a generation-scoped or version-scoped overload on that advice, delete it.

A backend cannot answer the read's question at any granularity: it retains nothing about material it never
held, so *destroyed* and *never here* are the same observation there. A read's tombstone therefore comes from
`IKeyDestructionLedger` — a durable record of a destruction that was performed. See
[A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md).
:::

## Verifying your implementation

`KeyManagementProviderConformanceTestKit` gained two arms for the generation, and they are twins — each
fails the defect the other admits:

- **`Generation_ShouldBeStable_AcrossARotation`** — a rotation must not move the generation. This is the arm
  that fails a provider reusing a backend per-version value, which nothing else in the kit notices.
- **`Generation_ShouldChange_WhenAHandleIsReprovisionedAfterDestruction`** — a handle re-provisioned after a
  completed destruction must report a **different** generation. Without this arm, the stability arm above is
  satisfied by a provider returning one constant for every key it ever holds.

See [Conformance kits gained arms](conformance-kit-arms-added.md) for the wrappers to add.

Azure Key Vault's generation mapping is **not covered by a conformance run** — there is no suite for that
provider, so its behaviour here is reasoned from the backend's documented tagging and versioning and is
unverified by us. Treat it accordingly if you depend on it.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is minted, used and destroyed
- [A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md) — why absence is never read as erasure
- [Erasure destroyed-key record](erasure-destroyed-key-record.md) — the record the generation is keyed in
- [Provisioning a key is no longer a rotation](create-key-if-absent.md) — the member that mints a subject's key
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
