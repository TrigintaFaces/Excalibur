---
sidebar_position: 7
title: Event Versioning
description: Handle schema evolution and event upgrades
---

# Event Versioning

As your system evolves, event schemas change. Event versioning enables graceful schema evolution without breaking existing data.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  ```
- Familiarity with [domain events](./domain-events.md) and [event stores](./event-store.md)

## Why Version Events?

Events are immutable and stored forever. When business requirements change:

- New fields need to be added
- Existing fields may need different types
- Fields may become required or optional
- Event structures may need to split or merge

## Versioning Strategies

### Weak Schema (Recommended)

Use flexible serialization that ignores unknown properties:

```csharp
// V1 - Original event
public record OrderCreated(Guid OrderId, string CustomerId, decimal TotalAmount) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

// V2 - Added field (backward compatible)
public record OrderCreatedV2(Guid OrderId, string CustomerId, decimal TotalAmount, string Currency = "USD") : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

// Configure serializer to handle schema evolution
services.AddJsonSerialization(options =>
{
    // Ignore unknown properties when deserializing
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
});
```

### Strong Schema (Message Upcasters)

Transform old events to new schema during loading using the upcasting pipeline:

```csharp
// V1 event (stored in database)
public record OrderCreatedV1 : DomainEventBase, IVersionedMessage
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public override string AggregateId => OrderId.ToString();

    // IVersionedMessage implementation
    int IVersionedMessage.Version => 1;
    public string MessageType => "OrderCreated";  // Same across all versions
}

// V2 event (current schema)
public record OrderCreated : DomainEventBase, IVersionedMessage
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public Money Total { get; init; } = default!;
    public override string AggregateId => OrderId.ToString();

    // IVersionedMessage implementation
    int IVersionedMessage.Version => 2;
    public string MessageType => "OrderCreated";  // Same across all versions
}

// Upcaster transforms V1 to V2
public class OrderCreatedV1ToV2 : IMessageUpcaster<OrderCreatedV1, OrderCreated>
{
    // Required: Specify version transition
    public int FromVersion => 1;
    public int ToVersion => 2;

    public OrderCreated Upcast(OrderCreatedV1 source)
    {
        return new OrderCreated
        {
            OrderId = source.OrderId,
            CustomerId = source.CustomerId,
            Total = Money.USD(source.TotalAmount),
            AggregateId = source.AggregateId,
            Version = source.Version,
            EventId = source.EventId,
            OccurredAt = source.OccurredAt
        };
    }
}
```

## Upcasting Pipeline Registration

Register upcasters using the builder pattern:

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    // Register upcasters via the pipeline builder
    builder.AddUpcastingPipeline(upcasting =>
    {
        // Register individual upcasters
        upcasting.RegisterUpcaster<OrderCreatedV1, OrderCreated>(new OrderCreatedV1ToV2());

        // Or scan assemblies for all upcasters
        upcasting.ScanAssembly(typeof(Program).Assembly);

        // Enable automatic upcasting during event replay
        upcasting.EnableAutoUpcastOnReplay();
    });
});
```

Event types use the `EventType` property for serialization, which defaults to the class name. Override it in `DomainEventBase`-derived records for custom type names:

```csharp
public record OrderCreated : DomainEventBase, IVersionedMessage
{
    // Override the virtual EventType property for custom serialization name
    public override string EventType => "order.created.v2";

    // IVersionedMessage implementation
    int IVersionedMessage.Version => 2;
    public string MessageType => "OrderCreated";
    // ...
}
```

## Core Interfaces

### IVersionedMessage

All versioned events must implement this interface:

```csharp
public interface IVersionedMessage
{
    /// <summary>
    /// Schema version (start at 1, increment for breaking changes).
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Logical message type name (constant across all versions).
    /// Example: "OrderCreated" for OrderCreatedV1, OrderCreatedV2, etc.
    /// </summary>
    string MessageType { get; }
}
```

### IMessageUpcaster

Transform old events to new schema:

