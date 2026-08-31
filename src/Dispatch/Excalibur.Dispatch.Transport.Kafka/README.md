# Excalibur.Dispatch.Transport.Kafka

Apache Kafka transport implementation for the Excalibur framework, providing high-throughput, distributed event streaming with exactly-once semantics and CloudEvents support.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.Kafka` | Starter | + Resilience (Polly) + Observability |

> **Tip:** If you are getting started, install `Excalibur.Dispatch.Kafka` instead of this package directly. It includes production-ready defaults.

## Overview

This package provides Apache Kafka integration for Excalibur.Dispatch, enabling:

- **High-Throughput Messaging**: Distributed streaming with partitioning and consumer groups
- **Exactly-Once Semantics**: Idempotent producers and transactional messaging
- **CloudEvents Support**: Standards-compliant structured event formatting. Registering the bundled mapper is annotated for trimming and ahead-of-time builds (it serializes payloads with reflection-based JSON); supply your own `ICloudEventMapper<TTransportMessage>` over a source-generated serializer to avoid the requirement.
- **Flexible Partitioning**: Multiple strategies including correlation ID, tenant ID, and round-robin
- **Compression**: Multiple algorithms (Snappy, LZ4, ZSTD, GZIP)
- **TLS Security**: Secure connections with SSL/SASL authentication

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.Kafka
```

## Quick Start

Register the transport with the fluent builder. This is the primary entry point: it wires the
producer, consumer, subscriber and transport adapter, and populates `KafkaOptions` and
`KafkaCloudEventOptions` from what you configure here.

```csharp
using Confluent.Kafka;

services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka =>
    {
        kafka.BootstrapServers("kafka.example.com:9093")
             .UseSecurityProtocol(SecurityProtocol.SaslSsl)
             .ConfigureConsumer(consumer => consumer.GroupId("my-consumer-group"))
             .ConfigureProducer(producer => producer.Acks(KafkaAckLevel.All))
             .MapTopic<OrderCreated>("orders-events");
    });
});
```

Without the dispatch builder, register the transport directly:

```csharp
services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("kafka.example.com:9093")
         .UseSecurityProtocol(SecurityProtocol.SaslSsl)
         .ConfigureConsumer(consumer => consumer.GroupId("my-consumer-group"));
});
```

Pass a name to run more than one Kafka cluster in the same host:

```csharp
services.AddKafkaTransport("analytics", kafka =>
{
    kafka.BootstrapServers("analytics-cluster:9093")
         .UseSecurityProtocol(SecurityProtocol.SaslSsl)
         .MapTopic<MetricEvent>("metrics-events");
});
```

> **TLS is required by default.** `KafkaOptions.RequireTls` defaults to `true`, and building a
> client against a broker whose effective security protocol is not `Ssl` or `SaslSsl` throws
> `TransportSecurityException` rather than connecting in the clear. For a local broker, opt out
> explicitly with `kafka.RequireTls(false)` on the builder, or `options.RequireTls = false` on
> `KafkaOptions`. See [Security Best Practices](#security-best-practices).

## Configuration

The fluent builder above is the supported way to configure the transport. `services.Configure<T>`
is available for settings the builder does not expose, and for binding from `IConfiguration`.

> **Ordering matters.** `AddKafkaTransport` registers its own `KafkaOptions` configuration
> delegate, which sets `BootstrapServers`, `SecurityProtocol`, `RequireTls` and the consumer
> settings. Options delegates run in registration order, so a `services.Configure<KafkaOptions>`
> call must come **after** `AddKafkaTransport` for those properties to survive.

### Connection Options

#### Basic Configuration

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "localhost:9092";
    options.Topic = "my-events";
    options.ConsumerGroup = "my-consumer-group";
    options.GroupProtocol = GroupProtocol.Consumer;

    // A plaintext local broker is refused unless TLS is waived explicitly.
    options.RequireTls = false;
});
```

#### Multiple Brokers

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "broker1:9092,broker2:9092,broker3:9092";
    options.Topic = "my-events";
    options.ConsumerGroup = "my-consumer-group";
});
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
KAFKA__BOOTSTRAPSERVERS=broker1:9093,broker2:9093
KAFKA__TOPIC=my-events
KAFKA__CONSUMERGROUP=my-consumer-group
KAFKA__CONSUMER__MAXBATCHSIZE=100
```

