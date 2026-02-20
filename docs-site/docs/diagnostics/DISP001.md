# DISP001: Handler Not Discoverable

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP001 |
| **Title** | Handler may not be discoverable |
| **Category** | Excalibur.Dispatch.Handlers |
| **Severity** | Warning |
| **Enabled by default** | Yes |

## Cause

A class implements `IDispatchHandler<T>` but may not be discovered by the source generators. This typically happens when:

- The handler is missing the `[AutoRegister]` attribute
- The handler is not in an assembly that is scanned during source generation

## Example

The following code triggers DISP001:

```csharp
// Warning DISP001: Handler 'CreateOrderHandler' implements IActionHandler<CreateOrder>
// but may not be discovered by source generators
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

**Option 1 (Recommended):** Add the `[AutoRegister]` attribute:

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

**Option 2:** Register the handler manually in DI:

```csharp
services.AddTransient<IActionHandler<CreateOrder>, CreateOrderHandler>();
```

## When to Suppress

Suppress this warning if you intentionally register handlers manually and do not use the source generator for discovery:

```csharp
#pragma warning disable DISP001
public class CreateOrderHandler : IActionHandler<CreateOrder>
#pragma warning restore DISP001
```

## See Also

- [DISP002: Missing AutoRegister Attribute](./DISP002.md)
- [Source Generators Getting Started](../source-generators/getting-started.md)
