---
sidebar_position: 4
---

# Azure Monitor

Comprehensive monitoring for Excalibur applications on Azure using Application Insights, Log Analytics, and Azure Monitor.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An Azure subscription with Application Insights resource
- Familiarity with [production observability](./production-observability.md) and [metrics reference](./metrics-reference.md)

## Overview

Azure Monitor provides:
- **Application Insights**: APM, distributed tracing, performance monitoring
- **Log Analytics**: Centralized logging with KQL queries
- **Metrics**: Custom and platform metrics
- **Alerts**: Smart alerts with action groups
- **Workbooks**: Interactive reports and dashboards

## Prerequisites

### Create Application Insights

**Azure CLI:**
```bash
# Create resource group
az group create \
  --name dispatch-rg \
  --location eastus

# Create Application Insights
az monitor app-insights component create \
  --app dispatch-insights \
  --location eastus \
  --resource-group dispatch-rg \
  --application-type web

# Get instrumentation key
az monitor app-insights component show \
  --app dispatch-insights \
  --resource-group dispatch-rg \
  --query instrumentationKey \
  --output tsv
```

**Terraform:**
```hcl
resource "azurerm_application_insights" "dispatch" {
  name                = "dispatch-insights"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"

  tags = {
    environment = "production"
    service     = "dispatch-api"
  }
}

output "instrumentation_key" {
  value     = azurerm_application_insights.dispatch.instrumentation_key
  sensitive = true
}

output "connection_string" {
  value     = azurerm_application_insights.dispatch.connection_string
  sensitive = true
}
```

## Application Insights Integration

### Install NuGet Package

```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

### Configure Application Insights

```csharp
// Program.cs
using Microsoft.ApplicationInsights.Extensibility;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// Configure telemetry
builder.Services.Configure<TelemetryConfiguration>(config =>
{
    config.TelemetryInitializers.Add(new DispatchTelemetryInitializer());
});

var app = builder.Build();
app.Run();
```

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft": "Warning"
      }
    }
  }
}
```

### Telemetry Initializer

Add custom properties to all telemetry:

```csharp
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

public class DispatchTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = "dispatch-api";
        telemetry.Context.Component.Version = "1.0.0";

        // Add custom properties
        if (telemetry is ISupportProperties supportProperties)
        {
            supportProperties.Properties["Environment"] =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            supportProperties.Properties["MachineName"] = Environment.MachineName;
        }
    }
}
```

## Custom Telemetry

### Track Command Processing

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public class TelemetryMiddleware : IDispatchMiddleware
{
    private readonly TelemetryClient _telemetryClient;

    public TelemetryMiddleware(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var messageType = message.GetType().Name;

        using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>("Message"))
        {
            operation.Telemetry.Type = "Dispatch";
            operation.Telemetry.Name = messageType;
            operation.Telemetry.Properties["message_type"] = messageType;
            operation.Telemetry.Properties["correlation_id"] = context.CorrelationId?.ToString();

            try
            {
                var result = await nextDelegate(message, context, cancellationToken);

                operation.Telemetry.Success = result.Succeeded;
                operation.Telemetry.ResultCode = result.Succeeded ? "Success" : "Failure";

                // Track custom metric
                _telemetryClient.TrackMetric(
                    "MessageProcessed",
                    1,
                    new Dictionary<string, string>
                    {
                        ["MessageType"] = messageType,
                        ["Status"] = result.Succeeded ? "Success" : "Failure"
                    });

                return result;
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = "Failure";

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["MessageType"] = messageType,
                    ["Operation"] = "ProcessMessage"
                });

                _telemetryClient.TrackMetric(
                    "MessageFailed",
                    1,
                    new Dictionary<string, string>
                    {
                        ["MessageType"] = messageType,
                        ["ExceptionType"] = ex.GetType().Name
                    });

                throw;
            }
        }
    }
}

// Register middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<TelemetryMiddleware>();
});
```

### Track Event Store Operations

```csharp
public class TelemetryEventStore : IEventStore
{
    private readonly IEventStore _inner;
    private readonly TelemetryClient _telemetryClient;

    public TelemetryEventStore(
        IEventStore inner,
        TelemetryClient telemetryClient)
    {
        _inner = inner;
        _telemetryClient = telemetryClient;
    }

