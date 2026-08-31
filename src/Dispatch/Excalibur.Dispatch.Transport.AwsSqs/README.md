# Excalibur.Dispatch.Transport.AwsSqs

AWS messaging transport implementation for the Excalibur framework, providing integration with Amazon SQS, SNS, and EventBridge services.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.Aws` | Starter | + Resilience (Polly) + Observability |

> **Tip:** If you are getting started, install `Excalibur.Dispatch.Aws` instead of this package directly. It includes production-ready defaults.

## Overview

This package provides AWS messaging integration for Excalibur.Dispatch, enabling:

- **Amazon SQS**: Standard and FIFO queues with long polling and batching
- **Amazon SNS**: Pub/sub messaging with topic subscriptions
- **Amazon EventBridge**: Event-driven architectures with event buses and rules
- **CloudEvents Support**: Standards-compliant event formatting. Registering the bundled mapper is annotated for trimming and ahead-of-time builds (it serializes payloads with reflection-based JSON); supply your own `ICloudEventMapper<TTransportMessage>` over a source-generated serializer to avoid the requirement.
- **KMS Encryption**: Server-side encryption with AWS Key Management Service
- **LocalStack Support**: Local development and testing without AWS account

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AwsSqs
```

## Configuration

### Connection Options

#### Using Default Credentials

AWS SDK automatically discovers credentials from environment, IAM roles, or credential files:

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"));
```

#### Using Explicit Credentials

```csharp
// The transport builder does not take credentials -- it resolves IAmazonSQS from DI, so
// credentials are configured on the AWS SDK client in the usual way.
services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
    new BasicAWSCredentials("accessKey", "secretKey"),
    RegionEndpoint.USEast1));

services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"));
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
AWS_ACCESS_KEY_ID=your-access-key
AWS_SECRET_ACCESS_KEY=your-secret-key
AWS_REGION=us-east-1
SQS_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/123456789/my-queue
```

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1")
    .MapQueue<OrderPlaced>(Environment.GetEnvironmentVariable("SQS_QUEUE_URL")!));
```

#### LocalStack for Development

Use LocalStack for local development without AWS credentials:

```csharp
// Point the AWS SDK client at LocalStack; the transport uses whatever IAmazonSQS is registered.
services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
    new AmazonSQSConfig { ServiceURL = "http://localhost:4566" }));

services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("http://localhost:4566/000000000000/my-queue"));
```

### Authentication

#### IAM Roles (Recommended for Production)

For EC2, ECS, Lambda, or EKS deployments, use IAM roles:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes",
        "sqs:ChangeMessageVisibility"
      ],
      "Resource": "arn:aws:sqs:us-east-1:123456789:my-queue"
    }
  ]
}
```

#### Assume Role

```csharp
// Role assumption belongs to the AWS SDK client; the transport consumes whatever
// IAmazonSQS is registered.
services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
    new AssumeRoleAWSCredentials(
        new BasicAWSCredentials("accessKey", "secretKey"),
        "arn:aws:iam::123456789:role/my-role",
        "session-name"),
    RegionEndpoint.USEast1));

services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"));
```

#### AWS SSO / Identity Center

Use AWS CLI profiles with SSO:

```csharp
services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
    new ProfileAWSCredentials("my-sso-profile"),
    RegionEndpoint.USEast1));

services.AddAwsSqsTransport("orders", sqs => sqs
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"));
```

### Message Configuration

#### Standard Queue Settings

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue")
    .ConfigureQueue(queue => queue
        .ReceiveWaitTimeSeconds(20)                              // long polling (0-20)
        .VisibilityTimeout(TimeSpan.FromSeconds(30))             // message lock timeout
        .MessageRetentionPeriod(TimeSpan.FromDays(4))));
```

#### FIFO Queue Settings

```csharp
// A .fifo queue URL selects FIFO behaviour; ConfigureFifo supplies the FIFO settings.
services.AddAwsSqsTransport("orders", sqs => sqs
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue.fifo")
    .ConfigureFifo(fifo => fifo
        .ContentBasedDeduplication(true)                         // derive the dedup ID from the body
        .MessageGroupIdSelector<OrderPlaced>(order => order.CustomerId)));
```

#### Batching

Sends are batched automatically: the sender chunks an outgoing set into `SendMessageBatch` calls at
the SQS ceiling of ten entries, and receives pull up to ten messages per `ReceiveMessage` call.
There is no batch-size knob because there is no value below the ceiling that improves anything.

#### Long Polling Configuration

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue")
    .ConfigureQueue(queue => queue.ReceiveWaitTimeSeconds(20)));  // 20s = maximum long poll
