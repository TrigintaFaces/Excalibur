# Excalibur.EventSourcing.Redis

Redis implementations for Excalibur event sourcing using Redis Streams for event storage and Redis Hash for snapshots.

## Features

- **RedisEventStore** - Event store using Redis Streams with Lua-scripted optimistic concurrency
- **RedisSnapshotStore** - Snapshot store using Redis Hash with optional TTL
- Undispatched event tracking via Redis Sorted Set for outbox pattern support

## Quick Start

```csharp
// Add both event store and snapshot store
services.AddRedisEventSourcing("localhost:6379");

// Or configure separately
services.AddRedisEventStore(options =>
{
    options.ConnectionString = "localhost:6379";
    options.StreamKeyPrefix = "es";
    options.DatabaseIndex = 0;
});

services.AddRedisSnapshotStore(options =>
{
    options.ConnectionString = "localhost:6379";
    options.KeyPrefix = "snap";
    options.SnapshotTtlSeconds = 86400; // 24 hours
});
```

## Redis Data Model

### Event Streams
- Key pattern: `es:{aggregateType}:{aggregateId}`
- Each entry contains serialized `StoredEvent` JSON
- Stream length is used for optimistic concurrency control

### Snapshots
- Key pattern: `snap:{aggregateType}:{aggregateId}`
- Stored as Redis Hash with fields: snapshotId, aggregateId, aggregateType, version, createdAt, data, metadata
- Only latest snapshot is stored per aggregate

### Undispatched Events
- Sorted set key: `es:undispatched`
- Members are event IDs, scored by timestamp
- Atomically added during event append via Lua script

## Requirements

- Redis 5.0+ (for Streams support)
- StackExchange.Redis 2.x
