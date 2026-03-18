---
sidebar_position: 12
title: Multi-Database Support
description: Use typed IDb interfaces and per-processor connection factories to register separate database connections for event stores, sagas, outbox, projections, and data processors.
---

# Multi-Database Support

Excalibur provides **typed marker interfaces** derived from `IDb` that let you register separate database connections for different stores. This is useful when your event store, saga state, outbox messages, and read-side projections live on different databases.

For data processing pipelines, Excalibur uses a different approach: **per-processor `Func<IDbConnection>` injection** with .NET 8 keyed services. See [Data Processing Multi-Database](#data-processing-multi-database) below.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Two or more data provider packages installed
- Familiarity with [IDb interface](../data-access/idb-interface.md) and [dependency injection](../core-concepts/dependency-injection.md)

## The Problem

In a simple setup, all stores share a single database connection:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(connectionString)));
```

This works when everything is in one database. But in larger deployments you may want:

- **Write database** for domain events and snapshots
- **Read database** for projections (CQRS read side)
- **Separate database** for saga state (isolate long-running processes)
- **Separate database** for the outbox (isolate transactional messaging)
- **Separate databases** for data processors (each processor reads from its own source)

Without typed interfaces, you'd have to register multiple `IDbConnection` instances and somehow distinguish them -- leading to error-prone string-keyed or factory-based approaches.

## Typed IDb Interfaces

Excalibur solves this with marker interfaces that extend `IDb`. Each interface is an empty type marker used purely for DI resolution:

| Interface | Package | Purpose | Used By |
|-----------|---------|---------|---------|
| `IDomainDb` | `Excalibur.Data` | Domain event store, snapshot store | `SqlServerEventStore`, `SqlServerSnapshotStore` |
| `ISagaDb` | `Excalibur.Data` | Saga state persistence | `SqlServerSagaStore`, `PostgresSagaStore` |
| `IOutboxDb` | `Excalibur.Data` | Transactional outbox | `SqlServerOutboxStore` |
| `IProjectionDb` | `Excalibur.Data` | SQL read-side projections | `SqlServerProjectionStore`, `PostgresProjectionStore` |
| ~~`IDataProcessorDb`~~ | ~~`Excalibur.Data.DataProcessing`~~ | **Removed in Sprint 657** -- use `Func<IDbConnection>` factory instead |
| ~~`IDataToProcessDb`~~ | ~~`Excalibur.Data.DataProcessing`~~ | **Removed in Sprint 657** -- use `Func<IDbConnection>` factory instead |
| `IDocumentDb` | `Excalibur.Data.Abstractions` | Cloud-native document databases | CosmosDB, DynamoDB, MongoDB, Firestore |

All SQL-based interfaces inherit from `IDb`:

```csharp
public interface IDomainDb : IDb;    // Domain events + snapshots
public interface ISagaDb : IDb;      // Saga state
public interface IOutboxDb : IDb;    // Outbox messages
public interface IProjectionDb : IDb; // SQL read-side projections
```

:::note
The typed `IDb` interfaces are for **SQL databases** that use `IDbConnection` (SQL Server, Postgres). Document databases (Elasticsearch, CosmosDB, MongoDB, Firestore) use their own SDK clients and are configured through `IOptions<T>` instead -- see [Document Database Projections](#document-database-projections) below.
:::

## Single Database (Default)

When all stores share one database, register only `IDomainDb`:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(connectionString)));
```

Stores that accept a `Func<SqlConnection>` factory will use this connection:

```csharp
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IDomainDb>();
    return new SqlServerEventStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>());
});
```

## Multi-Database Setup

Register each typed interface pointing to a different connection string:

```csharp
var writeDb = builder.Configuration.GetConnectionString("WriteDb");
var readDb = builder.Configuration.GetConnectionString("ReadDb");
var sagaDb = builder.Configuration.GetConnectionString("SagaDb");
var outboxDb = builder.Configuration.GetConnectionString("OutboxDb");

// Write side: domain events and snapshots
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeDb)));

// Read side: projections (CQRS pattern)
builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new SqlConnection(readDb)));

// Saga state: separate for isolation
builder.Services.AddScoped<ISagaDb>(_ =>
    new SagaDb(new SqlConnection(sagaDb)));

// Outbox: separate for transactional messaging
builder.Services.AddScoped<IOutboxDb>(_ =>
    new OutboxDb(new SqlConnection(outboxDb)));
```

Then wire up each store to its typed interface:

```csharp
// Event store uses IDomainDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IDomainDb>();
    return new SqlServerEventStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>());
});

// Projection store uses IProjectionDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IProjectionDb>();
    return new SqlServerProjectionStore<OrderReadModel>(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerProjectionStore<OrderReadModel>>>());
});

// Saga store uses ISagaDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<ISagaDb>();
    return new SqlServerSagaStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerSagaStore>>());
});

// Outbox store uses IOutboxDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IOutboxDb>();
    return new SqlServerOutboxStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerOutboxStore>>());
});
```

### Configuration

```json
{
  "ConnectionStrings": {
    "WriteDb": "Server=write-primary;Database=Domain;...",
    "ReadDb": "Server=read-replica;Database=Projections;...",
    "SagaDb": "Server=write-primary;Database=Sagas;...",
    "OutboxDb": "Server=write-primary;Database=Outbox;..."
  }
}
```

## Adapter Pattern

Each concrete class (`DomainDb`, `SagaDb`, `OutboxDb`, `ProjectionDb`) extends the abstract `Db` base class, which manages connection lifecycle:

```csharp
// All adapters follow this pattern
public class SagaDb(IDbConnection connection) : Db(connection), ISagaDb { }
public class OutboxDb(IDbConnection connection) : Db(connection), IOutboxDb { }
public class ProjectionDb(IDbConnection connection) : Db(connection), IProjectionDb { }
```

The `Db` base class:
- Ensures the connection is in a ready (open) state before returning it via `Connection`
- Implements `IDisposable` to close and dispose the connection
- Handles null-guard validation

## Data Processing Multi-Database {#data-processing-multi-database}

Data processing uses a different multi-database strategy than the typed `IDb` interfaces above. Instead of marker interfaces, data processing relies on **per-processor `Func<IDbConnection>` injection** using .NET 8 keyed services.

### Why a Different Approach?

The `IDb` marker interface pattern works well when you have a fixed, small number of database roles (domain, saga, outbox, projections). Data processing is different:

- The number of processors is **unbounded** -- you may have dozens of processors reading from different databases
- Each processor reads from a **specific source database** that is unique to its record type
- The orchestration manager (`DataOrchestrationManager`) needs its **own** connection factory for task management tables
- Creating a new marker interface per processor would be excessive

### Architecture Overview

```
AddDataProcessing(orchestrationFactory, ...)
    |
    v
DataOrchestrationManager  <-- uses orchestrationFactory for task tables
    |
    v
DataProcessor<TRecord>    <-- each subclass injects its own factory
    |                         via constructor or keyed services
    v
IRecordHandler<TRecord>   <-- resolved per-scope; processes individual records
```

The `AddDataProcessing` method registers a single `Func<IDbConnection>` that the `DataOrchestrationManager` uses to manage data task records (insert, update, delete). Individual `DataProcessor<TRecord>` subclasses are responsible for fetching their own data from whatever database they need.

### Single Database (Simple Case)

When all processors read from the same database as the orchestration manager, register one factory:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddDataProcessing(
    () => new SqlConnection(connectionString),
    builder.Configuration,
    "DataProcessing",
    typeof(Program).Assembly);
```

All processors discovered via assembly scanning will use this factory (injected as `Func<IDbConnection>` via DI).

### Multi-Database with Keyed Services (.NET 8+)

When processors need different databases, use .NET 8 keyed services to register named connection factories. Each processor resolves its own factory by key.

#### Step 1: Register Named Connection Factories

```csharp
var orchestrationDb = builder.Configuration.GetConnectionString("Orchestration");
var customersDb = builder.Configuration.GetConnectionString("CustomersDb");
var inventoryDb = builder.Configuration.GetConnectionString("InventoryDb");

// Orchestration database (for DataOrchestrationManager task tables)
builder.Services.AddDataProcessing(
    () => new SqlConnection(orchestrationDb),
    builder.Configuration,
    "DataProcessing",
    typeof(Program).Assembly);

// Keyed connection factories for individual processors
builder.Services.AddKeyedSingleton<Func<IDbConnection>>(
    "customers",
    (_, _) => () => new SqlConnection(customersDb));

builder.Services.AddKeyedSingleton<Func<IDbConnection>>(
    "inventory",
    (_, _) => () => new SqlConnection(inventoryDb));
```

#### Step 2: Inject Keyed Factories in Processors

Each `DataProcessor<TRecord>` subclass resolves its keyed factory via the `[FromKeyedServices]` attribute:

```csharp
public class CustomerMigrationProcessor : DataProcessor<CustomerRecord>
{
    private readonly Func<IDbConnection> _connectionFactory;

    public CustomerMigrationProcessor(
        [FromKeyedServices("customers")] Func<IDbConnection> connectionFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<DataProcessingConfiguration> configuration,
        IServiceProvider serviceProvider,
        ILogger<CustomerMigrationProcessor> logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
        _connectionFactory = connectionFactory;
    }

