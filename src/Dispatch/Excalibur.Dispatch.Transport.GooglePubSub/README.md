# Excalibur.Dispatch.Transport.GooglePubSub

Google Cloud Pub/Sub transport implementation for the Excalibur framework, providing scalable, serverless messaging with exactly-once delivery, message ordering, and dead letter topic support.

## Overview

This package provides Google Cloud Pub/Sub integration for Excalibur.Dispatch, enabling:

- **Serverless Messaging**: Fully managed, auto-scaling message infrastructure
- **Exactly-Once Delivery**: Guaranteed delivery with deduplication
- **Message Ordering**: Ordering keys for sequential processing
- **Dead Letter Topics**: Automatic handling of failed messages
- **CloudEvents Support**: Standards-compliant structured event formatting
- **Cloud Monitoring**: Native Google Cloud observability integration
- **Emulator Support**: Local development without GCP account

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.GooglePubSub
```

## Configuration

### Connection Options

#### Basic Configuration

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.ProjectId = "your-gcp-project-id";
    options.MaxMessages = 100;
    options.AckDeadline = TimeSpan.FromSeconds(30);
});
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
GOOGLE__PROJECTID=your-gcp-project-id
GOOGLE__MAXMESSAGES=100
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json
```

```csharp
services.Configure<GoogleProviderOptions>(configuration.GetSection("Google"));
```

#### Local Development with Emulator

Use the Pub/Sub emulator for local development without GCP credentials:

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.ProjectId = "test-project";
    options.UseEmulator = true;
    options.EmulatorHost = "localhost:8085";
    options.ValidateOnStartup = false;  // Emulator may not support all validations
});
```

Start the emulator:

```bash
# Install the emulator
gcloud components install pubsub-emulator

# Start the emulator
gcloud beta emulators pubsub start --project=test-project

# Set environment variable
export PUBSUB_EMULATOR_HOST=localhost:8085
```

### Authentication

#### Application Default Credentials (Recommended)

For production on GCP, use Workload Identity or service account:

```bash
# Set credentials file path
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account.json"
```

```csharp
// Application Default Credentials are used automatically
services.Configure<GoogleProviderOptions>(options =>
{
    options.ProjectId = "your-gcp-project-id";
});
```

#### Service Account Key File

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.ProjectId = "your-gcp-project-id";
    // Credentials loaded from GOOGLE_APPLICATION_CREDENTIALS environment variable
});
```

#### Workload Identity (GKE)

For GKE workloads, configure Workload Identity:

```yaml
# Kubernetes service account annotation
apiVersion: v1
kind: ServiceAccount
metadata:
  annotations:
    iam.gke.io/gcp-service-account: your-sa@your-project.iam.gserviceaccount.com
```

Required IAM roles:
- `roles/pubsub.publisher` - For publishing messages
- `roles/pubsub.subscriber` - For consuming messages
- `roles/pubsub.admin` - For topic/subscription management (if auto-creating)

### Message Configuration

