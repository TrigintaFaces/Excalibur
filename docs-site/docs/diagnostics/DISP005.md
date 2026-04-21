# DISP005: Handler Should Be Sealed

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP005 |
| **Title** | Handler should be sealed |
| **Category** | Excalibur.Dispatch.Messaging.Handlers |
| **Severity** | Warning |
| **Enabled by default** | Yes |

## Cause

A concrete handler class implements a dispatch handler interface but is not declared `sealed`. Sealing handler classes enables JIT devirtualization, reduces vtable overhead, and prevents accidental inheritance of framework handler types.

## Examples

### Non-compliant

```csharp
// Warning DISP005: Handler 'CreateOrderHandler' should be declared 'sealed'
public class CreateOrderHandler : IActionHandler<CreateOrder, OrderId>
{
    public Task<OrderId> HandleAsync(CreateOrder action, CancellationToken cancellationToken)
        => Task.FromResult(new OrderId(Guid.NewGuid()));
}
```

### Compliant

```csharp
public sealed class CreateOrderHandler : IActionHandler<CreateOrder, OrderId>
{
    public Task<OrderId> HandleAsync(CreateOrder action, CancellationToken cancellationToken)
        => Task.FromResult(new OrderId(Guid.NewGuid()));
}
```

## How to Fix

Add the `sealed` modifier to the handler class declaration. If the class is intentionally designed for inheritance (base handler pattern), suppress the diagnostic:

```csharp
#pragma warning disable DISP005
public abstract class BaseOrderHandler : IActionHandler<CreateOrder, OrderId>
#pragma warning restore DISP005
```

## When to Suppress

Suppress this diagnostic when:
- The handler class is an abstract base class designed for inheritance
- You have a documented pattern requiring handler polymorphism
