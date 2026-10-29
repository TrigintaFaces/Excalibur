---
sidebar_position: 10
title: Elasticsearch
description: Elasticsearch provider with resilient client, index management, projections, and health monitoring.
---

# Elasticsearch Provider

The Elasticsearch provider offers full-text search and analytics with a resilient client wrapper, index lifecycle management, projection store integration, and health monitoring.

## Projection tenant isolation

This provider's projection store is **estate-wide**: it does not add a tenant predicate to
projection reads or writes. A tenant routing rule or tenant-presence decorator does not add row
isolation when tenants share a destination. Row-discriminator multi-tenancy refuses an unmarked
projection store during registration.

Use an explicitly estate-wide read model for cross-tenant processing. For tenant-confined projections,
use a store that enforces the partition, such as SQL Server or PostgreSQL, together with a source
isolated to that tenant. See [projection tenant boundaries](../event-sourcing/projections.md#tenant-boundaries-during-global-stream-processing).

## Before You Start

- **.NET 10.0**
- An Elasticsearch cluster (local, Elastic Cloud, or AWS OpenSearch)
- Familiarity with [data access](../data-access/index.md) and [projections](../event-sourcing/projections.md)

## Installation

```bash
dotnet add package Excalibur.Data.ElasticSearch
```

**Dependencies:** `Excalibur.Data.Abstractions`, `Elastic.Clients.Elasticsearch`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddElasticsearchServices(configuration);
```

## Registration Options

### Basic Registration

```csharp
// From configuration
services.AddElasticsearchServices(configuration, registry: null);

// With pre-configured client
services.AddElasticsearchServices(elasticsearchClient, registry: null);

// With client settings callback
services.AddElasticsearchServices(configuration, registry: null, configureSettings: settings =>
{
    settings.DisableDirectStreaming();
});
```

### Repositories

A repository deriving from `ElasticRepositoryBase<TDocument>` implements three framework interfaces:
`IElasticRepositoryBase<TDocument>` (structured writes), `IElasticRepositoryBaseQuery<TDocument>` (search),
and `IInitializeElasticIndex` (index creation at startup). Register all three against one shared instance:

```csharp
services.AddElasticRepository<AuditLogDocument, AuditLogRepository>();
```

Everything resolves the same singleton, so the instance whose index was initialized is the instance that
serves requests. Call `host.InitializeElasticsearchIndexesAsync()` once at startup to run the registered
initializers — there is no need to write a hosted service that enumerates them.

:::warning A repository that reads a projection index must not initialize it

If the repository's document type is a **projection** registered with `AddElasticSearchProjectionStore<T>()`,
register the concrete repository yourself and do **not** use either helper — both add it to the
`IInitializeElasticIndex` set, and the projection store already owns that index. Both resolve the same name
through `ElasticSearchProjectionIndexConvention`, but the store's mapping is the richer one: it declares the
projection position field, applies the configured `IndexMappingConvention`, and uses the configured shard,
replica and refresh settings. A repository initializer running first at startup wins create-if-not-exists
and leaves the position field to dynamic mapping, which an index convention setting `dynamic: strict` then
rejects on every positioned write.

Leave `InitializeIndexAsync` alone in that case. It does nothing unless you override it, which is the
correct behaviour for an index someone else owns.

:::

A repository can be scoped instead:

```csharp
services.AddElasticRepository<CustomerProjection, CustomersListRepository>(ServiceLifetime.Scoped);
```

The repository and both framework interfaces become scoped. Index initialization still happens, but
through a separate singleton instance, because the initializer set is enumerated once from the root — so a
repository must not carry state from `InitializeAsync` into its query methods. A repository that injects a
scoped dependency cannot be registered this way at all: the container rejects the singleton initializer at
startup and names the scoped dependency. Register the concrete type yourself in that case, and initialize
its index from somewhere that has a scope.

If consumers inject a repository interface you declare yourself, register that instead:

```csharp
services.AddRepository<IOrderRepository, OrderRepository>();
```

This one registers the interface as scoped, so it suits a repository with scoped dependencies. Both
helpers add to the index-initializer set, so registering several repositories initializes every index.

### Resilient Client

Adds Polly-based retry and circuit breaker policies:

```csharp
services.AddResilientElasticsearchServices(configuration);
```

### Monitoring

```csharp
services.AddElasticsearchMonitoring(configuration);
```

### Combined Resilient + Monitoring

```csharp
services.AddMonitoredResilientElasticsearchServices(configuration);
```

### Index Management

```csharp
services.AddElasticsearchIndexManagement(configuration);
```

### Projection Store

```csharp
// Register all projections together (shared cluster)
services.AddElasticSearchProjections("https://es.example.com:9200", projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.Index.IndexName = "customers");
});
```

Projections are stored flat as the document root — no envelope wrapper. Custom repositories using `ElasticRepositoryBase<T>` can query the same index with natural field names. See [Projections — Document Storage Format](../event-sourcing/projections.md#document-storage-format) for details.

#### Verifying the cluster at start-up

A projection store builds its client on first use. A host that cannot reach its cluster therefore starts
normally, logs nothing, and discovers the problem when something first writes a document — as one error
per document, from inside whatever background job or request happens to run first. The host is up, the
logs are quiet, and the read models are not being written.

Ask once, at start-up, while someone is watching:

```csharp
var host = builder.Build();

