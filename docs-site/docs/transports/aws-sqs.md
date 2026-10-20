---
sidebar_position: 6
title: AWS SQS Transport
description: AWS SQS and SNS transport for AWS-native cloud messaging
---

# AWS SQS Transport

AWS Simple Queue Service (SQS) transport with optional SNS integration for AWS-native messaging.

## Before You Start

- **.NET 10.0**
- An AWS account with SQS access
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AwsSqs
```

:::tip One-Line Setup with Metapackage

For the fastest setup, use the **`Excalibur.Dispatch.Aws`** experience metapackage. It bundles the AWS SQS transport with Polly resilience and OpenTelemetry observability in a single call:

```bash
dotnet add package Excalibur.Dispatch.Aws
```

```csharp
services.AddDispatchAws(sqs =>
{
    sqs.UseRegion("us-west-2")
       .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)));
});
```

`AddDispatchAws` calls `AddDispatch` internally and configures `UseAwsSqs`, `UseResilience`, and `UseObservability`. Pass an optional second parameter (`Action<IDispatchBuilder>`) for additional pipeline configuration. See [Package Guide](../package-guide.md#experience-metapackages) for details.
:::

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
       .ConfigureCloudEvents(ce =>
       {
           ce.UseFifoFeatures = true;
           ce.DefaultMessageGroupId = "orders";
       })
       .MapQueue<OrderCreated>("https://sqs.us-east-1.amazonaws.com/123456789012/orders");
});
```

### Queue Provisioning

By default the transport assumes queues, dead-letter redrive policies, and SNS subscriptions are already provisioned (for example by infrastructure-as-code). You can opt in to having the transport apply the configured dead-letter redrive policy to the source queue and create the configured SNS-to-SQS subscriptions (with their filter policies) at startup:

```csharp
services.AddAwsSqsTransport("orders", sqs =>
{
    sqs.UseRegion("us-east-1")
       .ConfigureProvisioning(provisioning =>
       {
           provisioning.Enabled = true;                       // opt in
           provisioning.ApplyDeadLetterRedrivePolicy = true;  // set redrive policy on the source queue
           provisioning.CreateSnsSubscriptions = true;        // create SNS->SQS subscriptions + filter policies
           provisioning.FailOpen = true;                      // log and continue if provisioning fails
       });
});
```

Provisioning is **opt-in** because a messaging framework must not mutate cloud infrastructure unless the operator explicitly asks it to. When `FailOpen` is `true`, a missing IAM permission or transient error is logged and start-up continues rather than crashing the host.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Applies redrive policies and creates SNS subscriptions at startup. |
| `FailOpen` | `bool` | `true` | Logs and continues when provisioning fails (for example, missing IAM permissions) instead of surfacing the failure. |
| `ApplyDeadLetterRedrivePolicy` | `bool` | `true` | Sets the dead-letter redrive policy on the source queue when a dead-letter queue is configured. |
| `CreateSnsSubscriptions` | `bool` | `true` | Creates the configured SNS-to-SQS subscriptions and applies their filter policies. |

### Visibility Heartbeat

SQS makes a received message visible again (redelivered) once its visibility timeout elapses, even while the message is still being processed. A long-running handler can therefore be redelivered the same message before it finishes. When the visibility heartbeat is enabled, the subscriber periodically calls `ChangeMessageVisibility` for the in-flight message, extending its visibility window for as long as the handler runs (up to `MaxExtension`):

```csharp
services.AddAwsSqsTransport("orders", sqs =>
{
    sqs.UseRegion("us-east-1")
       .ConfigureVisibilityHeartbeat(heartbeat =>
       {
           heartbeat.Enabled = true;
           heartbeat.Interval = TimeSpan.FromSeconds(30);          // how often to extend
           heartbeat.VisibilityTimeout = TimeSpan.FromSeconds(60); // window requested per extension
           heartbeat.MaxExtension = TimeSpan.FromMinutes(10);      // safety bound per message
       });
});
```

The heartbeat is **opt-in** so the default consumer behaviour is unchanged. Choose an `Interval` shorter than `VisibilityTimeout` so the extension is applied before the current window expires. Once the `MaxExtension` budget is exhausted the subscriber stops extending visibility and the message is allowed to become visible again, providing a safety bound against a stuck handler. Handlers should still be idempotent because AWS delivery remains at-least-once.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Extends the visibility timeout of in-flight messages for long-running handlers. |
| `Interval` | `TimeSpan` | 30 seconds | Interval between visibility extensions. Keep shorter than `VisibilityTimeout`. |
| `VisibilityTimeout` | `TimeSpan` | 60 seconds | Visibility window requested on each extension (SQS allows 0 seconds to 12 hours). |
| `MaxExtension` | `TimeSpan` | 10 minutes | Maximum cumulative time the heartbeat keeps a single message in-flight. |

### Send Retry Budget and Deadline

