# Excalibur.Dispatch.Transport.AwsSqs

AWS messaging transport implementation for the Excalibur framework, providing integration with Amazon SQS, SNS, and EventBridge services.

## Overview

This package provides AWS messaging integration for Excalibur.Dispatch, enabling:

- **Amazon SQS**: Standard and FIFO queues with long polling and batching
- **Amazon SNS**: Pub/sub messaging with topic subscriptions
- **Amazon EventBridge**: Event-driven architectures with event buses and rules
- **CloudEvents Support**: Standards-compliant event formatting
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
services.AddAwsSqs(options =>
{
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
    options.Region = "us-east-1";
});
```

#### Using Explicit Credentials

```csharp
services.AddAwsSqs(options =>
{
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
    options.Region = "us-east-1";
    options.Credentials = new BasicAWSCredentials("accessKey", "secretKey");
});
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
services.AddAwsSqs(options =>
{
    options.QueueUrl = new Uri(Environment.GetEnvironmentVariable("SQS_QUEUE_URL")!);
    options.Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
});
```

#### LocalStack for Development

Use LocalStack for local development without AWS credentials:

```csharp
services.AddAwsSqs(options =>
{
    options.UseLocalStack = true;
    options.LocalStackUrl = new Uri("http://localhost:4566");
    options.QueueUrl = new Uri("http://localhost:4566/000000000000/my-queue");
});
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
services.AddAwsSqs(options =>
{
    options.Credentials = new AssumeRoleAWSCredentials(
        new BasicAWSCredentials("accessKey", "secretKey"),
        "arn:aws:iam::123456789:role/my-role",
        "session-name");
    options.Region = "us-east-1";
});
```

#### AWS SSO / Identity Center

Use AWS CLI profiles with SSO:

```csharp
services.AddAwsSqs(options =>
{
    options.Credentials = new ProfileAWSCredentials("my-sso-profile");
    options.Region = "us-east-1";
});
```

### Message Configuration

#### Standard Queue Settings

```csharp
services.AddAwsSqs(options =>
{
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");

    // Polling configuration
    options.MaxNumberOfMessages = 10;    // Max messages per receive (1-10)
    options.WaitTimeSeconds = TimeSpan.FromSeconds(20);   // Long polling wait time (0-20 seconds)
    options.VisibilityTimeout = TimeSpan.FromSeconds(30); // Message lock timeout

    // Message retention
    options.MessageRetentionPeriod = 345600;  // 4 days (in seconds)
});
```

#### FIFO Queue Settings

```csharp
services.AddAwsSqs(options =>
{
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue.fifo");

    // FIFO-specific options
    options.UseFifoQueue = true;
    options.ContentBasedDeduplication = true;  // Auto-generate deduplication ID from content
});
```

#### Batch Configuration

```csharp
services.AddAwsSqs(options =>
{
    options.BatchConfig = new BatchConfiguration
    {
        MaxBatchSize = 10,           // Messages per batch (max 10)
        MaxBatchWaitTime = TimeSpan.FromMilliseconds(100)
    };
});
```

#### Long Polling Configuration

```csharp
services.AddAwsSqs(options =>
{
    options.LongPollingConfig = new LongPollingConfiguration
    {
        WaitTimeSeconds = 20,        // Long polling duration
        MaxEmptyReceives = 5,        // Max empty receives before backing off
        BackoffMultiplier = 1.5      // Backoff multiplier
    };
});
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
services.AddAwsSqs(options =>
{
    options.MaxRetries = 3;                              // AWS SDK retry count
    options.RequestTimeout = TimeSpan.FromSeconds(30);   // Request timeout
    options.ValidateOnStartup = true;                    // Validate queue exists on startup
});
```

#### Dead Letter Queue Configuration

```csharp
services.Configure<DlqOptions>(options =>
{
    options.DeadLetterQueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-dlq");
    options.MaxRetries = 3;                              // Max retries before DLQ
    options.RetryDelay = TimeSpan.FromMinutes(5);        // Delay between retries
    options.UseExponentialBackoff = true;                // Exponential backoff
    options.MaxMessageAge = TimeSpan.FromDays(14);       // Max message age to process

    // Archive options
    options.ArchiveFailedMessages = true;
    options.ArchiveLocation = "s3://my-bucket/dlq-archive/";

    // Automatic redrive
    options.EnableAutomaticRedrive = true;
    options.AutomaticRedriveInterval = TimeSpan.FromHours(1);
});
```

### Encryption

#### KMS Server-Side Encryption

```csharp
services.AddAwsSqs(options =>
{
    options.EnableEncryption = true;
    options.KmsMasterKeyId = "alias/my-key";              // KMS key alias or ARN
    options.KmsDataKeyReusePeriodSeconds = 300;           // Data key reuse period (60-86400)
});
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

