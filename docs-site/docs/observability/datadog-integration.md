---
sidebar_position: 3
---

# Datadog Integration

Comprehensive monitoring and APM (Application Performance Monitoring) for Excalibur applications using Datadog.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Datadog account with APM enabled
- Familiarity with [production observability](./production-observability.md) and [metrics reference](./metrics-reference.md)

## Overview

Datadog provides:
- **APM (Application Performance Monitoring)**: Distributed tracing, service maps
- **Infrastructure Monitoring**: CPU, memory, disk, network
- **Log Management**: Centralized logging with correlation
- **Custom Metrics**: Business and application metrics
- **Alerting**: Intelligent alerts based on anomaly detection

## Prerequisites

### Datadog Account

Sign up at [datadoghq.com](https://www.datadoghq.com/)

### Install Datadog Agent

**Docker:**
```yaml
# docker-compose.yml
version: '3.8'

services:
  datadog-agent:
    image: gcr.io/datadoghq/agent:latest
    environment:
      - DD_API_KEY=${DD_API_KEY}
      - DD_SITE=datadoghq.com
      - DD_APM_ENABLED=true
      - DD_LOGS_ENABLED=true
      - DD_LOGS_CONFIG_CONTAINER_COLLECT_ALL=true
      - DD_DOGSTATSD_NON_LOCAL_TRAFFIC=true
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - /proc/:/host/proc/:ro
      - /sys/fs/cgroup/:/host/sys/fs/cgroup:ro
    ports:
      - "8126:8126/tcp"  # APM
      - "8125:8125/udp"  # DogStatsD

  dispatch-api:
    build: .
    environment:
      - DD_AGENT_HOST=datadog-agent
      - DD_TRACE_AGENT_PORT=8126
      - DD_SERVICE=dispatch-api
      - DD_ENV=production
      - DD_VERSION=1.0.0
    depends_on:
      - datadog-agent
```

**Kubernetes:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: datadog-secret
  namespace: default
type: Opaque
data:
  api-key: <base64-encoded-api-key>
---
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: datadog-agent
spec:
  selector:
    matchLabels:
      app: datadog-agent
  template:
    metadata:
      labels:
        app: datadog-agent
    spec:
      serviceAccountName: datadog-agent
      containers:
      - name: datadog-agent
        image: gcr.io/datadoghq/agent:latest
        env:
        - name: DD_API_KEY
          valueFrom:
            secretKeyRef:
              name: datadog-secret
              key: api-key
        - name: DD_SITE
          value: "datadoghq.com"
        - name: DD_APM_ENABLED
          value: "true"
        - name: DD_LOGS_ENABLED
          value: "true"
        - name: DD_KUBERNETES_KUBELET_HOST
          valueFrom:
            fieldRef:
              fieldPath: status.hostIP
        volumeMounts:
        - name: dockersocket
          mountPath: /var/run/docker.sock
        - name: procdir
          mountPath: /host/proc
          readOnly: true
        - name: cgroups
          mountPath: /host/sys/fs/cgroup
          readOnly: true
      volumes:
      - name: dockersocket
        hostPath:
          path: /var/run/docker.sock
      - name: procdir
        hostPath:
          path: /proc
      - name: cgroups
        hostPath:
          path: /sys/fs/cgroup
```

## APM (Distributed Tracing)

### Install Datadog .NET Tracer

```bash
# Install via NuGet
dotnet add package Datadog.Trace
```

### Configure Tracer

```csharp
// Program.cs
using Datadog.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure Datadog
builder.Services.AddSingleton(serviceProvider =>
{
    var settings = TracerSettings.FromDefaultSources();
    settings.ServiceName = "dispatch-api";
    settings.Environment = builder.Environment.EnvironmentName;
    settings.ServiceVersion = "1.0.0";
    settings.AnalyticsEnabled = true;

    return new Tracer(settings);
});

var app = builder.Build();

// Middleware is automatically registered by Datadog.Trace
app.Run();
```

### Automatic Instrumentation

Datadog automatically instruments:
- **ASP.NET Core**: HTTP requests, middleware
- **Entity Framework Core**: Database queries (if used)
- **HttpClient**: Outgoing HTTP calls
- **SQL Server**: ADO.NET and Dapper queries

**Enable automatic instrumentation:**
```bash
# Set environment variables
export DD_SERVICE=dispatch-api
export DD_ENV=production
export DD_VERSION=1.0.0
export DD_TRACE_ENABLED=true

# Run application with profiler
dotnet run
```

### Custom Spans

Add custom instrumentation for Dispatch operations:

```csharp
using Datadog.Trace;

public class TracingMiddleware : IDispatchMiddleware
{
    private readonly ITracer _tracer;

    public TracingMiddleware(ITracer tracer)
    {
        _tracer = tracer;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var messageType = message.GetType().Name;

        using (var scope = _tracer.StartActive("dispatch.message"))
        {
            var span = scope.Span;
            span.ResourceName = messageType;
            span.SetTag("message.type", messageType);
            span.SetTag("service.name", "dispatch-api");
            span.SetTag("correlation.id", context.CorrelationId?.ToString());

            try
            {
                var result = await nextDelegate(message, context, cancellationToken);

                span.SetTag("message.status", result.Succeeded ? "success" : "failure");
                return result;
            }
            catch (Exception ex)
            {
                span.SetException(ex);
                span.SetTag("message.status", "failure");
                throw;
            }
        }
    }
}

// Register middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<TracingMiddleware>();
});
```

### Event Store Tracing

```csharp
public class TracingEventStore : IEventStore
{
    private readonly IEventStore _inner;
    private readonly ITracer _tracer;

