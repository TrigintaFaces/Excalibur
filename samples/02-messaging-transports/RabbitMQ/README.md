# RabbitMQ Transport Sample

This sample demonstrates how to use `Excalibur.Dispatch.Transport.RabbitMQ` for publishing and consuming messages via RabbitMQ.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for running RabbitMQ)

## Quick Start

### 1. Start RabbitMQ

```bash
docker-compose up -d
```

This starts RabbitMQ 3.12 with the management plugin enabled.

### 2. Verify RabbitMQ is Running

Open the management UI at [http://localhost:15672](http://localhost:15672)
- Username: `guest`
- Password: `guest`

### 3. Run the Sample

```bash
dotnet run
```

## What This Sample Demonstrates

### Message Publishing

The sample publishes `OrderPlacedEvent` messages to RabbitMQ:

```csharp
var order = new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m);
await dispatcher.DispatchAsync(order, context, cancellationToken: default);
```

### RabbitMQ Configuration

The sample uses `AddRabbitMQTransport` with a fluent builder API:

```csharp
builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString(connectionString)
        .ConfigureExchange(exchange =>
        {
            exchange.Name("dispatch.events")
                .Type(RabbitMQExchangeType.Topic)
                .AutoDelete(true);
        })
        .ConfigureCloudEvents(ce =>
        {
            ce.Exchange.Persistence = RabbitMqPersistence.Persistent;
        });
});
```

### Routing Rules

Messages are routed to RabbitMQ using the `UseRouting` fluent API:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithSerialization(config => config.UseSystemTextJson());
    dispatch.UseRouting(routing =>
        routing.Transport.Route<OrderPlacedEvent>().To("rabbitmq"));
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
RabbitMQ/
├── Messages/
│   └── OrderPlacedEvent.cs      # Integration event definition
├── Handlers/
│   └── OrderPlacedEventHandler.cs # Message handler
├── Program.cs                    # Application entry point
├── appsettings.json             # Configuration
├── docker-compose.yml           # RabbitMQ container
└── README.md                    # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `RabbitMq:ConnectionString` | AMQP connection string | `amqp://guest:guest@localhost:5672/` |

Connection settings can be overridden in `appsettings.json` or via environment variables. The exchange name, type, and CloudEvents options are configured in code via the fluent builder API.

## Key Concepts

### Exchanges

RabbitMQ uses exchanges to route messages. This sample uses a **topic exchange** which routes based on routing key patterns.

### CloudEvents

The sample configures CloudEvents formatting via `ConfigureCloudEvents()` for interoperability with the [CloudEvents specification](https://cloudevents.io/).

### Integration Events

`OrderPlacedEvent` implements `IIntegrationEvent`, which signals that the event is intended for cross-service communication via a transport like RabbitMQ.

### Event Handlers

`OrderPlacedEventHandler` implements `IEventHandler<OrderPlacedEvent>` to process messages consumed from RabbitMQ.

## Cleanup

```bash
docker-compose down -v  # Stop and remove volumes
```

## Troubleshooting

### Connection Refused

Ensure RabbitMQ is running:
```bash
docker-compose ps
docker-compose logs rabbitmq
```

### Authentication Failed

Check credentials in `appsettings.json` match the Docker environment variables.

### Queue Not Created

Queues are created on first consume. To create manually, use the Management UI or CLI.

## Related Samples

- [Kafka](../Kafka/) - Apache Kafka transport
- [MultiBusSample](../MultiBusSample/) - Multiple transports in one application
- [RemoteBusSample](../RemoteBusSample/) - Remote bus patterns

## Learn More

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [Excalibur.Dispatch.Transport.RabbitMQ Package](../../../src/Dispatch/Excalibur.Dispatch.Transport.RabbitMQ/)
- [CloudEvents Specification](https://cloudevents.io/)
