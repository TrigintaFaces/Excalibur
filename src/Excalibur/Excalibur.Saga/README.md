# Excalibur.Saga

Saga state persistence and coordination for distributed transactions in .NET applications.

## Overview

`Excalibur.Saga` provides the foundation for implementing the Saga pattern - a way to manage distributed transactions across multiple services without requiring two-phase commit (2PC). It ensures data consistency in microservices architectures by coordinating compensating transactions when failures occur.

## Installation

```bash
dotnet add package Excalibur.Saga
```

For SQL Server persistence:

```bash
dotnet add package Excalibur.Saga.SqlServer
```

## Features

| Feature                   | Description                                      |
| ------------------------- | ------------------------------------------------ |
| **ISagaStore**            | Saga state persistence abstraction               |
| **SagaOptions**           | Configuration for timeouts, retries, concurrency |
| **Compensation Tracking** | Track compensation status per step               |
| **Retry Policies**        | Built-in exponential backoff and fixed delay     |
| **AOT Compatible**        | Full Native AOT support                          |
| **Provider Agnostic**     | Pluggable storage backends                       |

## Quick Start

### 1. Register Services

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExcaliburSaga(options =>
{
    options.MaxConcurrency = 10;
    options.DefaultTimeout = TimeSpan.FromMinutes(30);
    options.MaxRetryAttempts = 3;
    options.RetryDelay = TimeSpan.FromMinutes(1);
});

// Add SQL Server persistence (optional)
services.AddExcaliburSagaSqlServer(connectionString);
```

### 2. Define Saga State

```csharp
using Excalibur.Dispatch.Abstractions.Messaging.Delivery;

