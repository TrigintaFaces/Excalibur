---
sidebar_position: 6
title: Google Cloud Monitoring Integration
description: Monitor Dispatch applications with Google Cloud Monitoring
---

# Google Cloud Monitoring Integration

Dispatch integrates with Google Cloud Monitoring for comprehensive observability of applications running on Cloud Run, GKE, Cloud Functions, and Compute Engine. This guide covers metrics, logging with Cloud Logging, tracing with Cloud Trace, and custom dashboards.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Google Cloud project with Monitoring API enabled
- Familiarity with [production observability](./production-observability.md) and [metrics reference](./metrics-reference.md)

## Installation

```bash
# Core observability
dotnet add package Excalibur.Dispatch.Observability

# Google Cloud Functions hosting
dotnet add package Excalibur.Dispatch.Hosting.GoogleCloudFunctions

# Google Pub/Sub transport (includes Cloud Monitoring metrics)
dotnet add package Excalibur.Dispatch.Transport.GooglePubSub

# OpenTelemetry Google Cloud exporters
dotnet add package OpenTelemetry.Exporter.GoogleCloud
dotnet add package Google.Cloud.Monitoring.V3
```

## Basic Configuration

### Enable Dispatch Observability

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddDispatchObservability(options =>
{
    options.Enabled = true;
    options.ServiceName = "my-dispatch-service";
    options.ServiceVersion = "1.0.0";
});
```

### From Configuration

```csharp
builder.Services.AddDispatchObservability(
    builder.Configuration.GetSection("Dispatch:Observability"));
```

```json
{
  "Dispatch": {
    "Observability": {
      "Enabled": true,
      "ServiceName": "my-dispatch-service",
      "ServiceVersion": "1.0.0",
      "OtlpEndpoint": "http://localhost:4317"
    }
  }
}
```

## OpenTelemetry Integration

### Add Dispatch Metrics to OpenTelemetry

```csharp
using Excalibur.Dispatch.Observability.Metrics;

builder.Services.AddOpenTelemetry()
    .AddDispatchMetrics()      // Core Dispatch metrics
    .AddTransportMetrics()     // Transport-level metrics
    // Or add both at once:
    .AddAllDispatchMetrics();
```

### Available Metrics

#### Core Dispatch Metrics (`Excalibur.Dispatch`)

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.messages.processed` | Counter | Total messages processed |
| `dispatch.messages.duration` | Histogram | Processing duration in ms |
| `dispatch.messages.published` | Counter | Messages published |
| `dispatch.messages.failed` | Counter | Failed message processing |
| `dispatch.sessions.active` | Gauge | Active processing sessions |

#### Transport Metrics (`Excalibur.Dispatch.Transport`)

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.transport.messages_sent_total` | Counter | Messages sent |
| `dispatch.transport.messages_received_total` | Counter | Messages received |
| `dispatch.transport.errors_total` | Counter | Transport errors |
| `dispatch.transport.send_duration_ms` | Histogram | Send duration |
| `dispatch.transport.receive_duration_ms` | Histogram | Receive duration |
| `dispatch.transport.starts_total` | Counter | Transport starts |
| `dispatch.transport.stops_total` | Counter | Transport stops |
| `dispatch.transport.connection_status` | Gauge | Connection status |
| `dispatch.transport.pending_messages` | Gauge | Pending messages |

### Configure Cloud Trace

```csharp
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Excalibur.Dispatch.Observability.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

## Google Cloud Functions Integration

### Configure Cloud Functions Hosting

```csharp
builder.Services.AddGoogleCloudFunctionsServerless(options =>
{
    options.EnableColdStartOptimization = true;
    options.GracefulShutdownTimeout = TimeSpan.FromSeconds(5);
});

// Configure Cloud Functions-specific options
builder.Services.Configure<GoogleCloudFunctionsOptions>(options =>
{
    options.Runtime = "dotnet6";
    options.MinInstances = 1;
    options.MaxInstances = 100;
    options.IngressSettings = "ALLOW_ALL";
    options.VpcConnector = "projects/my-project/locations/us-central1/connectors/my-connector";
});
```

### GoogleCloudFunctionsOptions

