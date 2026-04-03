---
sidebar_position: 8
title: Audit Logging Providers
description: Per-provider setup for Elasticsearch, OpenSearch, Datadog, Splunk, Sentinel, and SQL Server audit backends.
---

# Audit Logging Providers

Dispatch audit logging uses `IAuditStore` as its core abstraction for compliance-grade storage and `IAuditLogExporter` / audit sinks for search and analytics projections. Provider-specific backends ship audit events to external platforms for analysis, alerting, and compliance reporting.

:::info Compliance Boundary (ADR-290)
Only SQL Server (and Postgres) backends implement `IAuditStore` with tamper-evident hash chains. Elasticsearch and OpenSearch serve as **audit sinks** -- write-only, search-optimized projections. They are not compliance-grade stores. See [Compliance Audit Logging](../compliance/audit-logging.md#provider-compliance-boundary) for details.
:::

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Security
  ```
- Familiarity with [audit logging](../security/audit-logging.md) and [compliance](../compliance/audit-logging.md)

## Core Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Default audit logging (in-memory store)
services.AddAuditLogging();

// With the SQL Server store (package: Excalibur.Dispatch.AuditLogging.SqlServer)
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

## Elasticsearch (Audit Sink)

Index audit events into Elasticsearch for full-text search, aggregation dashboards, and real-time alerting. This is a **sink** (write-only) -- not an `IAuditStore` implementation. See [ADR-290](../compliance/audit-logging.md#provider-compliance-boundary) for rationale.

### Installation

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.Elasticsearch
```

**Dependencies:** `Excalibur.Dispatch.Compliance.Abstractions`, `Microsoft.Extensions.Http`

### Audit Sink (Real-Time)

Writes individual audit events via the Bulk API with retry and round-robin cluster support:

```csharp
// With options callback
services.AddElasticsearchAuditSink(options =>
{
    // Single node
    options.ElasticsearchUrl = "https://es.example.com:9200";

    // Or cluster (round-robin)
    options.NodeUrls = ["https://es1:9200", "https://es2:9200", "https://es3:9200"];

    options.IndexPrefix = "dispatch-audit";   // indexes: dispatch-audit-2026.03.31
    options.ApiKey = "your-api-key";
    options.ApplicationName = "OrderService"; // fallback if AuditEvent.ApplicationName is null
    options.MaxRetryAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(1); // exponential backoff
});

// Or from IConfiguration (appsettings.json binding)
services.AddElasticsearchAuditSink(configuration.GetSection("AuditSink:Elasticsearch"));
```

:::tip ApplicationName Preference
The indexed `application_name` field uses `AuditEvent.ApplicationName` when set, falling back to the options-level `ApplicationName`. Set it once on the event via `ApplicationContext.ApplicationName` (automatic via DI) and all sinks pick it up.
:::

### Audit Exporter (Batch)

Bulk-exports audit events from your primary `IAuditStore` (e.g., SQL Server) into Elasticsearch for search indexing:

```csharp
// With options callback
services.AddElasticsearchAuditExporter(options =>
{
    options.ElasticsearchUrl = "https://es.example.com:9200";
    options.IndexPrefix = "dispatch-audit";
    options.BulkBatchSize = 500;
});

// Or from IConfiguration
services.AddElasticsearchAuditExporter(configuration.GetSection("AuditExporter:Elasticsearch"));
```

### Recommended Architecture

```
SQL Server = IAuditStore (compliance, hash-chained, tamper-evident)
Elasticsearch = Audit Sink (search, dashboards, alerting)
```

---

## OpenSearch (Audit Sink)

Full parity with the Elasticsearch audit sink, built on raw `HttpClient` (no `OpenSearch.Client` dependency). Same compliance boundary applies -- OpenSearch is a **sink**, not an `IAuditStore`.

### Installation

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.OpenSearch
```

**Dependencies:** `Excalibur.Dispatch.Compliance.Abstractions`, `Microsoft.Extensions.Http`

### Audit Sink (Real-Time)

```csharp
// With options callback
services.AddOpenSearchAuditSink(options =>
{
    // Single node
    options.OpenSearchUrl = "https://os.example.com:9200";

    // Or cluster (round-robin)
    options.NodeUrls = ["https://os1:9200", "https://os2:9200", "https://os3:9200"];

    options.IndexPrefix = "dispatch-audit";
    options.ApiKey = "your-api-key";
    options.ApplicationName = "OrderService"; // fallback if AuditEvent.ApplicationName is null
    options.MaxRetryAttempts = 3;
});

// Or from IConfiguration (appsettings.json binding)
services.AddOpenSearchAuditSink(configuration.GetSection("AuditSink:OpenSearch"));
```

Same `ApplicationName` preference hierarchy as Elasticsearch: event field takes precedence over options-level fallback.

### Audit Exporter (Batch)

```csharp
// With options callback
services.AddOpenSearchAuditExporter(options =>
{
    options.OpenSearchUrl = "https://os.example.com:9200";
    options.IndexPrefix = "dispatch-audit";
    options.BulkBatchSize = 500;
});

// Or from IConfiguration
services.AddOpenSearchAuditExporter(configuration.GetSection("AuditExporter:OpenSearch"));
```

---

## Datadog

Export audit events to Datadog for log analytics and dashboards.

### Installation

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.Datadog
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
dotnet add package Excalibur.Dispatch.AuditLogging.Splunk
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
dotnet add package Excalibur.Dispatch.AuditLogging.Sentinel
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
dotnet add package Excalibur.Dispatch.AuditLogging.SqlServer
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

You can register multiple backends. Use SQL Server as the compliance-grade `IAuditStore` and add sinks/exporters for search and analytics:

```csharp
services.AddAuditLogging();

// Primary: compliance-grade, hash-chained
services.AddSqlServerAuditStore(options => { /* ... */ });

// Search & analytics sinks
services.AddElasticsearchAuditSink(options => { /* ... */ });
services.AddOpenSearchAuditSink(options => { /* ... */ });

// SIEM exporters
services.AddDatadogAuditExporter(options => { /* ... */ });
services.AddSentinelAuditExporter(options => { /* ... */ });
```

## See Also

- [Audit Logging](../security/audit-logging.md) — Core audit logging architecture
- [Observability Overview](./index.md) — Metrics, tracing, and health checks
- [Compliance](../compliance/index.md) — Regulatory compliance checklists
