---
sidebar_position: 4
title: RabbitMQ Transport
description: RabbitMQ transport for flexible routing and traditional messaging patterns
---

# RabbitMQ Transport
RabbitMQ transport for flexible message routing, work queues, and traditional pub/sub patterns.

## Before You Start

- **.NET 10.0**
- A running RabbitMQ server (or Docker: `docker run -p 5672:5672 -p 15672:15672 rabbitmq:management`)
- Familiarity with [transport concepts](./index.md) and [choosing a transport](./choosing-a-transport.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

:::tip One-Line Setup with Metapackage

For the fastest setup, use the **`Excalibur.Dispatch.RabbitMQ`** experience metapackage. It bundles the RabbitMQ transport with Polly resilience and OpenTelemetry observability in a single call:

```bash
dotnet add package Excalibur.Dispatch.RabbitMQ
```

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration29
{
    public static void Configure(IServiceCollection services)
    {
        services.AddDispatchRabbitMQ(rmq =>
        {
            rmq.ConnectionString("amqps://app-user:configured-password@rabbitmq:5671/");
        });
    }
}
```

`AddDispatchRabbitMQ` calls `AddDispatch` internally and configures `UseRabbitMQ`, `UseResilience`, and `UseObservability`. Pass an optional second parameter (`Action<IDispatchBuilder>`) for additional pipeline configuration. See [Package Guide](../package-guide.md#experience-metapackages) for details.
:::

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration42
{
    public static void Configure(IServiceCollection services, System.Reflection.Assembly handlerAssembly)
    {
        services.AddPluggableSerialization(); // Transports don't seat a default serializer

        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(handlerAssembly);
            dispatch.UseRabbitMQ(rmq =>
            {
                rmq.ConnectionString("amqps://app-user:configured-password@rabbitmq:5671/")
                   .ConfigureExchange(exchange => exchange.Name("dispatch.events").Type(RabbitMQExchangeType.Topic))
                   .ConfigureCloudEvents(ce => ce.Exchange.EnablePublisherConfirms = true);
            });
        });
    }
}
```

### Standalone Registration
```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration58
{
    public static void Configure(IServiceCollection services, System.Reflection.Assembly handlerAssembly)
    {
        services.AddPluggableSerialization(); // Transports don't seat a default serializer

        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(handlerAssembly);
        });

        services.AddRabbitMQTransport(rmq =>
        {
            rmq.ConnectionString("amqps://app-user:configured-password@rabbitmq:5671/")
               .ConfigureExchange(exchange => exchange.Name("dispatch.events").Type(RabbitMQExchangeType.Topic))
               .ConfigureCloudEvents(ce => ce.Exchange.EnablePublisherConfirms = true);
        });
    }
}
```

RabbitMQ registers a keyed `IMessageBus` named `rabbitmq`:
```csharp
var bus = serviceProvider.GetRequiredKeyedService<IMessageBus>("rabbitmq");
```

### Named publishers and message destinations

Use `AddRabbitMQTransport("orders", ...)` or `UseRabbitMQ("orders", ...)` to configure a
named publisher. Resolve it through `IMessageBusProvider.GetRemoteMessageBus("orders")`.
Each name uses its own options and default connection/channel. Explicit SDK overrides may
intentionally share resources; the publisher borrows those resources.

Resolving `IDispatcher`, or using it only for local messages, does not open the named
RabbitMQ publisher's connection. A remote dispatch resolves the selected publisher on first
use; connection failures then follow the existing dispatch error policy. A successful bus is
reused. This does not defer explicit resolution of a remote bus, SDK channel or connection,
and hosted receivers may connect when the host starts. Consumer-supplied SDK resources keep
their existing borrowed-resource ownership rules.

`MapExchange<TMessage>("events")` selects the exchange for the exact runtime message type,
retaining the configured publication routing key. `MapQueue<TMessage>("orders")` alone
publishes through RabbitMQ's default exchange (`""`) with the queue name as its routing key.
Unmapped types retain the configured default exchange and routing key. These rules also
apply to CloudEvent publication. Exchange and queue prefixes apply to both declarations and mappings.

When both mappings select a custom exchange and queue, an explicit nonempty publication
routing key takes precedence. Otherwise, the publisher infers a key only from one unique
literal binding between that exchange and queue. Topic wildcard patterns are not publication
keys; ambiguous or wildcard-only bindings require an explicit key. Direct exchange keys are
literal, including `*` and `#`. Fanout and headers exchanges keep their broker routing
semantics; a queue mapping does not make their publication exclusive to that queue.

Mapping configuration is captured at registration. Configure mappings before completing the
registration callback; later mutations of a retained builder do not alter the publisher's routes.

## Transport Security

The transport refuses to build a connection that would carry your credentials and message payloads in
the clear. The refusal happens when the connection factory is built -- so a plaintext registration fails
at startup, where it is wired, rather than at the first message.

A connection carries TLS when either spelling says so:

```csharp
// The amqps scheme. The expected peer certificate name comes from the host.
rmq.ConnectionString("amqps://user:pass@rabbitmq:5671/vhost");

// Or host and port with explicit TLS settings.
rmq.HostName("rabbitmq").Port(5671)
   .Credentials("user", "pass")
   .UseSsl(ssl =>
   {
       ssl.ServerName = "rabbitmq.internal";       // defaults to the host being dialled
       ssl.CertificatePath = "/etc/ssl/client.p12";
       ssl.CertificatePassphrase = passphrase;
   });
```

The two combine. Certificate settings supplied through `UseSsl` are added to what the connection string
already resolved, so an `amqps://` string keeps the peer name it derived from the host.

A local broker with no certificate is a real configuration, and it has to be asked for:
Use a broker provisioned with nondefault credentials; disabling TLS does not permit `guest:guest`.

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration138
{
    public static void Configure(IServiceCollection services)
    {
        services.AddRabbitMQTransport("local-development", rmq =>
            rmq.ConnectionString("amqp://app-user:configured-password@localhost:5672/")
               .RequireTls(false));   // credentials and payloads travel in the clear
    }
}
```

Every AMQP client the transport creates -- the connection, its channels, the senders, receivers,
subscribers, the dead-letter queue manager and the health checks -- is reached through the one
connection factory this posture gates, so opting out is the only way past it.

## Configuration

### Fluent Builder Configuration

:::tip Start simple

For most applications, the Quick Start above is all you need. The fluent builder below is for advanced scenarios (custom exchanges, queue bindings, CloudEvents routing).
:::

Configure RabbitMQ transport using the fluent builder:

```csharp
services.AddPluggableSerialization(); // Transports don't seat a default serializer
services.AddRabbitMQTransport(rmq =>
{
    rmq.ConnectionString("amqps://user:pass@rabbitmq:5671/vhost")
       .ConfigureExchange(exchange =>
       {
           exchange.Name("dispatch.events")
                   .Type(RabbitMQExchangeType.Topic)
                   .Durable(true)
                   .AutoDelete(false);
       })
       .ConfigureQueue(queue =>
       {
           queue.Name("order-handlers")
                .Durable(true)
                .PrefetchCount(100);
       })
       .ConfigureBinding(binding =>
       {
           binding.Exchange("dispatch.events")
                  .Queue("order-handlers")
                  .RoutingKey("orders.*");
       })
       .ConfigureCloudEvents(ce =>
       {
           ce.ExchangeType = RabbitMQExchangeType.Topic;
           ce.Persistence = RabbitMqPersistence.Persistent;
           ce.RoutingStrategy = RabbitMqRoutingStrategy.EventType;
       });
});
```

### Broker Options

:::tip When do I need this?

Use `RabbitMqOptions` when you need fine-grained control over queue arguments, consumer behavior, or dead letter exchanges. The fluent builder above covers most scenarios.
:::

:::warning Removed dead connection options

`RabbitMqOptions.Connection` and `RabbitMqConnectionOptions` have been removed. Configure the actual
broker URI or host/port/virtual-host/credentials through `AddRabbitMQTransport` as shown above.
The live `RabbitMQConnectionOptions` belongs to that builder and remains supported. Supplied URI
structure and explicit default URI credentials are checked during registration, before a host starts;
missing URI selects host parts, whose host and port must be valid. Structural validation raises
`OptionsValidationException` naming the live connection type and transport. TLS and default
host-parts credentials retain their existing lazy typed security refusal when the internal factory is
actually constructed. A caller-supplied channel does not require unused internal connection defaults.

:::

Configure low-level broker behavior via `RabbitMqOptions`:

This example names the transport `"orders-processing"` and declares no explicit builder queue.
The keyed subscriber therefore uses that transport name as its queue; the low-level options below
use the same name and declare its TTL and dead-letter arguments once. Setting
`RabbitMqOptions.Queue.QueueName` alone does not retarget a subscriber whose transport name or
explicit builder queue differs.

The examples below apply the same low-level settings to **all** named and unnamed RabbitMQ options
instances with `ConfigureAll`. Register them after the transport builder. Plain
`Configure<RabbitMqOptions>(...)` only configures the unnamed instance, while topology and subscriber
setup read the instance named for the transport (`"rabbitmq"` by default). For distinct per-transport
topology, use each named transport builder rather than copying these global settings unchanged.

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration225
{
    public static void Configure(IServiceCollection services)
    {
        // The connection itself is configured on the builder, not in this block.
        services.AddRabbitMQTransport("orders-processing", rmq =>
            rmq.ConnectionString("amqps://user:pass@rabbitmq:5671/vhost"));

        services.ConfigureAll<RabbitMqOptions>(options =>
        {
            options.Exchange = "dispatch.events";
            options.RoutingKey = "orders.#";
            options.Queue.QueueName = "orders-processing";

            // Queue behavior
            options.Queue.QueueDurable = true;
            options.Queue.QueueExclusive = false;
            options.Queue.QueueAutoDelete = false;
            options.Queue.QueueArguments["x-message-ttl"] = 86400000; // 24 hours

            // Consumer ingress guard: over-limit messages are rejected before deserialization
            options.Consumption.MaxPayloadBytes = 4 * 1024 * 1024;

            // Dead letter exchange (non-CloudEvents)
            options.DeadLetter.EnableDeadLetterExchange = true;
            options.DeadLetter.DeadLetterExchange = "dispatch.dlx";
            options.DeadLetter.DeadLetterRoutingKey = "failed";
        });
    }
}
```

Acknowledgment is not a knob on these options: the transport always consumes with manual
acknowledgment, and what happens to a delivery is decided by the `MessageAction` your handler
returns (`Acknowledge`, `Reject` -- nacked without requeue, so it lands in the dead-letter
exchange when one is configured -- or `Requeue`).

Prefetch (QoS) and connection resilience live on the transport options rather than on
`RabbitMqOptions`:

```csharp
services.AddPluggableSerialization(); // Transports don't seat a default serializer
services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("rabbitmq")
       .AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.FromSeconds(10))
       .ConfigureQueue(queue => queue
           .Name("orders-processing")
           .PrefetchCount(100));
});
```

#### Automatic Connection Recovery (Fluent Builder)

When configuring the connection through the fluent builder, use `AutomaticRecovery` to enable recovery and set the reconnection interval in one call:

```csharp
services.AddPluggableSerialization(); // Transports don't seat a default serializer
services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("rabbitmq")
       .AutomaticRecovery(enabled: true, networkRecoveryInterval: TimeSpan.FromSeconds(10));
});
```

`NetworkRecoveryInterval` is the delay between reconnection attempts after a connection drop. When the `networkRecoveryInterval` argument is `null`, the existing configured value is retained.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AutomaticRecoveryEnabled` | `bool` | `true` | Reconnects automatically after a connection drop. |
| `NetworkRecoveryInterval` | `TimeSpan` | 10 seconds | Delay between reconnection attempts. |

### CloudEvents Options
Use `RabbitMqCloudEventOptions` for CloudEvents-specific features:

:::note Trimming and Native AOT
The CloudEvents mapper bundled with this transport serializes the message payload with
reflection-based JSON, so these registrations carry `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. A host that trims or publishes ahead of time gets a warning at the
call. To compose without the requirement, register your own `ICloudEventEncoder<TOutbound>`
backed by a source-generated serializer.
:::

```csharp
// Choose how CloudEvent payloads are serialized. This is required once, and the transport
// registration below reports it at startup if you leave it out.
//   - with a source-generated context: reflection-free, and safe to trim or publish ahead-of-time
//   - AddCloudEvents() with no argument: the reflective path, annotated so your build warns you
services.AddDispatch(dispatch => dispatch.AddCloudEvents(MyPayloadContext.Default));
services.AddCloudEventsForRabbitMq(options =>
{
    options.Exchange.DefaultExchange = "cloudevents";
    options.Exchange.ExchangeType = RabbitMQExchangeType.Topic;
    options.Exchange.Persistence = RabbitMqPersistence.Persistent;

    // Quorum queues + delivery guarantees
    options.UseQuorumQueues = true;
    options.Exchange.EnablePublisherConfirms = true;
    options.Exchange.MandatoryPublishing = true;

    // CloudEvents dead-letter + retry
    options.DeadLetter.EnableDeadLetterExchange = true;
    options.DeadLetter.DeadLetterExchange = "cloudevents.dlx";
    options.DeadLetter.MaxRetryAttempts = 3;
    options.DeadLetter.RetryDelay = TimeSpan.FromSeconds(30);
});
```

## Consumer Handling Options

Acknowledgment is not configurable: the transport always consumes with manual acknowledgment and acts on
what your handler returns. `MessageAction.Acknowledge` acks the delivery, `Reject` nacks it without requeue
(routed to the dead-letter exchange when one is configured), and `Requeue` nacks it with requeue so the
broker redelivers it. Retry policy and backoff are pipeline concerns rather than transport ones -- compose
them with the resilience middleware, which applies uniformly across every transport.

### Maximum Payload Size

The consumer rejects oversized deliveries before the body is deserialized (DoS hardening — the RabbitMQ
analogue of Kestrel's `MaxRequestBodySize`). An over-limit message is nacked with `requeue: false` (routed
to the dead-letter exchange when configured) and the rest of the batch continues, so a single large message
never poison-loops or strands the batch. The limit is `RabbitMqOptions.Consumption.MaxPayloadBytes`
(default **4 MiB**); set it to `null` to opt out for larger legitimate payloads. See the
[Payload Size Contract](../operations/runtime-contract.md#payload-size-contract).

## Publisher Confirms

Enable publisher confirms for guaranteed delivery:

```csharp
// Choose how CloudEvent payloads are serialized. This is required once, and the transport
// registration below reports it at startup if you leave it out.
//   - with a source-generated context: reflection-free, and safe to trim or publish ahead-of-time
//   - AddCloudEvents() with no argument: the reflective path, annotated so your build warns you
services.AddDispatch(dispatch => dispatch.AddCloudEvents(MyPayloadContext.Default));
services.AddCloudEventsForRabbitMq(options =>
{
    options.Publisher.EnableConfirms = true;
    options.Publisher.ConfirmTimeout = TimeSpan.FromSeconds(5);
    options.Publisher.MandatoryPublishing = true;
});
```

## Acknowledgment Behavior

Acknowledgment is not configurable. The transport always consumes with manual acknowledgment and
acts on the `MessageAction` the handler returns:

```csharp
await subscriber.SubscribeAsync(async (message, ct) =>
{
    try
    {
        await ProcessAsync(message, ct);
        return MessageAction.Acknowledge;
    }
    catch (TimeoutException)
    {
        return MessageAction.Requeue;   // nacked with requeue: true
    }
    catch (JsonException)
    {
        return MessageAction.Reject;    // nacked with requeue: false -- goes to the DLX if configured
    }
}, cancellationToken);
```

Enable the dead-letter exchange itself via `RabbitMqOptions`:

```csharp
using Excalibur.Dispatch.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

public static class RabbitMqSiteRegistration389
{
    public static void Configure(IServiceCollection services)
    {
        services.ConfigureAll<RabbitMqOptions>(options =>
        {
            options.DeadLetter.EnableDeadLetterExchange = true;
            options.DeadLetter.DeadLetterExchange = "dispatch.dlx";
        });
    }
}
```

## Health Checks
When using transport adapters, register aggregate health checks (for message bus-only usage, implement a custom check around the RabbitMQ client):

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks();
```

## Observability
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch");
        // Traces: publish, consume, ack, reject
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        // Metrics: message rates, consumer lag
    });
```

## Production Checklist
- [ ] Use durable queues and exchanges
- [ ] Enable publisher confirms for critical messages
- [ ] Configure dead letter exchange and retry policy
- [ ] Set prefetch count based on handler throughput
- [ ] Enable automatic recovery for transient network failures
- [ ] Use TLS (`amqps://`) in production, and leave `RequireTls` on so a plaintext connection is refused rather than made

## Next Steps
- [Kafka Transport](kafka.md) -- Partitioned event streaming
- [Multi-Transport Routing](multi-transport.md) -- Combine RabbitMQ with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) -- Compare RabbitMQ against other transports to find the best fit
- [Message Mapping](./message-mapping.md) -- Configure how message types map to exchanges and queues
- [Dead Letter Handling](../patterns/dead-letter.md) -- Strategies for managing failed messages with DLX
- [Multi-Transport Routing](./multi-transport.md) -- Route different message types across RabbitMQ and other transports
