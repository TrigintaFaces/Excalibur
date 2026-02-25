---
sidebar_position: 6
title: AWS SQS Transport
description: AWS SQS and SNS transport for AWS-native cloud messaging
---

# AWS SQS Transport

AWS Simple Queue Service (SQS) transport with optional SNS integration for AWS-native messaging.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An AWS account with SQS access
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AwsSqs
```

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseAwsSqs(sqs =>
    {
        sqs.UseRegion("us-west-2")
           .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
           .MapQueue<OrderCreated>("https://sqs.us-west-2.amazonaws.com/123456789012/orders");
    });
});
```

### Standalone Registration
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Named transport registration with fluent builder
services.AddAwsSqsTransport("orders", sqs =>
{
    sqs.UseRegion("us-west-2")
       .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
       .MapQueue<OrderCreated>("https://sqs.us-west-2.amazonaws.com/123456789012/orders");
});
```

### Lower-Level Message Bus Registration

For scenarios requiring direct message bus access without transport abstractions:

```csharp
// Add all AWS services (SQS, SNS, EventBridge)
services.AddAwsMessageBus(options =>
{
    options.Region = "us-west-2";
    options.EnableSqs = true;
    options.EnableSns = true;
    options.EnableEventBridge = false;
});
```

## Configuration

### Fluent Builder Configuration

Configure AWS SQS transport using the fluent builder:

```csharp
services.AddAwsSqsTransport("orders", sqs =>
{
    sqs.UseRegion("us-east-1")
       .UseSchemaRegistry(registry =>
       {
           registry.RegistryName = "my-registry";
           registry.DefaultCompatibility = AwsGlueCompatibilityMode.Backward;
       })
       .ConfigureQueue(queue =>
       {
           queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
                .MessageRetentionPeriod(TimeSpan.FromDays(7))
                .ReceiveWaitTimeSeconds(20)
                .DeadLetterQueue(dlq =>
                {
                    dlq.QueueArn("arn:aws:sqs:us-east-1:123456789012:orders-dlq")
                       .MaxReceiveCount(3);
                });
       })
       .ConfigureFifo(fifo =>
       {
           fifo.ContentBasedDeduplication(true)
               .MessageGroupIdSelector<OrderCreated>(msg => msg.TenantId);
       })
       .ConfigureBatch(batch =>
       {
           batch.SendBatchSize(10)
                .SendBatchWindow(TimeSpan.FromMilliseconds(100))
                .ReceiveMaxMessages(10);
       })
       .ConfigureCloudEvents(ce =>
       {
           ce.UseFifoFeatures = true;
           ce.DefaultMessageGroupId = "orders";
           ce.EnablePayloadCompression = true;
       })
       .MapQueue<OrderCreated>("https://sqs.us-east-1.amazonaws.com/123456789012/orders");
});
```

## Queue Types

### Standard Queue

```csharp
services.AddAwsSqsTransport(sqs =>
{
    sqs.UseRegion("us-west-2")
       .ConfigureQueue(queue =>
       {
           queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
                .MessageRetentionPeriod(TimeSpan.FromDays(4))
                .ReceiveWaitTimeSeconds(20);
       })
       .MapQueue<OrderCreated>("https://sqs.us-west-2.amazonaws.com/123456789012/orders");
    // Standard queues provide:
    // - At-least-once delivery
    // - Best-effort ordering
    // - Nearly unlimited throughput
});
```

### FIFO Queue

```csharp
services.AddAwsSqsTransport(sqs =>
{
    sqs.UseRegion("us-west-2")
       .ConfigureFifo(fifo =>
       {
           // Content-based deduplication (5-minute window)
           fifo.ContentBasedDeduplication(true)
               // Group messages by tenant for ordered processing
               .MessageGroupIdSelector<OrderCreated>(msg => msg.TenantId);
       })
       .MapQueue<OrderCreated>("https://sqs.us-west-2.amazonaws.com/123456789012/orders.fifo");
});
```

## SNS Integration (Pub/Sub)

Use `ConfigureSns` for SNS topic integration or `AddAwsSnsTransport` for standalone SNS:

```csharp
// Integrate SNS with SQS transport
services.AddAwsSqsTransport(sqs =>
{
    sqs.UseRegion("us-east-1")
       .ConfigureSns(sns =>
       {
           sns.TopicPrefix("myapp-")
              .AutoCreateTopics(true)
              .RawMessageDelivery(true)
              .MapTopic<OrderCreated>("arn:aws:sns:us-east-1:123:orders")
              .SubscribeQueue<OrderCreated>(sub =>
              {
                  sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
                     .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders")
                     .FilterPolicy(filter =>
                     {
                         filter.Attribute("priority").Equals("high");
                     });
              });
       });
});

