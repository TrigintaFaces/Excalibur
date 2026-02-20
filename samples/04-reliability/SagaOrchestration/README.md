# Saga Orchestration Example

This example demonstrates advanced saga orchestration patterns including timeout scheduling, LIFO compensation, state persistence, and operational monitoring.

## Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ORDER FULFILLMENT SAGA                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Execute:   ReserveInventory → ProcessPayment → ShipOrder → COMPLETE        │
│                                      │                                      │
│                                      └── (on failure)                       │
│                                              │                              │
│  Compensate:                                 ▼                              │
│             ReserveInventory ← ProcessPayment (LIFO order)                  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Patterns Demonstrated

### 1. Timeout Scheduling

Schedule timeouts to ensure resources aren't held indefinitely:

```csharp
// Schedule a 5-minute inventory reservation timeout
var timeoutId = await saga.RequestTimeoutAsync<InventoryReservationTimeout>(
    sagaId,
    TimeSpan.FromMinutes(5),
    cancellationToken);

// Cancel if saga completes before timeout
await saga.CancelTimeoutAsync(sagaId, timeoutId, cancellationToken);
```

### 2. LIFO Compensation

Steps are compensated in reverse order (Last-In-First-Out):

```csharp
// Execution order: A → B → C
// If C fails, compensation order: B → A (reverse)
foreach (var stepName in data.CompletedSteps.Reverse())
{
    var step = GetStep(stepName);
    await step.CompensateAsync(data, cancellationToken);
}
```

### 3. State Persistence

State is saved after each step for recovery:

```csharp
public async Task ExecuteStepAsync(OrderSagaData data, ISagaStep step)
{
    var success = await step.ExecuteAsync(data, cancellationToken);
    if (success)
    {
        data.CompletedSteps.Add(step.Name);
        data.Version++;
        await _stateStore.SaveAsync(data, cancellationToken); // Checkpoint
    }
}
```

### 4. Saga Resume After Restart

```csharp
// After process restart, recover and continue from last checkpoint
var saga = new OrderFulfillmentSaga(stateStore, timeoutStore, steps);
await saga.ResumeAsync(sagaId, cancellationToken);
```

### 5. Monitoring Dashboard

Operational visibility into saga execution:

```csharp
var dashboard = await dashboardService.GetDashboardAsync(cancellationToken);
// dashboard.TotalSagas, RunningCount, CompletedCount, FailedCount, SuccessRate

var stuckSagas = await dashboardService.GetStuckSagasAsync(
    TimeSpan.FromMinutes(30), cancellationToken);
// Sagas with no progress for 30+ minutes
```

## Project Structure

```
examples/SagaOrchestration/
├── SagaOrchestration.csproj
├── README.md
├── Program.cs                           # Demo entry point
├── Sagas/
│   ├── OrderSagaData.cs                # Saga state data
│   └── OrderFulfillmentSaga.cs         # Main orchestrator
├── Steps/
│   ├── ISagaStep.cs                    # Step interface
│   ├── ReserveInventoryStep.cs         # Step 1: Reserve inventory
│   ├── ProcessPaymentStep.cs           # Step 2: Process payment
│   └── ShipOrderStep.cs                # Step 3: Ship order
├── Timeouts/
│   └── SagaTimeouts.cs                 # Timeout markers & store
├── Monitoring/
│   └── SagaDashboardService.cs         # Dashboard & monitoring
└── Configuration/
    └── SagaConfiguration.cs            # DI setup
```

## Running the Example

```bash
cd examples/SagaOrchestration
dotnet run
```

### Expected Output

```
╔═══════════════════════════════════════════════════════════╗
║         SAGA ORCHESTRATION PATTERNS EXAMPLE               ║
╚═══════════════════════════════════════════════════════════╝

┌─────────────────────────────────────────────────────────┐
│  DEMO 1: Happy Path (All Steps Succeed)                 │
└─────────────────────────────────────────────────────────┘

Starting saga for Order: ORD-2024-001
Customer: CUST-12345, Amount: $299.99

info: SagaOrchestration.Sagas.OrderFulfillmentSaga[0]
      Saga saga-xxx started for order ORD-2024-001
info: SagaOrchestration.Steps.ReserveInventoryStep[0]
      Reserving inventory for order ORD-2024-001, SKU: SKU-WIDGET-100
info: SagaOrchestration.Steps.ProcessPaymentStep[0]
      Processing payment for order ORD-2024-001, Amount: $299.99
info: SagaOrchestration.Steps.ShipOrderStep[0]
      Creating shipment for order ORD-2024-001

Result: Completed
Completed Steps: ReserveInventory → ProcessPayment → ShipOrder
Tracking Number: TRK-20241223-XXXXXXXX

┌─────────────────────────────────────────────────────────┐
│  DEMO 2: Compensation (Payment Fails → LIFO Rollback)   │
└─────────────────────────────────────────────────────────┘

(Shows inventory reservation being released after payment failure)

┌─────────────────────────────────────────────────────────┐
│  DEMO 3: Monitoring Dashboard                           │
└─────────────────────────────────────────────────────────┘

╔═══════════════════════════════════════════════════════════╗
║                  SAGA DASHBOARD                           ║
╠═══════════════════════════════════════════════════════════╣
║  Total Sagas:          7                                 ║
║  Running:              2                                 ║
║  Completed:            3                                 ║
║  Failed:               1                                 ║
║  Compensating:         1                                 ║
║  Success Rate:        75%                                ║
╚═══════════════════════════════════════════════════════════╝
```

## Configuration

```csharp
builder.Services.AddSagaOrchestration(options =>
{
    options.MaxCompensationRetries = 3;
    options.InventoryReservationTimeout = TimeSpan.FromMinutes(5);
    options.PaymentConfirmationTimeout = TimeSpan.FromMinutes(10);
    options.StuckSagaThreshold = TimeSpan.FromMinutes(30);
});
```

## Best Practices

1. **Always persist state after each step** - Enables recovery after process restart
2. **Use version/optimistic concurrency** - Prevents concurrent updates from corrupting state
3. **Compensate in LIFO order** - Ensures proper rollback semantics
4. **Schedule timeouts for resource reservations** - Prevents holding resources indefinitely
5. **Monitor for stuck sagas** - Alert when sagas haven't progressed in N minutes

## Related Patterns

- [CDC Anti-Corruption Layer](../../09-advanced/CdcAntiCorruption/) - Translating CDC events to commands
- [Saga Functional Tests](../../../tests/functional/Excalibur.Dispatch.Tests.Functional/Workflows/) - Comprehensive test patterns

## Sprint 197 Reference

This example was created as part of Sprint 197 (Saga Orchestration Advanced Tests).

See: `management/sprints/sprint-197-plan.md`

