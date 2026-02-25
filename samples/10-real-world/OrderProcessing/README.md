# Order Processing Sample

This comprehensive sample demonstrates how multiple Dispatch and Excalibur patterns work together in a realistic order processing workflow.

## Overview

The sample implements a complete e-commerce order processing system that showcases:

- **Event Sourcing** - Order aggregate with full event history
- **CQRS Commands** - Separate command/query models with validation
- **Saga Pattern** - Multi-step workflow orchestration
- **Retry Pattern** - Exponential backoff for transient failures
- **Compensation** - Saga rollback on failure
- **External Service Integration** - Payment, shipping, inventory

## Quick Start

```bash
cd samples/10-real-world/OrderProcessing
dotnet run
```

## Order Workflow

```
┌────────────────────────────────────────────────────────────┐
│                    ORDER LIFECYCLE                          │
├────────────────────────────────────────────────────────────┤
│                                                             │
│    ┌─────────┐    ┌───────────┐    ┌─────────────────┐     │
│    │ Created │───►│ Validated │───►│ PaymentProcessed│     │
│    └─────────┘    └───────────┘    └─────────────────┘     │
│         │              │                    │               │
│         │              │                    ▼               │
│         │              │            ┌───────────┐           │
│         │              │            │  Shipped  │           │
│         │              │            └───────────┘           │
│         │              │                    │               │
│         │              │                    ▼               │
│         │              │            ┌───────────┐           │
│         │              │            │ Completed │           │
│         │              │            └───────────┘           │
│         │              │                                    │
│         │              ▼                                    │
│         │    ┌──────────────────┐                          │
│         │    │ ValidationFailed │                          │
│         │    └──────────────────┘                          │
│         │                                                   │
│         └──────────────────────────────────────────┐       │
│                                                     │       │
│                                             ┌───────▼──┐    │
│                                             │ Cancelled│    │
│                                             └──────────┘    │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

## Patterns Demonstrated

### 1. Event Sourcing (OrderAggregate)

```csharp
public class OrderAggregate : AggregateRoot<Guid>
{
    // State derived from events
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }

    // Commands raise events
    public void RecordPayment(string transactionId, decimal amount)
    {
        EnsureStatus(OrderStatus.Validated, "process payment");
        RaiseEvent(new PaymentProcessed(Id, transactionId, amount, Version));
    }

    // Events mutate state
    protected override void ApplyEventInternal(IDomainEvent @event) => @event switch
    {
        PaymentProcessed e => ApplyPaymentProcessed(e),
        // ... other events
    };
}
```

### 2. CQRS Commands with FluentValidation

```csharp
// Command
public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<OrderLineItem> Items,
    string ShippingAddress) : IDispatchAction;

// Validator
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item");

        RuleFor(x => x.ShippingAddress)
            .NotEmpty().MinimumLength(10).MaximumLength(500);
    }
}
```

### 3. Saga Pattern (OrderProcessingSaga)

```csharp
public sealed class OrderProcessingSaga : IActionHandler<ProcessOrderCommand>
{
    public async Task HandleAsync(ProcessOrderCommand action, CancellationToken ct)
    {
        var state = new SagaState();

        try
        {
            // Step 1: Validate inventory
            await ValidateInventoryAsync(order, state, ct);

            // Step 2: Reserve inventory
            await ReserveInventoryAsync(order, state, ct);

            // Step 3: Process payment (with retry)
            await ProcessPaymentAsync(order, state, ct);

            // Step 4: Create shipment
            await CreateShipmentAsync(order, state, ct);
        }
        catch (SagaFailedException)
        {
            // Execute compensating actions
            await CompensateAsync(order, state, ct);
        }
    }
}
```

### 4. Retry with Exponential Backoff

```csharp
private async Task ProcessPaymentAsync(...)
{
    const int maxRetries = 3;
    var attempt = 0;

    while (attempt < maxRetries)
    {
        attempt++;
        try
        {
            return await _paymentService.ProcessPaymentAsync(...);
        }
        catch (HttpRequestException)
        {
            if (attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                await Task.Delay(delay, ct);
            }
        }
    }

    throw new SagaFailedException("Payment failed after retries");
}
```

### 5. Compensation on Failure

```csharp
private async Task CompensateAsync(OrderAggregate order, SagaState state, ...)
{
    // Compensate in reverse order

    // Release inventory if reserved
    if (state.InventoryReserved)
    {
        await _inventoryService.ReleaseInventoryAsync(order.Id, ct);
    }

    // Refund payment if processed
    if (state.PaymentProcessed)
    {
        await _paymentService.RefundAsync(state.TransactionId, ct);
    }

    // Cancel the order
    order.Cancel("Saga compensation");
}
```

## Project Structure

```
OrderProcessing/
├── Domain/
│   ├── Aggregates/
│   │   └── OrderAggregate.cs    # Event-sourced aggregate
│   ├── Events/
│   │   └── OrderEvents.cs       # Domain events
│   └── Commands/
│       └── OrderCommands.cs     # CQRS commands
├── Handlers/
│   ├── OrderHandlers.cs         # Command handlers
│   └── OrderValidators.cs       # FluentValidation validators
├── Sagas/
│   └── OrderProcessingSaga.cs   # Workflow orchestration
├── ExternalServices/
│   └── ExternalServices.cs      # Service interfaces + mocks
├── Program.cs                   # Demo scenarios
└── README.md                    # This file
```

## Demo Scenarios

The sample demonstrates 6 scenarios:

1. **Successful Order Processing** - Complete workflow from creation to shipping
2. **Retry Pattern** - Transient payment failures with exponential backoff
3. **Validation Failure** - FluentValidation rejecting invalid commands
4. **Saga Compensation** - Inventory validation failure with rollback
5. **Order Cancellation** - Cancelling an order before processing
6. **Delivery Confirmation** - Completing the order lifecycle

## Production Considerations

### Use Persistent Storage

```csharp
// Replace InMemoryOrderStore with:
services.AddSqlServerEventSourcing(connectionString);
services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});
```

### Use Excalibur.Saga for Persistent State

```csharp
// Saga state persisted to database
services.AddSqlServerSagaStore(connectionString);
services.AddExcaliburSagas(sagas =>
{
    sagas.AddSaga<OrderProcessingSaga>();
});
```

### Use Excalibur.Dispatch.Resilience.Polly

```csharp
// Configure resilience policies
services.AddDispatch(builder => builder
    .AddPollyResilience(options =>
    {
        options.AddRetryPolicy(3, TimeSpan.FromMilliseconds(100));
        options.AddCircuitBreakerPolicy(5, TimeSpan.FromSeconds(30));
    }));
```

### Use the Outbox Pattern

```csharp
// Reliable messaging with transactional outbox
services.AddSqlServerOutboxStore(connectionString);
services.AddDispatch(builder => builder
    .AddOutboxMiddleware());
```

### Add Projections

```csharp
// Read models for queries
public sealed class OrderSummaryProjection : IProjection<string>
{
    public string OrderId { get; set; }
    public string CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }
    // Optimized for dashboard queries
}
```

## Dependencies

- `Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Abstractions` - Interfaces and contracts
- `Excalibur.Dispatch.Validation.FluentValidation` - Validation middleware
- `Excalibur.Domain` - Aggregate base classes
- `Excalibur.EventSourcing.Abstractions` - Event sourcing interfaces
- `FluentValidation` - Command validation
