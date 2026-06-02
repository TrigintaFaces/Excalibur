---
sidebar_position: 10
title: Elasticsearch
description: Elasticsearch provider with resilient client, index management, projections, and health monitoring.
---

# Elasticsearch Provider

The Elasticsearch provider offers full-text search and analytics with a resilient client wrapper, index lifecycle management, projection store integration, and health monitoring.

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
    projections.Add<CustomerProfile>(o => o.IndexName = "customers");
});
```

Projections are stored flat as the document root — no envelope wrapper. Custom repositories using `ElasticRepositoryBase<T>` can query the same index with natural field names. See [Projections — Document Storage Format](../event-sourcing/projections.md#document-storage-format) for details.

#### Builder Chain Integration

When using the `AddExcalibur` composition root, register ElasticSearch projections inside the event sourcing builder:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .AddElasticSearchProjections("https://es.example.com:9200", projections =>
        {
            projections.Add<OrderSummary>();
            projections.Add<CustomerProfile>(o => o.IndexName = "customers");
        })));
```

Or register a single projection store directly:

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .AddElasticSearchProjectionStore<OrderSummary>(opts =>
        {
            opts.NodeUri = "https://es.example.com:9200";
            opts.IndexPrefix = "orders";
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
        o.IndexMappingConvention = new TextSearchConvention());
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

The `IResilientElasticsearchClient` wraps the Elasticsearch client with retry and circuit breaker policies:

```csharp
public interface IResilientElasticsearchClient
{
    // Operations with automatic retry and circuit breaking
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

The projection store (`ElasticSearchProjectionStore<T>`) uses the three-tier strategy automatically during index creation — no additional configuration is needed.

## Cursor-Based Pagination

Excalibur provides cursor-based (keyset) pagination that maps directly to Elasticsearch's `search_after` API — delivering consistent, scalable paging without the performance cliff of deep `from + size` offsets.

### Core Components

| Component | Package | Purpose |
|---|---|---|
| `CursorEncoder` | `Excalibur.EventSourcing.Abstractions` | Backend-agnostic Base64url cursor encoding/decoding |
| `ElasticSearchCursorHelper` | `Excalibur.Data.ElasticSearch` | Converts between `CursorEncoder` primitives and ES `FieldValue` sort values |
| `CursorPagedResult<T>` | `Excalibur.EventSourcing.Abstractions` | Result type with items, total count, and opaque next-page cursor |

### How It Works

1. **First request** — no cursor, query returns the first page sorted by your chosen fields
2. **Subsequent requests** — pass the opaque cursor from the previous response; the framework decodes it into `search_after` sort values
3. **Last page** — `NextCursor` is `null`, `HasMore` is `false`

### Usage in a Controller

```csharp
[HttpGet("search")]
public async Task<CursorPagedResult<OrderSearchProjection>> Search(
    [FromQuery] string? query,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? cursor = null,
    CancellationToken cancellationToken = default)
{
    // Decode cursor into ES sort values (null for first page)
    var searchAfter = ElasticSearchCursorHelper.DecodeCursor(cursor);

    var searchRequest = new SearchRequestDescriptor<OrderSearchProjection>()
        .Index("orders")
        .Size(pageSize)
        .Sort(s => s.Field(f => f.CreatedAt, new FieldSort { Order = SortOrder.Desc }))
        .Sort(s => s.Field("_id", new FieldSort { Order = SortOrder.Asc }));

    if (searchAfter is not null)
    {
        searchRequest.SearchAfter(searchAfter);
    }

    if (!string.IsNullOrWhiteSpace(query))
    {
        searchRequest.Query(q => q.MultiMatch(m => m
            .Query(query)
            .Fields(new[] { "customerName", "status" })));
    }

    var response = await elasticClient.SearchAsync(searchRequest, cancellationToken);

    // Build result with encoded cursor for next page
    return ElasticSearchCursorHelper.ToCursorResult(response, pageSize);
}
```

### Bidirectional Pagination

For previous-page navigation, reverse the sort order and set `reverseItems: true`:

```csharp
// Previous page: reverse sort, then reverse items back to display order
var result = ElasticSearchCursorHelper.ToCursorResult(response, pageSize, reverseItems: true);
```

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

## Audit Sink

A separate package provides an Elasticsearch audit sink for real-time audit event indexing:

```bash
dotnet add package Excalibur.AuditLogging.Elasticsearch
```

```csharp
// With options callback
services.AddElasticsearchAuditSink(options =>
{
    // Single node
    options.ElasticsearchUrl = "https://es.example.com:9200";

    // Or cluster (round-robin)
    options.NodeUrls = ["https://es1:9200", "https://es2:9200", "https://es3:9200"];

    options.IndexPrefix = "dispatch-audit";
    options.ApplicationName = "MyApp"; // fallback if AuditEvent.ApplicationName is null
});

// Or from IConfiguration
services.AddElasticsearchAuditSink(configuration.GetSection("AuditSink:Elasticsearch"));
```

:::info

Elasticsearch serves as a search/analytics sink, not a compliance-grade audit store. Use SQL Server for tamper-evident hash-chained storage. See [ADR-290](../compliance/audit-logging.md#provider-compliance-boundary) and [Audit Logging Providers](../observability/audit-logging-providers.md#elasticsearch-audit-sink).
:::

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [MongoDB Provider](./mongodb.md) — Document store alternative
- [Audit Logging Providers](../observability/audit-logging-providers.md) — All audit backend configurations
- [Observability](../observability/index.md) — Elasticsearch for log aggregation
