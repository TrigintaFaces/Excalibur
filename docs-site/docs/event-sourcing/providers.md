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
| **SQL Server** | `Excalibur.EventSourcing.SqlServer` | `services.AddSqlServerEventSourcing(opts => opts.ConnectionString = connStr)` |
| **PostgreSQL** | `Excalibur.EventSourcing.Postgres` | `services.AddPostgresEventSourcing(opts => opts.ConnectionString = connStr)` |
| **MongoDB** | `Excalibur.Data.MongoDB` | `services.AddMongoDbSnapshotStore(opts => { ... })` |
| **Cosmos DB** | `Excalibur.Data.CosmosDb` | `services.AddCosmosDb(opts => { ... })` |
| **DynamoDB** | `Excalibur.Data.DynamoDb` | `services.AddDynamoDb(opts => { ... })` |
| **Firestore** | `Excalibur.Data.Firestore` | `services.AddFirestore(opts => { ... })` |
| **In-Memory** | `Excalibur.EventSourcing.InMemory` | `es.UseInMemory()` (builder only) |

Each `AddXxxEventSourcing()` call registers `IEventStore`, `ISnapshotStore`, and `IEventSourcedOutboxStore` for that provider.

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
    es.UseSqlServer(opts => opts.ConnectionString = connectionString)
      .AddRepository<OrderAggregate, Guid>();
});

// Or with detailed options
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlServer(options =>
    {
        options.ConnectionString = connectionString;
        options.EventStoreSchema = "es";
        options.SnapshotStoreSchema = "es";
        options.OutboxSchema = "es";
    });
});

// Alternative: Direct IServiceCollection registration
services.AddSqlServerEventSourcing(opts => opts.ConnectionString = connectionString);

// Individual stores
services.AddSqlServerEventStore(opts => opts.ConnectionString = connectionString);
services.AddSqlServerSnapshotStore(opts => opts.ConnectionString = connectionString);
services.AddSqlServerOutboxStore(opts => opts.ConnectionString = connectionString);

// With connection factory
services.AddSqlServerEventStore(() => new SqlConnection(connectionString));
services.AddSqlServerSnapshotStore(() => new SqlConnection(connectionString));
services.AddSqlServerOutboxStore(() => new SqlConnection(connectionString));

// With typed IDb marker (multi-database scenarios)
services.AddSqlServerEventStore<IOrderDb>();
services.AddSqlServerSnapshotStore<IOrderDb>();
services.AddSqlServerOutboxStore<IOrderDb>();
services.AddSqlServerEventSourcing<IOrderDb>(); // registers all three stores
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
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(opts => opts.ConnectionString = connectionString)
      .AddRepository<OrderAggregate, Guid>();
});

// Or with options
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(options =>
    {
        options.ConnectionString = connectionString;
        options.RegisterHealthChecks = true;
    });
});

// Alternative: Direct IServiceCollection registration
services.AddPostgresEventStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "events";
    options.EventsTableName = "event_store_events";  // Default
});

// With NpgsqlDataSource (recommended for connection pooling)
var dataSource = NpgsqlDataSource.Create(configuration.GetConnectionString("Postgres")!);
services.AddPostgresEventSourcing(dataSource);
```

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
    es.UsePostgres(opts =>
        opts.ConnectionString = "Host=cockroachdb.example.com;Port=26257;Database=events;...");
});

// YugabyteDB
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(opts =>
        opts.ConnectionString = "Host=yugabyte.example.com;Port=5433;Database=events;...");
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
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(options =>
    {
        options.ConnectionString = connectionString;
        options.DatabaseName = "events";
        options.ContainerName = "event-store";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(configuration.GetSection("CosmosDb"));
});

// Alternative: Direct registration
services.AddCosmosDbEventStore(options =>
{
    options.EventsContainerName = "events";
    options.PartitionKeyPath = "/streamId";  // Default
});
```

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
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "events";
    })
      .AddRepository<OrderAggregate, Guid>();
});

// Or with connection string shorthand
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB("mongodb://localhost:27017", "events")
      .AddRepository<OrderAggregate, Guid>();
});

// Alternative: Direct IServiceCollection registration
services.AddMongoDbEventStore(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "EventStore";
    options.CollectionName = "events";
});

// With custom client factory (receives IServiceProvider)
services.AddMongoDbEventStore(
    sp => sp.GetRequiredService<IMongoClient>(),
    options =>
    {
        options.DatabaseName = "EventStore";
    });
```

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

