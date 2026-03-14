# Multi-Bus Sample (RabbitMQ + Kafka)

This sample registers both RabbitMQ and Kafka transports and demonstrates how routing rules direct different message types to each broker.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for running RabbitMQ and Kafka)

## Quick Start

### 1. Start RabbitMQ and Kafka

```bash
docker-compose up -d
```

This starts both RabbitMQ (with management UI) and Kafka (with Zookeeper).

### 2. Run the Sample

```bash
dotnet run
```

## What This Sample Demonstrates

### Multi-Transport Routing

Different message types are routed to different transports:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseRouting(routing =>
    {
        routing.Transport.Route<RabbitPingEvent>().To("rabbitmq");
        routing.Transport.Route<KafkaPingEvent>().To("kafka");
    });
});
```

### RabbitMQ Transport Configuration

```csharp
builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString(connectionString)
        .ConfigureExchange(exchange =>
        {
            exchange.Name("dispatch.multibus")
                .Type(RabbitMQExchangeType.Topic)
                .AutoDelete(true);
        })
        .ConfigureCloudEvents(ce =>
        {
            ce.Exchange.Persistence = RabbitMqPersistence.Persistent;
        });
});
```

### Kafka Transport Configuration

```csharp
builder.Services.AddKafkaTransport("kafka", kafka =>
{
    kafka.BootstrapServers(bootstrapServers)
        .ConfigureProducer(producer =>
        {
            producer.ClientId("dispatch-multibus-producer")
                .Acks(KafkaAckLevel.All);
        })
        .ConfigureConsumer(consumer =>
        {
            consumer.GroupId("dispatch-multibus-consumer");
        })
        .MapTopic<KafkaPingEvent>("multibus-ping");
});
```

### Outbox and Inbox Pattern

The sample uses the outbox and inbox patterns for reliable messaging:

```csharp
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();
```

## Project Structure

```
MultiBusSample/
├── KafkaPingEvent.cs        # Integration event (routed to Kafka)
├── KafkaPingHandler.cs      # Kafka event handler
├── RabbitPingEvent.cs       # Integration event (routed to RabbitMQ)
├── RabbitPingHandler.cs     # RabbitMQ event handler
├── Program.cs               # Application entry point
├── appsettings.json         # Configuration
├── docker-compose.yml       # RabbitMQ + Kafka containers
└── README.md                # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `RabbitMq:ConnectionString` | AMQP connection string | `amqp://guest:guest@localhost:5672/` |
| `Kafka:BootstrapServers` | Kafka broker addresses | `localhost:9092` |

## Key Concepts

### Transport Isolation

Each transport is independently configured with its own name, connection settings, and behavior. The routing layer decides which transport receives each message type based on compile-time routing rules.

### Shared Outbox

Both transports share the same outbox and inbox stores, providing unified reliable messaging regardless of the target broker.

## Cleanup

```bash
docker-compose down -v  # Stop and remove volumes
```

## Related Samples

- [RabbitMQ](../RabbitMQ/) - Dedicated RabbitMQ transport sample
- [Kafka](../Kafka/) - Dedicated Kafka transport sample
- [RemoteBusSample](../RemoteBusSample/) - Single RabbitMQ transport sample