await host.VerifyElasticSearchProjectionConnectivityAsync();

await host.RunAsync();
```

It pings every distinct cluster your registered projection stores are configured to use — once per
cluster, not once per projection — using **those stores' own settings**, so the nodes, connection-pool
kind, timeout and credential tested are the ones the stores will actually connect with. It throws an
`InvalidOperationException` naming the cluster and the projections that write to it when one does not
answer, and also when no projection store is registered at all (a probe with nothing to probe has
verified nothing, and reporting success for it would be the silence this exists to remove).

:::note It is a separate call on purpose
Refusing to start on an unreachable dependency is a trade, not an improvement. Under an orchestrator it
turns a transient cluster blip into a crash-loop, and a read-model host that starts degraded and
recovers is often the better outcome. Call it to fail fast; catch and log it to start degraded; or omit
it. The choice is yours, which is why it is not wired into registration.

This is also not configuration validation, and is deliberately not expressed as `IValidateOptions<T>`
with `ValidateOnStart` alongside the checks that already run there. Those answer whether configuration
is well-formed — the same answer every time they are asked. Reachability has a different answer at two
different moments with identical configuration, and reporting it as invalid options would send you to
fix settings that are correct.
:::

#### Builder Chain Integration

When using the `AddExcalibur` composition root, register ElasticSearch projections inside the event sourcing builder:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .AddElasticSearchProjections("https://es.example.com:9200", projections =>
        {
            projections.Add<OrderSummary>();
            projections.Add<CustomerProfile>(o => o.Index.IndexName = "customers");
        })));
```

Or register a single projection store directly:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .AddElasticSearchProjectionStore<OrderSummary>(opts =>
        {
            opts.NodeUri = "https://es.example.com:9200";
            opts.Index.IndexPrefix = "orders";
        })));
```

#### Index Mapping Conventions

By default, Excalibur infers Elasticsearch field mappings from CLR property types using `DefaultIndexMappingConvention`:

| CLR Type | Elasticsearch Type |
|----------|-------------------|
| `string` | `keyword` |
| `int`, `long` | `long` |
| `decimal`, `double` | `double` |
| `bool` | `boolean` |
| `DateTime`, `DateTimeOffset` | `date` |

To customize mappings (e.g., full-text search with analyzers), implement `IIndexMappingConvention`:

```csharp
public class TextSearchConvention : IIndexMappingConvention
{
    public Properties ConfigureMappings(Type projectionType, Properties inferredProperties)
    {
        // Modify inferred mappings or replace entirely
        // Example: change string fields to text+keyword multi-field
        return inferredProperties;
    }
}

