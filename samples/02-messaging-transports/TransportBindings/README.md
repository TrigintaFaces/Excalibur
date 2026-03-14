# TransportBindings

Demonstrates the Transport Bindings API for configuring message routing.

## Purpose

This sample shows how to configure transport bindings - the rules that map incoming messages to Dispatch pipeline profiles. It demonstrates the declarative API for defining transport sources and routing.

## What This Sample Demonstrates

- **Transport Registration** - Adding transport providers (InMemory, Kafka, RabbitMQ, etc.)
- **Binding Configuration** - Mapping sources to dispatcher profiles
- **Transport Registry** - Inspecting registered transports
- **Binding Registry** - Viewing configured bindings

## Running the Sample

```bash
dotnet run --project samples/02-messaging-transports/TransportBindings
```

## Sample Output

```
Transport Bindings API Demo
===========================

Registered Transports:
  - test (InMemory)

Registered Bindings:
  (bindings are commented out in demo - uncomment to see them)

Transport Bindings API implementation complete!
Press any key to exit...
```

## Key Concepts

### Transport Registration

Register transport providers using the ADR-098 single entry point pattern:

```csharp
// Simple in-memory transport for testing
builder.Services.AddInMemoryTransport("test");

// Production transports (when infrastructure is available):
// builder.Services.AddKafkaTransport("kafka", k => k.BootstrapServers("localhost:9092"));
// builder.Services.AddRabbitMQTransport("rabbitmq", r => r.ConnectionString("amqp://localhost"));
// builder.Services.AddAzureServiceBusTransport("servicebus", sb => sb.ConnectionString("Endpoint=sb://..."));
// builder.Services.AddAzureStorageQueueTransport("orders", sq => sq.ConnectionString("..."));
// builder.Services.AddAzureEventHubsTransport("eventhubs", eh => eh.ConnectionString("Endpoint=sb://..."));
```

### Binding Configuration

Define how messages from transports route to Dispatch pipelines:

```csharp
builder.Services.AddEventBindings(b =>
{
    // Route messages from a queue to a dispatcher profile
    b.FromQueue("orders")
        .RouteName("order-received")
        .ToDispatcher("internal-event");

    // Route specific message types
    b.FromTransport("rabbitmq")
        .RouteType<GenericDispatchMessage>()
        .ToDispatcher("strict");
});
```

### Available Transports

| Transport | Method | Connection |
|-----------|--------|------------|
| InMemory | `AddInMemoryTransport(name)` | No external deps |
| Kafka | `AddKafkaTransport(name, configure)` | Kafka cluster |
| RabbitMQ | `AddRabbitMQTransport(name, configure)` | RabbitMQ server |
| Azure Service Bus | `AddAzureServiceBusTransport(name, configure)` | Azure connection |
| Azure Storage Queue | `AddAzureStorageQueueTransport(name, configure)` | Storage account |
| Azure Event Hubs | `AddAzureEventHubsTransport(name, configure)` | Event Hubs |

## Project Structure

```
TransportBindings/
├── TransportBindings.csproj    # Project file
├── Program.cs                  # Transport and binding configuration
└── README.md                   # This file
```

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Transport.Abstractions` | Transport interfaces |
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ provider |
| `Excalibur.Dispatch.Transport.Kafka` | Kafka provider |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus provider |

## Next Steps

- [MultiBusSample](../MultiBusSample/) - Full multi-transport example

---

*Category: Messaging Transports | Sprint 428*
