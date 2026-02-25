---
sidebar_position: 5
title: AWS CloudWatch Integration
description: Monitor Dispatch applications with AWS CloudWatch
---

# AWS CloudWatch Integration

Dispatch integrates with AWS CloudWatch for comprehensive monitoring of serverless and containerized applications running on AWS. This guide covers metrics, logging, tracing with AWS X-Ray, and custom dashboards.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An AWS account with CloudWatch access
- Familiarity with [production observability](./production-observability.md) and [metrics reference](./metrics-reference.md)

## Installation

```bash
# Core observability
dotnet add package Excalibur.Dispatch.Observability

# AWS Lambda hosting
dotnet add package Excalibur.Dispatch.Hosting.AwsLambda

# AWS SQS transport (includes CloudWatch metrics)
dotnet add package Excalibur.Dispatch.Transport.AwsSqs

# OpenTelemetry AWS exporters
dotnet add package OpenTelemetry.Exporter.AwsXRay
dotnet add package AWS.Distro.OpenTelemetry.AspNetCore
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
      "OtlpEndpoint": "https://otlp.amazonaws.com"
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

### Configure AWS X-Ray Tracing

```csharp
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Excalibur.Dispatch.Observability.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAWSInstrumentation()
            .AddXRayTraceId()
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri("https://otlp.us-east-1.amazonaws.com");
            });
    });
```

## AWS Lambda Integration

### Configure Lambda Hosting

```csharp
builder.Services.AddAwsLambdaServerless(options =>
{
    options.EnableColdStartOptimization = true;
    options.GracefulShutdownTimeout = TimeSpan.FromSeconds(5);
});

// Configure Lambda-specific options
builder.Services.Configure<AwsLambdaOptions>(options =>
{
    options.Runtime = "dotnet8";
    options.EnableProvisionedConcurrency = true;
    options.ReservedConcurrency = 100;
});
```

### AwsLambdaOptions

```csharp
public class AwsLambdaOptions
{
    // Enable provisioned concurrency (default: false)
    public bool EnableProvisionedConcurrency { get; set; }

    // Reserved concurrency limit (null = no limit)
    public int? ReservedConcurrency { get; set; }

    // Lambda runtime (default: "dotnet8")
    public string Runtime { get; set; } = "dotnet8";

    // Handler name for deployment
    public string? Handler { get; set; }

    // Package type: "Zip" or "Image" (default: "Zip")
    public string PackageType { get; set; } = "Zip";
}
```

### Lambda Function with Dispatch

```csharp
public class Function
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

        services.AddAwsLambdaServerless();

        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
    }

    public async Task<string> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            var message = JsonSerializer.Deserialize<OrderMessage>(record.Body);
            await _dispatcher.DispatchAsync(new ProcessOrderAction(message), context.CancellationToken);
        }

        return "OK";
    }
}
```

## Custom CloudWatch Metrics

Use standard `System.Diagnostics.Metrics` APIs to publish custom metrics. The OpenTelemetry SDK exports them to CloudWatch automatically when configured with the OTLP exporter.

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

## CloudWatch Alarms

### Recommended Alarms

Create CloudWatch alarms for critical Dispatch metrics:

```yaml
# CloudFormation template example
Resources:
  HighErrorRateAlarm:
    Type: AWS::CloudWatch::Alarm
    Properties:
      AlarmName: dispatch-high-error-rate
      MetricName: dispatch.messages.failed
      Namespace: Dispatch
      Statistic: Sum
      Period: 300
      EvaluationPeriods: 2
      Threshold: 10
      ComparisonOperator: GreaterThanThreshold
      AlarmActions:
        - !Ref AlertSNSTopic

  HighLatencyAlarm:
    Type: AWS::CloudWatch::Alarm
    Properties:
      AlarmName: dispatch-high-latency
      MetricName: dispatch.messages.duration
      Namespace: Dispatch
      Statistic: p95
      Period: 300
      EvaluationPeriods: 3
      Threshold: 5000
      ComparisonOperator: GreaterThanThreshold
      AlarmActions:
        - !Ref AlertSNSTopic

  DeadLetterQueueDepthAlarm:
    Type: AWS::CloudWatch::Alarm
    Properties:
      AlarmName: dispatch-dlq-depth
      MetricName: dispatch.transport.pending_messages
      Namespace: Dispatch
      Dimensions:
        - Name: queue_type
          Value: dead-letter
      Statistic: Average
      Period: 60
      EvaluationPeriods: 5
      Threshold: 100
      ComparisonOperator: GreaterThanThreshold