Configure the foreground send budget through the transport builder:

```csharp
sqs.UseMaxRetryAttempts(5)                       // five retries after the initial foreground send
   .UseRequestTimeout(TimeSpan.FromSeconds(30)); // total send deadline, including retry delays
```

The defaults are three retries and a thirty-second deadline. Zero retries allows one
foreground send. The framework creates a separate SQS send client with both
`MaxErrorRetry` and `MaxStaleConnectionRetries` set to zero, then uses a cached Polly
policy with backoff and Polly's built-in jitter for established transient foreground
failures. A registered message failure classifier can veto those retries. Unknown SDK
errors propagate without a local retry. The receive client's SDK retry and timeout
settings remain separately configured. The deadline wraps the whole send execution,
including backoff; caller cancellation is not treated as a retryable failure.
Cancellation and the Polly timeout are cooperative: an injected client must honor
the cancellation token. They cannot terminate a custom implementation that ignores it.

When the dispatch pipeline or persisted outbox already owns retries for the exact
message and context, the SQS bus performs one SDK send operation per outer attempt.
It retains the send deadline and does not add foreground retries. The outer owner's
configuration determines that operation's overall attempt ceiling. Nested dispatches
with another message or context retain their own retry policy.

Custom clients registered with `AddKeyedSingleton<IAmazonSQS>(transportName, ...)`
override that named transport's client. Registering an unkeyed client does not override
a named client. For sends inside a framework-owned retry budget, custom clients must
expose configuration with **both SDK retry settings at zero**. Missing or unreadable
configuration, or either retry setting being nonzero, produces a permanent
`ConfigurationException` before a request is sent. The framework does not mutate or
dispose a borrowed client itself. A consumer-owned singleton instance must be disposed
by its owner; DI-created clients retain their DI lifetime. Named registrations must
be resolved by their transport key; returning the same disposable from an additional
unkeyed DI factory would capture it for disposal a second time.

Named transports register their SDK client by key rather than creating an unkeyed
disposable alias. Resolve it with
`provider.GetRequiredKeyedService<IAmazonSQS>(transportName)`; publish through the
configured remote bus routing. For a separate `AddSqsChannelAdapter` entry point,
register its unkeyed SDK client explicitly or use `AddAwsMessageBus`. The complete
SQS channel infrastructure supplies its own client. An unkeyed client registration
continues to have its own lifetime and does not override a named transport.

Outside an owned retry scope, an injected client with SDK retries enabled keeps those
SDK retries and receives no additional Polly retry layer. Its SDK operation still has
the total send deadline. Arbitrary injected implementations can perform hidden work
that their configuration does not reveal; these settings cannot certify their physical
request count. Transport send attempts also do not prove broker acceptance or
exactly-once effects after an ambiguous response. Keep handlers idempotent.

