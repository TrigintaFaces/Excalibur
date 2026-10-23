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
| Continuous view updates | No — use `AddProjection<T>().Async()` with `EnableProjectionProcessing()` |

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
        .UseInMemory())
    .AddJobs(configurator =>
    {
        // This sample rebuilds every 30 seconds so you can watch it run.
        // In production a daily schedule such as "0 0 3 * * ?" is the usual choice.
        configurator.AddJob<ProjectionRebuildJob>("0/30 * * * * ?");
    }));

// Materialized views register on IServiceCollection, not on the event-sourcing builder.
builder.Services.AddMaterializedViews(views => views
    .AddBuilder<SalesDashboardView, SalesDashboardViewBuilder>());
```

## See Also

- [Materialized Views](../../../../docs-site/docs/event-sourcing/materialized-views.md)
- [GlobalStreamProjectionHost](../GlobalStreamProjectionHost/README.md) — Continuous stream tailing (not scheduled)
