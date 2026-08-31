---
sidebar_position: 1
title: Data Providers
description: Unified data access layer with pluggable providers for SQL, NoSQL, and cloud-native databases.
---

# Data Providers

Excalibur provides a **unified data access abstraction** across SQL, document, and cloud-native databases. Each provider implements common interfaces so your application logic remains database-agnostic while retaining access to provider-specific features.

## Before You Start

- **.NET 10.0**
- Install the core package plus your provider:
  ```bash
  dotnet add package Excalibur.Data.Abstractions
  dotnet add package Excalibur.Data.SqlServer  # or Postgres, MongoDb, etc.
  ```
- Familiarity with [data access](../data-access/index.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Architecture

```
┌─────────────────────────────────────────────────────┐
│              Application Layer                       │
│  IDataRequest<TConnection, TModel>                  │
│  IDocumentDataRequest<TConnection, TResult>         │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│         Core Abstractions                            │
│  IDb · IUnitOfWork · IDocumentDb                    │
│  IPersistenceProvider (5 core members)              │
│    ├─ IPersistenceProviderHealth (via GetService)   │
│    ├─ IPersistenceProviderConnection (via GetSvc)   │
│    └─ IPersistenceProviderTransaction (via GetSvc)  │
│  ISqlPersistenceProvider · IDocumentPersProvider     │
│  DelegatingPersistenceProvider · Builder             │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│            Provider Implementations                  │
│  SqlServer · Postgres · CosmosDb · DynamoDb         │
│  MongoDB · Redis · ElasticSearch · Firestore        │
│  InMemory                                            │
└─────────────────────────────────────────────────────┘
```

## Core Abstractions

### IDb

The fundamental database connection abstraction:

```csharp
public interface IDb
{
    IDbConnection Connection { get; }
    void Open();
    void Close();
    Task OpenAsync(CancellationToken cancellationToken); // DIM: delegates to Open()
    Task CloseAsync();                                    // DIM: delegates to Close()
}
```

### Typed Database Interfaces

Marker interfaces for registering separate database connections per store:

| Interface | Purpose |
|-----------|---------|
| `IDomainDb : IDb` | Domain event store, snapshot store |
| `ISagaDb : IDb` | Saga state persistence |
| `IOutboxDb : IDb` | Transactional outbox |
| `IProjectionDb : IDb` | Read-side projections (CQRS) |
| ~~`IDataProcessorDb`~~ | **Removed** -- use `Func<IDbConnection>` factory |
| ~~`IDataToProcessDb`~~ | **Removed** -- use `Func<IDbConnection>` factory |

See [Multi-Database Support](./multi-database.md) for registration patterns and examples.

### IUnitOfWork

Transaction management for SQL providers:

```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
```

### IDocumentDb

Document store operations for NoSQL providers. All operations require a mandatory partition key for correctness and performance (CosmosDB, DynamoDB, Firestore all require it):

```csharp
public interface IDocumentDb
{
    Task<T?> GetAsync<T>(string id, string partitionKey, CancellationToken cancellationToken) where T : class;
    Task UpsertAsync<T>(T document, string partitionKey, CancellationToken cancellationToken) where T : class;
    Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> QueryAsync<T>(string query, string partitionKey, CancellationToken cancellationToken) where T : class;
    object? GetService(Type serviceType) => null; // Escape hatch for IDocumentDbCrossPartition
}
```

Cross-partition operations (without explicit partition key) are available via `GetService`:

```csharp
public interface IDocumentDbCrossPartition
{
    Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken) where T : class;
    Task UpsertAsync<T>(T document, CancellationToken cancellationToken) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(string query, CancellationToken cancellationToken) where T : class;
}

// Usage:
var crossPartition = documentDb.GetService(typeof(IDocumentDbCrossPartition))
    as IDocumentDbCrossPartition;
```

### IDataRequest

The data request pattern decouples query definition from execution:

```csharp
public interface IDataRequest<TConnection, TModel>
{
    CommandDefinition Command { get; }
    DynamicParameters Parameters { get; }
    Func<TConnection, Task<TModel>> ResolveAsync { get; }
}
```

### IPersistenceProvider

Core provider abstraction — identity and lifecycle, with everything optional reached through `GetService`:

```csharp
public interface IPersistenceProvider : IAsyncDisposable, IDisposable
{
    string Name { get; }        // this configured instance
    string ProviderType { get; } // the family: "SQL", "Document", "KeyValue"
    Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken);
    object? GetService(Type serviceType) => null; // Escape hatch for sub-interfaces
}
```

Executing a data request is one of those optional capabilities, because it requires the store to be
reachable through an `IDbConnection` — document, key-value and search stores are not:

```csharp
public interface IDataRequestExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        IDataRequest<IDbConnection, TResult> request,
        CancellationToken cancellationToken);
}
```

Optional capabilities are accessed via `GetService(Type)`:

```csharp
// Health and diagnostics (health checks, metrics)
public interface IPersistenceProviderHealth
{
    bool IsAvailable { get; }
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
    Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken);
}

// How the store is reached, and what happens on transient failure
public interface IPersistenceProviderConnection
{
    string ConnectionString { get; }
    IDataRequestRetryPolicy RetryPolicy { get; }
}

// Ambient multi-statement transactions
public interface IPersistenceProviderTransaction
{
    Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
        IDataRequest<TConnection, TResult> request,
        ITransactionScope transactionScope,
        CancellationToken cancellationToken) where TConnection : IDisposable;
    ITransactionScope CreateTransactionScope(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        TimeSpan? timeout = null);
}

```

:::note Connection pool statistics

Pool occupancy is reported by the database drivers themselves, not by this interface. The drivers
publish it as .NET metrics for the pool your process actually owns — Npgsql emits
`db.client.connection.count` following the OpenTelemetry database conventions, `Microsoft.Data.SqlClient`
exposes its pool counters through `Microsoft.Data.SqlClient.EventSource`, and MySqlConnector ships its
own meter. Subscribe to those with `IMeterFactory` or `dotnet-counters` alongside the rest of your
telemetry.

A provider cannot report this honestly on its own behalf: the server-side views a query can reach
(`pg_stat_activity`, `sys.dm_exec_connections`) count every client of that server, including other
applications, which is a different quantity from your pool's occupancy.

:::


Connection metadata and transactions are separate capabilities because the sets of providers that can
honour them differ. Every provider holding a connection can describe it and describe its retry policy.
Only stores with an ambient, open-ended transaction model can honour a transaction scope: SQL Server,
PostgreSQL, MySQL and MongoDB do; the document and key-value providers (Cosmos DB, DynamoDB, Firestore,
Redis) do not, and decline it.

Declining means `GetService` answers `null`, which is the documented way to say no. A provider will not
hand you a transaction capability and then throw when you use it, so you can branch on the answer
before building a workflow around a scope. Stores whose atomicity has a different shape expose it
through their own surface instead -- for the cloud document providers, `ExecuteBatchAsync`, whose
signature states the partition-key and write-set constraints that an ambient scope cannot express.

`GetMetricsAsync` must return a dictionary containing a `"Provider"` entry naming the implementation
that answered, so metrics gathered from several providers can be told apart. Keys are compared
ordinally, so the casing is part of the contract -- `"provider"` is a different key. Remaining keys are
provider-specific and use PascalCase by convention.

```csharp
// Usage:
var health = provider.GetService(typeof(IPersistenceProviderHealth))
    as IPersistenceProviderHealth;
var tx = provider.GetService(typeof(IPersistenceProviderTransaction))
    as IPersistenceProviderTransaction;
```

Compose providers using the builder and decorator patterns:

```csharp
// DelegatingPersistenceProvider — decorator base (DelegatingHandler pattern)
// PersistenceProviderBuilder — ChatClientBuilder-style composition
var provider = new PersistenceProviderBuilder(innerProvider)
    .Use(inner => new TelemetryPersistenceProvider(inner))
    .Use(inner => new RetryPersistenceProvider(inner))
    .Build();
```

## Provider Hierarchy

| Interface | Extends | Used By |
|-----------|---------|---------|
| `IPersistenceProvider` | — | All providers |
| `ISqlPersistenceProvider` | `IPersistenceProvider` | SqlServer, Postgres |
| `IDocumentPersistenceProvider` | `IPersistenceProvider` | MongoDB, ElasticSearch |
| `ICloudNativePersistenceProvider` | `IDocumentPersistenceProvider` | CosmosDb, DynamoDb, Firestore |

## Available Providers

| Provider | Package | Type | Use Case |
|----------|---------|------|----------|
| [SQL Server](./sqlserver.md) | `Excalibur.Data.SqlServer` | SQL | Enterprise relational workloads |
| [Postgres](./postgres.md) | `Excalibur.Data.Postgres` | SQL | Open-source relational workloads |
| [Azure Cosmos DB](./cosmosdb.md) | `Excalibur.Data.CosmosDb` | Cloud-native | Global distribution, multi-model |
| [Amazon DynamoDB](./dynamodb.md) | `Excalibur.Data.DynamoDb` | Cloud-native | AWS serverless, key-value |
| [Google Firestore](./firestore.md) | `Excalibur.Data.Firestore` | Cloud-native | Google Cloud real-time sync |
| [MongoDB](./mongodb.md) | `Excalibur.Data.MongoDB` | Document | Flexible schema, aggregation |
| [Redis](./redis.md) | `Excalibur.Data.Redis` | Key-value | Caching, pub/sub, session state |
| [Elasticsearch](./elasticsearch.md) | `Excalibur.Data.ElasticSearch` | Search | Full-text search, analytics |
| [In-Memory](./inmemory.md) | `Excalibur.Data.InMemory` | Testing | Unit tests, development |
| [Oracle](./oracle.md) | `Excalibur.EventSourcing.Oracle` (+ Outbox/Inbox/Saga) | SQL | Event-sourcing, outbox, inbox, and saga persistence on Oracle Database |
| [Spanner](./spanner.md) | `Excalibur.Data.Spanner` | Cloud-native | Google Cloud Spanner connection foundation (retryable transactions) — **foundation only, no stores yet** |

:::note Oracle scope
The Oracle packages provide the **reliable-persistence subsystems** (event store, snapshot store, outbox, inbox, saga stores), not the `IDb` data-access layer used by the other rows above. See the [Oracle Provider](./oracle.md) page.
:::

:::note Spanner scope
`Excalibur.Data.Spanner` currently ships the **connection foundation only** — `AddSpannerDataProvider` + `ISpannerConnectionProvider` with retryable-transaction support. The event store, outbox, inbox, and saga stores are not yet available on Spanner. See the [Spanner Provider](./spanner.md) page.
:::

## Resilience

All providers support built-in resilience via the `IDataRequestRetryPolicy` interface hierarchy:

```csharp
// Core -- all providers implement this
public interface IDataRequestRetryPolicy
{
    int MaxRetryAttempts { get; }
    TimeSpan BaseRetryDelay { get; }
    bool ShouldRetry(Exception exception);
}

// Relational providers (SqlServer, Postgres, MySql) additionally implement:
public interface IRelationalDataRequestRetryPolicy : IDataRequestRetryPolicy
{
    Task<TResult> ResolveAsync<TConnection, TResult>(...);
}

// Document providers (MongoDB, Redis, InMemory) additionally implement:
public interface IDocumentDataRequestRetryPolicy : IDataRequestRetryPolicy
{
    Task<TResult> ResolveDocumentAsync<TConnection, TResult>(...);
}
```

Configure resilience options per provider:

```csharp
services.AddSqlServerPersistenceWithRetry(opts =>
{
    opts.ConnectionString = connectionString;
    opts.Resiliency.MaxRetryAttempts = 3;
    opts.Resiliency.RetryDelayMilliseconds = 1000;
});
```

## Transaction Scopes

SQL providers support distributed transactions via `ITransactionScope`:

```csharp
var scope = provider.CreateTransactionScope(IsolationLevel.ReadCommitted);
await scope.EnlistProviderAsync(provider, cancellationToken);

try
{
    await provider.ExecuteInTransactionAsync(request, scope, cancellationToken);
    await scope.CommitAsync(cancellationToken);
}
catch
{
    await scope.RollbackAsync(cancellationToken);
    throw;
}
```

## Cloud-Native Features

Cloud-native providers (CosmosDb, DynamoDb, Firestore) support:

- **Partition keys** via `IPartitionKey` for data sharding
- **Consistency options** via `IConsistencyOptions` (strong, eventual, session, bounded staleness)
- **Change feeds** via `IChangeFeedSubscription<T>` for real-time change tracking
- **Batch operations** via `ExecuteBatchAsync` for transactional multi-document writes
- **ETag-based concurrency** for optimistic concurrency control

```csharp
// Partition key example
var key = new PartitionKey("tenant-123", "/tenantId");
var result = await provider.GetByIdAsync<Order>("order-1", key, consistencyOptions: null, ct);

// Consistency options
var options = ConsistencyOptions.WithSession(sessionToken);
var query = await provider.QueryAsync<Order>(
    "SELECT * FROM c", key, parameters: null, consistencyOptions: options, ct);
```

## Default Naming Conventions

Each provider uses sensible defaults for table/collection/container names. You can override any of them via options.

| Provider | Store | Default Name | Override Property |
|----------|-------|-------------|-------------------|
| **SQL Server** | Event Store | `EventStoreEvents` | `EventStoreTable` |
| **SQL Server** | Snapshots | `EventStoreSnapshots` | `SnapshotStoreTable` |
| **SQL Server** | Outbox (unified) | `OutboxMessages` | `OutboxTableName` (via `services.AddExcalibur(x => x.AddOutbox(...))`) |
| **SQL Server** | Schema | `dbo` | `EventStoreSchema` / `SnapshotStoreSchema` |
| **PostgreSQL** | Event Store | `events` | `EventStoreTable` |
| **PostgreSQL** | Snapshots | `event_store_snapshots` | `SnapshotStoreTable` |
| **MongoDB** | Snapshots | `snapshots` | `CollectionName` |
| **Cosmos DB** | Snapshots | `snapshots` | `ContainerName` |
| **DynamoDB** | Snapshots | `Snapshots` | `TableName` |
| **Firestore** | Snapshots | `snapshots` | `CollectionName` |
| **ElasticSearch** | Projections | `{prefix}-{typename}` | `IndexName`, `IndexPrefix` |

:::tip SQL Injection Protection

SQL Server and PostgreSQL validate all schema and table names against a strict whitelist (`[a-zA-Z0-9_]`) and bracket/quote-escape them in queries. You cannot inject SQL through configuration.
:::

## What's Next

- [Multi-Database Support](./multi-database.md) -- Typed `IDb` interfaces for separate connections per store
- [SQL Server Provider](./sqlserver.md) -- Enterprise SQL workloads with full transaction support
- [Cosmos DB Provider](./cosmosdb.md) -- Global distribution and multi-model cloud-native access
- [MongoDB Provider](./mongodb.md) -- Flexible document storage with aggregation pipelines

## See Also

- [Data Access Overview](../data-access/index.md) — Repository patterns and data access abstractions using IDb and IDataRequest
- [SQL Server Provider](./sqlserver.md) — Enterprise SQL Server provider with full Dapper integration
- [Postgres Provider](./postgres.md) — Open-source Postgres provider with executor pattern
- [In-Memory Provider](./inmemory.md) — In-memory provider for unit testing and development

