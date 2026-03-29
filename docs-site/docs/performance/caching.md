---
sidebar_position: 5
title: Caching
description: Pipeline-integrated caching with ICacheable, attribute-based policies, and pluggable providers for memory, distributed (Redis), and hybrid stores.
---

# Caching

Excalibur.Dispatch.Caching integrates caching directly into the dispatch pipeline. Actions that implement `ICacheable<T>` are automatically cached, with support for memory, distributed, and hybrid cache modes.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch.Caching
  ```
- Familiarity with [pipeline concepts](../pipeline/index.md) and [middleware](../middleware/index.md)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Caching` | Pipeline caching middleware, `ICacheable<T>`, `ICacheProvider`, attribute-based caching |
| `Excalibur.Caching` | Adaptive TTL strategies, CQRS projection cache invalidation |

## Cache Modes

Excalibur.Dispatch supports three caching modes out of the box:

| Mode | Backend | Use Case |
|------|---------|----------|
| `Memory` | `IMemoryCache` (in-process) | Single-instance apps, fastest performance, not shared across servers |
| `Distributed` | `IDistributedCache` (Redis, SQL, etc.) | Multi-instance deployments, shared across servers, network latency cost |
| `Hybrid` | Memory L1 + Distributed L2 | Best of both: local speed with shared consistency via `Microsoft.Extensions.Caching.Hybrid` |

## Setup

### In-Memory Only

Best for single-instance applications or development:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatchMemoryCaching(
    configureMemory: options =>
    {
        options.SizeLimit = 1024; // Max entries
    },
    configureCaching: options =>
    {
        options.Enabled = true;
        options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
    });
```

### Redis (Distributed)

For multi-instance deployments with shared cache:

```csharp
services.AddDispatchRedisCaching(
    configureRedis: options =>
    {
        options.Configuration = "localhost:6379,abortConnect=false";
        options.InstanceName = "MyApp:";
    },
    configureCaching: options =>
    {
        options.Enabled = true;
        options.Distributed.KeyPrefix = "dispatch:";
        options.Distributed.MaxRetryAttempts = 3;
        options.Distributed.RetryDelay = TimeSpan.FromMilliseconds(100);
    });
```

With `appsettings.json`:

```json
{
  "Redis": {
    "Configuration": "localhost:6379,abortConnect=false,ssl=true,password=your-password",
    "InstanceName": "MyApp:"
  }
}
```

### Hybrid (Memory + Redis)

Recommended for production. Checks local memory first, falls back to Redis:

```csharp
services.AddDispatchHybridCaching(
    configureHybrid: options =>
    {
        options.MaximumPayloadBytes = 1024 * 1024; // 1MB max cached item
        options.DefaultEntryOptions = new()
        {
            Expiration = TimeSpan.FromMinutes(10),
            LocalCacheExpiration = TimeSpan.FromMinutes(2),
        };
    },
    configureRedis: options =>
    {
        options.Configuration = "localhost:6379,abortConnect=false";
        options.InstanceName = "MyApp:";
    },
    configureCaching: options =>
    {
        options.Enabled = true;
    });
```

### Builder Pattern

All modes also work through `IDispatchBuilder`:

```csharp
dispatch.UseCaching()
    .WithCachingOptions(options =>
    {
        options.Enabled = true;
        options.CacheMode = CacheMode.Hybrid;
        options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(10);
        options.Behavior.UseSlidingExpiration = true;
        options.Behavior.JitterRatio = 0.10; // 10% TTL jitter to avoid stampedes
    });
```

## ICacheable Actions

Make actions cacheable by implementing `ICacheable<T>`. Since `ICacheable<T>` extends `IDispatchAction<T>`, your action is automatically a dispatch action that returns a result:

```csharp
using Excalibur.Dispatch.Caching;

public class GetProductAction : ICacheable<ProductDto>
{
    public Guid ProductId { get; set; }

    // Cache for 5 minutes
    public int ExpirationSeconds => 300;

    // Unique cache key for this action
    public string GetCacheKey()
        => $"product:{ProductId}";

