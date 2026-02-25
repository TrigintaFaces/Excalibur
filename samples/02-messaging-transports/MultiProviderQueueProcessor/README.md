# MultiProviderQueueProcessor

Demonstrates consuming messages from multiple cloud providers with event sourcing and projections.

## Purpose

This sample shows a production-style setup combining:
- Multiple message transport providers (Azure Service Bus, Kafka, RabbitMQ, AWS SQS, Google Pub/Sub)
- SQL Server event store for persistence
- Elasticsearch projections for read models
- Domain-driven design patterns

## What This Sample Demonstrates

- **Multi-Provider Transport** - Consuming from multiple cloud message brokers
- **Event Sourcing** - Using SQL Server as the event store
- **Projections** - Updating Elasticsearch read models
- **Handler Discovery** - Automatic handler registration
- **Configuration** - Environment-based provider setup

## Prerequisites

- SQL Server (for event store)
- Elasticsearch (for projections)
- One or more message brokers (Azure Service Bus, Kafka, RabbitMQ, AWS SQS, Google Pub/Sub)

## Configuration

Create `appsettings.json` with your infrastructure settings:

```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost;Database=EventStore;..."
  },
  "ElasticSearch": {
    "Url": "http://localhost:9200"
  },
  "CloudMessaging": {
    "Providers": {
      "azure-servicebus": {
        "ConnectionString": "Endpoint=sb://...",
        "QueueName": "dispatch-events"
      },
      "kafka": {
        "BootstrapServers": "localhost:9092",
        "GroupId": "dispatch-consumer",
        "Topic": "dispatch-events"
      },
      "rabbitmq": {
        "ConnectionString": "amqp://guest:guest@localhost:5672",
        "QueueName": "dispatch-events"
      }
    }
  }
}
```

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
                    ┌───────────▼───────────┐
                    │   Dispatch Pipeline   │
                    │  (Handler Discovery)  │
                    └───────────┬───────────┘
                                │
         ┌──────────────────────┼──────────────────────┐
         │                      │                      │
┌────────▼────────┐   ┌────────▼────────┐   ┌────────▼────────┐
│  Event Store    │   │   Projections   │   │  Domain Logic   │
│  (SQL Server)   │   │ (Elasticsearch) │   │   (Handlers)    │
└─────────────────┘   └─────────────────┘   └─────────────────┘
```

## Key Configuration

### Transport Setup

```csharp
dispatch.AddEventTransports(transports =>
{
    // Azure Service Bus
    transports.AddAzureServiceBus("azure-servicebus", options =>
    {
        options.ConnectionString = config["ConnectionString"];
        options.QueueName = config["QueueName"];
    });

    // Kafka
    transports.AddKafka("kafka", options =>
    {
        options.BootstrapServers = config["BootstrapServers"];
        options.GroupId = config["GroupId"];
    });

    // RabbitMQ
    transports.AddRabbitMq("rabbitmq", options =>
    {
        options.ConnectionString = config["ConnectionString"];
    });
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
