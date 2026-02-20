---
sidebar_position: 2
---

# Grafana Dashboards

Building comprehensive Grafana dashboards for monitoring Excalibur applications with Prometheus metrics.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Grafana instance with Prometheus data source
- Familiarity with [metrics reference](./metrics-reference.md) and [production observability](./production-observability.md)

## Overview

Grafana provides powerful visualization and alerting for:
- **Application Metrics**: Request rates, error rates, latency
- **Business Metrics**: Commands processed, events published, aggregates loaded
- **Infrastructure Metrics**: CPU, memory, database connections
- **Custom Metrics**: Domain-specific KPIs

## Prerequisites

### Install Prometheus

```yaml
# docker-compose.yml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    container_name: prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'

  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning
    depends_on:
      - prometheus

volumes:
  prometheus-data:
  grafana-data:
```

### Prometheus Configuration

```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'dispatch-api'
    static_configs:
      - targets: ['host.docker.internal:8080']
        labels:
          app: 'dispatch-api'
          environment: 'production'

  - job_name: 'dispatch-worker'
    static_configs:
      - targets: ['host.docker.internal:8081']
        labels:
          app: 'dispatch-worker'
          environment: 'production'
```

## Add Prometheus Metrics to ASP.NET Core

### Install Package

```bash
dotnet add package prometheus-net.AspNetCore
```

### Configure Metrics

```csharp
// Program.cs
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

var app = builder.Build();

// Enable Prometheus metrics
app.UseMetricServer();  // /metrics endpoint
app.UseHttpMetrics();   // HTTP request metrics

app.MapControllers();

app.Run();
```

**Metrics endpoint:** `http://localhost:8080/metrics`

## Custom Dispatch Metrics

### Command Processing Metrics

```csharp
using Prometheus;

public class MetricsMiddleware : IDispatchMiddleware
{
    private static readonly Counter MessagesProcessed = Metrics
        .CreateCounter(
            "dispatch_messages_total",
            "Total messages processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "message_type", "status" }
            });

    private static readonly Histogram MessageDuration = Metrics
        .CreateHistogram(
            "dispatch_message_duration_seconds",
            "Message processing duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "message_type" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
            });

    private static readonly Gauge ActiveMessages = Metrics
        .CreateGauge(
            "dispatch_messages_active",
            "Number of messages currently being processed",
            new GaugeConfiguration
            {
                LabelNames = new[] { "message_type" }
            });

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var messageType = message.GetType().Name;

        using (MessageDuration.WithLabels(messageType).NewTimer())
        using (ActiveMessages.WithLabels(messageType).TrackInProgress())
        {
            try
            {
                var result = await nextDelegate(message, context, cancellationToken);

                MessagesProcessed
                    .WithLabels(messageType, result.Succeeded ? "success" : "failure")
                    .Inc();

                return result;
            }
            catch
            {
                MessagesProcessed
                    .WithLabels(messageType, "failure")
                    .Inc();

                throw;
            }
        }
    }
}

// Register middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<MetricsMiddleware>();
});
```

### Event Store Metrics

```csharp
public class MetricsEventStore : IEventStore
{
    private readonly IEventStore _inner;

    private static readonly Counter EventsAppended = Metrics
        .CreateCounter(
            "eventstore_events_appended_total",
            "Total events appended to event store",
            new CounterConfiguration
            {
                LabelNames = new[] { "aggregate_type", "event_type" }
            });

    private static readonly Histogram AppendDuration = Metrics
        .CreateHistogram(
            "eventstore_append_duration_seconds",
            "Duration of event store append operations",
            new HistogramConfiguration
            {
                LabelNames = new[] { "aggregate_type" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
            });

    private static readonly Counter ConcurrencyConflicts = Metrics
        .CreateCounter(
            "eventstore_concurrency_conflicts_total",
            "Total concurrency conflicts encountered",
            new CounterConfiguration
            {
                LabelNames = new[] { "aggregate_type" }
            });

    public MetricsEventStore(IEventStore inner)
    {
        _inner = inner;
    }

    public async ValueTask<AppendResult> AppendAsync(
        string aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using (AppendDuration.WithLabels(aggregateType).NewTimer())
        {
            try
            {
                var result = await _inner.AppendAsync(
                    aggregateId,
                    aggregateType,
                    events,
                    expectedVersion,
                    cancellationToken);

                foreach (var evt in events)
                {
                    EventsAppended
                        .WithLabels(aggregateType, evt.EventType)
                        .Inc();
                }

                return result;
            }
            catch (ConcurrencyException)
            {
                ConcurrencyConflicts
                    .WithLabels(aggregateType)
                    .Inc();

                throw;
            }
        }
    }

    // Implement other IEventStore methods (LoadAsync, etc.)...
}

// Register decorator
builder.Services.Decorate<IEventStore, MetricsEventStore>();
```