```csharp
services.Configure<KafkaOptions>(configuration.GetSection("Kafka"));
```

### Authentication

#### SASL/PLAIN (Username/Password)

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "kafka.example.com:9093";
    options.AdditionalConfig["security.protocol"] = "SASL_SSL";
    options.AdditionalConfig["sasl.mechanism"] = "PLAIN";
    options.AdditionalConfig["sasl.username"] = "your-api-key";
    options.AdditionalConfig["sasl.password"] = "your-api-secret";
});
```

#### SASL/SCRAM (Recommended for Production)

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "kafka.example.com:9093";
    options.AdditionalConfig["security.protocol"] = "SASL_SSL";
    options.AdditionalConfig["sasl.mechanism"] = "SCRAM-SHA-512";
    options.AdditionalConfig["sasl.username"] = "your-username";
    options.AdditionalConfig["sasl.password"] = "your-password";
});
```

#### TLS/SSL Only (No SASL)

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "kafka.example.com:9093";
    options.AdditionalConfig["security.protocol"] = "SSL";
    options.AdditionalConfig["ssl.ca.location"] = "/path/to/ca.crt";
    options.AdditionalConfig["ssl.certificate.location"] = "/path/to/client.crt";
    options.AdditionalConfig["ssl.key.location"] = "/path/to/client.key";
    options.AdditionalConfig["ssl.key.password"] = "key-password";
});
```

#### AWS MSK with IAM Authentication

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "b-1.mycluster.abc123.kafka.us-east-1.amazonaws.com:9098";
    options.AdditionalConfig["security.protocol"] = "SASL_SSL";
    options.AdditionalConfig["sasl.mechanism"] = "AWS_MSK_IAM";
    options.AdditionalConfig["sasl.jaas.config"] =
        "software.amazon.msk.auth.iam.IAMLoginModule required;";
});
```

### Message Configuration

#### Consumer Settings

```csharp
services.Configure<KafkaOptions>(options =>
{
    // Connection
    options.BootstrapServers = "localhost:9092";
    options.Topic = "my-events";
    options.ConsumerGroup = "my-consumer-group";

    // Offset management
    options.Consumer.EnableAutoCommit = false;       // Manual commits (recommended)
    options.Consumer.AutoCommitIntervalMs = 5000;    // If auto-commit is enabled
    options.Consumer.AutoOffsetReset = "latest";     // "earliest", "latest", or "none"

    // Batching
    options.Consumer.MaxBatchSize = 100;             // Messages per batch
    options.Consumer.MaxBatchWaitMs = 1000;          // Max wait for batch (ms)

    // Performance
    options.Consumer.QueuedMinMessages = 1000;       // Prefetch per partition
    options.Consumer.MaxConcurrentCommits = 10;      // Concurrent offset commits

    // Session management
    options.Consumer.SessionTimeoutMs = 30000;       // Consumer session timeout
    options.Consumer.MaxPollIntervalMs = 300000;     // Max time between polls

    // Partition handling
    options.Consumer.EnablePartitionEof = false;     // EOF detection
    options.Consumer.PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky;

    // Payload guard
    options.Consumer.MaxPayloadBytes = 4 * 1024 * 1024;

    // Security
    options.RequireTls = true;                       // Refuse a non-TLS broker connection
});
```

#### Producer Settings

Broker/consumer settings live on `KafkaOptions`; CloudEvent publishing settings
(acknowledgment, compression, partitioning) live on `KafkaCloudEventOptions` and its
nested `Producer` object.

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "kafka.example.com:9093";
    options.ConsumerGroup = "my-consumer-group";
});
```

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    // Partitioning
    options.PartitioningStrategy = KafkaPartitioningStrategy.RoundRobin;

    // Producer publishing settings
    options.Producer.AcknowledgmentLevel = KafkaAckLevel.All;      // All, Leader, or None
    options.Producer.CompressionType = KafkaCompressionType.Snappy;
});
```

