# InteractiveDemo

An interactive console demo for quickly exploring Dispatch event handling.

## Purpose

This sample provides a hands-on way to see Dispatch in action. Press any key to simulate order events and watch them flow through handlers.

## What This Sample Demonstrates

- **Event Definitions** - Creating `IDispatchEvent` implementations
- **Event Handlers** - Processing events with `IEventHandler<T>`
- **Handler Discovery** - Automatic handler registration
- **Interactive Flow** - Real-time event dispatching

## Running the Sample

```bash
dotnet run --project samples/01-getting-started/InteractiveDemo
```

Then press any key to simulate orders. Press `ESC` to exit.

## Sample Output

```
🚀 Excalibur Quick Demo

Press any key to simulate orders (ESC to exit):

📦 Order placed: a1b2c3d4-... for $456
🚚 Order shipped: a1b2c3d4-... at 14:32:15
📦 Order placed: e5f6g7h8-... for $123
🚚 Order shipped: e5f6g7h8-... at 14:32:18

👋 Shutting down...
```

## Project Structure

```
InteractiveDemo/
├── InteractiveDemo.csproj    # Project file
├── Program.cs          # Events, handlers, and demo logic
└── README.md           # This file
```

## Key Concepts

### Event Definition

Events implement `IDispatchEvent` and carry data about something that happened:

```csharp
public record OrderPlacedEvent(Guid OrderId, decimal Amount, DateTimeOffset EventTimestamp)
    : IDispatchEvent;
```

### Event Handlers

Handlers implement `IEventHandler<TEvent>` and react to events:

```csharp
public class OrderHandler : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent eventMessage, CancellationToken ct)
    {
        Console.WriteLine($"📦 Order placed: {eventMessage.OrderId}");
        return Task.CompletedTask;
    }
}
```

### Handler Registration

Handlers are discovered automatically:

```csharp
services.AddDispatch(dispatch =>
{
    _ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

## Dependencies

- `Dispatch` - Core messaging
- `Excalibur.Dispatch.Abstractions` - Event interfaces

## Next Steps

- [WebApiQuickStart](../WebApiQuickStart/) - Full ASP.NET Core API with commands and queries
- [DispatchOnly](../DispatchOnly/) - Detailed breakdown of message types

---

*Category: Getting Started | Sprint 607*