```csharp
public class GoogleCloudFunctionsOptions
{
    // .NET runtime version (default: "dotnet6")
    public string Runtime { get; set; } = "dotnet6";

    // Minimum instance count for warm starts (null = no minimum)
    public int? MinInstances { get; set; }

    // Maximum instance count (null = no limit)
    public int? MaxInstances { get; set; }

    // Ingress settings: "ALLOW_ALL", "ALLOW_INTERNAL_ONLY", "ALLOW_INTERNAL_AND_GCLB"
    public string IngressSettings { get; set; } = "ALLOW_ALL";

    // VPC connector for private network access
    public string? VpcConnector { get; set; }
}
```

### Cloud Function with Dispatch

```csharp
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;

public class Function : ICloudEventFunction<MessagePublishedData>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDispatcher _dispatcher;

    public Function()
    {
        var services = new ServiceCollection();

        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Function).Assembly);
            dispatch.AddObservability(obs => obs.ServiceName = "order-processor");
        });

        services.AddGoogleCloudFunctionsServerless();

        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
    }

    public async Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken ct)
    {
        var messageData = data.Message?.TextData;
        if (string.IsNullOrEmpty(messageData)) return;

        var message = JsonSerializer.Deserialize<OrderMessage>(messageData);
        await _dispatcher.DispatchAsync(new ProcessOrderAction(message), ct);
    }
}
```

## Custom Metrics

Use standard `System.Diagnostics.Metrics` APIs to publish custom metrics. The OpenTelemetry SDK exports them to Cloud Monitoring automatically when configured with the OTLP exporter.

### Custom Metrics Example

```csharp
public class OrderProcessingService
{
    private static readonly Meter OrderMeter = new("OrderService");
    private static readonly Counter<long> OrdersProcessed = OrderMeter.CreateCounter<long>("orders.processed");
    private static readonly Counter<long> OrdersFailed = OrderMeter.CreateCounter<long>("orders.failed");
    private static readonly Histogram<double> OrderDuration = OrderMeter.CreateHistogram<double>("orders.duration_ms");

    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        ILogger<OrderProcessingService> logger)
    {
        _logger = logger;
        _logger = logger;
    }

    public async Task ProcessOrderAsync(Order order, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await ProcessAsync(order, ct);

            // Record success metric
            OrdersProcessed.Add(1,
                new KeyValuePair<string, object?>("order_type", order.Type.ToString()),
                new KeyValuePair<string, object?>("region", order.Region));
        }
        catch (Exception ex)
        {
            OrdersFailed.Add(1);
            throw;
        }
        finally
        {
            // Record duration
            OrderDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
```

## Google Pub/Sub Integration

When using Google Pub/Sub as a transport, additional metrics and telemetry are automatically collected.

### Configure Pub/Sub Transport

```csharp
builder.Services.AddGooglePubSubTransport("orders", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("orders-topic")
          .SubscriptionId("orders-subscription")
          .ConfigureOptions(options =>
          {
              // Telemetry settings
              options.EnableOpenTelemetry = true;
              options.ExportToCloudMonitoring = true;
              options.TracingSamplingRatio = 0.1; // 10% sampling
              options.TelemetryExportIntervalSeconds = 60;
          });
});
```

### GooglePubSubOptions

```csharp
public sealed class GooglePubSubOptions
{
    // Google Cloud project ID
    public string ProjectId { get; set; } = string.Empty;

    // Pub/Sub topic ID for publishing
    public string TopicId { get; set; } = string.Empty;

    // Pub/Sub subscription ID for receiving
    public string SubscriptionId { get; set; } = string.Empty;

    // Full subscription name: projects/{project}/subscriptions/{subscription}
    public string SubscriptionName { get; }

    // Full topic name: projects/{project}/topics/{topic}
    public string TopicName { get; }

    // Message processing settings
    public int MaxPullMessages { get; set; } = 100;
    public int AckDeadlineSeconds { get; set; } = 60;
    public bool EnableAutoAckExtension { get; set; } = true;
    public int MaxConcurrentAcks { get; set; } = 10;
    public int MaxConcurrentMessages { get; set; } // 0 = ProcessorCount * 2

    // Dead letter settings
    public bool EnableDeadLetterTopic { get; set; }
    public string? DeadLetterTopicId { get; set; }

    // Telemetry settings
    public bool EnableOpenTelemetry { get; set; } = true;
    public bool ExportToCloudMonitoring { get; set; }
    public string? OtlpEndpoint { get; set; }
    public int TelemetryExportIntervalSeconds { get; set; } = 60;
    public bool EnableTracePropagation { get; set; } = true;
    public bool IncludeMessageAttributesInTraces { get; set; }
    public double TracingSamplingRatio { get; set; } = 0.1;
    public Dictionary<string, string> TelemetryResourceLabels { get; set; }
}
```