```

### Terraform Example

```hcl
resource "aws_cloudwatch_metric_alarm" "dispatch_errors" {
  alarm_name          = "dispatch-high-error-rate"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = "2"
  metric_name         = "dispatch.messages.failed"
  namespace           = "Dispatch"
  period              = "300"
  statistic           = "Sum"
  threshold           = "10"
  alarm_description   = "Dispatch message processing errors exceed threshold"
  alarm_actions       = [aws_sns_topic.alerts.arn]
}
```

## CloudWatch Logs Insights

### Useful Queries

**Find failed message processing:**
```
fields @timestamp, @message
| filter @message like /Failed to process message/
| sort @timestamp desc
| limit 100
```

**Analyze processing duration by message type:**
```
fields @timestamp, messageType, duration
| filter @message like /Message processed/
| stats avg(duration), max(duration), min(duration) by messageType
```

**Track dead letter queue activity:**
```
fields @timestamp, messageId, reason
| filter @message like /Dead letter/
| stats count(*) by reason
| sort count desc
```

**Find correlation across services:**
```
fields @timestamp, correlationId, @message
| filter correlationId = "abc-123"
| sort @timestamp asc
```

## CloudWatch Dashboards

### Pre-built Dashboard Template

```json
{
  "widgets": [
    {
      "type": "metric",
      "properties": {
        "title": "Message Throughput",
        "metrics": [
          ["Dispatch", "dispatch.messages.processed", {"stat": "Sum", "period": 60}],
          [".", "dispatch.messages.published", {"stat": "Sum", "period": 60}]
        ],
        "view": "timeSeries",
        "region": "us-east-1"
      }
    },
    {
      "type": "metric",
      "properties": {
        "title": "Error Rate",
        "metrics": [
          ["Dispatch", "dispatch.messages.failed", {"stat": "Sum", "period": 60}],
          [".", "dispatch.transport.errors_total", {"stat": "Sum", "period": 60}]
        ],
        "view": "timeSeries",
        "region": "us-east-1"
      }
    },
    {
      "type": "metric",
      "properties": {
        "title": "Processing Latency (p95)",
        "metrics": [
          ["Dispatch", "dispatch.messages.duration", {"stat": "p95", "period": 60}]
        ],
        "view": "timeSeries",
        "region": "us-east-1"
      }
    },
    {
      "type": "metric",
      "properties": {
        "title": "Transport Health",
        "metrics": [
          ["Dispatch", "dispatch.transport.connection_status", {"stat": "Average", "period": 60}],
          [".", "dispatch.transport.pending_messages", {"stat": "Average", "period": 60}]
        ],
        "view": "timeSeries",
        "region": "us-east-1"
      }
    }
  ]
}
```

## AWS SQS Integration

When using AWS SQS as a transport, additional metrics are automatically collected:

```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);

builder.Services.AddAwsSqsTransport("my-queue", sqs =>
{
    sqs.UseRegion("us-east-1")
       .MapQueue<MyMessage>("https://sqs.us-east-1.amazonaws.com/123456789012/my-queue");
});
```

### SQS-Specific Metrics

| Metric | Description |
|--------|-------------|
| `ApproximateNumberOfMessages` | Messages in queue |
| `ApproximateNumberOfMessagesNotVisible` | Messages in flight |
| `NumberOfMessagesSent` | Messages sent |
| `NumberOfMessagesReceived` | Messages received |
| `NumberOfMessagesDeleted` | Messages deleted |

## Best Practices

### 1. Use Structured Logging

```csharp
_logger.LogInformation(
    "Processing message {MessageId} of type {MessageType} for tenant {TenantId}",
    message.Id,
    message.GetType().Name,
    context.GetTenantId());
```

### 2. Add Custom Dimensions

```csharp
_metricsCollector.RecordMetric(new TransportMetricData
{
    Name = "orders.processed",
    Value = 1,
    Tags = new Dictionary<string, string>
    {
        ["environment"] = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "unknown",
        ["region"] = order.Region,
        ["order_type"] = order.Type.ToString()
    }
});
```

### 3. Configure Appropriate Retention

```csharp
// In CloudWatch, set log group retention
// Recommended: 14-30 days for detailed logs
// Recommended: 90 days for aggregated metrics
```

### 4. Enable X-Ray Sampling

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.SetSampler(new ParentBasedSampler(
            new TraceIdRatioBasedSampler(0.1))); // Sample 10%
    });
```

### 5. Use CloudWatch Contributor Insights

Enable Contributor Insights rules for:
- Top message types by volume
- Top error types
- Slowest message processing by type

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

## Related Documentation

- [Health Checks](health-checks.md) - Application health monitoring
- [Observability Overview](index.md) - Distributed tracing and metrics setup
- [Google Cloud Monitoring](google-cloud-monitoring.md) - GCP observability

## See Also

- [Production Observability](../observability/production-observability.md) — Operational best practices for monitoring Dispatch in production environments
- [Metrics Reference](../observability/metrics-reference.md) — Complete catalog of all OpenTelemetry metrics exposed by Dispatch
- [AWS Lambda Deployment](../deployment/aws-lambda.md) — Deploy Dispatch applications as AWS Lambda functions
