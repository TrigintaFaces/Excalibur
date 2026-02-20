---
sidebar_position: 3
title: Auto-Freeze
description: Automatic cache optimization for production performance
---

# Auto-Freeze

Dispatch automatically optimizes internal caches on application startup using `FrozenDictionary`. This provides lock-free, high-performance lookups in production without any configuration.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Auto-freeze is enabled by default — no additional setup required

## How It Works

When your application starts, Dispatch listens for `IHostApplicationLifetime.ApplicationStarted` and freezes all internal caches:

```csharp
// Automatic - happens on startup (default behavior)
var host = builder.Build();
await host.RunAsync();

// Caches freeze automatically when ApplicationStarted fires
// Zero configuration required!
```

### What Gets Frozen

| Cache | Purpose | Benefit |
|-------|---------|---------|
| Handler invocation | Compiled handler delegates | Lock-free lookups |
| Handler registry | Manual handler registrations | No synchronization overhead |
| Handler activation | Handler context setup | Faster activation |
| Result factory | Message result creation | Optimized result creation |
| Middleware evaluation | Middleware applicability metadata | Faster middleware filtering |

### Performance Impact

| Metric | Before Freeze | After Freeze | Improvement |
|--------|---------------|--------------|-------------|
| Handler lookup | ~50 ns | ~5 ns | **10x faster** |
| Memory overhead | Synchronization locks | None | Reduced GC pressure |
| CPU overhead | Lock contention possible | Lock-free | Better scalability |

## Configuration

### Default Behavior (Recommended)

Auto-freeze is enabled by default. No configuration needed:

```csharp
builder.Services.AddDispatch();
```

### Opt-out for Development

If you need to register handlers at runtime (rare), disable auto-freeze:

```csharp
builder.Services.Configure<DispatchOptions>(options =>
{
    options.Performance.AutoFreezeOnStart = false;
});
```

:::warning Runtime Registration
Disabling auto-freeze means caches remain mutable, using `ConcurrentDictionary` with synchronization overhead. Only disable if you have a specific need for runtime handler registration.
:::

### Hot Reload Detection

Auto-freeze is automatically disabled when hot reload is detected:

- `dotnet watch` sets `DOTNET_WATCH=1`
- Edit & Continue sets `DOTNET_MODIFIABLE_ASSEMBLIES=debug`

This ensures handler discovery works correctly during development without any configuration.

## Manual Cache Control

For advanced scenarios, use `IDispatchCacheManager` directly:

```csharp
public class WarmupService : IHostedService
{
    private readonly IDispatchCacheManager _cacheManager;

    public WarmupService(IDispatchCacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Optionally trigger freeze after custom warmup
        _cacheManager.FreezeAll();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

## Cache Status Diagnostics

Check the freeze status programmatically:

```csharp
public class DiagnosticsController : ControllerBase
{
    private readonly IDispatchCacheManager _cacheManager;

    public DiagnosticsController(IDispatchCacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    [HttpGet("cache-status")]
    public IActionResult GetCacheStatus()
    {
        var status = _cacheManager.GetStatus();

        return Ok(new
        {
            AllFrozen = status.AllFrozen,
            FrozenAt = status.FrozenAt,
            HandlerInvoker = status.HandlerInvokerFrozen,
            HandlerRegistry = status.HandlerRegistryFrozen,
            HandlerActivator = status.HandlerActivatorFrozen,
            ResultFactory = status.ResultFactoryFrozen,
            MiddlewareEvaluator = status.MiddlewareEvaluatorFrozen
        });
    }
}
```

### CacheFreezeStatus Properties

| Property | Type | Description |
|----------|------|-------------|
| `AllFrozen` | `bool` | True if all caches are frozen |
| `FrozenAt` | `DateTimeOffset?` | Timestamp when freeze occurred |
| `HandlerInvokerFrozen` | `bool` | Handler invocation cache status |
| `HandlerRegistryFrozen` | `bool` | Handler registry cache status |
| `HandlerActivatorFrozen` | `bool` | Handler activation cache status |
| `ResultFactoryFrozen` | `bool` | Result factory cache status |
| `MiddlewareEvaluatorFrozen` | `bool` | Middleware evaluator cache status |

## Health Check Integration

Add a health check to monitor cache status:

```csharp
public class CacheHealthCheck : IHealthCheck
{
    private readonly IDispatchCacheManager _cacheManager;

    public CacheHealthCheck(IDispatchCacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var status = _cacheManager.GetStatus();

        if (status.AllFrozen)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"All caches frozen at {status.FrozenAt}"));
        }

        // Degraded is OK for development mode
        return Task.FromResult(HealthCheckResult.Degraded(
            "Caches not frozen (development mode or late registration)"));
    }
}

// Register the health check
builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("dispatch-caches");
```

## Freeze Timing

The freeze occurs at `ApplicationStarted`, not `ApplicationStarting`. This ensures:

1. DI container is fully built
2. All handlers have been registered
3. Application is ready to serve requests

```
Host.Build() -> ConfigureServices -> ApplicationStarting -> ApplicationStarted -> FreezeAll()
                                                                              ^
                                                                    Caches freeze here
```

## Troubleshooting

### Caches Not Freezing

**Symptom:** `GetStatus().AllFrozen` returns false in production

**Causes:**
1. `AutoFreezeOnStart` disabled in configuration
2. Hot reload environment variables set
3. Application not using generic host

**Solution:**
```csharp
// Verify configuration
builder.Services.Configure<PerformanceOptions>(perf =>
{
    perf.AutoFreezeOnStart = true; // Explicit enable
});

// Or freeze manually after startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    var cacheManager = app.Services.GetRequiredService<IDispatchCacheManager>();
    cacheManager.FreezeAll();
});
```

### Performance Degradation

**Symptom:** Handler lookups slower than expected

**Diagnosis:**
```csharp
var status = cacheManager.GetStatus();
if (!status.AllFrozen)
{
    _logger.LogWarning("Caches not frozen - using ConcurrentDictionary fallback");
}
```

**Solution:** Ensure caches are frozen before handling production traffic.

## Best Practices

1. **Let it happen automatically** - The default configuration is optimal for most applications

2. **Don't disable without reason** - Only disable auto-freeze if you have a specific need for runtime handler registration

3. **Monitor in production** - Add a health check to verify caches are frozen

4. **Warmup before freeze** - If you have lazy-loaded handlers, ensure they're registered before `ApplicationStarted`

## See Also

- [Performance Overview](./index.md) - Full performance guide and optimization strategies
- [Caching](./caching.md) - Caching strategies and middleware
- [MessageContext Best Practices](./messagecontext-best-practices.md) - Hot-path optimization patterns for IMessageContext

## Next Steps

- [Performance Overview](./index.md) - Full performance guide
- [MessageContext Best Practices](./messagecontext-best-practices.md) - Hot-path optimization
- [Competitor Comparison](./competitor-comparison.md) - See performance vs alternatives
