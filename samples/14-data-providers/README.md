# 14 - Data Providers

Samples demonstrating Excalibur's data provider integrations. Each sample showcases the full capability surface of its provider.

## ElasticSearch

| Sample | What It Teaches |
|--------|----------------|
| [ElasticSearch-GettingStarted](ElasticSearch-GettingStarted/) | DI registration, repository pattern, index initialization, basic CRUD |
| [ElasticSearch-Querying](ElasticSearch-Querying/) | Search DSL: term, match, bool, range, wildcard queries; sorting; aggregations |
| [ElasticSearch-Paging](ElasticSearch-Paging/) | Offset paging (From/Size) and cursor-based deep pagination (SearchAfter) |
| [ElasticSearch-IndexManagement](ElasticSearch-IndexManagement/) | Index creation, templates, ILM policies, aliases, rollover |
| [ElasticSearch-Resilience](ElasticSearch-Resilience/) | Retry policies, circuit breaker, monitored client, health checks |
| [ElasticSearch-Projections](ElasticSearch-Projections/) | CQRS read model store via IProjectionStore, named options, query/count |
| [ElasticSearch-InboxOutbox](ElasticSearch-InboxOutbox/) | Idempotent inbox and transactional outbox backed by ElasticSearch |

## Cloud Document Stores

| Sample | Provider | Key Features |
|--------|----------|-------------|
| [CosmosDb](CosmosDb/) | Azure Cosmos DB | Partition keys, multi-region, change feed, batch execution, consistency levels |
| [DynamoDb](DynamoDb/) | AWS DynamoDB | Streams, DAX caching, consistent reads, sort keys |
| [Firestore](Firestore/) | Google Firestore | Emulator support, real-time listeners, flexible credentials |

## Document Databases

| Sample | Provider | Key Features |
|--------|----------|-------------|
| [MongoDB](MongoDB/) | MongoDB | Aggregation pipeline, transactions, connection pooling |
| [OpenSearch](OpenSearch/) | OpenSearch | Multi-node cluster, resilience, dead letter handling |

## Relational Databases

| Sample | Provider | Key Features |
|--------|----------|-------------|
| [Postgres](Postgres/) | PostgreSQL | Dapper, dead letter store, JSONB, prepared statements, NpgsqlDataSource |
| [MySql](MySql/) | MySQL | Dapper, connection pooling, SSL, retry policy |

## Key-Value / Cache

| Sample | Provider | Key Features |
|--------|----------|-------------|
| [Redis](Redis/) | Redis | Transactions, database selection, connection pooling, retry policy |

## Prerequisites

Each sample includes a README with Docker commands for its infrastructure dependency. All samples are standalone and independently runnable.
