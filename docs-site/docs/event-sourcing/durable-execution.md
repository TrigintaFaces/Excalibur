---
sidebar_position: 17
title: Durable Execution
description: Run durable workflows over your existing event store with a deterministic replay executor and at-least-once activities.
---

# Durable Execution

`Excalibur.Workflows` is an **embedded durable-execution engine**: a deterministic replay executor that runs durable workflows over your existing [event store](./event-store.md) — no separate workflow server or sidecar. Every non-deterministic decision is recorded to an append-only workflow journal, and instance state is reconstructed by replaying that journal, so a crash mid-execution resumes from the last completed step and journaled activities are never re-applied. The contracts live in `Excalibur.Workflows.Abstractions`.

:::info The determinism surface
`IWorkflowContext` is the determinism boundary — every non-deterministic decision a workflow body makes goes through it, is journaled, and replays deterministically. The context exposes:

- **Activities** — `CallActivityAsync<TResult>(...)` schedules a registered at-least-once activity and journals its result.
- **Deterministic time** — `UtcNowAsync(...)` returns a journaled `DateTimeOffset` (never read the wall clock directly).
- **Deterministic identifiers** — `NewGuidAsync(...)` returns a journaled `Guid` (never call `Guid.NewGuid()` directly).
- **Durable timers** — `CreateTimerAsync(delay, ...)` fires the timer exactly once across replays (use instead of `Task.Delay`).
- **External signals** — `WaitForSignalAsync<TResult>(signalName, ...)` suspends until a signal is delivered via `IWorkflowExecutor.SignalAsync`.

On the executor: `StartAsync`, `SignalAsync` (exactly-once external-signal delivery), `GetStatusAsync`, and `GetStateAsync` are all live.
:::

## Before You Start

- **.NET 10.0**
- A registered [event store](./event-store.md) and event serializer (via `AddEventSourcing(...)`) — the engine uses these as the workflow journal
- Familiarity with [event sourcing](./index.md)

## Installation

```bash
dotnet add package Excalibur.Workflows
```

**Dependencies:** `Excalibur.Workflows.Abstractions`, `Excalibur.EventSourcing.Abstractions`, `Excalibur.Dispatch.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`

At runtime the engine also requires an `IEventStore` (the journal) and an `IEventSerializer` to be registered by your event-sourcing configuration.

## Registration

`AddWorkflows()` registers the engine (the `IWorkflowExecutor`, the workflow and activity registries, and validated `WorkflowOptions`). `AddWorkflow(name, body)` registers a workflow body under a name, and `AddActivity<TActivity, TInput, TOutput>(name)` registers an at-least-once activity:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Excalibur.Workflows;

services
    .AddEventSourcing(/* your event store + serializer */)
    .AddWorkflows();

// A workflow body: ordinary async logic whose only source of non-determinism is the context.
services.AddWorkflow("order-fulfilment", async (context, input, cancellationToken) =>
{
    var reservation = await context.CallActivityAsync<ReservationResult>(
        "reserve-inventory", input, cancellationToken);

    await context.CallActivityAsync<object?>(
        "charge-payment", reservation, cancellationToken);

    return reservation;
});

// An activity carries the side effects a workflow body must not perform directly.
services.AddActivity<ReserveInventoryActivity, OrderInput, ReservationResult>("reserve-inventory");
```

A workflow body is a `WorkflowBody` delegate — `ValueTask<object?> (IWorkflowContext context, object input, CancellationToken cancellationToken)` — returning the workflow result, or `null` when it produces none.

### Options

`WorkflowOptions` is validated at startup (`ValidateOnStart`):

| Property | Purpose | Default |
|----------|---------|---------|
| `MaxReplayEvents` | Maximum journal events a single instance may accumulate before replay is rejected, guarding against unbounded histories. | `10,000` |

## The workflow body and `IWorkflowContext`

Inside a workflow body, the `IWorkflowContext` is the **determinism boundary** — the only legal surface for non-deterministic work. It records each call to the journal and, on replay, returns the previously recorded result instead of re-performing the operation.

The context exposes these operations, each journaled so replay returns the recorded result instead of re-performing the work:

| Member | What it does |
|--------|--------------|
| `ValueTask<TResult> CallActivityAsync<TResult>(string activityName, object input, CancellationToken cancellationToken)` | Schedules a registered activity for at-least-once execution and awaits its result. Scheduling and completion are journaled, so on replay the recorded result is returned **without** re-invoking the activity. |
| `ValueTask<DateTimeOffset> UtcNowAsync(CancellationToken cancellationToken)` | Returns the current time as a journaled `DateTimeOffset`. Use this instead of `DateTimeOffset.UtcNow` so the value is stable across replays. |
| `ValueTask<Guid> NewGuidAsync(CancellationToken cancellationToken)` | Returns a journaled `Guid`. Use this instead of `Guid.NewGuid()` so the identifier is stable across replays. |
| `ValueTask CreateTimerAsync(TimeSpan delay, CancellationToken cancellationToken)` | Schedules a durable timer that fires exactly once, surviving crashes and replay. Use this instead of `Task.Delay` or a wall-clock deadline. |
| `ValueTask<TResult> WaitForSignalAsync<TResult>(string signalName, CancellationToken cancellationToken)` | Suspends the workflow until an external signal of `signalName` is delivered (via `IWorkflowExecutor.SignalAsync`), then returns its payload. |

A workflow body that performs non-deterministic work directly — reading the clock with `DateTimeOffset.UtcNow`, generating a GUID with `Guid.NewGuid()`, sleeping with `Task.Delay`, or calling a remote service — instead of through the context (or a journaled activity) will diverge on replay. Use the context member for time, identifiers, timers, and signals; route external calls **inside an activity**, whose result is journaled.

:::tip Non-determinism analyzer
The opt-in `Excalibur.Workflows.Analyzers` package ships a Roslyn analyzer that flags non-deterministic calls (`DateTimeOffset.UtcNow`, `Guid.NewGuid()`, `Task.Delay`, and similar) inside a workflow body; the companion `Excalibur.Workflows.CodeFixes` package supplies a code-fix that redirects each to the corresponding `IWorkflowContext` member — so divergence is caught at build time rather than on replay.

```bash
dotnet add package Excalibur.Workflows.Analyzers
dotnet add package Excalibur.Workflows.CodeFixes
```
:::

### Activities

An `IActivity<TInput, TOutput>` is an at-least-once unit of side-effecting work invoked through `CallActivityAsync`:

```csharp
using Excalibur.Workflows;

