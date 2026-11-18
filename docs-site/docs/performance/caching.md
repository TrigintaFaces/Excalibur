---
sidebar_position: 5
title: Caching
description: Pipeline-integrated caching with ICacheable, attribute-based policies, and pluggable providers for memory, distributed (Redis), and hybrid stores.
---

# Caching

Excalibur.Dispatch.Caching integrates caching directly into the dispatch pipeline. Actions that implement `ICacheable<T>` are automatically cached, with support for memory, distributed, and hybrid cache modes.

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch.Caching
  ```
- Familiarity with [pipeline concepts](../pipeline/index.md) and [middleware](../middleware/index.md)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Caching` | Pipeline caching middleware, `ICacheable<T>`, `ICacheInvalidationService`, attribute-based caching (backed by `HybridCache`) |
| `Excalibur.Caching` | Adaptive TTL strategies, CQRS projection cache invalidation |

For adaptive TTL's bounded access history, clock semantics and scoped measured
results, see [Adaptive TTL access history](./adaptive-ttl.md).

## Cache Modes

Excalibur.Dispatch supports three caching modes out of the box:

| Mode | Backend | Use Case |
|------|---------|----------|
| `Memory` | `IMemoryCache` (in-process) | Single-instance apps, in-process entries, not shared across servers |
| `Distributed` | `IDistributedCache` (Redis, SQL, etc.) | Multi-instance deployments, shared across servers, network latency cost |
| `Hybrid` | Memory L1 + Distributed L2 | Local L1 with shared L2 through `Microsoft.Extensions.Caching.Hybrid` |

## Setup

### In-Memory Only

An option for single-instance applications or development; entries remain local to the process:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatchMemoryCaching(
    configureMemory: options =>
    {
        // Optional size limits require consistent size units on every cache entry.
        // Do not impose a limit on a shared cache unless all writers supply sizes.
    },
    configureCaching: options =>
    {
        options.Enabled = true;
        options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
    });
```

:::caution Tag invalidation has consistency limits

The custom distributed tag tracker uses opaque version stamps for registered distributed backends,
not a Redis-only tag-to-keys set. Local memoization and backend failures affect when another instance
observes a change. Invalidation is not a transaction with handler effects or cache publication; choose
entry lifetimes and application consistency rules accordingly. See [Tag Tracking](#tag-tracking).

:::

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

Combines local memory with Redis. Evaluate hit rate, resource cost and consistency requirements for the application:

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
        options.Behavior.JitterRatio = 0.10; // 10% TTL jitter to spread expirations
    });
```

## ICacheable Actions

Make actions cacheable by implementing `ICacheable<T>`. Since `ICacheable<T>` extends `IDispatchAction<T>`, your action is automatically a dispatch action that returns a result:

```csharp ignore
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

### Key derivation and fail-open behavior

Caching is a cross-cutting concern, so it must never break the underlying operation. When the pipeline cannot derive a stable cache key for an action, it **fails open**: it skips the cache and invokes the handler directly, exactly as if caching were disabled for that one request. This happens when:

- An `ICacheable<T>` action's `GetCacheKey()` cannot be resolved (for example, reflection over the action fails).
- A non-`ICacheable` action cached via `[CacheResult]` cannot be serialized to produce a content-based key.
- The action's runtime type has no resolvable type name.

In every case the request still executes — you simply get a cache miss instead of an error. The framework **never fabricates a substitute key** (such as an identity hash), because a guessed key could produce a false cross-request cache hit and return one caller's data to another. Skipped derivations are logged at `Debug` level.

### Custom cache key builders

Set the `CacheKeyBuilder` option to replace the built-in `DefaultCacheKeyBuilder` with your own `ICacheKeyBuilder`:

```csharp
using System;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

