# OutOfBandProjections

Materialized views that update asynchronously, instead of inline during `SaveAsync()`.

## Purpose

An inline projection updates while the aggregate is being saved, so the save pays for the
projection and the view is consistent the instant the save returns. That is the right trade for a
view that belongs to one aggregate and must be read back immediately.

It is the wrong trade for a view that spans aggregates or costs real work to compute. This sample
shows the other option: events are processed into views out of band, by a background service or by
an explicit call, and the view is eventually consistent.

## What This Sample Demonstrates

- **`IMaterializedViewBuilder<T>`** — how events map into a view
- **`IMaterializedViewProcessor`** — runs events through the registered builders
- **`MaterializedViewRefreshService`** — the background service that performs periodic catch-up
- **`IMaterializedViewStore`** — persistence for the view *and* its position, so catch-up knows
  where it stopped
- **Manual `ProcessEventAsync()`** — driving a view forward without the background service
- **`CatchUpAsync()` and `RebuildAsync()`** — resuming from a position, and discarding a view to
  rebuild it from the beginning

## When to Use This Over an Inline Projection

- The view crosses aggregates — regional sales computed across every order, as here
- The computation is expensive enough that it should not sit inside `SaveAsync()`
- The reader tolerates eventual consistency — dashboards and reports usually do

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
