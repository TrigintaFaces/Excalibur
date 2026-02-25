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
await dispatcher.DispatchAsync(order, context);
```

### RabbitMQ Configuration

```csharp
builder.Services.AddRabbitMqMessageBus(options =>
{
    options.ConnectionString = "amqp://guest:guest@localhost:5672/";
    options.ExchangeName = "dispatch.events";
    options.ExchangeType = ExchangeType.Topic;
    options.Persistence = MessagePersistence.Persistent;
    options.EnableCloudEvents = true;
});
```

### Routing Rules

Messages are routed to RabbitMQ based on type:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.WithRoutingRules(rules =>
        rules.AddRule<OrderPlacedEvent>((_, _) => "rabbitmq"));
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
RabbitMQ/
├── Messages/
│   └── OrderPlacedEvent.cs      # Domain event definition
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
| `RabbitMq:Exchange` | Exchange name | `dispatch.events` |
| `RabbitMq:QueueName` | Queue name for consuming | `dispatch.orders` |
| `RabbitMq:RoutingKey` | Routing key pattern | `orders.#` |

## Key Concepts

### Exchanges

RabbitMQ uses exchanges to route messages. This sample uses a **topic exchange** which routes based on routing key patterns.

### Queues

Queues store messages until consumed. With `QueueDurable = true`, messages survive broker restarts.

### CloudEvents

With `EnableCloudEvents = true`, messages are formatted according to the [CloudEvents specification](https://cloudevents.io/) for interoperability.

### Dead Letter Exchange

Enable `EnableDeadLetterExchange` to route failed messages to a DLX for later analysis.

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