### Pub/Sub with All Optimizations

```csharp
builder.Services.AddGooglePubSubTransport(pubsub =>
{
    pubsub.ProjectId("my-project");
    pubsub.TopicId("my-topic");
    pubsub.SubscriptionId("my-subscription");
    pubsub.ConfigureOptions(options =>
    {
        options.EnableOpenTelemetry = true;
        options.ExportToCloudMonitoring = true;
    });
});
```

### Pub/Sub-Specific Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `pubsub.messages.enqueued` | Counter | Messages enqueued for processing |
| `pubsub.messages.processed` | Counter | Messages successfully processed |
| `pubsub.messages.failed` | Counter | Messages that failed processing |
| `pubsub.message.queue_time` | Histogram | Time messages spend in queue |
| `pubsub.message.processing_time` | Histogram | Message processing duration |
| `pubsub.batches.created` | Counter | Batches created |
| `pubsub.batches.completed` | Counter | Batches completed |
| `pubsub.batch.size` | Histogram | Batch sizes |
| `pubsub.connections.created` | Counter | Connections created |
| `pubsub.connections.closed` | Counter | Connections closed |
| `pubsub.flow_control.permits` | Gauge | Available flow control permits |
| `pubsub.flow_control.bytes` | Gauge | Available flow control bytes |
| `pubsub.worker.active_count` | Gauge | Active worker count |
| `pubsub.worker.utilization` | Gauge | Worker utilization percentage |

### IGooglePubSubMetrics Interface

```csharp
public interface IGooglePubSubMetrics
{
    void MessageEnqueued();
    void MessageDequeued(TimeSpan queueTime);
    void MessageProcessed(TimeSpan duration);
    void MessageFailed();
    void BatchCreated(int size);
    void BatchCompleted(int size, TimeSpan duration);
    void ConnectionCreated();
    void ConnectionClosed();
    void RecordFlowControl(int permits, int bytes);
}
```

## Cloud Monitoring Alerting Policies

### Recommended Alerting Policies

Create alerting policies in Google Cloud Console or via Terraform:

```hcl
# Terraform example
resource "google_monitoring_alert_policy" "dispatch_high_error_rate" {
  display_name = "Dispatch High Error Rate"
  combiner     = "OR"

  conditions {
    display_name = "Error rate exceeds threshold"

    condition_threshold {
      filter          = "metric.type=\"custom.googleapis.com/cloudmessaging/messages/failed\" AND resource.type=\"global\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 10

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_RATE"
      }
    }
  }

  notification_channels = [google_monitoring_notification_channel.email.name]
}

resource "google_monitoring_alert_policy" "dispatch_high_latency" {
  display_name = "Dispatch High Latency"
  combiner     = "OR"

  conditions {
    display_name = "P95 latency exceeds threshold"

    condition_threshold {
      filter          = "metric.type=\"custom.googleapis.com/cloudmessaging/messages/duration\" AND resource.type=\"global\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 5000

      aggregations {
        alignment_period     = "60s"
        per_series_aligner   = "ALIGN_PERCENTILE_95"
      }
    }
  }

  notification_channels = [google_monitoring_notification_channel.email.name]
}

resource "google_monitoring_alert_policy" "pubsub_dead_letter_depth" {
  display_name = "Pub/Sub Dead Letter Queue Depth"
  combiner     = "OR"

  conditions {
    display_name = "Dead letter queue depth exceeds threshold"

    condition_threshold {
      filter          = "metric.type=\"pubsub.googleapis.com/subscription/dead_letter_message_count\" AND resource.type=\"pubsub_subscription\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 100

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_MEAN"
      }
    }
  }

  notification_channels = [google_monitoring_notification_channel.email.name]
}
```