### Outbox Metrics

```csharp
public class MetricsOutboxPublisher : IOutboxPublisher
{
    private readonly IOutboxPublisher _inner;

    private static readonly Counter MessagesPublished = Metrics
        .CreateCounter(
            "outbox_messages_published_total",
            "Total messages published from outbox",
            new CounterConfiguration
            {
                LabelNames = new[] { "message_type", "status" }
            });

    private static readonly Histogram PublishDuration = Metrics
        .CreateHistogram(
            "outbox_publish_duration_seconds",
            "Duration of outbox publish operations");

    private static readonly Gauge PendingMessages = Metrics
        .CreateGauge(
            "outbox_pending_messages",
            "Number of messages pending in outbox");

    public MetricsOutboxPublisher(IOutboxPublisher inner)
    {
        _inner = inner;
    }

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        using (PublishDuration.NewTimer())
        {
            try
            {
                await _inner.PublishAsync(message, cancellationToken);

                MessagesPublished
                    .WithLabels(message.MessageType, "success")
                    .Inc();
            }
            catch
            {
                MessagesPublished
                    .WithLabels(message.MessageType, "failure")
                    .Inc();

                throw;
            }
        }
    }
}
```

## Grafana Dashboard JSON

### Command Processing Dashboard

Save as `grafana/dashboards/dispatch-commands.json`:

```json
{
  "dashboard": {
    "title": "Excalibur - Command Processing",
    "tags": ["dispatch", "commands"],
    "timezone": "browser",
    "panels": [
      {
        "id": 1,
        "title": "Command Rate",
        "type": "graph",
        "gridPos": { "x": 0, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(rate(dispatch_commands_total[5m])) by (command_type)",
            "legendFormat": "{{command_type}}"
          }
        ],
        "yaxes": [
          { "label": "Commands/sec", "format": "short" }
        ]
      },
      {
        "id": 2,
        "title": "Command Success Rate",
        "type": "graph",
        "gridPos": { "x": 12, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(rate(dispatch_commands_total{status=\"success\"}[5m])) / sum(rate(dispatch_commands_total[5m]))",
            "legendFormat": "Success Rate"
          }
        ],
        "yaxes": [
          { "label": "Success Rate", "format": "percentunit" }
        ]
      },
      {
        "id": 3,
        "title": "Command Duration (p50, p95, p99)",
        "type": "graph",
        "gridPos": { "x": 0, "y": 8, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "histogram_quantile(0.50, sum(rate(dispatch_command_duration_seconds_bucket[5m])) by (le, command_type))",
            "legendFormat": "p50 - {{command_type}}"
          },
          {
            "expr": "histogram_quantile(0.95, sum(rate(dispatch_command_duration_seconds_bucket[5m])) by (le, command_type))",
            "legendFormat": "p95 - {{command_type}}"
          },
          {
            "expr": "histogram_quantile(0.99, sum(rate(dispatch_command_duration_seconds_bucket[5m])) by (le, command_type))",
            "legendFormat": "p99 - {{command_type}}"
          }
        ],
        "yaxes": [
          { "label": "Duration", "format": "s" }
        ]
      },
      {
        "id": 4,
        "title": "Active Commands",
        "type": "graph",
        "gridPos": { "x": 12, "y": 8, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(dispatch_commands_active) by (command_type)",
            "legendFormat": "{{command_type}}"
          }
        ],
        "yaxes": [
          { "label": "Active", "format": "short" }
        ]
      }
    ]
  }
}
```

### Event Store Dashboard

Save as `grafana/dashboards/dispatch-eventstore.json`:

```json
{
  "dashboard": {
    "title": "Excalibur - Event Store",
    "tags": ["dispatch", "eventstore"],
    "timezone": "browser",
    "panels": [
      {
        "id": 1,
        "title": "Events Appended Rate",
        "type": "graph",
        "gridPos": { "x": 0, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(rate(eventstore_events_appended_total[5m])) by (aggregate_type)",
            "legendFormat": "{{aggregate_type}}"
          }
        ],
        "yaxes": [
          { "label": "Events/sec", "format": "short" }
        ]
      },
      {
        "id": 2,
        "title": "Append Duration",
        "type": "graph",
        "gridPos": { "x": 12, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum(rate(eventstore_append_duration_seconds_bucket[5m])) by (le, aggregate_type))",
            "legendFormat": "p95 - {{aggregate_type}}"
          }
        ],
        "yaxes": [
          { "label": "Duration", "format": "s" }
        ]
      },
      {
        "id": 3,
        "title": "Concurrency Conflicts",
        "type": "graph",
        "gridPos": { "x": 0, "y": 8, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(rate(eventstore_concurrency_conflicts_total[5m])) by (aggregate_type)",
            "legendFormat": "{{aggregate_type}}"
          }
        ],
        "yaxes": [
          { "label": "Conflicts/sec", "format": "short" }
        ]
      },
      {
        "id": 4,
        "title": "Event Types Distribution",
        "type": "piechart",
        "gridPos": { "x": 12, "y": 8, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(increase(eventstore_events_appended_total[1h])) by (event_type)",
            "legendFormat": "{{event_type}}"
          }
        ]
      }
    ]
  }
}
```

### Outbox Dashboard

```json
{
  "dashboard": {
    "title": "Excalibur - Outbox",
    "tags": ["dispatch", "outbox"],
    "timezone": "browser",
    "panels": [
      {
        "id": 1,
        "title": "Outbox Publish Rate",
        "type": "graph",
        "gridPos": { "x": 0, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "sum(rate(outbox_messages_published_total{status=\"success\"}[5m]))",
            "legendFormat": "Success"
          },
          {
            "expr": "sum(rate(outbox_messages_published_total{status=\"failure\"}[5m]))",
            "legendFormat": "Failure"
          }
        ],
        "yaxes": [
          { "label": "Messages/sec", "format": "short" }
        ]
      },
      {
        "id": 2,
        "title": "Pending Messages",
        "type": "gauge",
        "gridPos": { "x": 12, "y": 0, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "outbox_pending_messages"
          }
        ],
        "options": {
          "orientation": "auto",
          "showThresholdLabels": false,
          "showThresholdMarkers": true
        },
        "fieldConfig": {
          "defaults": {
            "thresholds": {
              "mode": "absolute",
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 100, "color": "yellow" },
                { "value": 500, "color": "red" }
              ]
            }
          }
        }
      },
      {
        "id": 3,
        "title": "Publish Duration",
        "type": "graph",
        "gridPos": { "x": 0, "y": 8, "w": 24, "h": 8 },
        "targets": [
          {
            "expr": "histogram_quantile(0.50, rate(outbox_publish_duration_seconds_bucket[5m]))",
            "legendFormat": "p50"
          },
          {
            "expr": "histogram_quantile(0.95, rate(outbox_publish_duration_seconds_bucket[5m]))",
            "legendFormat": "p95"
          },
          {
            "expr": "histogram_quantile(0.99, rate(outbox_publish_duration_seconds_bucket[5m]))",
            "legendFormat": "p99"
          }
        ],
        "yaxes": [
          { "label": "Duration", "format": "s" }
        ]
      }
    ]
  }
}
```

## Provisioning Dashboards

### Dashboard Provisioning

```yaml
# grafana/provisioning/dashboards/dashboard.yml
apiVersion: 1

providers:
  - name: 'Dispatch Dashboards'
    orgId: 1
    folder: 'Excalibur.Dispatch'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /etc/grafana/provisioning/dashboards
```

### Datasource Provisioning

```yaml
# grafana/provisioning/datasources/prometheus.yml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: true
```

## Application Insights Integration

For Azure-hosted applications, integrate Application Insights with Grafana:

### Install Plugin

```bash
grafana-cli plugins install grafana-azure-monitor-datasource
```

### Configure Datasource

```yaml
# grafana/provisioning/datasources/azure-monitor.yml
apiVersion: 1

datasources:
  - name: Azure Monitor
    type: grafana-azure-monitor-datasource
    access: proxy
    jsonData:
      subscriptionId: <your-subscription-id>
      tenantId: <your-tenant-id>
      clientId: <your-client-id>
      cloudName: azuremonitor
    secureJsonData:
      clientSecret: <your-client-secret>
```