AWS documents that stale-connection retries have a separate budget from
`MaxErrorRetry`, which is why disabling only one setting is insufficient. See the
[AWS SDK client configuration reference](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/Runtime/TClientConfig.html)
and [Polly timeout semantics](https://www.pollydocs.org/strategies/timeout.html).

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

### SNS Retry Ownership and Named Clients

Each named SNS transport has its own management client, message bus and topic-encryption
configuration. Framework-created sends use a separate client with both SDK retry settings at
zero. The cached Polly pipeline applies the named `MaxRetryAttempts`, `RequestTimeout` and
`RetryPolicy` settings to foreground sends; connection retry and timeout settings remain on the
management client. Resolving a bus does not overwrite the configured foreground options.

An existing framework retry owner controls publication of the same message and context. SNS
propagates the failure without adding a foreground retry loop. Custom clients used by that owner
must expose readable configuration with `MaxErrorRetry` and `MaxStaleConnectionRetries` both zero;
otherwise publication refuses before making an SDK request. Outside that scope, a borrowed client
with SDK retries enabled retains its SDK policy and receives no additional Polly retry layer.
The send deadline includes retries and delays, and caller cancellation is not retried.

Framework registrations own their private send clients. An externally supplied SDK client remains
borrowed and is not disposed by the message bus. Resolve a custom named SNS client with its transport
name so that publication and topic management use the intended endpoint:
`provider.GetRequiredKeyedService<IAmazonSimpleNotificationService>(transportName)`.
Named transport registration does not create an unkeyed disposable alias for that client.

Framework retry counters describe logical retries, excluding the initial attempt. To observe SDK
operation attempts, configure AWS SDK telemetry before creating the clients and collect
`client.call.attempts`. These counters describe different layers; do not sum them as if each represented
an additional network request. See [AWS SDK telemetry configuration](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/observability.html)
and [SDK metric definitions](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/observability-metrics.html).

## EventBridge Ingestion Acknowledgements

The EventBridge message bus checks every `PutEvents` result. HTTP 200 alone is not
success: the single result must contain an event ID, no error fields, and a zero
failed-entry count. Rejected, incomplete or contradictory responses do not complete
a persisted outbox message.

`InternalFailure` and `ThrottlingException` propagate to the existing retry owner.
The bus adds no foreground retry loop. `MalformedDetail` is a poison failure;
known authorization, account and argument failures are permanent configuration
failures. Unknown errors and malformed acknowledgements leave ingestion uncertain.
These outcomes surface `DispatchDeliveryUncertainException`, classified as
`MessageFailureKind.Uncertain`. The default retry policy retries them within its configured
budget. Explicit retry exclusions still apply. Uncertainty means that acceptance was not
established; replay can therefore produce duplicates, so consumers must be idempotent.
Mixed fanout results preserve uncertainty even when another destination has a permanent failure.

Configure a readable dead-letter queue for recoverable retention. When that queue is unavailable,
the polling outbox records the observed attempt count and pending disposition before reclaiming
the row. Recovery retries disposition without sending the exhausted message again. A failed or
ambiguous bookkeeping write can still permit redelivery; this is an at-least-once contract.
SDK connection retries remain the client's responsibility; these entry-result checks do not
certify its physical request budget.

Native action, event and document detail is a JSON object containing the serialized
bytes, including their format marker:

```json
{"payload":"BASE64_SERIALIZED_BYTES"}
```

Target consumers decode `payload` from base64 before using the matching pluggable
deserializer. This repairs the previous bare-base64 detail, which was not a valid
EventBridge JSON object. CloudEvents retain their separate configured encoding.

An event ID acknowledges API ingestion; it does not prove downstream rule matching
or deduplication. AWS documents that a nonexistent bus can return HTTP 200 while
dropping an event without reporting failed entries. Provision the intended bus and
rules, and keep consumers idempotent: a lost response after ingestion can cause a
durable retry to publish again. See the [PutEvents response contract](https://docs.aws.amazon.com/eventbridge/latest/APIReference/API_PutEvents.html)
and [AWS failure handling guidance](https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-putevents.html).

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
           ce.CompressionThreshold = 64 * 1024; // 64KB
           ce.EnableDoDCompliance = true;
       });
});
```

### Standalone CloudEvents
:::note Trimming and Native AOT
The CloudEvents mapper bundled with this transport serializes the message payload with
reflection-based JSON, so these registrations carry `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. A host that trims or publishes ahead of time gets a warning at the
call. To compose without the requirement, register your own `ICloudEventEncoder<TOutbound>`
backed by a source-generated serializer.
:::

```csharp
services.AddCloudEventsForSqs(options =>
{
    options.UseFifoFeatures = true;
    options.DefaultMessageGroupId = "orders";
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

## Binary Payloads

An SQS message body is text: the service accepts the XML 1.0 character set and rejects anything else with
`InvalidMessageContents`. A message body that is not reversible text — one that is not well-formed UTF-8, or
whose text contains a character SQS does not accept — is therefore Base64-encoded on the wire and tagged with
`dispatch-body-encoding=base64`, which the SQS receiver and subscriber decode automatically. Round-tripping a
message through SQS returns the bytes the producer supplied, whatever those bytes are, so binary serializers
such as MessagePack and Protobuf are safe to use.

Bodies that are already reversible text are sent unchanged and carry no encoding attribute, so a queue read
with any other tool shows the same text it always has. Note that the attribute can appear for either reason:
compression implies it, and so does a binary body, independently.

Base64 expands a payload by roughly one third. A payload close to the 256 KiB SQS limit can therefore exceed
it once encoded, and SQS rejects it; the send reports a failure rather than delivering altered data.

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
        tracing.AddSource("Excalibur.Dispatch");
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
| Throughput | Very high; service quotas and configuration apply | Without high-throughput mode: 300 transactions/sec per partition per API action, or 3,000 messages/sec with batches of 10; high-throughput quotas vary by Region |
| Ordering | Best-effort | Guaranteed |
| Delivery | At-least-once | Exactly-once |
| Deduplication | Manual | Built-in (5-minute window) |
| Pricing | Lower | Higher |

These are AWS service characteristics, not measured framework throughput. See [Amazon SQS message quotas](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/quotas-messages.html) for mode, batching, and regional limits.

## Next Steps

- [Google Pub/Sub](google-pubsub.md) -- For GCP-native messaging
- [Multi-Transport Routing](multi-transport.md) -- Combine AWS SQS with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) -- Compare AWS SQS against other transports
- [AWS Lambda Deployment](../deployment/aws-lambda.md) -- Run Dispatch handlers in AWS Lambda with SQS triggers
- [Dead Letter Handling](../patterns/dead-letter.md) -- Strategies for processing failed messages from DLQ
- [AWS CloudWatch Integration](../observability/aws-cloudwatch.md) -- Configure AWS-native monitoring for Dispatch
