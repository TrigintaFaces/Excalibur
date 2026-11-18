# Data Processing Background Service Sample

Demonstrates running `Excalibur.Data.DataProcessing` as a long-lived `BackgroundService` (instead of Quartz.NET jobs). Shows the producer/consumer channel architecture with task orchestration.

## Architecture

```
POST /api/tasks/{recordType}
  -> IDataOrchestrationManager.AddDataTaskForRecordTypeAsync()
  -> DataProcessingHostedService (polls on interval)
  -> DataProcessor<T>.RunAsync() (producer -> channel -> consumer)
  -> OrderRecordHandler.ProcessAsync()
```

## What You'll Learn

This sample keeps `OrderDataProcessor` on the regular `DataProcessor<TRecord>` path. For an
event-sourced backfill whose handlers never read projections updated during that run, use the framework
`BulkDataProcessor<TRecord>` base and register the application's real `IProjectionProcessor`.
Records and durable checkpoints remain sequential; only inline projection waits are deferred to one
final catch-up attempt. The native sample at
`samples/10-aot/Excalibur.Dispatch.Aot.Sample/DataProcessingSmoke.cs` contains a database-independent
bulk example with a clearly marked projection stub. Do not use that stub in production.

Catch-up cancellation/errors fail the run and can cause last-page replay on recovery; handlers must
remain idempotent. Other non-caught-up outcomes log a warning and do not promise projection freshness.
The existing stall deadline includes final catch-up. Remove any application-owned final-catch-up wrapper
when adopting the framework base so catch-up is not invoked twice.

The runnable managed example at `samples/09-advanced/persistence-patterns/BulkDataProcessing`
demonstrates both modes using real repository saves and persisted inline projections, without a database.

- Running data processing as a hosted service
- Using `IDataOrchestrationManager` for task scheduling
- Producer/consumer batch processing via channels
- ASP.NET Core integration with Dispatch

## Run

Create a SQL Server database and run `setup-database.sql` in it. Set
`ConnectionStrings__DefaultConnection` to that database's connection string. The included localdb
connection is only a Windows development default. The task table is durable; order records are an
in-memory demonstration source, and the handler demonstrates processing rather than a production sink.


```bash
dotnet run
# Then POST to http://localhost:5000/api/tasks/OrderRecord
```

The sample calls `AddDataProcessing` to register the orchestration manager and processor registry,
then enables background polling. Registering a processor alone is insufficient. `/health` executes the
framework's DataProcessing health check.

A handler or checkpoint failure stops its task before later records can advance the cursor. Recovery can
replay the unfinished page, so a production sink must deduplicate by stable record identity. Loading a
backup can remove task rows; that cancels the affected run at its next checkpoint. If a backup restores
old task identifiers or source data, reconcile those tasks and their downstream effects before resuming.
See the consumer documentation on data processing and the SQL Server CDC restore runbook.