    public async ValueTask<AppendResult> AppendAsync(
        string aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>(
            "EventStore.Append"))
        {
            operation.Telemetry.Type = "EventStore";
            operation.Telemetry.Target = "SQL Server";
            operation.Telemetry.Name = $"Append {aggregateType}";
            operation.Telemetry.Properties["aggregate_type"] = aggregateType;
            operation.Telemetry.Properties["aggregate_id"] = aggregateId;
            operation.Telemetry.Properties["event_count"] = events.Count().ToString();
            operation.Telemetry.Properties["expected_version"] = expectedVersion.ToString();

            try
            {
                var result = await _inner.AppendAsync(
                    aggregateId,
                    aggregateType,
                    events,
                    expectedVersion,
                    cancellationToken);

                operation.Telemetry.Success = true;

                // Track event types
                foreach (var evt in events)
                {
                    _telemetryClient.TrackEvent(
                        "EventAppended",
                        new Dictionary<string, string>
                        {
                            ["AggregateType"] = aggregateType,
                            ["EventType"] = evt.EventType
                        });
                }

                // Track metrics
                _telemetryClient.TrackMetric(
                    "EventsAppended",
                    events.Count(),
                    new Dictionary<string, string>
                    {
                        ["AggregateType"] = aggregateType
                    });

                return result;
            }
            catch (ConcurrencyException ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = "ConcurrencyConflict";

                _telemetryClient.TrackMetric(
                    "ConcurrencyConflicts",
                    1,
                    new Dictionary<string, string>
                    {
                        ["AggregateType"] = aggregateType
                    });

                _telemetryClient.TrackException(ex);
                throw;
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                _telemetryClient.TrackException(ex);
                throw;
            }
        }
    }

    public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken)
    {
        using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>(
            "EventStore.Load"))
        {
            operation.Telemetry.Type = "EventStore";
            operation.Telemetry.Target = "SQL Server";
            operation.Telemetry.Properties["aggregate_id"] = aggregateId;

            var events = await _inner.LoadAsync(aggregateId, aggregateType, cancellationToken);

            operation.Telemetry.Success = true;
            operation.Telemetry.Properties["event_count"] = events.Count().ToString();

            _telemetryClient.TrackMetric(
                "EventsLoaded",
                events.Count(),
                new Dictionary<string, string>
                {
                    ["AggregateId"] = aggregateId
                });

            return events;
        }
    }
}

// Register decorator
builder.Services.Decorate<IEventStore, TelemetryEventStore>();
```

### Track Custom Business Metrics

```csharp
public class OrderMetrics
{
    private readonly TelemetryClient _telemetryClient;

    public OrderMetrics(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void RecordOrderCreated(string customerId, decimal orderValue, int itemCount)
    {
        _telemetryClient.TrackEvent(
            "OrderCreated",
            properties: new Dictionary<string, string>
            {
                ["CustomerId"] = customerId,
                ["ItemCount"] = itemCount.ToString()
            },
            metrics: new Dictionary<string, double>
            {
                ["OrderValue"] = (double)orderValue
            });

        _telemetryClient.TrackMetric(
            "OrderValue",
            (double)orderValue,
            new Dictionary<string, string>
            {
                ["CustomerId"] = customerId
            });
    }

    public void RecordOrderCancelled(string orderId, string reason)
    {
        _telemetryClient.TrackEvent(
            "OrderCancelled",
            new Dictionary<string, string>
            {
                ["OrderId"] = orderId,
                ["Reason"] = reason
            });

        _telemetryClient.TrackMetric(
            "OrdersCancelled",
            1,
            new Dictionary<string, string>
            {
                ["Reason"] = reason
            });
    }
}
```

## Log Analytics Integration

### Send Logs to Log Analytics

Application Insights automatically forwards logs to Log Analytics workspace.

**Query logs with KQL:**
```kusto
// Failed commands in last hour
traces
| where timestamp > ago(1h)
| where severityLevel >= 3
| where message contains "Command"
| project timestamp, message, severityLevel, customDimensions
| order by timestamp desc

// Command processing duration
dependencies
| where type == "Dispatch"
| where timestamp > ago(1h)
| summarize
    avg(duration),
    percentile(duration, 50),
    percentile(duration, 95),
    percentile(duration, 99)
  by name
| order by avg_duration desc

// Event store concurrency conflicts
exceptions
| where timestamp > ago(24h)
| where outerType == "ConcurrencyException"
| summarize count() by bin(timestamp, 1h), tostring(customDimensions.aggregate_type)
| render timechart

// Top failed commands
customMetrics
| where name == "CommandFailed"
| where timestamp > ago(24h)
| summarize failureCount = sum(value) by tostring(customDimensions.CommandType)
| order by failureCount desc
| take 10
```

## Kusto Queries

### Performance Analysis

**Command latency percentiles:**
```kusto
dependencies
| where type == "Dispatch"
| where timestamp > ago(1h)
| summarize
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99),
    max = max(duration),
    count = count()
  by command_type = tostring(customDimensions.command_type)
