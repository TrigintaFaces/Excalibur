# GlobalStreamProjectionHost Sample

Demonstrates continuous global event stream tailing using `GlobalStreamProjectionHost<TState>` — a BackgroundService that reads ALL events across all aggregates and applies them to a custom projection.

## What This Shows

- **GlobalStreamProjectionHost<TState>** — Background service that continuously polls the global stream
- **IGlobalStreamProjection<TState>** — Custom projection logic (receives every event in order)
- **GlobalStreamProjectionOptions** — Batch size, poll interval, checkpoint interval
- **Cross-aggregate state** — Builds metrics across multiple aggregate streams

## When to Use

| Scenario | Recommended Approach |
|----------|---------------------|
| Per-aggregate read models | `AddProjection<T>().Inline()` or `.Async()` |
| Cross-aggregate metrics/dashboards | **GlobalStreamProjectionHost** (this sample) |
| CDC-style continuous processing | **GlobalStreamProjectionHost** |
| Global statistics (event counts, revenue) | **GlobalStreamProjectionHost** |
| Scheduled full rebuild | [ProjectionRebuildJob](../ProjectionRebuildJob/README.md) |

## Running

```bash
dotnet run
```

The sample:
1. Starts the `GlobalStreamProjectionHost<SystemMetricsState>` (polls every 2 seconds)
2. Generates 1 order + 1 payment event every 3 seconds
3. The host picks up events and logs running totals

Press `Ctrl+C` to stop. The host persists its checkpoint position for restart.

## Key Code

```csharp
// Register the projection
services.AddSingleton<IGlobalStreamProjection<SystemMetricsState>, SystemMetricsProjection>();

// Configure — each host MUST have a unique ProjectionName
services.Configure<GlobalStreamProjectionOptions>(opts =>
{
    opts.ProjectionName = "SystemMetrics";
    opts.BatchSize = 100;
    opts.IdlePollingInterval = TimeSpan.FromSeconds(2);
    opts.CheckpointInterval = 50;
});

// Register as BackgroundService
services.AddHostedService<GlobalStreamProjectionHost<SystemMetricsState>>();
```

## Architecture

```
┌─────────────────────┐     ┌────────────────────────────────────┐
│   Event Store       │────▶│  GlobalStreamProjectionHost<TState> │
│   (Global Stream)   │     │                                    │
└─────────────────────┘     │  1. Poll (BatchSize events)        │
                            │  2. Deserialize each event         │
                            │  3. Call projection.ApplyAsync()   │
                            │  4. Checkpoint every N events      │
                            │  5. Idle wait if no events         │
                            └────────────────────────────────────┘
```

## See Also

- [GlobalStreamProjectionHost docs](../../../../docs-site/docs/event-sourcing/global-stream-projection-host.md)
- [ProjectionRebuildJob](../ProjectionRebuildJob/README.md) — Scheduled full rebuild (not continuous)
- [OutOfBandProjections](../OutOfBandProjections/README.md) — Materialized views with IMaterializedViewProcessor
