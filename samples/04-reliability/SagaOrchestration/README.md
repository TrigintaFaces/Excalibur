# Saga Orchestration Example

This example demonstrates the **Excalibur.Saga** framework's event-driven choreography model for long-running business processes.

## Overview

```
Event-Driven Saga Flow:

  StartOrderProcessing (creates saga)
       |
       v
  InventoryReserved --> PaymentProcessed --> OrderShipped --> COMPLETE
                              |
                              +-- PaymentFailed --> FAILED (graceful)
                              |
                              +-- PaymentTimeout --> FAILED (timed out)
```

## Key Patterns Demonstrated

### 1. Event-Driven Choreography with `SagaBase<TSagaState>`

The saga extends the framework's base class and reacts to events dispatched through `IDispatcher`:

```csharp
public sealed partial class OrderFulfillmentSaga(
    OrderSagaState initialState,
    IDispatcher dispatcher,
    ILogger<OrderFulfillmentSaga> logger)
    : SagaBase<OrderSagaState>(initialState, dispatcher, logger),
      ISagaTimeout<PaymentTimeout>
{
    public override async Task HandleAsync(object eventMessage, CancellationToken ct)
    {
        switch (eventMessage)
        {
            case StartOrderProcessing start:
                // Initialize state, schedule timeout
                break;
            case PaymentProcessed paid:
                // Cancel timeout, mark step complete
                break;
            // ...
        }
    }
}
```

### 2. Timeout Scheduling with `ISagaTimeout<T>`

Schedule timeouts and handle them via a strongly-typed interface:

```csharp
// Schedule a timeout during saga start
State.TimeoutId = await RequestTimeoutAsync<PaymentTimeout>(
    TimeSpan.FromMinutes(5), cancellationToken);

// Implement the timeout handler
public Task HandleTimeoutAsync(PaymentTimeout message, CancellationToken ct)
{
    State.FailureReason = "Payment confirmation timed out";
    MarkCompleted();
    return Task.CompletedTask;
}
```

### 3. Saga State with Built-in Idempotency

Extending `SagaState` provides versioning and duplicate event detection:

```csharp
public sealed class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    // ... domain-specific state
}
```

### 4. Framework DI Registration

```csharp
services.AddExcaliburOrchestration();    // Coordinator + middleware + store
services.AddSagaTimeoutDelivery();       // Timeout infrastructure
services.AddSaga<OrderFulfillmentSaga, OrderSagaState>();  // Register saga type

SagaRegistry.Register<OrderFulfillmentSaga, OrderSagaState>(info =>
{
    info.StartsWith<StartOrderProcessing>();
    info.Handles<InventoryReserved>();
    info.Handles<PaymentProcessed>();
    info.Handles<OrderShipped>();
    info.Handles<PaymentFailed>();
    info.Handles<PaymentTimeout>();
});
```

## Project Structure

```
SagaOrchestration/
├── SagaOrchestration.csproj
├── README.md
├── Program.cs                           # Demo entry point with two scenarios
├── Sagas/
│   ├── OrderSagaState.cs               # Saga state (extends SagaState)
│   ├── OrderSagaEvents.cs             # ISagaEvent implementations
│   └── OrderFulfillmentSaga.cs         # Saga logic (extends SagaBase<T>)
├── Timeouts/
│   └── SagaTimeouts.cs                 # PaymentTimeout (ISagaEvent)
└── Configuration/
    └── SagaConfiguration.cs            # DI setup
```

## Running the Example

```bash
cd samples/04-reliability/SagaOrchestration
dotnet run
```

### Expected Output

```
====================================================================
       SAGA ORCHESTRATION PATTERNS EXAMPLE (Excalibur.Saga)
====================================================================

--------------------------------------------------------------------
  DEMO 1: Happy Path (Event-Driven Saga)
--------------------------------------------------------------------

[1] Dispatching StartOrderProcessing (SagaId: abc12345...)
[2] Dispatching InventoryReserved
[3] Dispatching PaymentProcessed
[4] Dispatching OrderShipped

Saga completed through event-driven choreography.

--------------------------------------------------------------------
  DEMO 2: Payment Failure (Graceful Handling)
--------------------------------------------------------------------

[1] Dispatching StartOrderProcessing (SagaId: def67890...)
[2] Dispatching InventoryReserved
[3] Dispatching PaymentFailed

Saga handled payment failure gracefully.
```

## Framework Types Used

| Type | Package | Purpose |
|------|---------|---------|
| `SagaBase<TSagaState>` | Excalibur.Saga | Abstract saga base class |
| `SagaState` | Excalibur.Dispatch.Abstractions | Base state with versioning + idempotency |
| `ISagaEvent` | Excalibur.Dispatch.Abstractions | Event marker for saga routing |
| `ISagaTimeout<T>` | Excalibur.Saga | Typed timeout handler interface |
| `SagaRegistry` | Excalibur.Saga | Static event-to-saga mapping |
| `SagaCoordinator` | Excalibur.Saga | Routes events to saga instances |
| `IDispatcher` | Excalibur.Dispatch.Abstractions | Dispatches messages through pipeline |

## Best Practices

1. **Use `ISagaEvent` for all saga messages** -- enables automatic routing via the middleware
2. **Set `StepId` on events** -- enables idempotent deduplication per step
3. **Schedule timeouts for expected responses** -- prevents sagas from hanging indefinitely
4. **Use `MarkCompletedAsync` (not `MarkCompleted`)** -- cancels all pending timeouts automatically
5. **Register all handled events in `SagaRegistry`** -- the coordinator only routes registered events