| order by p99 desc
```

**Slowest requests:**
```kusto
requests
| where timestamp > ago(1h)
| where success == true
| top 20 by duration desc
| project timestamp, name, url, duration, resultCode
```

**Failed requests with exceptions:**
```kusto
requests
| where timestamp > ago(1h)
| where success == false
| join kind=inner (
    exceptions
    | where timestamp > ago(1h)
  ) on operation_Id
| project
    timestamp,
    requestName = name,
    url,
    exceptionType = type,
    exceptionMessage = outerMessage,
    stackTrace = details
| order by timestamp desc
```

### Availability Monitoring

**Health check results:**
```kusto
requests
| where name contains "/health"
| where timestamp > ago(24h)
| summarize
    totalChecks = count(),
    successfulChecks = countif(success == true),
    failedChecks = countif(success == false),
    avgDuration = avg(duration)
  by bin(timestamp, 5m)
| extend availability = (successfulChecks * 100.0) / totalChecks
| render timechart
```

### Error Analysis

**Exception trends:**
```kusto
exceptions
| where timestamp > ago(7d)
| summarize count() by
    bin(timestamp, 1h),
    exceptionType = type
| render timechart
```

**Top exceptions:**
```kusto
exceptions
| where timestamp > ago(24h)
| summarize
    count = count(),
    sample = any(outerMessage)
  by type
| order by count desc
| take 10
```

## Dashboards and Workbooks

### Create Dashboard via Azure CLI

```bash
az portal dashboard create \
  --resource-group dispatch-rg \
  --name dispatch-dashboard \
  --input-path dashboard.json
```

**dashboard.json:**
```json
{
  "properties": {
    "lenses": {
      "0": {
        "order": 0,
        "parts": {
          "0": {
            "position": {
              "x": 0,
              "y": 0,
              "colSpan": 6,
              "rowSpan": 4
            },
            "metadata": {
              "inputs": [
                {
                  "name": "ComponentId",
                  "value": "/subscriptions/{subscription-id}/resourceGroups/dispatch-rg/providers/microsoft.insights/components/dispatch-insights"
                }
              ],
              "type": "Extension/AppInsightsExtension/PartType/MetricsChartPart",
              "settings": {
                "content": {
                  "title": "Command Processing Rate"
                }
              }
            }
          }
        }
      }
    },
    "metadata": {
      "model": {
        "timeRange": {
          "type": "MsPortalFx.Composition.Configuration.ValueTypes.TimeRange",
          "value": {
            "relative": {
              "duration": 24,
              "timeUnit": 1
            }
          }
        }
      }
    }
  }
}
```

### Create Workbook

Navigate to Application Insights → Workbooks → New:

**Command Performance Workbook:**
```json
{
  "version": "Notebook/1.0",
  "items": [
    {
      "type": 1,
      "content": {
        "json": "## Command Processing Performance"
      }
    },
    {
      "type": 3,
      "content": {
        "version": "KqlItem/1.0",
        "query": "dependencies\n| where type == \"Dispatch\"\n| where timestamp > ago(1h)\n| summarize count() by bin(timestamp, 5m), command_type = tostring(customDimensions.command_type)\n| render timechart",
        "size": 0,
        "title": "Command Rate (5-minute bins)"
      }
    },
    {
      "type": 3,
      "content": {
        "version": "KqlItem/1.0",
        "query": "dependencies\n| where type == \"Dispatch\"\n| where timestamp > ago(1h)\n| summarize p95 = percentile(duration, 95) by command_type = tostring(customDimensions.command_type)\n| order by p95 desc",
        "size": 0,
        "title": "p95 Latency by Command Type"
      }
    }
  ]
}
```

## Alerts

### Metric Alert

**High command failure rate:**
```bash
az monitor metrics alert create \
  --name high-command-failure-rate \
  --resource-group dispatch-rg \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/dispatch-rg/providers/microsoft.insights/components/dispatch-insights" \
  --condition "avg customMetrics/CommandFailed > 10" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --description "Alert when command failure rate exceeds 10/min" \
  --severity 2
