# EventUpcasting

Event schema evolution: reading events that were written under an older shape, without rewriting
stored history.

## Purpose

An event store is append-only, so an event written last year keeps the shape it had last year.
Upcasting is how a current aggregate reads it: each stored version is transformed forward to the
version the code expects, at load time. This sample shows the transformation chain and the path
finding that picks a route through it.

## What This Sample Demonstrates

- **Version transformations** — `V1 -> V2 -> V3` upgraders registered against the upcasting pipeline
- **Direct upgrade paths** — a `V1 -> V3` upgrader that skips `V2`
- **Optimal path finding** — a breadth-first search over the registered upgraders, so the shortest
  route is chosen rather than the longest chain
- **Automatic upcasting on replay** — stored events upgraded while an aggregate is rehydrated, so
  the aggregate only ever sees the current shape
- **Manual pipeline use** — invoking the upcasting pipeline directly, outside aggregate replay

## Prerequisites

None beyond the .NET SDK. The sample runs entirely in memory — no database, container or cloud
account is required.

## Running the Sample

```bash
dotnet run --project samples/09-advanced/advanced/Versioning.Examples/EventUpcasting
```

## Project Structure

| Path | What it holds |
|------|---------------|
| `Program.cs` | Host configuration, upgrader registration, and the scenarios it walks through |
| `Domain/UserProfileAggregate.cs` | The aggregate whose replay consumes the upcast events |
| `Events/UserProfileEvents.cs` | The event versions — the `V1`, `V2` and `V3` shapes |
| `Upgraders/UserEventUpgraders.cs` | The transformations between those versions |