    public override async Task<IEnumerable<CustomerRecord>> FetchBatchAsync(
        long skip, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory();
        // Query customers database
        return await connection.Ready().ResolveAsync(
            new SelectCustomerBatch(skip, batchSize, cancellationToken));
    }
}
```

```csharp
public class InventorySnapshotProcessor : DataProcessor<InventoryRecord>
{
    private readonly Func<IDbConnection> _connectionFactory;

    public InventorySnapshotProcessor(
        [FromKeyedServices("inventory")] Func<IDbConnection> connectionFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<DataProcessingConfiguration> configuration,
        IServiceProvider serviceProvider,
        ILogger<InventorySnapshotProcessor> logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
        _connectionFactory = connectionFactory;
    }

    public override async Task<IEnumerable<InventoryRecord>> FetchBatchAsync(
        long skip, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory();
        // Query inventory database
        return await connection.Ready().ResolveAsync(
            new SelectInventoryBatch(skip, batchSize, cancellationToken));
    }
}
```

#### Step 3: Configure Connection Strings

```json
{
  "ConnectionStrings": {
    "Orchestration": "Server=primary;Database=DataProcessing;...",
    "CustomersDb": "Server=legacy-crm;Database=Customers;...",
    "InventoryDb": "Server=warehouse;Database=Inventory;..."
  },
  "DataProcessing": {
    "QueueSize": 500,
    "ProducerBatchSize": 100,
    "ConsumerBatchSize": 10,
    "MaxAttempts": 3,
    "DispatcherTimeoutMilliseconds": 60000
  }
}
```

### Multi-Database without Keyed Services

If you are targeting .NET 8 but prefer not to use keyed services, or need to support older frameworks, you can use explicit factory registration:

```csharp
var customersDb = builder.Configuration.GetConnectionString("CustomersDb");
var inventoryDb = builder.Configuration.GetConnectionString("InventoryDb");

// Register processors with AOT-safe explicit registration
builder.Services.AddDataProcessor<CustomerMigrationProcessor>();
builder.Services.AddDataProcessor<InventorySnapshotProcessor>();

// Register record handlers
builder.Services.AddRecordHandler<CustomerRecordHandler, CustomerRecord>();
builder.Services.AddRecordHandler<InventoryRecordHandler, InventoryRecord>();

// Each processor receives its factory via a wrapper service or direct registration
builder.Services.AddSingleton<CustomerConnectionFactory>(
    _ => new CustomerConnectionFactory(customersDb));
builder.Services.AddSingleton<InventoryConnectionFactory>(
    _ => new InventoryConnectionFactory(inventoryDb));
```

Where the connection factory is a simple typed wrapper:

```csharp
public sealed class CustomerConnectionFactory(string connectionString)
{
    public IDbConnection Create() => new SqlConnection(connectionString);
}
```

The processor then injects this typed factory instead of using `[FromKeyedServices]`:

```csharp
public class CustomerMigrationProcessor : DataProcessor<CustomerRecord>
{
    private readonly CustomerConnectionFactory _factory;

    public CustomerMigrationProcessor(
        CustomerConnectionFactory factory,
        IHostApplicationLifetime appLifetime,
        IOptions<DataProcessingConfiguration> configuration,
        IServiceProvider serviceProvider,
        ILogger<CustomerMigrationProcessor> logger)
        : base(appLifetime, configuration, serviceProvider, logger)
    {
        _factory = factory;
    }

    public override async Task<IEnumerable<CustomerRecord>> FetchBatchAsync(
        long skip, int batchSize, CancellationToken cancellationToken)
    {
        using var connection = _factory.Create();
        return await connection.Ready().ResolveAsync(
            new SelectCustomerBatch(skip, batchSize, cancellationToken));
    }
}
```

### Choosing Your Approach

| Approach | When to Use | Pros | Cons |
|----------|-------------|------|------|
| **Keyed services** | .NET 8+, many processors | Clean DI, no wrapper types | Requires .NET 8+, string keys |
| **Typed factory wrappers** | Any .NET version | Type-safe, explicit | One class per database source |
| **Single factory** | All processors use same DB | Simplest setup | No multi-database support |

### Key Design Decisions

1. **Orchestration vs processor databases are separate concerns.** The `Func<IDbConnection>` passed to `AddDataProcessing` is for the orchestration manager's task tables only. Processors should not depend on this factory for their source data.

2. **Processor-level injection (not framework-level).** The framework deliberately does not try to route different factories to different processors. Each processor subclass is responsible for declaring and resolving its own database dependency. This keeps the framework simple and gives consumers full control.

3. **Connection factories create and dispose per call.** Both `DataOrchestrationManager` and individual processors follow the pattern of `using var connection = _connectionFactory()` -- creating a fresh connection per operation and disposing it immediately after use. This avoids connection leaking and works correctly with connection pooling.

## Data Processing Databases

:::caution Removed in Sprint 657
`IDataProcessorDb`, `IDataToProcessDb`, `DataProcessorDb`, and `DataToProcessDb` have been removed. Data processing now uses `Func<IDbConnection>` connection factories directly. See [Data Processing Multi-Database](#data-processing-multi-database) above for the replacement pattern.
:::

## Common Patterns

### CQRS: Separate Read and Write

The most common multi-database pattern separates the write side (events) from the read side (projections):

```csharp
// Write side
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeConnectionString)));