#### CloudEvents Configuration

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    // Topic settings
    options.DefaultTopic = "cloud-events";
    options.DefaultPartitionCount = 3;
    options.DefaultReplicationFactor = 3;
    options.AutoCreateTopics = false;

    // Partitioning strategy
    options.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;

    // Exactly-once semantics
    options.Producer.EnableIdempotentProducer = true;
    options.Producer.EnableTransactions = false;
    options.Producer.TransactionalId = null;

    // Acknowledgment
    options.Producer.AcknowledgmentLevel = KafkaAckLevel.All;

    // Message size
    options.Producer.MaxMessageSizeBytes = 1048576;     // 1 MB

    // Compression
    options.Producer.EnableCompression = true;
    options.Producer.CompressionType = KafkaCompressionType.Snappy;
    options.Producer.CompressionThreshold = 1024;       // Compress messages > 1 KB

    // Consumer settings
    options.ConsumerGroupId = "cloudevents-consumer";
    options.OffsetReset = KafkaOffsetReset.Latest;

    // Retry settings
    options.Producer.RetrySettings = new KafkaRetryOptions
    {
        MaxRetryAttempts = 3,
        RetryDelay = TimeSpan.FromMilliseconds(100),
        MaxRetryDelay = TimeSpan.FromSeconds(30),
        UseExponentialBackoff = true,
        BackoffMultiplier = 2.0,
        UseJitter = true
    };
});
```

### Partitioning Strategies

| Strategy | Use Case |
|----------|----------|
| `CorrelationId` | Order preservation per correlation |
| `TenantId` | Multi-tenant isolation |
| `UserId` | User-scoped ordering |
| `Source` | Source-based routing |
| `Type` | Event type-based routing |
| `EventId` | Unique event distribution |
| `RoundRobin` | Even distribution |
| `Custom` | Custom partition key from extensions |

The default is `CorrelationId`.

### Compression Types

| Type | Characteristics |
|------|-----------------|
| `None` | No compression (fastest, largest) |
| `Gzip` | Good compression, slower |
| `Snappy` | Balanced speed/compression (recommended) |
| `Lz4` | Fastest compression |
| `Zstd` | Best compression ratio |

### Retry Policies

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    options.Producer.RetrySettings = new KafkaRetryOptions
    {
        MaxRetryAttempts = 3,                         // Retry attempts
        RetryDelay = TimeSpan.FromMilliseconds(100),  // Initial delay
        MaxRetryDelay = TimeSpan.FromSeconds(30),     // Maximum delay
        UseExponentialBackoff = true,                 // Exponential backoff
        BackoffMultiplier = 2.0,                      // Backoff factor
        UseJitter = true                              // Add randomization
    };
});
```

## Health Checks

The package ships a broker-connectivity probe. Register it on the standard health checks builder;
it reuses the producer already registered by `AddKafkaTransport`, so there is nothing further to
configure.

```csharp
services.AddHealthChecks()
    .AddKafkaTransportHealthCheck();
```

Override the name, failure status, or tags:

```csharp
services.AddHealthChecks()
    .AddKafkaTransportHealthCheck(
        name: "kafka-transport",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready", "messaging"]);
```

The check reports unhealthy when no producer has been established, and otherwise probes broker
metadata. Register the transport before the health check so that a producer exists to probe.

## Production Considerations

### Scaling

#### Partition Strategy

- **Partitions = Parallelism**: More partitions allow more concurrent consumers
- **Consumer Group Size**: Max consumers = number of partitions
- **Partition Key**: Choose keys that distribute load evenly

```csharp
// High-throughput configuration
services.Configure<KafkaCloudEventOptions>(options =>
{
    options.DefaultPartitionCount = 12;          // More partitions for parallelism
    options.DefaultReplicationFactor = 3;        // High availability
    options.PartitioningStrategy = KafkaPartitioningStrategy.RoundRobin;
});
```

#### Consumer Scaling

- Each partition can have only ONE consumer per consumer group
- Scale consumers up to partition count
- Use multiple consumer groups for different processing needs

### Performance Tuning

