---
sidebar_position: 8
title: Event Store Providers
description: Per-provider event store setup for SQL Server, PostgreSQL, MongoDB, Cosmos DB, DynamoDB, and Firestore.
---

# Event Store Providers

Each event store provider implements `IEventStore` with database-specific optimizations. Choose the provider that matches your database.

## Quick Start

Pick your database and copy the registration:

| Database | Package | Registration |
|----------|---------|-------------|
| **SQL Server** | `Excalibur.EventSourcing.SqlServer` | `es.UseSqlServer(sql => sql.ConnectionString(connStr))` |
| **PostgreSQL** | `Excalibur.EventSourcing.Postgres` | `es.UsePostgres(pg => pg.ConnectionString(connStr))` |
| **MongoDB** | `Excalibur.EventSourcing.MongoDB` | `es.UseMongoDB(mg => mg.ConnectionString(connStr).DatabaseName("events"))` |
| **Cosmos DB** | `Excalibur.EventSourcing.CosmosDb` | `es.UseCosmosDb(c => c.ConnectionString(connStr).DatabaseName("events"))` |
| **DynamoDB** | `Excalibur.EventSourcing.DynamoDb` | `es.UseDynamoDb(opts => { ... })` |
| **Firestore** | `Excalibur.EventSourcing.Firestore` | `es.UseFirestore(opts => { ... })` |
| **In-Memory** | `Excalibur.EventSourcing.InMemory` | `es.UseInMemory()` (builder only) |

Each `AddXxxEventSourcing()` call registers `IEventStore` and `ISnapshotStore` for that provider. Outbox is registered separately via `AddExcaliburOutbox()`.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the provider package for your database (see below)
- Familiarity with [event sourcing concepts](./concepts.md) and [event store setup](../configuration/event-store-setup.md)

## SQL Server

The primary event store provider with full transaction support.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

### Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlServer(sql => sql.ConnectionString(connectionString))
      .AddRepository<OrderAggregate, Guid>();
});

// Or with detailed options
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .EventStoreSchema("es")
           .SnapshotStoreSchema("es");
    });
});

// Individual stores
services.AddSqlServerEventStore(opts => opts.ConnectionString = connectionString);
services.AddSqlServerSnapshotStore(opts => opts.ConnectionString = connectionString);

// With connection factory
services.AddSqlServerEventStore(() => new SqlConnection(connectionString));
services.AddSqlServerSnapshotStore(() => new SqlConnection(connectionString));

// With typed IDb marker (multi-database scenarios)
services.AddSqlServerEventStore<IOrderDb>();
services.AddSqlServerSnapshotStore<IOrderDb>();
services.AddSqlServerEventSourcing<IOrderDb>(); // registers event store + snapshots

// Outbox is registered separately via the unified outbox package
services.AddExcaliburOutbox(outbox => outbox.UseSqlServer(connectionString));
```

---

## PostgreSQL

Open-source alternative with Npgsql-based access.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.Postgres
```

### Setup

```csharp
// Recommended: Fluent builder registration
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg => pg.ConnectionString(connectionString))
      .AddRepository<OrderAggregate, Guid>();
});

// With schema and table customization
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg =>
    {
        pg.ConnectionString(connectionString)
          .EventStoreSchema("events")
          .EventStoreTable("domain_events")
          .SnapshotStoreSchema("events")
          .SnapshotStoreTable("snapshots");
    });
});

// With NpgsqlDataSource (recommended for connection pooling, Azure, JSONB)
var dataSource = NpgsqlDataSource.Create(configuration.GetConnectionString("Postgres")!);
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg => pg.DataSource(dataSource))
      .AddRepository<OrderAggregate, Guid>();
});

// Named connection string (resolved from IConfiguration)
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg => pg.ConnectionStringName("EventStore"));
});
```

:::tip Connection overloads
The Postgres builder supports 5 connection methods (last-wins if multiple are called):

