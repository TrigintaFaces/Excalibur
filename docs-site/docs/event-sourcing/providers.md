---
sidebar_position: 8
title: Event Store Providers
description: Per-provider event store setup for SQL Server, PostgreSQL, MongoDB, Cosmos DB, DynamoDB, and Firestore.
---

# Event Store Providers

Each event store provider implements `IEventStore` with database-specific optimizations. Choose the provider that matches your database.

## Quick Start

Pick your database and copy the registration:

| Database | Package | Registration | Multi-tenant? |
|----------|---------|-------------|---------------|
| **SQL Server** | `Excalibur.EventSourcing.SqlServer` | `es.UseSqlServer(sql => sql.ConnectionString(connStr))` | Yes |
| **PostgreSQL** | `Excalibur.EventSourcing.Postgres` | `es.UsePostgres(pg => pg.ConnectionString(connStr))` | Yes |
| **MongoDB** | `Excalibur.EventSourcing.MongoDB` | `es.UseMongoDB(mg => mg.ConnectionString(connStr).DatabaseName("events"))` | Yes |
| **Cosmos DB** | `Excalibur.EventSourcing.CosmosDb` | `es.UseCosmosDb(c => c.ConnectionString(connStr).DatabaseName("events"))` | Yes |
| **DynamoDB** | `Excalibur.EventSourcing.DynamoDb` | `es.UseDynamoDb(opts => { ... })` | Yes |
| **Firestore** | `Excalibur.EventSourcing.Firestore` | `es.UseFirestore(opts => { ... })` | Yes |
| **In-Memory** | `Excalibur.EventSourcing.InMemory` | `es.UseInMemory()` (builder only) | Yes |

:::danger Upgrading a MongoDB, Cosmos DB, DynamoDB or Firestore event store: the key shape changed
These four now compose the owning tenant into the document key, so they confine tenants. **Documents
written by an earlier version have no tenant segment and are not addressable by the new key.** Nothing
is destroyed, and the store **refuses rather than reading them back as an empty stream** — so an
unmigrated deployment fails at its first read instead of silently splitting an aggregate's history in
two. You must re-key existing documents. This applies **even if you never enabled multi-tenancy**: the
key carries a reserved single-tenant or untenanted segment either way. See
[Upgrading the four document providers](#upgrading-the-four-document-providers) before you deploy.
:::

Each `AddXxxEventSourcing()` call registers `IEventStore` and `ISnapshotStore` for that provider. Outbox is registered separately via `services.AddExcalibur(x => x.AddOutbox(...))`.

Every provider stores the same identity: each event's declared `[MessageName]` -- not its CLR type name -- is written to the store's event-type column or field, and resolved back to a CLR type through the registered event-type registry on read. See [Stable Message Names](domain-events.md#stable-message-names).

## Before You Start

- **.NET 10.0**
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(sql => sql.ConnectionString(connectionString))
      .AddRepository<OrderAggregate, Guid>();
}));

// Or with detailed options
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .EventStoreSchema("es")
           .SnapshotStoreSchema("es");
    });
}));

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
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseSqlServer(connectionString)));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg => pg.ConnectionString(connectionString))
      .AddRepository<OrderAggregate, Guid>();
}));

// With schema and table customization
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg =>
    {
        pg.ConnectionString(connectionString)
          .EventStoreSchema("events")
          .EventStoreTable("domain_events")
          .SnapshotStoreSchema("events")
          .SnapshotStoreTable("snapshots");
    });
}));

// With NpgsqlDataSource (recommended for connection pooling, Azure, JSONB)
var dataSource = NpgsqlDataSource.Create(configuration.GetConnectionString("Postgres")!);
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg => pg.DataSource(dataSource))
      .AddRepository<OrderAggregate, Guid>();
}));

// Named connection string (resolved from IConfiguration)
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg => pg.ConnectionStringName("EventStore"));
}));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg =>
        pg.ConnectionString("Host=cockroachdb.example.com;Port=26257;Database=events;..."));
}));

// YugabyteDB
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg =>
        pg.ConnectionString("Host=yugabyte.example.com;Port=5433;Database=events;..."));
}));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString(connectionString)
              .DatabaseName("events")
              .ContainerName("event-store");
    })
    .AddRepository<OrderAggregate, Guid>();
}));

