---
sidebar_position: 3
title: Audit Logging
description: Comprehensive audit logging for compliance and security
---

# Audit Logging

This guide covers the audit logging system in Excalibur.Dispatch, including configuration, hash chain integrity, SIEM integration, and compliance features.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.AuditLogging
  dotnet add package Excalibur.Dispatch.AuditLogging.SqlServer  # or your provider
  ```
- Familiarity with [security concepts](./index.md) and compliance requirements

## Overview

The audit logging system provides a hash-chained, tamper-evident audit trail for compliance and security monitoring:

| Capability | Description | Compliance |
|------------|-------------|------------|
| Hash Chain Integrity | Each event hashed with previous event's hash | SOC 2 CC6.1 |
| Role-Based Access | Segregation of duties for audit log access | SOC 2 CC6.2 |
| SIEM Integration | Export to Splunk, Sentinel, Datadog | SOC 2 CC7.1 |
| 7-Year Retention | Long-term compliance storage | SOC 2 CC7.2 |

---

## Installation

```bash
# Core package
dotnet add package Excalibur.Dispatch.AuditLogging

# Storage providers
dotnet add package Excalibur.Dispatch.AuditLogging.SqlServer

# SIEM exporters
dotnet add package Excalibur.Dispatch.AuditLogging.Splunk
dotnet add package Excalibur.Dispatch.AuditLogging.Sentinel
dotnet add package Excalibur.Dispatch.AuditLogging.Datadog
```

---

## Quick Start

### Basic Configuration

```csharp
using Excalibur.Dispatch.AuditLogging.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Add audit logging with SQL Server storage
builder.Services.AddAuditLogging()
    .UseAuditStore<SqlServerAuditStore>();

builder.Services.Configure<SqlServerAuditOptions>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("AuditDb")!;
    options.EnableHashChain = true;  // Tamper detection
    options.RetentionPeriod = TimeSpan.FromDays(7 * 365);  // 7 years
});
```

### Logging Audit Events

```csharp
public class OrderService
{
    private readonly IAuditLogger _auditLogger;

    public OrderService(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, string userId)
    {
        var order = await ProcessOrder(request);

        await _auditLogger.LogAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = AuditEventType.DataModification,
            Action = "Order.Create",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = userId,
            ResourceId = order.Id.ToString(),
            ResourceType = "Order",
            ResourceClassification = DataClassification.Confidential,
            TenantId = request.TenantId,
            CorrelationId = Activity.Current?.Id,
            Metadata = new Dictionary<string, string>
            {
                ["order_total"] = order.Total.ToString("C"),
                ["item_count"] = order.Items.Count.ToString()
            }
        });

        return order;
    }
}
```

---

## Audit Event Structure

### Event Types

```csharp
public enum AuditEventType
{
    System = 0,              // General system events
    Authentication = 1,       // Login, logout, MFA
    Authorization = 2,        // Permission checks, access grants
    DataAccess = 3,          // Read, query operations
    DataModification = 4,     // Create, update, delete
    ConfigurationChange = 5,  // Configuration updates
    Security = 6,            // Key rotation, encryption
    Compliance = 7,          // Data export, erasure requests
    Administrative = 8,       // Admin actions
    Integration = 9          // API calls, external systems
}
```

### Outcomes

```csharp
public enum AuditOutcome
{
    Success = 0,   // Operation completed successfully
    Failure = 1,   // Operation failed
    Denied = 2,    // Authorization denied
    Error = 3,     // Error occurred
    Pending = 4    // In progress
}
```

### Event Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `EventId` | string | Yes | Unique identifier |
| `EventType` | AuditEventType | Yes | Category of event |
| `Action` | string | Yes | Action performed (e.g., "User.Login") |
| `Outcome` | AuditOutcome | Yes | Result of operation |
| `Timestamp` | DateTimeOffset | Yes | When event occurred |
| `ActorId` | string | Yes | Who performed action |
| `ResourceId` | string | No | Target resource identifier |
| `ResourceType` | string | No | Type of resource |
| `TenantId` | string | No | Tenant identifier |
| `CorrelationId` | string | No | Distributed tracing ID |
| `PreviousEventHash` | string | Auto | Hash of previous event |
| `EventHash` | string | Auto | Hash of this event |

---

## Hash Chain Integrity

### How It Works

Each audit event is cryptographically linked to the previous event:

```
Event 1: Hash = SHA256(EventData)
Event 2: Hash = SHA256(EventData + Event1.Hash)
Event 3: Hash = SHA256(EventData + Event2.Hash)
...
```

This creates a tamper-evident chain where modifying any event breaks the chain.

### Verify Integrity

```csharp
var auditLogger = serviceProvider.GetRequiredService<IAuditLogger>();

