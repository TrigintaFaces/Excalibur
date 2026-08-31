# Excalibur.Dispatch.Transport.GooglePubSub

Google Cloud Pub/Sub transport implementation for the Excalibur framework, providing scalable, serverless messaging with exactly-once delivery, message ordering, and dead letter topic support.

## Overview

This package provides Google Cloud Pub/Sub integration for Excalibur.Dispatch, enabling:

- **Serverless Messaging**: Fully managed, auto-scaling message infrastructure
- **Exactly-Once Delivery**: Guaranteed delivery with deduplication
- **Message Ordering**: Ordering keys for sequential processing
- **Dead Letter Topics**: Automatic handling of failed messages
- **CloudEvents Support**: Standards-compliant structured event formatting. Registering the bundled mapper is annotated for trimming and ahead-of-time builds (it serializes payloads with reflection-based JSON); supply your own `ICloudEventMapper<TTransportMessage>` over a source-generated serializer to avoid the requirement.
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
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options => options.Subscriber.MaxPullMessages = 100));
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
PUBSUB__CONNECTION__PROJECTID=your-gcp-project-id
PUBSUB__CONNECTION__TOPICID=orders
PUBSUB__CONNECTION__SUBSCRIPTIONID=orders-worker
PUBSUB__SUBSCRIBER__MAXPULLMESSAGES=100
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json
```

```csharp
services.AddGooglePubSubTransport("pubsub", configuration.GetSection("PubSub"));
```

#### Local Development with Emulator

Use the Pub/Sub emulator for local development without GCP credentials:

The Google client libraries pick the emulator up from the `PUBSUB_EMULATOR_HOST` environment
variable, so no framework-side switch is needed — point the variable at the emulator and configure
the transport exactly as you would against the real service:

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("test-project")
    .TopicId("orders")
    .SubscriptionId("orders-worker"));
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
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker"));
```

#### Service Account Key File

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker"));
// Credentials are loaded from the GOOGLE_APPLICATION_CREDENTIALS environment variable
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
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options =>
    {
        // Pull behaviour
        options.Subscriber.MaxPullMessages = 100;
        options.Subscriber.MaxPayloadBytes = 10 * 1024 * 1024;

        // Delivery guarantees. Both are verified against the subscription at start-up and fail
        // loud when the subscription does not provide what was asked for.
        options.Subscriber.EnableExactlyOnceDelivery = false;
        options.Subscriber.EnableMessageOrdering = false;

        // Streaming-pull flow control
        options.Subscriber.FlowControl.MaxOutstandingElementCount = 1000;
        options.Subscriber.FlowControl.MaxOutstandingByteCount = 100_000_000;  // 100 MB
    }));
```

#### CloudEvents Configuration

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("cloud-events")
    .SubscriptionId("cloud-events-subscription")
    .ConfigureCloudEvents(options =>
    {
        // Message ordering
        options.UseOrderingKeys = true;

        // Compression
        options.Transport.EnableCompression = false;
        options.Transport.CompressionThreshold = 1024 * 1024;  // 1 MB
    }));
```

#### Dead Letter Queue Configuration

Dead lettering is a property of the Pub/Sub subscription: the transport applies the policy at
start-up so messages a handler rejects are moved by Pub/Sub itself after the delivery-attempt
ceiling, rather than by a framework-side retry loop.

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options =>
    {
        options.Subscriber.DeadLetter.Enable = true;
        options.Subscriber.DeadLetter.TopicId = "orders-dlq";

        // Apply the policy to the subscription at start-up rather than expecting it to
        // already exist.
        options.Subscriber.DeadLetter.AutoApplyPolicy = true;

        // Delivery attempts before Pub/Sub moves the message to the dead-letter topic.
        options.Subscriber.DeadLetter.MaxDeliveryAttempts = 5;
    }));
```

`EnableDeadLetter("orders-dlq")` on the builder is shorthand for the first two lines.

### Retry Policies

Transient RPC retries are handled by the Google client library's own default retry settings; the
transport does not layer a second retry policy over them. Delivery retries for a message a handler
rejects are governed by the subscription's dead-letter policy — see
[Dead Letter Queue Configuration](#dead-letter-queue-configuration).

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

#### Subscription Scaling

- **Pull subscriptions**: Scale horizontally with multiple subscribers
- **Push subscriptions**: Auto-scale with Cloud Run or Cloud Functions
- **Message ordering**: Use ordering keys for partitioned processing

```csharp
// High-throughput configuration
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options =>
    {
        options.Subscriber.MaxPullMessages = 1000;
        options.Subscriber.FlowControl.MaxOutstandingElementCount = 10_000;
        options.Subscriber.FlowControl.MaxOutstandingByteCount = 500_000_000;  // 500 MB
    }));
```

#### Topic Scaling

- Topics auto-scale automatically
- Use multiple subscriptions for different consumer groups
- Consider regional topics for lower latency

### Performance Tuning

#### High-Throughput Publisher

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options =>
    {
        options.Subscriber.MaxPullMessages = 1000;
        options.Subscriber.FlowControl.MaxOutstandingElementCount = 10_000;
        options.Subscriber.FlowControl.MaxOutstandingByteCount = 500_000_000;
    }));
```

```csharp
services.Configure<GooglePubSubCloudEventOptions>(options =>
{
    options.Transport.EnableCompression = true;
    options.Transport.CompressionThreshold = 10240;  // 10 KB
});
```

#### Low-Latency Consumer

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options =>
    {
        options.Subscriber.MaxPullMessages = 10;  // Smaller batches
        options.Subscriber.FlowControl.MaxOutstandingElementCount = 100;
    }));
```

#### Exactly-Once Processing

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options => options.Subscriber.EnableExactlyOnceDelivery = true));
```

```csharp
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .ConfigureOptions(options => options.Subscriber.EnableExactlyOnceDelivery = true));
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
services.AddGooglePubSubTransport("pubsub", transport => transport
    .ProjectId("your-gcp-project-id")
    .TopicId("orders")
    .SubscriptionId("orders-worker")
    .MapTopic<OrderPlaced>("orders")
    .EnableDeadLetter("orders-dlq")
    .ConfigureOptions(options =>
    {
        // Pull behaviour
        options.Subscriber.MaxPullMessages = 100;
        options.Subscriber.MaxPayloadBytes = 10 * 1024 * 1024;

        // Delivery guarantees, verified against the subscription at start-up
        options.Subscriber.EnableExactlyOnceDelivery = false;
        options.Subscriber.EnableMessageOrdering = false;

        // Streaming-pull flow control
        options.Subscriber.FlowControl.MaxOutstandingElementCount = 1000;
        options.Subscriber.FlowControl.MaxOutstandingByteCount = 100_000_000;

        // Dead letter
        options.Subscriber.DeadLetter.Enable = true;
        options.Subscriber.DeadLetter.TopicId = "orders-dlq";
        options.Subscriber.DeadLetter.AutoApplyPolicy = true;
        options.Subscriber.DeadLetter.MaxDeliveryAttempts = 5;

        // Telemetry
        options.Telemetry.EnableOpenTelemetry = true;
        options.Telemetry.ExportToCloudMonitoring = false;
    })
    .ConfigureCloudEvents(options =>
    {
        options.UseOrderingKeys = true;
        options.Transport.EnableCompression = false;
        options.Transport.CompressionThreshold = 1024 * 1024;
    }));
```

## See Also

- [Google Cloud Pub/Sub Documentation](https://cloud.google.com/pubsub/docs)
- [Pub/Sub Emulator Documentation](https://cloud.google.com/pubsub/docs/emulator)
- [CloudEvents Specification](https://cloudevents.io/)
