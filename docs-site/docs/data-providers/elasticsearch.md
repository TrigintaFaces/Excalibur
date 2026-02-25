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
services.AddElasticsearchProjections(configuration);
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

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [MongoDB Provider](./mongodb.md) — Document store alternative
- [Observability](../observability/index.md) — Elasticsearch for log aggregation
