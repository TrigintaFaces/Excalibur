---
title: Erasure registrations must declare a store kind
sidebar_label: Erasure registration store kind
---

# Erasure registrations must declare a store kind

**If you use GDPR erasure with the SQL Server or PostgreSQL data inventory, this upgrade needs two actions
from you: apply one schema script, and classify your existing registrations. Until you classify them,
erasure will not report `Completed`.**

This is deliberate. The framework refuses to certify an erasure it cannot demonstrate, rather than
reporting success over a store nobody erased.

## What changed

A registered data location now records **which kind of store holds it**, so an erasure contributor can be
offered the obligations it covers and can name what it actually erased.

| Added | Type |
|---|---|
| `DataLocationRegistration.StoreKind` | `DataStoreKind` |
| `DataInventory.DeclaredLocationKinds` | `IReadOnlyDictionary<DataLocationKey, DataStoreKind>` |
| `ErasureContributorContext.DeclaredLocations` | `IReadOnlyList<DataLocationKey>` |

`DataStoreKind` is not new and is not an enum — it is a value type with the built-in kinds `EventStore`,
`Snapshot`, `Outbox`, `Inbox`, `Projection`, `Saga`, `Audit` and `Cache`, plus `DataStoreKind.Create(value)`
for a store kind of your own.

## 1. Apply the schema script

The column is added by a script shipped in the provider package:

| Provider | Script |
|---|---|
| SQL Server | `Scripts/009_AddRegistrationStoreKind.sql` |
| PostgreSQL | `Scripts/006_AddRegistrationStoreKind.sql` |

Both scripts are packed in the NuGet package and restore to
`~/.nuget/packages/<package-id>/<version>/scripts/`. They add a **nullable** `StoreKind` column to the
data-inventory registrations table and are guarded, so re-running one is a no-op.

Apply it with your own migration runner, or by hand, before the upgraded application starts. Run it with
your tool's stop-on-first-error behaviour (`sqlcmd -b`, `psql -v ON_ERROR_STOP=1`) so a failure is visible
rather than skipped.

## 2. Classify your existing registrations

**This is the step that is easy to miss, because nothing fails at startup.**

The column is nullable on purpose: a registration written before it existed reads back as
`DataStoreKind.Unknown`. A contributor is offered only the declared pairs whose store kind it lists in
`CoveredStoreKinds`, and **no built-in contributor lists `Unknown`** — so an unclassified registration
reaches nobody, discharges nothing, and an erasure covering that subject reports a non-`Completed` outcome
instead of certifying a location no contributor claimed.

So immediately after the upgrade, an estate whose registrations all predate this change behaves exactly as
it did before — erasure refuses — and it keeps refusing until you classify them.

**What to do:** set `StoreKind` on each registration to the kind of store that actually holds it, either by
re-registering through `IDataInventoryService.RegisterDataLocationAsync` with the property set, or by
updating the `StoreKind` column directly for registrations you can classify from your own records.

```csharp
await inventory.RegisterDataLocationAsync(
    new DataLocationRegistration
    {
        TableName = "Orders",
        FieldName = "CustomerEmail",
        DataCategory = "ContactDetails",
        DataSubjectIdColumn = "CustomerId",
        IdType = DataSubjectIdType.UserId,
        KeyIdColumn = "EncryptionKeyId",
        StoreKind = DataStoreKind.Projection,   // <-- the new requirement
    },
    cancellationToken);
```

## How to tell whether you are done

Run an erasure for a test subject and read the certificate's outcome. A `Completed` certificate means every
registered location for that subject was claimed by a contributor. Anything else means at least one
location is still unclassified or uncovered — the certificate names what it covered, and its coverage
statement tells you what it does **not**.

:::warning A certificate covers only what you registered
This certificate covers the data locations registered with the framework's data inventory when the erasure
ran. Personal data held anywhere else, including stores your application writes directly, is not covered by
it, and its absence from this certificate is not evidence that it was erased. See
[Known issues](../known-issues.md) for the current limits of erasure coverage.
:::
