---
sidebar_position: 11
title: OpenSearch
description: OpenSearch provider with resilient client, index management, projections, health monitoring, and materialized views.
---

# OpenSearch Provider

Full parity with the [Elasticsearch provider](./elasticsearch.md), built on `OpenSearch.Client`. Covers projections, index lifecycle management (ISM), resilient client, health monitoring, dead letter handling, materialized views, and tenant sharding.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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

### Resilient Client

Adds retry and circuit breaker policies around OpenSearch operations:

```csharp
services.Configure<OpenSearchResilienceOptions>(opts =>
{
    opts.MaxRetries = 3;
    opts.CircuitBreakerThreshold = 5;
});
```

| Option Type | Key Settings |
|-------------|-------------|
| `OpenSearchResilienceOptions` | MaxRetries, CircuitBreakerThreshold |
| `OpenSearchRetryPolicyOptions` | MaxRetries, BaseDelay, MaxDelay |
| `CircuitBreakerOptions` | FailureThreshold, ResetTimeout, HalfOpenMaxAttempts |
| `OpenSearchTimeoutOptions` | RequestTimeout, ConnectionTimeout |

### Monitoring

```csharp
services.Configure<OpenSearchMonitoringOptions>(opts =>
{
    // Enable health monitoring, metrics, request logging
});
```

Includes:
- **Health monitoring** via `OpenSearchHealthMonitor` (cluster green/yellow/red)
- **OTel metrics** via `OpenSearchMetrics` (operations, latency, errors)
- **Activity tracing** via `OpenSearchActivitySource`
- **Request logging** via `OpenSearchRequestLogger`
- **Performance diagnostics** via `OpenSearchPerformanceDiagnostics`

### Health Checks

```csharp
services.AddHealthChecks()
    .AddOpenSearchHealthCheck();
```

### Index Lifecycle Management (ISM)

OpenSearch uses ISM (Index State Management) instead of Elasticsearch's ILM:

```csharp
// Interfaces available via DI
// IIndexLifecycleManager -- ISM policy management
// IIndexTemplateManager -- Index template management
// IIndexOperationsManager -- Index CRUD, aliases, rollover
// IIndexAliasManager -- Alias management
```

### Materialized Views

```csharp
services.AddOpenSearchMaterializedViews(opts =>
{
    opts.NodeUri = "https://opensearch.example.com:9200";
});
```

### Dead Letter Handling

Failed documents are captured in a dead letter index:

```csharp
services.Configure<OpenSearchDeadLetterOptions>(opts =>
{
    opts.IndexName = "dead-letters";
    opts.MaxRetries = 3;
});
```

### Tenant Sharding

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.EnableTenantSharding(opts => opts.EnableTenantSharding = true);
    builder.UseOpenSearchTenantProjectionStore<OrderSummary>();
});
```

### Persistence Provider

```csharp
services.AddOpenSearchPersistence(opts =>
{
    opts.RefreshPolicy = OpenSearchRefreshPolicy.WaitFor;
});
```

### Host Extensions

Initialize indexes at application startup:

```csharp
var host = builder.Build();
await host.InitializeOpenSearchIndexesAsync();
await host.RunAsync();
```

## Elasticsearch vs OpenSearch Comparison

| Feature | Elasticsearch Package | OpenSearch Package |
|---------|----------------------|-------------------|
| Client library | `Elastic.Clients.Elasticsearch` | `OpenSearch.Client` (NEST-based) |
| Index lifecycle | ILM (Index Lifecycle Management) | ISM (Index State Management) |
| Projection store | `AddElasticSearchProjectionStore<T>` | `AddOpenSearchProjectionStore<T>` |
| Batch registration | `AddElasticSearchProjections()` | `AddOpenSearchProjections()` |
| Health check | `AddElasticsearchHealthCheck()` | `AddOpenSearchHealthCheck()` |
| Resilient client | `AddResilientElasticsearchServices()` | `IResilientOpenSearchClient` + options |
| Tenant sharding | `UseElasticSearchTenantProjectionStore<T>` | `UseOpenSearchTenantProjectionStore<T>` |
| Event ID range | 106000-106999 | 108000-108999 |

The two packages provide **feature parity**. Choose based on your search engine.

## See Also

- [Elasticsearch Provider](./elasticsearch.md) -- Elastic-based equivalent
- [Projections](../event-sourcing/projections.md) -- Projection concepts and builder API
- [Event Store Providers](../event-sourcing/providers.md) -- Event store provider comparison
