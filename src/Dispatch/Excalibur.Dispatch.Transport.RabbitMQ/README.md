# Excalibur.Dispatch.Transport.RabbitMQ

RabbitMQ transport implementation for the Excalibur framework, providing reliable message queuing with advanced features including dead letter handling, CloudEvents publishing and decoding, and automatic recovery.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.RabbitMQ` | Starter | + Resilience (Polly) + Observability |

> **Tip:** If you are getting started, install `Excalibur.Dispatch.RabbitMQ` instead of this package directly. It includes production-ready defaults.

## Overview

This package provides RabbitMQ integration for Excalibur.Dispatch, enabling:

- **Message Publishing & Consuming**: Full support for exchanges, queues, and routing
- **CloudEvents Support**: DoD-compliant structured and binary mode CloudEvents on outbound messages. Inbound messages are decoded automatically. On the send path, select payload serialization separately through core `AddCloudEvents`: use `AddCloudEvents(MyPayloadContext.Default)` with generated metadata for your payload types when trimming or publishing with Native AOT. The bundled encoder uses that configured serializer; a custom encoder is not required for metadata serialization. The parameterless core overload remains the annotated reflective choice. Validate transport dependencies and custom converters separately. Inbound decoding is trim-safe and ahead-of-time-safe, attaches the decoded event to the message rather than replacing it, and delivers every message in the batch — a malformed one arrives carrying a decode error instead of a decoded event, never dropped.
- **Reliability Features**: Dead letter queues, publisher confirms, automatic recovery
- **Batching**: Configurable batch processing; measure completed work and resource use

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

## Configuration

### Connection Options

#### Using Connection String

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration36
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport("rabbitmq", rmq =>
            rmq.ConnectionString("amqps://user:password@rabbitmq:5671/vhost"));
    }
}
```

The transport builder owns the live connection input. The former `RabbitMqOptions.Connection` copy and `RabbitMqConnectionOptions` type have been removed. Supplied URI structure and explicit default URI credentials are checked at registration; absent URI selects host/port parts. Actual internal factory creation rechecks structure and applies the existing TLS and parts-credential security posture. Caller-supplied channel/factory paths do not cause unused internal defaults to be dialed or eagerly validated for TLS.

Configure host parts with the same builder:

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration45
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport("rabbitmq", rmq => rmq.HostName("rabbitmq")
            .Port(5671).UseSsl().Credentials("app-user", "app-password"));
    }
}
```

#### Using Individual Properties

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration52
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport("rabbitmq", rmq =>
            rmq.HostName("rabbitmq").Port(5671).UseSsl().Credentials("app-user", "app-password"));
        services.Configure<RabbitMqOptions>("rabbitmq", options =>
        {
            options.Exchange = "dispatch.events";
            options.Queue.QueueName = "my-service-queue";
            options.RoutingKey = "orders.*";
        });
    }
}
```

#### Environment Variables

Configure via environment variables for containerized deployments:

```bash
RABBITMQ__CONNECTION__CONNECTIONSTRING=amqps://user:password@rabbitmq:5671/
RABBITMQ__EXCHANGE=dispatch.events
RABBITMQ__QUEUE__QUEUENAME=my-service-queue
```

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public static class RabbitMqRegistration73
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        var brokerUri = configuration["RabbitMQ:Connection:ConnectionString"]
            ?? throw new InvalidOperationException("RabbitMQ connection URI is required.");
        services.AddRabbitMQTransport("rabbitmq", rmq => rmq.ConnectionString(brokerUri));
        services.Configure<RabbitMqOptions>("rabbitmq", configuration.GetSection("RabbitMQ"));
    }
}
```

### Authentication

#### Username/Password (Connection String)

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration84
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport(rmq =>
            rmq.ConnectionString("amqps://username:password@hostname:5671/vhost"));
    }
}
```

#### TLS/SSL Configuration

For production environments, enable TLS:

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration93
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport(rmq =>
            rmq.ConnectionString("amqps://user:password@hostname:5671/"));
    }
}
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
    options.Queue.QueueName = "order-processor";
    options.Queue.QueueDurable = true;      // Survive broker restart (default: true)
    options.Queue.QueueExclusive = false;   // Allow multiple consumers (default: false)
    options.Queue.QueueAutoDelete = false;  // Keep queue when consumers disconnect (default: false)

    // Routing
    options.RoutingKey = "orders.#";  // Wildcard routing pattern
});
```

#### Consumer Settings

`RabbitMqOptions.Consumption` carries the inbound payload guard; over-limit messages are rejected
before the body is deserialized.

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.Consumption.MaxPayloadBytes = 4 * 1024 * 1024;  // default: 4 MiB; null to opt out
});
```