### Query Application Insights

```
customMetrics
| where name == "dispatch_commands_total"
| summarize sum(value) by bin(timestamp, 5m), tostring(customDimensions.command_type)
```

## CloudWatch Integration

For AWS-hosted applications:

### Install Plugin

```bash
grafana-cli plugins install cloudwatch
```

### Configure Datasource

```yaml
# grafana/provisioning/datasources/cloudwatch.yml
apiVersion: 1

datasources:
  - name: CloudWatch
    type: cloudwatch
    jsonData:
      authType: keys
      defaultRegion: us-east-1
    secureJsonData:
      accessKey: <your-access-key>
      secretKey: <your-secret-key>
```

## Alerting

### Configure Alert Rules

```yaml
# grafana/provisioning/alerting/alerts.yml
apiVersion: 1

groups:
  - name: dispatch-alerts
    interval: 1m
    rules:
      - alert: HighCommandFailureRate
        expr: |
          sum(rate(dispatch_commands_total{status="failure"}[5m]))
          /
          sum(rate(dispatch_commands_total[5m]))
          > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High command failure rate"
          description: "Command failure rate is {{ $value | humanizePercentage }}"

      - alert: SlowCommandProcessing
        expr: |
          histogram_quantile(0.95,
            sum(rate(dispatch_command_duration_seconds_bucket[5m]))
            by (le, command_type)
          ) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Slow command processing"
          description: "p95 latency for {{ $labels.command_type }} is {{ $value }}s"

      - alert: HighConcurrencyConflicts
        expr: |
          sum(rate(eventstore_concurrency_conflicts_total[5m]))
          by (aggregate_type)
          > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High concurrency conflicts"
          description: "{{ $labels.aggregate_type }} has {{ $value }} conflicts/sec"

      - alert: OutboxBacklog
        expr: outbox_pending_messages > 1000
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "Outbox backlog"
          description: "{{ $value }} messages pending in outbox"
```

### Notification Channels

```yaml
# grafana/provisioning/notifiers/slack.yml
apiVersion: 1

notifiers:
  - name: Slack Alerts
    type: slack
    uid: slack-alerts
    org_id: 1
    is_default: true
    send_reminder: false
    settings:
      url: https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK
      recipient: '#alerts'
      username: Grafana
```

## Custom Variables

Add dashboard variables for filtering:

```json
{
  "templating": {
    "list": [
      {
        "name": "environment",
        "type": "query",
        "query": "label_values(dispatch_commands_total, environment)",
        "current": {
          "text": "production",
          "value": "production"
        }
      },
      {
        "name": "command_type",
        "type": "query",
        "query": "label_values(dispatch_commands_total{environment=\"$environment\"}, command_type)",
        "multi": true,
        "includeAll": true
      }
    ]
  }
}
```

**Usage in queries:**
```
sum(rate(dispatch_commands_total{environment="$environment", command_type=~"$command_type"}[5m]))
```

## Kubernetes Dashboards

Monitor Kubernetes-deployed Dispatch apps:

```json
{
  "dashboard": {
    "title": "Excalibur - Kubernetes",
    "panels": [
      {
        "title": "Pod CPU Usage",
        "targets": [
          {
            "expr": "sum(rate(container_cpu_usage_seconds_total{pod=~\"dispatch-.*\"}[5m])) by (pod)"
          }
        ]
      },
      {
        "title": "Pod Memory Usage",
        "targets": [
          {
            "expr": "sum(container_memory_working_set_bytes{pod=~\"dispatch-.*\"}) by (pod)"
          }
        ]
      },
      {
        "title": "Pod Restarts",
        "targets": [
          {
            "expr": "sum(kube_pod_container_status_restarts_total{pod=~\"dispatch-.*\"}) by (pod)"
          }
        ]
      }
    ]
  }
}
```

## Business Metrics Dashboard

Track domain-specific KPIs:

