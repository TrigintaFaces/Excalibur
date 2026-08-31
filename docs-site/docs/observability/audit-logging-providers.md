---
sidebar_position: 8
title: Audit Logging Providers
description: Per-provider setup for Elasticsearch, OpenSearch, Datadog, Splunk, Sentinel, and SQL Server audit backends.
---

# Audit Logging Providers

Dispatch audit logging uses `IAuditStore` as its core abstraction for compliance-grade storage and `IAuditLogExporter` for search and analytics projections. Provider-specific backends ship audit events to external platforms for analysis, alerting, and compliance reporting.

:::info Compliance Boundary

Only SQL Server (and Postgres) backends implement `IAuditStore` with tamper-evident hash chains. Elasticsearch and OpenSearch register `IAuditLogExporter` only -- write-only, search-optimized projections. They are not compliance-grade stores. See [Compliance Audit Logging](../compliance/audit-logging.md#provider-compliance-boundary) for details.
:::

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Security
  ```
- Familiarity with [audit logging](../security/audit-logging.md) and [compliance](../compliance/audit-logging.md)

## Core Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Default audit logging (in-memory store)
services.AddAuditLogging();

// With the SQL Server store (package: Excalibur.AuditLogging.SqlServer)
services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "audit";
});

// With a factory
services.AddAuditLogging(sp => new CustomAuditStore(sp.GetRequiredService<ILogger>()));
```

### RBAC Audit Store

```csharp
services.AddRbacAuditStore();
```

### Custom Role Provider

```csharp
services.AddAuditRoleProvider<MyRoleProvider>();
```

---

## Elasticsearch (Audit Exporter)

Index audit events into Elasticsearch for full-text search, aggregation dashboards, and real-time alerting. The package registers `IAuditLogExporter` only -- never an `IAuditStore`. See the [provider compliance boundary](../compliance/audit-logging.md#provider-compliance-boundary) for rationale.

### Installation

```bash
dotnet add package Excalibur.AuditLogging.Elasticsearch
```

**Dependencies:** `Excalibur.Compliance.Abstractions`, `Microsoft.Extensions.Http`

### Registration

One entry point covers both the single-event write (`ExportAsync`) and the batched Bulk-API
write (`ExportBatchAsync`), with retry and round-robin cluster failover on every attempt:

```csharp
using Excalibur.AuditLogging.Elasticsearch;

// Single node
services.AddElasticsearchAuditExporter(es =>
{
    es.NodeUri(new Uri("https://es.example.com:9200"))
      .IndexName("dispatch-audit");   // indexes: dispatch-audit-2026.03.31
});

// Or a cluster -- every retry attempt round-robins to the next node
services.AddElasticsearchAuditExporter(es =>
{
    es.NodeUris([new Uri("https://es1:9200"), new Uri("https://es2:9200"), new Uri("https://es3:9200")])
      .IndexName("dispatch-audit");
});

// Or bind the whole option set from configuration (appsettings.json)
services.AddElasticsearchAuditExporter(es => es.BindConfiguration("AuditExporter:Elasticsearch"));
```

The builder exposes connection (`NodeUri`, `NodeUris`, `CloudId`), index naming (`IndexName`),
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
    o.MaxRetryAttempts = 3;
    o.RetryBaseDelay = TimeSpan.FromSeconds(1); // exponential backoff
    o.ApplicationName = "OrderService";         // fallback if AuditEvent.ApplicationName is null
});
```

:::tip ApplicationName Preference

The indexed `application_name` field uses `AuditEvent.ApplicationName` when set, falling back to the options-level `ApplicationName`. Set it once on the event via `ApplicationContext.ApplicationName` (automatic via DI) and every exporter picks it up.
:::

### Recommended Architecture

```
SQL Server = IAuditStore (compliance, hash-chained, tamper-evident)
Elasticsearch = IAuditLogExporter (search, dashboards, alerting)
```

---

## OpenSearch (Audit Exporter)

Full parity with the Elasticsearch audit exporter, built on raw `HttpClient` (no `OpenSearch.Client` dependency). Same compliance boundary applies -- OpenSearch registers `IAuditLogExporter`, not an `IAuditStore`.

### Installation

```bash
dotnet add package Excalibur.AuditLogging.OpenSearch
```

**Dependencies:** `Excalibur.Compliance.Abstractions`, `Microsoft.Extensions.Http`

### Registration

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

// Or bind the whole option set from configuration (appsettings.json)
services.AddOpenSearchAuditExporter(os => os.BindConfiguration("AuditExporter:OpenSearch"));
```

