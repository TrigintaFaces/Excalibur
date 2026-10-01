---
title: An interrupted erasure can now be completed, and SQL stores need one new table
sidebar_label: Erasure destroyed-key record
description: The erasure now records which keys it destroyed, so a retry can attest what its first pass achieved. SQL Server and PostgreSQL erasure stores require a new table and will refuse to start without it.
---

# An interrupted erasure can now be completed, and SQL stores need one new table

**If you use the SQL Server or PostgreSQL erasure store, your application will refuse to start after this
upgrade until you create one new table.** The DDL is below. That refusal is deliberate and it is the whole
of the consumer-facing change.

**If you use only the in-memory erasure store, or you implement `IErasureStore` yourself**, read the
contract section — the interface gained one member.

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

The fix is that the **request remembers which keys it destroyed**, and a retry reads what its own earlier
pass wrote. A key reported absent is attested if and only if this same request recorded destroying it.
A key reported absent that this request never destroyed still contributes nothing and is still not an
error — a location whose key never existed is an inventory that disagrees with reality, and counting it as
erased would hide exactly that.

## What you must do: create one table

The store verifies its schema at startup and **fails fast, naming the missing table**, rather than failing
later inside an erasure. You will see `ErasureStoreNotProvisionedException` naming
`ErasureDestroyedKeys` (SQL Server) or `erasure_destroyed_keys` (PostgreSQL).

Re-running the shipped `001_CreateComplianceSchema.sql` is enough — every statement in it is idempotent, so
it creates what is missing and leaves what exists alone. If you apply DDL by hand, this is the whole of it:

### SQL Server

```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'ErasureDestroyedKeys')
BEGIN
    CREATE TABLE [compliance].[ErasureDestroyedKeys] (
        RequestId   UNIQUEIDENTIFIER NOT NULL,
        KeyHandle   NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        DestroyedAt DATETIMEOFFSET   NOT NULL,
        CONSTRAINT PK_ErasureDestroyedKeys PRIMARY KEY (RequestId, KeyHandle)
    );
END
```

### PostgreSQL

```sql
CREATE TABLE IF NOT EXISTS "compliance"."erasure_destroyed_keys" (
    request_id    UUID        NOT NULL,
    key_handle    TEXT COLLATE "C" NOT NULL,
    destroyed_at  TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (request_id, key_handle)
);
```

**The collation is not incidental.** A key handle is an opaque identifier, not text to be folded. A
case-insensitive collation would treat two distinct subjects' handles as one and attest coverage for a key
that was never destroyed, so both engines are told to compare byte-for-byte — matching how the framework
compares handles. If you rename the table through
`SqlServerErasureStoreOptions.DestroyedKeysTableName` or its PostgreSQL equivalent, keep the collation and
the composite primary key.

The primary key is what makes re-recording a handle a no-op at the database, so two passes of one request
cannot lose each other's records and no pass has to read-modify-write a list.

If you provision with `AutoCreateSchema = true`, the store creates the table for you on startup and there is
nothing to do.

## What changed in the contract

`IErasureStore` gained one member:

```csharp
Task RecordKeyDestroyedAsync(
    Guid requestId,
    string keyHandle,
    CancellationToken cancellationToken);
```

- **Appends, never replaces.** A later pass adds to what earlier passes wrote. Truncating the set would
  erase the evidence of every earlier pass — the same defect by another route.
- **Idempotent.** Recording a handle already recorded for the same request is a no-op.
- **Written per key, as each destruction returns** — not at completion. The pass that needs this record is
  the one that did not reach completion, so a write deferred to the end never happens in the only case it
  is for.
- **Throws when the request does not exist.** The caller attests a destruction on the strength of this
  record, so accepting it quietly would hand out a false assurance.

`ErasureStatus` gained `DestroyedKeyHandles`, the handles the request has destroyed across every pass. It is
empty rather than null when nothing has been destroyed yet. A count cannot answer the question it answers:
`KeysDeleted` says how many keys the last pass destroyed, this says *which* keys the request has destroyed
in total.

A destruction whose record could not be written is **not attested on that pass**. The erasure fails and
stays retryable, which is the recoverable direction — attesting it and losing the record would make it
uncountable on every later pass.

## A record of a past destruction is necessary, not sufficient

**If you rely on an interrupted erasure being completable, your key provider must be able to report whether a
key is destroyed.** Every provider this framework ships can; a custom provider that does not supply
`IKeyDestructionStatusProvider` will leave interrupted erasures uncertifiable, and the erasure says so rather
than assuming.

The reason is the limit of what this record can prove. It says *this request destroyed that handle* — a fact
about the past. Before attesting, the erasure asks a second question: *is the handle destroyed now?* Those
differ, because a write for the same data subject landing while the erasure runs can re-occupy a handle that
was destroyed moments earlier, and the material behind it is then live again.

So the two compose, and the order matters: **the record makes a retried destruction countable, and the
provider's answer makes it true.** A handle this request destroyed which now reports as live is **not**
attested — attesting it would tell a data subject their data is unreadable while it is not.

This is not a new requirement; it is the documented contract of that capability. The retry path is simply
where it starts to bite, because a first pass that completes in one go has less opportunity to be overtaken.

## Verifying your own store

`ErasureStoreConformanceTestKit` gained three arms for this member. If you derive a suite from it, see
[Conformance kits gained arms](conformance-kit-arms-added.md) for the wrappers to add.

## Related

- [Crypto-shredding](../compliance/crypto-shredding.md) — how a data subject's key is destroyed
- [Consumer conformance toolkit](../testing/conformance-toolkit.md) — deriving a suite from a shipped kit
- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
