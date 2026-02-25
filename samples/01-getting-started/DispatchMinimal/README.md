# DispatchMinimal

A minimal sample demonstrating pure Dispatch messaging patterns without any Excalibur dependencies.

## Purpose

This sample shows how to use Dispatch as a lightweight MediatR alternative for simple command/query/event scenarios. Use this when you don't need aggregate roots, event sourcing, or other CQRS/ES patterns.

## Running the Sample

```bash
dotnet run --project samples/DispatchMinimal/DispatchMinimal.csproj
```

## What This Sample Demonstrates

### Message Types

- **IDispatchAction** (Commands) - Represent intent to change state
- **IDispatchEvent** (Events) - Notify multiple handlers that something happened
- **IDispatchDocument** (Queries) - Request data without changing state

### Patterns

1. **Handler Registration** - Using `AddDispatch(assembly)` for auto-discovery
2. **Message Dispatching** - Using `IDispatcher.DispatchAsync()`
3. **Custom Middleware** - Logging middleware showing pipeline interception
4. **Multiple Event Handlers** - Same event handled by different handlers

## Project Structure

```
DispatchMinimal/
├── Messages/
│   ├── CreateOrderCommand.cs   # IDispatchAction
│   ├── OrderCreatedEvent.cs    # IDispatchEvent
│   └── GetOrderQuery.cs        # IDispatchDocument
├── Handlers/
│   ├── CreateOrderHandler.cs   # Command handler
│   ├── OrderCreatedHandler.cs  # Event handlers (2)
│   └── GetOrderHandler.cs      # Query handler
├── Middleware/
│   └── LoggingMiddleware.cs    # Custom pipeline middleware
├── Program.cs                  # Entry point
└── README.md
```

## Key Code Examples

### Defining a Command

```csharp
public record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction;
```

### Creating Handlers

**Command Handler (returns a value):**

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
    public Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        return Task.FromResult(orderId);
    }
}
```

**Event Handler:**

```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order created: {eventMessage.OrderId}");
        return Task.CompletedTask;
    }
}
```

**Document Handler:**

```csharp
public class GetOrderHandler : IDocumentHandler<GetOrderQuery>
{
    public Task HandleAsync(GetOrderQuery document, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Processing document for order: {document.OrderId}");
        return Task.CompletedTask;
    }
}
```

### Dispatching Messages

```csharp
var dispatcher = provider.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext(provider);

// Command with return value
var result = await dispatcher.DispatchAsync<CreateOrderCommand, Guid>(
    new CreateOrderCommand("WIDGET-123", 5), context, CancellationToken.None);
var orderId = result.ReturnValue;

// Event (fire and forget)
await dispatcher.DispatchAsync(new OrderCreatedEvent(orderId, "WIDGET-123", 5), context, CancellationToken.None);

// Document query
await dispatcher.DispatchAsync(new GetOrderQuery(orderId), context, CancellationToken.None);
```

## When to Upgrade

Consider upgrading to Excalibur when you need:

- Aggregate roots with domain events
- Event sourcing and event store
- Projections and read models
- Complex domain invariants

See [samples/MIGRATION.md](../../MIGRATION.md) for upgrade guidance.

## Dependencies

This sample has **NO Excalibur dependencies** - only:

- `Dispatch`
- `Excalibur.Dispatch.Abstractions`

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.