// With endpoint + auth key (Azure portal credentials)
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
        cosmos.Endpoint("https://myaccount.documents.azure.com:443/", authKey)
              .DatabaseName("events"));
}));

// With pre-configured CosmosClient
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
        cosmos.Client(cosmosClient).DatabaseName("events"));
}));
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

:::info Serializer-agnostic persisted documents

If you supply your own `CosmosClient` (via `Client(...)` or `ClientFactory(...)`), be aware that the Cosmos SDK v3 **default serializer is Newtonsoft.Json**, not System.Text.Json. The framework's persisted Cosmos documents are **dual-annotated** — `[JsonPropertyName]` (System.Text.Json) **and** `[JsonProperty]` (Newtonsoft) on every persisted property — so the correct lowercase wire keys are emitted regardless of which serializer your injected client uses. You do not need to configure the client's serializer for framework documents to round-trip correctly.
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

`UseDynamoDb` takes a configuration action on `IDynamoDBEventSourcingBuilder`. Every setting is a fluent
method call, and the connection methods (`ServiceUrl`, `Region`, `Client`, `ClientFactory`,
`BindConfiguration`) are **last-wins** — the last one you call is the one that takes effect.

```csharp
using Amazon;
using Microsoft.Extensions.DependencyInjection;

// AWS region, using the default credential chain
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo =>
    {
        dynamo.Region(RegionEndpoint.USEast1)
              .TableName("event-store");
    })
    .AddRepository<OrderAggregate, Guid>();
}));

// Local DynamoDB / LocalStack
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo =>
    {
        dynamo.ServiceUrl("http://localhost:8000")
              .TableName("event-store");
    });
}));

// Bind from IConfiguration
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo => dynamo.BindConfiguration("DynamoDb"));
}));
```

### Change Feed (DynamoDB Streams)

The event store appends, loads, and reads versions through the DynamoDB client alone. **A DynamoDB Streams
client is only needed to consume a change feed** — if you are not reading one, you do not need to configure
anything here, and the store will register and resolve without it.

When you configure the connection by **service URL or region**, the registration owns the connection and
builds a matching Streams client for you, so the change feed works with no extra configuration:

```csharp
// Change feed available: the registration builds both clients from the region
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo =>
    {
        dynamo.Region(RegionEndpoint.USEast1)
              .TableName("event-store");
    });
}));
```

When you supply your **own** `IAmazonDynamoDB` via `Client` or `ClientFactory`, the registration will not
guess at the endpoint and credentials behind it, so no Streams client is built. Supply one yourself with
`StreamsClient` (an instance) or `StreamsClientFactory` (resolved from the container), so the change feed
runs under your own credentials, endpoint, and telemetry:

```csharp
using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Your own clients, wired for the change feed
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo =>
    {
        dynamo.Client(myDynamoDbClient)
              .StreamsClient(myStreamsClient)
              .TableName("event-store");
    });
}));

// Or build both from the container, so configuration resolves at startup
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseDynamoDb(dynamo =>
    {
        dynamo.ClientFactory(sp => new AmazonDynamoDBClient(
                  new AmazonDynamoDBConfig
                  {
                      ServiceURL = sp.GetRequiredService<IConfiguration>()["Aws:ServiceUrl"]
                  }))
              .StreamsClientFactory(sp => new AmazonDynamoDBStreamsClient(
                  new AmazonDynamoDBStreamsConfig
                  {
                      ServiceURL = sp.GetRequiredService<IConfiguration>()["Aws:ServiceUrl"]
                  }))
              .TableName("event-store");
    });
}));
```

`StreamsClient` and `StreamsClientFactory` are **last-wins against each other**, but are independent of the
connection method: choosing or changing a connection mode neither sets nor clears the Streams client. Either
one may be combined with any connection method.

:::note What happens if you skip it

A store built without a Streams client is fully functional for appends, loads, and version queries. It
reports the change feed as **unavailable** rather than failing to construct: asking it for
`ICloudNativeEventStoreChangeFeed` returns `null` instead of an instance that throws on first use. Calling a
change-feed operation on it directly throws `InvalidOperationException` naming the missing client.
:::

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

`UseFirestore` takes a fluent builder. Each connection value is configured by a method call.

