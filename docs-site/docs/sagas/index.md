---
sidebar_position: 9
title: Sagas & Workflows
description: Orchestrate multi-step business processes with event-driven coordination, timeout handling, and compensation using Excalibur.Saga.
---

# Sagas & Workflows

Sagas coordinate multi-step business processes where failure in one step requires compensating (rolling back) previously completed steps. Excalibur provides an **event-driven saga model** built on `SagaBase<T>` and `ISagaCoordinator`.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Saga
  dotnet add package Excalibur.Saga.SqlServer  # for persistence
  ```
- Familiarity with [handlers](../handlers.md) and [dependency injection](../core-concepts/dependency-injection.md)

## How Sagas Work

Each saga is a class that extends `SagaBase<TState>`. The framework routes incoming events to saga instances via `ISagaCoordinator`. The saga processes one event at a time, updates its state, and suspends until the next event arrives. State is persisted between events so the saga survives process restarts.

```
Event arrives → SagaHandlingMiddleware → SagaCoordinator → SagaBase<T>.HandleAsync()
                                                         → ISagaStore.SaveAsync()
```

This event-driven model works across independently deployed microservices, long-running workflows, and modular monoliths alike.

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Saga` | Core saga engine, coordinator, timeout infrastructure |
| `Excalibur.Saga.SqlServer` | SQL Server saga state persistence |
| `Excalibur.Saga.Postgres` | PostgreSQL saga state persistence |
| `Excalibur.Saga.CosmosDb` | Azure Cosmos DB saga state persistence |
| `Excalibur.Saga.DynamoDb` | AWS DynamoDB saga state persistence |
| `Excalibur.Saga.MongoDB` | MongoDB saga state persistence |
| `Excalibur.Saga.Firestore` | Google Firestore saga state persistence |

## Quick Start

### 1. Define Saga State

```csharp
using Excalibur.Saga.Orchestration;

public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    // Populated as steps complete
    public string? ReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? ShipmentTrackingNumber { get; set; }
    public string? FailureReason { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
}
```

### 2. Define Saga Events

Events must implement `ISagaEvent` so the coordinator can route them:

```csharp
using Excalibur.Dispatch;

// Start event — creates a new saga instance
public record StartOrderProcessing(
    string SagaId, string OrderId, string CustomerId, decimal TotalAmount) : ISagaEvent;

// Step completion events
public record InventoryReserved(string SagaId, string ReservationId) : ISagaEvent;
public record PaymentProcessed(string SagaId, string TransactionId) : ISagaEvent;
public record OrderShipped(string SagaId, string TrackingNumber) : ISagaEvent;

// Failure events
public record PaymentFailed(string SagaId, string Reason) : ISagaEvent;
```

### 3. Implement the Saga

```csharp
using Excalibur.Dispatch;
using Excalibur.Saga;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.Logging;

public sealed partial class OrderFulfillmentSaga(
    OrderSagaState initialState,
    IDispatcher dispatcher,
    ILogger<OrderFulfillmentSaga> logger)
    : SagaBase<OrderSagaState>(initialState, dispatcher, logger)
{
    public override bool HandlesEvent(object eventMessage)
    {
        return eventMessage is StartOrderProcessing
            or InventoryReserved
            or PaymentProcessed
            or OrderShipped
            or PaymentFailed;
    }

    public override async Task HandleAsync(
        object eventMessage, CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrderProcessing start:
                State.OrderId = start.OrderId;
                State.CustomerId = start.CustomerId;
                State.TotalAmount = start.TotalAmount;
                LogSagaStarted(State.SagaId, start.OrderId);
                break;

            case InventoryReserved reserved:
                State.ReservationId = reserved.ReservationId;
                State.CompletedSteps.Add("ReserveInventory");
                break;

            case PaymentProcessed paid:
                State.PaymentTransactionId = paid.TransactionId;
                State.CompletedSteps.Add("ProcessPayment");
                break;

            case OrderShipped shipped:
                State.ShipmentTrackingNumber = shipped.TrackingNumber;
                State.CompletedSteps.Add("ShipOrder");
                LogSagaCompleted(State.SagaId, State.OrderId);
                await MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
                break;

            case PaymentFailed failed:
                State.FailureReason = failed.Reason;
                MarkCompleted();
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Saga {SagaId} started for order {OrderId}")]
    private partial void LogSagaStarted(Guid sagaId, string orderId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Saga {SagaId} completed for order {OrderId}")]
    private partial void LogSagaCompleted(Guid sagaId, string orderId);
}
```

### 4. Register and Configure

