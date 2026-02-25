---
sidebar_position: 9
title: Cron Timer Transport
description: Configure scheduled message triggering with cron expressions for background jobs and recurring tasks.
---

# Cron Timer Transport

The Cron Timer transport enables scheduled message dispatching using cron expressions. Unlike queue-based transports that receive messages from external systems, this transport **generates** messages on a schedule, making it ideal for background jobs, recurring tasks, and scheduled workflows.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Transport.CronTimer
  ```
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

:::tip ASP.NET Core Eventing Framework
This transport fulfills the `AddTimerEventQueue()` capability from the [ASP.NET Core Eventing Framework proposal](https://github.com/dotnet/aspnetcore/issues/53219). See [From ASP.NET Core Eventing Proposal](../migration/from-aspnet-eventing-proposal.md) for a complete comparison of how Dispatch implements all the proposed features.
:::

---

## Quick Start

### 1. Configure the Transport

```csharp
// Register ICronScheduler (required)
builder.Services.AddSingleton<ICronScheduler, CronScheduler>();

// Simple cron timer with default name
builder.Services.AddCronTimerTransport("*/5 * * * *");

// Named cron timer with options
builder.Services.AddCronTimerTransport("daily-report", "0 2 * * *", options =>
{
    options.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    options.RunOnStartup = false;
});

// Typed cron timer (recommended for multiple timers)
builder.Services.AddCronTimerTransport<CleanupTimer>("*/5 * * * *");
builder.Services.AddCronTimerTransport<HourlySyncTimer>("0 * * * *", options =>
{
    options.PreventOverlap = true;
});
```

### 2. Define Timer Markers (for typed timers)

```csharp
// Define empty structs that implement ICronTimerMarker
public struct CleanupTimer : ICronTimerMarker { }
public struct HourlySyncTimer : ICronTimerMarker { }
public struct DailyReportTimer : ICronTimerMarker { }
```

### 3. Create a Typed Handler (Recommended)

```csharp
// Handler receives ONLY CleanupTimer events - no filtering needed!
public class CleanupHandler : IActionHandler<CronTimerTriggerMessage<CleanupTimer>>
{
    private readonly ICleanupService _cleanupService;
    private readonly ILogger<CleanupHandler> _logger;

    public CleanupHandler(ICleanupService cleanupService, ILogger<CleanupHandler> logger)
    {
        _cleanupService = cleanupService;
        _logger = logger;
    }

    public async Task HandleAsync(
        CronTimerTriggerMessage<CleanupTimer> action,
        CancellationToken cancellationToken)
    {
        // No need to check TimerName - this handler only receives CleanupTimer events
        _logger.LogInformation(
            "Running cleanup job triggered at {Time} (scheduled: {Cron})",
            action.TriggerTimeUtc,
            action.CronExpression);

        await _cleanupService.CleanupExpiredDataAsync(cancellationToken);
    }
}
```

### 4. Register the Handler

```csharp
// Auto-discovery (recommended)
builder.Services.AddDispatch(typeof(Program).Assembly);

