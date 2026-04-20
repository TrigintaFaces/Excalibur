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

- Running data processing as a hosted service
- Using `IDataOrchestrationManager` for task scheduling
- Producer/consumer batch processing via channels
- ASP.NET Core integration with Dispatch

## Run

```bash
dotnet run
# Then POST to http://localhost:5000/api/tasks/OrderRecord
```
