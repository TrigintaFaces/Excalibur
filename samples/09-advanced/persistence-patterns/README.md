# 09-advanced/persistence-patterns — Event Stores, Snapshots, and Handler Persistence

Production-grade persistence for event-sourced systems. Shows event store providers, snapshot tuning, inbox deduplication, transactional handlers, multi-database setups, and session-scoped state.

## Samples

### Event Stores

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [ProjectionsSample](ProjectionsSample/) | In-memory event store, inline projections, multi-stream projections, checkpoint tracking, rebuild patterns | None |
| [SqlServerEventStore](SqlServerEventStore/) | SQL Server persistence, aggregate rehydration, direct event store access, configuration via `BindConfiguration` | Docker (SQL Server) |
| [CosmosDbEventStore](CosmosDbEventStore/) | Cosmos DB partitioning, change feed, global distribution, consistency levels | Cosmos DB Emulator or Azure |

### Performance & Tuning

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [SnapshotStrategies](SnapshotStrategies/) | Interval, time-based, size-based, composite snapshot strategies; tuning guide; replace-snapshot (last-wins) | None |

### Handler Persistence

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [InboxIdempotency](InboxIdempotency/) | At-least-once delivery with deduplication guarantees | None (in-memory) |
| [TransactionalHandlers](TransactionalHandlers/) | ACID handler execution with `TransactionScope` | None (in-memory) |
| [SessionManagement](SessionManagement/) | Session-aware message processing, state tracking | None (in-memory) |

### Scale-out

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [MultiDatabase](MultiDatabase/) | Event sourcing across multiple database systems | Docker |
| [CloudStorageSnapshots](CloudStorageSnapshots/) | S3 / Blob / GCS snapshot storage with event sourcing | Docker or cloud credentials |

> **Looking for multi-tenant event sourcing?** That sample is composed end-to-end (tenant context + routing + projections + query scoping) and lives at [`11-real-world/MultiTenantEventSourcing/`](../../11-real-world/MultiTenantEventSourcing/).

## Provider Selection Guide

| Provider | Best For | Consistency | Scaling |
|----------|----------|-------------|---------|
| **In-Memory** | Testing, development, learning | Strong | N/A |
| **SQL Server** | Enterprise, ACID transactions | Strong | Vertical |
| **Cosmos DB** | Global distribution, high throughput | Tunable | Horizontal |
| **Cloud storage snapshots** | Low-cost long-term retention | Eventual | N/A (blob) |

## Learning Path

1. **[ProjectionsSample](ProjectionsSample/)** — grasp inline/async projections with no infra
2. **[SqlServerEventStore](SqlServerEventStore/)** — persist real events + rehydrate aggregates
3. **[SnapshotStrategies](SnapshotStrategies/)** — tune performance for heavily-loaded aggregates
4. **[InboxIdempotency](InboxIdempotency/)** + **[TransactionalHandlers](TransactionalHandlers/)** — exactly-once handler semantics
5. **[CloudStorageSnapshots](CloudStorageSnapshots/)** — offload snapshots to S3/Blob/GCS for long-term retention

## Related

- [09-advanced/cdc/](../cdc/) — once events are in the store, CDC pushes them downstream
- [09-advanced/querying/](../querying/) — projections + data-provider repositories for read models
- [11-real-world/FullStackAddExcalibur](../../11-real-world/FullStackAddExcalibur/) — full stack using these patterns together