```csharp
// Application Default Credentials (ADC) -- the ambient service account of the host
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseFirestore(firestore => firestore
        .ProjectId("my-gcp-project")
        .CollectionName("events"))
    .AddRepository<OrderAggregate, Guid>();
}));

// Or bind the connection values from IConfiguration
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseFirestore(firestore => firestore.BindConfiguration("Firestore"));
}));
```

### Authenticating

When no credential is configured the client uses Application Default Credentials, so it connects as
whatever identity the host exposes. Supply a service account explicitly when the store must connect as a
specific principal -- for example under least privilege, or when each tenant has its own service account.

```csharp
// Service account from a file on disk
es.UseFirestore(firestore => firestore
    .ProjectId("my-gcp-project")
    .CredentialsPath("/var/secrets/event-store-service-account.json")
    .CollectionName("events"));

// Service account supplied inline, e.g. read from a secret manager during startup
string credentialJson = secrets.GetEventStoreCredential();

es.UseFirestore(firestore => firestore
    .ProjectId("my-gcp-project")
    .CredentialsJson(credentialJson)
    .CollectionName("events"));
```

`CredentialsPath` and `CredentialsJson` each clear the other, so the last call wins and the fluent builder
cannot hold both. When both arrive together through configuration binding, the inline JSON credential is
used and the path is ignored.

:::note Emulator
`EmulatorHost(...)` clears any configured credentials, because the emulator does not authenticate.
:::

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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseMongoDB(mg =>
    {
        mg.ConnectionString("mongodb://localhost:27017")
          .DatabaseName("events")
          .CollectionName("event_store_events");
    })
    .AddRepository<OrderAggregate, Guid>();
}));

// With pre-configured IMongoClient
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseMongoDB(mg => mg.Client(mongoClient).DatabaseName("events"))
      .AddRepository<OrderAggregate, Guid>();
}));

// With DI-aware client factory
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseMongoDB(mg =>
        mg.ClientFactory(sp => sp.GetRequiredService<IMongoClient>())
          .DatabaseName("events"));
}));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlite(options =>
    {
        options.ConnectionString = "Data Source=events.db";
    });
}));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseInMemory()
      .AddRepository<OrderAggregate, Guid>();
}));

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

## Tenant confinement by provider

Tenant confinement is **per provider**. It is not a property of the event-sourcing subsystem, and the
providers do not all have it.

**The property, stated so you can test it yourself.** A provider *confines* when an operation performed
under tenant A observes and mutates only rows written under A. Append three events for aggregate `a` under
tenant A, then load `a` under tenant B. A confining provider returns **zero** events. A non-confining
provider returns **three**.

| Provider | Confines? | What holds the boundary | How this was established |
|----------|-----------|-------------------------|--------------------------|
| SQL Server | **Yes** | Tenant column bound in every statement; tenant is inside the stream uniqueness constraint | Conformance suite — three tenant arms |
| PostgreSQL | **Yes** | Same | Conformance suite — three tenant arms |
| Oracle | **Yes** | Same | Conformance suite — three tenant arms |
| SQLite | **Yes** | Same | Conformance suite — three tenant arms |
| Redis | **Yes** | Tenant is a segment of the stream key, so the version counter is tenant-scoped too | Conformance suite — three tenant arms, plus a dedicated tenancy suite |
| In-Memory | **Yes** | Tenant is a component of the stream dictionary key | Conformance suite — three tenant arms, run without a container |
| MongoDB | **Yes** | Tenant leads the stored stream id, which is inside the unique `(streamId, aggregateType, version)` index — so the version sequence is tenant-scoped too | Conformance suite — three tenant arms, against a real MongoDB |
| Cosmos DB | **Yes** | Tenant leads the partition key, so each tenant has its own logical partition and its own version sequence | Conformance suite — three tenant arms, against the Cosmos emulator |
| DynamoDB | **Yes** | Tenant leads the partition key, so each tenant has its own item set and its own sort-key sequence | Conformance suite — three tenant arms, against DynamoDB Local |
| Firestore | **Yes** | Tenant leads the document id, so each tenant has its own documents and its own version sequence | Conformance suite — three tenant arms, against the Firestore emulator |
| Tenant routing (sharding) | **UNVERIFIED** | Routes each tenant to a distinct physical store; confinement is your shard map's, not the inner store's | Source only. The sharding integration suite is not among those we hold a measurement of executing |
| Cold store — S3, Azure Blob, GCS | **UNVERIFIED** | The tenant is an encoded segment of the object key | Source only. We hold no measurement of the tiered-storage integration suites executing |