## Cloud Logging Queries

### Useful Log Explorer Queries

**Find failed message processing:**
```
resource.type="cloud_function" OR resource.type="cloud_run_revision"
severity>=ERROR
jsonPayload.message=~"Failed to process message"
```

**Analyze processing duration by message type:**
```
resource.type="cloud_function"
jsonPayload.message=~"Message processed"
| json messageType, duration
| GROUP BY messageType
| AGGREGATE AVG(duration), MAX(duration), MIN(duration)
```

**Track dead letter queue activity:**
```
resource.type="pubsub_subscription"
jsonPayload.message=~"Dead letter"
| json messageId, reason
| GROUP BY reason
| COUNT(*)
```

**Find correlation across services:**
```
labels."logging.googleapis.com/trace"="projects/my-project/traces/abc123"
| ORDER BY timestamp ASC
```

## Cloud Monitoring Dashboards

### Pre-built Dashboard JSON

```json
{
  "displayName": "Dispatch Message Processing",
  "gridLayout": {
    "columns": "2",
    "widgets": [
      {
        "title": "Message Throughput",
        "xyChart": {
          "dataSets": [
            {
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/cloudmessaging/messages/processed\"",
                  "aggregation": {
                    "alignmentPeriod": "60s",
                    "perSeriesAligner": "ALIGN_RATE"
                  }
                }
              }
            },
            {
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/cloudmessaging/messages/published\"",
                  "aggregation": {
                    "alignmentPeriod": "60s",
                    "perSeriesAligner": "ALIGN_RATE"
                  }
                }
              }
            }
          ]
        }
      },
      {
        "title": "Error Rate",
        "xyChart": {
          "dataSets": [
            {
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/cloudmessaging/messages/failed\"",
                  "aggregation": {
                    "alignmentPeriod": "60s",
                    "perSeriesAligner": "ALIGN_RATE"
                  }
                }
              }
            }
          ]
        }
      },
      {
        "title": "Processing Latency (P95)",
        "xyChart": {
          "dataSets": [
            {
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/cloudmessaging/messages/duration\"",
                  "aggregation": {
                    "alignmentPeriod": "60s",
                    "perSeriesAligner": "ALIGN_PERCENTILE_95"
                  }
                }
              }
            }
          ]
        }
      },
      {
        "title": "Pub/Sub Flow Control",
        "xyChart": {
          "dataSets": [
            {
              "timeSeriesQuery": {
                "timeSeriesFilter": {
                  "filter": "metric.type=\"custom.googleapis.com/cloudmessaging/pubsub/flow_control/permits\"",
                  "aggregation": {
                    "alignmentPeriod": "60s",
                    "perSeriesAligner": "ALIGN_MEAN"
                  }
                }
              }
            }
          ]
        }
      }
    ]
  }
}
```

## PubSubTelemetryProvider

For advanced telemetry needs, use the `PubSubTelemetryProvider`:

```csharp
public class MessageProcessingService
{
    private readonly PubSubTelemetryProvider _telemetry;

    public MessageProcessingService(PubSubTelemetryProvider telemetry)
    {
        _telemetry = telemetry;
    }

    public async Task ProcessMessageAsync(PubsubMessage message, string subscription, CancellationToken ct)
    {
        // Start distributed trace
        using var activity = _telemetry.RecordMessageReceived(message, subscription);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await ProcessAsync(message, ct);
            _telemetry.RecordMessageAcknowledged(message.MessageId, subscription, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _telemetry.RecordMessageNacked(message.MessageId, subscription, ex.GetType().Name);
            throw;
        }
    }
}
```

### Export to Cloud Monitoring

```csharp
// Manual export to Cloud Monitoring
await _telemetry.ExportToCloudMonitoringAsync(cancellationToken);
```

## Best Practices

### 1. Use Structured Logging

```csharp
_logger.LogInformation(
    "Processing message {MessageId} of type {MessageType} for tenant {TenantId}",
    message.Id,
    message.GetType().Name,
    context.GetTenantId());
```

