# Remote Bus Sample (RabbitMQ)

This sample demonstrates how to configure Dispatch with a RabbitMQ transport and the Excalibur outbox/inbox processors for reliable remote messaging.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for running RabbitMQ)

## Quick Start

### 1. Start RabbitMQ

```bash
docker run -d -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 2. Verify RabbitMQ is Running

Open the management UI at [http://localhost:15672](http://localhost:15672)
- Username: `guest`
- Password: `guest`

### 3. Run the Sample

```bash
dotnet run
```

## What This Sample Demonstrates

### RabbitMQ Transport Configuration

The sample uses `AddRabbitMQTransport` with a fluent builder API:

```csharp
builder.Services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString(connectionString)
        .ConfigureExchange(exchange =>
        {
            exchange.Name("dispatch.remote")
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
    // JSON is the default -- no serialization config needed
    dispatch.UseRouting(routing =>
        routing.Transport.Route<PingEvent>().To("rabbitmq"));
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

### Integration Events

`PingEvent` implements `IIntegrationEvent`, which signals that the event is intended for cross-service communication via a transport like RabbitMQ.

### Command Handlers

`PingCommand` demonstrates in-process command handling that does not route through the transport.

## Project Structure

```
RemoteBusSample/
├── PingCommand.cs           # Command definition (in-process)
├── PingCommandHandler.cs    # Command handler
├── PingEvent.cs             # Integration event (routed to RabbitMQ)
├── PingHandler.cs           # Event handler
├── Program.cs               # Application entry point
├── appsettings.json         # Configuration
└── README.md                # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `RabbitMq:ConnectionString` | AMQP connection string | `amqp://guest:guest@localhost:5672/` |

The broker these samples run against is plaintext, so the registration calls `RequireTls(false)`.
The transport otherwise refuses an unencrypted connection; a real deployment uses an `amqps://`
connection string and leaves that alone.

Connection settings can be overridden in `appsettings.json` or via environment variables.

## Cleanup

```bash
docker stop <container-id>
```

## Related Samples

- [RabbitMQ](../RabbitMQ/) - Dedicated RabbitMQ transport sample
- [Kafka](../Kafka/) - Apache Kafka transport
- [MultiBusSample](../MultiBusSample/) - Multiple transports in one application
