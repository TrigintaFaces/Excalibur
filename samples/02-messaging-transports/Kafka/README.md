# Kafka Transport Sample

This sample demonstrates how to use `Excalibur.Dispatch.Transport.Kafka` for publishing and consuming messages via Apache Kafka.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for running Kafka)

## Quick Start

### 1. Start Kafka

```bash
docker-compose up -d
```

This starts Kafka 7.5 in KRaft mode (no Zookeeper required).

### 2. Verify Kafka is Running

Wait for the container to be healthy:
```bash
docker-compose ps
```

You can also check the logs:
```bash
docker-compose logs -f kafka
```

Look for "Kafka Server started" in the output.

### 3. Run the Sample

```bash
dotnet run
```

## What This Sample Demonstrates

### Message Publishing

The sample publishes `SensorReadingEvent` messages to Kafka:

```csharp
var reading = new SensorReadingEvent(
    SensorId: "SENSOR-001",
    Temperature: 22.5,
    Humidity: 45.0,
    Timestamp: DateTimeOffset.UtcNow);

await dispatcher.DispatchAsync(reading, context);
```

### Kafka Configuration

```csharp
builder.Services.AddKafkaMessageBus(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.ProducerClientId = "dispatch-sensor-producer";
    options.ConsumerGroupId = "dispatch-sensor-consumer";
    options.DefaultTopic = "sensor-readings";
    options.AutoCreateTopics = true;
    options.CompressionType = KafkaCompressionType.Snappy;
    options.AckLevel = KafkaAckLevel.All;
    options.PartitioningStrategy = KafkaPartitioningStrategy.Key;
});
```

### Routing Rules

Messages are routed to Kafka based on type:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.WithRoutingRules(rules =>
        rules.AddRule<SensorReadingEvent>((_, _) => "kafka"));
});
```

### Consumer Groups

Kafka uses consumer groups for message distribution:
- Multiple instances with the same `ConsumerGroupId` share the workload
- Each partition is consumed by exactly one consumer in the group
- Enables horizontal scaling of message processing

## Project Structure

```
Kafka/
├── Messages/
│   └── SensorReadingEvent.cs     # Event definition
├── Handlers/
│   └── SensorReadingEventHandler.cs # Message handler
├── Program.cs                     # Application entry point
├── appsettings.json              # Configuration
├── docker-compose.yml            # Kafka container (KRaft mode)
└── README.md                     # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Kafka:BootstrapServers` | Kafka broker addresses | `localhost:9092` |
| `Kafka:Topic` | Default topic name | `sensor-readings` |
| `Kafka:ConsumerGroup` | Consumer group ID | `dispatch-sensor-consumer` |

## Key Concepts

### Partitioning

Kafka partitions topics for parallel processing. With `PartitioningStrategy.Key`, messages with the same key (e.g., SensorId) go to the same partition, ensuring ordered processing per sensor.

### Consumer Groups

Multiple application instances can share message processing:
- Same `ConsumerGroupId` = workload distributed across instances
- Different `ConsumerGroupId` = each group gets all messages

### Compression

The sample uses Snappy compression (`KafkaCompressionType.Snappy`) for:
- Reduced network bandwidth
- Better throughput for high-volume scenarios
- Minimal CPU overhead

### Acknowledgments

With `KafkaAckLevel.All`:
- Messages are acknowledged only after all replicas confirm
- Provides strongest durability guarantee
- Trade-off: slightly higher latency

## Scaling

Run multiple instances to demonstrate consumer group rebalancing:

```bash
# Terminal 1
dotnet run

# Terminal 2 (in another terminal)
dotnet run
```

Watch the logs to see how partitions are distributed between consumers.

## Cleanup

```bash
docker-compose down -v  # Stop and remove volumes
```

## Troubleshooting

### Connection Refused

Ensure Kafka is running and healthy:
```bash
docker-compose ps
docker-compose logs kafka
```

### Topic Not Found

Enable auto-create topics or create manually:
```bash
docker exec kafka-sample kafka-topics --bootstrap-server localhost:9092 --create --topic sensor-readings --partitions 3 --replication-factor 1
```

### Consumer Not Receiving Messages

Check consumer group status:
```bash
docker exec kafka-sample kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group dispatch-sensor-consumer
```

## Related Samples

- [RabbitMQ](../RabbitMQ/) - RabbitMQ transport
- [MultiBusSample](../MultiBusSample/) - Multiple transports in one application
- [MultiTransport](../MultiTransport/) - Multi-transport patterns

## Learn More

- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [Confluent Kafka .NET Client](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
- [Excalibur.Dispatch.Transport.Kafka Package](../../../src/Dispatch/Excalibur.Dispatch.Transport.Kafka/)
- [CloudEvents Specification](https://cloudevents.io/)

