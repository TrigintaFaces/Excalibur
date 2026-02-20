# AWS SQS Transport Sample

This sample demonstrates how to use `Excalibur.Dispatch.Transport.AwsSqs` for publishing and consuming messages via AWS SQS with LocalStack for local development.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
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

## What This Sample Demonstrates

### Message Publishing

The sample publishes `OrderPlacedEvent` messages to AWS SQS:

```csharp
var order = new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m);
await dispatcher.DispatchAsync(order, context);
```

### AWS SQS Configuration

```csharp
builder.Services.AddAwsSqs(opts =>
{
    opts.Region = "us-east-1";
    opts.QueueUrl = new Uri("http://localhost:4566/000000000000/dispatch-orders");
    opts.ServiceUrl = new Uri("http://localhost:4566"); // LocalStack endpoint
});
```

### LocalStack Client Configuration

```csharp
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.USEast1,
        ServiceURL = "http://localhost:4566",
        UseHttp = true,
    };
    return new AmazonSQSClient(
        new BasicAWSCredentials("test", "test"),
        config);
});
```

### Routing Rules

Messages are routed to AWS SQS based on type:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.WithRoutingRules(rules =>
        rules.AddRule<OrderPlacedEvent>((_, _) => "sqs"));
});
```

### Outbox Pattern

The sample uses the outbox pattern for reliable messaging:

```csharp
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddOutboxHostedService();
```

## Project Structure

```
AwsSqs/
├── Messages/
│   └── OrderPlacedEvent.cs       # Domain event definition
├── Handlers/
│   └── OrderPlacedEventHandler.cs # Message handler
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
opts.UseFifoQueue = true;
opts.ContentBasedDeduplication = true;
// Queue name must end with .fifo
```

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
