---
sidebar_position: 3
title: Auto-Freeze
description: Publishing frozen snapshots of selected internal caches
---

# Auto-Freeze

Dispatch can publish frozen snapshots of selected internal caches at application startup using `FrozenDictionary`. Measure the effect on your workload.

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Auto-freeze is enabled by default — no additional setup required

## How It Works

When enabled and hot reload is not detected, Dispatch attempts to freeze selected caches when
`IHostApplicationLifetime.ApplicationStarted` is signaled. Check status after the attempt; a lock
timeout can leave caches unfrozen.

```csharp
// Automatic - happens on startup (default behavior)
var host = builder.Build();
await host.RunAsync();

// ApplicationStarted triggers the configured freeze attempt.
// Verify the resulting cache status.
```

### What Gets Frozen

| Cache | Purpose | Benefit |
|-------|---------|---------|
| Handler invocation | Compiled handler delegates | Lock-free lookups |
| Handler registry | Manual handler registrations | Immutable lookup snapshot |
| Handler activation | Handler context setup | Cached activation metadata |
| Result factory | Message result creation | Cached result factory metadata |

### Performance Impact

The following figures are a historical snapshot from 2026-09-04. They have not
been refreshed by the October benchmark campaign and are not current release
measurements or a before/after-freeze comparison.

| Metric | Measured | Notes |
|--------|----------|-------|
| Handler registry lookup (warm hit) | 4.09-4.12 ns, 0 B | Consistent across transient, scoped and singleton handlers |
| Handler registry lookup (cold miss) | 6.35-6.41 ns, 0 B | A miss costs more than a hit |
| Resolve action handler (singleton) | 5.50 ns, 0 B | |
| Resolve action handler (transient) | 7.05 ns | |
| Resolve action handler (scoped) | 69.76 ns | This observation does not isolate scope creation from other work |
| Memory overhead | Not measured in this table | No GC-pressure reduction established by these lookup timings |
| CPU scalability | Not measured in this table | Lookup latency does not establish throughput under contention |

Measured 2026-09-04, `HandlerResolutionBenchmarks`, BenchmarkDotNet 0.15.8 in-process on
.NET 10.0.11 (i9-14900K).

:::note Historical profile-selection measurements

Earlier versions cached profile selection by message type. Measurements recorded on 2026-09-05
compared that implementation's concurrent and frozen dictionaries. They do not describe the current
`PipelineProfileRegistry`: it evaluates compatibility against each message and publishes an immutable
snapshot of profile membership. This preserves message-dependent matching and observes completed
registration/removal changes. There is no per-message-type profile-selection cache to freeze.

Do not disable auto-freeze to work around historical profile-cache behavior. Handler registries and
invoker registrations are separate mechanisms; register those before freezing. The handler lookup
measurements above are steady-state results, not a measured before/after-freeze speedup.
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

Disabling automatic freezing preserves mutable paths; measure their effect on the workload.
Choose this setting according to registration and lifecycle requirements.
:::

### Hot Reload Detection

Auto-freeze is automatically disabled when hot reload is detected:

- `dotnet watch` sets `DOTNET_WATCH=1`
- Edit & Continue sets `DOTNET_MODIFIABLE_ASSEMBLIES=debug`

Hot-reload detection skips automatic freezing; it does not itself verify handler discovery.

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
            ResultFactory = status.ResultFactoryFrozen
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

## Health Check Integration

This optional configuration-status check reports freeze state, not overall application health.
Choose healthy/degraded policy according to the deployment's requirements:

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

        // Example policy only; false does not identify the cause.
        return Task.FromResult(HealthCheckResult.Degraded(
            "Selected caches are not all frozen; inspect configuration and startup logs"));
    }
}

// Register the health check
builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("dispatch-caches");
```

## Freeze Timing

The callback runs when `ApplicationStarted` is signaled. Complete intended registration and
warmup before freezing; the signal itself does not verify registration completeness or readiness.

```
Configure services -> Build -> Start -> ApplicationStarted callback -> FreezeAll() attempt
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
builder.Services.Configure<DispatchOptions>(options =>
{
    options.CrossCutting.Performance.AutoFreezeOnStart = true;
});

// Or freeze manually after startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    var cacheManager = app.Services.GetRequiredService<IDispatchCacheManager>();
    cacheManager.FreezeAll();
});
```

### Investigating Lookup Performance

**Symptom:** Handler lookups slower than expected

**Check freeze status:**
```csharp
var status = cacheManager.GetStatus();
if (!status.AllFrozen)
{
    _logger.LogWarning("Caches not frozen - using ConcurrentDictionary fallback");
}
```

Compare matched workloads before attributing a slowdown to freeze status. An unfrozen cache alone does not establish the cause of slower lookups.

## Best Practices

1. **Let it happen automatically** - Start with the default configuration and verify it against your workload

2. **Don't disable without reason** - Only disable auto-freeze if you have a specific need for runtime handler registration

3. **Monitor in production** - Observe freeze status separately from application health

4. **Warmup before freeze** - If you have lazy-loaded handlers, ensure they're registered before `ApplicationStarted`

## See Also

- [Performance Overview](./index.md) - Full performance guide and optimization strategies
- [Caching](./caching.md) - Caching strategies and middleware
- [MessageContext Best Practices](./messagecontext-best-practices.md) - Hot-path optimization patterns for IMessageContext

## Next Steps

- [Performance Overview](./index.md) - Full performance guide
- [MessageContext Best Practices](./messagecontext-best-practices.md) - Hot-path optimization
- [Benchmark Results](./competitor-comparison.md) - Measured dispatch overhead and methodology
