---
sidebar_position: 3
title: Auto-Freeze
description: Automatic cache optimization for production performance
---

# Auto-Freeze

Dispatch automatically optimizes internal caches on application startup using `FrozenDictionary`. This provides lock-free, high-performance lookups in production without any configuration.

## Before You Start

- **.NET 10.0**
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

| Metric | Measured | Notes |
|--------|----------|-------|
| Handler registry lookup (warm hit) | 4.09-4.12 ns, 0 B | Consistent across transient, scoped and singleton handlers |
| Handler registry lookup (cold miss) | 6.35-6.41 ns, 0 B | A miss costs more than a hit |
| Resolve action handler (singleton) | 5.50 ns, 0 B | |
| Resolve action handler (transient) | 7.05 ns | |
| Resolve action handler (scoped) | 69.76 ns | Scope creation dominates; the registry is not the cost here |
| Memory overhead | Synchronization locks removed | Reduced GC pressure |
| CPU overhead | Lock-free after freeze | Better scalability |

Measured 2026-09-04, `HandlerResolutionBenchmarks`, BenchmarkDotNet 0.15.8 in-process on
.NET 10.0.11 (i9-14900K).

:::caution Freezing measures slower for profile selection, and the 10x figure described something else

This page previously stated a handler lookup of ~50 ns before freeze against ~5 ns after, a 10x
improvement. That comparison had never been measured. Part of it has been now, and it does not
support the claim.

**Profile selection: freezing costs more, at every registered type count tested.** Warm and frozen
arms measured under one job configuration, rotating across all registered message types:

| Registered message types | Warm (`ConcurrentDictionary`) | Frozen (`FrozenDictionary`) |
|--------------------------|-------------------------------|-----------------------------|
| 1                        | 3.15 ns, 0 B                  | 3.99 ns, 0 B                |
| 10                       | 2.98 ns, 0 B                  | 5.56 ns, 0 B                |
| 100                      | 3.57 ns, 0 B                  | 6.45 ns, 0 B                |

There is no crossover. The frozen dictionary is 27% slower at one registered type and 81% slower at
a hundred, and the gap widens with the type count instead of closing. Both allocate nothing.

**What a ~10x figure probably described is the first lookup, not the freeze.** Selecting a profile
for a message type that is not yet cached runs the full profile scan: **~310 ns and 128 B**, roughly
a hundred times a cached lookup, independent of how many types are already cached. That is a
cold-versus-cached difference, which every cache delivers whether or not it is later frozen.

**Handler lookup is still unmeasured.** The handler invocation, registry, activation and result
caches are separate from profile selection, and no like-for-like before-and-after-freeze arm exists
for them. The figures in the table above this note are frozen steady-state costs with no unfrozen
counterpart. Do not read the profile-selection result as a measurement of those.

Measured 2026-09-05, `ProfileSelectionScaleBenchmarks`, BenchmarkDotNet 0.15.8 in-process on
.NET 10.0.11 (i9-14900K).
:::

:::warning A message type first seen after freezing is never cached

Freezing the profile-selection cache releases it, and the code path that would add a newly seen
type to it afterwards has nothing to add to. So a message type that was not present when the
freeze happened runs the full profile scan on **every dispatch, indefinitely** -- about 310 ns
and 128 B each time, rather than the ~3 ns a cached lookup costs.

Auto-freeze is enabled by default and happens at startup, so this affects any type that arrives
later: one registered by a plugin, a handler in an assembly loaded on demand, or a generic
constructed at run time. Nothing is logged when it happens.

If that describes your application, disable auto-freeze -- the measurements above show freezing
costs more than it saves for profile selection at every type count tested, so you give up
nothing on this path by leaving the cache unfrozen.
:::

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
    options.CrossCutting.Performance.AutoFreezeOnStart = false;
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