    public TracingEventStore(IEventStore inner, ITracer tracer)
    {
        _inner = inner;
        _tracer = tracer;
    }

    public async ValueTask<AppendResult> AppendAsync(
        string aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using (var scope = _tracer.StartActive("eventstore.append"))
        {
            var span = scope.Span;
            span.ResourceName = $"Append {aggregateType}";
            span.SetTag("aggregate.type", aggregateType);
            span.SetTag("aggregate.id", aggregateId);
            span.SetTag("event.count", events.Count());
            span.SetTag("expected.version", expectedVersion);

            try
            {
                var result = await _inner.AppendAsync(
                    aggregateId,
                    aggregateType,
                    events,
                    expectedVersion,
                    cancellationToken);

                span.SetTag("operation.status", "success");
                return result;
            }
            catch (ConcurrencyException ex)
            {
                span.SetTag("operation.status", "concurrency_conflict");
                span.SetException(ex);
                throw;
            }
            catch (Exception ex)
            {
                span.SetTag("operation.status", "failure");
                span.SetException(ex);
                throw;
            }
        }
    }

    public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken)
    {
        using (var scope = _tracer.StartActive("eventstore.load"))
        {
            var span = scope.Span;
            span.ResourceName = $"Load Events {aggregateId}";
            span.SetTag("aggregate.id", aggregateId);
            span.SetTag("aggregate.type", aggregateType);

            var events = await _inner.LoadAsync(aggregateId, aggregateType, cancellationToken);

            span.SetTag("event.count", events.Count);
            return events;
        }
    }
}

// Register decorator
builder.Services.Decorate<IEventStore, TracingEventStore>();
```

## Custom Metrics

### Install DogStatsD

```bash
dotnet add package DogStatsD-CSharp-Client
```

### Configure DogStatsD

```csharp
using StatsdClient;

var builder = WebApplication.CreateBuilder(args);

// Configure DogStatsD
var dogstatsdConfig = new StatsdConfig
{
    StatsdServerName = builder.Configuration["Datadog:AgentHost"] ?? "localhost",
    StatsdPort = 8125,
    Prefix = "dispatch"
};

DogStatsd.Configure(dogstatsdConfig);

var app = builder.Build();
app.Run();
```

### Metrics Middleware

```csharp
public class MetricsMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var messageType = message.GetType().Name;
        var tags = new[] { $"message_type:{messageType}" };

        DogStatsd.Increment("messages.processed", tags: tags);
        DogStatsd.Gauge("messages.active", 1, tags: tags);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await nextDelegate(message, context, cancellationToken);

            stopwatch.Stop();

            DogStatsd.Histogram(
                "message.duration",
                stopwatch.ElapsedMilliseconds,
                tags: tags.Append($"status:{(result.Succeeded ? "success" : "failure")}").ToArray());

            DogStatsd.Gauge("messages.active", -1, tags: tags);

            return result;
        }
        catch (Exception)
        {
            stopwatch.Stop();

            DogStatsd.Increment(
                "messages.failed",
                tags: tags.Append("status:failure").ToArray());

            DogStatsd.Histogram(
                "message.duration",
                stopwatch.ElapsedMilliseconds,
                tags: tags.Append("status:failure").ToArray());

            DogStatsd.Gauge("messages.active", -1, tags: tags);

            throw;
        }
    }
}
```

### Business Metrics

```csharp
public class OrderMetrics
{
    public static void RecordOrderCreated(string customerId, decimal orderValue)
    {
        DogStatsd.Increment("orders.created");
        DogStatsd.Histogram("order.value", (double)orderValue);
        DogStatsd.Set("customers.active", customerId);
    }

