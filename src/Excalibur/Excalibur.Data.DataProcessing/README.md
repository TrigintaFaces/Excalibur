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

The SQL Server orchestrator requires a factory that returns a new, closed connection. It claims each task with a database-scoped session application lock, rereads its eligibility, and retains the connection until processing and asynchronous scope cleanup finish. Competing workers skip owned tasks. Each checkpoint, failure update, and deletion verifies ownership on that session. Claim connections disable pooling so a lost grant or release response cannot strand an application lock in the pool; this adds one physical connection per claimed task, not per record.

A lost database session fences subsequent task-state writes. It cannot undo external handler effects already underway. Handlers must remain idempotent and tolerate replay and, after connection loss, overlapping external effects. All workers must use the ownership-aware implementation; older workers do not honor these locks. Drain tasks outside ambient transactions; enqueue may participate in a business transaction.

`DispatcherTimeoutMilliseconds` requests cancellation for each claimed task, including scope cleanup. Cancellation is cooperative: a handler that ignores it can delay completion. The manager joins processing before releasing ownership. An observed timeout consumes a failed attempt; host cancellation does not. `Attempts` counts observed failures, not process crashes. `CompletedCount` counts successful processing attempts and may include replayed records, rather than distinct records.

Repeated failed hosted cycles degrade health and become unhealthy at `UnhealthyThreshold`; a successful cycle resets the failure streak. Exceeding shutdown drain time reports the service as stopped and unhealthy even if noncooperative work has not yet finished.
