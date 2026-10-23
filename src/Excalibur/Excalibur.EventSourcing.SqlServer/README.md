# Excalibur.EventSourcing.SqlServer

SQL Server implementation of event sourcing infrastructure for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.SqlServer` | Starter | + Dispatch core + Outbox + Hosting |
| `Excalibur.SqlServer` | Complete | + Inbox + Saga + Leader Election + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

## Features

- `SqlServerEventStore` - Dapper-based event store implementation
- `SqlServerSnapshotStore` - SQL Server snapshot persistence
- Optimized for high-throughput event streaming
- Connection factory pattern for multi-database scenarios
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Recommended: Builder-integrated registration
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlServer(options =>
    {
        options.ConnectionString = connectionString;
        options.EventStoreSchema = "events";
    });
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
}));

// Alternative: Direct registration
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
});
```

## Database Schema

The store does **not** create its tables at runtime. Provision them before the first append, by
running the scripts shipped inside this package under `scripts/`:

| Script | Creates | Required |
|---|---|---|
| `scripts/001_CreateEventStoreSchema.sql` | `dbo.EventStoreEvents` | Yes — this is the event store itself |
| `scripts/002_CreateSnapshotSchema.sql` | `dbo.EventStoreSnapshots` | Only if you enable snapshots |
| `scripts/003_CreateCursorMapSchema.sql` | `dbo.ProjectionCursorMaps` | Only if a projection resumes from per-stream positions |
| `scripts/004_CreateSubscriptionCheckpointSchema.sql` | `dbo.SubscriptionCheckpoints` | Only if you run catch-up subscriptions |
| `scripts/006_CreateOperationReceipts.sql` | `dbo.EventStoreEventsOperations`, `dbo.EventStoreEventsSources` | For atomic operation receipts and source-generation binding |

Apply fresh-install scripts 001/002 once. The receipt script is guarded; it does not delete existing receipts or source bindings.

`EventStoreEvents.TenantId` is created `NOT NULL`, and an untenanted event stores the reserved
`__untenanted__` sentinel rather than `NULL`, so an untenanted event is a value rather than a missing
one and the stream-identity constraint binds it like any other.

A database provisioned by an earlier prerelease has no in-place upgrade path; re-provision it from
these scripts.

## Atomic retry identities

`SqlServerOperationExecutor` protects **one nonempty event append** per operation. Supply fresh
owned primary connections to the same database, the same schema/event-table names, and the same
`ITenantContext` as `SqlServerEventStore`. Apply script 006 first. For custom table names the receipt
and source tables are `<events-table>Operations` and `<events-table>Sources` in that schema.

```csharp
var operations = new SqlServerOperationExecutor(
    () => new SqlConnection(connectionString), tenantContext);

var outcome = await operations.ExecuteAsync(
    scope: "orders:create:authenticated-caller-id",
    operationKey: clientSuppliedRetryKey,
    fingerprint: SHA256.HashData(immutableRequestBytes),
    action: ct => SaveOrderAsync(ct),
    cancellationToken);
```

Allocate the client operation key **before sending** and reuse it after a lost response. The fingerprint
must describe immutable input, including target and original preconditions; do not hash newly generated
event IDs or current enrichment-query results. The scope must distinguish authenticated callers and
operation namespaces. The provider additionally partitions identities by tenant and event-store names.

The executor checks the receipt before invoking the callback. The SQL event store inserts that receipt
in the **same local transaction as the events**, including its transactional-outbox append path. Receipt
uniqueness is the concurrency arbiter. The initial lookup connection is released before handler execution,
so a small SQL pool cannot fill with coordinators waiting for a second connection. Concurrent callbacks
may run, but a losing attempt rolls back its candidate append and recovers the original receipt. A durable
attempt ID distinguishes the winner from an attempt whose insert was rolled back. A receipt carries
the original aggregate ID/type, committed stream version and global positions. Reusing a key with different
input throws `SqlServerOperationConflictException`. A replay can return HTTP 202 with the original read
location/session token rather than reproduce a stale HTTP response body. Buffer HTTP response output
until reconciliation; a controller's losing concurrency response must not escape before the receipt is checked.

This is not a general transaction around arbitrary handler code. The callback must await its work and
perform at most one nonempty append; multiple events in that append are supported. Independently committed
business writes, external calls, erasure/archive operations and multiple aggregate saves are outside this
contract. Nested scopes, extra append attempts and changed destinations/tenants are rejected. No-event
callbacks have no receipt and can run again. Retain receipts for the entire accepted retry lifetime;
automatically deleting them reopens the duplicate-write window. Cancellation or a commit error does not
prove rollback: retry the same identity and reconcile its durable receipt.

For CDC, derive the identity from configured source/consumer, capture generation, table, LSN, sequence
value and operation. Keep the receipt in the **target** event-store database; CDC can then safely retry
after the target commit but before its separate source processed-mark/checkpoint. Optional CDC deduplication
filters remain useful bookkeeping, but are not a substitute for this atomic target receipt.

`BindSourceAsync` durably binds a stable configured source name to a SHA-256 identity describing its
database GUID, recovery fork and capture object's creation identity. Check it at startup and before delivery.
Do not use `cdc.change_tables.start_lsn` as an incarnation: cleanup advances that watermark. Source
replacement throws `SqlServerSourceChangedException`; it never silently accepts a new history.

Before restoring a source, stop **all** consumers and drain in-flight work. After restore, reconcile or
rebuild checkpoints, identity mappings, target events and projections before explicitly rebinding. The
binding detects replacement; it does not fence an administrator forcing a restore during an in-flight
handler. Store bindings outside source backup refreshes. Restoring the target database also rolls back
receipts and may leave Elasticsearch ahead of SQL, requiring coordinated projection recovery.

## Related Packages

- `Excalibur.EventSourcing` - Core event sourcing abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