```csharp
// 1. Direct connection string (creates NpgsqlDataSource internally)
pg.ConnectionString(connectionString);

// 2. Named connection string (resolved from IConfiguration)
pg.ConnectionStringName("EventStore");

// 3. Bind from appsettings.json section
pg.BindConfiguration("EventSourcing:Postgres");

// 4. Pre-configured NpgsqlDataSource (Azure Managed Identity, JSONB, custom pooling)
pg.DataSource(preBuiltDataSource);

// 5. DataSource factory (receives IServiceProvider for DI-aware creation)
pg.DataSourceFactory(sp =>
{
    var builder = new NpgsqlDataSourceBuilder(connStr);
    builder.EnableDynamicJson();
    return builder.Build();
});
```

All connection paths converge to `NpgsqlDataSource` for proper connection pooling — even `ConnectionString` and `ConnectionStringName` create an `NpgsqlDataSource` internally.
:::

### Projection Store

Register a Postgres-backed projection store for read models:

```csharp
// With connection string
services.AddPostgresProjectionStore<OrderSummaryProjection>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "order_summaries"; // Optional: defaults to snake_case type name
});

// With NpgsqlDataSource (recommended for connection pooling)
services.AddPostgresProjectionStore<OrderSummaryProjection>(
    dataSourceFactory: sp => sp.GetRequiredService<NpgsqlDataSource>(),
    configureOptions: options =>
    {
        options.TableName = "order_summaries";
    });
```

`PostgresProjectionStoreOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string?` | Required | Postgres connection string |
| `TableName` | `string?` | Type name (snake_case) | Table name for projections |
| `JsonSerializerOptions` | `JsonSerializerOptions?` | camelCase, no indent | JSON serializer options for projection data |

### CockroachDB and YugabyteDB Compatibility

The Postgres provider works with **CockroachDB** and **YugabyteDB** out of the box -- both databases are PostgreSQL wire-compatible and work with Npgsql. No code changes or additional packages are needed.

```csharp
// CockroachDB
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg =>
        pg.ConnectionString("Host=cockroachdb.example.com;Port=26257;Database=events;..."));
});

// YugabyteDB
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(pg =>
        pg.ConnectionString("Host=yugabyte.example.com;Port=5433;Database=events;..."));
});
```

**Known considerations:**

| Database | Default Port | Notes |
|----------|-------------|-------|
| PostgreSQL | 5432 | Full feature support |
| CockroachDB | 26257 | Distributed SQL. `SERIALIZABLE` isolation by default (stricter than Postgres `READ COMMITTED`). |
| YugabyteDB | 5433 | Distributed SQL. Compatible with Postgres extensions. Supports `NpgsqlDataSource` pooling. |

All three use the same `Excalibur.EventSourcing.Postgres` package, DDL, and query paths. Tenant sharding (`UsePostgresTenantEventStore`) and parallel catch-up (`PostgresRangeQueryEventStore`) also work with wire-compatible databases.

:::tip
For CockroachDB, set `options.SchemaName = "public"` (CockroachDB does not support custom schemas in the same way as PostgreSQL). For YugabyteDB, the default `public` schema works as expected.
:::

---

## Azure Cosmos DB

Globally distributed event store with partition-based scaling.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.CosmosDb
```

### Setup

```csharp
// Recommended: Fluent builder registration (5 canonical connection overloads)
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString(connectionString)
              .DatabaseName("events")
              .ContainerName("event-store");
    })
    .AddRepository<OrderAggregate, Guid>();
});

// With endpoint + auth key (Azure portal credentials)
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
        cosmos.Endpoint("https://myaccount.documents.azure.com:443/", authKey)
              .DatabaseName("events"));
});

// With pre-configured CosmosClient
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
        cosmos.Client(cosmosClient).DatabaseName("events"));
});
```

:::tip Connection overloads
The CosmosDb builder supports 5 connection methods (last-wins if multiple are called):

```csharp
// 1. Connection string
cosmos.ConnectionString(connectionString);

// 2. Endpoint + auth key (Azure portal)
cosmos.Endpoint("https://myaccount.documents.azure.com:443/", authKey);

// 3. Pre-configured CosmosClient instance
cosmos.Client(existingCosmosClient);

// 4. DI-aware client factory
cosmos.ClientFactory(sp => sp.GetRequiredService<CosmosClient>());

