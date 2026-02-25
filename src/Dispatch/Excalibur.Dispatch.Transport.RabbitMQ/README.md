# Excalibur.Dispatch.Transport.RabbitMQ

RabbitMQ transport implementation for the Excalibur framework, providing reliable message queuing with advanced features including dead letter handling, CloudEvents support, and automatic recovery.

## Overview

This package provides RabbitMQ integration for Excalibur.Dispatch, enabling:

- **Message Publishing & Consuming**: Full support for exchanges, queues, and routing
- **CloudEvents Support**: DoD-compliant structured and binary mode CloudEvents
- **Reliability Features**: Dead letter queues, publisher confirms, automatic recovery
- **Batching**: Configurable batch processing for high-throughput scenarios
- **Encryption**: Optional message-level encryption support

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

## Configuration

### Connection Options

#### Using Connection String

```csharp
services.AddRabbitMqMessageBus(options =>
{
    options.ConnectionString = "amqp://user:password@localhost:5672/vhost";
});
```

#### Using Individual Properties

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.ConnectionString = "amqp://localhost";
    options.Exchange = "dispatch.events";
    options.QueueName = "my-service-queue";
    options.RoutingKey = "orders.*";
});
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
RABBITMQ__CONNECTIONSTRING=amqp://user:password@rabbitmq:5672/
RABBITMQ__EXCHANGE=dispatch.events
RABBITMQ__QUEUENAME=my-service-queue
```

```csharp
services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
```

### Authentication

#### Username/Password (Connection String)

```csharp
options.ConnectionString = "amqp://username:password@hostname:5672/vhost";
```

#### TLS/SSL Configuration

For production environments, enable TLS:

```csharp
options.ConnectionString = "amqps://user:password@hostname:5671/";
```

#### Certificate-Based Authentication

When using client certificates, configure the connection factory directly through the RabbitMQ.Client library before registering services.

### Message Configuration

#### Exchange and Queue Settings

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Exchange configuration
    options.Exchange = "dispatch.events";

    // Queue configuration
    options.QueueName = "order-processor";
    options.QueueDurable = true;      // Survive broker restart (default: true)
    options.QueueExclusive = false;   // Allow multiple consumers (default: false)
    options.QueueAutoDelete = false;  // Keep queue when consumers disconnect (default: false)

    // Routing
    options.RoutingKey = "orders.#";  // Wildcard routing pattern
});
```

#### Consumer Settings

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Prefetch (QoS)
    options.PrefetchCount = 100;      // Messages to prefetch (default: 100)
    options.PrefetchGlobal = false;   // Per-consumer prefetch (default: false)

    // Acknowledgment
    options.AutoAck = false;          // Manual acknowledgment (default: false)
    options.RequeueOnReject = true;   // Requeue rejected messages (default: true)

    // Batching
    options.MaxBatchSize = 50;        // Max messages per batch (default: 50)
    options.MaxBatchWaitMs = 500;     // Max wait for batch (default: 500ms)

    // Consumer identification
    options.ConsumerTag = "order-service-1";
});
```

#### CloudEvents Support

Enable CloudEvents for interoperable event-driven architectures:

```csharp
services.AddRabbitMqMessageBus(options =>
{
    options.ConnectionString = "amqp://localhost";
    options.EnableCloudEvents = true;  // Default: true

    // CloudEvents-specific settings
    options.ExchangeType = ExchangeType.Topic;
    options.Persistence = MessagePersistence.Persistent;
    options.RoutingStrategy = RoutingStrategy.EventType;
});
```

For DoD-compliant validation:

```csharp
services.AddRabbitMqCloudEventValidation(enableDoDCompliance: true);
```

#### Encryption

Enable message-level encryption for sensitive data:

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.EnableEncryption = true;
});
```

### Retry Policies

#### Dead Letter Queue Configuration

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Enable dead letter handling
    options.EnableDeadLetterExchange = true;
    options.DeadLetterExchange = "dispatch.dlx";
    options.DeadLetterRoutingKey = "failed.orders";
});
```

#### Connection Recovery

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Connection resilience
    options.ConnectionTimeoutSeconds = 30;           // Connection timeout (default: 30)
    options.AutomaticRecoveryEnabled = true;         // Auto-reconnect (default: true)
    options.NetworkRecoveryIntervalSeconds = 10;     // Recovery interval (default: 10)
});
```

## Health Checks

### Registration

The transport implements `ITransportHealthChecker` for integration with ASP.NET Core health checks:

```csharp
services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready", "messaging" });
```

### Configuration

Configure health check behavior:

```csharp
services.Configure<RabbitMqHealthCheckOptions>(options =>
{
    options.Timeout = TimeSpan.FromSeconds(5);
    options.IncludeQueueMetrics = true;
});
```

### Custom Health Check Implementation

