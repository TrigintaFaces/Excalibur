# ProjectionRebuildJob Sample

Demonstrates scheduling a full projection rebuild using `ProjectionRebuildJob` from `Excalibur.Jobs` with Quartz cron scheduling.

## What This Shows

- **ProjectionRebuildJob** — A built-in `IBackgroundJob` that calls `IMaterializedViewProcessor.RebuildAsync()`
- **Quartz scheduling** — `IJobConfigurator.AddJob<T>(cronExpression)` for periodic execution
- **IMaterializedViewBuilder<T>** — Defines how domain events map to a materialized view
- **Full rebuild pattern** — Replays all events to regenerate projection state from scratch

## When to Use

| Scenario | Use ProjectionRebuildJob? |
|----------|--------------------------|
| Projection schema changed | Yes — rebuild regenerates with new mapping |
| Projection got corrupted | Yes — full rebuild restores correct state |
| Nightly data integrity check | Yes — schedule off-peak |
| Real-time view updates | No — use inline projections or AsyncProjectionProcessingHost |

## Running

```bash
dotnet run
```

Watch the console for:
1. `Seeding 5 order events...` — initial data
2. `[ProjectionRebuildJob] Starting...` — job fires on schedule
3. `[ProjectionRebuildJob] Completed` — rebuild complete

## Key Code

```csharp
builder.Services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .UseInMemoryEventStore()
        .AddMaterializedView<SalesDashboardView, SalesDashboardViewBuilder>())
    .AddJobs(configurator =>
    {
        // Daily at 3 AM (production)
        configurator.AddJob<ProjectionRebuildJob>("0 0 3 * * ?");
    }));
```

## See Also

- [Materialized Views](../../../../docs-site/docs/event-sourcing/materialized-views.md)
- [GlobalStreamProjectionHost](../GlobalStreamProjectionHost/README.md) — Continuous stream tailing (not scheduled)