public class OrderFulfillmentState : SagaState
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }

    // Step completion tracking
    public bool OrderCreated { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool InventoryReserved { get; set; }
    public bool ShipmentScheduled { get; set; }

    // Compensation tracking
    public bool PaymentRefunded { get; set; }
    public bool InventoryReleased { get; set; }

    // Error information
    public string? FailureReason { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
}
```

### 3. Implement Saga Orchestrator

```csharp
using Excalibur.Dispatch.Abstractions.Messaging.Delivery;

public class OrderFulfillmentSaga : Saga<OrderFulfillmentState>
{
    public OrderFulfillmentSaga(
        OrderFulfillmentState state,
        IDispatcher dispatcher,
        ILogger<OrderFulfillmentSaga> logger)
        : base(state, dispatcher, logger)
    {
    }

    public override bool HandlesEvent(object eventMessage)
    {
        return eventMessage is StartOrderFulfillment
            or OrderCreated
            or PaymentProcessed
            or PaymentFailed
            or InventoryReserved
            or ShipmentScheduled;
    }

    public override async Task HandleAsync(
        object eventMessage,
        CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrderFulfillment start:
                await HandleStart(start, cancellationToken);
                break;
            case OrderCreated created:
                await HandleOrderCreated(created, cancellationToken);
                break;
            case PaymentProcessed processed:
                await HandlePaymentProcessed(processed, cancellationToken);
                break;
            case PaymentFailed failed:
                await HandlePaymentFailed(failed, cancellationToken);
                break;
            // ... additional handlers
        }
    }

    private async Task HandleStart(
        StartOrderFulfillment start,
        CancellationToken cancellationToken)
    {
        State.OrderId = start.OrderId;
        State.CustomerId = start.CustomerId;
        State.TotalAmount = start.TotalAmount;

        Logger.LogInformation("Starting saga for order {OrderId}", start.OrderId);

        await Dispatcher.DispatchAsync(
            new CreateOrder(start.OrderId, start.CustomerId, start.TotalAmount),
            cancellationToken);
    }

    // ... additional handler methods
}
```

## Core Concepts

### Saga State

The `SagaState` base class provides essential tracking:

```csharp
public abstract class SagaState
{
    /// <summary>
    /// Unique identifier for this saga instance.
    /// </summary>
    public Guid SagaId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Whether the saga has completed (successfully or via compensation).
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// Current step in the saga workflow.
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// When the saga was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

### Saga Store

`ISagaStore` persists saga state across process restarts:

```csharp
public interface ISagaStore
{
    Task<TSagaState?> GetAsync<TSagaState>(
        Guid sagaId,
        CancellationToken cancellationToken)
        where TSagaState : SagaState;

    Task SaveAsync<TSagaState>(
        TSagaState state,
        CancellationToken cancellationToken)
        where TSagaState : SagaState;

    Task DeleteAsync(
        Guid sagaId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TSagaState>> GetPendingAsync<TSagaState>(
        CancellationToken cancellationToken)
        where TSagaState : SagaState;
}
```

### Compensation Status

Track compensation state per step:

```csharp
public enum CompensationStatus
{
    /// <summary>
    /// Step completed successfully, no compensation needed.
    /// </summary>
    NotRequired,

    /// <summary>
    /// Compensation is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Compensation is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Compensation completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Compensation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Step cannot be compensated.
    /// </summary>
    NotCompensable
}
```

## Configuration Options

### SagaOptions

```csharp
public class SagaOptions
{
    /// <summary>
    /// Maximum concurrent saga executions.
    /// Default: 10
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Default timeout for saga steps.
    /// Default: 30 minutes
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum retry attempts before dead letter.
    /// Default: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// Default: 1 minute
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}
```

### Retry Policies

Built-in retry strategies for transient failures:

```csharp
// Exponential backoff: 1s, 2s, 4s...
var exponential = RetryPolicy.ExponentialBackoff(
    maxAttempts: 3,
    initialDelay: TimeSpan.FromSeconds(1));

// Fixed delay between retries
var fixed = RetryPolicy.FixedDelay(
    maxAttempts: 3,
    delay: TimeSpan.FromSeconds(5));

// Custom policy
var custom = new RetryPolicy
{
    MaxAttempts = 5,
    InitialDelay = TimeSpan.FromMilliseconds(500),
    MaxDelay = TimeSpan.FromSeconds(30),
    BackoffMultiplier = 2.0,
    UseJitter = true  // Prevents thundering herd
};
```

## Saga Patterns

### Orchestration Pattern

A central coordinator manages the saga workflow:

```
┌─────────────┐
│ Orchestrator│
└──────┬──────┘
       │
       ├──► Step 1: Create Order
       ├──► Step 2: Process Payment
       ├──► Step 3: Reserve Inventory
       └──► Step 4: Schedule Shipment
```

**Use when:**

- Complex workflows with conditional logic
- Need central visibility and monitoring
- Easier debugging requirements

### Choreography Pattern

Services react to events without central coordination:

```
OrderPlaced ──► PaymentProcessed ──► InventoryReserved ──► ShipmentScheduled
     │                │                      │
     ▼                ▼                      ▼
  Payment          Inventory              Shipping
  Service          Service                Service
```

**Use when:**

- Simple, linear workflows
- Loose coupling required
- High scalability needed

## Compensation

When a step fails, previous steps must be compensated (rolled back):

```csharp
public class ProcessPaymentStep : ISagaStep<OrderSagaData>
{
    public string Name => "ProcessPayment";
    public TimeSpan Timeout => TimeSpan.FromSeconds(60);
    public RetryPolicy? RetryPolicy => RetryPolicy.ExponentialBackoff(3);
    public bool CanCompensate => true;

    public async Task<StepResult> ExecuteAsync(
        SagaExecutionContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        var paymentId = await _gateway.ChargeAsync(
            context.Data.CustomerId,
            context.Data.Amount,
            cancellationToken);

        context.Data.PaymentId = paymentId;
        return StepResult.Success();
    }

    public async Task<StepResult> CompensateAsync(
        SagaExecutionContext<OrderSagaData> context,
        CancellationToken cancellationToken)
    {
        // Refund the payment
        await _gateway.RefundAsync(
            context.Data.PaymentId,
            cancellationToken);

        return StepResult.Success();
    }
}
```

## Timeout and Retry

Configure timeouts and retries per step:

```csharp
public class ExternalApiStep : ISagaStep<OrderSagaData>
{
    public string Name => "CallExternalApi";

    // Step times out after 45 seconds
    public TimeSpan Timeout => TimeSpan.FromSeconds(45);

    // Retry with exponential backoff + jitter
    public RetryPolicy? RetryPolicy => new RetryPolicy
    {
        MaxAttempts = 4,
        InitialDelay = TimeSpan.FromSeconds(1),
        BackoffMultiplier = 2.0,
        UseJitter = true
    };

    public bool CanCompensate => true;

    // ... implementation
}
```

## Integration with Event Sourcing

Sagas work seamlessly with event-sourced aggregates:

```csharp
public class OrderSagaEventHandler : IEventHandler<OrderPlaced>
{
    private readonly ISagaCoordinator _coordinator;

    public async Task HandleAsync(
        OrderPlaced @event,
        CancellationToken cancellationToken)
    {
        // Start saga when order is placed
        await _coordinator.StartAsync(new StartOrderFulfillment
        {
            SagaId = Guid.NewGuid().ToString(),
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            TotalAmount = @event.TotalAmount
        }, cancellationToken);
    }
}
```

## SQL Server Implementation

For production use, add SQL Server persistence:

```csharp
services.AddExcaliburSagaSqlServer(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "saga";
    options.TableName = "SagaState";
});
```

This creates a table to store saga state:

```sql
CREATE TABLE [saga].[SagaState] (
    [SagaId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [SagaType] NVARCHAR(256) NOT NULL,
    [State] NVARCHAR(MAX) NOT NULL,
    [Status] INT NOT NULL,
    [CurrentStep] INT NOT NULL,
    [StartedAt] DATETIMEOFFSET NOT NULL,
    [CompletedAt] DATETIMEOFFSET NULL,
    [Version] INT NOT NULL
);
```

## Monitoring

Track saga execution with logging and metrics:

```csharp
public class MonitoredSaga : Saga<OrderFulfillmentState>
{
    public override async Task HandleAsync(
        object eventMessage,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Saga.HandleEvent");
        activity?.SetTag("saga.id", State.SagaId);
        activity?.SetTag("event.type", eventMessage.GetType().Name);

        try
        {
            await base.HandleAsync(eventMessage, cancellationToken);
            SagaMetrics.EventsProcessed.Inc();
        }
        catch (Exception ex)
        {
            SagaMetrics.EventsFailed.Inc();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Best Practices

### DO

- **Persist state before external calls** - Recover from crashes
- **Make steps idempotent** - Handle duplicate deliveries
- **Design compensations carefully** - Not all actions can be undone
- **Use correlation IDs** - Track the entire distributed transaction
- **Set appropriate timeouts** - Prevent indefinite hanging
- **Monitor saga health** - Alert on stuck or failed sagas

### DON'T

- **Don't mix orchestration and choreography** - Choose one per saga
- **Don't store transient data in saga state** - Only track step completion
- **Don't make orchestrators do business logic** - Delegate to services
- **Don't forget compensation** - Always plan for failure scenarios
- **Don't use sagas for simple operations** - Adds unnecessary complexity

## Related Packages

| Package                    | Purpose                                  |
| -------------------------- | ---------------------------------------- |
| `Excalibur.Saga.SqlServer` | SQL Server saga store implementation     |
| `Excalibur.Dispatch.Patterns`        | Saga step abstractions and orchestration |
| `Excalibur.Dispatch.Abstractions`    | Core saga interfaces                     |
| `Dispatch`                 | Message dispatching for saga events      |

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