#### High-Throughput Producer

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.Consumer.MaxBatchSize = 500;          // Larger batches
    options.Consumer.MaxBatchWaitMs = 50;         // Shorter wait time
    options.Consumer.QueuedMinMessages = 5000;    // More prefetch
});
```

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    options.Producer.AcknowledgmentLevel = KafkaAckLevel.Leader;  // Faster acks (less durable)
    options.Producer.EnableCompression = true;
    options.Producer.CompressionType = KafkaCompressionType.Lz4;  // Fast compression
    options.Producer.EnableIdempotentProducer = false;            // Disable for max throughput
});
```

#### Low-Latency Consumer

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.Consumer.MaxBatchSize = 1;            // Process immediately
    options.Consumer.MaxBatchWaitMs = 0;          // No batching delay
    options.Consumer.SessionTimeoutMs = 10000;    // Faster failure detection
    options.Consumer.MaxPollIntervalMs = 60000;   // Shorter poll interval
});
```

#### Exactly-Once Processing

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    options.Producer.EnableIdempotentProducer = true;    // Idempotent writes
    options.Producer.EnableTransactions = true;          // Transactional processing
    options.Producer.TransactionalId = "my-service-txn"; // Unique transaction ID
    options.Producer.AcknowledgmentLevel = KafkaAckLevel.All;
});
```

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.Consumer.EnableAutoCommit = false;    // Manual offset commits
});
```

### Monitoring and Alerting

Key Kafka metrics to monitor:

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| Consumer Lag | Messages behind latest offset | > 10,000 |
| Under-Replicated Partitions | Partitions with insufficient replicas | > 0 |
| Request Rate | Requests per second | Baseline deviation |
| Network Throughput | Bytes in/out per second | Approaching network limit |
| Disk Usage | Broker disk utilization | > 80% |
| ISR Shrink Rate | In-sync replica changes | > 0 (investigate) |

### Security Best Practices

1. **Use TLS** for all connections in production
2. **Use SASL/SCRAM** instead of PLAIN for authentication
3. **Enable ACLs** to restrict topic access per client
4. **Rotate credentials** regularly
5. **Use separate credentials** per service/environment
6. **Enable audit logging** for compliance

### Cost Optimization (Cloud Providers)

1. **Right-size partitions**: More partitions = higher cost
2. **Set retention policies**: Don't retain data longer than needed
3. **Enable compression**: Reduces storage and network costs
4. **Use tiered storage**: Move cold data to cheaper storage (Confluent Cloud)
5. **Monitor throughput**: Pay for what you use

## Troubleshooting

### Common Issues

#### Connection Refused

```
Confluent.Kafka.KafkaException: Local: Broker transport failure
```

**Solutions:**
- Verify bootstrap servers are correct and reachable
- Check firewall allows port 9092 (or 9093 for TLS)
- Verify Kafka broker is running: `kafka-broker-api-versions --bootstrap-server localhost:9092`
- Check DNS resolution for broker hostnames

#### Authentication Failed

```
Confluent.Kafka.KafkaException: SASL authentication failed
```

**Solutions:**
- Verify username/password are correct
- Check SASL mechanism matches broker configuration
- Ensure security protocol is correct (SASL_SSL vs SASL_PLAINTEXT)
- Verify SSL certificates if using TLS

#### Consumer Group Rebalancing

```
Confluent.Kafka.KafkaException: Group coordinator not available
```

**Solutions:**
- Increase `SessionTimeoutMs` for slow-processing consumers
- Reduce `MaxPollIntervalMs` if processing takes too long
- Check broker logs for coordinator issues
- Verify consumer group ID is unique per application instance

#### Message Too Large

```
Confluent.Kafka.KafkaException: Message size too large
```

**Solutions:**
- Increase `message.max.bytes` on broker
- Increase `max.request.size` on producer
- Enable compression to reduce message size
- Split large messages into smaller chunks

#### Offset Out of Range

```
Confluent.Kafka.KafkaException: Offset out of range
```

**Solutions:**
- Set appropriate `AutoOffsetReset` policy
- Check topic retention settings
- Verify consumer group hasn't been idle too long
- Reset consumer group offsets if needed

### Logging Configuration

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Dispatch.Transport.Kafka": "Debug",
      "Confluent.Kafka": "Warning"
    }
  }
}
```