#### Provider Settings

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    // Project configuration
    options.ProjectId = "your-gcp-project-id";

    // Emulator settings
    options.UseEmulator = false;
    options.EmulatorHost = "localhost:8085";

    // Request handling
    options.RequestTimeout = TimeSpan.FromSeconds(60);
    options.ValidateOnStartup = true;

    // Batch settings
    options.MaxMessages = 100;

    // Acknowledgment
    options.AckDeadline = TimeSpan.FromSeconds(30);

    // Delivery semantics
    options.EnableExactlyOnceDelivery = false;
    options.EnableMessageOrdering = false;

    // Flow control
    options.FlowControl = new FlowControlSettings
    {
        MaxOutstandingMessages = 1000,
        MaxOutstandingBytes = 100_000_000,  // 100 MB
        LimitExceededBehavior = true
    };

    // Retry settings
    options.RetrySettings = new RetrySettings
    {
        InitialRetryDelay = TimeSpan.FromMilliseconds(100),
        RetryDelayMultiplier = 2.0,
        MaxRetryDelay = TimeSpan.FromSeconds(60),
        TotalTimeout = TimeSpan.FromMinutes(10)
    };
});
```

#### CloudEvents Configuration

```csharp
services.Configure<GooglePubSubCloudEventOptions>(options =>
{
    // Topic/Subscription settings
    options.ProjectId = "your-gcp-project-id";
    options.DefaultTopic = "cloud-events";
    options.DefaultSubscription = "cloud-events-subscription";

    // Message ordering
    options.UseOrderingKeys = true;

    // Message size (Pub/Sub supports up to 10MB)
    options.MaxMessageSizeBytes = 10 * 1024 * 1024;

    // Deduplication
    options.EnableDeduplication = true;

    // Compression
    options.EnableCompression = false;
    options.CompressionThreshold = 1024 * 1024;  // 1 MB

    // Delivery semantics
    options.UseExactlyOnceDelivery = false;
    options.AckDeadline = TimeSpan.FromMinutes(10);

    // Cloud Monitoring integration
    options.EnableCloudMonitoring = true;
    options.CloudMonitoringPrefix = "dispatch.cloudevents";

    // Retry policy
    options.RetryPolicy = new GooglePubSubRetryPolicy
    {
        MaxRetryAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(60),
        DelayMultiplier = 2.0,
        UseJitter = true
    };
});
```

#### Dead Letter Queue Configuration

```csharp
services.Configure<DeadLetterOptions>(options =>
{
    // DLQ topic
    options.DeadLetterTopicName = TopicName.FromProjectTopic("your-project", "your-dlq-topic");

    // Delivery attempts
    options.DefaultMaxDeliveryAttempts = 5;

    // Auto-creation
    options.AutoCreateDeadLetterResources = true;

    // Retention
    options.DeadLetterRetentionDuration = TimeSpan.FromDays(7);

    // Automatic retry from DLQ
    options.EnableAutomaticRetry = false;
    options.AutomaticRetryInterval = TimeSpan.FromHours(1);
    options.AutomaticRetryBatchSize = 100;

    // Monitoring
    options.EnableMonitoring = true;
    options.MonitoringInterval = TimeSpan.FromMinutes(5);

    // Alerting thresholds
    options.AlertThresholdMessageCount = 1000;
    options.AlertThresholdMessageAge = TimeSpan.FromHours(24);

    // Non-retryable errors
    options.NonRetryableReasons = new HashSet<string>
    {
        "INVALID_MESSAGE_FORMAT",
        "UNAUTHORIZED",
        "MESSAGE_TOO_LARGE",
        "UNSUPPORTED_OPERATION"
    };

    // Message handling
    options.PreserveMessageOrdering = false;
    options.EnableCompression = true;
});
```

### Retry Policies

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.RetrySettings = new RetrySettings
    {
        InitialRetryDelay = TimeSpan.FromMilliseconds(100),   // Initial delay
        RetryDelayMultiplier = 2.0,                           // Exponential backoff
        MaxRetryDelay = TimeSpan.FromSeconds(60),             // Maximum delay
        TotalTimeout = TimeSpan.FromMinutes(10)               // Total retry window
    };
});
```

## Health Checks

### Registration

```csharp
services.AddHealthChecks()
    .AddCheck<GooglePubSubHealthCheck>("pubsub", tags: new[] { "ready", "messaging" });
```

### Configuration

```csharp
services.Configure<GooglePubSubHealthCheckOptions>(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.ProjectId = "your-gcp-project-id";
});
```

### Custom Health Check Implementation

```csharp
public class GooglePubSubHealthCheck : IHealthCheck
{
    private readonly ITransportHealthChecker _healthChecker;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _healthChecker.CheckQuickHealthAsync(cancellationToken);

            return result.Status switch
            {
                TransportHealthStatus.Healthy => HealthCheckResult.Healthy("Pub/Sub reachable"),
                TransportHealthStatus.Degraded => HealthCheckResult.Degraded(result.Description),
                _ => HealthCheckResult.Unhealthy(result.Description)
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Pub/Sub unreachable", ex);
        }
    }
}
```

## Production Considerations

### Scaling

#### Subscription Scaling

- **Pull subscriptions**: Scale horizontally with multiple subscribers
- **Push subscriptions**: Auto-scale with Cloud Run or Cloud Functions
- **Message ordering**: Use ordering keys for partitioned processing

```csharp
// High-throughput configuration
services.Configure<GoogleProviderOptions>(options =>
{
    options.MaxMessages = 1000;
    options.FlowControl = new FlowControlSettings
    {
        MaxOutstandingMessages = 10000,
        MaxOutstandingBytes = 500_000_000  // 500 MB
    };
});
```

#### Topic Scaling

- Topics auto-scale automatically
- Use multiple subscriptions for different consumer groups
- Consider regional topics for lower latency

### Performance Tuning