    public static void RecordOrderCancelled(string reason)
    {
        DogStatsd.Increment("orders.cancelled", tags: new[] { $"reason:{reason}" });
    }

    public static void RecordInventoryLevel(string productId, int quantity)
    {
        DogStatsd.Gauge(
            "inventory.level",
            quantity,
            tags: new[] { $"product_id:{productId}" });
    }
}
```

## Log Management

### Install Serilog Datadog Sink

```bash
dotnet add package Serilog.Sinks.Datadog.Logs
```

### Configure Logging

```csharp
using Serilog;
using Serilog.Sinks.Datadog.Logs;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Datadog sink
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service", "dispatch-api")
        .Enrich.WithProperty("env", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty("version", "1.0.0")
        .WriteTo.Console()
        .WriteTo.DatadogLogs(
            apiKey: context.Configuration["Datadog:ApiKey"],
            source: "csharp",
            service: "dispatch-api",
            host: Environment.MachineName,
            tags: new[] { "env:production", "version:1.0.0" }
        );
});

var app = builder.Build();
app.Run();
```

### Structured Logging

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating order {OrderId} for customer {CustomerId} with {ItemCount} items",
            action.OrderId,
            action.CustomerId,
            action.Items.Count);

        try
        {
            var order = Order.Create(
                command.OrderId,
                command.CustomerId,
                command.Items);

            await _repository.SaveAsync(order, cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} created successfully with total value {OrderValue:C}",
                command.OrderId,
                order.TotalValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create order {OrderId} for customer {CustomerId}",
                command.OrderId,
                command.CustomerId);

            throw;
        }
    }
}
```

### Log Correlation with Traces

```csharp
using Datadog.Trace;
using Serilog.Context;

public class LogCorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public LogCorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var scope = Tracer.Instance.ActiveScope;

        if (scope != null)
        {
            using (LogContext.PushProperty("dd.trace_id", scope.Span.TraceId))
            using (LogContext.PushProperty("dd.span_id", scope.Span.SpanId))
            {
                await _next(context);
            }
        }
        else
        {
            await _next(context);
        }
    }
}

// Register middleware
app.UseMiddleware<LogCorrelationMiddleware>();
```

**Log output with correlation:**
```json
{
  "timestamp": "2025-01-01T12:00:00Z",
  "level": "Information",
  "message": "Order 12345 created successfully",
  "dd.trace_id": "1234567890123456789",
  "dd.span_id": "9876543210987654321",
  "service": "dispatch-api",
  "env": "production"
}
```

## Dashboards

### Create Dashboard via API

```csharp
using System.Net.Http.Json;

public class DatadogDashboardCreator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _appKey;

    public async Task CreateDispatchDashboardAsync()
    {
        var dashboard = new
        {
            title = "Excalibur - Overview",
            description = "Command processing, event store, and outbox metrics",
            widgets = new[]
            {
                new
                {
                    definition = new
                    {
                        type = "timeseries",
                        requests = new[]
                        {
                            new
                            {
                                q = "sum:dispatch.commands.processed{*} by {command_type}.as_rate()",
                                display_type = "line"
                            }
                        },
                        title = "Command Processing Rate"
                    }
                },
                new
                {
                    definition = new
                    {
                        type = "query_value",
                        requests = new[]
                        {
                            new
                            {
                                q = "avg:dispatch.commands.active{*}",
                                aggregator = "avg"
                            }
                        },
                        title = "Active Commands"
                    }
                }
            },
            layout_type = "ordered"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.datadoghq.com/api/v1/dashboard?api_key={_apiKey}&application_key={_appKey}",
            dashboard);

        response.EnsureSuccessStatusCode();
    }
}
```

### Dashboard Template

Save as `datadog-dashboard.json`:

