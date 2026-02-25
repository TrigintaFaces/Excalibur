---
sidebar_position: 9
title: Sagas & Workflows
description: Orchestrate multi-step business processes with compensation, parallel steps, and retry policies using Excalibur.Saga.
---

# Sagas & Workflows

Sagas coordinate multi-step business processes where failure in one step requires compensating (rolling back) previously completed steps. Excalibur provides two saga APIs for different scenarios.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Saga
  dotnet add package Excalibur.Saga.SqlServer  # for persistence
  ```
- Familiarity with [handlers](../handlers.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Two Approaches

Excalibur supports two saga patterns. Choose based on how your services communicate:

| | Step-Based | Event-Driven |
|--|-----------|-------------|
| **API** | `ISagaDefinition` + `ISagaStep` | `SagaBase<T>` + `ISagaCoordinator` |
| **Execution** | Runs all steps sequentially in one process | Processes one event at a time, suspends between events |
| **Service calls** | Steps `await` service calls directly | Steps publish commands/events via Dispatch |
| **Best for** | In-process coordination, API gateway, modular monolith | Cross-service microservices, long-running workflows |
| **Guide** | [Building Your First Saga](building-your-first-saga.md) | [Orchestration vs Choreography](orchestration-vs-choreography.md) |

**Step-based** sagas are simpler to write and debug. The orchestrator calls each service directly and handles compensation automatically. Use them when all participating services are reachable from a single process (e.g., a modular monolith or an API that calls internal services).

**Event-driven** sagas work across independently deployed microservices. Each step publishes a command, then the saga suspends until it receives a response event. State is persisted between events so the saga survives process restarts. Use them when services are truly independent and communicate only through messages.

:::tip New to sagas?
Start with **[Building Your First Saga](building-your-first-saga.md)** to learn compensation, failure handling, and retry logic using the simpler step-based API. Then read **[Orchestration vs Choreography](orchestration-vs-choreography.md)** for the event-driven pattern used in microservice architectures.
:::

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Saga` | Core saga engine, orchestrator, coordinator |
| `Excalibur.Saga.SqlServer` | SQL Server saga state persistence |

## Quick Start

### 1. Define a Saga

Create a saga by implementing `ISagaDefinition<TSagaData>`:

```csharp
using Excalibur.Saga.Abstractions;

public class OrderSagaData
{
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ShipmentId { get; set; }
    public bool InventoryReserved { get; set; }
}

public class OrderSaga : ISagaDefinition<OrderSagaData>
{
    public string Name => "OrderSaga";
    public TimeSpan Timeout => TimeSpan.FromMinutes(30);

    public IReadOnlyList<ISagaStep<OrderSagaData>> Steps => new ISagaStep<OrderSagaData>[]
    {
        new ReserveInventoryStep(),
        new ProcessPaymentStep(),
        new ShipOrderStep()
    };

    // RetryPolicy is optional - return null for no retries
    // To implement custom retry logic, implement IRetryPolicy
    public IRetryPolicy? RetryPolicy => null;

    public Task OnCompletedAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Called when all steps succeed
        return Task.CompletedTask;
    }

    public Task OnFailedAsync(
        ISagaContext<OrderSagaData> context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Called when saga fails after compensation
        return Task.CompletedTask;
    }
}
```

### 2. Define Steps

Each step implements `ISagaStep<TSagaData>` with execute and compensate logic:

```csharp
public class ReserveInventoryStep : ISagaStep<OrderSagaData>
{
    public string Name => "ReserveInventory";
    public bool CanCompensate => true;
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);

    public async Task<StepResult> ExecuteAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Reserve inventory for the order
        context.Data.InventoryReserved = true;
        return StepResult.Success();
    }

    public async Task<StepResult> CompensateAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Release the reserved inventory
        context.Data.InventoryReserved = false;
        return StepResult.Success();
    }
}

public class ProcessPaymentStep : ISagaStep<OrderSagaData>
{
    public string Name => "ProcessPayment";
    public bool CanCompensate => true;
    public TimeSpan Timeout => TimeSpan.FromSeconds(60);

    public async Task<StepResult> ExecuteAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Charge the customer
        context.Data.PaymentId = Guid.NewGuid();
        return StepResult.Success();
    }

    public async Task<StepResult> CompensateAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Refund the charge
        context.Data.PaymentId = Guid.Empty;
        return StepResult.Success();
    }
}

public class ShipOrderStep : ISagaStep<OrderSagaData>
{
    public string Name => "ShipOrder";
    public bool CanCompensate => false; // Cannot un-ship
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public async Task<StepResult> ExecuteAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        context.Data.ShipmentId = Guid.NewGuid();
        return StepResult.Success();
    }

    public Task<StepResult> CompensateAsync(
        ISagaContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Not compensable - CanCompensate is false
        return Task.FromResult(StepResult.Success());
    }
}
```

### 3. Register and Execute

```csharp
using Microsoft.Extensions.DependencyInjection;

// Registration
services.AddExcaliburSaga(options =>
{
    // Configure saga options
});

// Execution via ISagaOrchestrator
public class OrderController
{
    private readonly ISagaOrchestrator _orchestrator;
    private readonly OrderSaga _sagaDefinition;

    public OrderController(ISagaOrchestrator orchestrator, OrderSaga sagaDefinition)
    {
        _orchestrator = orchestrator;
        _sagaDefinition = sagaDefinition;
    }

    public string CreateOrder(CreateOrderRequest request)
    {
        var saga = _orchestrator.CreateSaga(
            _sagaDefinition,
            new OrderSagaData { OrderId = request.OrderId });

        return saga.SagaId;
    }
}
```