#### High-Throughput Publisher

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.MaxMessages = 1000;
    options.RequestTimeout = TimeSpan.FromSeconds(30);
    options.FlowControl = new FlowControlSettings
    {
        MaxOutstandingMessages = 10000,
        MaxOutstandingBytes = 500_000_000
    };
});

services.Configure<GooglePubSubCloudEventOptions>(options =>
{
    options.EnableCompression = true;
    options.CompressionThreshold = 10240;  // 10 KB
});
```

#### Low-Latency Consumer

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.MaxMessages = 10;  // Smaller batches
    options.AckDeadline = TimeSpan.FromSeconds(10);  // Shorter deadline
    options.FlowControl = new FlowControlSettings
    {
        MaxOutstandingMessages = 100
    };
});
```

#### Exactly-Once Processing

```csharp
services.Configure<GoogleProviderOptions>(options =>
{
    options.EnableExactlyOnceDelivery = true;
    options.AckDeadline = TimeSpan.FromMinutes(10);  // Longer deadline for exactly-once
});

services.Configure<GooglePubSubCloudEventOptions>(options =>
{
    options.UseExactlyOnceDelivery = true;
    options.EnableDeduplication = true;
});
```

### Monitoring and Alerting

Key Cloud Monitoring metrics:

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `pubsub.googleapis.com/subscription/num_undelivered_messages` | Backlog size | > 10,000 |
| `pubsub.googleapis.com/subscription/oldest_unacked_message_age` | Message age | > 600s |
| `pubsub.googleapis.com/subscription/ack_message_count` | Ack rate | Baseline deviation |
| `pubsub.googleapis.com/subscription/dead_letter_message_count` | DLQ rate | > 0 (investigate) |
| `pubsub.googleapis.com/topic/send_message_count` | Publish rate | Baseline deviation |

### Security Best Practices

1. **Use Workload Identity** in GKE for automatic credential rotation
2. **Apply least-privilege IAM** roles per service
3. **Enable VPC Service Controls** for network-level isolation
4. **Use CMEK encryption** for sensitive data
5. **Enable audit logging** for compliance
6. **Rotate service account keys** regularly (or avoid them with Workload Identity)

### Cost Optimization

1. **Use regional topics** when possible (cheaper than multi-region)
2. **Set message retention** appropriately (shorter = cheaper)
3. **Enable compression** for large messages
4. **Monitor unused subscriptions** and delete them
5. **Use filters** to reduce message delivery to subscriptions
6. **Batch messages** when possible to reduce API calls

## Troubleshooting

### Common Issues

#### Permission Denied

```
Google.Apis.Requests.RequestError: The caller does not have permission [403]
```

**Solutions:**
- Verify service account has required IAM roles
- Check project ID is correct
- Ensure Workload Identity is properly configured (GKE)
- Verify GOOGLE_APPLICATION_CREDENTIALS path

#### Topic/Subscription Not Found

```
Google.Cloud.PubSub.V1.NotFoundException: Resource not found (404)
```

**Solutions:**
- Verify topic/subscription exists
- Check project ID matches resource project
- Ensure resource names are fully qualified
- Create resources if AutoCreate is disabled

#### Message Acknowledgment Timeout

```
DeadlineExceeded: The deadline for the operation expired
```

**Solutions:**
- Increase `AckDeadline` to match processing time
- Use message lease extension for long-running operations
- Reduce message batch size
- Check for slow message handlers

#### Ordering Key Errors

```
InvalidArgument: Ordering key cannot be set when enable_message_ordering is false
```

