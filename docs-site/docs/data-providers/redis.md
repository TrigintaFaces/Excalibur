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

// Inbox with Redis
services.AddExcaliburInbox(inbox =>
{
    inbox.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .KeyPrefix("inbox")
             .Database(0);
    });
});
```

## Registration

All Redis subsystem builders support 4 canonical connection overloads: `ConnectionString()`, `Multiplexer()`, `MultiplexerFactory()`, and `BindConfiguration()`.

### Inbox Store

```csharp
services.AddExcaliburInbox(inbox =>
{
    inbox.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .KeyPrefix("myapp-inbox")
             .Database(0);
    });
});
```

### Outbox Store

```csharp
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .KeyPrefix("outbox")
             .Database(1);
    });
});
```

### Event Sourcing

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .KeyPrefix("myapp")
             .Database(0);
    })
    .AddRepository<OrderAggregate, Guid>();
});
```

### Leader Election

```csharp
services.AddExcaliburLeaderElection(le =>
{
    le.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .LockKey("myapp:leader")
             .Database(0);
    });
});
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
