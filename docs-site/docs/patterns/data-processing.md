---
sidebar_position: 11
title: Data Processing
description: Batch data processing pipelines with producer-consumer architecture, keyed connection factories, and configurable orchestration.
---

# Data Processing

Excalibur's data processing module provides a producer-consumer pipeline for batch processing database records. It handles orchestration (task tracking, progress, retries), while you supply the data fetching and record handling logic.

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Data.DataProcessing
  ```
- A SQL database for orchestration tables (task requests, progress tracking)
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [IOptions pattern](../configuration/index.md)

## Architecture

```mermaid
flowchart LR
    subgraph Orchestration
        DM[DataOrchestrationManager]
        DB[(Orchestration DB)]
        DM --> DB
    end

    subgraph Pipeline
        DP["DataProcessor‹TRecord›"]
        Q["Channel‹TRecord›"]
        RH["IRecordHandler‹TRecord›"]
        DP -- produces --> Q
        Q -- consumes --> RH
    end

    DM --> DP
```

The `DataOrchestrationManager` tracks data task requests in a SQL table. Each `DataProcessor<TRecord>` runs a producer-consumer loop: the producer fetches batches from the source database and writes to an in-memory `Channel<TRecord>`, while the consumer reads from the channel and delegates to `IRecordHandler<TRecord>` implementations.

## Quick Start

### 1. Define a record type

```csharp
public record CustomerRecord(int Id, string Name, string Email);
```

### 2. Implement a data processor

```csharp
public class CustomerProcessor : DataProcessor<CustomerRecord>
{
    private readonly Func<IDbConnection> _connectionFactory;

    public CustomerProcessor(
        [FromKeyedServices("customers")] Func<IDbConnection> connectionFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<DataProcessingOptions> configuration,
        IServiceProvider serviceProvider,
        ILogger<CustomerProcessor> logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
        _connectionFactory = connectionFactory;
    }

    public override async Task<CursorFetchResult<CustomerRecord>> FetchBatchAsync(
        string? cursor, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory();
        return await connection.Ready().ResolveAsync(
            new SelectCustomerBatch(cursor, batchSize, cancellationToken));
    }
}
```

### 3. Implement a record handler

```csharp
public class CustomerMigrationHandler : IRecordHandler<CustomerRecord>
{
    private readonly ILogger<CustomerMigrationHandler> _logger;