var result = await auditLogger.VerifyIntegrityAsync(
    startDate: DateTimeOffset.UtcNow.AddMonths(-1),
    endDate: DateTimeOffset.UtcNow,
    cancellationToken);

if (result.IsValid)
{
    Console.WriteLine($"Verified {result.TotalEventsValidated} events - integrity OK");
}
else
{
    Console.WriteLine($"INTEGRITY VIOLATION: {result.CorruptedEvents} corrupted events found");
    Console.WriteLine($"Details: {result.Message}");
}
```

### Integrity Result Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | bool | Whether hash chain is intact |
| `TotalEventsValidated` | long | Number of events checked |
| `CorruptedEvents` | long | Number of corrupted events found |
| `CorruptedEventIds` | IReadOnlyList\<string\> | IDs of corrupted events |
| `Message` | string? | Detailed validation message |

---

## Compliance Mapping

### SOC 2 Controls

| Control | Requirement | Dispatch Feature |
|---------|-------------|------------------|
| CC4.1 | Monitor system components | Automatic dispatch/handler logging |
| CC4.2 | Evaluate security events | `AuditEventType` categorization |
| CC6.1 | Logical access security | Hash chain integrity |
| CC6.2 | Access restriction | Role-based audit access |
| CC7.1 | Security event identification | Event type taxonomy |
| CC7.2 | Security event response | 7-year retention, SIEM export |

### GDPR Article 30

| Requirement | Implementation |
|-------------|----------------|
| Processing activities | `AuditEventType.DataAccess`, `DataModification` |
| Categories of data subjects | `ResourceType` field |
| Categories of recipients | `ActorId`, `ActorType` fields |
| Time limits for erasure | `RetentionPeriod` configuration |
| Security measures | Hash chain, RBAC, encryption |

### HIPAA 164.312(b)

| Requirement | Implementation |
|-------------|----------------|
| Audit controls | Automatic handler/dispatch logging |
| Record access | `DataAccess` event type |
| Record modification | `DataModification` event type |
| Activity review | Query API, SIEM integration |

---

## SIEM Integration

### Splunk (HTTP Event Collector)

```csharp
using Excalibur.Dispatch.AuditLogging.Splunk;

builder.Services.AddHttpClient<SplunkAuditExporter>();
builder.Services.AddSingleton<IAuditLogExporter, SplunkAuditExporter>();

builder.Services.Configure<SplunkExporterOptions>(options =>
{
    options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
    options.HecToken = builder.Configuration["Splunk:HecToken"]!;
    options.Index = "audit";
    options.SourceType = "audit:dispatch";
    options.Source = "my-application";
    options.MaxBatchSize = 100;
    options.EnableCompression = true;
});
```

### Azure Sentinel (Log Analytics)

```csharp
using Excalibur.Dispatch.AuditLogging.Sentinel;

builder.Services.AddHttpClient<SentinelAuditExporter>();
builder.Services.AddSingleton<IAuditLogExporter, SentinelAuditExporter>();

builder.Services.Configure<SentinelExporterOptions>(options =>
{
    options.WorkspaceId = builder.Configuration["Sentinel:WorkspaceId"]!;
    options.SharedKey = builder.Configuration["Sentinel:SharedKey"]!;
    options.LogType = "DispatchAudit";
    options.TimeGeneratedField = "timestamp";
    options.MaxBatchSize = 100;
});
```

### Datadog (Logs API)

```csharp
using Excalibur.Dispatch.AuditLogging.Datadog;

builder.Services.AddHttpClient<DatadogAuditExporter>();
builder.Services.AddSingleton<IAuditLogExporter, DatadogAuditExporter>();

