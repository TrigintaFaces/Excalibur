---
sidebar_position: 10
title: Elasticsearch
description: Elasticsearch provider with resilient client, index management, projections, and health monitoring.
---

# Elasticsearch Provider

The Elasticsearch provider offers full-text search and analytics with a resilient client wrapper, index lifecycle management, projection store integration, and health monitoring.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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

See [Projections](../event-sourcing/projections.md#registering-store-backends) for index naming, multi-node clusters, and per-projection overrides.

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

## Audit Sink

A separate package provides an Elasticsearch audit sink for real-time audit event indexing:

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.Elasticsearch
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