```

#### Payload Compression

Compress large payloads when publishing to stay within the 256 KB SQS limit:

```csharp
var publishOptions = new PublishOptions
{
    Compression = CompressionAlgorithm.Gzip,
    CompressionThresholdBytes = 10 * 1024, // 10 KB
};

var publisher = serviceProvider.GetRequiredService<ICloudMessagePublisher>();
await publisher.PublishAsync(new CloudMessage
{
    Body = Encoding.UTF8.GetBytes("payload"),
}, CancellationToken.None);
```

Compressed messages include `dispatch-compression` and `dispatch-body-encoding=base64` attributes; the SQS consumer automatically decodes them.
Supported compression algorithms for SQS payloads are Gzip, Deflate, and Brotli. Snappy is not supported.

### Retry Policies

#### Retry Configuration

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    .UseMaxRetryAttempts(3)                              // AWS SDK retry count
    .UseRequestTimeout(TimeSpan.FromSeconds(30)));
```

#### Dead Letter Queue Configuration

```csharp
// The redrive policy is an SQS queue attribute: name the DLQ by ARN and the receive count
// after which SQS moves the message.
services.AddAwsSqsTransport("orders", sqs => sqs
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue")
    .ConfigureQueue(queue => queue
        .DeadLetterQueue(dlq => dlq
            .QueueArn("arn:aws:sqs:us-east-1:123456789:my-dlq")
            .MaxReceiveCount(3))));

// Have the transport apply that redrive policy to the queue at startup:
services.AddAwsSqsTransport("orders", sqs => sqs
    .ConfigureProvisioning(p =>
    {
        p.Enabled = true;
        p.ApplyDeadLetterRedrivePolicy = true;
    }));
```

### Encryption

#### KMS Server-Side Encryption

```csharp
// SQS server-side encryption is a queue attribute, not a transport setting: enable SSE-KMS
// on the queue itself (console, CloudFormation, or Terraform). Messages are then encrypted
// at rest transparently, and the transport needs no configuration for it.
//
// The publishing identity needs kms:GenerateDataKey and kms:Decrypt on the key -- see the
// IAM policy below.
```

#### Required IAM Permissions for KMS

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "kms:GenerateDataKey",
        "kms:Decrypt"
      ],
      "Resource": "arn:aws:kms:us-east-1:123456789:key/my-key-id"
    }
  ]
}
```

## Health Checks

The transport adapter implements `ITransportHealthChecker`. Register the transport-agnostic health
check from `Excalibur.Dispatch`, which resolves every registered transport checker:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks(
        name: "transports",
        tags: new[] { "ready", "messaging" });
```

For finer control, `AddTransportHealthChecks` also accepts an options delegate or an
`IConfiguration` section.

You do not need to author a health check yourself.

## Production Considerations

### Scaling

#### Horizontal Scaling

- Use **multiple consumers** reading from the same queue
- Adjust `VisibilityTimeout` based on message processing time
- Use Lambda with SQS triggers for automatic scaling

#### FIFO Queue Considerations

- FIFO queues have **300 TPS limit** per message group
- Use multiple message groups for higher throughput
- Consider standard queues if ordering is not critical

### Performance Tuning

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    // High-throughput configuration
    .ConfigureQueue(queue => queue
        .ReceiveWaitTimeSeconds(20)                       // long polling (reduces API calls)
        .VisibilityTimeout(TimeSpan.FromMinutes(5))));    // 5 minutes for slow processing
```

### Monitoring and Alerting

Key CloudWatch metrics to monitor:

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `ApproximateNumberOfMessagesVisible` | Messages waiting | > 10,000 |
| `ApproximateNumberOfMessagesNotVisible` | In-flight messages | > VisibilityTimeout |
| `ApproximateAgeOfOldestMessage` | Message age | > retention period / 2 |
| `NumberOfMessagesSent` | Send rate | Baseline deviation |
| `NumberOfMessagesDeleted` | Process rate | < send rate (backlog growing) |

### Cost Optimization

1. **Use long polling** (`WaitTimeSeconds = TimeSpan.FromSeconds(20)`) to reduce API calls
2. **Batch operations** for sends and deletes
3. **Use FIFO queues only when needed** (higher cost)
4. **Set appropriate retention periods** to avoid storage costs

### Security Best Practices

1. **Use IAM roles** instead of access keys in production
2. **Enable KMS encryption** for sensitive data
3. **Use VPC endpoints** to keep traffic within AWS
4. **Apply least-privilege permissions** per queue
5. **Enable CloudTrail** for audit logging

## SNS Integration

### Configuration

```csharp
services.AddAwsSnsTransport("notifications", sns => sns
    .TopicArn("arn:aws:sns:us-east-1:123456789:my-topic")
    .Region("us-east-1"));