builder.Services.Configure<DatadogExporterOptions>(options =>
{
    options.ApiKey = builder.Configuration["Datadog:ApiKey"]!;
    options.Site = "datadoghq.com";
    options.Service = "my-application";
    options.Source = "audit";
    options.Tags = "env:production,team:platform";
    options.UseCompression = true;
});
```

### Export Background Service

```csharp
public class AuditExportService : BackgroundService
{
    private readonly IAuditStore _auditStore;
    private readonly IAuditLogExporter _exporter;
    private readonly ILogger<AuditExportService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var query = new AuditQuery
            {
                StartDate = DateTimeOffset.UtcNow.AddMinutes(-5),
                EndDate = DateTimeOffset.UtcNow,
                MaxResults = 1000
            };

            var events = await _auditStore.QueryAsync(query, stoppingToken);

            if (events.Count > 0)
            {
                var result = await _exporter.ExportBatchAsync(events, stoppingToken);
                _logger.LogInformation(
                    "Exported {Success}/{Total} events",
                    result.SuccessCount, result.TotalCount);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

---

## Role-Based Access Control

### Access Levels

| Role | Access Level | Permissions |
|------|--------------|-------------|
| None | 0 | No access |
| Developer | 1 | No access (segregation of duties) |
| SecurityAnalyst | 2 | Read security events only |
| ComplianceOfficer | 3 | Read all events |
| Administrator | 4 | Full access including export |

### Configuration

```csharp
builder.Services.AddAuditLogging()
    .AddRbacAuditStore();

builder.Services.AddAuditRoleProvider<ClaimsBasedRoleProvider>();

public class ClaimsBasedRoleProvider : IAuditRoleProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(AuditLogRole.None);

        if (user.IsInRole("audit-admin"))
            return Task.FromResult(AuditLogRole.Administrator);

        if (user.IsInRole("compliance-officer"))
            return Task.FromResult(AuditLogRole.ComplianceOfficer);

        if (user.IsInRole("security-analyst"))
            return Task.FromResult(AuditLogRole.SecurityAnalyst);

        return Task.FromResult(AuditLogRole.Developer);
    }
}
```

---

## Querying Audit Events

```csharp
var auditStore = serviceProvider.GetRequiredService<IAuditStore>();

var query = new AuditQuery
{
    // Time range
    StartDate = DateTimeOffset.UtcNow.AddDays(-30),
    EndDate = DateTimeOffset.UtcNow,

    // Event filters
    EventTypes = new[] { AuditEventType.Authentication, AuditEventType.Authorization },
    Outcomes = new[] { AuditOutcome.Denied, AuditOutcome.Failure },

    // Actor/Resource filters
    ActorId = "user-123",
    ResourceType = "Order",

    // Multi-tenant
    TenantId = "tenant-abc",

    // Pagination
    MaxResults = 50,
    OrderByDescending = true
};

var events = await auditStore.QueryAsync(query, cancellationToken);
var count = await auditStore.CountAsync(query, cancellationToken);
```

---

## Best Practices

### What to Audit

**Always audit:**
- Authentication events (login, logout, MFA)
- Authorization failures (access denied)
- Data access to sensitive resources
- Data modifications (create, update, delete)
- Configuration changes
- Key management operations

**Include contextual information:**
- Correlation IDs for distributed tracing
- Session IDs for user journey tracking
- IP addresses for security analysis
- Data classification levels

### Performance

1. **Use batching** for high-volume scenarios
2. **Enable compression** for SIEM export
3. **Configure retention** appropriately (7 years for SOC 2)
4. **Use partitioning** for large volumes (SQL Server Enterprise)

### Security

1. **Separate audit database** from application data
2. **Encrypt connections** with TLS
3. **Restrict access** using RBAC
4. **Enable hash chain** for tamper detection
5. **Secure credentials** in Key Vault

---

## Related Documentation

- [Security Guide](../advanced/security.md) - Comprehensive security hardening
- [Encryption Architecture](encryption-architecture.md) - Data protection and key lifecycle
- [Compliance Checklists](../compliance/index.md) - Regulatory requirements

## See Also

- [Compliance Audit Logging](../compliance/audit-logging.md) — Compliance-focused audit logging requirements and checklists
- [Authorization & Audit (A3)](./authorization.md) — Activity-based authorization, grants, and the A3 audit subsystem
- [Production Observability](../observability/production-observability.md) — Monitoring, alerting, and observability patterns for production deployments