// Apply via options
services.AddElasticSearchProjections("https://es.example.com:9200", projections =>
{
    projections.Add<ProductSearch>(o =>
        o.Index.IndexMappingConvention = new TextSearchConvention());
});
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddElasticHealthCheck("elasticsearch", timeout: TimeSpan.FromSeconds(5));
```

### Security

```csharp
services.AddElasticsearchSecurity(configuration);
```

### Performance Optimizations

```csharp
services.AddResilientElasticsearchServices(configuration);
```

## Resilient Client

### Projection error persistence

`AddElasticSearchProjectionManagement(configuration)` uses one validated management configuration
for its error handler and other management services. `ElasticSearch:Projections` remains accepted;
`ElasticSearch:ProjectionManagement` is applied afterward and wins for explicitly configured values.
Direct `ProjectionErrorHandler` construction still accepts `ElasticsearchConfigurationOptions.Projections`.

```json
{
  "ElasticSearch": {
    "ProjectionManagement": {
      "ErrorHandling": { "ErrorIndexName": "projection-errors", "StoreErrors": true },
      "RetryPolicy": {
        "Enabled": true,
        "MaxIndexAttempts": 3,
        "MaxBulkAttempts": 2,
        "BaseDelay": "00:00:01",
        "MaxDelay": "00:00:30",
        "UseExponentialBackoff": true,
        "UseJitter": true
      }
    }
  }
}
```

These attempt limits include the initial request. Polly owns error-record write retries;
Each attempt selects one eligible node through the supplied SDK client's node pool, honoring its
node predicate and any globally forced node, then uses `ForceNode` for a single SDK attempt.
The pinned Elastic.Transport 0.17.1 ignores request-local `MaxRetries`, so setting it to zero alone
cannot enforce this budget. `ForceNode` is the SDK's documented no-retry path. The shared client,
authentication, request invoker and serializers remain unchanged; subsequent Polly attempts can
select another node from the same cluster. This does not preserve automatic per-request SDK
ping/sniff/failover behavior. Metadata lookup is also a single forced-node request; index provisioning
retains its separate SDK behavior and is outside the write budget. Transient 429, 502, 503 and 504 responses, known
connection/name-resolution failures, prematurely ended responses and identifiable timeouts are
retried. A known HTTP status takes precedence over a secondary transport failure; known permanent
statuses are never retried. Unknown exceptions, TLS failures and caller cancellation are excluded. Mixed bulk responses retry only transient
failed creates whose operation and identifiers match the submitted records. Successes and permanent
failures are removed from the next request. Malformed item mappings terminate the operation.
Retry logs report the next attempt, total budget and delay. A jitter factor of 0.2 varies the
backoff by up to 20% in either direction, with the configured maximum applied afterward.

Each call resolves and pins its concrete write index before retrying, so an alias rollover cannot
send the retry to another index. A direct index or an alias with an unambiguous write index is
supported, including its index routing. Metadata resolution requires `view_index_metadata` (or
`manage`); unresolved targets, ambiguous aliases and data streams are refused for error persistence.
The configured target remains the target for searches and resolution operations.

Records have stable IDs and use create-only writes, preventing a retry from overwriting an existing
resolved record. A conflict is a terminal warning, not proof that the stored payload matches.
Keep the supplied metadata dictionary and its values unchanged until the handler call completes;
the SDK may serialize them again for a later attempt. Arbitrary metadata object graphs are not deep-cloned.
An unacknowledged create may have committed; retries are safe against a retained record in the pinned
index. This is best-effort diagnostic persistence, not an exactly-once guarantee: deleting a record
or deleting and recreating its index during retries removes that protection. Retention must preserve
records through the retry window. Caller cancellation propagates; exhausted or permanent failures
are logged. Index provisioning, read-model updates, reindexing, alias changes and evolving rebuild
status writes are outside this retry policy.

See the [Polly retry strategy](https://www.pollydocs.org/strategies/retry.html) and Elasticsearch's
[index metadata API](https://www.elastic.co/docs/api/doc/elasticsearch/operation/operation-indices-get)
for delay semantics and required permissions. The pinned
[BoundConfiguration implementation](https://github.com/elastic/elastic-transport-net/blob/6197ab83c50e41507a8fb6f31357705fa6a8032a/src/Elastic.Transport/Components/Pipeline/BoundConfiguration.cs#L44-L46)
computes retry count from global settings unless a node is forced. Node selection consumes the SDK's
[complete node view](https://github.com/elastic/elastic-transport-net/blob/6197ab83c50e41507a8fb6f31357705fa6a8032a/src/Elastic.Transport/Components/NodePool/NodePool.cs#L39-L41).

The `IResilientElasticsearchClient` wraps the Elasticsearch client with retry and circuit breaker policies:

```csharp
public interface IResilientElasticsearchClient
{
    bool IsCircuitBreakerOpen { get; }

