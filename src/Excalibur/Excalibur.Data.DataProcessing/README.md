# Excalibur.Data.DataProcessing

Data processing utilities for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Data.DataProcessing
```

## Features

- Batch processing
- ETL operations
- Data transformation
- Bulk operations

## Fixed SQL execution

The built-in task requests execute statically bound ADO.NET commands and use explicit row mapping.
Their existing command and parameter metadata remains available; execution does not generate
parameter binders or row materializers at runtime. Cancellation, command timeouts and explicit
transactions remain part of the command contract.

Requests dispose their commands and readers. They leave an already-open caller connection open;
a connection opened for a request is closed after completion or failure. This does not make every
consumer-provided data request NativeAOT compatible: validate custom execution paths separately.

## The queue table is transient

`AddDataTaskForRecordTypeAsync` writes one row and returns its identifier. That row does not stay:

| outcome | the row |
|---|---|
| the processor completed the task | **deleted** |
| the processor failed | kept, with its attempt count raised |
| the attempt count reached the maximum | kept, and no longer selected — a failed backfill stays visible |

So **an absent row is not evidence that the enqueue failed.** With a drain running — a background
service, or a scheduled job — a task whose processor succeeds can be gone within one polling interval.
Query by the returned identifier to tell an enqueue that never happened from one already processed.

Each enqueue logs, at `Information`, the identifier it wrote, the schema-qualified table, the database
the write actually landed in, and whether an ambient transaction was in scope. That line is the fastest
way to settle where a task went:

```text
Queued data task 0190c3f1-… for record type 'Orders' into [DataProcessor].[DataTaskRequests]
on database 'AppDb'. Ambient transaction: none.
```

Two further reasons a row can appear missing, both of which the enqueue reports no error for:

- **It is in another schema than the one you are looking at.** The default is
  `[DataProcessor].[DataTaskRequests]`, not the connection's default schema — and it is written on the
  connection you supplied to the registration, which need not be the database the rest of the
  application uses.
- **An ambient transaction rolled it back.** The enqueue participates in `Transaction.Current` on
  purpose, so queueing work can be part of the business transaction that decided to queue it. A scope
  disposed without being completed therefore discards the row. The drain refuses an ambient transaction
  instead, because processing must not be undone by one.

## Tenancy

The data task queue is a global, type-wide sweep: it runs outside any tenant scope, and its enqueue and
drain surfaces (`IDataOrchestrationManager.AddDataTaskForRecordTypeAsync`, the pending-task drain) accept
no tenant identity. If your `IRecordFetcher` records are tenant-owned, your fetcher must apply the tenant
predicate itself -- the queue applies none.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Database schema

**This package does not create its tables.** The DDL ships inside the `.nupkg` under `scripts/`, and
you apply it — so a schema change to your database is always something you did deliberately, never
something a version upgrade did to you while you were not looking.

| Script |
|---|
| `scripts/001_CreateDataProcessingSchema.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.data.dataprocessing/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.

## Task ownership and recovery

### Choosing regular or bulk processing

`DataProcessor<TRecord>` preserves inline projection waits. Use it when a handler needs to read
projections updated by its own writes or by an earlier record. `BulkDataProcessor<TRecord>` is an
explicit opt-in for backfills whose handlers do not depend on those reads. It still processes records
sequentially and awaits every durable checkpoint; it does not batch away checkpoints or parallelize handlers.

Derive your processor from `BulkDataProcessor<TRecord>` using the same constructor and
`FetchBatchAsync` implementation. Register it with `AddProcessor<TProcessor>()` and register the real
`IProjectionProcessor` for your application's subscriptions. The bulk processor resolves that service
before handling any records, suppresses per-record inline projection waits, then attempts catch-up once
after records and checkpoints complete. Ordinary processors do not resolve this service.

Handler/checkpoint failures skip catch-up. Catch-up exceptions and cancellation fail the run without
undoing saved records or durable checkpoints. Recovery may replay the last page; handlers must remain
idempotent. Other non-caught-up outcomes emit a warning and allow record-task completion: a returned
record count is **not** proof that projections are current. Catch-up covers the processor's configured
subscription and captured stream head. The orchestration stall/total deadlines also cover catch-up;
no artificial progress checkpoints are generated while waiting.

When migrating from an application-owned `BulkDataProcessor` wrapper, remove its `RunAsync` override
that calls `ProcessPendingAsync`; retaining it would perform a second catch-up. Use the framework base
directly. The AOT sample's `DataProcessingSmoke.cs` demonstrates both explicit registrations and a
database-independent bulk contract probe; its projection stub is test infrastructure, not a production
projection processor. No production throughput improvement is claimed by that probe.

Runnable examples are in `samples/09-advanced/persistence-patterns/BulkDataProcessing` (real
repository saves and inline projections, managed execution) and `samples/10-aot/BulkDataProcessing`
(standalone native contract probe). Their READMEs give commands and verification limits.

The SQL Server orchestrator requires a factory that returns a new, closed connection. It claims each task with a database-scoped session application lock, rereads its eligibility, and retains the connection until processing and asynchronous scope cleanup finish. Competing workers skip owned tasks. Each checkpoint, failure update, and deletion verifies ownership on that session. Claim connections disable pooling so a lost grant or release response cannot strand an application lock in the pool; this adds one physical connection per claimed task, not per record.

A lost database session fences subsequent task-state writes. It cannot undo external handler effects already underway. Handlers must remain idempotent and tolerate replay and, after connection loss, overlapping external effects. All workers must use the ownership-aware implementation; older workers do not honor these locks. Drain tasks outside ambient transactions; enqueue may participate in a business transaction.

`ProcessingStallTimeout` bounds how long a claimed task may make NO PROGRESS, not its total duration. The stall deadline restarts after durable progress; a single handler or checkpoint exceeding that interval can still trigger cancellation. Set `ProcessingTotalTimeout` as well only if you need an absolute cap for work that never ends while reporting progress. Cancellation requests a stop, including scope cleanup. Cancellation is cooperative: a handler that ignores it can delay completion. The manager joins processing before releasing ownership. An observed timeout consumes a failed attempt; host cancellation does not. `Attempts` counts observed failures, not process crashes. `CompletedCount` counts successful processing attempts and may include replayed records, rather than distinct records.

Repeated failed hosted cycles degrade health and become unhealthy at `UnhealthyThreshold`; a successful cycle resets the failure streak. Exceeding shutdown drain time reports the service as stopped and unhealthy even if noncooperative work has not yet finished.
