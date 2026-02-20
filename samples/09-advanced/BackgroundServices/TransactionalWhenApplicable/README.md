# TransactionalWhenApplicable - Exactly-Once Delivery

This example demonstrates **exactly-once delivery** using atomic transactions when outbox and inbox share the same database.

## Overview

- **DeliveryGuarantee**: `TransactionalWhenApplicable`
- **Behavior**: Atomic outbox+inbox completion in single SQL Server transaction
- **Result**: Exactly-once delivery (zero duplicates)
- **Fallback**: MinimizedWindow when same-database not detected

## When to Use

Choose TransactionalWhenApplicable when:
- You need **exactly-once semantics** (no duplicates allowed)
- Outbox and inbox are in the **same SQL Server database**
- You can accept the additional overhead of inbox insertion

## Requirements

1. **SQL Server** outbox and inbox stores (other providers not supported)
2. **Same connection string** for both outbox and inbox
3. **SqlServerInboxOptions** provided to SqlServerOutboxStore constructor

## Configuration

```csharp
// Same connection string for both
var connectionString = "Server=...;Database=SharedDb;...";

services.Configure<SqlServerOutboxOptions>(opt => opt.ConnectionString = connectionString);
services.Configure<SqlServerInboxOptions>(opt => opt.ConnectionString = connectionString);

services.Configure<OutboxOptions>(options =>
{
    options.DeliveryGuarantee = OutboxDeliveryGuarantee.TransactionalWhenApplicable;
});

// Register with inbox options for transactional completion
services.AddSingleton(sp =>
{
    var outboxOpts = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
    var inboxOpts = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>();
    var logger = sp.GetRequiredService<ILogger<SqlServerOutboxStore>>();
    return new SqlServerOutboxStore(outboxOpts, inboxOpts, logger);
});
```

## How It Works

When same-database is detected:
1. `TryMarkSentAndReceivedAsync` is called
2. Single transaction: UPDATE outbox + INSERT inbox
3. Both succeed or both roll back
4. Message either in both tables or neither

## Fallback Behavior

Falls back to MinimizedWindow when:
- Connection strings don't match (different databases)
- SqlServerInboxOptions not provided
- Transaction fails

## Running the Example

```bash
cd samples/BackgroundServices/TransactionalWhenApplicable
dotnet run
