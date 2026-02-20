---
sidebar_position: 5
title: Caching
description: Pipeline-integrated caching with ICacheable, attribute-based policies, and pluggable providers for memory and distributed stores.
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

## Package

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Caching` | Pipeline caching middleware, `ICacheable<T>`, `ICacheProvider`, attribute-based caching |

## Setup

Register caching services with `IDispatchBuilder`:

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.AddDispatchCaching();

// With options
builder.WithCachingOptions(options =>
{
    // Configure caching behavior
});

// With a result cache policy
builder.WithResultCachePolicy(policy =>
{
    // Configure result-level caching
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
| `ExpirationSeconds` | How long to cache the result |
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
| `ExpirationSeconds` | `int` | — | Cache duration in seconds |
| `Tags` | `string[]` | — | Tags for grouped invalidation |
| `OnlyIfSuccess` | `bool` | `true` | Only cache successful results |
| `IgnoreNullResult` | `bool` | `true` | Skip caching when result is null |

## Cache Providers

Implement `ICacheProvider` for custom cache backends:

```csharp
using Excalibur.Dispatch.Caching;

public class RedisCacheProvider : ICacheProvider
{
    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken)
    {
        // Retrieve from Redis
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken,
        TimeSpan? expiration = null,
        string[]? tags = null)
    {
        // Store in Redis
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken)
    {
        // Remove single key
    }

    public async Task RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken)
    {
        // Remove all entries with this tag
    }

    public async Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken)
    {
        // Check if key exists
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken)
    {
        // Clear all cached entries
    }
}
```

### ICacheProvider Methods

| Method | Purpose |
|--------|---------|
| `GetAsync<T>(key, ct)` | Retrieve a cached value |
| `SetAsync<T>(key, value, ct, expiration?, tags?)` | Store a value with optional expiration and tags |
| `RemoveAsync(key, ct)` | Remove a specific entry |
| `RemoveByTagAsync(tag, ct)` | Remove all entries with a tag |
| `ExistsAsync(key, ct)` | Check if a key exists |
| `ClearAsync(ct)` | Clear all cached entries |

## Cache Modes

`CacheMode` controls where cache data is stored:

| Mode | Description |
|------|-------------|
| `Memory` | In-process memory cache (fastest, per-instance) |
| `Distributed` | External cache store (Redis, SQL, etc.) |
| `Hybrid` | Check memory first, fall back to distributed |

## Cache Invalidation

Invalidate caches by key or tag:

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

## What's Next

- [Performance Best Practices](messagecontext-best-practices.md) - Optimize message processing
- [Auto-Freeze](auto-freeze.md) - Immutable message optimization
- [Middleware](../middleware/index.md) - Custom pipeline middleware

## See Also

- [Built-in Middleware](../middleware/built-in.md) — Overview of all built-in middleware including caching integration
- [Auto-Freeze](./auto-freeze.md) — Immutable message optimization for improved caching and thread safety
- [Performance Tuning](../operations/performance-tuning.md) — Operational guidance for tuning Dispatch performance in production
