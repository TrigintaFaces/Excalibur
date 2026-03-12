# MultiProviderQueueProcessor

Demonstrates consuming messages from multiple cloud providers with event sourcing and projections.

## Purpose

This sample shows a production-style setup combining:
- Multiple message transport providers (Azure Service Bus, Kafka, RabbitMQ, AWS SQS, Google Pub/Sub)
- SQL Server event store for persistence
- Elasticsearch projections for read models
- Domain-driven design patterns

## What This Sample Demonstrates

- **Multi-Provider Transport** - Consuming from multiple cloud message brokers via ADR-098 single entry points
- **Event Sourcing** - Using SQL Server as the event store
- **Projections** - Updating Elasticsearch read models
- **Handler Discovery** - Automatic handler registration via assembly scanning
- **Configuration** - Environment-based provider setup with conditional registration

## Prerequisites

- SQL Server (for event store)
- Elasticsearch (for projections)
- One or more message brokers (Azure Service Bus, Kafka, RabbitMQ, AWS SQS, Google Pub/Sub)

## Configuration

Create `appsettings.json` with your infrastructure settings:

```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost;Database=Dispatch_Events;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "ElasticSearch": {
    "Url": "http://localhost:9200"
  },
  "CloudMessaging": {
    "Providers": {
      "azure-servicebus": {
        "ConnectionString": "Endpoint=sb://...",
        "QueueName": "dispatch-events",
        "MaxConcurrentCalls": 10,
        "PrefetchCount": 20
      },
      "kafka": {
        "BootstrapServers": "localhost:9092",
        "GroupId": "processor-group",
        "Topic": "events"
      },
      "rabbitmq": {
        "ConnectionString": "amqp://guest:guest@localhost:5672",
        "QueueName": "dispatch-events",
        "PrefetchCount": 10
      },
      "aws-sqs": {
        "QueueUrl": "https://sqs.us-east-1.amazonaws.com/...",
        "MaxNumberOfMessages": 10,
        "WaitTimeSeconds": 20,
        "VisibilityTimeout": 30
      },
      "google-pubsub": {
        "ProjectId": "my-gcp-project",
        "TopicId": "dispatch-events",
        "SubscriptionId": "dispatch-processor"
      }
    }
  }
}
```

Each provider is conditionally registered -- only providers with non-empty connection strings are activated.

## Running the Sample

```bash
# Ensure infrastructure is running
docker-compose up -d  # If using Docker for dependencies

# Run the processor
dotnet run --project samples/02-messaging-transports/MultiProviderQueueProcessor
```

## Project Structure

```
MultiProviderQueueProcessor/
├── MultiProviderQueueProcessor.csproj
├── Program.cs                      # Main configuration
├── appsettings.json                # Provider configuration
├── Domain/
│   └── Order.cs                    # Aggregate root
├── Events/
│   └── OrderEvents.cs              # Domain events
├── Handlers/
│   └── OrderEventHandlers.cs       # Event handlers
├── Infrastructure/
│   ├── DatabaseInitializer.cs      # Schema setup
│   ├── OrderRepository.cs          # Event-sourced repository
│   └── OutboxProcessorService.cs   # Outbox background service
└── Projections/
    ├── IOrderProjectionUpdater.cs        # Projection interface
    └── ElasticOrderProjectionUpdater.cs  # Read model updates
```

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Azure Service   │    │     Kafka       │    │    RabbitMQ     │
│      Bus        │    │                 │    │                 │
└────────┬────────┘    └────────┬────────┘    └────────┬────────┘
         │                      │                      │
         └──────────────────────┼──────────────────────┘
                                │
               ┌────────────────┼────────────────┐
               │                │                │
      ┌────────┴────────┐      │       ┌────────┴────────┐
      │    AWS SQS      │      │       │  Google Pub/Sub │
      └────────┬────────┘      │       └────────┬────────┘
               │               │                │
               └───────────────┼────────────────┘
                               │
                   ┌───────────▼───────────┐
                   │   Dispatch Pipeline   │
                   │  (Handler Discovery)  │
                   └───────────┬───────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
┌───────▼─────────┐   ┌───────▼─────────┐   ┌───────▼─────────┐
│  Event Store    │   │   Projections   │   │  Domain Logic   │
│  (SQL Server)   │   │ (Elasticsearch) │   │   (Handlers)    │
└─────────────────┘   └─────────────────┘   └─────────────────┘
```

## Key Configuration

### Dispatch and Handler Setup

```csharp
builder.Services.AddDispatch(dispatch =>
{
    _ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

### Transport Setup (ADR-098 Single Entry Points)

Each transport is registered directly on `IServiceCollection` using its dedicated `AddXxxTransport()` extension method with a fluent builder API:

```csharp
// Azure Service Bus
builder.Services.AddAzureServiceBusTransport("azure-servicebus", asb =>
{
    _ = asb.ConnectionString(azureConnectionString)
        .ConfigureProcessor(processor =>
        {
            _ = processor.MaxConcurrentCalls(10)
                .PrefetchCount(20);
        })
        .MapEntity<object>(azureConfig["QueueName"] ?? "dispatch-events");
});

// Kafka
builder.Services.AddKafkaTransport("kafka", kafka =>
{
    _ = kafka.BootstrapServers(kafkaServers)
        .ConfigureConsumer(consumer =>
        {
            _ = consumer.GroupId("processor-group");
        })
        .MapTopic<object>("events");
});

// RabbitMQ
builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    _ = rmq.ConnectionString(rabbitConnectionString)
        .ConfigureQueue(queue =>
        {
            _ = queue.Name("dispatch-events")
                .PrefetchCount(10);
        });
});

// AWS SQS
builder.Services.AddAwsSqsTransport("aws-sqs", sqs =>
{
    _ = sqs.ConfigureQueue(queue =>
        {
            _ = queue.ReceiveWaitTimeSeconds(20)
                .VisibilityTimeout(TimeSpan.FromSeconds(30));
        })
        .ConfigureBatch(batch =>
        {
            _ = batch.ReceiveMaxMessages(10);
        })
        .MapQueue<object>(awsQueueUrl);
});

// Google Pub/Sub
builder.Services.AddGooglePubSubTransport("google-pubsub", pubsub =>
{
    _ = pubsub.ProjectId("my-gcp-project")
        .TopicId("dispatch-events")
        .SubscriptionId("dispatch-processor");
});
```

### Event Store Setup

```csharp
builder.Services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.EventStoreTable = "Events";
    options.SnapshotStoreTable = "Snapshots";
    options.OutboxTable = "EventSourcedOutbox";
});
```

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus |
| `Excalibur.Dispatch.Transport.Kafka` | Apache Kafka |
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ |
| `Excalibur.Dispatch.Transport.AwsSqs` | AWS SQS |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google Pub/Sub |
| `Excalibur.EventSourcing.SqlServer` | SQL Server event store |
| `Excalibur.Data.ElasticSearch` | Elasticsearch projections |

## Next Steps

- [MultiBusSample](../MultiBusSample/) - Simpler multi-bus example
- [SagaOrchestration](../../04-reliability/SagaOrchestration/) - Distributed transactions

---

*Category: Messaging Transports | Sprint 428*
