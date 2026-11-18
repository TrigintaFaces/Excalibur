---
title: DISP004 — Optimization hint
description: "The analyzer suggests a potential optimization; verify contract compatibility and measure its effect."
---

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
- **Use `ValueTask`** - Consider it when the handler contract permits it and measurements identify an allocating baseline. Returning `Task.CompletedTask` already reuses a completed task.
- **Use `readonly struct`** - Value types used as messages could be `readonly struct` to avoid defensive copies
- **Register as singleton** - Consider it only when dependency lifetimes, concurrent use and disposal semantics permit it; measure whether construction allocations matter.

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

The following example returns `Task.CompletedTask`, so it does not demonstrate a per-call task allocation that switching to `ValueTask` removes. The return type must match the implemented handler contract; this illustration is not a verified performance improvement.

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

Suppress this diagnostic when the suggestion does not fit the required behavior or a representative measurement does not justify the change.

## See Also

- [DISP003: Reflection Without AOT Annotation](./DISP003.md)
- [Performance Guide](../performance/competitor-comparison.md)
