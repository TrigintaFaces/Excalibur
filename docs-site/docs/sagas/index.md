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
using Excalibur.Dispatch.Abstractions;

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
using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Abstractions;
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
using Excalibur.Dispatch.Abstractions.Messaging;
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