```csharp
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;

// Register saga coordination + timeout delivery
services.AddExcaliburOrchestration();
services.AddSagaTimeoutDelivery();

// Register your saga type
services.AddSaga<OrderFulfillmentSaga, OrderSagaState>();

// Map events to saga instances
SagaRegistry.Register<OrderFulfillmentSaga, OrderSagaState>(info =>
{
    info.StartsWith<StartOrderProcessing>();
    info.Handles<InventoryReserved>();
    info.Handles<PaymentProcessed>();
    info.Handles<OrderShipped>();
    info.Handles<PaymentFailed>();
});
```

Or use the builder pattern with a persistence provider:

```csharp
services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
{
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts();
}));
```

## Declarative Timeouts with ISagaTimeout&lt;T&gt;

Sagas can declare strongly-typed timeout handlers using `ISagaTimeout<T>`. When a timeout fires and the saga implements a matching interface, the framework routes directly to `HandleTimeoutAsync` instead of the general `HandleAsync`:

```csharp
public sealed class PaymentTimeout : ISagaEvent
{
    public string SagaId { get; set; } = string.Empty;
    public string? StepId => "PaymentTimeout";
}

public sealed partial class OrderFulfillmentSaga
    : SagaBase<OrderSagaState>,
      ISagaTimeout<PaymentTimeout>
{
    // Schedule a timeout in HandleAsync
    // State.TimeoutId = await RequestTimeoutAsync<PaymentTimeout>(
    //     TimeSpan.FromMinutes(5), cancellationToken);

    public Task HandleTimeoutAsync(
        PaymentTimeout message, CancellationToken cancellationToken)
    {
        State.FailureReason = "Payment confirmation timed out";
        MarkCompleted();
        return Task.CompletedTask;
    }
}
```

A saga can implement multiple `ISagaTimeout<T>` interfaces for different timeout types. This follows the NServiceBus `IHandleTimeouts<T>` pattern.

## Idempotent Event Replay

`SagaState` automatically tracks processed event IDs to prevent duplicate command dispatch. When a saga event is delivered (including crash replays or concurrent duplicates), the `SagaCoordinator` calls `SagaState.TryMarkEventProcessed(eventId)` before executing the handler:

- Returns `true` — event is new, process it normally
- Returns `false` — event already processed, skip silently

The processed event set is bounded to 1,000 entries (oldest trimmed when exceeded) and persisted with the saga state.

:::info NServiceBus Pattern

This follows the same idempotent replay pattern used by NServiceBus sagas, where saga state includes a list of handled message IDs.
:::

## Optimistic Concurrency

Saga persistence enforces **optimistic concurrency** so two events arriving for the same saga (for example, a business event racing a timeout) cannot silently overwrite each other.

- `SagaState.Version` is the concurrency token — the version the saga was **loaded** at (a brand-new saga is `0`). You perform **no version arithmetic**; the store owns the increment (EF-style).
- On save, the store performs a versioned compare-and-set: it persists the new state only if the stored row is still at the loaded version, bumping `Version` to `loadedVersion + 1`.
- If a concurrent write already advanced the row, the save matches no rows and the store throws `ConcurrencyException` — exactly one of the racing saves wins, and no update is lost.