    Task<SearchResponse<TDocument>> SearchAsync<TDocument>(SearchRequest request, CancellationToken cancellationToken);
    Task<IndexResponse> IndexAsync<TDocument>(IIndexRequest<TDocument> request, CancellationToken cancellationToken);
    Task<UpdateResponse<TDocument>> UpdateAsync<TDocument>(IUpdateRequest<TDocument, object> request, CancellationToken cancellationToken);
    Task<DeleteResponse> DeleteAsync(IDeleteRequest request, CancellationToken cancellationToken);
    Task<BulkResponse> BulkAsync(IBulkRequest request, CancellationToken cancellationToken);
    Task<GetResponse<TDocument>> GetAsync<TDocument>(IGetRequest request, CancellationToken cancellationToken);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
```

## Index Lifecycle Management

Manage indices, templates, aliases, and ILM policies:

- `IIndexInitializer` — Bootstrap indices on startup
- `IIndexTemplateManager` — Manage index templates
- `IIndexLifecycleManager` — Configure ILM policies
- `IIndexOperationsManager` — CRUD operations on indices
- `IIndexAliasManager` — Manage index aliases

## Index Field Mappings

By default, Elasticsearch uses dynamic mapping to guess field types from the first document indexed. This can produce incorrect types — for example, mapping a numeric string as `long` instead of `keyword`, or missing full-text search capability on name fields.

Excalibur provides a **three-tier mapping strategy** that gives you control over how fields are mapped:

### Tier 1: Explicit Mapping (Recommended)

Implement `IElasticIndexConfiguration<T>` on your document class for full control:

```csharp
using Elastic.Clients.Elasticsearch.Mapping;
using Excalibur.Data.ElasticSearch;

public sealed class CustomerSearchProjection : IElasticIndexConfiguration<CustomerSearchProjection>
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<string> Tags { get; set; } = [];