public sealed class ProductCacheKeyBuilder : ICacheKeyBuilder
{
    public string? CreateKey(IDispatchAction action, IMessageContext context)
    {
        // Return null to signal "no cache identity" — the pipeline then skips caching
        // for this request. Implementations MUST be infallible: return null rather than
        // throwing when a key cannot be derived.
        if (action is not GetProductAction product || product.ProductId == Guid.Empty)
        {
            return null; // do not cache
        }

        var tenant = context.GetTenantId() ?? "global";
        return $"{tenant}:product:{product.ProductId}";
    }

    // Direct-invalidation overload: builds the stored key for a logical key under a
    // tenant/user identity, without a live IMessageContext. Apply the SAME identity
    // folding here as in CreateKey(action, context) so a direct invalidation removes
    // the exact key a cacheable query stored. This overload performs no serialization
    // and is AOT-/trimming-safe.
    public string CreateKey(string logicalKey, string? tenantId, string? userId)
    {
        var tenant = tenantId ?? "global";
        return $"{tenant}:{logicalKey}";
    }
}
```

`ICacheKeyBuilder` has two members; a custom builder must implement both:

| Member | Purpose |
|--------|---------|
| `string? CreateKey(IDispatchAction action, IMessageContext context)` | Derives the cache key for an in-flight request. Returns `null` to signal "do not cache". |
| `string CreateKey(string logicalKey, string? tenantId, string? userId)` | Builds the stored key for a logical key under a tenant/user identity, **without** a live `IMessageContext`. Used for direct-key invalidation. The built-in implementation performs no serialization; verify custom implementations and the complete native composition. |

The action/context overload returns `string?`:

- A **non-null** key is used to look up and store the cached result.
- A **`null`** result means **"do not cache"** — the pipeline bypasses the cache and invokes the handler.

This overload **must be infallible** for a "cannot derive a key" condition: return `null` instead of throwing, so a key-building failure degrades to a cache miss rather than failing the request.

> **Apply the same identity transform in both overloads.** A raw logical key can never equal the stored, identity-folded key, so the invalidation overload must fold tenant/user exactly as the request overload does — otherwise direct invalidation silently misses the entry it meant to remove.

## Attribute-Based Caching

Use `[CacheResult]` to add caching without implementing `ICacheable<T>`:

```csharp ignore
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

### A Cached Handler Must Have No Side Effects

This applies to `[CacheResult]` and to `ICacheable<T>` equally, and it is the one rule that will bite you if
it is missed.

A cached handler is run as the **value factory** of `HybridCache`, and a value factory is not invoked once
per dispatch:

| What happens | Handler invocations |
|---|---|
| Cache hit | **none** — the stored value is returned and your handler never runs |
| Concurrent compatible requests for the same key on one cache instance | May share an in-flight factory; this is not a distributed exactly-once guarantee, including across cancellation and retries |
| Background refresh | **one extra**, on a task no dispatch is awaiting |

Handler invocation count therefore need not match dispatch count. Do not rely on cache coalescing to
provide exactly-once effects.

Read it as: a cached handler answers *"what is the value for this key"* and nothing else. Anything that must
happen exactly once per request — a write, an outbound message, a counter, an audit entry — belongs outside
the cached handler:

```csharp
// WRONG: the audit entry is skipped on every cache hit and can be written twice on a refresh.
[CacheResult(ExpirationSeconds = 600)]
public sealed class GetCustomerAction : IDispatchAction<CustomerDto> { public Guid Id { get; init; } }

public sealed class GetCustomerHandler(ICustomerRepository repo, IAuditLog audit)
    : IActionHandler<GetCustomerAction, CustomerDto>
{
    public async Task<CustomerDto> HandleAsync(GetCustomerAction action, CancellationToken cancellationToken)
    {
        await audit.RecordAccessAsync(action.Id, cancellationToken); // side effect inside the value factory
        return await repo.GetAsync(action.Id, cancellationToken);
    }
}
```

Keep the handler a pure read, and put the per-request work in its own middleware or in the caller — where it
runs once per dispatch regardless of whether the read was served from cache.

## Cache Invalidation

### By Key or Tag

Invalidate caches by key or tag using `ICacheInvalidationService`:

```csharp ignore
using Excalibur.Dispatch.Caching;

public class UpdateProductHandler : IActionHandler<UpdateProductAction>
{
    private readonly ICacheInvalidationService _cache;

    public UpdateProductHandler(ICacheInvalidationService cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(
        UpdateProductAction action,
        CancellationToken ct)
    {
        // Update the product...

        // Invalidate specific entry
        await _cache.InvalidateKeysAsync([$"product:{action.ProductId}"], ct);

        // Invalidate all product listings
        await _cache.InvalidateTagsAsync(["products"], ct);
    }
}
```

For direct, low-level access you can also inject `HybridCache`
(`Microsoft.Extensions.Caching.Hybrid`) and call `RemoveAsync` / `RemoveByTagAsync`.

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
// Register projection caching after dispatch caching.
// AddProjectionCaching(...) is the canonical extension on IEventSourcingBuilder
// (the pre-unification AddExcaliburProjectionCaching aggregator
// is internal to Excalibur.Caching).
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseCaching(o => o.Enabled = true);
});
builder.Services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es.AddProjectionCaching()));
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

### NativeAOT verification limits

`AddDispatchCaching` carries trimming and dynamic-code warnings because result caching reconstructs
values using their runtime types. Do not treat the package's `IsAotCompatible` setting or a managed
test pass as proof that this result-caching path works in a native executable.

The 2026-11-04 UTC direct-invalidation probe using HybridCache 10.5.0 also failed native compilation
with IL2026, IL3050 and IL2070 diagnostics in its default JSON serializer factory. This is a failed
verification of that composition, not a measured NativeAOT pass or proof that every configuration
is unsupported. Original logs are under
`artifacts/benchmarks/current-refresh-20261101/direct-invalidation-native-publish-r1` and
`direct-invalidation-native-publish-r2`; the harness is
`tests/harnesses/CacheInvalidationAotProbe`.
Microsoft's [HybridCache NativeAOT guidance](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0#use-hybrid-cache-with-native-aot)
requires compatible serialization and trimming configuration. Verify the actual published executable
and its cached types before relying on native deployment.

### Ordinary-tag propagation

`HybridCache` has native logical tag invalidation. Dispatch also uses `ICacheTagTracker` version stamps:
entries record stamps at write time and the middleware compares them on reads, subject to local memoization
and failure handling. There is no reverse tag-to-keys set. Both `CacheInvalidationMiddleware` and the
registered `ICacheInvalidationService` bump custom stamps for ordinary tags. Direct service invalidation
first awaits native `HybridCache.RemoveByTagAsync`, then bumps each distinct ordinary tag sequentially.
Other instances observe the new stamps after their local stamp memo refreshes; this is not immediate
cross-instance visibility. Native invalidation or tracker failures propagate from the direct service,
and a failure partway through multiple tags can leave earlier tags invalidated.

Null, empty and whitespace-only tags retain native no-op handling. The reserved `"*"` tag is passed to
native HybridCache without creating a custom wildcard stamp. Cross-instance wildcard propagation is not
established by the ordinary-tag tests. Calling `HybridCache` directly bypasses Dispatch's custom tracker.
Tag invalidation does not change configured cache-entry retention or physically delete every tagged entry.

### ICacheTagTracker

```csharp
public interface ICacheTagTracker
{
    Task<string> GetOrCreateStampAsync(string tag, CancellationToken cancellationToken);
    Task BumpStampAsync(string tag, CancellationToken cancellationToken);
}
```

| Method | Purpose |
|--------|---------|
| `GetOrCreateStampAsync` | Resolves a tag's current version stamp, creating one the first time the tag is seen. Used both to embed a tag's stamp into a new entry at write time, and to check an existing entry's recorded stamp against the tag's current one at read time. |
| `BumpStampAsync` | Replaces a tag's version stamp. Entries recording the old stamp are rejected when subsequent validation observes the replacement; local memoization and failure handling affect visibility. |