**What "established" means here, exactly.** Every provider suite inherits the same three tenant arms from
the shared conformance kit, and none overrides or skips them. No event-store container fixture opts into
graceful degradation, so a missing container fails that provider's run loudly rather than passing it by
skipping. That is what was checked for each row: the arm exists, it is inherited unmodified, and it cannot
pass by not running.

The last two rows say UNVERIFIED for a different reason: their suites exist and are not quarantined, but we
hold no measurement of them actually executing. We are not willing to call that verified.

### How the four document providers confine

Each composes the owning tenant into the document key as its leading segment:

```
DynamoDB    partition key    t:{tenantId}:{aggregateType}:{aggregateId}
Cosmos DB   partition key    t:{tenantId}:{aggregateType}:{aggregateId}
Firestore   document id      t:{tenantId}:{aggregateType}:{aggregateId}:{version}
MongoDB     streamId         t:{tenantId}:{aggregateId}
```

**The tenant is in the key, not in a filter, and the difference is not cosmetic.** A filter confines reads
while leaving both tenants on one document set and one version counter — so the second tenant to use an
aggregate identifier is told it has a concurrency conflict on a stream it never wrote, and can never create
it. Aggregate identifiers come from your domain, so natural keys such as an order number collide across
tenants as a matter of course. Composing the key makes a cross-tenant read unaddressable rather than
filtered out, and gives each tenant its own version sequence as a consequence rather than as a second
mechanism.

The tenant term is always present. A host that never enables multi-tenancy resolves the framework
single-tenant default; a genuinely untenanted deployment resolves the reserved untenanted value. There is no
key without a tenant segment.

Their **snapshot** stores already composed the tenant into the document id and are unchanged — that
asymmetry is why these four confined snapshots but not events until now.

### Upgrading the four document providers

**The stored key shape changed, and there is no migration tool.** Documents written by an earlier version
carry `{aggregateType}:{aggregateId}` (MongoDB: a `streamId` of `{aggregateId}`) with no tenant segment.
Nothing reads that shape any more, so after upgrading, an aggregate written by the earlier version is
**unaddressable** — its events are still in the store and still readable by the earlier package version,
but no key this version composes names them.

**The store refuses rather than reading them back as an empty stream.** Each of the four guards every
point at which it would otherwise act on the absence of documents. The first time one is reached, it
checks the configured collection or table for a document whose key carries no tenant segment; finding
one, it throws `InvalidOperationException` naming the collection and the offending key, and **modifies
nothing**. An empty stream would otherwise be taken for a new aggregate and appended at version 0,
leaving you with two disjoint histories under one identity while the store still held the first — so an
unmigrated deployment fails at its first misleading read, with every event intact.

The check is not on the startup path and costs nothing in normal operation: a read that returns documents
proves the collection is addressable and is never probed, so only silence is checked, at most once per
store instance. **The full procedure, both limits of the guard, and the saga-store half of this change
are in [Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the
tenant](../migration/nosql-tenant-key-rekey.md).**

**Which keys — because your snapshots were re-keyed already.** On these four backends the *snapshot*
store has composed the tenant into its document id since an earlier release. Only the **event** documents
change here. You did not have to migrate the snapshots and you do not have to now: a snapshot whose key
misses is simply not found, and the aggregate rebuilds from its event stream. **An event stream that
misses has nothing behind it to rebuild from**, which is the whole reason this one needs a procedure and
that one did not.

A tool cannot do this for you in the general case: deciding which tenant an existing untenanted document
belongs to is a question about your deployment, not about the data. Per collection or table:

1. **Stop writers.** The re-key is not safe against a live writer.
2. **Export every event document**, preserving `version` order within each stream.
3. **Re-key each document** by prefixing `t:{tenantId}:` to the existing key. If you ran single-tenant, use
   the framework's default tenant identifier; if you never enabled ambient tenancy at all, use the reserved
   untenanted value. Both are public constants (`TenantDefaults.DefaultTenantId`,
   `TenantScope.UntenantedSentinel`) — copy the value from there rather than retyping it. A mistyped variant
   strands every row in a partition nothing queries, and nothing reports it.