// 5. Bind from appsettings.json section
cosmos.BindConfiguration("EventSourcing:CosmosDb");
```

`CosmosClient` is registered as a singleton — it's thread-safe and expensive to create.
:::

### Partition Strategy

Cosmos DB event stores partition by aggregate ID. Each aggregate's events are stored in a single logical partition for transactional consistency.

---

## Amazon DynamoDB

Serverless event store for AWS workloads.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.DynamoDb
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseDynamoDb(options =>
    {
        options.TableName = "event-store";
        options.Region = "us-east-1";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseDynamoDb(configuration.GetSection("DynamoDb"));
});

// Alternative: Direct registration
services.AddDynamoDbEventStore(options =>
{
    options.EventsTableName = "Events";
});
```

### Key Schema

DynamoDB event stores use the aggregate ID as the partition key and event version as the sort key, providing efficient sequential reads per aggregate.

---

## Google Firestore

Real-time event store for Google Cloud workloads.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.Firestore
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseFirestore(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CollectionName = "events";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseFirestore(configuration.GetSection("Firestore"));
});

// Alternative: Direct registration
services.AddFirestoreEventStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.EventsCollectionName = "events";
});
```

### Collection Structure

Firestore event stores use subcollections under aggregate documents, leveraging Firestore's hierarchical document model.

---

## MongoDB

Document-oriented event store with flexible schema and horizontal scaling via sharding.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.MongoDB
```

### Setup

```csharp
// Recommended: Fluent builder registration (4 canonical connection overloads)
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB(mg =>
    {
        mg.ConnectionString("mongodb://localhost:27017")
          .DatabaseName("events")
          .CollectionName("event_store_events");
    })
    .AddRepository<OrderAggregate, Guid>();
});

// With pre-configured IMongoClient
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB(mg => mg.Client(mongoClient).DatabaseName("events"))
      .AddRepository<OrderAggregate, Guid>();
});

// With DI-aware client factory
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB(mg =>
        mg.ClientFactory(sp => sp.GetRequiredService<IMongoClient>())
          .DatabaseName("events"));
});
```

:::tip Connection overloads
The MongoDB builder supports 4 connection methods (last-wins if multiple are called):

```csharp
// 1. Connection string (creates IMongoClient singleton internally)
mg.ConnectionString("mongodb://localhost:27017");

// 2. Pre-configured IMongoClient instance
mg.Client(existingMongoClient);

// 3. DI-aware client factory
mg.ClientFactory(sp => sp.GetRequiredService<IMongoClient>());

// 4. Bind from appsettings.json section
mg.BindConfiguration("EventSourcing:MongoDB");
```

`IMongoClient` is registered as a singleton — it's thread-safe and expensive to create.
:::

### Document Model

MongoDB event stores use a single collection per aggregate type with the aggregate ID as the document key. Events are stored as embedded arrays within the aggregate document.

---

## SQLite (Local Development)

Zero-Docker local development and testing. Auto-creates tables on first use.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.Sqlite
```

### Setup

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlite(options =>
    {
        options.ConnectionString = "Data Source=events.db";
    });
});
```

Registers both `IEventStore` and `ISnapshotStore` backed by SQLite.

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | Required | SQLite connection string (e.g., `Data Source=events.db`) |
| `EventStoreTable` | `"Events"` | Table name for events |
| `SnapshotStoreTable` | `"Snapshots"` | Table name for snapshots |

:::tip When to use SQLite
SQLite is ideal for **local development**, **quick prototyping**, and **unit/integration tests** where you want a real database without Docker. For production workloads, use SQL Server, PostgreSQL, or a cloud provider.
:::

---

## In-Memory (Testing)

For unit and integration tests:

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseInMemory()
      .AddRepository<OrderAggregate, Guid>();
});

