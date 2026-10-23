# OutOfBandProjections

Materialized views that update on their own refresh schedule, with position tracking you own.

## Purpose

A builder-API projection (`AddProjection<T>().Async()`) is applied by the projection host, which
reads the global event stream in order and keeps one subscription checkpoint across every
projection it applies. That is the right trade for a per-aggregate read model.

It is the wrong trade for a view that spans aggregates, costs real work to compute, or needs its
own resumable position independent of every other projection. This sample shows the other option:
events are processed into views by a dedicated refresh service or by an explicit call, and each
view tracks where it stopped under its own `ViewName`.

## What This Sample Demonstrates

- **`IMaterializedViewBuilder<T>`** — how events map into a view
- **`IMaterializedViewProcessor`** — runs events through the registered builders
- **`MaterializedViewRefreshService`** — the background service that performs periodic catch-up
- **`IMaterializedViewStore`** — persistence for the view *and* its position, so catch-up knows
  where it stopped
- **Manual `ProcessEventAsync()`** — driving a view forward without the background service
- **`CatchUpAsync()` and `RebuildAsync()`** — resuming from a position, and discarding a view to
  rebuild it from the beginning

## When to Use This Over a Builder-API Projection

- The view crosses aggregates — regional sales computed across every order, as here
- The computation is expensive enough to want its own schedule rather than the stream's pace
- You want the view's position tracked separately, under its own `ViewName`, so it can be caught
  up or rebuilt without touching any other projection

## Prerequisites

None beyond the .NET SDK. The view store in this sample is in memory, so no database, container or
cloud account is required.

## Running the Sample

```bash
dotnet run --project samples/09-advanced/persistence-patterns/OutOfBandProjections
```

## Project Structure

| Path | What it holds |
|------|---------------|
| `Program.cs` | Host configuration and the catch-up / rebuild scenarios |
| `Domain/Events.cs` | The events the view is built from |
| `Views/RegionalSalesSummary.cs` | The view itself — the shape a reader queries |
| `Views/RegionalSalesViewBuilder.cs` | The `IMaterializedViewBuilder<T>` that folds events into it |
| `Infrastructure/InMemoryMaterializedViewStore.cs` | View + position persistence for the sample |