4. **Re-import**, then load one aggregate per tenant and check the event count matches the export.

If you can afford to rebuild your read models, the cheaper route is to point the provider at a fresh
collection and leave the old one in place.

### What happens if you register one in a multi-tenant host

**It starts.** Each provider registration supplies the ambient tenant to the store and declares the
capability in the same act, for every contract the store is registered under, so the multi-tenancy startup
check passes under both isolation strategies and in either registration order.

Under `Sharding`, routing each tenant to a distinct physical store is the *physical* half of separation and
the key is the *logical* half. **A shard map that points two tenants at the same database is now still
safe**, because the store contributes its own tenant term.

A single-tenant host is unaffected: there is one partition, so there is nothing to cross.

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

Tiered storage copies older event payloads from the primary database to object storage. The hot row remains, retaining event identity, tenant, aggregate version and global position; `EventData` becomes null and `ArchivedAt` records archival. This preserves the global stream for replay. All cold store providers implement `IColdEventStore` and use gzip-compressed JSON.

Register a supported hot store before calling `UseTieredStorage`, and register one cold store. SQL Server and PostgreSQL tiered registrations restore archived payloads for aggregate reads and both global query methods. Registering a cold provider alone does not enable archival or read-through.

#### `WriteAsync` returns a durable watermark

`Task<long> WriteAsync(KeyedTenantPartition tenant, string aggregateId, string aggregateType, IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)`

Every `IColdEventStore` method takes the tenant partition, aggregate ID and aggregate type. Identity comparisons are case-sensitive. Reads validate the complete archive before applying the exclusive `fromVersion` filter. Malformed data, conflicting identities and duplicate event identities fail rather than appearing absent.

The returned value is the **durable low-water mark**: the highest version `V` such that every version from zero through `V` for that stream is durably present in cold storage. The archive service clears hot payloads only through this prefix, bounded by its selected archive candidate. This receipt is an aggregate-version boundary, not a global subscription watermark.

Defined returns:

| Case | Return |
|------|--------|
| `events` is empty | `-1` after successful layout/archive validation; otherwise throws. Clear no hot payloads. |
| Every submitted event is already present and identical | The existing confirmed contiguous prefix |
| A conditional upload has been acknowledged | The contiguous prefix of the complete merged archive |
| The durable archive contains versions `0, 1, 5` | `1`; version `5` remains stored but does not bridge the gap |
| The durable archive lacks version zero | `-1`, even when the upload succeeded |

:::warning If you implement `IColdEventStore` yourself
An upload acknowledgement alone does not prove a contiguous prefix. Return only the prefix actually present after a durable write, and preserve existing events when retrying a conditional-write conflict. Callers must honor the returned boundary before clearing hot payloads.
:::

### Read-through and erasure

An archive marker is not an erased event. Read-through fetches the cold payload and then rechecks that event's state against the authoritative primary hot store. If that observation reports the reserved `$erased` marker, the returned event has no payload or metadata. Otherwise the cold event must match the selected hot event's identity and immutable data. For a non-erased event, a missing or inconsistent archive fails the read; global reads return no partial page when a later restore fails. An authoritative erased result does not require a matching cold event. Cold fetch or parse failures still fail before that observation can occur.

Each archived event is rechecked, even when its stream's cold data was already fetched for the page. This is a per-event freshness check, not an atomic snapshot or a fence against an erasure that commits after the check. Tiered read-through does not erase retained cold objects. The shipped tiered composition denies the event-store erasure capability: enabling event-store erasure with `UseTieredStorage` is rejected at host startup. An external cold-storage policy does not remove this guard.

The hot provider must expose the authoritative-reader capability. SQL Server's connection-string registration supports this; a custom connection factory must use the explicit owned-primary factory contract. It must supply fresh owned connections to the same writable primary. Borrowed connections, ambient snapshots and replicas cannot provide the required freshness. Replacing the hot store with an incompatible read composition or provider binding is rejected. Compatible singleton decorators must preserve the tiered composition receipt and provider source identity.

### Azure Blob Storage

In this example, `eventStoreConnectionString` comes from your application configuration.

```bash
dotnet add package Excalibur.EventSourcing.AzureBlob
dotnet add package Excalibur.EventSourcing.SqlServer
```

