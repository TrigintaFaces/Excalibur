# Excalibur.EventSourcing.Redis

Redis implementations for Excalibur event sourcing using Redis Streams for event storage and Redis Hash for snapshots.

## Features

- **RedisEventStore** - Event store using Redis Streams with Lua-scripted optimistic concurrency
- **RedisSnapshotStore** - Snapshot store using Redis Hash with optional TTL
- Undispatched event tracking via Redis Sorted Set for outbox pattern support

## Quick Start

```csharp
// Registers the Redis event store and snapshot store together.
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
    es.UseRedis(redis => redis.ConnectionString("localhost:6379"))));

// With a key prefix and a non-default database index:
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
    es.UseRedis(redis => redis
        .ConnectionString("localhost:6379")
        .KeyPrefix("es")
        .Database(0))));
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

## Requirements

- Redis 5.0+ (for Streams support)
- StackExchange.Redis 2.x