## Compensation

When a step fails, the saga engine automatically compensates previously completed steps **in reverse order**:

```
Step 1: ReserveInventory  ✓ (completed)
Step 2: ProcessPayment    ✓ (completed)
Step 3: ShipOrder         ✗ (failed)

Compensation runs:
  → Compensate ProcessPayment  (refund)
  → Compensate ReserveInventory (release stock)
```

Steps with `CanCompensate = false` are skipped during compensation. Place non-compensable steps last when possible.

## Saga Status

Track saga progress through these statuses:

| Status | Meaning |
|--------|---------|
| `Created` | Saga initialized, not yet started |
| `Running` | Executing steps |
| `Completed` | All steps succeeded |
| `Failed` | Steps failed, compensation finished or not possible |
| `Compensating` | Rolling back completed steps |
| `Compensated` | All compensations succeeded |
| `Cancelled` | Manually cancelled |
| `Suspended` | Paused, awaiting external input |
| `Expired` | Timed out |

### Step Status

| Status | Meaning |
|--------|---------|
| `NotStarted` | Step has not begun |
| `Running` | Currently executing |
| `Succeeded` | Completed successfully |
| `Failed` | Execution failed |
| `Skipped` | Conditionally skipped |
| `TimedOut` | Exceeded step timeout |

### Compensation Status

| Status | Meaning |
|--------|---------|
| `NotRequired` | Step did not need compensation |
| `Pending` | Awaiting compensation |
| `Running` | Compensation in progress |
| `Succeeded` | Compensation completed |
| `Failed` | Compensation failed |
| `NotCompensable` | Step cannot be compensated (`CanCompensate = false`) |

## Managing Sagas

Use `ISagaOrchestrator` to manage saga lifecycle:

```csharp
// Create a new saga (synchronous, requires saga definition)
var saga = orchestrator.CreateSaga(sagaDefinition, data);
var sagaId = saga.SagaId;

// Query saga state (requires type parameter)
var saga = await orchestrator.GetSagaAsync<OrderSagaData>(sagaId, ct);

// List active sagas
var active = await orchestrator.ListActiveSagasAsync(ct);

// Cancel a running saga (requires reason)
await orchestrator.CancelSagaAsync(sagaId, "User requested cancellation", ct);
```

## Event-Driven Sagas

The step-based pattern shown above runs all steps in one call. For sagas that span independent microservices, use the **event-driven** pattern instead. The `ISagaCoordinator` processes incoming events to advance the saga one step at a time, persisting state between events. See [Orchestration vs Choreography](orchestration-vs-choreography.md) for the full event-driven pattern using `SagaBase<T>`.

Events must implement `ISagaEvent`:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;

// Define a saga event (must implement ISagaEvent)
public record OrderPlaced(Guid OrderId, string SagaId, string? StepId = null) : ISagaEvent;

// Handle saga events via the coordinator
public class OrderEventHandler : IEventHandler<OrderPlaced>
{
    private readonly ISagaCoordinator _coordinator;
    private readonly IMessageContextAccessor _contextAccessor;

    public OrderEventHandler(
        ISagaCoordinator coordinator,
        IMessageContextAccessor contextAccessor)
    {
        _coordinator = coordinator;
        _contextAccessor = contextAccessor;
    }

    public async Task HandleAsync(OrderPlaced @event, CancellationToken ct)
    {
        // Get context from accessor (set by pipeline during message processing)
        var context = _contextAccessor.MessageContext
            ?? throw new InvalidOperationException("No message context available");

        // ProcessEventAsync requires message context and ISagaEvent
        await _coordinator.ProcessEventAsync(context, @event, ct);
    }
}
```

## SQL Server Saga Store

Persist saga state to SQL Server for durability:

```csharp
services.AddExcaliburSaga(options =>
{
    // Configure SQL Server persistence
});
```

The SQL Server store provides:
- Durable saga state persistence
- Concurrent saga execution safety
- Query support for saga status and history

## Retry Policies

Configure retry behavior by implementing `IRetryPolicy`:

```csharp
// No retries (default)
public IRetryPolicy? RetryPolicy => null;

// Custom retry policy - implement IRetryPolicy
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(2);

    public bool ShouldRetry(Exception exception)
    {
        // Return true for transient failures
        return exception is TimeoutException or HttpRequestException;
    }
}

// Use in saga definition
public IRetryPolicy? RetryPolicy => new ExponentialBackoffRetryPolicy
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromSeconds(2)
};
```

The `IRetryPolicy` interface defines:

| Member | Description |
|--------|-------------|
| `MaxAttempts` | Maximum number of retry attempts |
| `Delay` | Delay between retry attempts |
| `ShouldRetry(Exception)` | Determines if an exception should trigger a retry |

## What's Next

- [Outbox Pattern](../patterns/outbox.md) - Reliable message publishing
- [Inbox Pattern](../patterns/inbox.md) - Idempotent message processing
- [Event Sourcing](../event-sourcing/index.md) - Store state as events
- [Jobs & Workflows](../patterns/jobs.md) - Background job coordination

## See Also

- [Building Your First Saga](./building-your-first-saga.md) — Step-by-step guide to creating a saga with compensation and retry logic
- [Orchestration vs Choreography](./orchestration-vs-choreography.md) — Compare centralized orchestration and decentralized choreography patterns
- [Event Sourcing](../event-sourcing/index.md) — Store domain state as a sequence of events, often used alongside sagas
