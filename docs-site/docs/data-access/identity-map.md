---
sidebar_position: 3
title: Identity Map
description: External-to-internal aggregate ID resolution for CDC, integration, and anti-corruption layers.
---

# Identity Map

The **Identity Map Store** provides a write-side authoritative mapping between external identifiers and internal aggregate IDs. It sits in the integration boundary between your domain and external systems.

## When to Use

| Scenario | Example |
|----------|---------|
| **CDC Ingestion** | Legacy system sends transactions by `ExternalTransactionId` -- you need to map to your `Transaction` aggregate ID |
| **Cross-Aggregate Reference** | Creating a `Transaction` from a command that contains `AccountNumber` -- you need the `Account` aggregate's ID |
| **Anti-Corruption Layer** | An API receives ERP codes and must resolve them to domain aggregate IDs before dispatching commands |
| **Idempotent Import** | Ensuring the same external record is not imported twice as separate aggregates |

## Installation

```bash
dotnet add package Excalibur.Data.IdentityMap.SqlServer
```

## Registration

```csharp
services.AddIdentityMap(identity =>
{
    identity.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("dbo")        // default
           .TableName("IdentityMap") // default
           .MaxBatchSize(200);       // default: 100
    });
});
```

Or via the unified Excalibur builder:

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur.AddIdentityMap(identity =>
        identity.UseSqlServer(sql => sql.ConnectionString(connectionString)));
});
```

For testing, use the in-memory provider:

```csharp
services.AddInMemoryIdentityMap();
```

## Core Operations

### Resolve

Look up an internal aggregate ID from an external identifier:

```csharp
// String result
string? aggregateId = await identityMap.ResolveAsync(
    "LegacyCore", externalTransactionId, "Transaction", ct);

// Typed result (Guid, int, long, etc.)
Guid? transactionId = await identityMap.ResolveAsync<Guid>(
    "LegacyCore", externalTransactionId, "Transaction", ct);
```

### Bind (Upsert)

Create or update a mapping:

```csharp
await identityMap.BindAsync(
    "LegacyCore", externalTransactionId, "Transaction",
    newTransactionId.ToString(), ct);

// Typed convenience
await identityMap.BindAsync<Guid>(
    "LegacyCore", externalTransactionId, "Transaction", newTransactionId, ct);
```

### TryBind (Insert-if-not-exists)

Atomically bind only if no mapping exists. Returns the existing mapping on conflict:

```csharp
var result = await identityMap.TryBindAsync(
    "LegacyCore", externalId, "Transaction",
    Guid.NewGuid().ToString(), ct);

if (result.WasCreated)
{
    // New mapping -- create the aggregate
}
else
{
    // Mapping already existed -- result.AggregateId is the existing one
}
```

### Unbind

Remove a mapping:

```csharp
bool removed = await identityMap.UnbindAsync(
    "LegacyCore", externalId, "Transaction", ct);
```

### Batch Resolve

Resolve multiple external IDs in one call (uses chunked IN clauses, no TVPs):

```csharp
var resolved = await identityMap.ResolveBatchAsync(
    "LegacyCore",
    [txnId1, txnId2, txnId3],
    "Transaction", ct);

// resolved is IReadOnlyDictionary<string, string>
// Keys are external IDs, values are aggregate IDs
```

## Composite Keys

When an external system identifies records by multiple fields, use `CompositeKey`:

```csharp
using Excalibur.Data.IdentityMap;

// Two-part key: ClientNo + AccountNo uniquely identifies an Account
var key = CompositeKey.Create("ClientNo", clientNo, "AccountNo", accountNo);

var accountId = await identityMap.ResolveAsync<Guid>(
    "LegacyCore", key, "Account", ct);
```

`CompositeKey.Create` produces a deterministic, pipe-delimited string like `CLIENTNO=C-123|ACCOUNTNO=A-456`. Names are normalized to uppercase, values are trimmed, and pipe characters within values are escaped.

### Overloads

```csharp
// Two parts
CompositeKey.Create("Name1", value1, "Name2", value2);

// Three parts
CompositeKey.Create("Name1", value1, "Name2", value2, "Name3", value3);

// Arbitrary parts
CompositeKey.Create(
    ("Branch", branchCode),
    ("Dept", deptCode),
    ("EmpId", empId));
```

## Real-World Example: CDC Transaction Ingestion

```csharp
public class TransactionCdcHandler : IDataChangeHandler
{
    private readonly IIdentityMapStore _identityMap;
    private readonly IEventSourcedRepository<Transaction, Guid> _repo;

    public async Task HandleAsync(DataChange change, CancellationToken ct)
    {
        var legacyTxnId = change.GetValue<string>("TransactionId");
        var legacyAccountKey = CompositeKey.Create(
            "ClientNo", change.GetValue<string>("ClientNo"),
            "AccountNo", change.GetValue<string>("AccountNo"));

        // 1. Resolve the Account aggregate
        var accountId = await _identityMap.ResolveAsync<Guid>(
            "LegacyCore", legacyAccountKey, "Account", ct)
            ?? throw new InvalidOperationException(
                $"No Account mapped for {legacyAccountKey}");

        // 2. Idempotent bind for the Transaction
        var bind = await _identityMap.TryBindAsync(
            "LegacyCore", legacyTxnId, "Transaction",
            Guid.NewGuid().ToString(), ct);

        if (bind.WasCreated)
        {
            // First time -- create the Transaction aggregate
            var txn = Transaction.Create(
                Guid.Parse(bind.AggregateId),
                accountId.Value,
                change.GetValue<decimal>("Amount"));
            await _repo.SaveAsync(txn, ct);
        }
        else
        {
            // Already imported -- reconcile
            var txn = await _repo.LoadAsync(Guid.Parse(bind.AggregateId), ct);
            txn.Reconcile(change.GetValue<decimal>("Amount"));
            await _repo.SaveAsync(txn, ct);
        }
    }
}
```

## SQL Server Schema

The SQL Server provider requires a single table (no TVPs, no stored procedures):

```sql
CREATE TABLE [dbo].[IdentityMap] (
    ExternalSystem  NVARCHAR(128)    NOT NULL,
    ExternalId      NVARCHAR(256)    NOT NULL,
    AggregateType   NVARCHAR(256)    NOT NULL,
    AggregateId     NVARCHAR(256)    NOT NULL,
    CreatedAt       DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_IdentityMap PRIMARY KEY CLUSTERED
        (ExternalSystem, ExternalId, AggregateType),
    INDEX IX_IdentityMap_AggregateId (AggregateType, AggregateId)
);
```

The schema and table name are configurable. A migration script is included in the package at `Scripts/CreateIdentityMapTable.sql`.

## Architecture

```
┌──────────────────────────────────────────────────┐
│  CDC Handler / Command Handler / API Controller  │
│                                                  │
│  "I have an external ID, I need an aggregate ID" │
└──────────────────┬───────────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────────┐
│            IIdentityMapStore                     │
│                                                  │
│  ResolveAsync    BindAsync    TryBindAsync        │
│  UnbindAsync     ResolveBatchAsync               │
└──────────────────┬───────────────────────────────┘
                   │
          ┌────────┼────────┐
          ▼        ▼        ▼
     SqlServer  InMemory  (future)
```

The identity map is **not** a read model or projection. It is a write-side infrastructure concern:

- **Event Store**: source of truth for domain events
- **Identity Map**: source of truth for external-to-internal ID translation
- **Projections**: source of truth for read/query UX only