```json
{
  "title": "Excalibur - Overview",
  "description": "Comprehensive monitoring for Dispatch applications",
  "widgets": [
    {
      "definition": {
        "type": "timeseries",
        "requests": [
          {
            "q": "sum:dispatch.commands.processed{*} by {command_type}.as_rate()",
            "display_type": "line",
            "style": {
              "palette": "dog_classic",
              "line_type": "solid",
              "line_width": "normal"
            }
          }
        ],
        "title": "Command Processing Rate (commands/sec)"
      }
    },
    {
      "definition": {
        "type": "query_value",
        "requests": [
          {
            "q": "sum:dispatch.commands.active{*}",
            "aggregator": "avg"
          }
        ],
        "title": "Active Commands",
        "precision": 0
      }
    },
    {
      "definition": {
        "type": "heatmap",
        "requests": [
          {
            "q": "avg:dispatch.command.duration{*} by {command_type}"
          }
        ],
        "title": "Command Duration Heatmap"
      }
    },
    {
      "definition": {
        "type": "toplist",
        "requests": [
          {
            "q": "top(sum:dispatch.commands.failed{*} by {command_type}.as_count(), 10, 'sum', 'desc')"
          }
        ],
        "title": "Top Failed Commands"
      }
    }
  ],
  "layout_type": "ordered"
}
```

## Monitors (Alerts)

### Create Monitor via API

```csharp
public class DatadogMonitorCreator
{
    public async Task CreateHighFailureRateMonitorAsync()
    {
        var monitor = new
        {
            type = "metric alert",
            query = "avg(last_5m):sum:dispatch.commands.failed{*}.as_rate() > 0.1",
            name = "High Command Failure Rate",
            message = @"
                Command failure rate is above 10%.

                Check the APM traces for failed commands:
                https://app.datadoghq.com/apm/traces

                @slack-alerts @pagerduty
            ",
            tags = new[] { "service:dispatch-api", "env:production" },
            options = new
            {
                thresholds = new
                {
                    critical = 0.1,
                    warning = 0.05
                },
                notify_no_data = false,
                notify_audit = false,
                require_full_window = false,
                new_host_delay = 300,
                include_tags = true,
                escalation_message = "Failure rate still high after 15 minutes"
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.datadoghq.com/api/v1/monitor?api_key={_apiKey}&application_key={_appKey}",
            monitor);

        response.EnsureSuccessStatusCode();
    }
}
```

### Common Monitors

**Slow Command Processing:**
```
avg(last_10m):avg:dispatch.command.duration{command_type:*} by {command_type} > 5000
```

**High Concurrency Conflicts:**
```
sum(last_5m):sum:eventstore.concurrency.conflicts{*}.as_rate() > 0.1
```

**Outbox Backlog:**
```
avg(last_15m):avg:outbox.pending.messages{*} > 1000
```

**High Memory Usage:**
```
avg(last_5m):avg:system.mem.pct_usable{service:dispatch-api} < 0.1
```

## Service Map

Visualize service dependencies automatically:

1. Navigate to **APM** → **Service Map** in Datadog UI
2. See automatic visualization of:
   - dispatch-api → SQL Server (event store)
   - dispatch-api → Redis (caching)
   - dispatch-api → Azure Service Bus (messaging)
   - dispatch-worker → SQL Server (outbox)

## Profiling

Enable continuous profiling:

```csharp
using Datadog.Trace.Profiling;

var builder = WebApplication.CreateBuilder(args);

// Enable profiler
if (builder.Environment.IsProduction())
{
    Profiler.Instance.Start();
}

var app = builder.Build();
app.Run();
```

**Environment variables:**
```bash
DD_PROFILING_ENABLED=true
DD_PROFILING_CPU_ENABLED=true
DD_PROFILING_HEAP_ENABLED=true
```

## Synthetic Monitoring

Create synthetic tests for health checks:

```json
{
  "type": "api",
  "name": "Dispatch API Health Check",
  "message": "Health check failed @slack-alerts",
  "config": {
    "request": {
      "method": "GET",
      "url": "https://api.example.com/health"
    },
    "assertions": [
      {
        "type": "statusCode",
        "operator": "is",
        "target": 200
      },
      {
        "type": "responseTime",
        "operator": "lessThan",
        "target": 1000
      },
      {
        "type": "body",
        "operator": "contains",
        "target": "Healthy"
      }
    ]
  },
  "locations": ["aws:us-east-1", "aws:eu-west-1"],
  "options": {
    "tick_every": 300,
    "min_failure_duration": 0,
    "min_location_failed": 1
  }
}
```

