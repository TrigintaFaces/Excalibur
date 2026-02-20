# Real-World Samples

Production-style samples with realistic complexity and best practices demonstrating how multiple Dispatch and Excalibur patterns work together.

## Samples

| Sample | Description | Patterns Used |
|--------|-------------|---------------|
| [OrderProcessing](OrderProcessing/) | Complete order processing workflow | Event Sourcing, CQRS, Saga, Retry |
| [ECommerce](ECommerce/) | E-commerce order processing system | Hosted Services, Health Checks |
| [EnhancedStores](EnhancedStores/) | Enhanced store patterns | Repository, Persistence |

## OrderProcessing Sample (Flagship)

The [OrderProcessing](OrderProcessing/) sample demonstrates how multiple Dispatch and Excalibur patterns work together in a realistic e-commerce workflow.

### Patterns Demonstrated

| Pattern | Implementation | Package |
|---------|----------------|---------|
| **Event Sourcing** | OrderAggregate with full event history | `Excalibur.Domain` |
| **CQRS** | Separate command/query models | `Dispatch` |
| **Saga Pattern** | 5-step workflow orchestration | Custom implementation |
| **Retry** | Exponential backoff for payment | Built-in |
| **Compensation** | Automatic rollback on failure | Saga state machine |
| **Validation** | FluentValidation integration | `Excalibur.Dispatch.Validation.FluentValidation` |

### Order Workflow

```
┌─────────┐    ┌───────────┐    ┌─────────────────┐    ┌───────────┐    ┌───────────┐
│ Created │───►│ Validated │───►│ PaymentProcessed│───►│  Shipped  │───►│ Completed │
└─────────┘    └───────────┘    └─────────────────┘    └───────────┘    └───────────┘
     │              │
     │              ▼
     │    ┌──────────────────┐
     │    │ ValidationFailed │
     │    └──────────────────┘
     │
     └──────────────────────────────────────────────────────────┐
                                                                 │
                                                         ┌───────▼──┐
                                                         │ Cancelled│
                                                         └──────────┘
```

### Running

```bash
cd samples/10-real-world/OrderProcessing
dotnet run
```

### Demo Scenarios

1. **Successful Order Processing** - Complete workflow from creation to shipping
2. **Retry Pattern** - Transient payment failures with exponential backoff
3. **Validation Failure** - FluentValidation rejecting invalid commands
4. **Saga Compensation** - Inventory validation failure with rollback
5. **Order Cancellation** - Cancelling an order before processing
6. **Delivery Confirmation** - Completing the order lifecycle

## ECommerce Sample

The [ECommerce](ECommerce/) sample demonstrates a complete order processing system with:

- **Order Processing** - Create, validate, and fulfill orders
- **Hosted Services** - Background order processing
- **Health Checks** - Readiness and liveness probes
- **Repository Pattern** - Clean data access
- **Service Layer** - Business logic separation

### Running

```bash
dotnet run --project samples/10-real-world/ECommerce
```

## Design Principles

These samples follow:

1. **Clean Architecture** - Separation of concerns between layers
2. **CQRS** - Commands for writes, queries for reads
3. **Domain-Driven Design** - Rich domain models
4. **Dependency Injection** - Loosely coupled components
5. **Configuration** - Environment-based settings

## Building Production Systems

### Recommended Patterns

| Concern | Pattern | Package |
|---------|---------|---------|
| Messaging | Dispatch pipeline | `Dispatch` |
| Domain | Aggregates | `Excalibur.Domain` |
| Persistence | Event sourcing | `Excalibur.EventSourcing` |
| Reliability | Outbox | `Excalibur.EventSourcing.SqlServer` |
| Resilience | Circuit breaker | `Excalibur.Dispatch.Resilience.Polly` |
| Observability | OpenTelemetry | `Excalibur.Dispatch.Observability` |
| Validation | FluentValidation | `Excalibur.Dispatch.Validation.FluentValidation` |
| Coordination | Leader election | `Excalibur.LeaderElection.Redis` |

### Production Checklist

- [ ] Health checks configured
- [ ] Structured logging enabled
- [ ] Metrics exported
- [ ] Distributed tracing active
- [ ] Retry policies in place
- [ ] Circuit breakers configured
- [ ] Outbox pattern for reliability
- [ ] Validation middleware enabled
- [ ] Leader election for singletons

### Upgrading from In-Memory to Production

```csharp
// Replace InMemoryOrderStore with:
services.AddSqlServerEventSourcing(connectionString);
services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});

// Add resilience
services.AddDispatch(builder => builder
    .AddPollyResilience(options =>
    {
        options.AddRetryPolicy(3, TimeSpan.FromMilliseconds(100));
        options.AddCircuitBreakerPolicy(5, TimeSpan.FromSeconds(30));
    }));

// Add outbox for reliability
services.AddSqlServerOutboxStore(connectionString);
services.AddDispatch(builder => builder.AddOutboxMiddleware());
```

## Related Categories

- [09-advanced/](../09-advanced/) - Individual pattern samples
- [04-reliability/](../04-reliability/) - Outbox, retry, circuit breaker
- [06-security/](../06-security/) - Encryption, audit logging
- [07-observability/](../07-observability/) - OpenTelemetry, health checks

---

*Category: Real-World | Sprint 434*
