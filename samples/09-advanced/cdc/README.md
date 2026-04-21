# 09-advanced/cdc — Change Data Capture

Change Data Capture patterns: reading database change streams, adapting legacy schemas, and pushing events into an event store or projection pipeline.

## Samples

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [CdcAntiCorruption](CdcAntiCorruption/) | Anti-corruption layer, schema adaptation (V1/V2/V3), CDC history backfill, data-processing framework | Docker (SQL Server) |
| [CdcEventStoreElasticsearch](CdcEventStoreElasticsearch/) | Full CQRS pipeline: CDC → event store → ES projections, `IProjectionStore<T>` vs `ElasticRepositoryBase<T>`, full-text search, aggregations, materialized views | Docker (SQL Server + Elasticsearch) |
| [CdcJobQuartz](CdcJobQuartz/) | Quartz-scheduled CDC processing job (polling fallback alternative to background service) | Docker (SQL Server, Quartz store) |

## Which Sample to Start With

- **Integrating with legacy databases?** Start with [CdcAntiCorruption](CdcAntiCorruption/) — it shows schema drift, V1→V2→V3 adaptation, and backfill of historical rows.
- **Want the end-to-end CQRS story?** Use [CdcEventStoreElasticsearch](CdcEventStoreElasticsearch/) — CDC feeds the event store, projections materialize into ES indices, and a search API demonstrates the read side.
- **Need scheduled rather than streaming CDC?** [CdcJobQuartz](CdcJobQuartz/) shows Quartz-driven CDC with distributed coordination.

## Materialized Views

The **CdcEventStoreElasticsearch** sample includes `IMaterializedViewBuilder<T>` patterns (added in Sprint 789). This is the recommended way to build CDC-driven read models — the builder tracks view position and event-stream checkpoints so rebuilds are resumable and idempotent.

## Related

- [09-advanced/persistence-patterns/](../persistence-patterns/) — where the events land after CDC ingest
- [09-advanced/querying/](../querying/) — ElasticSearch / Cosmos / Postgres repositories for the read side
- [09-advanced/deployment/JobWorkerSample](../deployment/JobWorkerSample/) — Quartz hosting pattern used by `CdcJobQuartz`