A tag with no stamp yet in the backend (nothing has ever invalidated it) and a cache entry with no recorded stamp for one of its own tags (typically an entry written before this mechanism existed) are different conditions: the former is safe to treat as still valid, while the latter cannot be proven valid and is treated as invalidated. Cached data written under the previous key-tracking mechanism requires no migration — it is read as this second, "unprovable" case on its first post-upgrade read (one cache miss), and every subsequent write is stamped normally from then on.

### Built-in Implementations

The correct implementation is selected automatically based on `CacheMode` and whether a real `IDistributedCache` is registered:

| Implementation | Selected When | Storage |
|----------------|---------------|---------|
| `InMemoryCacheTagTracker` | Memory mode, or Distributed/Hybrid without a real distributed cache | In-process `ConcurrentDictionary`, bounded by `TagTrackerCapacity` |
| `DistributedCacheTagTracker` | Distributed or Hybrid mode with a real `IDistributedCache` (not `MemoryDistributedCache`) | One key per tag in the distributed cache, plus a local memo; cold initialization can perform Get followed by Set |

You do not need to register `ICacheTagTracker` manually -- `AddDispatchCaching()` and `UseCaching()` handle it automatically. A locally memoized stamp can avoid a backend round trip until `TagStampRefreshInterval` (default 5 seconds) elapses. Refresh happens on demand; backend latency, failures and overlapping operations mean this is not a strict cross-instance visibility deadline.

:::info Fail-open and atomicity

Pipeline middleware handles tag-store failures by logging and continuing without the affected cache operation; cancellation still propagates. This policy does not cover direct calls to `ICacheInvalidationService`, whose failures propagate to its caller. Individual backend calls do not make cold initialization, multi-tag invalidation, cache publication and handler effects one atomic transaction. Verify the consistency contract of the selected backend.
:::

### Capacity Limits

`CacheOptions.TagTrackerCapacity` (default: 10,000 distinct tags) bounds locally retained tag entries.
The in-memory tracker serializes admission with its capacity check. The distributed tracker now applies
the same option to its local memo: new tags at capacity still read or write their backend stamps, but
are not memoized. Existing memo entries can refresh. Successful bumps remove the local memo entry so
the next read reloads the backend, avoiding publication of a superseded stamp after overlapping writes.

This expands the option's earlier in-memory-only scope and can increase distributed-cache reads under
tag pressure or after invalidation. It does not change backend stamp lifetime or cached-entry retention.
The bound covers retained entry count, not tag-string byte size or backend operations already in flight.
Measure distinct-tag growth and backend load before increasing capacity:

```csharp
services.AddDispatchCaching(options =>
{
    options.Enabled = true;
    options.TagTrackerCapacity = 50_000; // Allow up to 50,000 distinct tags
});
```

## Configuration Reference

### CacheOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Must be explicitly enabled |
| `CacheMode` | `CacheMode` | `Hybrid` | `Memory`, `Distributed`, or `Hybrid` |
| `DefaultTags` | `string[]` | `[]` | Tags applied to all cached items |
| `TagTrackerCapacity` | `int` | `10,000` | Max locally retained tag entries; distributed overflow bypasses memoization without deleting backend stamps |
| `TagStampRefreshInterval` | `TimeSpan` | 5 sec | Local memo refresh interval, checked on demand; not a strict cross-instance visibility deadline |
| `TagStampLifetime` | `TimeSpan` | 1000 days | How long a tag's version stamp record is retained in the distributed cache backend; must exceed `DefaultExpiration` |
| `GlobalPolicy` | `IResultCachePolicy?` | `null` | Cross-cutting cache policy |
| `CacheKeyBuilder` | `ICacheKeyBuilder?` | `null` | Custom key generation (default: `DefaultCacheKeyBuilder`) |

