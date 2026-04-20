# 09-advanced/querying — Projections, Streaming, and Data-Provider Repositories

Read-side patterns: projections of event streams, streaming handlers with backpressure, pipeline validation, and a full set of data-provider repositories (ElasticSearch, CosmosDb, DynamoDb, Firestore, MongoDB, OpenSearch, Postgres, MySql, Redis).

## Streaming & Validation

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [StreamingHandlers](StreamingHandlers/) | `IAsyncEnumerable` streaming, backpressure, progress reporting, 4 handler types | None |
| [FluentValidationSample](FluentValidationSample/) | Pipeline validation, conditional rules, async validators, cross-field constraints | None |

## ElasticSearch

| Sample | What It Teaches |
|--------|-----------------|
| [ElasticSearch-GettingStarted](ElasticSearch-GettingStarted/) | DI registration, repository pattern, index initialization, basic CRUD |
| [ElasticSearch-Querying](ElasticSearch-Querying/) | Search DSL: term, match, bool, range, wildcard queries; sorting; aggregations |
| [ElasticSearch-Paging](ElasticSearch-Paging/) | Offset paging (From/Size) and cursor-based deep pagination (SearchAfter) |
| [ElasticSearch-IndexManagement](ElasticSearch-IndexManagement/) | Index creation, templates, ILM policies, aliases, rollover |
| [ElasticSearch-Resilience](ElasticSearch-Resilience/) | Retry policies, circuit breaker, monitored client, health checks |
| [ElasticSearch-Projections](ElasticSearch-Projections/) | CQRS read model store via `IProjectionStore`, named options, query/count |
| [ElasticSearch-InboxOutbox](ElasticSearch-InboxOutbox/) | Idempotent inbox and transactional outbox backed by ElasticSearch |

## Cloud Document Stores

| Sample | Provider | Key Features |
|--------|----------|--------------|
| [CosmosDb](CosmosDb/) | Azure Cosmos DB | Partition keys, multi-region, change feed, batch execution, consistency levels |
| [DynamoDb](DynamoDb/) | AWS DynamoDB | Streams, DAX caching, consistent reads, sort keys |
| [Firestore](Firestore/) | Google Firestore | Emulator support, real-time listeners, flexible credentials |

## Document Databases

| Sample | Provider | Key Features |
|--------|----------|--------------|
| [MongoDB](MongoDB/) | MongoDB | Aggregation pipeline, transactions, connection pooling |
| [OpenSearch](OpenSearch/) | OpenSearch | Multi-node cluster, resilience, dead letter handling |

## Relational Databases

| Sample | Provider | Key Features |
|--------|----------|--------------|
| [Postgres](Postgres/) | PostgreSQL | Dapper, dead letter store, JSONB, prepared statements, `NpgsqlDataSource` |
| [MySql](MySql/) | MySQL | Dapper, connection pooling, SSL, retry policy |

## Key-Value / Cache

| Sample | Provider | Key Features |
|--------|----------|--------------|
| [Redis](Redis/) | Redis | Transactions, database selection, connection pooling, retry policy |

## Learning Path

1. **[StreamingHandlers](StreamingHandlers/)** — understand streaming/backpressure before you start pushing high-volume read models
2. **[FluentValidationSample](FluentValidationSample/)** — fail-fast validation at the pipeline boundary
3. **[ElasticSearch-GettingStarted](ElasticSearch-GettingStarted/)** — easiest path to a real read model
4. **[ElasticSearch-Projections](ElasticSearch-Projections/)** + **[ElasticSearch-Querying](ElasticSearch-Querying/)** — the full CQRS read side
5. **[CosmosDb](CosmosDb/)** / **[DynamoDb](DynamoDb/)** / **[MongoDB](MongoDB/)** — pick your cloud doc store
6. **[Postgres](Postgres/)** / **[MySql](MySql/)** — relational read models via Dapper

## Infrastructure Notes

- Every sample with Docker requirements ships a `docker-compose.yml`.
- Every repository provider supports resilience policies, OpenTelemetry instrumentation, and health checks (demonstrated in `ElasticSearch-Resilience`).
- Paging patterns differ by provider — see the sample that matches your provider for the idiomatic approach.

## Related

- [09-advanced/persistence-patterns/](../persistence-patterns/) — the event store that feeds these projections
- [09-advanced/cdc/](../cdc/) — CDC pipelines that populate read models
