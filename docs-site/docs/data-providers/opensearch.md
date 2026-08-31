---
sidebar_position: 11
title: OpenSearch
description: OpenSearch provider with projections, index state management, health checks, dead letter handling, materialized views, and tenant sharding.
---

# OpenSearch Provider

Built on `OpenSearch.Client`. Covers projections, index state management (ISM), health checks, dead letter handling, materialized views, and tenant sharding.

## Before You Start

- **.NET 10.0**
- An OpenSearch cluster (local, AWS OpenSearch Service, or self-hosted)
- Familiarity with [data access](../data-access/index.md) and [projections](../event-sourcing/projections.md)

## Installation

```bash
dotnet add package Excalibur.Data.OpenSearch
```

**Dependencies:** `Excalibur.Data.Abstractions`, `OpenSearch.Client`

## Quick Start

```csharp
// Register OpenSearch client + projection store
services.AddOpenSearchServices("https://opensearch.example.com:9200");

services.AddOpenSearchProjectionStore<OrderSummary>(opts =>
{
    opts.NodeUri = "https://opensearch.example.com:9200";
    opts.IndexName = "order-summaries";
});
```

## Registration Options

### Client Registration

The projection store uses a client registered in DI and falls back to `OpenSearchProjectionStoreOptions.NodeUri` only when none is registered. It accepts either registration shape — the `IOpenSearchClient` interface, or the concrete `OpenSearchClient` that every registration entry point in this package produces — so `AddOpenSearchServices(...)` and `AddExcaliburOpenSearch(...)` are each sufficient on their own. No separate interface registration is required.

```csharp
var client = new OpenSearchClient(new ConnectionSettings(new Uri("https://opensearch.example.com:9200")));

services.AddOpenSearchServices(client);   // the projection store resolves and uses this client
```

Registering a client is what keeps the store off `NodeUri`. With no client in the container the store builds its own from the configured node address, which is a local address by default — so a missed registration surfaces only when nothing happens to be listening there.


```csharp
// Single node
services.AddOpenSearchServices("https://opensearch.example.com:9200");

// Multi-node cluster
services.AddOpenSearchServices(new[]
{
    new Uri("https://node1.example.com:9200"),
    new Uri("https://node2.example.com:9200"),
    new Uri("https://node3.example.com:9200"),
});

// With custom connection settings
services.AddOpenSearchServices("https://opensearch.example.com:9200",
    configureSettings: settings =>
    {
        settings.BasicAuthentication("admin", "password");
        settings.DisableDirectStreaming();
    });

// With preconfigured client
var client = new OpenSearchClient(new ConnectionSettings(new Uri("https://...")));
services.AddOpenSearchServices(client);
```

### Projection Store

```csharp
// Per-projection registration
services.AddOpenSearchProjectionStore<OrderSummary>(opts =>
{
    opts.NodeUri = "https://opensearch.example.com:9200";
    opts.IndexName = "order-summaries";
});

// With node URI shorthand
services.AddOpenSearchProjectionStore<OrderSummary>(
    "https://opensearch.example.com:9200");

// With shared client factory
services.AddOpenSearchProjectionStore<OrderSummary>(
    clientFactory: sp => sp.GetRequiredService<OpenSearchClient>(),
    configureOptions: opts => opts.IndexName = "order-summaries");

// Batch registration (multiple projections, shared node)
services.AddOpenSearchProjections("https://opensearch.example.com:9200", projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(opts => opts.IndexName = "customers");
    projections.Add<ProductCatalog>(opts => opts.IndexName = "products");
});
```

### Retries and Timeouts

Retry, timeout, and connection-pool behaviour belong to `OpenSearch.Client` and are configured on the
`ConnectionSettings` you hand to the client. This package does not wrap the client in a policy of its own.

```csharp
services.AddOpenSearchServices("https://opensearch.example.com:9200",
    configureSettings: settings =>
    {
        settings.MaximumRetries(3);
        settings.RequestTimeout(TimeSpan.FromSeconds(30));
    });
```

For policy-based resilience across a wider surface than a single client call, compose
`Microsoft.Extensions.Resilience` / Polly around your own call sites.

### Health Checks

`AddOpenSearchHealthCheck` requires a name and a timeout -- neither has a default.

```csharp
services.AddHealthChecks()
    .AddOpenSearchHealthCheck(name: "opensearch", timeout: TimeSpan.FromSeconds(10));
```

### Index Lifecycle Management (ISM)

OpenSearch uses ISM (Index State Management) instead of Elasticsearch's ILM:

Index management is opt-in. Register a client first; the managers resolve from whichever
client registration is present -- the `IOpenSearchClient` interface or the concrete
`OpenSearchClient`. Resolving a manager with no client in the container throws and names the
missing registration.

```csharp
services.AddOpenSearchServices("https://opensearch.example.com:9200");
services.AddOpenSearchIndexManagement();

// Resolvable after the call above:
//   IIndexLifecycleManager   -- ISM policy management
//   IIndexTemplateManager    -- index and component templates
//   IIndexOperationsManager  -- index CRUD, rollover, optimization
//   IIndexAliasManager       -- alias management
```

### Materialized Views

