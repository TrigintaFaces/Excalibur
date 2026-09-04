---
sidebar_position: 2
title: Domain Events
description: Define and work with domain events for event sourcing
---

# Domain Events

Domain events represent facts that have happened in your domain. They are immutable records of state changes.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [event sourcing concepts](./index.md) and [domain modeling](../domain-modeling/index.md)

## Defining Events

### Using the Base Record

The `DomainEvent` abstract record provides auto-generated defaults for `EventId` (UUID v7), `OccurredAt`, and `Metadata`. Every event declares the name it is stored and transmitted under with `[MessageName]` — see [Stable Message Names](#stable-message-names) below. A domain event carries only its own business data — it does **not** carry an aggregate id or a stream version. The aggregate id is supplied when the event is persisted (as a parameter to the event store), and the stream version is assigned by the store at append time (see [Event Store](event-store.md)).

```csharp
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItem> Items) : DomainEvent;

public record OrderLineItem(
    string ProductId,
    int Quantity,
    decimal UnitPrice);
```

### Stable Message Names

Every event declares the name it is known by with `[MessageName]`, from the `Excalibur.Dispatch` namespace:

```csharp
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(Guid OrderId, string CustomerId) : DomainEvent;
```

This name is the event's identity. It is written into the event store, it is the outbox `MessageType` your
consumers route on, and it is the CloudEvents `type` external subscribers write filters against — one name,
every path. Nothing is derived from the CLR type, so the type is free to move between namespaces and
assemblies, and free to ship in a new assembly version, without changing the identity of anything already
written.

The attribute is required. Registering an event type that declares no name throws, naming the type:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es.RegisterEventTypes<OrderCreated>()));
// InvalidOperationException if OrderCreated carries no [MessageName].
```

Two types may not declare the same name — stored data records the name and nothing else, so they could not
be told apart on the way back. That collision is refused at registration, where it is a configuration error
rather than data read into the wrong type.

:::caution The name is permanent
Once events have been written under a name, that name is part of your stored data forever. Choose it when
you define the event, and change it only through an alias (below). Deleting or editing a live name makes
every event stored under it unreadable.
:::

#### Choosing a name

Use `<Publisher>.<BoundedContext>.<EventName>` — PascalCase, dot-separated:

```
Contoso.Sales.CustomerCreated
Contoso.Orders.OrderShipped
```

This mirrors the shape Azure Event Grid uses for its own system events (`Microsoft.Storage.BlobCreated`),
and it satisfies the CloudEvents specification's recommendation that `type` be prefixed with a
reverse-DNS-style name identifying the organisation that defines the event's semantics. Strict reverse DNS
(`com.contoso.sales.CustomerCreated`) is equally acceptable — pick one and keep to it. The framework's own
events use `Excalibur.<Subsystem>.<EventName>`.

Do **not** put a version segment in the name. A payload that changes shape is handled by an
[upcaster](versioning.md); a name that changes is handled by an alias. A version in the name conflates the
two and leaves you with a name per revision.

Names are validated when the attribute is constructed. A name must start and end with a letter or digit and
may otherwise contain letters, digits, and the separators `.`, `-`, `_` and `:`, up to 256 characters. That
set needs no escaping in a database column, a URL, a file name, or a broker topic.

#### Renaming: aliases

To change the name an event is known by, declare the new name and keep the old one as an alias:

```csharp
[MessageName("Contoso.Sales.CustomerCreated")]
[MessageNameAlias("Contoso.Crm.CustomerCreated")]
public sealed record CustomerCreated(Guid CustomerId, string Name) : DomainEvent;
```

`[MessageNameAlias]` is repeatable — apply it once per historical name and keep them all. It affects reading
only: events are always written under the current declared name, so the retired name stops spreading and the
store converges as new events arrive. Removing an alias makes every event still stored under that name
unreadable.

Aliases can also be registered at configuration time, which is how you attach a name you cannot put in an
attribute — such as the assembly-qualified names written by an earlier version of this framework:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .RegisterEventTypes<CustomerCreated>()
        .RegisterEventTypeAlias(
            "Contoso.Crm.Events.CustomerCreated, Contoso.Crm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            typeof(CustomerCreated))));
```