// Or add SNS as a separate transport
services.AddAwsSnsTransport(sns =>
{
    sns.TopicArn("arn:aws:sns:us-east-1:123456789:my-topic")
       .Region("us-east-1")
       .EnableRawMessageDelivery();
});
```

## CloudEvents Configuration

Configure CloudEvents via the transport builder or standalone:

### Via Transport Builder
```csharp
services.AddAwsSqsTransport(sqs =>
{
    sqs.UseRegion("us-east-1")
       .ConfigureCloudEvents(ce =>
       {
           ce.UseFifoFeatures = true;
           ce.DefaultMessageGroupId = "orders";
           ce.EnableContentBasedDeduplication = true;
           ce.EnablePayloadCompression = true;
           ce.CompressionThreshold = 64 * 1024; // 64KB
           ce.EnableDoDCompliance = true;
       });
});
```

### Standalone CloudEvents
```csharp
services.UseCloudEventsForSqs(options =>
{
    options.UseFifoFeatures = true;
    options.DefaultMessageGroupId = "orders";
    options.EnablePayloadCompression = true;
});
```

## Dead Letter Queue

Configure DLQ via AWS console or infrastructure-as-code (CloudFormation/Terraform). The DLQ ARN is specified at the queue level, not in code.

### Processing Dead Letters

```csharp
services.AddHostedService<DeadLetterProcessor>();

public class DeadLetterProcessor : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly string _dlqUrl;
    private readonly ILogger<DeadLetterProcessor> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _dlqUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20
            }, ct);

            foreach (var message in response.Messages)
            {
                _logger.LogWarning("Dead letter: {MessageId}", message.MessageId);
                // Process or archive
                await _sqs.DeleteMessageAsync(_dlqUrl, message.ReceiptHandle, ct);
            }
        }
    }
}
```

## Payload Compression

```csharp
var publishOptions = new PublishOptions
{
    Compression = CompressionAlgorithm.Gzip,
    CompressionThresholdBytes = 10 * 1024,
};
```

Compressed messages include `dispatch-compression` and `dispatch-body-encoding=base64` attributes; the SQS consumer automatically decodes them.
Supported compression algorithms for SQS payloads are Gzip, Deflate, and Brotli. Snappy is not supported.

## LocalStack Development

```csharp
services.AddAwsMessageBus(options =>
{
    options.UseLocalStack = true;
    options.ServiceUrl = new Uri("http://localhost:4566");
    options.Region = "us-east-1";
    options.EnableSqs = true;
});
```

## AWS Glue Schema Registry

Production schema registry integration for message validation and evolution.

### Quick Start

```csharp
services.AddAwsGlueSchemaRegistry(options =>
{
    options.RegistryName = "my-registry";
    options.Region = RegionEndpoint.USEast1;
    options.DefaultCompatibility = AwsGlueCompatibilityMode.Backward;
});
```

### Configuration Options

```csharp
services.AddAwsGlueSchemaRegistry(options =>
{
    // Registry configuration
    options.RegistryName = "my-registry";
    options.Region = RegionEndpoint.USEast1;

    // Schema format (Avro, JSON, Protobuf)
    options.DataFormat = AwsGlueDataFormat.Json;

    // Compatibility mode for schema evolution
    options.DefaultCompatibility = AwsGlueCompatibilityMode.Backward;

    // Auto-register schemas on first use
    options.AutoRegisterSchemas = true;

    // Caching (reduces API calls)
    options.CacheTtl = TimeSpan.FromHours(1);
    options.MaxCachedSchemas = 1000;

    // Retry configuration
    options.MaxRetries = 3;
    options.RetryBaseDelay = TimeSpan.FromMilliseconds(100);
    options.RequestTimeout = TimeSpan.FromSeconds(30);
});
```

### Via Transport Builder

```csharp
services.AddAwsSqsTransport(sqs =>
{
    sqs.UseRegion("us-east-1")
       .UseSchemaRegistry(registry =>
       {
           registry.RegistryName = "my-registry";
           registry.DefaultCompatibility = AwsGlueCompatibilityMode.Backward;
           registry.AutoRegisterSchemas = true;
       });
});
```

### Compatibility Modes

| Mode | Description |
|------|-------------|
| `Disabled` | Schema validation is disabled |
| `None` | No compatibility checking |
| `Backward` | New schema can read data from previous version |
| `BackwardAll` | New schema can read data from all previous versions |
| `Forward` | Previous schema can read data from new version |
| `ForwardAll` | All previous schemas can read data from new version |
| `Full` | Both backward and forward compatible |
| `FullAll` | Both backward and forward compatible with all versions |

### Data Formats

```csharp
// JSON Schema (default)
options.DataFormat = AwsGlueDataFormat.Json;