**Solutions:**
- Enable `EnableMessageOrdering` on the subscription
- Use `UseOrderingKeys = true` in CloudEvent options
- Recreate subscription with ordering enabled (can't be changed after creation)

#### Flow Control Blocking

```
Resource exhausted: Flow control capacity exceeded
```

**Solutions:**
- Increase `MaxOutstandingMessages` and `MaxOutstandingBytes`
- Speed up message processing
- Scale horizontally with more consumers
- Check for memory issues in your application

### Logging Configuration

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Dispatch.Transport.GooglePubSub": "Debug",
      "Google": "Warning",
      "Grpc": "Warning"
    }
  }
}
```

### Debug Tips

1. **Use Cloud Console** to inspect messages in topics/subscriptions

2. **Check Cloud Logging**:
   ```bash
   gcloud logging read "resource.type=pubsub_subscription" --limit 50
   ```

3. **Use gcloud CLI**:
   ```bash
   # List topics
   gcloud pubsub topics list

   # List subscriptions
   gcloud pubsub subscriptions list

   # Pull messages manually
   gcloud pubsub subscriptions pull your-subscription --auto-ack --limit=10

   # View subscription details
   gcloud pubsub subscriptions describe your-subscription
   ```

4. **Enable gRPC debugging**:
   ```csharp
   Environment.SetEnvironmentVariable("GRPC_VERBOSITY", "DEBUG");
   Environment.SetEnvironmentVariable("GRPC_TRACE", "all");
   ```

5. **Use emulator for local testing**:
   ```bash
   # Start emulator
   gcloud beta emulators pubsub start --project=test-project

   # In another terminal
   $(gcloud beta emulators pubsub env-init)
   ```

6. **Docker Compose for local development**:
   ```yaml
   # docker-compose.yml
   services:
     pubsub-emulator:
       image: google/cloud-sdk:latest
       command: gcloud beta emulators pubsub start --host-port=0.0.0.0:8085
       ports:
         - "8085:8085"
   ```

## Complete Configuration Reference

```csharp
// Provider Options
services.Configure<GoogleProviderOptions>(options =>
{
    // Project configuration
    options.ProjectId = "your-gcp-project-id";

    // Emulator settings
    options.UseEmulator = false;
    options.EmulatorHost = "localhost:8085";

    // Request handling
    options.RequestTimeout = TimeSpan.FromSeconds(60);
    options.ValidateOnStartup = true;

    // Batch settings
    options.MaxMessages = 100;

    // Acknowledgment
    options.AckDeadline = TimeSpan.FromSeconds(30);

    // Delivery semantics
    options.EnableExactlyOnceDelivery = false;
    options.EnableMessageOrdering = false;

    // Flow control
    options.FlowControl = new FlowControlSettings
    {
        MaxOutstandingMessages = 1000,
        MaxOutstandingBytes = 100_000_000,
        LimitExceededBehavior = true
    };

    // Retry settings
    options.RetrySettings = new RetrySettings
    {
        InitialRetryDelay = TimeSpan.FromMilliseconds(100),
        RetryDelayMultiplier = 2.0,
        MaxRetryDelay = TimeSpan.FromSeconds(60),
        TotalTimeout = TimeSpan.FromMinutes(10)
    };
});

// CloudEvents Options
services.Configure<GooglePubSubCloudEventOptions>(options =>
{
    // Topic/Subscription
    options.ProjectId = "your-gcp-project-id";
    options.DefaultTopic = "cloud-events";
    options.DefaultSubscription = "cloud-events-subscription";

    // Message handling
    options.UseOrderingKeys = true;
    options.MaxMessageSizeBytes = 10 * 1024 * 1024;
    options.EnableDeduplication = true;

    // Compression
    options.EnableCompression = false;
    options.CompressionThreshold = 1024 * 1024;

    // Delivery
    options.UseExactlyOnceDelivery = false;
    options.AckDeadline = TimeSpan.FromMinutes(10);

    // Monitoring
    options.EnableCloudMonitoring = true;
    options.CloudMonitoringPrefix = "dispatch.cloudevents";

    // Retry
    options.RetryPolicy = new GooglePubSubRetryPolicy
    {
        MaxRetryAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(60),
        DelayMultiplier = 2.0,
        UseJitter = true
    };
});

// Dead Letter Options
services.Configure<DeadLetterOptions>(options =>
{
    options.DeadLetterTopicName = TopicName.FromProjectTopic("your-project", "your-dlq-topic");
    options.DefaultMaxDeliveryAttempts = 5;
    options.AutoCreateDeadLetterResources = true;
    options.DeadLetterRetentionDuration = TimeSpan.FromDays(7);
    options.EnableAutomaticRetry = false;
    options.AutomaticRetryInterval = TimeSpan.FromHours(1);
    options.AutomaticRetryBatchSize = 100;
    options.EnableMonitoring = true;
    options.MonitoringInterval = TimeSpan.FromMinutes(5);
    options.AlertThresholdMessageCount = 1000;
    options.AlertThresholdMessageAge = TimeSpan.FromHours(24);
    options.PreserveMessageOrdering = false;
    options.EnableCompression = true;
});
```

## See Also

- [Google Cloud Pub/Sub Documentation](https://cloud.google.com/pubsub/docs)
- [Pub/Sub Emulator Documentation](https://cloud.google.com/pubsub/docs/emulator)
- [CloudEvents Specification](https://cloudevents.io/)