```csharp
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Extensions.DependencyInjection;

services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseSqlServer(sql => sql.ConnectionString(eventStoreConnectionString));
    builder.UseAzureBlobColdEventStore(opts =>
    {
        opts.ConnectionString("DefaultEndpointsProtocol=https;...");
        opts.ContainerName("event-archive");
    });
    builder.UseTieredStorage(policy => policy.MaxAge = TimeSpan.FromDays(90));
}));
```

### AWS S3

Use this cold registration in the same hot-store and `UseTieredStorage` configuration shown above.

```bash
dotnet add package Excalibur.EventSourcing.AwsS3
```

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseAwsS3ColdEventStore(opts =>
    {
        opts.BucketName("my-event-archive");
        opts.Region("us-east-1");
        opts.KeyPrefix("events");
    });
}));
```

### Google Cloud Storage

Use this cold registration in the same hot-store and `UseTieredStorage` configuration shown above.

```bash
dotnet add package Excalibur.EventSourcing.Gcs
```

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseGcsColdEventStore(opts =>
    {
        opts.BucketName("my-event-archive");
        opts.ObjectPrefix("events");
    });
}));
```

### Cold Store Comparison

| Provider | Package | Authentication |
|----------|---------|----------------|
| **Azure Blob** | `Excalibur.EventSourcing.AzureBlob` | Connection string or DefaultAzureCredential |
| **AWS S3** | `Excalibur.EventSourcing.AwsS3` | AWS SDK default credential chain |
| **GCS** | `Excalibur.EventSourcing.Gcs` | Google Application Default Credentials |

The default `ColdArchiveLayout.Legacy` layouts use Base64Url-encoded tenant and aggregate-ID segments:

| Provider | Object key relative to its container or bucket |
|----------|-----------------------------------------------|
| Azure Blob | `{tenantSegment}/{aggregateSegment}.json.gz` |
| S3 | `{keyPrefix}/{tenantSegment}/{aggregateSegment}/events.json.gz` |
| GCS | `{objectPrefix}/{tenantSegment}/{aggregateSegment}/events.json.gz` |

An empty S3/GCS prefix omits the prefix and its separator. Writes merge by version and use conditional updates to prevent lost concurrent additions.

These legacy keys do not contain the aggregate type. If another type already occupies the same tenant/ID key, the provider rejects the operation. Each cloud provider builder accepts `.Layout(ColdArchiveLayout.TypedV2)` to select independent typed streams. The layout is fixed when the provider is constructed; changing options afterward does not switch it.

| Provider | TypedV2 object key |
|----------|--------------------|
| Azure Blob | `v2/{tenantSegment}/{typeSegment}/{aggregateSegment}.json.gz` |
| S3/GCS | `{configuredPrefix}/v2/{tenantSegment}/{typeSegment}/{aggregateSegment}/events.json.gz` |

Every identity segment is independently encoded by `ColdStorageKey`; do not construct or rename keys manually. An empty prefix omits its separator.

### Moving an archive namespace to TypedV2

Custom hot providers must expose `IEventStoreArchive`, `IEventStoreArchiveReader` and `IEventStoreArchiveScanner` through their captured store's `GetService` capability path. The reader addresses the candidate's tenant explicitly and returns raw hot rows through its version ceiling. Restricting decorators must explicitly authorize or deny this payload capability. Tiered startup rejects missing readers/scanners and separately registered archive services that would split discovery, reading and tombstoning across different sources. SQL Server and PostgreSQL provide these capabilities.

Successful pages with a continuation are processed immediately. The configured archive interval applies before a new round and after a page-fetch failure, rather than between every page. Large rounds can produce sustained database and cold-storage activity; the page size is not a rate limit or a bound on database I/O.

Hosted archival scans a bounded number of stream identities per page, including streams that currently have no eligible work. Completed candidate failures do not reset paging. Progress requires successful page retrieval and completing candidate attempts; a hanging attempt or repeated fetch failure can still delay later streams. Policy values and the age evaluation instant are fixed for each scan round; configuration changes apply to the next round. New stream identities join a later round, while retention still considers new events appended to an existing stream. Cancellation retries the unfinished page. Scan continuations are process-local and restarting begins a new round; repeated restarts before a round finishes can still delay later streams. This continuation is unrelated to global subscriber checkpoints. Custom providers must implement the bounded scan contract before enabling hosted archival; there is no automatic fallback to repeated one-shot discovery.