// Apache Avro
options.DataFormat = AwsGlueDataFormat.Avro;

// Protocol Buffers
options.DataFormat = AwsGlueDataFormat.Protobuf;
```

### Schema Operations

The AWS Glue Schema Registry client implements `IAwsSchemaRegistry`:

```csharp
public interface IAwsSchemaRegistry
{
    // Register a schema version
    Task<string> RegisterSchemaAsync<T>(string schema, int version);

    // Get schema by version ID
    Task<SchemaInfo?> GetSchemaAsync(string schemaId);

    // Get latest schema version for a type
    Task<SchemaInfo?> GetLatestSchemaAsync<T>();

    // Validate compatibility before registration
    Task<bool> ValidateCompatibilityAsync(string schemaId, string newSchema, int newVersion);
}
```

### IAM Permissions Required

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "glue:GetRegistry",
                "glue:GetSchema",
                "glue:GetSchemaVersion",
                "glue:RegisterSchemaVersion",
                "glue:CreateSchema",
                "glue:CheckSchemaVersionValidity"
            ],
            "Resource": "*"
        }
    ]
}
```

---

## Health Checks

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks();
```

## Observability

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch.Observability");
        tracing.AddAWSInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        // CloudWatch metrics also available
    });
```

## Lambda Integration

```csharp
public class OrderFunction
{
    private readonly IDispatcher _dispatcher;

    [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
    public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            var action = JsonSerializer.Deserialize<CreateOrderAction>(record.Body);
            await _dispatcher.DispatchAsync(action, context.CancellationToken);
        }
    }
}
```

## Production Checklist

- [ ] Use IAM roles (not access keys)
- [ ] Enable long polling (20 seconds)
- [ ] Configure dead letter queues
- [ ] Use FIFO queues for ordering requirements
- [ ] Enable server-side encryption
- [ ] Set appropriate visibility timeout
- [ ] Configure CloudWatch alarms
- [ ] Use VPC endpoints for private access

## Comparison: Standard vs FIFO

| Feature | Standard | FIFO |
|---------|----------|------|
| Throughput | Unlimited | 3,000 msg/sec (batch), 300 msg/sec (individual) |
| Ordering | Best-effort | Guaranteed |
| Delivery | At-least-once | Exactly-once |
| Deduplication | Manual | Built-in (5-minute window) |
| Pricing | Lower | Higher |

## Next Steps

- [Google Pub/Sub](google-pubsub.md) — For GCP-native messaging
- [Multi-Transport Routing](multi-transport.md) — Combine AWS SQS with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) — Compare AWS SQS against other transports
- [AWS Lambda Deployment](../deployment/aws-lambda.md) — Run Dispatch handlers in AWS Lambda with SQS triggers
- [Dead Letter Handling](../patterns/dead-letter.md) — Strategies for processing failed messages from DLQ
- [AWS CloudWatch Integration](../observability/aws-cloudwatch.md) — Configure AWS-native monitoring for Dispatch
