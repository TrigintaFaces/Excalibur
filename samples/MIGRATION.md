# Migration Guide: Dispatch to Excalibur

This guide explains when and how to evolve from pure Dispatch messaging to the full Excalibur CQRS/ES framework.

## Understanding the Separation

### Dispatch (Messaging Framework)
- Message dispatching and routing
- Handler pipelines and middleware
- Serialization and transport abstractions
- `IDispatchAction`, `IDispatchEvent`, `IDispatchDocument`

### Excalibur (Application Framework)
- Domain modeling with aggregates and entities
- Event sourcing and event stores
- Read model projections
- Saga/Process managers
- Repository patterns

## When to Use Each

### Start with Dispatch When:
- Building a simple CQRS application without event sourcing
- Replacing MediatR with a more feature-rich alternative
- You need messaging patterns but not persistence patterns
- Building microservices that communicate via messages

### Upgrade to Excalibur When:
- You need aggregate roots with domain invariants
- You want event sourcing for audit trail or time-travel debugging
- You need read model projections from event streams
- You're building complex domain logic with sagas

## Migration Path

### Step 1: Pure Dispatch (DispatchMinimal pattern)

```csharp
// Simple command with no aggregate
public record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction<Guid>;

// Handler creates order directly
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
    public Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        // Direct persistence or API call
        return Task.FromResult(orderId);
    }
}
```

### Step 2: Add Domain Events (Optional intermediate step)

```csharp
// Domain event for order creation
public record OrderCreatedEvent(Guid OrderId, string ProductId, int Quantity) : IDispatchEvent;

// Handler that publishes events
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
    private readonly IDispatcher _dispatcher;

    public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        // Persist order...

        // Publish event
        await _dispatcher.DispatchAsync(
            new OrderCreatedEvent(orderId, action.ProductId, action.Quantity),
            context, ct);

        return orderId;
    }
}
```

### Step 3: Full Excalibur with Aggregates

```csharp
// Domain event as part of aggregate
public record OrderCreatedEvent(
    Guid OrderId,
    string ProductId,
    int Quantity,
    DateTimeOffset CreatedAt) : IDomainEvent
{
    public Guid Id => OrderId;
    public Guid AggregateId => OrderId;
    public int Version { get; init; }
    public DateTimeOffset OccurredAt => CreatedAt;
    public string EventType => nameof(OrderCreatedEvent);
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();
}

// Aggregate root with event sourcing
public class Order : AggregateRoot
{
    public string ProductId { get; private set; }
    public int Quantity { get; private set; }
    public OrderStatus Status { get; private set; }

    public Order(Guid id, string productId, int quantity) : base(id)
    {
        RaiseEvent(new OrderCreatedEvent(id, productId, quantity, DateTimeOffset.UtcNow));
    }

    protected override void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                ProductId = e.ProductId;
                Quantity = e.Quantity;
                Status = OrderStatus.Created;
                break;
        }
    }
}

// Command handler using repository
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken ct)
    {
        var order = new Order(Guid.NewGuid(), action.ProductId, action.Quantity);
        await _repository.SaveAsync(order, ct);
        return order.Id;
    }
}
```

## Package Reference Changes

### Dispatch Only (DispatchMinimal)
```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>
```

### Full Excalibur
```xml
<ItemGroup>
  <!-- Dispatch core -->
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />

  <!-- Excalibur domain -->
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.Domain\Excalibur.Domain.csproj" />

  <!-- Event sourcing -->
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing\Excalibur.EventSourcing.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing.SqlServer\Excalibur.EventSourcing.SqlServer.csproj" />
</ItemGroup>
```

## Handler Interface Changes

| Dispatch Pattern | Excalibur Pattern | Use Case |
|-----------------|-------------------|----------|
| `IActionHandler<T>` | Same | Commands without return |
| `IActionHandler<T, TResult>` | Same | Commands with return |
| `IEventHandler<T>` | Same + projections | Event subscribers |
| `IDocumentHandler<T>` | Query handlers | Read operations |

## Key Concepts Comparison

| Concept | Dispatch | Excalibur |
|---------|----------|-----------|
| Commands | `IDispatchAction` | `IDispatchAction` |
| Events | `IDispatchEvent` | `IDomainEvent` (richer) |
| Queries | `IDispatchDocument` | Query handlers |
| Persistence | Your choice | Event store + snapshots |
| State management | External | Aggregate roots |
| Read models | Manual | Projections |

## Sample Applications

- **DispatchMinimal** - Pure Dispatch messaging patterns
- **ExcaliburCqrs** - Full CQRS/ES with aggregates and event sourcing

## Common Migration Questions

**Q: Can I mix Dispatch and Excalibur in the same project?**
A: Yes! Excalibur builds on top of Excalibur.Dispatch. You can have some handlers using pure Dispatch patterns while others use full aggregate/event sourcing.

**Q: Do I need to migrate all at once?**
A: No. Start with aggregates that need the richest behavior (audit trails, complex invariants) and leave simpler operations as pure Dispatch handlers.

**Q: What about existing events?**
A: `IDomainEvent` extends `IDispatchEvent`, so existing events can be upgraded by implementing the additional properties.