public sealed class ReserveInventoryActivity : IActivity<OrderInput, ReservationResult>
{
    public async ValueTask<ReservationResult> ExecuteAsync(
        OrderInput input, CancellationToken cancellationToken)
    {
        // Side effects live here. Must be idempotent.
        return await _inventory.ReserveAsync(input, cancellationToken);
    }
}
```

Because a crash between an activity's execution and the journaling of its completion causes the activity to run again on replay, **every activity MUST be idempotent** — executing it twice with the same input must not double-apply its effect.

## Replay, journaling, and exactly-once semantics

The engine records each step to an append-only journal and reconstructs instance state by replaying it. Concretely, what the code guarantees today:

- **Crash-safe resume.** `StartAsync` loads the instance journal and replays it: a fresh start records the lifecycle open; a resume (already-started, crashed mid-execution) replays the existing journal and resumes at the first un-journaled step.
- **Journal-native deduplication.** Each activity step has a deterministic idempotency key of `instanceId:stepOrdinal`. A journaled `ActivityCompleted` at that step short-circuits re-execution and returns the recorded result — so a crash *after* an activity completed but *before* the next step never re-applies that activity.
- **At-least-once activities, exactly-once effects via idempotency.** Activities themselves are at-least-once (they may re-run on replay before their completion was journaled); the exactly-once guarantee for their *effect* is the caller's responsibility, delivered by writing idempotent activities.
- **Single-writer journal via optimistic concurrency.** Every journal append is made with an expected version; a concurrent writer racing the same instance surfaces as `WorkflowConcurrencyException`, so two racing executions cannot both extend the same instance's history.
- **Idempotent completion.** Re-invoking `StartAsync` for an instance whose journal already contains a `WorkflowCompleted` event is a no-op.
- **Bounded histories.** An instance journal larger than `WorkflowOptions.MaxReplayEvents` is rejected at load time rather than replayed unbounded.

An activity that throws is journaled as failed and surfaces to the workflow body as `WorkflowActivityException` (carrying the activity name and step ordinal). `IWorkflowExecutor.GetStatusAsync` reports a `WorkflowStatus` of `Running`, `Completed`, or `Faulted` for an instance; `GetStateAsync` returns the fuller `WorkflowState?` snapshot.

## External signals

A workflow body can suspend on `context.WaitForSignalAsync<TPayload>("approval", cancellationToken)` and resume when a producer delivers that signal:

```csharp
ValueTask SignalAsync(
    string instanceId,
    string signalName,
    string signalId,     // stable, producer-supplied — used to deduplicate redelivery
    object payload,
    CancellationToken cancellationToken);
```

Delivery is **exactly-once into the journal**: the signal is admitted to a dedup-keyed inbox on `signalId`, so re-delivering the same `signalId` is a no-op. The producer MUST supply a **stable** `signalId` — a freshly generated one each call would defeat deduplication. The running instance drains the inbox into its journal at a deterministic replay boundary; the signal never writes the instance journal directly, preserving single-writer replay safety.

:::warning Sensitive data in failure records
A failed activity's exception message is persisted to the journal. If your activities can throw exceptions whose messages carry PII or secrets, sanitize or redact those messages before they propagate out of the activity — the journal is durable storage and is read back on replay.
:::

## What's Next

- [Event Store](./event-store.md) — The journal the engine runs over
- [Event Sourcing Overview](./index.md) — Aggregates, repositories, and stores
- [Outbox Pattern](../patterns/outbox.md) — Reliable messaging for activity side effects