```csharp
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly ITransportHealthChecker _healthChecker;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _healthChecker.CheckQuickHealthAsync(cancellationToken);

        return result.Status switch
        {
            TransportHealthStatus.Healthy => HealthCheckResult.Healthy(),
            TransportHealthStatus.Degraded => HealthCheckResult.Degraded(result.Description),
            _ => HealthCheckResult.Unhealthy(result.Description)
        };
    }
}
```

## Production Considerations

### Scaling

#### Horizontal Scaling

- Use **competing consumers** pattern with shared queue name
- Set `QueueExclusive = false` to allow multiple consumers
- Adjust `PrefetchCount` based on processing time (lower for slow consumers)

#### High Availability

- Deploy RabbitMQ in **cluster mode** with mirrored queues
- Use `QueueDurable = true` for message persistence
- Enable `AutomaticRecoveryEnabled` for automatic reconnection

### Performance Tuning

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // High-throughput configuration
    options.PrefetchCount = 250;         // Increase for fast processors
    options.MaxBatchSize = 100;          // Larger batches
    options.MaxBatchWaitMs = 100;        // Shorter wait times
    options.AutoAck = false;             // Keep manual ack for reliability
});
```

### Monitoring and Alerting

Key metrics to monitor:

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| Queue Depth | Messages waiting | > 10,000 |
| Consumer Utilization | Active consumers | < 1 |
| Message Rate | Messages/second | Baseline deviation |
| Unacked Messages | Pending acknowledgments | > PrefetchCount × 2 |

### Security Best Practices

1. **Use TLS** (`amqps://`) in production
2. **Rotate credentials** regularly using environment variables
3. **Limit permissions** per virtual host and user
4. **Enable encryption** for sensitive payloads
5. **Use separate virtual hosts** for different environments

## Troubleshooting

### Common Issues

#### Connection Refused

```
RabbitMQ.Client.Exceptions.BrokerUnreachableException: None of the specified endpoints were reachable
```

**Solutions:**
- Verify RabbitMQ is running: `rabbitmqctl status`
- Check hostname/port in connection string
- Verify firewall allows port 5672 (or 5671 for TLS)
- Confirm credentials are correct

#### Authentication Failed

```
RabbitMQ.Client.Exceptions.AuthenticationFailureException: ACCESS_REFUSED
```

**Solutions:**
- Verify username/password
- Check virtual host permissions: `rabbitmqctl list_permissions -p /vhost`
- Ensure user has access to the virtual host

#### Queue Not Found

```
RabbitMQ.Client.Exceptions.OperationInterruptedException: NOT_FOUND - no queue
```

**Solutions:**
- Queue may not be declared; enable auto-declaration
- Check queue name spelling
- Verify the queue exists: `rabbitmqctl list_queues`

#### Message Redelivery Loop

Messages continuously redelivered without processing.

**Solutions:**
- Check for exceptions in message handler
- Verify `RequeueOnReject` setting matches desired behavior
- Configure dead letter queue to capture failed messages
- Review `PrefetchCount` to avoid overwhelming consumers

### Logging Configuration

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Dispatch.Transport.RabbitMQ": "Debug",
      "RabbitMQ.Client": "Warning"
    }
  }
}
```

### Debug Tips

1. **Enable RabbitMQ Management Plugin**: Access web UI at `http://localhost:15672`
2. **Monitor connections**: `rabbitmqctl list_connections`
3. **Check channel status**: `rabbitmqctl list_channels`
4. **View queue bindings**: `rabbitmqctl list_bindings`
5. **Trace messages**: Enable RabbitMQ Firehose tracer for message inspection

## Complete Configuration Reference

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Connection
    options.ConnectionString = "amqp://user:pass@localhost:5672/";
    options.ConnectionTimeoutSeconds = 30;
    options.AutomaticRecoveryEnabled = true;
    options.NetworkRecoveryIntervalSeconds = 10;

    // Exchange
    options.Exchange = "dispatch.events";

    // Queue
    options.QueueName = "my-service";
    options.QueueDurable = true;
    options.QueueExclusive = false;
    options.QueueAutoDelete = false;
    options.QueueArguments = new Dictionary<string, object>
    {
        ["x-message-ttl"] = 86400000,  // 24 hours
        ["x-max-length"] = 100000
    };

    // Routing
    options.RoutingKey = "orders.#";

    // Consumer
    options.PrefetchCount = 100;
    options.PrefetchGlobal = false;
    options.AutoAck = false;
    options.RequeueOnReject = true;
    options.ConsumerTag = "order-processor-1";

    // Batching
    options.MaxBatchSize = 50;
    options.MaxBatchWaitMs = 500;

    // Dead Letter
    options.EnableDeadLetterExchange = true;
    options.DeadLetterExchange = "dispatch.dlx";
    options.DeadLetterRoutingKey = "failed";

    // Security
    options.EnableEncryption = false;
});
```

## See Also

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [CloudEvents Specification](https://cloudevents.io/)