    // Tags for grouped invalidation
    public string[] GetCacheTags()
        => [$"product:{ProductId}", "products"];

    // Conditional caching (receives the handler result)
    public bool ShouldCache(object? result)
        => ProductId != Guid.Empty && result is not null;
}
```

The pipeline automatically checks the cache before executing the handler. On a cache hit, the handler is skipped and the cached result is returned.

### ICacheable Members

| Member | Purpose |
|--------|---------|
| `ExpirationSeconds` | How long to cache the result (default: 60s) |
| `GetCacheKey()` | Unique key identifying this specific request |
| `GetCacheTags()` | Tags for grouped invalidation |
| `ShouldCache(object? result)` | Whether to cache this particular result |

## Attribute-Based Caching

Use `[CacheResult]` to add caching without implementing `ICacheable<T>`:

```csharp
using Excalibur.Dispatch.Caching;

[CacheResult(
    ExpirationSeconds = 600,
    Tags = new[] { "products" },
    OnlyIfSuccess = true,
    IgnoreNullResult = true)]
public class ListProductsAction : IDispatchAction<IReadOnlyList<ProductDto>>
{
    public string Category { get; set; } = string.Empty;
}
```

### CacheResultAttribute Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExpirationSeconds` | `int` | 0 (use default) | Cache duration in seconds |
| `Tags` | `string[]` | `[]` | Tags for grouped invalidation |
| `OnlyIfSuccess` | `bool` | `true` | Only cache successful results |
| `IgnoreNullResult` | `bool` | `true` | Skip caching when result is null |

## Cache Invalidation

### By Key or Tag

Invalidate caches by key or tag using `ICacheProvider`:

```csharp
public class UpdateProductHandler : IActionHandler<UpdateProductAction>
{
    private readonly ICacheProvider _cache;

    public UpdateProductHandler(ICacheProvider cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(
        UpdateProductAction action,
        CancellationToken ct)
    {
        // Update the product...

        // Invalidate specific entry
        await _cache.RemoveAsync($"product:{action.ProductId}", ct);

        // Invalidate all product listings
        await _cache.RemoveByTagAsync("products", ct);
    }
}
```

### Bulk Invalidation Service

For cross-cutting invalidation, inject `ICacheInvalidationService`:

```csharp
public class ProductCatalogRefreshHandler
{
    private readonly ICacheInvalidationService _invalidation;

    public ProductCatalogRefreshHandler(ICacheInvalidationService invalidation)
    {
        _invalidation = invalidation;
    }

    public async Task HandleAsync(CatalogRefreshedEvent evt, CancellationToken ct)
    {
        // Invalidate all product-related caches at once
        await _invalidation.InvalidateTagsAsync(["products", "categories"], ct);

        // Or specific keys
        await _invalidation.InvalidateKeysAsync(
            evt.UpdatedProductIds.Select(id => $"product:{id}"),
            ct);
    }
}
```

### CQRS Projection Invalidation

When using event sourcing with projections, the `Excalibur.Caching` package provides automatic cache invalidation when projections update:

```bash
dotnet add package Excalibur.Caching
```

```csharp
// Register projection caching after dispatch caching
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseCaching(o => o.Enabled = true);
});
builder.Services.AddExcaliburProjectionCaching();
```

Implement `IProjectionTagResolver<T>` to map domain events to cache tags:

```csharp
public class ProductUpdatedTagResolver : IProjectionTagResolver<ProductUpdatedEvent>
{
    public IEnumerable<string> GetTags(ProductUpdatedEvent message)
    {
        yield return $"product:{message.ProductId}";
        yield return "products";
    }
}
```

When the projection handler processes `ProductUpdatedEvent`, the `IProjectionCacheInvalidator` automatically invalidates cache entries tagged with the resolved tags.

## Tag Tracking

`HybridCache` (used in Hybrid mode) has native tag-based invalidation support. However, `IMemoryCache` and `IDistributedCache` do not track which cache keys belong to which tags. The `ICacheTagTracker` interface bridges this gap by maintaining key-to-tag mappings so that tag-based invalidation works across all three cache modes.

