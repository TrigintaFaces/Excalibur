# AWS SQS Transport Sample

This sample demonstrates how to use `Excalibur.Dispatch.Transport.AwsSqs` for publishing and consuming messages via AWS SQS with LocalStack for local development.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for running LocalStack)
- [AWS CLI](https://aws.amazon.com/cli/) (optional, for queue management)
- [LocalStack CLI](https://docs.localstack.cloud/getting-started/installation/) (optional)

## Quick Start

### 1. Start LocalStack

```bash
docker-compose up -d
```

This starts LocalStack with SQS enabled and automatically creates the required queues via `init-sqs.sh`.

### 2. Verify Queues (Optional)

```bash
# Using LocalStack CLI
awslocal sqs list-queues

# Or using AWS CLI with endpoint override
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

### 3. Run the Sample

```bash
dotnet run
```

For a finite local verification run, publish the three sample orders and stop the host:

```bash
dotnet run -- --Sample:RunOnce true
```

## What This Sample Demonstrates

### Message Publishing

The sample publishes `OrderPlacedEvent` messages to AWS SQS:

```csharp
var order = new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m);
await dispatcher.DispatchAsync(order, context, cancellationToken: default);
```

### AWS SQS Transport Configuration

```csharp
builder.Services.AddAwsSqsTransport("sqs", sqs =>
{
    sqs.UseRegion("us-east-1")
        .UseMaxRetryAttempts(2)
        .UseRequestTimeout(TimeSpan.FromSeconds(10))
        .MapQueue<OrderPlacedEvent>(queueUrl);
});
```

### LocalStack Client Configuration

```csharp
builder.Services.AddKeyedSingleton<IAmazonSQS>("sqs", (_, _) =>
{
    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.USEast1,
        ServiceURL = "http://localhost:4566",
        UseHttp = true,
        MaxErrorRetry = 0,
        MaxStaleConnectionRetries = 0,
    };
    return new AmazonSQSClient(
        new BasicAWSCredentials("test", "test"),
        config);
});
```

The client key must match the transport name. An unkeyed client does not override the
named transport's client. This custom LocalStack client disables both SDK retry loops;
the framework owns foreground send retries, while an outbox or pipeline retry owner
suppresses that additional send policy for its exact message and context. Two configured
retries allow at most three foreground SDK send operations, with one total ten-second
deadline for each send execution. Caller cancellation remains effective.

Production uses the framework registration without a custom SDK client. It separates
send retry ownership from the receive client's SDK settings. An injected client with
SDK retries enabled cannot be used inside a framework-owned send budget; configure
both SDK retry settings to zero for that use.

### Routing Rules

Messages are routed to AWS SQS based on type:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithSerialization(serialization => serialization.RegisterSystemTextJson().UseSystemTextJson());
    dispatch.UseRouting(routing =>
    {
        routing.Transport.Route<OrderPlacedEvent>().To("sqs");
        routing.Endpoints.Route<OrderPlacedEvent>().To("sqs");
    });
    dispatch.UseMiddleware<RoutingMiddleware>();
});
```

The pipeline explicitly includes routing middleware and selects a remote endpoint.
Transport selection alone can still leave an event on the local handler path.

### Outbox Pattern

The sample registers the framework outbox and inbox services:

```csharp
builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
    outbox.UseInMemory().EnableBackgroundProcessing()));
builder.Services.AddInMemoryInboxStore();
// The inbox/outbox processors require an explicit dead-letter decision -- there is no default, so no
// deployment can silently discard a message it failed to deliver. This sample has no durable store to
// dead-letter into, so it chooses discard; a real deployment registers a durable queue instead, for
// example WithSqlServerDeadLetterQueue(...) on the outbox builder.
builder.Services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);
builder.Services.AddInboxHostedService();
```

Registering a store does not automatically stage directly dispatched events. This
transport demonstration publishes its three events directly; buffering handler-produced
events requires `dispatch.UseOutbox()` and the framework `IOutboxWriter`, as shown in the
[outbox template](../../../templates/excalibur-outbox/README.md).