### CacheBehaviorOptions (`options.Behavior`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultExpiration` | `TimeSpan` | 10 min | Default TTL for cached items |
| `UseSlidingExpiration` | `bool` | `true` | Reset expiration on access |
| `CacheTimeout` | `TimeSpan` | 200 ms | Max wait for a single distributed (L2) backend call. A read that runs longer is treated as a miss and a write that runs longer is dropped, so a slow backend degrades to a cache miss rather than slowing every request. It does **not** bound handler execution — a cached handler may take as long as its work takes without losing single-flight protection. The deadline is enforced by cancelling the backend call, so it depends on your `IDistributedCache` honouring its `CancellationToken`; a backend that ignores cancellation will still block for as long as it takes. |
| `JitterRatio` | `double` | `0.10` | Random TTL variance (0-1) to spread expirations; not a stampede-prevention guarantee |
| `EnableStatistics` | `bool` | `false` | Collect hit/miss/eviction metrics |
| `EnableCompression` | `bool` | `false` | Compress values in distributed cache |

### DistributedCacheOptions (`options.Distributed`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyPrefix` | `string` | `"dispatch:"` | Prefix for all distributed cache keys |
| `UseBinarySerialization` | `bool` | `false` | Binary serialization; measure size and CPU cost for the selected serializer and payload |
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
| `BreakDuration` | `TimeSpan` | 30 sec | How long circuit stays open |
| `HalfOpenTestLimit` | `int` | `3` | Test requests in half-open state |
| `HalfOpenSuccessThreshold` | `int` | `2` | Successes needed to close circuit |

## Cache Providers

Custom cache backends implement the standard `IDistributedCache` abstraction
(`Microsoft.Extensions.Caching.Distributed`) — the framework layers its memory L1,
tag tracking, and pipeline middleware on top of whatever `IDistributedCache` you supply:

```csharp
using Microsoft.Extensions.Caching.Distributed;

public class CustomDistributedCache : IDistributedCache
{
    public byte[]? Get(string key) { /* ... */ }
    public Task<byte[]?> GetAsync(string key, CancellationToken token) { /* ... */ }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { /* ... */ }
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token) { /* ... */ }
    public void Refresh(string key) { /* ... */ }
    public Task RefreshAsync(string key, CancellationToken token) { /* ... */ }
    public void Remove(string key) { /* ... */ }
    public Task RemoveAsync(string key, CancellationToken token) { /* ... */ }
}
```

Register with the generic distributed caching method:

```csharp
services.AddDispatchDistributedCaching<CustomDistributedCache>(options =>
{
    options.Enabled = true;
    options.CacheMode = CacheMode.Distributed;
});
```

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
| Multiple servers, local cache plus shared backing store | `Hybrid` (default); measure hit rate, latency and consistency requirements |
| Serverless (Azure Functions, AWS Lambda) | `Distributed` (Redis) |

**TTL jitter:** The default `JitterRatio` is 0.10 (10%). Jitter spreads expirations but does not guarantee stampede prevention; measure concurrent misses and backend load.

**Circuit breaker:** Review its failure threshold, failure window, open duration and half-open behavior for the deployment. The default threshold is 5 failures and the default open duration is 30 seconds; actual transitions and fallback behavior depend on configuration and observed failures.

**Statistics:** Enable `Behavior.EnableStatistics` in staging/production to track hit rates. Low hit rates may indicate poor key design or too-short TTLs.

**Compression:** Evaluate `Behavior.EnableCompression` with representative payloads. Compare stored size and network traffic against compression/decompression CPU and latency; large payloads are not necessarily compressible.

## What's Next

- [Performance Best Practices](messagecontext-best-practices.md) - Optimize message processing
- [Auto-Freeze](auto-freeze.md) - Immutable message optimization
- [Middleware](../middleware/index.md) - Custom pipeline middleware

## See Also

- [Built-in Middleware](../middleware/built-in.md) -- Overview of all built-in middleware including caching integration
- [Auto-Freeze](./auto-freeze.md) -- Immutable message optimization for improved caching and thread safety
- [Performance Tuning](../operations/performance-tuning.md) -- Operational guidance for tuning Dispatch performance in production
