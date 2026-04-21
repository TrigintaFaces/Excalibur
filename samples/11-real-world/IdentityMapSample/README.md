# Identity Map Sample

Demonstrates the Aggregate Identity Map store for external-to-internal ID resolution in common integration scenarios.

## Scenarios

### 1. CDC Ingestion (Legacy System Migration)

When importing transactions from a legacy system, you need to:
- Check if the transaction was already imported (idempotency)
- Resolve related aggregates (Account, Client, Branch) by their legacy keys
- Create new aggregate IDs for first-time imports

```csharp
// Resolve related aggregates by their legacy keys
var compositeKey = CompositeKey.Create("ClientNo", cdcRecord.ClientNo, "AccountNo", cdcRecord.AccountNo);

var resolved = await identityMap.ResolveBatchAsync(
    "LegacyCore",
    [compositeKey, cdcRecord.BranchNo, cdcRecord.EmployeeNo],
    // Note: batch resolve requires same aggregate type.
    // For mixed types, use individual ResolveAsync calls.
    ...);
```

### 2. Command Handler (Cross-Aggregate Reference)

When creating a financial transaction, you have the Account Number from the user but need the Account's aggregate ID:

```csharp
public class CreateTransactionHandler : IRequestHandler<CreateTransactionCommand>
{
    private readonly IIdentityMapStore _identityMap;
    private readonly IEventSourcedRepository<Transaction, Guid> _repo;

    public async Task Handle(CreateTransactionCommand cmd, CancellationToken ct)
    {
        // Resolve Account aggregate ID from business key
        var accountId = await _identityMap.ResolveAsync<Guid>(
            "Internal", cmd.AccountNumber, "Account", ct);

        if (accountId is null)
            throw new InvalidOperationException($"Unknown account: {cmd.AccountNumber}");

        var transaction = Transaction.Create(Guid.NewGuid(), accountId.Value, cmd.Amount);
        await _repo.SaveAsync(transaction, ct);
    }
}
```

### 3. Composite Keys

When an external system identifies records by multiple fields:

```csharp
// Two-part composite key (ClientNo + AccountNo -> Account)
var key = CompositeKey.Create("ClientNo", "C-12345", "AccountNo", "A-67890");
await identityMap.BindAsync<Guid>("LegacyCore", key, "Account", newAccountId, ct);

// Later, resolve it
var accountId = await identityMap.ResolveAsync<Guid>("LegacyCore", key, "Account", ct);

// Three-part key (Branch + Department + EmployeeId -> Employee)
var empKey = CompositeKey.Create(
    "Branch", "NYC-01",
    "Dept", "FIN",
    "EmpId", "E-999");
await identityMap.BindAsync<Guid>("HRSystem", empKey, "Employee", newEmployeeId, ct);
```

### 4. Idempotent Bind (TryBind)

When importing, use `TryBindAsync` to atomically check-and-create:

```csharp
var result = await identityMap.TryBindAsync(
    "LegacyCore",
    legacyTransactionId,
    "Transaction",
    Guid.NewGuid().ToString(),
    ct);

if (result.WasCreated)
{
    // First time seeing this transaction -- create aggregate
    var txn = Transaction.Create(Guid.Parse(result.AggregateId), ...);
    await repo.SaveAsync(txn, ct);
}
else
{
    // Already imported -- route to update/reconcile
    var existing = await repo.LoadAsync(Guid.Parse(result.AggregateId), ct);
    existing.Reconcile(...);
    await repo.SaveAsync(existing, ct);
}
```

## Registration

```csharp
// SQL Server
services.AddIdentityMap(identity =>
{
    identity.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("dbo")
           .TableName("IdentityMap");
    });
});

// In-memory (testing)
services.AddInMemoryIdentityMap();

// Via Excalibur unified builder
services.AddExcalibur(excalibur =>
{
    excalibur.AddIdentityMap(identity =>
    {
        identity.UseSqlServer(sql => sql.ConnectionString(connectionString));
    });
});
```

## SQL Server Schema

```sql
CREATE TABLE [dbo].[IdentityMap] (
    ExternalSystem  NVARCHAR(128) NOT NULL,
    ExternalId      NVARCHAR(256) NOT NULL,
    AggregateType   NVARCHAR(256) NOT NULL,
    AggregateId     NVARCHAR(256) NOT NULL,
    CreatedAt       DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_IdentityMap PRIMARY KEY CLUSTERED (ExternalSystem, ExternalId, AggregateType),
    INDEX IX_IdentityMap_AggregateId (AggregateType, AggregateId)
);
```