Prefetch (QoS) is a queue setting on the transport builder:

```csharp
services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("localhost")
       .ConfigureQueue(queue => queue
           .Name("order-processor")
           .PrefetchCount(100));   // default: 100
});
```

Acknowledgment is not configurable: the transport always consumes with manual acknowledgment, and
the `MessageAction` returned by the handler decides the delivery's fate -- `Acknowledge`, `Requeue`
(nack with requeue), or `Reject` (nack without requeue, so it reaches the dead-letter exchange when
one is configured).

#### CloudEvents Support

Enable CloudEvents formatting on published messages, for interoperable event-driven architectures.
Inbound messages are decoded automatically and need no registration — this call configures the
send path only:

```csharp
services.AddCloudEventsForRabbitMq(rabbitMq =>
{
    rabbitMq.Exchange.DefaultExchange = "dispatch.events";
    rabbitMq.Exchange.ExchangeType = RabbitMQExchangeType.Topic;
    rabbitMq.Exchange.Persistence = RabbitMqPersistence.Persistent;
    rabbitMq.Exchange.EnablePublisherConfirms = true;
});
```

For DoD-compliant validation:

```csharp
services.AddRabbitMqCloudEventValidation(enableDoDCompliance: true);
```

### Retry Policies

#### Dead Letter Queue Configuration

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    // Enable dead letter handling
    options.DeadLetter.EnableDeadLetterExchange = true;
    options.DeadLetter.DeadLetterExchange = "dispatch.dlx";
    options.DeadLetter.DeadLetterRoutingKey = "failed.orders";
});
```

#### Connection Recovery

```csharp
services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("localhost")
       .AutomaticRecovery(
           enabled: true,                                       // Auto-reconnect (default: true)
           networkRecoveryInterval: TimeSpan.FromSeconds(10));  // Recovery interval (default: 10s)
});
```

## Health Checks

The transport adapter implements `ITransportHealthChecker`, and this package ships the ASP.NET Core
health check that surfaces it. Register it on the standard health-checks builder:

```csharp
services.AddHealthChecks()
    .AddRabbitMqTransportHealthCheck(
        name: "rabbitmq-transport",
        tags: new[] { "ready", "messaging" });
```

To cover every registered transport with a single check instead, use the transport-agnostic
overload from `Excalibur.Dispatch`:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks(name: "transports", tags: new[] { "ready" });
```

You do not need to author a health check yourself -- both entry points resolve the registered
`ITransportHealthChecker` implementations for you.

## Production Considerations

### Scaling

#### Horizontal Scaling

- Use **competing consumers** pattern with shared queue name
- Set `QueueExclusive = false` to allow multiple consumers
- Adjust `PrefetchCount` based on processing time (lower for slow consumers)

#### High Availability

- Deploy RabbitMQ in **cluster mode** with mirrored queues
- Use `Queue.QueueDurable = true` for message persistence
- Enable `AutomaticRecovery` on the transport builder for automatic reconnection

### Performance Tuning

```csharp
services.AddRabbitMQTransport(rmq =>
{
    rmq.ConfigureQueue(queue => queue
        .Name("order-processor")
        .PrefetchCount(250));   // Increase for fast processors
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
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqRegistration342
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport("rabbitmq", rmq =>
            rmq.ConnectionString("amqps://user:pass@rabbitmq:5671/"));
        services.Configure<RabbitMqOptions>("rabbitmq", options =>
        {
            // Exchange
            options.Exchange = "dispatch.events";

            // Queue. QueueArguments is a populated dictionary, not a settable property.
            options.Queue.QueueName = "my-service";
            options.Queue.QueueDurable = true;
            options.Queue.QueueExclusive = false;
            options.Queue.QueueAutoDelete = false;
            options.Queue.QueueArguments["x-message-ttl"] = 86400000;  // 24 hours
            options.Queue.QueueArguments["x-max-length"] = 100000;

            // Routing
            options.RoutingKey = "orders.#";

            // Consumer
            options.Consumption.MaxPayloadBytes = null;  // No inbound payload cap

            // Dead Letter
            options.DeadLetter.EnableDeadLetterExchange = true;
            options.DeadLetter.DeadLetterExchange = "dispatch.dlx";
            options.DeadLetter.DeadLetterRoutingKey = "failed";
        });
    }
}
```

Connection recovery and prefetch are configured on the transport builder rather than on
`RabbitMqOptions`:

```csharp
services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("localhost")
       .AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.FromSeconds(10))
       .ConfigureQueue(queue => queue.Name("my-service").PrefetchCount(100));
});
```

## See Also

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [CloudEvents Specification](https://cloudevents.io/)

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
