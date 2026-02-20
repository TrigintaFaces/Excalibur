---
sidebar_position: 11
title: Jobs & Workflows
description: Background job execution, workflow orchestration, and distributed coordination with Excalibur.Jobs.
---

# Jobs & Workflows

Excalibur.Jobs provides background job scheduling, multi-step workflows, and distributed job coordination — all powered by [Quartz.NET](https://www.quartz-scheduler.net/) under the hood.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Jobs
  ```
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [.NET hosted services](https://learn.microsoft.com/en-us/dotnet/core/extensions/hosted-services)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Jobs` | Job scheduling, workflows, coordination, distributed locks, built-in jobs |
| `Excalibur.Jobs.Abstractions` | `IBackgroundJob`, `IBackgroundJob<TContext>`, `IJobConfig` interfaces |

## Quick Start

### 1. Create a Job

Implement `IBackgroundJob` for simple jobs, or `IBackgroundJob<TContext>` when you need typed input:

```csharp
using Excalibur.Jobs.Abstractions;

public class CleanupJob : IBackgroundJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Perform cleanup work
    }
}
```

### 2. Register and Schedule

Use `AddExcaliburJobHost` to set up the complete job hosting environment with Quartz.NET:

```csharp
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddExcaliburJobHost(
    configureJobs: jobs =>
    {
        // Cron-scheduled job (runs at midnight daily)
        jobs.AddJob<CleanupJob>("0 0 0 * * ?");

        // Recurring job (runs every 5 minutes)
        jobs.AddRecurringJob<HealthPingJob>(TimeSpan.FromMinutes(5));

        // One-time job (runs immediately on startup)
        jobs.AddOneTimeJob<MigrationJob>();

        // Delayed job (runs 30 seconds after startup)
        jobs.AddDelayedJob<WarmupJob>(TimeSpan.FromSeconds(30));

        // Conditional job (only in production)
        jobs.AddJobIf(builder.Environment.IsProduction(), j =>
            j.AddJob<MetricsAggregationJob>("0 */15 * * * ?"));
    },
    typeof(Program).Assembly);

var app = builder.Build();
app.Run();
```

That's it. `AddExcaliburJobHost` registers Quartz.NET, the hosted service, health checks, and all your jobs in one call.

### 3. Run It

Your job runs on the configured schedule. Quartz.NET handles scheduling, thread management, and trigger persistence.

## Registration Options

There are several ways to register jobs depending on your needs:

### Unified Entry Point (Recommended)

```csharp
services.AddExcaliburJobHost(
    configureQuartz: q =>
    {
        // Low-level Quartz configuration (optional)
        q.UseMicrosoftDependencyInjectionJobFactory();
    },
    configureJobs: jobs =>
    {
        jobs.AddJob<CleanupJob>("0 0 0 * * ?");
        jobs.AddRecurringJob<PingJob>(TimeSpan.FromMinutes(1));
    },
    typeof(Program).Assembly);
```

### Individual Registration

If you need more control, register jobs individually:

```csharp
// Cron-scheduled
services.AddBackgroundJob<CleanupJob>("0 0 0 * * ?");

// With typed context
services.AddBackgroundJob<ReportJob, ReportContext>(
    "0 0 6 * * ?",
    new ReportContext { Format = "PDF", StartDate = DateOnly.FromDateTime(DateTime.Today) });

// Fixed interval
services.AddRecurringJob<PingJob>(TimeSpan.FromMinutes(5));
```

### Job Configurator Fluent API

The `IJobConfigurator` supports chaining:

| Method | Description |
|--------|-------------|
| `AddJob<T>(cron)` | Schedule with a cron expression |
| `AddJob<T, TContext>(cron, context)` | Schedule with typed context |
| `AddRecurringJob<T>(interval)` | Run at fixed intervals |
| `AddOneTimeJob<T>()` | Run once on startup |
| `AddDelayedJob<T>(delay)` | Run once after a delay |
| `AddJobIf(condition, configure)` | Conditionally add jobs |
| `AddJobInstances<T>(configs...)` | Multiple instances with different schedules |

## Jobs with Typed Context

For jobs that need input data, implement `IBackgroundJob<TContext>`:

```csharp
using Excalibur.Jobs.Abstractions;

public class ReportJob : IBackgroundJob<ReportContext>
{
    public async Task ExecuteAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        // Generate report using context.Format, context.StartDate, etc.
    }
}

public class ReportContext
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Format { get; set; } = "PDF";
}
```

Register with context data:

```csharp
services.AddBackgroundJob<ReportJob, ReportContext>(
    "0 0 6 * * ?",  // 6 AM daily
    new ReportContext { Format = "PDF", StartDate = DateOnly.FromDateTime(DateTime.Today) });
```

## Built-In Jobs

Excalibur ships with several ready-to-use jobs. Each implements the Quartz `IJob` interface with `[DisallowConcurrentExecution]` and provides static configuration methods for scheduling and health checks.

### OutboxJob

Processes the [outbox table](outbox.md), publishing pending messages to transports. Use this for reliable at-least-once message delivery.

```csharp
services.AddExcaliburJobHost(configureQuartz: q =>
{
    // Register OutboxJob with its configuration section
    OutboxJob.ConfigureJob(q, builder.Configuration.GetSection("Jobs:Outbox"));
});

// Add its health check
builder.Services.AddHealthChecks();
OutboxJob.ConfigureHealthChecks(
    builder.Services.AddHealthChecks(),
    builder.Configuration.GetSection("Jobs:Outbox"),
    loggerFactory);
```

```json title="appsettings.json"
{
  "Jobs": {
    "Outbox": {
      "JobName": "outbox-processor",
      "CronSchedule": "0/10 * * * * ?",
      "DegradedThreshold": "00:05:00",
      "UnhealthyThreshold": "00:10:00"
    }
  }
}
```

### CdcJob

Runs [change data capture](cdc.md) processing to detect and publish database changes.

```csharp
CdcJob.ConfigureJob(q, builder.Configuration.GetSection("Jobs:Cdc"));
CdcJob.ConfigureHealthChecks(healthChecks, builder.Configuration.GetSection("Jobs:Cdc"), loggerFactory);
```

### DataProcessingJob

Generic data processing pipeline job for batch operations.

```csharp
DataProcessingJob.ConfigureJob(q, builder.Configuration.GetSection("Jobs:DataProcessing"));
DataProcessingJob.ConfigureHealthChecks(healthChecks, builder.Configuration.GetSection("Jobs:DataProcessing"), loggerFactory);
```

### HealthCheckJob

Periodic health check execution job. Runs all registered `IHealthCheck` implementations on a schedule.

```csharp
// Register as a simple background job
services.AddBackgroundJob<HealthCheckJob>("0 */5 * * * ?");  // Every 5 minutes
```

### OutboxProcessorJob

A simpler alternative to `OutboxJob` — implements `IBackgroundJob` instead of Quartz `IJob`. Use this when you want to process the outbox via the `AddBackgroundJob` registration pattern rather than Quartz-native configuration.

```csharp
services.AddRecurringJob<OutboxProcessorJob>(TimeSpan.FromSeconds(10));
```

## Job Configuration

All configurable jobs use `JobConfig` (or a subclass) for their settings:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `JobName` | `string` | `""` | Unique name for the job |
| `JobGroup` | `string` | `"Default"` | Quartz job group |
| `CronSchedule` | `string` | `""` | Cron expression for scheduling |
| `Disabled` | `bool` | `false` | Disables the job without removing it |
| `DegradedThreshold` | `TimeSpan` | 5 minutes | Time without heartbeat before health is degraded |
| `UnhealthyThreshold` | `TimeSpan` | 10 minutes | Time without heartbeat before health is unhealthy |

### Runtime Configuration Changes

Jobs that implement `IConfigurableJob<TConfig>` can be monitored for configuration changes at runtime. When the configuration changes (e.g., `Disabled` toggled), the job is automatically paused or resumed:

```csharp
services.AddJobWatcher<OutboxJob, OutboxJobConfig>(
    builder.Configuration.GetSection("Jobs:Outbox"));
```

## Health Checks

Each job tracks its health via `JobHeartbeatTracker`, a singleton that records when a job last executed successfully. The `JobHealthCheck` compares the last heartbeat against the configured thresholds:

- **Healthy** — Last heartbeat within `DegradedThreshold` (default: 5 minutes)
- **Degraded** — Last heartbeat between `DegradedThreshold` and `UnhealthyThreshold`
- **Unhealthy** — No heartbeat recorded, or last heartbeat exceeds `UnhealthyThreshold` (default: 10 minutes)

Health checks are registered per-job via each job's `ConfigureHealthChecks` static method. `AddExcaliburJobHost` automatically registers the `JobHeartbeatTracker` singleton.

## Distributed Coordination

For multi-instance deployments, `IJobCoordinator` prevents duplicate execution and distributes work across instances. It composes three focused interfaces:

| Interface | Methods | Purpose |
|-----------|---------|---------|
| `IJobLockProvider` | `TryAcquireLockAsync` | Distributed exclusive locks |
| `IJobRegistry` | `RegisterInstanceAsync`, `UnregisterInstanceAsync`, `GetActiveInstancesAsync` | Instance registration and discovery |
| `IJobDistributor` | `DistributeJobAsync`, `ReportJobCompletionAsync` | Work distribution across instances |

### Setup

```csharp
// Redis-backed coordination
services.AddJobCoordinationRedis("localhost:6379");

// Or with an existing connection
services.AddJobCoordinationRedis(existingConnectionMultiplexer);

// Or with a custom implementation
services.AddJobCoordination<MyCustomCoordinator>();
```

### Distributed Locks

Use `IJobLockProvider` (or `IJobCoordinator`) to acquire exclusive locks that prevent concurrent execution of the same job across instances:

```csharp
using Excalibur.Jobs.Coordination;

public class ExclusiveImportJob : IBackgroundJob
{
    private readonly IJobLockProvider _locks;

    public ExclusiveImportJob(IJobLockProvider locks)
    {
        _locks = locks;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var jobLock = await _locks.TryAcquireLockAsync(
            "exclusive-import",
            TimeSpan.FromMinutes(10),
            cancellationToken);

        if (jobLock is null)
        {
            return; // Another instance holds the lock
        }

        // Lock acquired — safe to proceed
        await DoExclusiveWork(cancellationToken);

        // Extend if the work takes longer than expected
        await jobLock.ExtendAsync(TimeSpan.FromMinutes(5), cancellationToken);

        // Lock released automatically via IAsyncDisposable
    }
}
```

The `IDistributedJobLock` provides:

| Member | Purpose |
|--------|---------|
| `JobKey` | The job identifier this lock covers |
| `InstanceId` | The instance that holds the lock |
| `AcquiredAt` | When the lock was acquired |
| `ExpiresAt` | When the lock will expire |
| `IsValid` | Whether the lock is still active |
| `ExtendAsync(duration, ct)` | Extend the lock's TTL |
| `ReleaseAsync(ct)` | Explicitly release the lock |
| `DisposeAsync()` | Auto-releases on disposal |

### Instance Registration

Use `IJobRegistry` to register worker instances so the coordinator knows who's available:

```csharp
using Excalibur.Jobs.Coordination;

public class WorkerService : BackgroundService
{
    private readonly IJobRegistry _registry;

    public WorkerService(IJobRegistry registry)
    {
        _registry = registry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instanceId = Environment.MachineName;

        await _registry.RegisterInstanceAsync(
            instanceId,
            new JobInstanceInfo(instanceId, Environment.MachineName,
                new JobInstanceCapabilities(
                    maxConcurrentJobs: 4,
                    supportedJobTypes: ["reports", "notifications"])),
            stoppingToken);

        try
        {
            // Do work...
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await _registry.UnregisterInstanceAsync(instanceId, CancellationToken.None);
        }
    }
}
```

### Work Distribution

Use `IJobDistributor` to distribute jobs across registered instances:

```csharp
// Distribute a job to an available instance
var assignedInstance = await _distributor.DistributeJobAsync(
    "daily-report",
    new { ReportDate = DateTime.Today },
    cancellationToken);

// Report completion when done
await _distributor.ReportJobCompletionAsync(
    "daily-report",
    instanceId,
    success: true,
    result: new { RowsProcessed = 1500 },
    cancellationToken);
```

:::tip Use ILeaderElection for leader election
`IJobCoordinator` handles job locking, instance registration, and work distribution. For leader election (electing a single coordinator instance), use `ILeaderElection` from the [Leader Election](../leader-election/index.md) package instead.
:::

## Workflows

:::caution Preview
`WorkflowContext` is currently a **preview implementation** using in-memory state only. It does not provide durable scheduling, real step dispatch, or persistent checkpoints. A production workflow orchestration implementation is planned.
:::

Workflows chain multiple steps with typed input and output:

```csharp
using Excalibur.Jobs.Workflows;

public class OrderProcessingWorkflow : IWorkflow<OrderInput, OrderOutput>
{
    public async Task<WorkflowResult<OrderOutput>> ExecuteAsync(
        OrderInput input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // Step 1: Validate order
        var validated = await ValidateOrder(input, cancellationToken);

        // Step 2: Process payment
        var payment = await ProcessPayment(validated, cancellationToken);

        // Step 3: Fulfill order
        var fulfillment = await FulfillOrder(payment, cancellationToken);

        return WorkflowResult<OrderOutput>.Success(new OrderOutput
        {
            OrderId = input.OrderId,
            TrackingNumber = fulfillment.TrackingNumber
        });
    }
}

public record OrderInput(Guid OrderId, decimal Amount);
public record OrderOutput
{
    public Guid OrderId { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
}
```

### Register Workflows

```csharp
services.AddWorkflows();  // Register core workflow services
services.AddWorkflow<OrderProcessingWorkflow, OrderInput, OrderOutput>();
```

This registers:
- The workflow implementation
- `IWorkflow<OrderInput, OrderOutput>` for DI resolution
- `WorkflowJob<OrderProcessingWorkflow, OrderInput, OrderOutput>` for executing the workflow as a background job

## See Also

- [Patterns Overview](./index.md) - All messaging and integration patterns
- [Outbox Pattern](outbox.md) - Reliable message publishing (used by OutboxJob)
- [CDC](cdc.md) - Change data capture (used by CdcJob)
- [Leader Election](../leader-election/index.md) - Distributed leader coordination
- [Resilience with Polly](../operations/resilience-polly.md) - Retry policies for job resilience
- [Health Checks](../observability/health-checks.md) - Monitoring job health
- [Configuration](../core-concepts/configuration.md) - Dispatch configuration options
