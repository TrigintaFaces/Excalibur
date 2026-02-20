---
sidebar_position: 1
title: Data Providers
description: Unified data access layer with pluggable providers for SQL, NoSQL, and cloud-native databases.
---

# Data Providers

Excalibur provides a **unified data access abstraction** across SQL, document, and cloud-native databases. Each provider implements common interfaces so your application logic remains database-agnostic while retaining access to provider-specific features.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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
| `IDataProcessorDb : IDb` | Data processing pipeline |
| `IDataToProcessDb : IDb` | Records awaiting processing |

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

Core provider abstraction — focused on data request execution with optional capabilities via sub-interfaces:

```csharp
public interface IPersistenceProvider : IAsyncDisposable, IDisposable
{
    string Name { get; }
    string ProviderType { get; }
    Task<TResult> ExecuteAsync<TConnection, TResult>(
        IDataRequest<TConnection, TResult> request,
        CancellationToken cancellationToken) where TConnection : IDisposable;
    Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken);
    object? GetService(Type serviceType) => null; // Escape hatch for sub-interfaces
}
```

Optional capabilities are accessed via `GetService(Type)`:

```csharp
// Health and diagnostics (health checks, metrics, pool stats)
public interface IPersistenceProviderHealth
{
    bool IsAvailable { get; }
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
    Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken);
    Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken);
}

// Transaction coordination (connection string, retry, transactions)
public interface IPersistenceProviderTransaction
{
    string ConnectionString { get; }
    IDataRequestRetryPolicy RetryPolicy { get; }
    Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
        IDataRequest<TConnection, TResult> request,
        ITransactionScope transactionScope,
        CancellationToken cancellationToken) where TConnection : IDisposable;
    ITransactionScope CreateTransactionScope(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        TimeSpan? timeout = null);
}

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

## Resilience

All providers support built-in resilience via `IDataRequestRetryPolicy`:

```csharp
public interface IDataRequestRetryPolicy
{
    int MaxRetryAttempts { get; }
    TimeSpan BaseRetryDelay { get; }
    bool ShouldRetry(Exception exception);
}
```

Configure resilience options per provider:

```csharp
services.AddSqlServerPersistenceWithRetry(
    connectionString,
    maxRetryAttempts: 3,
    retryDelayMilliseconds: 1000);
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

## What's Next

- [Multi-Database Support](./multi-database.md) — Typed `IDb` interfaces for separate connections per store
- [SQL Server Provider](./sqlserver.md) — Enterprise SQL workloads with full transaction support
- [Cosmos DB Provider](./cosmosdb.md) — Global distribution and multi-model cloud-native access
- [MongoDB Provider](./mongodb.md) — Flexible document storage with aggregation pipelines

## See Also

- [Data Access Overview](../data-access/index.md) — Repository patterns and data access abstractions using IDb and IDataRequest
- [SQL Server Provider](./sqlserver.md) — Enterprise SQL Server provider with full Dapper integration
- [Postgres Provider](./postgres.md) — Open-source Postgres provider with executor pattern
- [In-Memory Provider](./inmemory.md) — In-memory provider for unit testing and development