### 2. Add Custom Labels

```csharp
_metricsCollector.RecordCounter("orders.processed", 1,
    new Dictionary<string, string>
    {
        ["environment"] = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "unknown",
        ["region"] = order.Region,
        ["order_type"] = order.Type.ToString()
    });
```

### 3. Configure Appropriate Log Retention

```csharp
// In Cloud Logging, configure log bucket retention
// Recommended: 30 days for detailed logs
// Recommended: 400 days for aggregated metrics
```

### 4. Enable Trace Sampling

```csharp
builder.Services.Configure<GooglePubSubOptions>(options =>
{
    options.EnableTracePropagation = true;
    options.TracingSamplingRatio = 0.1; // Sample 10% of requests
});
```

### 5. Use Log-Based Metrics

Create log-based metrics in Cloud Monitoring for custom analysis:

```
# Create a log-based metric for slow message processing
filter: "resource.type=\"cloud_run_revision\" AND jsonPayload.duration > 5000"
metric_descriptor:
  type: "logging.googleapis.com/user/slow_message_processing"
  metric_kind: DELTA
  value_type: INT64
```

### 6. Set Up Error Reporting Integration

```csharp
// Errors logged with appropriate severity are automatically captured
_logger.LogError(exception,
    "Failed to process message {MessageId}: {ErrorType}",
    message.Id,
    exception.GetType().Name);
```

## Configuration Reference

### ContextObservabilityOptions

```csharp
public class ContextObservabilityOptions
{
    // Enable observability (default: false)
    public bool Enabled { get; set; }

    // Service name for telemetry
    public string ServiceName { get; set; } = "dispatch-service";

    // Service version
    public string ServiceVersion { get; set; } = "1.0.0";

    // OTLP endpoint for exporting telemetry
    public string? OtlpEndpoint { get; set; }

    // Export to Prometheus (default: false)
    public bool ExportToPrometheus { get; set; }

    // Export to Application Insights (default: false)
    public bool ExportToApplicationInsights { get; set; }

    // Application Insights connection string
    public string? ApplicationInsightsConnectionString { get; set; }

    // Custom resource attributes
    public Dictionary<string, string> ResourceAttributes { get; }
}
```

### GooglePubSubTelemetry Constants

```csharp
public static class GooglePubSubTelemetry
{
    // Activity source name for distributed tracing
    public const string ActivitySourceName = "Excalibur.Dispatch.Transport.GooglePubSub.PubSub";

    // OpenTelemetry tag names
    public static class Tags
    {
        public const string MessageId = "messaging.message_id";
        public const string OrderingKey = "messaging.ordering_key";
        public const string WorkerId = "messaging.worker_id";
        public const string Subscription = "messaging.destination";
        public const string Topic = "messaging.destination_kind";
        public const string ProjectId = "gcp.project_id";
        public const string BatchSize = "messaging.batch.message_count";
        public const string ErrorType = "error.type";
    }

    // Metric names
    public static class TelemetryMetrics
    {
        public const string MessagesEnqueued = "pubsub.messages.enqueued";
        public const string MessagesProcessed = "pubsub.messages.processed";
        public const string MessagesFailed = "pubsub.messages.failed";
        public const string QueueTime = "pubsub.message.queue_time";
        public const string ProcessingTime = "pubsub.message.processing_time";
        public const string WorkerUtilization = "pubsub.worker.utilization";
        public const string ActiveWorkers = "pubsub.worker.active_count";
    }
}
```

## Related Documentation

- [Health Checks](health-checks.md) - Application health monitoring
- [Observability Overview](index.md) - Distributed tracing and metrics setup
- [AWS CloudWatch](aws-cloudwatch.md) - AWS observability

## See Also

- [Production Observability](../observability/production-observability.md) — Operational best practices for monitoring Dispatch in production environments
- [Metrics Reference](../observability/metrics-reference.md) — Complete catalog of all OpenTelemetry metrics exposed by Dispatch
- [Google Cloud Functions Deployment](../deployment/google-cloud-functions.md) — Deploy Dispatch applications as Google Cloud Functions