// Read side (can be a read replica or entirely separate database)
builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new SqlConnection(readConnectionString)));
```

### Isolated Saga State

Long-running sagas can generate significant load. Isolating saga state prevents contention with the main event store:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(domainConnectionString)));

builder.Services.AddScoped<ISagaDb>(_ =>
    new SagaDb(new SqlConnection(sagaConnectionString)));
```

### Dedicated Outbox Database

When outbox throughput is high, a separate database prevents lock contention with domain writes:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(domainConnectionString)));

builder.Services.AddScoped<IOutboxDb>(_ =>
    new OutboxDb(new SqlConnection(outboxConnectionString)));
```

## Postgres

The same pattern works with Npgsql for Postgres:

```csharp
using Npgsql;

builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new NpgsqlConnection(writeConnectionString)));

builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new NpgsqlConnection(readConnectionString)));
```

## Document Database Projections

When projections target a **document database** (Elasticsearch, CosmosDB, MongoDB), the typed `IDb` interfaces don't apply -- these stores use SDK clients, not `IDbConnection`. Each document-based projection store is configured through its own `IOptions<T>`:

### Elasticsearch

```csharp
builder.Services.Configure<ElasticSearchProjectionStoreOptions>(options =>
{
    options.ConnectionString = "https://search-cluster:9200";
    options.IndexPrefix = "projections";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    ElasticSearchProjectionStore<OrderReadModel>>();
```

### Azure Cosmos DB

```csharp
builder.Services.Configure<CosmosDbProjectionStoreOptions>(options =>
{
    options.ConnectionString = "AccountEndpoint=https://...";
    options.DatabaseName = "Projections";
    options.ContainerName = "read-models";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    CosmosDbProjectionStore<OrderReadModel>>();
```

### MongoDB

```csharp
builder.Services.Configure<MongoDbProjectionStoreOptions>(options =>
{
    options.ConnectionString = "mongodb://read-cluster:27017";
    options.DatabaseName = "Projections";
    options.CollectionName = "read-models";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    MongoDbProjectionStore<OrderReadModel>>();
```

### Mixed: SQL Events + Document Projections

A common architecture uses SQL Server for the write side (event store, outbox, sagas) and a document database for the read side (projections). In this case, use `IDomainDb` for SQL stores and `IOptions<T>` for the document projection store:

```csharp
// Write side: SQL Server for events, outbox, sagas
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeConnectionString)));

// Read side: Elasticsearch for projections (no IProjectionDb needed)
builder.Services.Configure<ElasticSearchProjectionStoreOptions>(options =>
{
    options.ConnectionString = "https://search-cluster:9200";
    options.IndexPrefix = "projections";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    ElasticSearchProjectionStore<OrderReadModel>>();
```

`IProjectionDb` is only needed when projections use a **SQL database** (SQL Server or Postgres) that requires a separate `IDbConnection` from the write side.

## Package Reference

| Package | Types |
|---------|-------|
| `Excalibur.Data.Abstractions` | `IDb`, `Db`, `IDocumentDb` |
| `Excalibur.Data` | `IDomainDb`, `DomainDb`, `ISagaDb`, `SagaDb`, `IOutboxDb`, `OutboxDb`, `IProjectionDb`, `ProjectionDb` |
| `Excalibur.Data.DataProcessing` | `Func<IDbConnection>` factory (via `AddDataProcessing`), `AddDataProcessor<T>`, `AddRecordHandler<T, TRecord>` |

## See Also

- [Data Providers Overview](./index.md) -- Unified data access layer with all available provider implementations
- [IDb Interface](../data-access/idb-interface.md) -- Database connection abstraction and IDataRequest pattern used by typed IDb interfaces
- [SQL Server Provider](./sqlserver.md) -- Enterprise SQL Server provider with transaction scope and retry support
