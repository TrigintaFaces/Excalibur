---
sidebar_position: 9
title: Redis
description: Redis provider for inbox/outbox stores, caching, and pub/sub integration.
---

# Redis Provider

The Redis provider offers key-value storage with TTL support, pub/sub integration, and dedicated inbox/outbox store implementations for the messaging infrastructure.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Redis instance (local, Azure Cache for Redis, or ElastiCache)
- Familiarity with [data access](../data-access/index.md) and [caching](../performance/caching.md)

## Installation

```bash
dotnet add package Excalibur.Data.Redis
```

**Dependencies:** `Excalibur.Data.Abstractions`, `StackExchange.Redis`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddRedisInboxStore(options =>
{
    options.ConnectionString = "localhost:6379";
});
```

## Registration Options

### Inbox Store

```csharp
// With options callback
services.AddRedisInboxStore(options =>
{
    options.ConnectionString = "localhost:6379";
    options.Database = 0;
});

// With connection string
services.AddRedisInboxStore("localhost:6379");
```

### Outbox Store

```csharp
services.AddRedisOutboxStore(options =>
{
    options.ConnectionString = "localhost:6379";
    options.Database = 1;
});

// With connection string
services.AddRedisOutboxStore("localhost:6379");
```

## Use Cases

Redis is primarily used in Excalibur for:

- **Inbox deduplication** — Ensure messages are processed exactly once
- **Outbox store** — Reliable message publishing with at-least-once delivery
- **Caching layer** — Paired with `Excalibur.Dispatch.Caching` for middleware-level caching
- **Leader election** — See `Excalibur.LeaderElection.Redis`

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Caching](../performance/caching.md) — Dispatch caching middleware
- [Leader Election](../leader-election/index.md) — Redis-based leader election
