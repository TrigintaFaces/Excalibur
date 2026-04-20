# 09-advanced — Advanced Patterns

Production-grade patterns for event sourcing, CDC integration, read-side projections, distributed coordination, scheduled jobs, and schema evolution.

## Prerequisites

These samples build on the fundamentals from [01-getting-started/](../01-getting-started/). If you haven't worked through [EventSourcingIntro](../01-getting-started/EventSourcingIntro/) yet, start there.

## Subcategory Map

`09-advanced/` is split into five focused subcategories. Pick the one that matches what you're building:

| Subcategory | What It Covers | Typical Reader |
|-------------|----------------|----------------|
| [persistence-patterns/](persistence-patterns/) | Event stores (SQL Server, Cosmos, in-memory), snapshots, inbox, transactional handlers, multi-database, session state, multi-tenant sharding, cloud-storage snapshots | You're designing the write side of an event-sourced system |
| [cdc/](cdc/) | Change Data Capture, anti-corruption layer, full CQRS pipelines (CDC → event store → projections), Quartz-scheduled CDC | You're integrating with legacy databases or building CQRS end-to-end |
| [querying/](querying/) | Projections, streaming handlers with backpressure, validation, and 15+ data-provider repositories (ElasticSearch, CosmosDb, DynamoDb, Firestore, MongoDB, OpenSearch, Postgres, MySql, Redis) | You're designing the read side / picking a storage provider |
| [deployment/](deployment/) | Background services, leader election, Quartz job workers, production middleware pipelines, test harnesses | You're running the system in production |
| [advanced/](advanced/) | Event versioning and schema evolution (4 scenarios: domain, ecommerce, integration, GDPR) | You're designing for long-term schema change |

## Recommended Learning Tracks

### Event Sourcing Track (start here for CQRS)

1. [persistence-patterns/ProjectionsSample](persistence-patterns/ProjectionsSample/) — in-memory projections, no infrastructure
2. [persistence-patterns/SqlServerEventStore](persistence-patterns/SqlServerEventStore/) — real persistence, aggregate rehydration
3. [persistence-patterns/SnapshotStrategies](persistence-patterns/SnapshotStrategies/) — performance optimization
4. [cdc/CdcEventStoreElasticsearch](cdc/CdcEventStoreElasticsearch/) — full CQRS: CDC + projections + ES search + API

### CDC / Legacy Integration Track

1. [cdc/CdcAntiCorruption](cdc/CdcAntiCorruption/) — schema adaptation, backfill, history gap recovery
2. [cdc/CdcEventStoreElasticsearch](cdc/CdcEventStoreElasticsearch/) — full pipeline: CDC → event store → ES projections
3. [cdc/CdcJobQuartz](cdc/CdcJobQuartz/) — scheduled CDC alternative (Quartz)

### Read Side / Data Provider Track

1. [querying/StreamingHandlers](querying/StreamingHandlers/) — backpressure and progress reporting
2. [querying/ElasticSearch-GettingStarted](querying/ElasticSearch-GettingStarted/) — the simplest real read model
3. [querying/ElasticSearch-Projections](querying/ElasticSearch-Projections/) — CQRS read model with `IProjectionStore`
4. Pick the provider sample matching your production choice (CosmosDb / MongoDB / Postgres / etc.)

### Versioning & Schema Evolution Track

1. [advanced/Versioning.Examples/EventUpcasting](advanced/Versioning.Examples/EventUpcasting/) — BFS-based V1→V2→V3 aggregate replay
2. [advanced/Versioning.Examples/EcommerceOrderVersioning](advanced/Versioning.Examples/EcommerceOrderVersioning/) — multi-hop event transforms
3. [advanced/Versioning.Examples/IntegrationEventVersioning](advanced/Versioning.Examples/IntegrationEventVersioning/) — cross-service compatibility
4. [advanced/Versioning.Examples/UserProfileVersioning](advanced/Versioning.Examples/UserProfileVersioning/) — GDPR-aware schema evolution

### Background Processing & Production Track

1. [deployment/BackgroundServices](deployment/BackgroundServices/) — 4 hosting patterns
2. [deployment/JobWorkerSample](deployment/JobWorkerSample/) — Quartz scheduling, persistent store, Redis coordination
3. [deployment/LeaderElection](deployment/LeaderElection/) — singleton guarantees across replicas
4. [deployment/ProductionPipeline](deployment/ProductionPipeline/) — full middleware stack

## Running Samples

```bash
# Most samples (no infrastructure needed)
dotnet run --project samples/09-advanced/persistence-patterns/ProjectionsSample

# Samples with Docker dependencies
cd samples/09-advanced/persistence-patterns/SqlServerEventStore
docker-compose up -d    # Start infrastructure
dotnet run              # Run the sample

cd samples/09-advanced/cdc/CdcEventStoreElasticsearch
docker-compose up -d    # SQL Server + Elasticsearch
dotnet run              # Web API at http://localhost:5000
```

## Prerequisites

| Requirement | Needed By |
|-------------|-----------|
| .NET 9.0 SDK | All samples |
| Docker Desktop | `SqlServerEventStore`, `LeaderElection`, `CdcAntiCorruption`, `CdcEventStoreElasticsearch`, some `BackgroundServices` |
| Cosmos DB Emulator | `CosmosDbEventStore` |

## Next Steps

- [../11-real-world/](../11-real-world/) — production-style samples combining multiple patterns
- [Event Sourcing Documentation](../../docs-site/docs/event-sourcing/)
- [CDC Pattern](../../docs-site/docs/patterns/cdc.md)