```csharp
public interface IMessageUpcaster<in TOld, out TNew>
    where TOld : IDispatchMessage, IVersionedMessage
    where TNew : IDispatchMessage, IVersionedMessage
{
    int FromVersion { get; }
    int ToVersion { get; }
    TNew Upcast(TOld oldMessage);
}
```

## Common Evolution Patterns

### Adding Fields

```csharp
// Safe - add optional fields with defaults
public record OrderCreated(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string? Currency = "USD",           // New optional field
    DateTime? EstimatedDelivery = null  // New optional field
) : DomainEventBase;
```

### Renaming Fields

```csharp
// Use JSON property name for backward compatibility
public record OrderCreated(
    Guid OrderId,
    [property: JsonPropertyName("customerId")]
    string BuyerId,  // Renamed from CustomerId
    decimal TotalAmount
) : DomainEventBase;
```

### Changing Types

```csharp
// V1: decimal
public record OrderCreatedV1 : DomainEventBase, IVersionedMessage
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public override string AggregateId => OrderId.ToString();

    int IVersionedMessage.Version => 1;
    public string MessageType => "OrderCreated";
}

// V2: Money value object
public record OrderCreated : DomainEventBase, IVersionedMessage
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public Money Total { get; init; } = default!;
    public override string AggregateId => OrderId.ToString();

    int IVersionedMessage.Version => 2;
    public string MessageType => "OrderCreated";
}

// Upcaster
public class OrderCreatedUpcaster : IMessageUpcaster<OrderCreatedV1, OrderCreated>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public OrderCreated Upcast(OrderCreatedV1 source)
    {
        return new OrderCreated
        {
            OrderId = source.OrderId,
            CustomerId = source.CustomerId,
            Total = Money.USD(source.TotalAmount),
            AggregateId = source.AggregateId,
            Version = source.Version,
            EventId = source.EventId,
            OccurredAt = source.OccurredAt
        };
    }
}
```

### Splitting Events

When a single event needs to become multiple events, handle this in your aggregate's event application or use a custom upcaster that returns multiple events:

```csharp
// V1: Single event with multiple concerns
public record OrderProcessedV1 : DomainEventBase, IVersionedMessage
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ShippedAt { get; init; }
    public string? TrackingNumber { get; init; }
    public override string AggregateId => OrderId.ToString();

    int IVersionedMessage.Version => 1;
    public string MessageType => "OrderProcessed";
}

// V2: Split into focused events
public record OrderStatusChanged(Guid OrderId, string Status) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderShipped(Guid OrderId, string TrackingNumber, DateTime ShippedAt) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

// Handle V1 events in aggregate by treating as multiple logical events
protected override void ApplyEventInternal(IDomainEvent @event)
{
    switch (@event)
    {
        case OrderProcessedV1 e:
            // Apply status change
            Status = e.Status;
            // Apply shipping if present
            if (e.ShippedAt.HasValue)
            {
                TrackingNumber = e.TrackingNumber;
                ShippedAt = e.ShippedAt;
            }
            break;
        case OrderStatusChanged e:
            Status = e.Status;
            break;
        case OrderShipped e:
            TrackingNumber = e.TrackingNumber;
            ShippedAt = e.ShippedAt;
            break;
    }
}
```

### Merging Events

When separate events should become a single event, the aggregate handles both old and new patterns:

```csharp
// V1: Separate events
public record AddressChangedV1(Guid OrderId, Address NewAddress) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

public record ContactChangedV1(Guid OrderId, string Email, string Phone) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

// V2: Combined event
public record CustomerDetailsUpdated(
    Guid OrderId,
    Address? NewAddress = null,
    string? Email = null,
    string? Phone = null) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}
```

## Upcaster Pipeline

Multiple upcasters chain automatically:

```csharp
// Event evolution: V1 -> V2 -> V3
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddUpcastingPipeline(upcasting =>
    {
        upcasting.RegisterUpcaster<OrderCreatedV1, OrderCreatedV2>(new V1ToV2Upcaster());  // 1 -> 2
        upcasting.RegisterUpcaster<OrderCreatedV2, OrderCreated>(new V2ToV3Upcaster());   // 2 -> 3
    });
});

// Loading V1 event automatically upgrades through chain:
// V1 -> V2 -> V3 (current)
```