**Archive-policy compatibility:** age and global-position thresholds are alternative triggers; `MaxPosition` means strictly below that global position. Retention protects the newest N events by aggregate version, even when all events are old, and can be used alone. Earlier SQL implementations incorrectly combined triggers with AND, compared aggregate versions, and could reject entire streams with retention configured. Reassess existing settings when upgrading. A covering snapshot is no longer a prerequisite: the framework requires a durable, recoverable cold-history prefix before clearing hot payloads. Snapshots remain optional.

This is an explicit administrative cutover, not an automatic upgrade. Changing the layout setting alone does not migrate existing archives. The migration capability does not enumerate archives: supply each exact tenant, aggregate ID and aggregate type from your authoritative inventory.

1. Stop and drain all Legacy reads, writes and retries across the entire container or configured bucket prefix, including operations from updated binaries. Keep old binaries fenced out afterward. The framework does not acquire this external fence.
2. Obtain `IColdEventStoreMigration` from the same captured `IColdEventStore` instance used by the application. A decorator may deny the capability; do not bypass it by resolving another provider or unwrapping it. Namespace activation needs namespace-wide administrative authority.
3. Call `ActivateTypedLayoutAsync(cancellationToken)` once; retries are supported. This publishes a permanent namespace marker and blocks Legacy access, but does not establish that any stream has migrated.
4. Call `MigrateAsync(tenant, aggregateId, aggregateType, cancellationToken)` for every occupied legacy slot. Missing, empty, ambiguous or conflicting archives fail rather than being assigned a guessed type. Retrying a completed migration validates its retained history.
5. Run consumers and archival with `.Layout(ColdArchiveLayout.TypedV2)` against the same namespace. Activation does not change an already-created instance's layout. Verify migration coverage and application reads before resuming normal work.

Retain the namespace marker, migration receipts, original archives and typed archives. Do not expire them through lifecycle rules, recreate the container/bucket, or replace them outside the protocol. Failure or cancellation may leave durable migration state; resume the operation rather than deleting that state to roll back. Migration does not authorize erasing retained data.

Typed reads and writes validate retained migration history, including identical retries, empty batches, filtered reads and existence checks. An occupied legacy slot without a valid receipt is an error. After activation, new streams can use typed storage when no legacy slot exists, or for another aggregate type at the same tenant/ID after the occupied slot's migration receipt and retained baseline validate. Their namespace marker prevents a later Legacy restart from silently treating them as absent.

**Client requirements:** S3 typed access and migration require endpoint discovery from the captured client and a fixed single-bucket namespace; access-point ARNs and multi-region aliases are rejected. Endpoint or addressing changes can invalidate persisted migration identity. GCS typed access requires a client that preserves compressed bytes. Framework-created GCS clients are configured accordingly and disposed with the provider. Supplied clients are not reconfigured or disposed by the store; factory-created DI clients retain DI ownership. An incompatible supplied client fails on typed access or migration. GCS absence checks also require bucket-metadata permission; authorization failures are not treated as empty storage.

### Archive Metrics

The `ArchiveMetrics` type declares the following instruments in meter `Excalibur.EventSourcing.Archive`. Instrument declarations alone do not establish that a particular operation emits measurements; verify emission before using these names for operational alerts.

| Metric | Type | Description |
|--------|------|-------------|
| `excalibur.eventsourcing.archive.events_archived` | Counter | Events moved to cold storage |
| `excalibur.eventsourcing.archive.events_deleted` | Counter | Hot payloads cleared after archival; the historical metric name is retained |
| `excalibur.eventsourcing.archive.cold_reads` | Counter | Read-through operations from cold |
| `excalibur.eventsourcing.archive.errors` | Counter | Archive operation failures |
| `excalibur.eventsourcing.archive.duration_seconds` | Histogram | Batch archive duration |

## See Also

- [Event Sourcing Overview](./index.md) -- Architecture and core abstractions
- [Event Store](./event-store.md) -- `IEventStore` interface details
- [Snapshots](./snapshots.md) -- Snapshot store configuration
- [Change Data Capture](../patterns/cdc.md) -- CDC patterns and provider support