## Error Tracking

Datadog automatically tracks errors from:
- Exceptions in APM traces
- Error logs from Serilog
- Failed HTTP requests

**Search errors in UI:**
```
service:dispatch-api status:error
```

**Group by error type:**
```
service:dispatch-api status:error | top error.type
```

## Integration with Azure

For Azure-hosted applications:

```json
{
  "integrations": {
    "azure": {
      "tenant_name": "your-tenant",
      "client_id": "your-client-id",
      "client_secret": "your-client-secret",
      "host_filters": "env:production",
      "app_service_plan_filters": "tag:service:dispatch"
    }
  }
}
```

Datadog will collect:
- Azure App Service metrics
- Azure SQL Database metrics
- Azure Service Bus metrics
- Azure Application Insights data

## Integration with AWS

For AWS-hosted applications:

```json
{
  "integrations": {
    "aws": {
      "account_id": "123456789012",
      "role_name": "DatadogAWSIntegrationRole",
      "host_tags": ["env:production", "service:dispatch"],
      "filter_tags": ["service:dispatch"]
    }
  }
}
```

Datadog will collect:
- EC2/ECS/Lambda metrics
- RDS metrics
- SQS/SNS metrics
- CloudWatch logs

## Best Practices

### 1. Use Unified Service Tagging

Always set these three tags:
```bash
DD_SERVICE=dispatch-api
DD_ENV=production
DD_VERSION=1.0.0
```

### 2. Tag Metrics Consistently

```csharp
DogStatsd.Increment(
    "orders.created",
    tags: new[]
    {
        "service:dispatch-api",
        "env:production",
        "customer_type:premium"
    });
```

### 3. Correlate Logs and Traces

Always include trace IDs in logs:
```csharp
using (LogContext.PushProperty("dd.trace_id", Tracer.Instance.ActiveScope?.Span.TraceId))
{
    _logger.LogInformation("Processing order");
}
```

### 4. Set Resource Names

```csharp
var span = scope.Span;
span.ResourceName = "POST /api/orders";  // Better than default
span.OperationName = "web.request";
```

### 5. Use Anomaly Detection

Create monitors with anomaly detection:
```
avg(last_4h):anomalies(avg:dispatch.commands.processed{*}.as_rate(), 'basic', 2, direction='both', alert_window='last_15m', interval=60, count_default_zero='true') >= 1
```

## Testing Locally

```bash
# Run Datadog Agent locally
docker run -d \
  --name datadog-agent \
  -e DD_API_KEY=$DD_API_KEY \
  -e DD_SITE=datadoghq.com \
  -e DD_APM_ENABLED=true \
  -e DD_LOGS_ENABLED=true \
  -p 8126:8126 \
  -p 8125:8125/udp \
  gcr.io/datadoghq/agent:latest

# Run your application
export DD_AGENT_HOST=localhost
export DD_TRACE_AGENT_PORT=8126
export DD_SERVICE=dispatch-api
dotnet run

# Generate test traffic
curl http://localhost:8080/api/orders -X POST

# View traces in Datadog UI
open https://app.datadoghq.com/apm/traces
```

## Cost Optimization

1. **Sample traces** (don't trace every request):
   ```bash
   DD_TRACE_SAMPLE_RATE=0.1  # Trace 10% of requests
   ```

2. **Limit custom metrics**:
   - Avoid high-cardinality tags (user IDs, request IDs)
   - Use metric aggregation

3. **Filter logs**:
   ```csharp
   .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
   .MinimumLevel.Override("System", LogEventLevel.Warning)
   ```

## Next Steps

1. Install Datadog Agent (Docker/Kubernetes)
2. Add Datadog.Trace NuGet package
3. Configure unified service tagging (service, env, version)
4. Add custom instrumentation for Dispatch operations
5. Send custom metrics via DogStatsD
6. Configure structured logging with Serilog
7. Create dashboards and monitors
8. Set up alerts and notifications
9. Explore APM service map and traces
10. Enable continuous profiling

## See Also

- [Observability Overview](./index.md) - Monitor Dispatch applications with OpenTelemetry, health checks, and integrations
- [Metrics Reference](./metrics-reference.md) - Complete catalog of 100+ available metrics
- [Grafana Dashboards](./grafana-dashboards.md) - Pre-built Grafana dashboards with Prometheus metrics
