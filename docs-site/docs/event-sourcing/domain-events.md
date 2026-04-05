---
sidebar_position: 2
title: Domain Events
description: Define and work with domain events for event sourcing
---

# Domain Events

Domain events represent facts that have happened in your domain. They are immutable records of state changes.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [event sourcing concepts](./index.md) and [domain modeling](../domain-modeling/index.md)

## Defining Events

### Using the Base Record

The `DomainEvent` abstract record provides auto-generated defaults for `EventId` (UUID v7), `Version`, `OccurredAt`, `EventType`, and `Metadata`. Override `AggregateId` in derived records:

```csharp
public sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItem> Items) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderLineItem(
    string ProductId,
    int Quantity,
    decimal UnitPrice);
```

### Event Naming Conventions

| Convention | Example | Guideline |
|------------|---------|-----------|
| Past tense | `OrderCreated`, `PaymentReceived` | Events are facts that happened |
| Specific | `OrderShippedToCustomer` | Not generic `StateChanged` |
| Domain language | `InvoiceIssued` | Match ubiquitous language |

### Rich Event Data

Include all data needed to understand what happened:

```csharp
// Good - self-contained event
public sealed record OrderShipped(
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    Address ShippingAddress,
    DateTime EstimatedDelivery,
    IReadOnlyList<ShippedItem> Items) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

// Bad - lacks context
public sealed record OrderShipped(
    Guid OrderId,
    string TrackingNumber) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}
```

## Event Properties

### Standard Properties

Every domain event includes:

```csharp
public interface IDomainEvent
{
    // Unique identifier for this event instance
    string EventId { get; }

    // Which aggregate this event belongs to
    string AggregateId { get; }

    // Aggregate version after this event
    long Version { get; }

    // When the event occurred
    DateTimeOffset OccurredAt { get; }

    // Type name for serialization
    string EventType { get; }

    // Optional metadata
    IDictionary<string, object>? Metadata { get; }
}
```

### Metadata

Add cross-cutting concerns without polluting event data:

```csharp
// When raising events, add metadata using fluent API
var @event = new OrderCreated(orderId, customerId, amount, items)
    .WithMetadata("UserId", currentUserId)
    .WithMetadata("TenantId", tenantId)
    .WithCorrelationId(correlationId)
    .WithCausationId(causationId)
    .WithMetadata("IpAddress", clientIp);
```

### Correlation and Causation

Track event chains:

```csharp
public static class EventMetadataKeys
{
    public const string CorrelationId = "CorrelationId";
    public const string CausationId = "CausationId";
    public const string UserId = "UserId";
}

// First event in chain
var orderCreated = new OrderCreated(...)
{
    Metadata = new Dictionary<string, object>
    {
        [EventMetadataKeys.CorrelationId] = Guid.NewGuid().ToString(),
        [EventMetadataKeys.CausationId] = commandId
    }
};

// Subsequent event carries same correlation, caused by previous event
var paymentReceived = new PaymentReceived(...)
{
    Metadata = new Dictionary<string, object>
    {
        [EventMetadataKeys.CorrelationId] = orderCreated.Metadata[EventMetadataKeys.CorrelationId],
        [EventMetadataKeys.CausationId] = orderCreated.EventId
    }
};
```

## Event Categories

### Domain Events vs Integration Events

```csharp
// Domain Event - internal to bounded context
// Contains rich domain data, extends DomainEvent
public sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItem> Items,
    DiscountApplied? Discount = null) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

// Integration Event - published to other bounded contexts
// Contains only what others need to know (no base class required)
public record OrderCreatedIntegrationEvent(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTimeOffset CreatedAt) : IIntegrationEvent;
```

### Event Transformation

Transform domain events to integration events using `IMessagePublisher`. Use `IMessageContextAccessor` to access the current context and `CreateChildContext()` to propagate correlation metadata:

```csharp
public class OrderCreatedPublisher : IEventHandler<OrderCreated>
{
    private readonly IMessagePublisher _publisher;
    private readonly IMessageContextAccessor _contextAccessor;

    public OrderCreatedPublisher(
        IMessagePublisher publisher,
        IMessageContextAccessor contextAccessor)
    {
        _publisher = publisher;
        _contextAccessor = contextAccessor;
    }

    public async Task HandleAsync(OrderCreated @event, CancellationToken ct)
    {
        var integrationEvent = new OrderCreatedIntegrationEvent(
            Guid.Parse(@event.AggregateId),
            @event.CustomerId,
            @event.TotalAmount,
            @event.OccurredAt);

        // CreateChildContext() automatically propagates:
        // - CorrelationId (for distributed tracing)
        // - CausationId (set to parent's MessageId)
        // - TenantId, UserId, SessionId, WorkflowId
        // - TraceParent (OpenTelemetry)
        var childContext = _contextAccessor.MessageContext?.CreateChildContext();

        await _publisher.PublishAsync(integrationEvent, childContext!, ct);
    }
}
```

:::tip Context Propagation
`CreateChildContext()` ensures correlation chains flow through your system:
- **CorrelationId** groups all messages in a business transaction
- **CausationId** links each message to its direct cause
- **TraceParent** integrates with OpenTelemetry distributed tracing
:::

## Event Validation

### Immutable Construction

Events should be valid at construction:

```csharp
public sealed record OrderCreated : DomainEvent
{
    public Guid OrderId { get; }
    public string CustomerId { get; }
    public decimal TotalAmount { get; }
    public override string AggregateId => OrderId.ToString();

    public OrderCreated(Guid orderId, string customerId, decimal totalAmount)
    {
        // Validate at construction
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId required", nameof(orderId));
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("CustomerId required", nameof(customerId));
        if (totalAmount < 0)
            throw new ArgumentException("TotalAmount cannot be negative", nameof(totalAmount));

        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}
```

### Using Init-Only Properties

Combine init-only properties with the required base constructor:

```csharp
public sealed record OrderCreated : DomainEvent
{
    public required Guid OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
    public override string AggregateId => OrderId.ToString();
}

// Usage - compiler enforces required properties
var @event = new OrderCreated
{
    OrderId = orderId,
    CustomerId = customerId,
    TotalAmount = amount
};
```

## Serialization

### Default Serialization

Events are serialized using the configured serializer. JSON (System.Text.Json) is the default and works with any POCO event type -- no attributes needed.

```csharp
// Register event sourcing
services.AddExcaliburEventSourcing();

// Default: JSON (System.Text.Json) -- works with any POCO event type.
// For binary serialization, install the provider package and call a single method:

// MemoryPack for maximum .NET performance
services.AddMemoryPackSerializer();

// Or MessagePack for cross-language support
services.AddMessagePackSerializer();
```

:::info No serializer-specific attributes needed
Consumer event types do **not** need `[MemoryPackable]`, `[MessagePackObject]`, or any other serializer-specific attributes. Only the internal envelope wrapper uses these attributes. Your domain events remain plain POCOs regardless of which serializer you choose.
:::

### Custom Type Names

The default `EventType` returns the class name (e.g., `"OrderCreated"`). To customize the type name for serialization, hide the base property with `new`:

```csharp
public sealed record OrderCreated(Guid OrderId, string CustomerId) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();

    // Override the virtual EventType property to customize the serialization name
    public override string EventType => "order.created.v1";
}
```

### Handling Unknown Properties

Configure JSON serializer to handle schema evolution:

```csharp
services.AddJsonSerialization(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
});
```

## Best Practices

### Do

- Name events in past tense
- Include all relevant data in the event
- Keep events immutable
- Use metadata for cross-cutting concerns
- Version events when schemas change

### Don't

- Include entity references (only IDs)
- Store derived or computed values
- Include sensitive data without encryption
- Use generic event names like `DataChanged`
- Modify events after they're raised

## Next Steps

- [Aggregates](aggregates.md) — Emit events from aggregates
- [Event Versioning](versioning.md) — Handle schema evolution
- [Event Store](event-store.md) — Persist events

## See Also

- [Domain Modeling](../domain-modeling/index.md) — Broader domain-driven design building blocks including entities and value objects
- [Outbox Pattern](../patterns/outbox.md) — Reliable publishing of domain events to external systems
- [Event Application Pattern](./event-application-pattern.md) — How aggregates apply domain events to update state
- [Aggregates (Domain Modeling)](../domain-modeling/aggregates.md) — Aggregate design guidance from the domain modeling perspective