    public static Properties ConfigureIndex() => new()
    {
        { "customerId", new KeywordProperty() },
        { "name", new TextProperty
            {
                Fields = new Properties
                {
                    { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                }
            }
        },
        { "email", new TextProperty
            {
                Fields = new Properties
                {
                    { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                }
            }
        },
        { "totalSpent", new DoubleNumberProperty() },
        { "isActive", new BooleanProperty() },
        { "createdAt", new DateProperty() },
        { "tags", new KeywordProperty() }
    };
}
```

Use explicit mapping when you need:
- Full-text search fields (`TextProperty`) with keyword sub-fields
- Nested object queries (`NestedProperty`)
- Custom analyzers or field-specific settings

### Tier 2: Reflection-Inferred Mapping (Good Default)

When `IElasticIndexConfiguration<T>` is not implemented, the framework reflects over public properties and maps them to appropriate Elasticsearch types:

| .NET Type | Elasticsearch Type |
|---|---|
| `string`, `Guid`, enums | `keyword` |
| `int`, `short`, `byte`, `long` (and unsigned variants) | `long` |
| `float`, `double`, `decimal` | `double` |
| `DateTime`, `DateTimeOffset`, `DateOnly` | `date` |
| `bool` | `boolean` |
| `List<string>`, `string[]`, `IReadOnlyList<string>` | `keyword` |
| Complex nested types | Skipped (ES dynamic mapping) |

Nullable types are unwrapped — `int?` maps to `long`, `DateTime?` maps to `date`, etc.

This tier is automatic and requires no code changes. It is suitable when all string fields are exact-match (IDs, codes, statuses) and no full-text search is needed.

### Tier 3: Dynamic Mapping (Fallback)

If both explicit and inferred mapping are bypassed, Elasticsearch uses its own dynamic mapping rules. This is not recommended for production — it can produce incorrect types (e.g., mapping `"12345"` as `long` instead of `keyword`).

### Using Mappings with ElasticRepositoryBase

For non-projection documents using `ElasticRepositoryBase<T>`, call `InitializeIndexWithMappingsAsync` in your override:

```csharp
public class CustomerRepository : ElasticRepositoryBase<CustomerDocument>
{
    public CustomerRepository(ElasticsearchClient client)
        : base(client, "customers") { }

    public override async Task InitializeIndexAsync(CancellationToken cancellationToken)
    {
        await InitializeIndexWithMappingsAsync(
            numberOfShards: 1,
            numberOfReplicas: 1,
            cancellationToken).ConfigureAwait(false);
    }
}
```

That override is all a repository needs: `InitializeAsync` — the member the registered initializer set
awaits — calls it, so `host.InitializeElasticsearchIndexesAsync()` provisions the index at startup.

Provisioning raises if it fails. The settings and mappings live in the create request, so a store or
repository that carried on after a failed create would have the first write auto-create the index with a
dynamically inferred mapping — discarding the declared mapping, the shard and replica counts and the
refresh interval while reporting healthy. A create refused because the index already exists is not a
failure: two instances starting together both attempt it, and the loser carries on.

The projection store (`ElasticSearchProjectionStore<T>`) uses the three-tier strategy automatically during index creation — no additional configuration is needed.

## Cursor-Based Pagination

Excalibur provides cursor-based (keyset) pagination that maps directly to Elasticsearch's `search_after` API — delivering consistent, scalable paging without the performance cliff of deep `from + size` offsets.

### Core Components

| Component | Package | Purpose |
|---|---|---|
| `CursorEncoder` | `Excalibur.EventSourcing.Abstractions` | Backend-agnostic Base64url cursor encoding/decoding |
| `ElasticSearchCursorHelper` | `Excalibur.Data.ElasticSearch` | Converts between `CursorEncoder` primitives and ES `FieldValue` sort values |
| `CursorPagedResult<T>` | `Excalibur.EventSourcing.Abstractions` | Result type with items, total count, and opaque forward/backward cursors |

### How It Works

1. **First request** — no cursor, query returns the first page sorted by your chosen fields
2. **Subsequent requests** — pass the opaque cursor from the previous response (`NextCursor` to go forward, `PreviousCursor` to go back); the framework decodes it into `search_after` sort values
3. **Boundaries** — `NextCursor` is `null` on the last page (`HasMore == false`); `PreviousCursor` is `null` on the first page (`HasPrevious == false`)

### Usage in a Controller

```csharp
[HttpGet("search")]
public async Task<CursorPagedResult<OrderSearchProjection>> Search(
    CancellationToken cancellationToken,
    [FromQuery] string? query,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? cursor = null,
    [FromQuery] PageNavigation navigation = PageNavigation.Next)
{
    // Decode cursor into ES sort values (null for first page / First & Last navigation)
    var searchAfter = ElasticSearchCursorHelper.DecodeCursor(cursor);

    // For Previous/Last, reverse the sort so search_after walks backward;
    // ToCursorResult flips the items back to display order.
    var reverse = navigation is PageNavigation.Previous or PageNavigation.Last;
    var primary = reverse ? SortOrder.Asc : SortOrder.Desc;
    var tiebreak = reverse ? SortOrder.Desc : SortOrder.Asc;

    var searchRequest = new SearchRequestDescriptor<OrderSearchProjection>()
        .Index("orders")
        .Sort(s => s.Field(f => f.CreatedAt, new FieldSort { Order = primary }))
        .Sort(s => s.Field("_id", new FieldSort { Order = tiebreak }));

    if (!string.IsNullOrWhiteSpace(query))
    {
        searchRequest.Query(q => q.MultiMatch(m => m
            .Query(query)
            .Fields(new[] { "customerName", "status" })));
    }

    // Set page size (over-fetching one peek row) and apply the cursor. First/Last
    // navigate without a cursor; Next/Previous pass the decoded search_after.
    var pageCursor = navigation is PageNavigation.Next or PageNavigation.Previous
        ? searchAfter
        : null;
    ElasticSearchCursorHelper.ApplyCursorPaging(searchRequest, pageSize, pageCursor);

    var response = await elasticClient.SearchAsync(searchRequest, cancellationToken);

    // Build a bidirectional result: NextCursor (forward) + PreviousCursor (backward),
    // peek row trimmed.
    return ElasticSearchCursorHelper.ToCursorResult(response, pageSize, navigation);
}
```

### Bidirectional Pagination

`ToCursorResult` is given the `PageNavigation` the query was issued for and returns both `NextCursor` and `PreviousCursor`. For **Previous** and **Last**, reverse the sort order before searching (as shown above) — `ToCursorResult` detects the reversed navigation and flips the items back into display order automatically. No `reverseItems` flag is needed.

### Supported Sort Value Types

The `CursorEncoder` handles all common Elasticsearch sort value types:

| Type | Encoding | Round-trip behavior |
|---|---|---|
| `string` | JSON string | Exact round-trip |
| `long`, `int` | JSON number | Decoded as `long` |
| `double`, `float`, `decimal` | JSON number | Decoded as `long` (if integer) or `double` |
| `bool` | JSON true/false | Exact round-trip |
| `null` | JSON null | Exact round-trip |
| `DateTimeOffset`, `DateTime` | Unix epoch milliseconds | Decoded as `long` |
| `DateOnly`, `TimeOnly` | ISO 8601 string | Decoded as `string` |

Cursors are Base64url-encoded (URL-safe, no padding) and opaque to consumers — the internal format may change between versions.

### Design Notes

- **Backend-agnostic**: `CursorEncoder` and `CursorPagedResult<T>` live in the `Excalibur.EventSourcing` namespace (package `Excalibur.EventSourcing.Abstractions`) with no Elasticsearch dependency. They work with any store that supports keyset pagination (SQL Server, CosmosDB, etc.).
- **Corrupt cursors are safe**: Invalid or tampered cursors return `null` from `DecodeCursor`, causing the query to start from the beginning rather than failing.
- **Always include a tiebreaker sort**: Use `_id` or another unique field as the last sort criterion to ensure deterministic ordering when primary sort values are identical.

## Audit Exporter

A separate package indexes audit events into Elasticsearch for search, dashboards, and alerting:

```bash
dotnet add package Excalibur.AuditLogging.Elasticsearch
```

```csharp
using Excalibur.AuditLogging.Elasticsearch;

// Single node
services.AddElasticsearchAuditExporter(es =>
{
    es.NodeUri(new Uri("https://es.example.com:9200"))
      .IndexName("dispatch-audit");
});

// Or a cluster -- every retry attempt round-robins to the next node
services.AddElasticsearchAuditExporter(es =>
{
    es.NodeUris([new Uri("https://es1:9200"), new Uri("https://es2:9200"), new Uri("https://es3:9200")])
      .IndexName("dispatch-audit");
});

// Or bind the whole option set from configuration
services.AddElasticsearchAuditExporter(es => es.BindConfiguration("AuditExporter:Elasticsearch"));
```

The builder covers connection (`NodeUri`, `NodeUris`, `CloudId`), index naming (`IndexName`),
and configuration binding (`BindConfiguration`). The remaining settings on
`ElasticsearchExporterOptions` -- `ApiKey`, `BulkBatchSize`, `RefreshPolicy`, `ApplicationName`,
`MaxRetryAttempts`, `RetryBaseDelay`, `Timeout` -- come from the bound configuration section, or
from a `Configure` call registered after the exporter:

```csharp
using Excalibur.AuditLogging.Elasticsearch;

services.AddElasticsearchAuditExporter(es => es.NodeUri(new Uri("https://es.example.com:9200")));
services.Configure<ElasticsearchExporterOptions>(o =>
{
    o.ApiKey = configuration["Elasticsearch:ApiKey"];
    o.BulkBatchSize = 500;
    o.ApplicationName = "MyApp"; // fallback when AuditEvent.ApplicationName is null
});
```

:::info

Elasticsearch is a search/analytics projection of the audit trail, not a compliance-grade audit store: the package registers `IAuditLogExporter` and never `IAuditStore`. Use SQL Server for tamper-evident hash-chained storage. See [provider compliance boundary](../compliance/audit-logging.md#provider-compliance-boundary) and [Audit Logging Providers](../observability/audit-logging-providers.md#elasticsearch-audit-exporter).
:::

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [MongoDB Provider](./mongodb.md) — Document store alternative
- [Audit Logging Providers](../observability/audit-logging-providers.md) — All audit backend configurations
- [Observability](../observability/index.md) — Elasticsearch for log aggregation