// Or explicit registration for typed handlers
builder.Services.AddTransient<IActionHandler<CronTimerTriggerMessage<CleanupTimer>>, CleanupHandler>();
```

---

## Configuration Options

```csharp
builder.Services.AddCronTimerTransport("my-timer", "0 * * * *", options =>
{
    // Time zone for cron evaluation (default: UTC)
    options.TimeZone = TimeZoneInfo.Local;

    // Fire immediately when application starts (default: false)
    options.RunOnStartup = true;

    // Skip scheduled run if previous execution still active (default: true)
    options.PreventOverlap = true;
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `TimeZone` | `TimeZoneInfo.Utc` | Time zone for evaluating the cron expression |
| `RunOnStartup` | `false` | Trigger immediately when the transport starts |
| `PreventOverlap` | `true` | Skip scheduled triggers if a previous execution is still running |

---

## Cron Expression Reference

The cron timer supports standard 5-field and 6-field (with seconds) cron expressions:

```
┌───────────── minute (0-59)
│ ┌───────────── hour (0-23)
│ │ ┌───────────── day of month (1-31)
│ │ │ ┌───────────── month (1-12)
│ │ │ │ ┌───────────── day of week (0-6, Sunday=0)
│ │ │ │ │
* * * * *
```

### Common Patterns

| Expression | Description |
|------------|-------------|
| `*/5 * * * *` | Every 5 minutes |
| `0 * * * *` | Every hour at minute 0 |
| `0 0 * * *` | Daily at midnight |
| `0 9 * * 1-5` | Weekdays at 9 AM |
| `0 0 1 * *` | First day of every month at midnight |
| `0 0 * * 0` | Every Sunday at midnight |
| `0 */2 * * *` | Every 2 hours |
| `30 4 1,15 * *` | 4:30 AM on the 1st and 15th of each month |

---

## CronTimerTriggerMessage Properties

When a cron timer fires, it dispatches a `CronTimerTriggerMessage` (or `CronTimerTriggerMessage<TTimer>` for typed timers):

```csharp
public record CronTimerTriggerMessage : IDispatchEvent
{
    // Name of the timer transport that fired (e.g., "cleanup", "daily-report")
    public string TimerName { get; init; }

    // The cron expression that triggered this message
    public string CronExpression { get; init; }

    // UTC timestamp when the timer actually fired
    public DateTimeOffset TriggerTimeUtc { get; init; }

    // Time zone ID used for the cron schedule
    public string TimeZone { get; init; }
}

// Generic variant for typed timers
public record CronTimerTriggerMessage<TTimer> : CronTimerTriggerMessage
    where TTimer : ICronTimerMarker
{
    // The marker type for this timer (useful for logging/diagnostics)
    public Type TimerType => typeof(TTimer);
}
```

### Handling Multiple Timers

**Recommended: Use Typed Timers (No Filtering Required)**

With typed timers, each handler automatically receives only its specific timer events:

```csharp
// Timer markers
public struct CleanupTimer : ICronTimerMarker { }
public struct DailyReportTimer : ICronTimerMarker { }
public struct HourlySyncTimer : ICronTimerMarker { }

// Registration
builder.Services.AddCronTimerTransport<CleanupTimer>("*/5 * * * *");
builder.Services.AddCronTimerTransport<DailyReportTimer>("0 2 * * *");
builder.Services.AddCronTimerTransport<HourlySyncTimer>("0 * * * *");

// Each handler receives ONLY its timer's events
public class CleanupHandler : IActionHandler<CronTimerTriggerMessage<CleanupTimer>>
{
    public Task HandleAsync(CronTimerTriggerMessage<CleanupTimer> action, CancellationToken cancellationToken)
    {
        // No filtering needed - this handler only receives CleanupTimer events
        return DoCleanupAsync(cancellationToken);
    }
}

public class DailyReportHandler : IActionHandler<CronTimerTriggerMessage<DailyReportTimer>>
{
    public Task HandleAsync(CronTimerTriggerMessage<DailyReportTimer> action, CancellationToken cancellationToken)
    {
        return GenerateDailyReportAsync(cancellationToken);
    }
}
```

**Alternative: Non-Typed Timers with Manual Filtering**

If you prefer a single handler for multiple timers, use the non-generic message type:

```csharp
// Registration with string names
builder.Services.AddCronTimerTransport("cleanup", "*/5 * * * *");
builder.Services.AddCronTimerTransport("daily-report", "0 2 * * *");

// Single handler with switch
public class UnifiedCronHandler : IActionHandler<CronTimerTriggerMessage>
{
    public async Task HandleAsync(CronTimerTriggerMessage action, CancellationToken cancellationToken)
    {
        switch (action.TimerName)
        {
            case "cleanup":
                await HandleCleanupAsync(cancellationToken);
                break;
            case "daily-report":
                await HandleDailyReportAsync(cancellationToken);
                break;
            default:
                // Unknown timer - log and ignore
                break;
        }
    }
}
```

---

## Health Checks

The Cron Timer transport implements `ITransportHealthChecker` for ASP.NET Core health check integration:

```csharp
builder.Services.AddHealthChecks()
    .AddTransportHealthChecks();

app.MapHealthChecks("/health");
```

Health check response includes:

```json
{
  "status": "Healthy",
  "description": "Cron timer transport is healthy, next trigger: 2026-01-18T20:00:00+00:00",
  "data": {
    "CronExpression": "0 * * * *",
    "TimeZone": "UTC",
    "TotalTriggers": 42,
    "SuccessfulTriggers": 41,
    "FailedTriggers": 1,
    "SkippedOverlapTriggers": 3,
    "LastTriggerTime": "2026-01-18T19:00:00+00:00",
    "NextScheduledTrigger": "2026-01-18T20:00:00+00:00"
  }
}
```

---

## Metrics

The transport emits OpenTelemetry metrics via the shared `TransportMeter`:

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.transport.messages_received_total` | Counter | Total trigger messages dispatched |
| `dispatch.transport.errors_total` | Counter | Failed executions |
| `dispatch.transport.receive_duration_ms` | Histogram | Handler execution time |
| `dispatch.transport.starts_total` | Counter | Transport start events |
| `dispatch.transport.stops_total` | Counter | Transport stop events |
| `dispatch.transport.connection_status` | Gauge | Running status (0=stopped, 1=running) |

Additional cron-specific statistics (total triggers, successful, failed, skipped overlap) are exposed through health check data rather than separate metrics.

---

## Testing

Handler classes are fully testable:

```csharp
[Fact]
public async Task CleanupHandler_ShouldCallCleanupService()
{
    // Arrange
    var cleanupService = A.Fake<ICleanupService>();
    var logger = A.Fake<ILogger<CleanupHandler>>();
    var handler = new CleanupHandler(cleanupService, logger);

    // For typed timers, use the generic message type
    var message = new CronTimerTriggerMessage<CleanupTimer>
    {
        TimerName = "CleanupTimer",
        CronExpression = "*/5 * * * *",
        TriggerTimeUtc = DateTimeOffset.UtcNow,
        TimeZone = "UTC"
    };

    // Act
    await handler.HandleAsync(message, CancellationToken.None);

    // Assert
    A.CallTo(() => cleanupService.CleanupExpiredDataAsync(A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
}
```

---

## Cloud-Specific Schedulers

For cloud-native deployments, consider using cloud scheduler integration alongside the cron timer:

### AWS EventBridge Scheduler

```csharp
services.AddAwsEventBridgeScheduler(options =>
{
    options.Region = "us-east-1";
    options.ScheduleGroupName = "dispatch-schedules";
    options.TargetArn = "arn:aws:lambda:us-east-1:123456789:function:dispatch-handler";
    options.RoleArn = "arn:aws:iam::123456789:role/dispatch-scheduler-role";
    options.ScheduleTimeZone = "UTC";
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `Region` | `"us-east-1"` | AWS region for the scheduler |
| `ScheduleGroupName` | `"default"` | EventBridge schedule group name |
| `TargetArn` | Required | ARN of the target (Lambda, SQS, etc.) |
| `RoleArn` | Optional | IAM role ARN for EventBridge to assume |
| `ScheduleTimeZone` | `"UTC"` | Time zone for schedule expressions |
| `MaxRetries` | `3` | Maximum retry attempts |
| `DeadLetterQueueArn` | Optional | Dead letter queue ARN |

---

## See Also

- [From ASP.NET Core Eventing Proposal](../migration/from-aspnet-eventing-proposal.md) — Full comparison with the proposed framework
- [Multi-Transport Routing](multi-transport.md) — Route messages to different transports
- [Health Checks](../observability/health-checks.md) — Monitor transport health
- [Middleware](../middleware/index.md) — Add cross-cutting concerns to handlers