```

### Log Alert

**Concurrency conflicts:**
```bash
az monitor scheduled-query create \
  --name concurrency-conflicts \
  --resource-group dispatch-rg \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/dispatch-rg/providers/microsoft.insights/components/dispatch-insights" \
  --condition "count > 10" \
  --condition-query "exceptions | where type == 'ConcurrencyException' | summarize count()" \
  --description "Alert on high concurrency conflicts" \
  --evaluation-frequency 5m \
  --window-size 15m \
  --severity 2
```

### Smart Detection

Enable automatic anomaly detection:

```bash
az monitor app-insights component update \
  --app dispatch-insights \
  --resource-group dispatch-rg \
  --enable-smart-detection true
```

Smart Detection automatically alerts on:
- Abnormal rise in failure rate
- Performance degradation
- Memory leak detection
- Slow page load times

## Action Groups

Create action group for notifications:

```bash
az monitor action-group create \
  --name dispatch-alerts \
  --resource-group dispatch-rg \
  --short-name dispatch \
  --email-receiver name=oncall email=oncall@example.com \
  --webhook-receiver name=slack uri=https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK
```

**Link to alert:**
```bash
az monitor metrics alert update \
  --name high-command-failure-rate \
  --resource-group dispatch-rg \
  --add-action dispatch-alerts
```

## Live Metrics Stream

Enable real-time monitoring:

```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableQuickPulseMetricStream = true;
});
```

**View in Azure Portal:**
Application Insights → Live Metrics

Shows:
- Incoming requests/sec
- Outgoing requests/sec
- Overall health
- Server metrics (CPU, memory)
- Sample telemetry

## Availability Tests

Create web test to monitor health endpoint:

```bash
az monitor app-insights web-test create \
  --resource-group dispatch-rg \
  --name dispatch-health-check \
  --web-test-kind ping \
  --location "East US" \
  --defined-web-test-name "Dispatch Health Check" \
  --url "https://api.example.com/health" \
  --enabled true \
  --frequency 300 \
  --timeout 30 \
  --retry-enabled true
```

## Distributed Tracing

Application Insights automatically correlates:
- HTTP requests
- Database calls (SQL Server, Cosmos DB)
- Azure Service Bus messages
- Redis cache operations

**Custom correlation:**
```csharp
using Microsoft.ApplicationInsights;
using System.Diagnostics;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryClient _telemetryClient;

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;

        if (activity != null)
        {
            activity.AddTag("correlation_id", context.TraceIdentifier);
            activity.AddTag("user_id", context.User?.Identity?.Name ?? "anonymous");
        }

        await _next(context);
    }
}
```

## Application Map

Visualize dependencies automatically:

Navigate to: Application Insights → Application Map

Shows:
- dispatch-api → SQL Server (event store)
- dispatch-api → Azure Service Bus
- dispatch-api → Redis Cache
- dispatch-worker → Outbox (SQL Server)

**Filter by cloud role:**
```csharp
telemetry.Context.Cloud.RoleName = "dispatch-api";
telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
```

## Continuous Export

Export telemetry to storage for long-term retention:

```bash
az monitor app-insights component continues-export create \
  --resource-group dispatch-rg \
  --app dispatch-insights \
  --record-types Requests Trace Exception Metric \
  --dest-account /subscriptions/{subscription-id}/resourceGroups/dispatch-rg/providers/Microsoft.Storage/storageAccounts/dispatchlogs \
  --dest-container appinsights \
  --is-enabled true
```

## Sampling

Control telemetry volume and cost:

```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = true;
});

