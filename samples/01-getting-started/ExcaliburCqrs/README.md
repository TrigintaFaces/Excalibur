# ExcaliburCqrs

A sample demonstrating full CQRS/Event Sourcing patterns using Excalibur and Excalibur.Dispatch.

## Purpose

This sample shows how to build event-sourced aggregates using the Excalibur framework. It progresses from the pure Dispatch patterns shown in `DispatchMinimal` to full domain modeling with aggregates, events, and repositories.

## Running the Sample

```bash
dotnet run --project samples/ExcaliburCqrs/ExcaliburCqrs.csproj
```

## What This Sample Demonstrates

### Event Sourcing Patterns

- **Aggregate Roots** - `AggregateRoot<TKey>` base class for event-sourced entities
- **Domain Events** - `DomainEvent` base record implementing `IDomainEvent`
- **Event Application** - `ApplyEventInternal()` with pattern matching (switch expressions)
- **Event Store** - In-memory event store for development/testing
- **Repository Pattern** - `IEventSourcedRepository<TAggregate, TKey>` for loading/saving

### Order Lifecycle

The sample demonstrates an order aggregate with these state transitions:

```
Created --> Confirmed --> Shipped
   |
   +--> Add Items (while in Created status)
```

Business invariants:
- Items can only be added to orders in `Created` status
- Orders can only be confirmed if they have at least one item
- Orders can only be shipped if they are `Confirmed`

## Project Structure

```
ExcaliburCqrs/
├── Domain/
│   ├── Aggregates/
│   │   └── OrderAggregate.cs    # Event-sourced aggregate root
│   └── Events/
│       └── OrderEvents.cs       # Domain events (OrderCreated, etc.)
├── Messages/
│   └── OrderCommands.cs         # Commands (IDispatchAction) and queries
├── Handlers/
│   └── CreateOrderHandler.cs    # Command handlers using repository
├── Program.cs                   # Entry point
└── README.md
```

## Key Code Examples

### Defining Domain Events

```csharp
public sealed record OrderCreated : DomainEvent
{
    public OrderCreated(Guid orderId, string productId, int quantity, long version)
        : base(orderId.ToString(), version)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid OrderId { get; init; }
    public string ProductId { get; init; }
    public int Quantity { get; init; }
}
```

### Creating an Aggregate Root

```csharp
public class OrderAggregate : AggregateRoot<Guid>
{
    public OrderStatus Status { get; private set; }

    public static OrderAggregate Create(Guid id, string productId, int quantity)
    {
        var order = new OrderAggregate(id);
        order.RaiseEvent(new OrderCreated(id, productId, quantity, order.Version));
        return order;
    }

    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        OrderCreated e => ApplyOrderCreated(e),
        OrderItemAdded e => ApplyOrderItemAdded(e),
        OrderConfirmed e => ApplyOrderConfirmed(e),
        OrderShipped e => ApplyOrderShipped(e),
        _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
    };

    private bool ApplyOrderCreated(OrderCreated e)
    {
        Id = e.OrderId;
        Status = OrderStatus.Created;
        return true;
    }
}
```

### Command Handler with Repository

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
    private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

    public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), action.ProductId, action.Quantity);
        await _repository.SaveAsync(order, cancellationToken);
        return order.Id;
    }
}
```

### Service Registration

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});

services.AddInMemoryEventStore();
```

## Comparison with DispatchMinimal

| Concept | DispatchMinimal | ExcaliburCqrs |
|---------|-----------------|---------------|
| Commands | `IDispatchAction` | Same |
| Events | `IDispatchEvent` | `DomainEvent : IDomainEvent` |
| State | External (your DB) | In aggregate (event-sourced) |
| Persistence | Your choice | `IEventSourcedRepository` + event store |
| Handler Pattern | Direct action | Load aggregate → Execute → Save |

## Dependencies

This sample uses:

- `Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Abstractions` - Message types and `IDomainEvent`
- `Excalibur.Domain` - `AggregateRoot<T>` base class
- `Excalibur.EventSourcing` - Repository and event store abstractions
- `Excalibur.EventSourcing.InMemory` - In-memory event store

## Known Issues

The InMemoryEventStore currently has a type resolution issue when loading events:
- Events are stored with simple type names (e.g., "OrderCreated")
- The serializer expects assembly-qualified type names for deserialization
- This causes events to fail silently during replay

This will be addressed in a future sprint. The sample still demonstrates the correct patterns for:
- Aggregate creation with `RaiseEvent()`
- Event application with `ApplyEventInternal()`
- Handler patterns with repository injection
- Business invariant enforcement

## Production Considerations

The in-memory event store is for development/testing only. For production, use:

- `Excalibur.EventSourcing.SqlServer` - SQL Server event store
- `Excalibur.EventSourcing.CosmosDb` - Azure Cosmos DB event store
- `Excalibur.EventSourcing.DynamoDb` - AWS DynamoDB event store

See the respective package documentation for setup instructions.

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
