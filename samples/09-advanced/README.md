# Advanced Samples

Production-grade patterns for event sourcing, CDC integration, distributed coordination, and background processing.

## Where to Start

These samples build on the fundamentals from [01-getting-started/](../01-getting-started/). If you haven't worked through [EventSourcingIntro](../01-getting-started/EventSourcingIntro/) yet, start there.

### Learning Tracks

Pick the track that matches what you're building:

```
Event Sourcing Track (start here for CQRS)
  1. ProjectionsSample .............. In-memory projections, no infrastructure needed
  2. SqlServerEventStore ............ Real persistence, aggregate rehydration
  3. SnapshotStrategies ............. Performance optimization for loaded aggregates
  4. CdcEventStoreElasticsearch ..... Full CQRS: CDC + projections + ES search + API

CDC / Legacy Integration Track
  1. CdcAntiCorruption .............. Schema adaptation, backfill, history gap recovery
  2. CdcEventStoreElasticsearch ..... Full pipeline: CDC -> event store -> ES projections

Versioning & Schema Evolution Track
  1. Versioning.Examples/ ........... 4 sub-projects covering all versioning scenarios:
     - EventUpcasting ............... BFS-based V1->V2->V3 aggregate replay
     - EcommerceOrderVersioning ..... Order event evolution patterns
     - IntegrationEventVersioning ... Cross-service message compatibility
     - UserProfileVersioning ........ GDPR-aware schema evolution

Background Processing Track
  1. BackgroundServices ............. 4 hosting patterns (at-least-once, transactional, etc.)
  2. JobWorkerSample ................ Quartz scheduling, Redis coordination, health checks
  3. ../13-jobs/CdcJobQuartz ........ Quartz-based CDC processing (scheduled alternative)
```

## Event Sourcing

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [ProjectionsSample](ProjectionsSample/) | CQRS read models, inline projections, multi-stream projections, checkpoint tracking, rebuild patterns | None (in-memory) |
| [SqlServerEventStore](SqlServerEventStore/) | SQL Server persistence, aggregate rehydration, direct event store access, configuration | Docker (SQL Server) |
| [CosmosDbEventStore](CosmosDbEventStore/) | Cosmos DB partitioning, change feed, global distribution | Cosmos DB Emulator or Azure |
| [SnapshotStrategies](SnapshotStrategies/) | Interval, time-based, size-based, composite snapshot strategies, tuning guide | None (in-memory) |

### Provider Selection Guide

| Provider | Best For | Consistency | Scaling |
|----------|----------|-------------|---------|
| **SQL Server** | Enterprise, ACID transactions | Strong | Vertical |
| **Cosmos DB** | Global distribution, high throughput | Tunable | Horizontal |
| **In-Memory** | Testing, development, learning | Strong | N/A |

## CDC & Legacy Integration

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [CdcAntiCorruption](CdcAntiCorruption/) | Anti-corruption layer, schema adaptation (V1/V2/V3), CDC history backfill, data processing framework | Docker (SQL Server) |
| [CdcEventStoreElasticsearch](CdcEventStoreElasticsearch/) | Full CQRS pipeline: CDC -> event store -> ES projections, `IProjectionStore<T>` vs `ElasticRepositoryBase<T>`, full-text search, aggregations | Docker (SQL Server + Elasticsearch) |

**Which CDC sample?**
- **Start with CdcAntiCorruption** if you need to integrate with legacy databases and want focused ACL patterns
- **Start with CdcEventStoreElasticsearch** if you want the full end-to-end CQRS story with search

## Versioning & Schema Evolution

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [Versioning.Examples/](Versioning.Examples/) | 4 sub-projects covering all event versioning scenarios | None (in-memory) |

Sub-projects:

| Sub-project | Scenario | Key Patterns |
|-------------|----------|-------------|
| EventUpcasting | Domain event V1->V2->V3 with aggregate replay | BFS path finding, auto-upcasting |
| EcommerceOrderVersioning | Order event evolution with multi-hop transforms | Schema splitting (Total -> Subtotal + Tax) |
| IntegrationEventVersioning | Cross-service message compatibility | UpcastingMessageBusDecorator, migration detection |
| UserProfileVersioning | GDPR-focused schema evolution | Consent tracking, email encryption, assembly scanning |

## Distributed Coordination

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [LeaderElection](LeaderElection/) | Redis-based leader election, TTL leases, failover callbacks | Docker (Redis) |
| [SessionManagement](SessionManagement/) | Session-aware message processing, state tracking | None (in-memory) |

## Validation

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [FluentValidationSample](FluentValidationSample/) | Pipeline validation, conditional rules, async validators, cross-field constraints | None |

## Background Processing

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [BackgroundServices](BackgroundServices/) | 4 hosting patterns: at-least-once inbox, transactional, minimized window, basic polling | Varies by sub-project |
| [JobWorkerSample](JobWorkerSample/) | Quartz scheduling, persistent store, Redis coordination, CDC/outbox/data processing jobs, health checks | Docker (SQL Server, Redis) |

> **Looking for Quartz-based CDC scheduling?** See [13-jobs/CdcJobQuartz](../13-jobs/CdcJobQuartz/).

## Other Advanced Patterns

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [StreamingHandlers](StreamingHandlers/) | `IAsyncEnumerable` streaming, backpressure, progress reporting, 4 handler types | None |
| [InboxIdempotency](InboxIdempotency/) | At-least-once delivery with deduplication guarantees | None (in-memory) |
| [TransactionalHandlers](TransactionalHandlers/) | ACID handler execution with TransactionScope | None (in-memory) |
| [MultiDatabase](MultiDatabase/) | Event sourcing across multiple database systems | Docker |
| [ProductionPipeline](ProductionPipeline/) | Full middleware stack: security, validation, transactions, observability | None (in-memory) |
| [DataProcessingBackgroundService](DataProcessingBackgroundService/) | Background data processing with retry and error handling | None |

## Running Samples

```bash
# Most samples (no infrastructure needed)
dotnet run --project samples/09-advanced/ProjectionsSample

# Samples with Docker dependencies
cd samples/09-advanced/SqlServerEventStore
docker-compose up -d    # Start infrastructure
dotnet run              # Run the sample

cd samples/09-advanced/CdcEventStoreElasticsearch
docker-compose up -d    # SQL Server + Elasticsearch
dotnet run              # Web API at http://localhost:5000
```

## Prerequisites

| Requirement | Needed By |
|-------------|-----------|
| .NET 9.0 SDK | All samples |
| Docker Desktop | SqlServerEventStore, LeaderElection, CdcAntiCorruption, CdcEventStoreElasticsearch, BackgroundServices (some) |
| Cosmos DB Emulator | CosmosDbEventStore |

## Next Steps

- [10-real-world/](../10-real-world/) -- Production-style samples combining multiple patterns
- [Event Sourcing Documentation](../../docs-site/docs/event-sourcing/) -- Comprehensive guides
- [CDC Pattern](../../docs-site/docs/patterns/cdc.md) -- Change Data Capture reference
