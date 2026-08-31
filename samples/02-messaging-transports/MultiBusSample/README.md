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
builder.Services.AddInMemoryInboxStore();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();
```

### Per-Transport Keyed Seam (advanced)

Most messages flow through the routing rules above. When you need low-level access to one specific
transport — a custom relay, a health probe, or a one-off publish outside the routing rules — resolve
that transport's `ITransportSender` directly, keyed by the name you registered it under:

```csharp
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;

// "rabbitmq" is the name passed to AddRabbitMQTransport("rabbitmq", …).
var rabbitSender = host.Services.GetRequiredKeyedService<ITransportSender>("rabbitmq");

var healthPing = TransportMessage.FromString("health-ping");
healthPing.Subject = "multibus.health";

var result = await rabbitSender.SendAsync(healthPing, cancellationToken: default);
await rabbitSender.FlushAsync(cancellationToken: default);
```

Every transport package registers a keyed `ITransportSender` (and `ITransportReceiver`) under its name.
See [Per-Transport Extension Point](../../../docs-site/docs/transports/keyed-transport-seam.md) for the
full contract and when to reach for it.

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

The broker these samples run against is plaintext, so the registration calls `RequireTls(false)`.
The transport otherwise refuses an unencrypted connection; a real deployment uses an `amqps://`
connection string and leaves that alone.
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