### Debug Tips

1. **Use Kafka CLI tools**:
   ```bash
   # List topics
   kafka-topics --bootstrap-server localhost:9092 --list

   # Describe consumer group
   kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group my-group

   # View topic messages
   kafka-console-consumer --bootstrap-server localhost:9092 --topic my-topic --from-beginning
   ```

2. **Enable librdkafka debug**:
   ```csharp
   options.AdditionalConfig["debug"] = "broker,topic,msg";
   ```

3. **Check consumer lag**:
   ```bash
   kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group my-group
   ```

4. **Monitor with JMX** or use Kafka UI tools like AKHQ, Conduktor, or Confluent Control Center

5. **Use Docker for local development**:
   ```yaml
   # docker-compose.yml
   services:
     kafka:
       image: confluentinc/cp-kafka:latest
       ports:
         - "9092:9092"
       environment:
         KAFKA_BROKER_ID: 1
         KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
         KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
         KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
   ```

## Complete Configuration Reference

Two options types cover the surface. `KafkaOptions` carries the broker connection, the security
posture and the consumer tuning; `KafkaCloudEventOptions` carries the CloudEvent topic,
partitioning and producer settings.

```csharp
services.Configure<KafkaOptions>(options =>
{
    // Connection
    options.BootstrapServers = "kafka.example.com:9093";
    options.Topic = "my-events";
    options.ConsumerGroup = "my-consumer-group";
    options.GroupProtocol = GroupProtocol.Consumer;

    // Security posture
    options.SecurityProtocol = SecurityProtocol.SaslSsl;
    options.RequireTls = true;

    // Offset management
    options.Consumer.EnableAutoCommit = false;
    options.Consumer.AutoCommitIntervalMs = 5000;
    options.Consumer.AutoOffsetReset = "latest";

    // Batching
    options.Consumer.MaxBatchSize = 100;
    options.Consumer.MaxBatchWaitMs = 1000;

    // Performance
    options.Consumer.QueuedMinMessages = 1000;
    options.Consumer.MaxConcurrentCommits = 10;

    // Session management
    options.Consumer.SessionTimeoutMs = 30000;
    options.Consumer.MaxPollIntervalMs = 300000;

    // Partitions
    options.Consumer.EnablePartitionEof = false;
    options.Consumer.PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky;

    // Payload guard
    options.Consumer.MaxPayloadBytes = 4 * 1024 * 1024;

    // Additional librdkafka config
    options.AdditionalConfig["socket.keepalive.enable"] = "true";
});
```

```csharp
services.Configure<KafkaCloudEventOptions>(options =>
{
    // Topics
    options.DefaultTopic = "cloud-events";
    options.DefaultPartitionCount = 3;
    options.DefaultReplicationFactor = 1;
    options.AutoCreateTopics = false;

    // Partitioning
    options.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;

    // Consumer
    options.ConsumerGroupId = "cloudevents-consumer";
    options.OffsetReset = KafkaOffsetReset.Latest;

    // Exactly-once
    options.Producer.EnableIdempotentProducer = true;
    options.Producer.EnableTransactions = false;
    options.Producer.TransactionalId = null;

    // Message settings
    options.Producer.AcknowledgmentLevel = KafkaAckLevel.All;
    options.Producer.MaxMessageSizeBytes = 1048576;

    // Compression
    options.Producer.EnableCompression = true;
    options.Producer.CompressionType = KafkaCompressionType.Snappy;
    options.Producer.CompressionThreshold = 1024;

    // Retry
    options.Producer.RetrySettings = new KafkaRetryOptions
    {
        MaxRetryAttempts = 3,
        RetryDelay = TimeSpan.FromMilliseconds(100),
        MaxRetryDelay = TimeSpan.FromSeconds(30),
        UseExponentialBackoff = true,
        BackoffMultiplier = 2.0,
        UseJitter = true
    };
});
```

## See Also

- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [Confluent Kafka .NET Client](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
- [CloudEvents Specification](https://cloudevents.io/)
