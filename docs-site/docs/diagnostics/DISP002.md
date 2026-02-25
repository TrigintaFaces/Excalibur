# DISP002: Missing AutoRegister Attribute

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP002 |
| **Title** | Consider adding [AutoRegister] attribute |
| **Category** | Excalibur.Dispatch.Handlers |
| **Severity** | Info |
| **Enabled by default** | Yes |

## Cause

A handler class could benefit from the `[AutoRegister]` attribute to enable automatic discovery and registration by the source generator, reducing manual DI configuration.

## Example

The following code triggers DISP002:

```csharp
// Info DISP002: Handler 'CreateOrderHandler' could benefit from [AutoRegister]
// attribute for automatic registration
public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
    {
        // ...
        return Task.CompletedTask;
    }
}
```

## How to Fix

Add the `[AutoRegister]` attribute to enable automatic discovery:

```csharp
[AutoRegister]
public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
    {
        // ...
        return Task.CompletedTask;
    }
}
```

You can customize the handler's service lifetime:

```csharp
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class StatelessHandler : IActionHandler<PingCommand>
{
    public Task HandleAsync(PingCommand action, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

## When to Suppress

Suppress this diagnostic if you prefer explicit manual registration or have a custom handler registration strategy.

## See Also

- [DISP001: Handler Not Discoverable](./DISP001.md)
- [Source Generators Getting Started](../source-generators/getting-started.md)