builder.Services.Configure<TelemetryConfiguration>(config =>
{
    // Fixed-rate sampling
    var builder = config.DefaultTelemetrySink.TelemetryProcessorChainBuilder;
    builder.UseSampling(10); // Keep 10% of telemetry
    builder.Build();
});
```

**Disable sampling for specific operations:**
```csharp
var operation = _telemetryClient.StartOperation<DependencyTelemetry>("CriticalOperation");
operation.Telemetry.SamplingPercentage = 100; // Never sample
```

## Performance Counters

Monitor system metrics:

```csharp
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.ConfigureTelemetryModule<PerformanceCollectorModule>((module, options) =>
{
    module.Counters.Add(new PerformanceCounterCollectionRequest(
        @"\Process(??APP_WIN32_PROC??)\% Processor Time",
        "CPU usage"));

    module.Counters.Add(new PerformanceCounterCollectionRequest(
        @"\Process(??APP_WIN32_PROC??)\Private Bytes",
        "Memory usage"));

    module.Counters.Add(new PerformanceCounterCollectionRequest(
        @"\.NET CLR Exceptions(??APP_CLR_PROC??)\# of Exceps Thrown / sec",
        "Exceptions/sec"));
});
```

## Azure Functions Integration

For serverless Dispatch applications:

```csharp
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;

[assembly: FunctionsStartup(typeof(Startup))]

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddApplicationInsightsTelemetry();

        builder.Services.Configure<TelemetryConfiguration>(config =>
        {
            config.TelemetryInitializers.Add(new DispatchTelemetryInitializer());
        });

        // Register Dispatch
        builder.Services.AddDispatch(dispatch =>
        {
            dispatch.UseMiddleware<TelemetryMiddleware>();
        });
    }
}
```

## Best Practices

### 1. Use Cloud Role Names

```csharp
telemetry.Context.Cloud.RoleName = "dispatch-api";
telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
```

### 2. Track Custom Properties

```csharp
_telemetryClient.TrackEvent("OrderCreated", new Dictionary<string, string>
{
    ["CustomerId"] = customerId,
    ["OrderValue"] = orderValue.ToString("C"),
    ["Environment"] = environment
});
```

### 3. Use Dependency Tracking

```csharp
using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>("Redis.Get"))
{
    operation.Telemetry.Type = "Redis";
    operation.Telemetry.Target = "cache.redis.com";

    var value = await _cache.GetAsync(key);

    operation.Telemetry.Success = value != null;
    return value;
}
```

### 4. Correlate Logs

```csharp
using Microsoft.Extensions.Logging;

_logger.LogInformation(
    "Processing order {OrderId} for customer {CustomerId}",
    orderId,
    customerId);
```

Logs automatically include `operation_Id` for correlation with traces.

### 5. Monitor Availability

Create availability tests for critical endpoints:
- `/health`
- `/api/orders` (smoke test)
- Key business workflows

## Cost Optimization

1. **Enable adaptive sampling** (keeps representative sample)
2. **Filter noisy telemetry**:
   ```csharp
   config.TelemetryProcessors.Add(new FilteringTelemetryProcessor());
   ```
3. **Set daily cap**:
   ```bash
   az monitor app-insights component update \
     --app dispatch-insights \
     --resource-group dispatch-rg \
     --cap 5  # 5 GB/day
   ```

## Testing Locally

```csharp
// appsettings.Development.json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx"
  }
}
```

**View telemetry locally:**
1. Run application: `dotnet run`
2. Generate traffic
3. Open Azure Portal → Application Insights
4. View Live Metrics, Logs, Metrics

## Next Steps

1. Create Application Insights resource
2. Install `Microsoft.ApplicationInsights.AspNetCore`
3. Configure connection string
4. Add telemetry initializer
5. Implement custom tracking for Dispatch operations
6. Create KQL queries for common scenarios
7. Build workbooks and dashboards
8. Set up alerts and action groups
9. Enable availability tests
10. Review Application Map and dependencies

## See Also

- [Observability Overview](./index.md) - Monitor Dispatch applications with OpenTelemetry, health checks, and integrations
- [Metrics Reference](./metrics-reference.md) - Complete catalog of 100+ available metrics
- [Health Checks](./health-checks.md) - Application health monitoring for load balancers and orchestrators
