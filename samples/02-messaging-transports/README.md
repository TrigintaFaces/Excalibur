# Messaging Transport Samples

Transport configuration, multi-bus scenarios, and cross-provider messaging.

## Choosing a Transport

| Transport | Best For | Local Dev | Cloud Native |
|-----------|----------|-----------|--------------|
| **[RabbitMQ](RabbitMQ/)** | General messaging, complex routing, fan-out patterns | Docker | On-prem, AWS MQ |
| **[Kafka](Kafka/)** | High-throughput streaming, event log, replay | Docker (KRaft) | Confluent, AWS MSK |
| **[Azure Service Bus](AzureServiceBus/)** | Azure-native apps, enterprise messaging | Emulator | Azure |
| **[AWS SQS](AwsSqs/)** | AWS-native apps, serverless integration | LocalStack | AWS |

## Dedicated Transport Samples

These samples demonstrate each transport provider with complete setup and examples:

| Sample | Transport | Description | Local Dev |
|--------|-----------|-------------|-----------|
| **[RabbitMQ](RabbitMQ/)** | RabbitMQ 3.12 | Topic exchange routing, CloudEvents | Docker Compose |
| **[Kafka](Kafka/)** | Kafka 7.5 (KRaft) | Consumer groups, partitioning, compression | Docker Compose |
| **[AzureServiceBus](AzureServiceBus/)** | Azure Service Bus | Queues, topics, sessions | Azure / Emulator |
| **[AwsSqs](AwsSqs/)** | AWS SQS | Standard/FIFO queues, DLQ | LocalStack |

## Multi-Transport Samples

These samples show how to combine multiple transports:

| Sample | Description | Transports |
|--------|-------------|------------|
| [MultiTransport](MultiTransport/) | Using multiple transports in a single application | In-memory, RabbitMQ |
| [MultiBusSample](MultiBusSample/) | Multi-bus configuration with Kafka and RabbitMQ | RabbitMQ, Kafka |
| [RemoteBusSample](RemoteBusSample/) | RabbitMQ remote bus with command/event handlers | RabbitMQ |
| [TransportBindings](TransportBindings/) | Transport binding configuration examples | Various |
| [MultiProviderQueueProcessor](MultiProviderQueueProcessor/) | Processing messages from multiple queue providers | Various |

## Transport Comparison

| Feature | RabbitMQ | Kafka | Azure Service Bus | AWS SQS |
|---------|----------|-------|-------------------|---------|
| **Message Model** | Queue/Exchange | Log/Topic | Queue/Topic | Queue |
| **Ordering** | Per-queue | Per-partition | Per-session | FIFO queues |
| **Replay** | No | Yes | No | No |
| **Max Message Size** | 128 MB | 1 MB (default) | 256 KB (Standard) | 256 KB |
| **Throughput** | High | Very High | High | High |
| **Persistence** | Optional | Always | Always | Always |
| **Protocol** | AMQP | Kafka | AMQP | HTTPS |

## Quick Start

### Local Development (Docker)

```bash
# RabbitMQ
cd samples/02-messaging-transports/RabbitMQ
docker-compose up -d
dotnet run

# Kafka (KRaft mode - no Zookeeper!)
cd samples/02-messaging-transports/Kafka
docker-compose up -d
dotnet run

# AWS SQS (LocalStack)
cd samples/02-messaging-transports/AwsSqs
docker-compose up -d
dotnet run
```

### Cloud Development

```bash
# Azure Service Bus (requires Azure subscription)
cd samples/02-messaging-transports/AzureServiceBus
# Update appsettings.json with connection string
dotnet run
```

## Key Concepts

### Transport Configuration

```csharp
builder.Services.AddEventTransports(transports =>
{
    // RabbitMQ
    transports.AddRabbitMq("rabbitmq", opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@localhost:5672";
    });

    // Kafka
    transports.AddKafka("kafka", opts =>
    {
        opts.BootstrapServers = "localhost:9092";
    });

    // Azure Service Bus
    transports.AddAzureServiceBus("azuresb", opts =>
    {
        opts.ConnectionString = "Endpoint=sb://...";
    });

    // AWS SQS
    transports.AddAwsSqs("sqs", opts =>
    {
        opts.Region = "us-east-1";
    });
});
```

### Routing Rules

Route specific message types to specific transports:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.WithRoutingRules(rules =>
    {
        rules.AddRule<OrderEvent>((_, _) => "rabbitmq");
        rules.AddRule<SensorReading>((_, _) => "kafka");
        rules.AddRule<AuditEvent>((_, _) => "azuresb");
    });
});
```

### Multi-Bus Architecture

When you need separate message buses for different concerns:

```csharp
// Local bus for internal commands
var localBus = services.AddDispatchBus("local", cfg => cfg.UseInMemory());

// Remote bus for integration events
var remoteBus = services.AddDispatchBus("remote", cfg => cfg.UseRabbitMq());
```

## Prerequisites

| Transport | Local Development | Production |
|-----------|-------------------|------------|
| RabbitMQ | Docker | Docker, AWS MQ, CloudAMQP |
| Kafka | Docker | Confluent Cloud, AWS MSK, self-hosted |
| Azure Service Bus | Emulator (optional) | Azure subscription |
| AWS SQS | LocalStack | AWS account |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ message transport |
| `Excalibur.Dispatch.Transport.Kafka` | Apache Kafka transport |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus transport |
| `Excalibur.Dispatch.Transport.AwsSqs` | AWS SQS/SNS transport |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google Cloud Pub/Sub transport |

## What's Next?

- [04-reliability/](../04-reliability/) - Sagas and distributed transactions
- [09-advanced/CrossLangSample/](../09-advanced/CrossLangSample/) - Cross-language messaging
- [09-advanced/SessionManagement/](../09-advanced/SessionManagement/) - AWS SQS FIFO session patterns

---

*Category: Messaging Transports | Sprint 429*
