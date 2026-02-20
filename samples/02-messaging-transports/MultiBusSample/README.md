# MultiBusSample

This sample registers both RabbitMQ and Kafka message buses and demonstrates how routing rules
can direct different message types to each broker. Multiple instances of the application can run
simultaneously, sharing the same inbox and outbox stores.

## Running the sample

1. Start RabbitMQ and Kafka using the provided `docker-compose.yml`:

```bash
docker compose up
```

2. In separate terminals run the application. Launch two or more instances to simulate multiple hosts:

```bash
cd samples/MultiBusSample
dotnet run
```

Watch the console output from each instance. `RabbitPingEvent` messages are routed through the
RabbitMQ bus while `KafkaPingEvent` messages are routed through the Kafka bus. Because the hosts
share the same stores, each message is processed exactly once regardless of how many instances are running.