### ICacheTagTracker

```csharp
public interface ICacheTagTracker
{
    Task RegisterKeyAsync(string key, string[] tags, CancellationToken cancellationToken);
    Task<HashSet<string>> GetKeysByTagsAsync(string[] tags, CancellationToken cancellationToken);
    Task UnregisterKeyAsync(string key, CancellationToken cancellationToken);
}
```

| Method | Purpose |
|--------|---------|
| `RegisterKeyAsync` | Associates a cache key with one or more tags |
| `GetKeysByTagsAsync` | Returns all cache keys associated with the specified tags |
| `UnregisterKeyAsync` | Removes tag mappings for a key (called on eviction/expiry) |

### Built-in Implementations

The correct implementation is selected automatically based on `CacheMode` and whether a real `IDistributedCache` is registered:

| Implementation | Selected When | Storage |
|----------------|---------------|---------|
| `InMemoryCacheTagTracker` | Memory mode, or Distributed/Hybrid without a real distributed cache | In-process `ConcurrentDictionary`, bounded by `TagTrackerCapacity` |
| `DistributedCacheTagTracker` | Distributed or Hybrid mode with a real `IDistributedCache` (not `MemoryDistributedCache`) | Stores mappings in the distributed cache alongside cached data |

You do not need to register `ICacheTagTracker` manually -- `AddDispatchCaching()` and `UseCaching()` handle it automatically.

### Capacity Limits

`InMemoryCacheTagTracker` is bounded by `CacheOptions.TagTrackerCapacity` (default: 10,000). When the tracker reaches capacity, new registrations are silently skipped to prevent unbounded memory growth. Increase this value if your application caches more than 10,000 distinct keys with tags:

```csharp
services.AddDispatchCaching(options =>
{
    options.Enabled = true;
    options.TagTrackerCapacity = 50_000; // Allow up to 50,000 tracked entries
});
```

## Configuration Reference

### CacheOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Must be explicitly enabled |
| `CacheMode` | `CacheMode` | `Hybrid` | `Memory`, `Distributed`, or `Hybrid` |
| `DefaultTags` | `string[]` | `[]` | Tags applied to all cached items |
| `TagTrackerCapacity` | `int` | `10,000` | Max key-to-tag entries tracked by `InMemoryCacheTagTracker`; new registrations are skipped when full |
| `GlobalPolicy` | `IResultCachePolicy?` | `null` | Cross-cutting cache policy |
| `CacheKeyBuilder` | `ICacheKeyBuilder?` | `null` | Custom key generation (default: `DefaultCacheKeyBuilder`) |

### CacheBehaviorOptions (`options.Behavior`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultExpiration` | `TimeSpan` | 10 min | Default TTL for cached items |
| `UseSlidingExpiration` | `bool` | `true` | Reset expiration on access |
| `CacheTimeout` | `TimeSpan` | 200 ms | Max wait for cache operations |
| `JitterRatio` | `double` | `0.10` | Random TTL variance (0-1) to prevent stampedes |
| `EnableStatistics` | `bool` | `false` | Collect hit/miss/eviction metrics |
| `EnableCompression` | `bool` | `false` | Compress values in distributed cache |

### DistributedCacheConfiguration (`options.Distributed`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyPrefix` | `string` | `"dispatch:"` | Prefix for all distributed cache keys |
| `UseBinarySerialization` | `bool` | `false` | Binary serialization (more efficient, less debuggable) |
| `MaxRetryAttempts` | `int` | `3` | Retry attempts for distributed operations |
| `RetryDelay` | `TimeSpan` | 100 ms | Delay between retries |

### CacheResilienceOptions (`options.Resilience`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableFallback` | `bool` | `true` | Fall back to direct execution when cache fails |
| `LogMetricsOnDisposal` | `bool` | `true` | Log performance metrics on shutdown |

