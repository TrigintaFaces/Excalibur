# DispatchContextInitializer DX Guide

## When to Use

`DispatchContextInitializer` is used to set up ambient context for top-level dispatch calls outside of the pipeline (e.g., from ASP.NET controllers or background services).

**For most scenarios, use the convenience extension method instead:**
```csharp
// Preferred: auto-creates context
await dispatcher.DispatchAsync(message, cancellationToken);
```

**Use DispatchContextInitializer when:**
- You need to set specific context properties (TenantId, UserId) before dispatch
- You're calling from a non-DI context where ambient context isn't available
- You need explicit control over context lifecycle

**Do NOT use when:**
- The convenience `DispatchAsync<T>(message, ct)` overload suffices
- You're dispatching from within a handler (ambient context already exists)