Give the stored name exactly as it appears in the store's event-type column. An alias maps a name to a type;
it does not change the event's shape, so register an upcaster as well if the payload changed too.

### Event Type Naming

The conventions below are about the CLR type's name. The name it is *stored*
under is separate and is declared with `[MessageName]` (above).

| Convention | Example | Guideline |
|------------|---------|-----------|
| Past tense | `OrderCreated`, `PaymentReceived` | Events are facts that happened |
| Specific | `OrderShippedToCustomer` | Not generic `StateChanged` |
| Domain language | `InvoiceIssued` | Match ubiquitous language |

### Rich Event Data

Include all data needed to understand what happened:

```csharp
// Good - self-contained event
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped(
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    Address ShippingAddress,
    DateTime EstimatedDelivery,
    IReadOnlyList<ShippedItem> Items) : DomainEvent;

// Bad - lacks context
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped(
    Guid OrderId,
    string TrackingNumber) : DomainEvent;
```

## Event Properties

### Standard Properties

Every domain event includes:

```csharp
public interface IDomainEvent : IDispatchEvent
{
    // Unique identifier for this event instance
    string EventId { get; }

    // When the event occurred (UTC)
    DateTimeOffset OccurredAt { get; }

    // Optional metadata for cross-cutting concerns
    IDictionary<string, object>? Metadata { get; }

    // Correlation ID for tracking a chain of related operations (read from Metadata)
    string? CorrelationId { get; }

    // Causation ID identifying the command or event that caused this event (read from Metadata)
    string? CausationId { get; }
}
```

:::info Stream identity is not on the event
An event no longer carries an `AggregateId` or a `Version`. The **aggregate id** is passed to the event store as a parameter when appending or loading, and the **stream version** is assigned by the store and surfaced on the persisted envelope (`StoredEvent.Version` / `HistoricEvent.Version`) during replay — never read from the event payload. This keeps the messaging contract free of persistence concerns.
:::

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
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItem> Items,
    DiscountApplied? Discount = null) : DomainEvent;

// Integration Event - published to other bounded contexts
// Contains only what others need to know (no base class required)
[MessageName("Contoso.Orders.OrderCreatedIntegration")]
public record OrderCreatedIntegrationEvent(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTimeOffset CreatedAt) : IIntegrationEvent;
```

### Event Transformation

Transform domain events to integration events using `IDispatcher`. Use `IMessageContextAccessor` to access the current context and `CreateChildContext()` to propagate correlation metadata:

```csharp
public class OrderCreatedPublisher : IEventHandler<OrderCreated>
{
    private readonly IDispatcher _dispatcher;
    private readonly IMessageContextAccessor _contextAccessor;

    public OrderCreatedPublisher(
        IDispatcher dispatcher,
        IMessageContextAccessor contextAccessor)
    {
        _dispatcher = dispatcher;
        _contextAccessor = contextAccessor;
    }

    public async Task HandleAsync(OrderCreated @event, CancellationToken ct)
    {
        var integrationEvent = new OrderCreatedIntegrationEvent(
            @event.OrderId,
            @event.CustomerId,
            @event.TotalAmount,
            @event.OccurredAt);

        // Called from within a handler, DispatchAsync derives a child context from
        // the current handler's message and automatically propagates:
        // - CorrelationId (for distributed tracing)
        // - CausationId (set to parent's MessageId)
        // - TenantId, UserId, SessionId, WorkflowId
        // - TraceParent/tracestate (OpenTelemetry)
        await _dispatcher.DispatchAsync(integrationEvent, ct);
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
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated : DomainEvent
{
    public Guid OrderId { get; }
    public string CustomerId { get; }
    public decimal TotalAmount { get; }

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
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated : DomainEvent
{
    public required Guid OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing());

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

### Serialized Type Name

The name an event is serialized and resolved under is the one it declares with `[MessageName]` — see
[Stable Message Names](#stable-message-names). There is no per-event property to override, and nothing is
derived from the class name.

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