#### Circuit Breaker (`options.Resilience.CircuitBreaker`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable cache circuit breaker |
| `FailureThreshold` | `int` | `5` | Consecutive failures to open circuit |
| `FailureWindow` | `TimeSpan` | 1 min | Time window for counting failures |
| `OpenDuration` | `TimeSpan` | 30 sec | How long circuit stays open |
| `HalfOpenTestLimit` | `int` | `3` | Test requests in half-open state |
| `HalfOpenSuccessThreshold` | `int` | `2` | Successes needed to close circuit |

## Cache Providers

Implement `ICacheProvider` for custom cache backends:

```csharp
using Excalibur.Dispatch.Caching;

public class CustomCacheProvider : ICacheProvider
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) { /* ... */ }
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken,
        TimeSpan? expiration = null, string[]? tags = null) { /* ... */ }
    public Task RemoveAsync(string key, CancellationToken cancellationToken) { /* ... */ }
    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken) { /* ... */ }
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) { /* ... */ }
}
```

Register with the generic distributed caching method:

```csharp
services.AddDispatchDistributedCaching<CustomCacheProvider>(options =>
{
    options.Enabled = true;
    options.CacheMode = CacheMode.Distributed;
});
```

### ICacheProvider Methods

| Method | Purpose |
|--------|---------|
| `GetAsync<T>(key, ct)` | Retrieve a cached value |
| `SetAsync<T>(key, value, ct, expiration?, tags?)` | Store a value with optional expiration and tags |
| `RemoveAsync(key, ct)` | Remove a specific entry |
| `RemoveByTagAsync(tag, ct)` | Remove all entries with a tag |
| `ExistsAsync(key, ct)` | Check if a key exists |

## Health Monitoring

Monitor cache health and performance in production by injecting `ICacheHealthMonitor`:

```csharp
public class CacheHealthCheck
{
    private readonly ICacheHealthMonitor _monitor;

    public CacheHealthCheck(ICacheHealthMonitor monitor)
    {
        _monitor = monitor;
    }

    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        var status = await _monitor.GetHealthStatusAsync(ct);
        // status.IsHealthy, status.ResponseTimeMs, status.ConnectionStatus

        var perf = _monitor.GetPerformanceSnapshot();
        // perf.HitCount, perf.MissCount, perf.ErrorCount

        return status.IsHealthy;
    }
}
```

When Redis is registered, `CacheHealthMonitor` pings Redis via `IConnectionMultiplexer.PingAsync()` to verify connectivity.

## Production Recommendations

**Choose the right mode:**

| Scenario | Recommended Mode |
|----------|-----------------|
| Single server, low latency required | `Memory` |
| Multiple servers, shared state needed | `Distributed` (Redis) |
| Multiple servers, best performance | `Hybrid` (default) |
| Serverless (Azure Functions, AWS Lambda) | `Distributed` (Redis) |

**TTL jitter:** Keep `JitterRatio` at 0.10 (10%) to avoid cache stampedes where many entries expire simultaneously.

**Circuit breaker:** Leave enabled in production. If Redis goes down, the circuit breaker opens after 5 failures and the pipeline falls back to direct handler execution for 30 seconds before retrying.

**Statistics:** Enable `Behavior.EnableStatistics` in staging/production to track hit rates. Low hit rates may indicate poor key design or too-short TTLs.

**Compression:** Enable `Behavior.EnableCompression` for large cached objects in distributed mode to reduce Redis memory usage and network bandwidth.

## What's Next

- [Performance Best Practices](messagecontext-best-practices.md) - Optimize message processing
- [Auto-Freeze](auto-freeze.md) - Immutable message optimization
- [Middleware](../middleware/index.md) - Custom pipeline middleware

## See Also

- [Built-in Middleware](../middleware/built-in.md) -- Overview of all built-in middleware including caching integration
- [Auto-Freeze](./auto-freeze.md) -- Immutable message optimization for improved caching and thread safety
- [Performance Tuning](../operations/performance-tuning.md) -- Operational guidance for tuning Dispatch performance in production