```csharp
// Custom business metrics
public class OrderMetrics
{
    private static readonly Counter OrdersCreated = Metrics
        .CreateCounter(
            "orders_created_total",
            "Total orders created");

    private static readonly Counter OrdersCancelled = Metrics
        .CreateCounter(
            "orders_cancelled_total",
            "Total orders cancelled");

    private static readonly Histogram OrderValue = Metrics
        .CreateHistogram(
            "order_value_dollars",
            "Order value in dollars",
            new HistogramConfiguration
            {
                Buckets = new[] { 10, 50, 100, 250, 500, 1000, 5000 }
            });

    public static void RecordOrderCreated(decimal value)
    {
        OrdersCreated.Inc();
        OrderValue.Observe((double)value);
    }

    public static void RecordOrderCancelled()
    {
        OrdersCancelled.Inc();
    }
}
```

**Dashboard:**
```json
{
  "dashboard": {
    "title": "Business Metrics - Orders",
    "panels": [
      {
        "title": "Orders Created (Hourly)",
        "targets": [
          {
            "expr": "sum(increase(orders_created_total[1h]))"
          }
        ]
      },
      {
        "title": "Cancellation Rate",
        "targets": [
          {
            "expr": "sum(rate(orders_cancelled_total[5m])) / sum(rate(orders_created_total[5m]))"
          }
        ]
      },
      {
        "title": "Order Value Distribution",
        "targets": [
          {
            "expr": "histogram_quantile(0.50, rate(order_value_dollars_bucket[1h]))",
            "legendFormat": "Median"
          },
          {
            "expr": "histogram_quantile(0.95, rate(order_value_dollars_bucket[1h]))",
            "legendFormat": "p95"
          }
        ]
      }
    ]
  }
}
```

## Best Practices

### 1. Use Consistent Label Names

```csharp
// GOOD - Consistent labeling
private static readonly Counter CommandsProcessed = Metrics
    .CreateCounter(
        "dispatch_commands_total",
        "Total commands processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "command_type", "status", "environment" }
        });

// BAD - Inconsistent labeling
private static readonly Counter CommandsProcessed = Metrics
    .CreateCounter(
        "commands_count",  // Inconsistent naming
        "Commands",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "result" }  // Different label names
        });
```

### 2. Choose Appropriate Metric Types

- **Counter**: Monotonically increasing values (requests, errors)
- **Gauge**: Values that can go up or down (active connections, queue depth)
- **Histogram**: Distributions (latency, request size)
- **Summary**: Like histogram, but calculated client-side

### 3. Use Histogram Buckets Wisely

```csharp
// Exponential buckets for latency
Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
// [0.001, 0.002, 0.004, 0.008, 0.016, 0.032, 0.064, 0.128, 0.256, 0.512]

// Linear buckets for sizes
Buckets = Histogram.LinearBuckets(0, 100, 10)
// [0, 100, 200, 300, 400, 500, 600, 700, 800, 900]
```

### 4. Limit Cardinality

```csharp
// BAD - High cardinality (user_id can have millions of values)
CommandsProcessed.WithLabels(commandType, userId).Inc();

// GOOD - Low cardinality
CommandsProcessed.WithLabels(commandType, "success").Inc();
```

### 5. Dashboard Organization

1. **Overview Dashboard**: High-level health metrics
2. **Component Dashboards**: Dispatcher, Event Store, Outbox
3. **Business Dashboards**: Domain-specific KPIs
4. **Infrastructure Dashboards**: CPU, memory, network

## Testing Dashboards Locally

```bash
# Start Prometheus + Grafana
docker-compose up -d

# Run your application
dotnet run

# Generate test traffic
curl http://localhost:8080/api/orders -X POST -d '{"productId":"123"}'

# View metrics
curl http://localhost:8080/metrics

# Access Grafana
open http://localhost:3000
# Login: admin / admin
```

## Next Steps

1. Add Prometheus metrics to your Dispatch application
2. Deploy Prometheus and Grafana (Docker Compose or Kubernetes)
3. Import dashboard JSON files
4. Configure alerting rules
5. Set up notification channels (Slack, PagerDuty, email)
6. Create custom business metrics dashboards
7. Integrate with other monitoring platforms:
   - [Datadog Integration](./datadog-integration.md)
   - [Azure Monitor](./azure-monitor.md)

## See Also

- [Observability Overview](./index.md) - Monitor Dispatch applications with OpenTelemetry, health checks, and integrations
- [Metrics Reference](./metrics-reference.md) - Complete catalog of 100+ available metrics
- [Datadog Integration](./datadog-integration.md) - Datadog APM and custom metrics integration
