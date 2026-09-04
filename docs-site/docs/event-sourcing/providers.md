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

For hot/cold storage separation at petabyte scale, archived events are moved from the primary (hot) store to a cold store in blob/object storage. All cold store providers implement `IColdEventStore` (4 methods: `WriteAsync`, `ReadAsync`, `ReadAsync(fromVersion)`, `HasArchivedEventsAsync`) and use a gzip-compressed JSON format.

#### `WriteAsync` returns a durable watermark

`Task<long> WriteAsync(KeyedTenantPartition tenant, string aggregateId, IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)`

Every `IColdEventStore` method takes a `KeyedTenantPartition` as its first parameter. Cold storage keys are composed from that partition, so events archived under one tenant are not addressable from another tenant's read or watermark check.

The returned value is the **durable low-water mark**: the highest version `V` such that *every* version `<= V` for that aggregate is durably committed in cold storage. It is a contiguous durable prefix, never the merely-submitted maximum. The archive service deletes hot events only up to this watermark, so a partial or deferred cold write bounds hot deletion instead of destroying the only remaining copy.

Defined returns:

| Case | Return |
|------|--------|
| `events` is empty | `-1` — nothing durably added by this call; delete nothing |
| Every submitted version is already present in cold storage | The confirmed maximum of the submitted range |
| The upload receipt has been awaited and acknowledged | The submitted maximum |
| Only part of the batch is durable (or a buffered write is deferred) | The highest contiguously-durable version |

:::warning If you implement `IColdEventStore` yourself
Return the submitted maximum **only after** the storage receipt confirms durability. Returning it earlier authorizes the caller to delete not-yet-archived events from the hot tier. Callers must likewise honour the returned value — awaiting a `Task<long>` and discarding the result compiles cleanly and reintroduces the data-loss path.
:::

### Azure Blob Storage

```bash
dotnet add package Excalibur.EventSourcing.AzureBlob
```

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseAzureBlobColdEventStore(opts =>
    {
        opts.ConnectionString("DefaultEndpointsProtocol=https;...");
        opts.ContainerName("event-archive");
        opts.BlobPrefix("events");
    });
}));
```

### AWS S3

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

All providers store events as `{prefix}/{tenant}/{aggregateId}/events.json.gz` and support merge-on-write (read existing, append new, write back). Both the tenant and aggregate segments are encoded, so events archived under one tenant are not addressable from another tenant's read or watermark check.

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