The sample's in-memory stores are volatile and lose state when the process exits. Use
durable providers for production recovery. Successful dispatch is checked before the
sample logs an order as published; compilation alone does not verify broker delivery.

## Project Structure

```
AwsSqs/
├── Messages/
│   └── OrderPlacedEvent.cs       # Domain event definition
├── Handlers/
│   └── OrderPlacedHandler.cs # Message handler
├── Program.cs                     # Application entry point
├── appsettings.json              # Configuration
├── docker-compose.yml            # LocalStack container
├── init-sqs.sh                   # Queue initialization script
└── README.md                     # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `AwsSqs:Region` | AWS region | `us-east-1` |
| `AwsSqs:ServiceUrl` | Service URL (LocalStack endpoint) | `http://localhost:4566` |
| `AwsSqs:QueueUrl` | Full SQS queue URL | Required |
| `AwsSqs:UseLocalStack` | Use LocalStack mode | `true` |

## Key Concepts

### LocalStack

[LocalStack](https://localstack.cloud/) provides a fully functional local AWS cloud stack for development and testing. This sample uses LocalStack to run SQS locally without needing an AWS account.

### Queue URL Format

- **LocalStack**: `http://localhost:4566/000000000000/queue-name`
- **AWS**: `https://sqs.{region}.amazonaws.com/{account-id}/queue-name`

### Dead Letter Queue (DLQ)

The `init-sqs.sh` script configures a DLQ for failed message handling. Messages that fail processing 3 times are automatically moved to `dispatch-orders-dlq`.

### FIFO Queues

For ordered, exactly-once delivery, enable FIFO queues:

```csharp
// FIFO is configured through its own builder, not a boolean flag. Supplying the
// builder at all is what marks the adapter as FIFO.
opts.ConfigureFifo(fifo => fifo
    .ContentBasedDeduplication(true));

// Queue name must end with .fifo
```

`ConfigureFifo` also exposes `MessageGroupIdSelector` and `DeduplicationIdSelector` when you need to
derive the group or deduplication id from the message rather than relying on content-based
deduplication.

### Long Polling

SQS long polling reduces empty responses and costs:

```csharp
opts.WaitTimeSeconds = TimeSpan.FromSeconds(20);
```

## Production Configuration

For production AWS deployment:

1. Set `UseLocalStack` to `false`
2. Configure AWS credentials via:
   - Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
   - AWS credentials file (`~/.aws/credentials`)
   - IAM role (recommended for EC2/ECS/Lambda)
3. Use proper queue URL format

```json
{
  "AwsSqs": {
    "Region": "us-east-1",
    "QueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/dispatch-orders",
    "UseLocalStack": false
  }
}
```

## Cleanup

```bash
# Stop LocalStack
docker-compose down -v

# Remove all volumes
docker-compose down --volumes --remove-orphans
```

## Troubleshooting

### Connection Refused

Ensure LocalStack is running:
```bash
docker-compose ps
docker-compose logs localstack
```

### Queue Not Found

Create queue manually:
```bash
awslocal sqs create-queue --queue-name dispatch-orders
```

### Invalid Credentials (LocalStack)

LocalStack accepts any credentials. Use dummy values:
```csharp
new BasicAWSCredentials("test", "test")
```

### Messages Not Appearing

1. Check queue exists: `awslocal sqs list-queues`
2. Check messages: `awslocal sqs receive-message --queue-url http://localhost:4566/000000000000/dispatch-orders`
3. Verify service URL matches LocalStack port

## Related Samples

- [RabbitMQ](../RabbitMQ/) - RabbitMQ transport
- [Kafka](../Kafka/) - Apache Kafka transport
- [AzureServiceBus](../AzureServiceBus/) - Azure Service Bus transport

## Learn More

- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
- [Excalibur.Dispatch.Transport.AwsSqs Package](../../../src/Dispatch/Excalibur.Dispatch.Transport.AwsSqs/)
- [AWS SDK for .NET](https://aws.amazon.com/sdk-for-net/)
