# DISP004: Optimization Hint

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP004 |
| **Title** | Optimization hint |
| **Category** | Excalibur.Dispatch.Performance |
| **Severity** | Info |
| **Enabled by default** | Yes |

## Cause

The analyzer detected a potential performance improvement. Common suggestions include:

- **Seal the class** - Handler classes that are not inherited can be `sealed`, enabling devirtualization
- **Use `ValueTask`** - Methods returning `Task` that complete synchronously could return `ValueTask` to avoid allocation
- **Use `readonly struct`** - Value types used as messages could be `readonly struct` to avoid defensive copies
- **Register as singleton** - Stateless handlers can be registered as singletons to avoid per-request allocation

## Examples

### Unsealed handler class

```csharp
// Info DISP004: Consider sealing 'CreateOrderHandler' for better performance
public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

Fix:

```csharp
public sealed class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

### Synchronous handler returning Task

```csharp
// Info DISP004: Consider returning ValueTask instead of Task
public sealed class PingHandler : IActionHandler<Ping>
{
    public Task HandleAsync(Ping action, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

Fix:

```csharp
public sealed class PingHandler : IActionHandler<Ping>
{
    public ValueTask HandleAsync(Ping action, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
```

## When to Suppress

Suppress this diagnostic if the optimization does not apply to your scenario or if readability is preferred over the minor performance gain.

## See Also

- [DISP003: Reflection Without AOT Annotation](./DISP003.md)
- [Performance Guide](../performance/competitor-comparison.md)