```csharp
services.AddMaterializedViews(views =>
{
    views.UseOpenSearch(opts =>
    {
        opts.NodeUri = "https://opensearch.example.com:9200";
    });
});

// Or with the node URI shorthand
services.AddMaterializedViews(views => views.UseOpenSearch("https://opensearch.example.com:9200"));
```

### Dead Letter Handling

Failed documents are captured in a dead letter index:

```csharp
services.Configure<OpenSearchDeadLetterOptions>(opts =>
{
    opts.DeadLetterIndexPrefix = "dead-letters";
    opts.MaxRetryCount = 3;
    opts.RetentionPeriod = TimeSpan.FromDays(30);
});

services.AddSingleton<OpenSearchDeadLetterHandler>();
```

### Tenant Sharding

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.EnableTenantSharding(opts => opts.DefaultShardId = "shard-default");
    builder.UseOpenSearchTenantProjectionStore<OrderSummary>();
}));
```

### Persistence Provider

```csharp
services.AddOpenSearchPersistence(opts =>
{
    opts.RefreshPolicy = OpenSearchRefreshPolicy.WaitFor;
});
```

### Host Extensions

Verify cluster connectivity at startup. The call pings the cluster and throws
`InvalidOperationException` if it is unreachable.

```csharp
var host = builder.Build();
await host.VerifyOpenSearchConnectivityAsync();
await host.RunAsync();
```

## Elasticsearch vs OpenSearch Comparison

| Feature | Elasticsearch Package | OpenSearch Package |
|---------|----------------------|-------------------|
| Client library | `Elastic.Clients.Elasticsearch` | `OpenSearch.Client` (NEST-based) |
| Index lifecycle | ILM (Index Lifecycle Management) | ISM (Index State Management) |
| Projection store | `AddElasticSearchProjectionStore<T>` | `AddOpenSearchProjectionStore<T>` |
| Batch registration | `AddElasticSearchProjections()` | `AddOpenSearchProjections()` |
| Health check | `AddElasticsearchHealthCheck()` | `AddOpenSearchHealthCheck(name, timeout)` |
| Index management | `AddElasticsearchIndexManagement()` | `AddOpenSearchIndexManagement()` |
| Tenant sharding | `UseElasticSearchTenantProjectionStore<T>` | `UseOpenSearchTenantProjectionStore<T>` |
| Event ID range | 106000-106999 | 108000-108999 |
| Audit exporter | `AddElasticsearchAuditExporter()` | `AddOpenSearchAuditExporter()` |
| Audit event IDs | 93471-93475 | 93490-93505 |

The two are close but not identical. The Elasticsearch package additionally ships schema evolution,
projection rebuild management, a security/auditing subsystem, and a client-side resilience wrapper;
the OpenSearch package does not. Everything listed in the table above is present in both.

## Audit Exporter

A separate package indexes audit events into OpenSearch for search, dashboards, and alerting:

```bash
dotnet add package Excalibur.AuditLogging.OpenSearch
```

```csharp
using Excalibur.AuditLogging.OpenSearch;

// Single node
services.AddOpenSearchAuditExporter(os =>
{
    os.NodeUri(new Uri("https://os.example.com:9200"))
      .IndexName("dispatch-audit");
});

// Or a cluster -- every retry attempt round-robins to the next node
services.AddOpenSearchAuditExporter(os =>
{
    os.NodeUris([new Uri("https://os1:9200"), new Uri("https://os2:9200"), new Uri("https://os3:9200")])
      .IndexName("dispatch-audit");
});

// Or bind the whole option set from configuration
services.AddOpenSearchAuditExporter(os => os.BindConfiguration("AuditExporter:OpenSearch"));
```

The builder covers connection (`NodeUri`, `NodeUris`), index naming (`IndexName`), and
configuration binding (`BindConfiguration`). The remaining settings on
`OpenSearchExporterOptions` -- `ApiKey`, `BulkBatchSize`, `RefreshPolicy`, `ApplicationName`,
`MaxRetryAttempts`, `RetryBaseDelay`, `Timeout` -- come from the bound configuration section, or
from a `Configure` call registered after the exporter:

```csharp
using Excalibur.AuditLogging.OpenSearch;

services.AddOpenSearchAuditExporter(os => os.NodeUri(new Uri("https://os.example.com:9200")));
services.Configure<OpenSearchExporterOptions>(o =>
{
    o.ApiKey = configuration["OpenSearch:ApiKey"];
    o.BulkBatchSize = 500;
    o.ApplicationName = "MyApp"; // fallback when AuditEvent.ApplicationName is null
});
```

:::info

OpenSearch is a search/analytics projection of the audit trail, not a compliance-grade audit store: the package registers `IAuditLogExporter` and never `IAuditStore`. Use SQL Server for tamper-evident hash-chained storage. See [provider compliance boundary](../compliance/audit-logging.md#provider-compliance-boundary) and [Audit Logging Providers](../observability/audit-logging-providers.md#opensearch-audit-exporter).
:::

## See Also

- [Elasticsearch Provider](./elasticsearch.md) -- Elastic-based equivalent
- [Projections](../event-sourcing/projections.md) -- Projection concepts and builder API
- [Audit Logging Providers](../observability/audit-logging-providers.md) -- All audit backend configurations
- [Event Store Providers](../event-sourcing/providers.md) -- Event store provider comparison
