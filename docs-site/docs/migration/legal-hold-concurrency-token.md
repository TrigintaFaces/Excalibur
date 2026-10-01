---
title: A legal hold carries a concurrency token, and an existing database fails closed
sidebar_label: Legal hold concurrency token
description: LegalHold gained a Version, updating one is now compare-and-set, and a database provisioned before the column fails at startup naming it. There is no ALTER script; re-provision.
---

# A legal hold carries a concurrency token, and an existing database fails closed

A legal hold is the authority that stops an erasure destroying records a data controller is legally obliged
to keep. Updating one is a read-modify-write: you read a hold, decide from what you read, and write the whole
record back. **Without a version check, that decision is applied over whatever the record became in the
meantime** — so a hold whose expiry was extended between a sweep's read and its write is released anyway, and
the next erasure for that subject proceeds.

The loss there is not the hold. It is the records the hold protected, and it is irreversible.

`LegalHold` now carries a `Version`, and the store applies an update only while the stored record still holds
the version the caller read.

## Two things to do, and the order matters

1. **Provision the schema**, because an existing database fails closed at startup — see
   [The schema change](#the-schema-change).
2. **If you implement `ILegalHoldStore` yourself, implement the compare-and-set** — see
   [If you have a custom store](#if-you-have-a-custom-store). Nothing forces this at compile time, so a
   custom store that ignores the version keeps the lost update silently.

If you use a shipped store and provision from the shipped script, there is nothing else to change: the
version round-trips through the framework's own call sites without your code mentioning it.

## The schema change

Both shipped create scripts carry the column, so **a database provisioned fresh from them is already
correct**:

| provider | table | column |
| --- | --- | --- |
| SQL Server | `[compliance].[LegalHolds]` | `Version INT NOT NULL DEFAULT 0` |
| PostgreSQL | `legal_holds` | `version INT NOT NULL DEFAULT 0` |

**A database provisioned before that column existed fails at startup**, naming the column:

```text
Table 'compliance.LegalHolds' exists but is missing 1 column(s) that this store's statements
bind: Version. This is a schema provisioned before those columns were introduced. Enabling
automatic schema creation will NOT repair it, because that path only creates tables that are
absent. Run the shipped migration scripts against this database, then restart.
```

This is the loud, designed path. The check reads the **column** catalogue rather than the table catalogue
precisely so that it cannot report healthy on the database that is broken — a table that is present and the
wrong shape. Had it only asked whether the table existed, you would have got a dead store plus a check that
said it was fine, and the real failure would have arrived later as a raw `Invalid column name` far from its
cause.

:::warning There is no ALTER script, and `AutoCreateSchema` will not save you
Two things the startup message does not make obvious:

- **`AutoCreateSchema = true` does not repair this database.** That path only creates tables that are
  *absent*; it does not alter one that exists in an older shape. The message says so, and it is the half
  people skip.
- **No incremental migration script ships for this column.** The shipped `Scripts/` directories contain
  create scripts only, and the framework does not preserve previous-version data. So the remedy the message
  points at generically does not exist for this column specifically: **re-provision the compliance schema
  from the shipped create script** rather than looking for an `ALTER`.

If you must keep existing holds, add the column yourself — `INT NOT NULL DEFAULT 0` — before restarting. The
default is correct for existing rows: `0` is the value a never-updated hold carries, so an existing hold
becomes updatable on its first read-modify-write without special handling.
:::

## Why the version is a portable `int`, not a provider-native token

Each store could have exposed its own native concurrency primitive — a SQL Server `rowversion`, a PostgreSQL
`xmin`, an ETag. It deliberately does not. **Three stores comparing three incomparable token types would make
a hold read from one store mean something different from one read from another**, and a legal hold is exactly
the record that must mean the same thing everywhere. A plain `int` compares the same way on every provider.

## Reading and writing a hold

**The store owns the value; a caller only carries it.** A newly created hold is stored at `0`, reads return
whatever is stored, and each successful update increments it by one. Setting it by hand does not move the
stored record — it only changes which stored version the next update will accept.

**Round-trip it unchanged.** Building the record you write with a `with` expression over the one you read
carries the version across for free, which is why no call site needs to mention it:

```csharp
var hold = await store.GetHoldAsync(holdId, ct);
if (hold is null)
{
    return;
}

// `with` carries Version across. This is the shape to use.
var extended = hold with { ExpiresAt = hold.ExpiresAt?.AddDays(30) };

var applied = await store.UpdateHoldAsync(extended, ct);
```

:::danger Constructing a fresh `LegalHold` from parts is how you lose the protection
A hold built from scratch rather than from the one you read gets `Version = 0`, which no *updated* record
still carries. The write is then **refused rather than silently applied** — so this fails closed, which is the
right direction, but it fails. Use `with` over the record you read.
:::

### The three outcomes

`UpdateHoldAsync`'s signature has **not** changed — it is still
`Task<bool> UpdateHoldAsync(LegalHold hold, CancellationToken cancellationToken)`. The version travels inside
the `LegalHold`. What changed is that there are now three distinguishable outcomes rather than two:

| outcome | meaning | what to do |
| --- | --- | --- |
| returns `true` | applied; stored version incremented | nothing |
| returns `false` | **nothing to write to** — no such hold, or it belongs to another tenant | treat as absent |
| throws `LegalHoldConcurrencyException` | the record is present and visible but **moved under you**; the write was **not** applied | **re-read and re-decide** |

Keeping "nothing to write to" as `false` and "the record moved" as an exception is what makes the two
distinguishable at all — they were previously the same `false`.

```csharp
try
{
    var applied = await store.UpdateHoldAsync(extended, ct);
    if (!applied)
    {
        // No such hold, or not visible to this tenant. Not a conflict.
    }
}
catch (LegalHoldConcurrencyException ex)
{
    // The record moved between your read and your write, and your decision was made on
    // the stale version. Re-READ and re-DECIDE -- see the warning below.
    logger.LogWarning(
        "Legal hold {HoldId} moved from version {Expected} to {Actual}; re-reading.",
        ex.HoldId, ex.ExpectedVersion, ex.ActualVersion);
}
```

The exception carries `HoldId`, `ExpectedVersion` and `ActualVersion` (all nullable), and derives from
`InvalidOperationException` — so an existing broad catch already catches it, but will not distinguish it from
`DuplicateLegalHoldException`. **The remedies differ:** a duplicate means the identifier is taken; a conflict
means your premise expired.

:::danger Do not retry the same write with a refreshed version
Re-reading the stored version and re-applying **the same decision** reintroduces the lost update with extra
steps. The point of the conflict is that the decision itself was made against a record that has since
changed — a hold someone extended while you were deciding to release it. **Re-read, then decide again from
what you read.**
:::

### Why a conflict is an exception rather than a return value

Because the failure it prevents is **silent**. A released hold is well-formed and carries a plausible reason,
so an auditor inspecting it afterwards sees a legitimate release; the extension simply is not there, and
nothing downstream learns that the erasure which followed was unlawful.

A conflict signalled as a `bool` or an enum can be discarded at the call site — and a discarded conflict
reproduces exactly that silence one layer up. An exception cannot be dropped by accident.

## If you have a custom store

**This is the case that needs your attention, because nothing enforces it at compile time.**
`UpdateHoldAsync`'s signature is unchanged, so a custom `ILegalHoldStore` still compiles — and if it writes
the record without comparing the version, it **keeps the lost update silently.** Your implementation must:

1. **Apply the write only while the stored version matches the caller's**, and increment it on success. The
   shipped stores do this in one statement, which is the property that matters — a read-then-write in two
   statements reintroduces the race it exists to close:

   ```sql
   UPDATE ... SET ..., Version = Version + 1
   WHERE HoldId = @HoldId AND Version = @ExpectedVersion
   ```

2. **Distinguish the two zero-row cases.** When no row is affected, re-read the stored version: if there is
   no such visible row, return `false`; if there is one and it differs, throw. The shipped stores use
   `LegalHoldConcurrencyException.ForHold(holdId, expectedVersion, actualVersion)`.

3. **Keep the tenant predicate on the update.** A tenant must be able to *see* an estate-wide hold, because
   it blocks their erasures, but must not be able to *modify* one — a mutation matching an estate-wide row
   would let one tenant re-home an estate-wide preservation order into its own partition, lifting it for
   everyone else.

## Related

- [GDPR erasure → legal holds](../compliance/gdpr-erasure.md#legal-holds) — registering a store, creating and releasing holds
- [Resolved issues](../resolved-issues.md) — the lost-update defect this closes
- [What's new](../whats-new.md#before-you-upgrade)
