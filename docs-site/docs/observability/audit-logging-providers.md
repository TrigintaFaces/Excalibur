---
sidebar_position: 8
title: Audit Logging Providers
description: Per-provider setup for Datadog, Splunk, Sentinel, and SQL Server audit log exporters.
---

# Audit Logging Providers

Dispatch audit logging uses `IAuditStore` as its core abstraction. Provider-specific exporters ship audit events to external platforms for analysis, alerting, and compliance reporting.

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

You can register multiple exporters. The core `IAuditStore` dispatches to all registered exporters:

```csharp
services.AddAuditLogging();
services.AddSqlServerAuditStore(options => { /* primary store */ });
services.AddDatadogAuditExporter(options => { /* analytics */ });
services.AddSentinelAuditExporter(options => { /* SIEM */ });
```

## See Also

- [Audit Logging](../security/audit-logging.md) — Core audit logging architecture
- [Observability Overview](./index.md) — Metrics, tracing, and health checks
- [Compliance](../compliance/index.md) — Regulatory compliance checklists