### Registration

```csharp
services.AddHealthChecks()
    .AddCheck<SqsHealthCheck>("sqs", tags: new[] { "ready", "messaging" });
```

### Configuration

```csharp
services.Configure<SqsHealthCheckOptions>(options =>
{
    options.Timeout = TimeSpan.FromSeconds(5);
    options.QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/my-queue";
});
```

### Custom Health Check Implementation

```csharp
public class SqsHealthCheck : IHealthCheck
{
    private readonly IAmazonSQS _sqsClient;
    private readonly AwsSqsOptions _options;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _sqsClient.GetQueueAttributesAsync(
                _options.QueueUrl?.ToString(),
                new List<string> { "ApproximateNumberOfMessages" },
                cancellationToken);

            var messageCount = int.Parse(
                response.Attributes["ApproximateNumberOfMessages"]);

            return messageCount > 10000
                ? HealthCheckResult.Degraded($"Queue depth: {messageCount}")
                : HealthCheckResult.Healthy($"Queue depth: {messageCount}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQS unreachable", ex);
        }
    }
}
```

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
services.AddAwsSqs(options =>
{
    // High-throughput configuration
    options.MaxNumberOfMessages = 10;         // Max batch size
    options.WaitTimeSeconds = TimeSpan.FromSeconds(20);   // Long polling (reduces API calls)
    options.VisibilityTimeout = TimeSpan.FromMinutes(5);  // 5 minutes for slow processing

    options.BatchConfig = new BatchConfiguration
    {
        MaxBatchSize = 10,
        MaxBatchWaitTime = TimeSpan.FromMilliseconds(50)  // Faster batching
    };
});
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
services.AddAwsSns(options =>
{
    options.TopicArn = "arn:aws:sns:us-east-1:123456789:my-topic";
    options.Region = "us-east-1";
});
```

### Fanout Pattern (SNS to Multiple SQS)

```csharp
// Publisher uses SNS
services.AddAwsSns(options =>
{
    options.TopicArn = "arn:aws:sns:us-east-1:123456789:orders-topic";
});

// Multiple consumers subscribe SQS queues to the topic
// Configure in AWS Console or via CloudFormation
```

## EventBridge Integration

### Configuration

```csharp
services.AddAwsEventBridge(options =>
{
    options.EventBusName = "my-event-bus";
    options.Region = "us-east-1";
    options.DefaultSource = "my-application";
    options.DefaultDetailType = "dispatch.event";
    options.EnableArchiving = true;
    options.ArchiveName = "my-event-archive";
    options.ArchiveRetentionDays = 7;
});
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
services.AddAwsSqs(options =>
{
    // Connection
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
    options.Region = "us-east-1";
    options.ServiceUrl = null;  // Custom endpoint (LocalStack, etc.)
    options.UseLocalStack = false;
    options.LocalStackUrl = new Uri("http://localhost:4566");

    // Authentication
    options.Credentials = null;  // Uses default credential chain

    // Queue type
    options.UseFifoQueue = false;
    options.ContentBasedDeduplication = false;

    // Polling
    options.MaxNumberOfMessages = 10;
    options.WaitTimeSeconds = TimeSpan.FromSeconds(20);
    options.VisibilityTimeout = TimeSpan.FromSeconds(30);

    // Message settings
    options.MessageRetentionPeriod = 345600;  // 4 days

    // Reliability
    options.MaxRetries = 3;
    options.RequestTimeout = TimeSpan.FromSeconds(30);
    options.ValidateOnStartup = true;
    options.EnableDeduplication = false;

    // Encryption
    options.EnableEncryption = false;
    options.KmsMasterKeyId = null;
    options.KmsDataKeyReusePeriodSeconds = 300;
});

// Dead Letter Queue
services.Configure<DlqOptions>(options =>
{
    options.DeadLetterQueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-dlq");
    options.MaxRetries = 3;
    options.RetryDelay = TimeSpan.FromMinutes(5);
    options.UseExponentialBackoff = true;
    options.MaxMessageAge = TimeSpan.FromDays(14);
    options.ArchiveFailedMessages = true;
    options.ArchiveLocation = "s3://bucket/archive/";
    options.BatchSize = 10;
    options.EnableAutomaticRedrive = false;
    options.AutomaticRedriveInterval = TimeSpan.FromHours(1);
});
```

## See Also

- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)
- [AWS SNS Documentation](https://docs.aws.amazon.com/sns/)
- [AWS EventBridge Documentation](https://docs.aws.amazon.com/eventbridge/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
