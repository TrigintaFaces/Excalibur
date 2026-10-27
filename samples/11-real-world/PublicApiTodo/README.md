# PublicApiTodo

A small todo application that exercises the whole consumer path — dispatching, domain modelling,
event sourcing, projections and queries — using **only public APIs**.

## Purpose

Most samples demonstrate one capability. This one exists to answer a different question: *can a
consumer build a working application from the published surface alone?*

Its constraint is the point. The project takes no `InternalsVisibleTo` and references no internal
type, so it can only compile if every API it needs is genuinely public. If it builds and runs, the
public surface it touches is sufficient for the scenario it covers.

## What This Sample Demonstrates

- **Message dispatching** — `IDispatcher` and `IActionHandler`
- **Domain modelling** — `AggregateRoot<TKey>`, `RaiseEvent`, event application
- **Event sourcing** — `IEventSourcedRepository`, `IEventStore`, snapshots
- **Projections** — `IProjectionStore<T>` and a read model built from the event stream
- **Query handling** — queries dispatched through handlers rather than read off the aggregate

## Prerequisites

None beyond the .NET SDK. The sample uses the in-memory event store specifically so it runs with no
database, container or cloud account.

## Running the Sample

```bash
dotnet run --project samples/11-real-world/PublicApiTodo
```

## Project Structure

| Path | What it holds |
|------|---------------|
| `Program.cs` | Host configuration and the scenario it walks through |
| `Domain/TodoAggregate.cs` | The aggregate — state changes raised as events |
| `Domain/Events/TodoEvents.cs` | The events it raises |
| `Messages/TodoCommands.cs` | The commands and queries a caller dispatches |
| `Handlers/TodoCommandHandlers.cs` | Command handlers — load, mutate, save |
| `Handlers/TodoQueryHandlers.cs` | Query handlers — read from the projection, not the aggregate |
| `Projections/TodoProjection.cs` | The read model built from the event stream |
| `Projections/InMemoryProjectionStore.cs` | Projection persistence for the sample |

## A Note on Scope

Passing here means the public surface supports *this* scenario. It is not a statement about
surfaces this application never touches — a transport, a real event store provider, or the
compliance packages are all outside what it validates.