This behavior is identical whether the saga is driven through `SagaManager` or `SagaCoordinator`. Handle `ConcurrencyException` by reloading the saga and replaying the event (the [idempotent replay](#idempotent-event-replay) guard makes reprocessing safe):

```csharp
try
{
    await sagaStore.SaveAsync(state, cancellationToken);
}
catch (ConcurrencyException)
{
    // A concurrent event advanced this saga. Reload and let the event be re-delivered;
    // TryMarkEventProcessed prevents duplicate command dispatch.
}
```

:::info Changed in Sprint 840 (bd-eszc06)

The SQL saga store previously issued an unconditional last-writer-wins `UPDATE` that ignored `Version`, so concurrent saves for one saga could lose updates. The save path now always enforces the version check; there is no save path that ignores `Version`.
:::

## Save-Then-Dispatch Ordering

Commands and events a saga emits during `HandleAsync` — via the `SendCommandAsync` / `PublishEventAsync` helpers on `SagaBase<TState>` — are **buffered** and dispatched only **after** the saga state has been durably persisted:

```
HandleAsync(event)
  → SendCommandAsync(cmd)   // buffered, NOT dispatched yet
  → PublishEventAsync(evt)  // buffered, NOT dispatched yet
→ ISagaStore.SaveAsync(state)        // state + processed-eventId persisted FIRST
→ FlushPendingDispatchesAsync()      // buffered messages dispatched, in emit (FIFO) order
```

This guarantees that a `SaveAsync` failure dispatches **nothing**: the event is re-delivered later and the saga re-buffers its emissions without double-dispatching, so a persistence failure can never leave the saga state behind already-sent side effects. Dispatch is driven by the coordinator after the save, so a saga subclass cannot trigger an early "dispatch-before-save" — the ordering is structural.

:::info Changed in Sprint 850 (bd-lc178k)
Previously emitted commands were dispatched immediately and `SaveAsync` ran afterward, so a persistence failure followed by replay re-dispatched the command (duplicate side effects). `SendCommandAsync` / `PublishEventAsync` remain the same `protected` helpers; they now return after buffering (no dispatch result) because the actual dispatch happens after the save.
:::

## Handling Events for Missing Sagas

When a correlated event arrives for a saga instance that does not exist (already completed, expired, or never started), the coordinator invokes the registered `ISagaNotFoundHandler<TSaga>` instead of silently dropping the event. A default `LoggingNotFoundHandler<TSaga>` is registered out of the box, so the orphaned continuation is always logged.

Register a custom handler to dead-letter, park, or compensate the orphaned event:

```csharp
public sealed class OrderSagaNotFoundHandler : ISagaNotFoundHandler<OrderSaga>
{
    public Task HandleAsync(object message, string sagaId, CancellationToken cancellationToken)
    {
        // e.g. route to a dead-letter queue, raise a compensation, or alert
        return Task.CompletedTask;
    }
}

// Registration (ISagaBuilder fluent API):
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithNotFoundHandler<OrderSaga, OrderSagaNotFoundHandler>()));
```

`WithNotFoundHandler<TSaga>()` (no handler type) registers the default logging handler explicitly. Registration uses `TryAdd` semantics, so your custom handler replaces the default only when registered first. If no handler is resolvable, the coordinator falls back to a warning log (fail-open).

:::info Changed in Sprint 850 (bd-ckavfs)
`ISagaNotFoundHandler<TSaga>` existed but was never invoked — the saga-not-found branch only logged and returned. It is now resolved and called.
:::

## Persistence Providers

Each provider plugs into the `ISagaBuilder` fluent API:

```csharp
// SQL Server
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts()));

// PostgreSQL
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UsePostgres(pg => pg.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts()));

// Azure Cosmos DB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString("AccountEndpoint=...;AccountKey=...")
              .DatabaseName("myapp")
              .ContainerName("sagas");
    })));

// AWS DynamoDB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseDynamoDb(options =>
    {
        options.Connection.Region = "us-east-1";
        options.TableName = "sagas";
    })));

// MongoDB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseMongoDB(mongo =>
    {
        mongo.ConnectionString("mongodb://localhost:27017")
             .DatabaseName("myapp")
             .CollectionName("sagas");
    })));

// Google Firestore
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseFirestore(options =>
    {
        options.ProjectId = "my-project";
        options.CollectionName = "sagas";
    })));
```

## Builder Extensions

The `ISagaBuilder` fluent API provides optional capabilities:

| Extension | Purpose |
|-----------|---------|
| `.WithCoordination()` | Registers `SagaCoordinator` + `SagaHandlingMiddleware` |
| `.WithTimeouts()` | Enables timeout scheduling and delivery |
| `.WithInstrumentation()` | Adds OpenTelemetry tracing and metrics |
| `.WithOutbox()` | Integrates saga events with the outbox pattern |
| `.WithCorrelation()` | Enables saga lookup by business identifiers |
| `.WithReminders()` | Saga reminder scheduling |

## SQL Server Correlation Queries

Look up saga instances by business identifiers using `ISagaCorrelationQuery`:

```csharp
// Find sagas by correlation ID (uses indexed computed column)
var sagas = await correlationQuery.FindByCorrelationIdAsync("order-123", ct);

// Find sagas by arbitrary JSON property (uses JSON_VALUE)
var sagas = await correlationQuery.FindByPropertyAsync("CustomerId", "cust-456", ct);
```

Register via the builder:

```csharp
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCorrelationQuery()));
```

Property names in `FindByPropertyAsync` are validated against a `[GeneratedRegex]` whitelist to prevent JSON path injection.

## What's Next

- [Orchestration vs Choreography](./orchestration-vs-choreography.md) — Compare centralized orchestration and decentralized choreography patterns
- [Outbox Pattern](../patterns/outbox.md) — Reliable message publishing
- [Inbox Pattern](../patterns/inbox.md) — Idempotent message processing
- [Event Sourcing](../event-sourcing/index.md) — Store state as events
