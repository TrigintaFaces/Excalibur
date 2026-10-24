---
title: Erasure recovery and fresh SQL schema provisioning
sidebar_label: Erasure destroyed-key record
description: The erasure now records which key generations it destroyed, so a retry can attest what its first pass achieved and a read can report the subject's erasure. SQL Server and PostgreSQL erasure stores need two tables and will refuse to start without them.
---

# Erasure recovery and fresh SQL schema provisioning

**SQL Server and PostgreSQL erasure stores require the current schema.** Earlier alpha schemas
are not supported upgrade targets. Provision a new compliance schema with the shipped
`001_CreateComplianceSchema.sql`, preserving any earlier schema and its destruction evidence.
The ledger provisioning scripts never delete existing records and refuse incompatible table shapes.

**If you use only the in-memory erasure store, or you implement `IErasureStore` yourself**, read
[What changed in the contract](#what-changed-in-the-contract) — the interface gained two members, one member
changed signature, and the store must now also implement `IKeyDestructionLedger`.

## The defect this fixes

Crypto-shredding erases a data subject by destroying their key. The erasure recorded a key as erased only
when the destruction *completed during that pass*.

Asking a key store to destroy a key it has **already destroyed** reports the key as **absent** — and that is
the same answer it gives for a key that **never existed**. Those two facts have opposite consequences for
attestation, and nothing in the key store can tell them apart.

So an erasure that destroyed a subject's key and then failed part-way through the remaining work — a
network fault, a contributor error, a process restart — attested that key on its first pass and attested
**nothing** for it on the retry. The coverage gate then saw a discovered location whose key was not in the
deleted set, reported it uncovered, and refused to complete.

**The subject's data was destroyed and their erasure could never be reported complete.** Article 17
completion was permanently unreachable for any interrupted erasure, and no number of retries could clear
it. The request stayed short of `Completed` forever, and the shortfall looked identical to a key that had
never existed.

The fix is that the **request remembers which key generations it destroyed**, and a retry reads what its own
earlier pass wrote. A key reported absent is attested if and only if a record says this destruction happened.

The same record now carries a second, heavier load: it is the **sole basis on which a read may report a
crypto-shredded field as erased**. `IKeyDestructionLedger.IsGenerationDestroyedAsync` resolves against this
table, and an erasure store is what registers that ledger. See
[A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md).

## Two tables, and they are deliberately separate

| Table | Written | What a row means |
|---|---|---|
| destruction **intents** | **before** the destroy | this request intends to destroy this generation. The material may still be **live**. |
| destroyed **keys** (the ledger) | **after** the destroy returns | this generation's material was irreversibly destroyed. A row's existence **is** that statement. |

They are held apart because a staged row names live material. If the ledger predicate could see a staged
row, a live key would report as destroyed, and the read would tombstone recoverable personal data while
claiming a lawful erasure — silent in both directions. Nothing that resolves the destruction predicate may
reach the intents table.

Staging has to happen first for a reason that is easy to miss: **the destruction destroys the record's own
input.** On every backend this framework supports, the generation identifier *is* backend material — a
key-vault version id, a CMK id, a sidecar marker, an in-memory entry — and none of them is readable once the
material is gone. So a crash between destroying the key and recording its generation is not a window a
retry repairs: the generation is unreadable forever, the retry cannot land the record either, and the
subject's ciphertext is then permanently undecryptable with no way to report their erasure. The staged row
is the only remaining copy, and it is what leaves the retry something to work from.

Recording a destruction removes its own staged intent, and only its own.

## Provisioning the schema

For a fresh deployment, run the current `001_CreateComplianceSchema.sql` or enable
`AutoCreateSchema = true`. Automatic creation only creates absent tables; it does not upgrade
existing ones. Earlier alpha request schemas changed tenant identity and tenant-partition columns.
Those identities cannot safely be backfilled with arbitrary defaults or copied from one another.
This alpha requires a new compliance schema for those deployments.

When the request schema is already current but ledger tables are absent, run PostgreSQL
`004_AddKeyDestructionLedger.sql` or SQL Server `005_AddKeyDestructionLedger.sql`.
These scripts validate both existing tables before creating either missing table, in one transaction.
They serialize concurrent executions, require the exact ordered primary keys, and check required
column types, nullability, and ordinal key collations. Repeating a successful script preserves records.
They do not certify unrelated schema objects or repair earlier table shapes.

An incompatible table causes an error and rolls back the script. Existing records remain intact.
Do not delete destruction evidence to make provisioning succeed. A historical record without a
generation cannot establish that a particular generation was destroyed; its missing identity cannot
be reconstructed after the material is gone. Keep that earlier schema separate from a fresh deployment.

The store also verifies required columns at startup and raises `ErasureStoreNotProvisionedException`
when they are absent. This startup check does not replace the provisioning scripts' shape validation.

### The DDL, if you apply it by hand

The primary key is **the pair** `(KeyHandle, KeyGeneration)`, and the handle is **not** nullable.
Keying on the generation alone would rest on an assumption nothing enforces — that one generation
identifies key material uniquely across every handle — which a key provider that *derives* its
generation rather than minting one would break, giving two distinct keys a single row and letting one
subject's destruction answer for another's live key. Keying on the handle alone would be wrong in the
other direction: it would drop a second destruction at the same handle. The pair admits many
generations per handle and refuses two rows for one pair.

#### SQL Server

```sql
CREATE TABLE [compliance].[ErasureDestroyedKeys] (
    KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
    RequestId     UNIQUEIDENTIFIER NULL,
    KeyHandle     NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
    DestroyedAt   DATETIMEOFFSET   NOT NULL,
    RecordedBy    NVARCHAR(32)     NOT NULL,
    CONSTRAINT PK_ErasureDestroyedKeys PRIMARY KEY (KeyHandle, KeyGeneration)
);

CREATE INDEX IX_ErasureDestroyedKeys_Request
    ON [compliance].[ErasureDestroyedKeys] (RequestId, KeyHandle);

CREATE TABLE [compliance].[ErasureDestructionIntents] (
    RequestId     UNIQUEIDENTIFIER NOT NULL,
    KeyHandle     NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
    KeyGeneration NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
    StagedAt      DATETIMEOFFSET   NOT NULL,
    CONSTRAINT PK_ErasureDestructionIntents
        PRIMARY KEY (RequestId, KeyHandle, KeyGeneration)
);
```

#### PostgreSQL

```sql
CREATE TABLE "compliance"."erasure_destroyed_keys" (
    key_generation TEXT COLLATE "C" NOT NULL,
    request_id     UUID        NULL,
    key_handle     TEXT COLLATE "C" NOT NULL,
    destroyed_at   TIMESTAMPTZ NOT NULL,
    recorded_by    TEXT        NOT NULL,
    PRIMARY KEY (key_handle, key_generation)
);

CREATE INDEX ix_erasure_destroyed_keys_request
    ON "compliance"."erasure_destroyed_keys" (request_id, key_handle);

CREATE TABLE "compliance"."erasure_destruction_intents" (
    request_id     UUID        NOT NULL,
    key_handle     TEXT COLLATE "C" NOT NULL,
    key_generation TEXT COLLATE "C" NOT NULL,
    staged_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (request_id, key_handle, key_generation)
);
```

Four properties of that shape are load-bearing:

- **The primary key is the generation alone.** A generation is minted once and never reused, so one
  generation is one destruction and a second row for it is a contradiction the database refuses. The request
  and the handle are **audit attributes, never key components** — keying on the handle silently drops a
  second destruction at that handle, which is a destruction that happened and is not on file anywhere.
- **The request and handle are nullable.** A destruction the *consumer* performed and asserted through
  `IKeyDestructionLedger.RecordDestroyedGenerationAsync` has no erasure request and no handle to name. The
  read predicate names neither, so their absence costs nothing.
- **`RecordedBy` states who asserted the destruction** — `framework-erasure` for one this framework
  performed (staged before the destroy, recorded after it), `caller-assertion` for one the consumer
  performed and recorded themselves, which nothing re-verified. It is stated explicitly rather than inferred
  from the nulls above, because deriving a fact from a missing value is the reasoning this table exists to
  replace.
- **The collation is not incidental.** A generation and a key handle are opaque identifiers, not text to be
  folded. A case-insensitive collation would treat two distinct values as one and attest coverage for
  material that was never destroyed, so both engines are told to compare byte-for-byte — matching how the
  framework compares them.

If you rename either table through `DestroyedKeysTableName` or `DestructionIntentsTableName` on
`SqlServerErasureStoreOptions` / `PostgresErasureStoreOptions`, keep the collations and the keys.

## What changed in the contract

### The store must also implement `IKeyDestructionLedger`

It is not optional. Without it, no read of a crypto-shredded field can ever report a subject's erasure, and
a crypto-shredding composition refuses to start. Every shipped erasure store implements both contracts on
one type, and its registration publishes the ledger from that same instance.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Compliance;

public static class DestructionLedgerUsage
{
    public static ValueTask<bool> WasDestroyedAsync(
        IKeyDestructionLedger ledger, string keyHandle, string keyGeneration, CancellationToken cancellationToken)
        => ledger.IsGenerationDestroyedAsync(keyHandle, keyGeneration, cancellationToken);

    public static Task RecordAsync(
        IKeyDestructionLedger ledger, string keyHandle, string keyGeneration, CancellationToken cancellationToken)
        => ledger.RecordDestroyedGenerationAsync(keyHandle, keyGeneration, cancellationToken);
}
```

`IsGenerationDestroyedAsync` returns `true` **only** when a row exists for that handle and generation. It returns
`false` when none does — which covers a live generation, one destroyed outside this framework, and one that
never existed; those three are not distinguished and none of them may produce a tombstone. **Being unable to
read the ledger throws.** It is never reported as `false`: that value would be indistinguishable, in logs
and in metrics, from "asked, and there is no row".

### `IErasureStore` gained two members and one changed signature

```csharp
// Changed: now keyed on the generation.
Task RecordKeyDestroyedAsync(
    Guid requestId,
    string keyHandle,
    string keyGeneration,
    CancellationToken cancellationToken);

// New: staged BEFORE the destruction.
Task StageKeyDestructionAsync(
    Guid requestId,
    string keyHandle,
    string keyGeneration,
    CancellationToken cancellationToken);

// New: the recovery read, for a pass that destroyed material and failed before recording it.
Task<IReadOnlyList<string>> GetStagedKeyGenerationsAsync(
    Guid requestId,
    string keyHandle,
    CancellationToken cancellationToken);
```

What each owes:

- **`RecordKeyDestroyedAsync` appends, never replaces.** A later pass adds to what earlier passes wrote.
  Truncating the set would erase the evidence of every earlier pass — the same defect by another route.
- **Idempotent on the handle and generation pair.** Recording a pair already recorded is a no-op. Two **different**
  generations destroyed at one handle are two destructions and produce **two** rows with their own instants.
- **Written per key, as each destruction returns** — not at completion. The pass that needs this record is
  the one that did not reach completion, so a write deferred to the end never happens in the only case it is
  for.
- **Recording clears its own staged intent, and only its own.** The generations a broader cleanup would wipe
  are destructions *not yet recorded*, and a staged intent is the only copy of a generation that survives its
  own destruction.
- **`StageKeyDestructionAsync` is idempotent and appends**, and a staged row must **never** satisfy the
  ledger predicate.
- **`GetStagedKeyGenerationsAsync` returns empty, never null**, and reports what was **staged** — a statement
  about an intention, never about an outcome. A caller that writes a destruction record from an answer here
  is asserting the material is gone and owes positive evidence of that.
- **Both writes throw `KeyNotFoundException` when the request does not exist.** The caller attests a
  destruction on the strength of these records, so accepting one quietly would hand out a false assurance.

`ErasureStatus.DestroyedKeyHandles` is unchanged — the handles the request has destroyed across every pass,
empty rather than null when nothing has been destroyed yet. A count cannot answer the question it answers:
`KeysDeleted` says how many keys the last pass destroyed, this says *which* keys the request has destroyed
in total.

A destruction whose record could not be written is **not attested on that pass**. The erasure fails and
stays retryable, which is the recoverable direction — attesting it and losing the record would make it
uncountable on every later pass.

## A record of a past destruction is necessary, not sufficient

**If you rely on an interrupted erasure being completable, your key provider must be able to report whether a
key handle is destroyed.** Every provider this framework ships can; a custom provider that does not supply
`IKeyDestructionStatusProvider` will leave interrupted erasures uncertifiable, and the erasure says so rather
than assuming.

The reason is the limit of what this record can prove. It says *this generation was destroyed* — a fact about
the past. Before attesting, the erasure asks a second question about the handle: *is it destroyed now?* Those
differ, because a write for the same data subject landing while the erasure runs can re-occupy a handle that
was destroyed moments earlier, and the material behind it is then live again.

So the two compose, and the order matters: **the record makes a retried destruction countable, and the
provider's answer makes it true.** A handle this request destroyed which now reports as live is **not**
attested — attesting it would tell a data subject their data is unreadable while it is not.

This is the handle-scoped question, and it is the only one a key backend can answer. A **read** of one
ciphertext asks a different question, of the generation, and must never ask the backend — see
[A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md).

## Verifying your own store

`ErasureStoreConformanceTestKit` gained six arms for this work. If you derive a suite from it, see
[Conformance kits gained arms](conformance-kit-arms-added.md) for the wrappers to add.

## Related

- [A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md) — why a read resolves against this table and nothing else
- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is destroyed
- [Consumer conformance toolkit](../testing/conformance-toolkit.md) — deriving a suite from a shipped kit
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
