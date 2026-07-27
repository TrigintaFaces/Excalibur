# Transport DI Registration Patterns

## 4 Transport Providers

| Transport | Entry Point | Builder | Health Check | CloudEvents | DLQ |
|-----------|------------|---------|-------------|-------------|-----|
| RabbitMQ | `AddRabbitMQ()` | `IRabbitMqBuilder` | Yes | Yes | Yes |
| Kafka | `AddKafka()` | `IKafkaBuilder` | Yes | Yes | Yes |
| Azure ServiceBus | `AddAzureServiceBus()` | `IAzureServiceBusBuilder` | Yes | Yes | Yes |
| Google Pub/Sub | `AddGooglePubSub()` | `IPubSubBuilder` | Yes | Yes | Yes |
| AWS SQS | `AddAwsSqs()` | `IAwsSqsBuilder` | Yes | No | Yes |

## Common Pattern

All transports follow the same DI registration pattern:
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddTransport<RabbitMQTransportOptions>("rabbitmq", transport =>
    {
        transport.ConnectionString = "amqps://...";
    });
});
```

Each transport registers: `ITransportSender`, `ITransportReceiver`, `ITransportSubscriber` (keyed by name), plus telemetry decorators, health checks, and `IValidateOptions<T>` validators.
