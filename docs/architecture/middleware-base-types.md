# Middleware Base Types

## 4 Base Types

| Base Type | Use When | Allocation | Stage |
|-----------|----------|------------|-------|
| `DispatchMiddlewareBase` | Standard pipeline middleware | Normal | Any |
| `ZeroAllocationMiddlewareBase` | Hot-path (>10K msg/sec) | Zero heap alloc | Any |
| `IDispatchMiddleware` (interface) | ASP.NET-like pattern, full control | Normal | Any |
| Security middleware bases | Authentication/authorization | Normal | Authentication (175) / Authorization (300) |

## Decision Tree

1. **Hot path?** -> `ZeroAllocationMiddlewareBase`
2. **Auth/authz?** -> Security middleware bases
3. **Need full control?** -> Implement `IDispatchMiddleware` directly
4. **Standard middleware?** -> `DispatchMiddlewareBase`

## Pattern

```csharp
internal sealed class MyMiddleware : DispatchMiddlewareBase
{
    public MyMiddleware() : base(DispatchMiddlewareStage.Processing) { }

    public override async Task<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchMiddlewareDelegate next,
        CancellationToken cancellationToken)
    {
        // Pre-processing
        var result = await next(message, context, cancellationToken);
        // Post-processing
        return result;
    }
}
```