    public CustomerMigrationHandler(ILogger<CustomerMigrationHandler> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(CustomerRecord record, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing customer {Id}: {Name}", record.Id, record.Name);
        // Transform, validate, write to target database, etc.
        await Task.CompletedTask;
    }
}
```

### 4. Register services

```csharp
// AOT-safe explicit registration (recommended)
builder.Services.AddDataProcessor<CustomerProcessor>(
    builder.Configuration, "DataProcessing");
builder.Services.AddRecordHandler<CustomerMigrationHandler, CustomerRecord>();

// Register the source database connection factory
builder.Services.AddKeyedSingleton<Func<IDbConnection>>(
    "customers",
    (_, _) => () => new SqlConnection(customersConnectionString));
```

## DI Registration

### Assembly Scanning (Reflection-Based)

Discovers all `IDataProcessor` and `IRecordHandler<T>` implementations via assembly scanning. Registers the orchestration connection factory as a keyed singleton.

```csharp
builder.Services.AddDataProcessing(
    () => new SqlConnection(orchestrationConnectionString),
    builder.Configuration,
    "DataProcessing",
    typeof(Program).Assembly);
```

:::warning AOT Compatibility
`AddDataProcessing` uses reflection-based assembly scanning and is annotated with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`. For AOT-safe deployments, use the explicit generic overloads below.
:::

### AOT-Safe Explicit Registration

Register individual processors and handlers without assembly scanning:

```csharp
// Bare registration (no configuration)
builder.Services.AddDataProcessor<CustomerProcessor>();
builder.Services.AddRecordHandler<CustomerMigrationHandler, CustomerRecord>();

// With inline configuration object
builder.Services.AddDataProcessor<CustomerProcessor>(new DataProcessingOptions
{
    QueueSize = 128,
    ProducerBatchSize = 50,
    ConsumerBatchSize = 20
});

// With IConfiguration binding (recommended for production)
builder.Services.AddDataProcessor<CustomerProcessor>(
    builder.Configuration, "DataProcessing");
builder.Services.AddRecordHandler<CustomerMigrationHandler, CustomerRecord>(
    builder.Configuration, "DataProcessing");
```

### Registration API Reference

| Method | Configuration | Validation |
|--------|--------------|------------|
| `AddDataProcessor<T>()` | None | None |
| `AddDataProcessor<T>(DataProcessingOptions)` | Inline object | `ValidateDataAnnotations` + `ValidateOnStart` + cross-property |
| `AddDataProcessor<T>(IConfiguration, string)` | Bind from section | `ValidateDataAnnotations` + `ValidateOnStart` |
| `AddRecordHandler<T,R>()` | None | None |
| `AddRecordHandler<T,R>(DataProcessingOptions)` | Inline object | `ValidateDataAnnotations` + `ValidateOnStart` + cross-property |
| `AddRecordHandler<T,R>(IConfiguration, string)` | Bind from section | `ValidateDataAnnotations` + `ValidateOnStart` |
| `AddDataProcessing(Func, IConfig, string, Assembly[])` | Assembly scanning + bind | `ValidateDataAnnotations` + `ValidateOnStart` |
| `EnableDataProcessingBackgroundService(Action?)` | Optional configure action | `ValidateDataAnnotations` + `ValidateOnStart` + cross-property |
| `EnableDataProcessingBackgroundService(IConfiguration, string)` | Bind from section | `ValidateDataAnnotations` + `ValidateOnStart` + cross-property |

## Configuration

### DataProcessingOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SchemaName` | `string` | `"DataProcessor"` | SQL schema for the orchestration table |
| `TableName` | `string` | `"DataTaskRequests"` | SQL table name for orchestration task records |
| `QualifiedTableName` | `string` | (computed) | Read-only `[SchemaName].[TableName]` with bracket-escaping |
| `QueueSize` | `int` | 5000 | In-memory channel capacity between producer and consumer |
| `ProducerBatchSize` | `int` | 100 | Records fetched per producer iteration |
| `ConsumerBatchSize` | `int` | 10 | Records dequeued per consumer iteration |
| `MaxAttempts` | `int` | 3 | Maximum retry attempts per data task |
| `DispatcherTimeoutMilliseconds` | `int` | 60000 | Timeout for a dispatcher to process tasks (ms) |

All numeric properties require values > 0 (enforced by `[Range(1, int.MaxValue)]`).

### appsettings.json

```json
{
  "DataProcessing": {
    "SchemaName": "DataProcessor",
    "TableName": "DataTaskRequests",
    "QueueSize": 128,
    "ProducerBatchSize": 50,
    "ConsumerBatchSize": 20,
    "MaxAttempts": 3,
    "DispatcherTimeoutMilliseconds": 60000
  }
}
```

### Cross-Property Validation

An `IValidateOptions<DataProcessingOptions>` validator enforces inter-property constraints at startup:

| Rule | Constraint |
|------|-----------|
| `ProducerBatchSize` must not exceed `QueueSize` | Prevents the producer from overwhelming the channel |
| `ConsumerBatchSize` must not exceed `QueueSize` | Prevents impossible dequeue sizes |
| `DispatcherTimeoutMilliseconds` must be 1,000–3,600,000 | Enforces 1 second to 1 hour range |

If any constraint fails, the application throws `OptionsValidationException` at startup (fail-fast).

## Database Setup

The data processing system requires one table in your SQL Server database. Create the schema and table before starting the application:

```sql
-- Create the schema (if it doesn't exist)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'DataProcessor')
BEGIN
    EXEC('CREATE SCHEMA [DataProcessor]');
END
GO

-- Create the data task requests table
IF NOT EXISTS (SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[DataProcessor].[DataTaskRequests]') AND type = N'U')
BEGIN
    CREATE TABLE [DataProcessor].[DataTaskRequests]
    (
        [DataTaskId]      UNIQUEIDENTIFIER  NOT NULL,
        [CreatedAt]       DATETIMEOFFSET    NOT NULL,
        [RecordType]      NVARCHAR(256)     NOT NULL,
        [Attempts]        INT               NOT NULL DEFAULT 0,
        [MaxAttempts]     INT               NOT NULL DEFAULT 3,
        [CompletedCount]  INT               NOT NULL DEFAULT 0,
        [FetchCursor]     NVARCHAR(512)     NULL,
        [ProcessedCursor] NVARCHAR(512)     NULL,

        CONSTRAINT [PK_DataTaskRequests] PRIMARY KEY CLUSTERED ([DataTaskId])
    );

    -- Filtered index for the polling query (SELECT ... WHERE Attempts < MaxAttempts ORDER BY CreatedAt)
    CREATE NONCLUSTERED INDEX [IX_DataTaskRequests_Pending]
        ON [DataProcessor].[DataTaskRequests] ([Attempts], [MaxAttempts])
        INCLUDE ([DataTaskId], [CreatedAt], [RecordType], [CompletedCount], [FetchCursor], [ProcessedCursor])
        WHERE [Attempts] < [MaxAttempts];
END
GO
```

If you customize `SchemaName` or `TableName` in `DataProcessingOptions`, update the script accordingly. A complete setup script is included in the [DataProcessingBackgroundService sample](https://github.com/nickniverson/Excalibur.Dispatch/tree/main/samples/09-advanced/deployment/DataProcessingBackgroundService/setup-database.sql).

## Orchestration Connection

The `AddDataProcessing` method registers the orchestration database connection factory as a **keyed singleton** under `DataProcessingKeys.OrchestrationConnection`. This connection is used by `DataOrchestrationManager` to manage data task records.

```csharp
// Resolve explicitly in your own services
public class MyService(
    [FromKeyedServices(DataProcessingKeys.OrchestrationConnection)]
    Func<IDbConnection> orchestrationFactory)
{
    // orchestrationFactory creates connections to the orchestration database
}
```

The key value is `"Excalibur.DataProcessing.Orchestration"`.

## Multi-Database

When processors need different source databases, use keyed services (introduced in .NET 8, stable in .NET 10):

```csharp
var orchestrationDb = builder.Configuration.GetConnectionString("Orchestration");
var customersDb = builder.Configuration.GetConnectionString("CustomersDb");
var inventoryDb = builder.Configuration.GetConnectionString("InventoryDb");

// Orchestration database (registered as keyed singleton automatically)
builder.Services.AddDataProcessing(
    () => new SqlConnection(orchestrationDb),
    builder.Configuration,
    "DataProcessing",
    typeof(Program).Assembly);

// Source database factories for individual processors
builder.Services.AddKeyedSingleton<Func<IDbConnection>>(
    "customers",
    (_, _) => () => new SqlConnection(customersDb));

builder.Services.AddKeyedSingleton<Func<IDbConnection>>(
    "inventory",
    (_, _) => () => new SqlConnection(inventoryDb));
```

Each processor injects its keyed factory:

```csharp
public class CustomerProcessor : DataProcessor<CustomerRecord>
{
    private readonly Func<IDbConnection> _connectionFactory;

    public CustomerProcessor(
        [FromKeyedServices("customers")] Func<IDbConnection> connectionFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<DataProcessingOptions> configuration,
        IServiceProvider serviceProvider,
        ILogger<CustomerProcessor> logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
        _connectionFactory = connectionFactory;
    }

    public override async Task<CursorFetchResult<CustomerRecord>> FetchBatchAsync(
        string? cursor, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory();
        return await connection.Ready().ResolveAsync(
            new SelectCustomerBatch(cursor, batchSize, cancellationToken));
    }
}
```

See [Multi-Database Support](../data-providers/multi-database.md#data-processing-multi-database) for full details including configuration examples.

## Key Abstractions

### IDataProcessor

The core processing interface. Implementations run the producer-consumer pipeline.

```csharp
public interface IDataProcessor : IAsyncDisposable, IDisposable
{
    Task<long> RunAsync(
        long completedCount,
        string? processedCursor,
        UpdateCompletedCount updateCompletedCount,
        CancellationToken cancellationToken);
}
```

### DataProcessor\<TRecord\>

Abstract base class providing the producer-consumer pipeline. You implement `FetchBatchAsync` to supply records:

```csharp
public abstract class DataProcessor<TRecord> : IDataProcessor, IRecordFetcher<TRecord>
{
    // You implement this:
    public abstract Task<CursorFetchResult<TRecord>> FetchBatchAsync(
        string? cursor, int batchSize, CancellationToken cancellationToken);
}
```

The base class handles:
- Channel-based producer-consumer coordination
- Batch sizing (configurable via `DataProcessingOptions`)
- Cursor-based progress tracking (via `UpdateCompletedCount` delegate with opaque cursor tokens)
- Crash-safe resume (on restart, fetch cursor resets to last durable `ProcessedCursor`)
- Graceful shutdown on application stop
- Logging via `[LoggerMessage]` source generation

### IRecordFetcher\<TRecord\>

Defines cursor-based batch fetching. `DataProcessor<TRecord>` implements this interface — you provide the implementation via `FetchBatchAsync`:

```csharp
public interface IRecordFetcher<TRecord>
{
    Task<CursorFetchResult<TRecord>> FetchBatchAsync(
        string? cursor, int batchSize, CancellationToken cancellationToken);
}
```

The `cursor` parameter is an opaque token produced by the previous call's `NextCursor`. Pass `null` to start from the beginning. Your implementation defines the cursor format (e.g., a primary key, timestamp, or composite value).

### CursorFetchResult\<TRecord\>

Returned by `FetchBatchAsync` to carry a page of records and the cursor to the next page:

```csharp
public sealed record CursorFetchResult<TRecord>(
    IReadOnlyList<TRecord> Records,
    string? NextCursor);
```

- **`Records`** — the records in this page.
- **`NextCursor`** — an opaque cursor pointing to the start of the next page, or `null` if there are no more pages. An empty `Records` collection with a non-null cursor is valid (the page was empty but more data may follow).

### UpdateCompletedCount

Delegate used by the orchestration layer to checkpoint progress:

```csharp
public delegate Task UpdateCompletedCount(
    long complete, string? processedCursor, CancellationToken cancellationToken);
```

- **`complete`** — the running total of processed records.
- **`processedCursor`** — the cursor of the last fully processed page boundary, or `null` for per-record count-only checkpoints (the SQL layer uses `COALESCE` to preserve the existing cursor when `null` is passed).

### IRecordHandler\<TRecord\>

Processes individual records from the consumer side of the channel:

```csharp
public interface IRecordHandler<in TRecord>
{
    Task ProcessAsync(TRecord record, CancellationToken cancellationToken);
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Registration | Use AOT-safe `AddDataProcessor<T>` for production; assembly scanning for prototyping |
| Configuration | Use `IConfiguration` binding with `ValidateOnStart` for fail-fast startup |
| Connection management | Use `Func<IDbConnection>` factories with keyed services for multi-database |
| Batch sizes | Set `ProducerBatchSize` and `ConsumerBatchSize` at or below `QueueSize` (validated at startup) |
| Timeouts | Keep `DispatcherTimeoutMilliseconds` at 1000ms or above; default 60s is suitable for most cases |
| Error handling | Orchestration DB calls are automatically retried when `Excalibur.Data.SqlServer` is registered; implement domain-level retry in `IRecordHandler<T>.ProcessAsync`; `MaxAttempts` controls task-level retries |

## Background Processing

Instead of scheduling data processing via Quartz jobs, you can use the built-in `BackgroundService` that polls for pending data tasks on a configurable interval.

### Enable Background Service

```csharp
// Enable with defaults (5s polling interval)
builder.Services.EnableDataProcessingBackgroundService();

// Enable with custom options via delegate
builder.Services.EnableDataProcessingBackgroundService(options =>
{
    options.PollingInterval = TimeSpan.FromSeconds(10);
    options.DrainTimeoutSeconds = 60;
    options.UnhealthyThreshold = 5;
});

// Or bind from appsettings.json section (AOT-safe)
builder.Services.EnableDataProcessingBackgroundService(
    builder.Configuration, "DataProcessingService");
```

The hosted service calls `IDataOrchestrationManager.ProcessDataTasksAsync()` on each polling cycle. It works with both the assembly-scanning registration path (`AddDataProcessing`) and the AOT-safe explicit registration path (`AddDataProcessor<T>`).

:::info Two Separate Options Classes
Configuration is split into two concerns:

- **`DataProcessingOptions`** -- pipeline tuning: `SchemaName`, `TableName`, `QueueSize`, `ProducerBatchSize`, `ConsumerBatchSize`, `MaxAttempts`
- **`DataProcessingHostedServiceOptions`** -- polling lifecycle: `PollingInterval`, `Enabled`, `DrainTimeoutSeconds`, `UnhealthyThreshold`

```csharp
// Pipeline tuning (queue sizes, batching, table config)
builder.Services.AddDataProcessor<OrderDataProcessor>(new DataProcessingOptions
{
    SchemaName = "DataProcessor",
    TableName = "DataTaskRequests",
    QueueSize = 500,
    ProducerBatchSize = 50,
    ConsumerBatchSize = 10,
    MaxAttempts = 3,
});
builder.Services.AddRecordHandler<OrderRecordHandler, OrderRecord>();

// Or bind pipeline options from appsettings.json:
// builder.Services.AddDataProcessor<OrderDataProcessor>(
//     builder.Configuration, "DataProcessing");

// Polling/lifecycle tuning (separate concern) -- delegate
builder.Services.EnableDataProcessingBackgroundService(options =>
{
    options.PollingInterval = TimeSpan.FromSeconds(10);
    options.DrainTimeoutSeconds = 60;
});

// Or bind polling/lifecycle from appsettings.json:
// builder.Services.EnableDataProcessingBackgroundService(
//     builder.Configuration, "DataProcessingService");
```

Both can also be configured entirely via `appsettings.json`:

```json
{
  "DataProcessing": {
    "SchemaName": "DataProcessor",
    "TableName": "DataTaskRequests",
    "QueueSize": 500,
    "ProducerBatchSize": 50,
    "ConsumerBatchSize": 10,
    "MaxAttempts": 3
  },
  "DataProcessingService": {
    "PollingInterval": "00:00:10",
    "Enabled": true,
    "DrainTimeoutSeconds": 60,
    "UnhealthyThreshold": 5
  }
}
```

:::

### DataProcessingHostedServiceOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PollingInterval` | `TimeSpan` | 5 seconds | Interval between polling cycles |
| `Enabled` | `bool` | `true` | Whether the background processor is active |
| `DrainTimeoutSeconds` | `int` | 30 | Seconds to wait for in-flight processing during shutdown |
| `UnhealthyThreshold` | `int` | 3 | Consecutive errors before the service is considered unhealthy |

:::tip Computed Property
`DrainTimeout` is a read-only `TimeSpan` computed from `DrainTimeoutSeconds`. Use `DrainTimeoutSeconds` in configuration.
:::

### Cross-Property Validation

An `IValidateOptions<DataProcessingHostedServiceOptions>` validator enforces at startup:

| Rule | Constraint |
|------|-----------|
| `PollingInterval` must be positive | Prevents zero or negative polling |
| `DrainTimeout` must exceed `PollingInterval` | Ensures the drain window covers at least one full cycle |
| `UnhealthyThreshold` must be >= 1 | At least one error before marking unhealthy |

### Health Checks

The data processing subsystem integrates with ASP.NET Core health checks via a 3-tier model:

| Status | Condition |
|--------|-----------|
| **Healthy** | Service is running and processing within normal intervals |
| **Degraded** | No activity for longer than `DegradedInactivityTimeout` (default 5 minutes) |
| **Unhealthy** | Service stopped, or no activity for longer than `UnhealthyInactivityTimeout` (default 10 minutes) |

Register the health check:

```csharp
builder.Services.AddHealthChecks()
    .AddDataProcessingHealthCheck();

// Or with custom options:
builder.Services.AddHealthChecks()
    .AddDataProcessingHealthCheck(
        name: "data_processing",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready" });

// Configure inactivity thresholds:
builder.Services.Configure<DataProcessingHealthCheckOptions>(options =>
{
    options.DegradedInactivityTimeout = TimeSpan.FromMinutes(3);
    options.UnhealthyInactivityTimeout = TimeSpan.FromMinutes(8);
});
```

The health check reports diagnostic data including `IsRunning`, `TotalProcessed`, `TotalFailed`, `TotalCycles`, `LastActivityTime`, and `InactivitySeconds`.

:::tip Internal Tracking
The hosted service also tracks basic health state internally (`IsHealthy`, `ConsecutiveErrors`, `LastSuccessfulProcessing`) and marks itself unhealthy after `UnhealthyThreshold` consecutive errors. The `IHealthCheck` integration is the recommended approach for production monitoring.
:::

### Resilience

When `Excalibur.Data.SqlServer` (or another provider that registers `IDataAccessPolicyFactory`) is present, all orchestration database operations are automatically wrapped in a comprehensive resilience policy:

- **Retry** — 3 attempts with exponential backoff and jitter for transient SQL failures (timeouts, deadlocks, connection resets, Azure SQL throttling)
- **Circuit breaker** — Opens after sustained failure (50% failure rate over 60 seconds, minimum 5 requests) and stays open for 30 seconds before allowing probe requests

This covers all five orchestration DB operations: task insertion, pending task selection, attempt count updates, completed count checkpoints, and task deletion.

When no `IDataAccessPolicyFactory` is registered (e.g., when using a non-SQL Server backend without a Polly provider), operations execute directly without retry or circuit breaker wrapping.

```csharp
// Resilience is automatic when SqlServer is registered:
builder.Services.AddExcaliburDataSqlServer(options =>
{
    options.ConnectionString = connectionString;
});

// IDataAccessPolicyFactory is now available — DataProcessing
// automatically discovers and uses it for all DB operations.
builder.Services.AddDataProcessing(dp =>
{
    dp.ConnectionFactory(() => new SqlConnection(connectionString))
      .AddProcessor<OrderProcessor>()
      .EnableBackgroundProcessing();
});
```

### Database Restore Survivability

The data processing pipeline is designed to survive database unavailability and data replacement (e.g., restoring from a production backup):

**During database unavailability (restore in progress):**
- All DB operations are guarded with try-catch — the processing loop continues without crashing
- Attempt count and delete operations log failures but don't propagate exceptions
- The resilience policy (when registered) retries transient failures before surfacing errors
- The health check transitions to Degraded, then Unhealthy based on inactivity duration

**After data replacement (restored from backup):**
- **Stale task detection** — Each completed-count checkpoint verifies the task row still exists (checks rows affected). If the row was replaced or deleted during a restore, the checkpoint returns 0 rows and the processor aborts that task cleanly
- **Consecutive failure threshold** — If a processor encounters 5 consecutive record-level failures (e.g., foreign key violations from mismatched data), it breaks out of the current batch instead of burning through the entire queue
- **Idempotent re-processing** — Tasks that fail to be deleted after completion are re-selected on the next poll; processors should handle re-processing idempotently via the `completedCount` and `processedCursor` resume parameters

### Configuration via appsettings.json

```json
{
  "DataProcessingService": {
    "PollingInterval": "00:00:10",
    "Enabled": true,
    "DrainTimeoutSeconds": 60,
    "UnhealthyThreshold": 5
  }
}
```

```csharp
builder.Services.EnableDataProcessingBackgroundService(
    builder.Configuration, "DataProcessingService");
```

### Complete Example

```csharp
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// 1. Register orchestration connection
builder.Services.AddKeyedSingleton(
    DataProcessingKeys.OrchestrationConnection,
    (_, _) => (Func<IDbConnection>)(() => new SqlConnection(connectionString)));

// 2. Register processor and handler (AOT-safe)
builder.Services.AddDataProcessor<OrderDataProcessor>(
    builder.Configuration, "DataProcessing");
builder.Services.AddRecordHandler<OrderRecordHandler, OrderRecord>();

// 3. Enable background service (bind from appsettings.json)
builder.Services.EnableDataProcessingBackgroundService(
    builder.Configuration, "DataProcessingService");

// 4. Register health check (optional but recommended)
builder.Services.AddHealthChecks()
    .AddDataProcessingHealthCheck();

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

### Quartz vs Background Service

| Aspect | Quartz Job | Background Service |
|--------|-----------|-------------------|
| Scheduling | Cron expressions, complex schedules | Fixed polling interval |
| Dependencies | Requires `Excalibur.Jobs` + Quartz.NET | Built-in, no extra packages |
| Concurrency control | Job disallow concurrent execution | Single hosted service instance |
| Health monitoring | Via Quartz job listeners | Built-in `IHealthCheck` with Healthy/Degraded/Unhealthy tiers |
| Graceful shutdown | Quartz scheduler shutdown | `DrainTimeout` with linked cancellation |
| Best for | Complex schedules, multi-job orchestration | Simple polling, minimal dependencies |

## See Also

- [Multi-Database Support](../data-providers/multi-database.md#data-processing-multi-database) -- Keyed service registration for multi-database processors
- [Configuration Overview](../configuration/index.md) -- IOptions pattern and ValidateOnStart
- [SQL Server Provider](../data-providers/sqlserver.md) -- SQL Server connection setup