## Aggregate Compatibility

Update aggregate to handle all versions:

```csharp
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; } = string.Empty;
    public Money Total { get; private set; } = default!;

    private Order() { }

    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            // Handle current version
            case OrderCreated e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                Total = e.Total;
                break;

            // Handle legacy version (if upcaster not configured)
            case OrderCreatedV1 e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                Total = Money.USD(e.TotalAmount);
                break;
        }
    }
}
```

## Testing Event Versioning

```csharp
public class EventVersioningTests
{
    [Fact]
    public void Upcaster_Transforms_V1_To_V2()
    {
        // Arrange
        var aggregateId = Guid.NewGuid().ToString();
        var v1Event = new OrderCreatedV1(aggregateId, version: 1)
        {
            OrderId = Guid.Parse(aggregateId),
            CustomerId = "customer-1",
            TotalAmount = 100m
        };
        var upcaster = new OrderCreatedUpcaster();

        // Act
        var v2Event = upcaster.Upcast(v1Event);

        // Assert
        v2Event.OrderId.Should().Be(v1Event.OrderId);
        v2Event.CustomerId.Should().Be(v1Event.CustomerId);
        v2Event.Total.Amount.Should().Be(100m);
        v2Event.Total.ISOCurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task Aggregate_Loads_From_Mixed_Versions()
    {
        // Arrange - use DI to get configured repository with upcasting
        var services = new ServiceCollection();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.UseEventStore<InMemoryEventStore>();
            builder.AddRepository<Order, Guid>();
            builder.AddUpcastingPipeline(upcasting =>
            {
                upcasting.RegisterUpcaster<OrderCreatedV1, OrderCreated>(
                    new OrderCreatedUpcaster());
                upcasting.EnableAutoUpcastOnReplay();
            });
        });

        var sp = services.BuildServiceProvider();
        var repository = sp.GetRequiredService<IEventSourcedRepository<Order, Guid>>();

        // Create order with V1 event (simulating legacy data)
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, "customer-1");
        await repository.SaveAsync(order, CancellationToken.None);

        // Act - Load triggers automatic upcasting
        var loaded = await repository.GetByIdAsync(orderId, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.CustomerId.Should().Be("customer-1");
    }
}
```

## Best Practices

### Do

- Use optional fields with defaults for new properties
- Keep upgraders simple and stateless
- Test upgraders thoroughly
- Document breaking changes
- Version event type names explicitly

### Don't

- Remove fields from events
- Change the meaning of existing fields
- Make optional fields required
- Store upgrader state
- Skip versions in upgrade chain

## Migration Checklist

When evolving an event schema:

1. [ ] Create new event type implementing `IVersionedMessage`:
   - `int Version` property (increment from previous)
   - `string MessageType` property (same as previous version)
2. [ ] Implement upcaster from previous version (`IMessageUpcaster<TOld, TNew>`):
   - `int FromVersion` property
   - `int ToVersion` property
   - `TNew Upcast(TOld)` method
3. [ ] Register upcaster via `AddUpcastingPipeline`
4. [ ] Update aggregate's `ApplyEventInternal`
5. [ ] Update projections to handle both versions
6. [ ] Test with mixed-version event streams
7. [ ] Deploy to staging and verify
8. [ ] Monitor upcasting performance in production

## Next Steps

- [Domain Events](domain-events.md) — Define event schemas
- [Event Store](event-store.md) — Understand event persistence
- [Projections](projections.md) — Update projections for schema changes

## See Also

- [Version Upgrades](../migration/version-upgrades.md) — Guide for upgrading across framework versions
- [Projections](./projections.md) — How projections handle versioned events and schema changes
- [Migrations](./migrations.md) — Database migration strategies for event store schema changes
- [Serialization](../middleware/serialization.md) — Serialization configuration that underpins event versioning