```

### Fanout Pattern (SNS to Multiple SQS)

```csharp
// Publisher uses SNS
services.AddAwsSnsTransport("notifications", sns => sns
    .TopicArn("arn:aws:sns:us-east-1:123456789:orders-topic"));

// Multiple consumers subscribe SQS queues to the topic
// Configure in AWS Console or via CloudFormation
```

## EventBridge Integration

### Configuration

```csharp
services.AddAwsEventBridgeTransport("events", bus => bus
    .EventBusName("my-event-bus")
    .Region("us-east-1")
    .DefaultSource("my-application")
    .DefaultDetailType("dispatch.event")
    .EnableArchiving(retentionDays: 7, archiveName: "my-event-archive"));
```

## Troubleshooting

### Common Issues

#### Access Denied

```
Amazon.SQS.AmazonSQSException: Access to the resource is denied.
```

**Solutions:**
- Verify IAM permissions include required SQS actions
- Check queue policy allows your principal
- Ensure KMS permissions if encryption is enabled
- Verify the correct AWS account/region

#### Queue Does Not Exist

```
Amazon.SQS.AmazonSQSException: The specified queue does not exist.
```

**Solutions:**
- Verify queue URL is correct
- Check queue exists in the correct region
- Ensure queue name matches (case-sensitive)
- For FIFO queues, include `.fifo` suffix

#### Message Not Deleted

Messages keep reappearing after processing.

**Solutions:**
- Ensure message is explicitly deleted after processing
- Increase `VisibilityTimeout` if processing takes longer
- Check for exceptions preventing deletion
- Verify delete permissions in IAM policy

#### Visibility Timeout Too Short

```
Amazon.SQS.AmazonSQSException: Message has expired
```

**Solutions:**
- Increase `VisibilityTimeout` to exceed processing time
- Use `ChangeMessageVisibility` for long-running tasks
- Consider breaking large tasks into smaller messages

### Logging Configuration

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Dispatch.Transport.AwsSqs": "Debug",
      "Amazon": "Warning",
      "Amazon.SQS": "Information"
    }
  }
}
```

### Debug Tips

1. **Enable AWS SDK logging**:
   ```csharp
   AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
   AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.OnError;
   ```

2. **Use AWS CLI to test**:
   ```bash
   aws sqs receive-message --queue-url https://sqs.us-east-1.amazonaws.com/123456789/my-queue
   ```

3. **Check CloudWatch Logs** for Lambda-based consumers

4. **Use X-Ray** for distributed tracing

5. **LocalStack logs** for local development issues

## Complete Configuration Reference

```csharp
services.AddAwsSqsTransport("orders", sqs => sqs
    // Connection
    .UseRegion("us-east-1")
    .MapQueue<OrderPlaced>("https://sqs.us-east-1.amazonaws.com/123456789/my-queue")
    .WithQueuePrefix("prod-")

    // Queue behaviour
    .ConfigureQueue(queue => queue
        .VisibilityTimeout(TimeSpan.FromSeconds(30))
        .MessageRetentionPeriod(TimeSpan.FromDays(4))
        .ReceiveWaitTimeSeconds(20)
        .DelaySeconds(0)
        .DeadLetterQueue(dlq => dlq
            .QueueArn("arn:aws:sqs:us-east-1:123456789:my-dlq")
            .MaxReceiveCount(3)))

    // FIFO queues only
    .ConfigureFifo(fifo => fifo
        .ContentBasedDeduplication(true)
        .MessageGroupIdSelector<OrderPlaced>(order => order.CustomerId))


    // Reliability
    .UseMaxRetryAttempts(3)
    .UseRequestTimeout(TimeSpan.FromSeconds(30))
    .UseMaxPayloadBytes(256 * 1024)
    .ConfigureVisibilityHeartbeat(heartbeat => heartbeat.Enabled = true)

    // Create/patch the queue and its redrive policy at startup
    .ConfigureProvisioning(provisioning =>
    {
        provisioning.Enabled = true;
        provisioning.ApplyDeadLetterRedrivePolicy = true;
        provisioning.FailOpen = true;
    }));
```

Credentials and custom endpoints are AWS SDK concerns: register the `IAmazonSQS` client you
want and the transport will use it.

## See Also

- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)
- [AWS SNS Documentation](https://docs.aws.amazon.com/sns/)
- [AWS EventBridge Documentation](https://docs.aws.amazon.com/eventbridge/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
