# DISP006: Message Type Missing Dispatch Interface

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP006 |
| **Title** | Message type should implement a dispatch interface |
| **Category** | Excalibur.Dispatch.Messaging.Handlers |
| **Severity** | Warning |
| **Enabled by default** | Yes |

## Cause

A type was used as a type argument to `IDispatcher.DispatchAsync<T>()` but does not implement any of the dispatch marker interfaces (`IDispatchAction<TResponse>`, `IDispatchEvent`, or `IDispatchMessage`). The dispatcher uses these interfaces for routing decisions -- without them, the message may not be delivered correctly.

## Examples

### Non-compliant

```csharp
// Warning DISP006: Type 'PlainObject' does not implement IDispatchAction<T>,
// IDispatchEvent, or IDispatchMessage
public class PlainObject { public string Name { get; set; } }

// Usage:
await dispatcher.DispatchAsync(new PlainObject(), context, ct);
```

### Compliant

```csharp
// Implements IDispatchAction<string> -- dispatcher knows this is a request-response action
public sealed record CreateOrderCommand : IDispatchAction<OrderId>
{
    public string ProductName { get; init; } = string.Empty;
}

// Implements IDispatchEvent -- dispatcher knows this is a multi-handler event
public sealed record OrderCreatedEvent(Guid OrderId) : IDispatchEvent;
```

## How to Fix

Add the appropriate dispatch interface to your message type:

| Scenario | Interface | Handler Type |
|----------|-----------|-------------|
| Request-response (returns a value) | `IDispatchAction<TResponse>` | `IActionHandler<TAction, TResponse>` |
| Fire-and-forget command | `IDispatchAction<Unit>` | `IActionHandler<TAction>` |
| Event (multiple handlers) | `IDispatchEvent` | `IEventHandler<TEvent>` |
| Domain event | `IDomainEvent` | `IEventHandler<TEvent>` |

## When to Suppress

Suppress this diagnostic when dispatching a dynamic or generic message type where the interface constraint is enforced by the calling infrastructure.