// Alternative: Direct registration
services.AddInMemoryEventStore();
```

---

## Provider Comparison

| Provider | Package | Transaction Support | Scaling Model |
|----------|---------|-------------------|---------------|
| SQL Server | `Excalibur.EventSourcing.SqlServer` | Full ACID | Vertical + read replicas |
| PostgreSQL | `Excalibur.EventSourcing.Postgres` | Full ACID | Vertical + read replicas |
| MongoDB | `Excalibur.EventSourcing.MongoDB` | Document-level | Sharding |
| Cosmos DB | `Excalibur.EventSourcing.CosmosDb` | Partition-scoped | Global distribution |
| DynamoDB | `Excalibur.EventSourcing.DynamoDb` | Item-level | On-demand / provisioned |
| Firestore | `Excalibur.EventSourcing.Firestore` | Document-level | Automatic |
| SQLite | `Excalibur.EventSourcing.Sqlite` | Full ACID (single-writer) | Single process |
| In-Memory | `Excalibur.EventSourcing.InMemory` | None | Single process |

## Batch Projection Registration

When registering multiple projections for the same provider, use the batch registrar API instead of individual `AddXxxProjectionStore<T>()` calls:

```csharp
// SQL Server: register multiple projections sharing the same connection
services.AddSqlServerProjections(connectionString, projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.TableName = "CustomerViews");
});

// MongoDB
services.AddMongoDbProjections(connectionString, "MyApp", projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.CollectionName = "customers");
});

// CosmosDB
services.AddCosmosDbProjections(connectionString, "MyDatabase", projections =>
{
    projections.Add<OrderSummary>();
});

// PostgreSQL
services.AddPostgresProjections(connectionString, projections =>
{
    projections.Add<OrderSummary>();
});

// ElasticSearch
services.AddElasticSearchProjections("https://es.example.com:9200", projections =>
{
    projections.Add<OrderSummary>();
});
```

See [Data Providers](../data-providers/index.md) for provider-specific details and naming conventions.

## Cold Event Store Providers (Tiered Storage)

For hot/cold storage separation at petabyte scale, archived events are moved from the primary (hot) store to a cold store in blob/object storage. All cold store providers implement `IColdEventStore` (4 methods: `WriteAsync`, `ReadAsync`, `ReadAsync(fromVersion)`, `HasArchivedEventsAsync`) and use a gzip-compressed JSON format.

### Azure Blob Storage

```bash
dotnet add package Excalibur.EventSourcing.AzureBlob
```

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseAzureBlobColdEventStore(opts =>
    {
        opts.ConnectionString = "DefaultEndpointsProtocol=https;...";
        opts.ContainerName = "event-archive";
        opts.BlobPrefix = "events";
    });
});
```

### AWS S3

```bash
dotnet add package Excalibur.EventSourcing.AwsS3
```

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseAwsS3ColdEventStore(opts =>
    {
        opts.BucketName = "my-event-archive";
        opts.Region = "us-east-1";
        opts.KeyPrefix = "events";
    });
});
```

### Google Cloud Storage

```bash
dotnet add package Excalibur.EventSourcing.Gcs
```

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseGcsColdEventStore(opts =>
    {
        opts.BucketName = "my-event-archive";
        opts.ObjectPrefix = "events";
    });
});
```

### Cold Store Comparison

| Provider | Package | Authentication |
|----------|---------|----------------|
| **Azure Blob** | `Excalibur.EventSourcing.AzureBlob` | Connection string or DefaultAzureCredential |
| **AWS S3** | `Excalibur.EventSourcing.AwsS3` | AWS SDK default credential chain |
| **GCS** | `Excalibur.EventSourcing.Gcs` | Google Application Default Credentials |

All providers store events as `{prefix}/{aggregateId}/events.json.gz` and support merge-on-write (read existing, append new, write back).

### Archive Metrics

Meter: `Excalibur.EventSourcing.Archive`

| Metric | Type | Description |
|--------|------|-------------|
| `excalibur.eventsourcing.archive.events_archived` | Counter | Events moved to cold storage |
| `excalibur.eventsourcing.archive.events_deleted` | Counter | Events removed from hot store |
| `excalibur.eventsourcing.archive.cold_reads` | Counter | Read-through operations from cold |
| `excalibur.eventsourcing.archive.errors` | Counter | Archive operation failures |
| `excalibur.eventsourcing.archive.duration_seconds` | Histogram | Batch archive duration |

## See Also

- [Event Sourcing Overview](./index.md) -- Architecture and core abstractions
- [Event Store](./event-store.md) -- `IEventStore` interface details
- [Snapshots](./snapshots.md) -- Snapshot store configuration
- [Change Data Capture](../patterns/cdc.md) -- CDC patterns and provider support