The OpenSearch builder is the Elasticsearch one minus `CloudId`. The remaining settings on
`OpenSearchExporterOptions` come from the bound configuration section, or from a `Configure`
call registered after the exporter:

```csharp
using Excalibur.AuditLogging.OpenSearch;

services.AddOpenSearchAuditExporter(os => os.NodeUri(new Uri("https://os.example.com:9200")));
services.Configure<OpenSearchExporterOptions>(o =>
{
    o.ApiKey = configuration["OpenSearch:ApiKey"];
    o.BulkBatchSize = 500;
    o.MaxRetryAttempts = 3;
    o.ApplicationName = "OrderService";
});
```

Same `ApplicationName` preference hierarchy as Elasticsearch: the event field takes precedence over the options-level fallback.

---

## Datadog

Export audit events to Datadog for log analytics and dashboards.

### Installation

```bash
dotnet add package Excalibur.AuditLogging.Datadog
```

### Setup

```csharp
services.AddAuditLogging();

services.AddDatadogAuditExporter(options =>
{
    options.ApiKey = "your-datadog-api-key";
    options.Site = "datadoghq.com"; // or datadoghq.eu
});
```

---

## Splunk

Export audit events to Splunk via HEC (HTTP Event Collector).

### Installation

```bash
dotnet add package Excalibur.AuditLogging.Splunk
```

### Setup

```csharp
services.AddAuditLogging();

// With options callback
services.AddSplunkAuditExporter(options =>
{
    options.HecEndpoint = "https://splunk.example.com:8088";
    options.Token = "your-hec-token";
    options.Index = "audit";
});

// Or from configuration section
services.AddSplunkAuditExporter(configurationSection: "Splunk");
```

---

## Microsoft Sentinel

Export audit events to Azure Sentinel for SIEM analysis.

### Installation

```bash
dotnet add package Excalibur.AuditLogging.Sentinel
```

### Setup

```csharp
services.AddAuditLogging();

services.AddSentinelAuditExporter(options =>
{
    options.WorkspaceId = "your-workspace-id";
    options.SharedKey = "your-shared-key";
    options.LogType = "DispatchAudit";
});
```

---

## SQL Server

Persist audit events to SQL Server for relational querying and long-term retention.

### Installation

```bash
dotnet add package Excalibur.AuditLogging.SqlServer
```

### Setup

```csharp
// With options callback
services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = "Server=localhost;Database=Audit;Trusted_Connection=true;";
    options.TableName = "AuditEvents";
    options.SchemaName = "audit";
});

// Or with pre-built options
var auditOptions = new SqlServerAuditOptions
{
    ConnectionString = connectionString,
    TableName = "AuditEvents"
};
services.AddSqlServerAuditStore(auditOptions);
```

---

## Combining Providers

You can register multiple backends. Use SQL Server as the compliance-grade `IAuditStore` and add exporters for search and analytics:

```csharp
services.AddAuditLogging();

// Primary: compliance-grade, hash-chained
services.AddSqlServerAuditStore(options => { /* ... */ });

// Search & analytics exporters
services.AddElasticsearchAuditExporter(es => { /* ... */ });
services.AddOpenSearchAuditExporter(os => { /* ... */ });

// SIEM exporters
services.AddDatadogAuditExporter(dd => { /* ... */ });
services.AddSentinelAuditExporter(sentinel => { /* ... */ });
```

## See Also

- [Audit Logging](../security/audit-logging.md) — Core audit logging architecture
- [Observability Overview](./index.md) — Metrics, tracing, and health checks
- [Compliance](../compliance/index.md) — Regulatory compliance checklists
