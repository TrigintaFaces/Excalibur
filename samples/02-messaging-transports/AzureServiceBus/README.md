# Azure Service Bus Transport Sample

This sample demonstrates how to use `Excalibur.Dispatch.Transport.AzureServiceBus` for publishing and consuming messages via Azure Service Bus.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Subscription](https://azure.microsoft.com/free/) (for Azure Service Bus)
- OR [Azure Service Bus Emulator](https://docs.microsoft.com/azure/service-bus-messaging/overview-emulator) for local development

## Quick Start

### 1. Create Azure Service Bus Resources

#### Option A: Azure Portal

1. Create a Service Bus namespace in [Azure Portal](https://portal.azure.com)
2. Create a queue named `dispatch-orders`
3. Copy the connection string from **Shared access policies** > **RootManageSharedAccessKey**

#### Option B: Azure CLI

```bash
# Create resource group
az group create --name dispatch-samples --location eastus

# Create Service Bus namespace
az servicebus namespace create \
    --name dispatch-sample-ns \
    --resource-group dispatch-samples \
    --sku Standard

# Create queue
az servicebus queue create \
    --name dispatch-orders \
    --namespace-name dispatch-sample-ns \
    --resource-group dispatch-samples

# Get connection string
az servicebus namespace authorization-rule keys list \
    --namespace-name dispatch-sample-ns \
    --resource-group dispatch-samples \
    --name RootManageSharedAccessKey \
    --query primaryConnectionString -o tsv
```

#### Option C: Local Development with Emulator

For local development without Azure, you can use the Azure Service Bus Emulator:

```bash
# Install via Docker
docker run -d -p 5672:5672 -p 15672:15672 \
    --name servicebus-emulator \
    mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
```

### 2. Configure the Application

Update `appsettings.json` with your connection string:

```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY",
    "QueueName": "dispatch-orders"
  }
}
```

Or set via environment variable:

```bash
export AzureServiceBus__ConnectionString="Endpoint=sb://..."
```

### 3. Run the Sample

```bash
dotnet run
```

## What This Sample Demonstrates

### Message Publishing

The sample publishes `OrderPlacedEvent` messages to Azure Service Bus:

```csharp
var order = new OrderPlacedEvent("ORD-001", "CUST-100", 99.99m);
await dispatcher.DispatchAsync(order, context);
```

### Azure Service Bus Configuration

```csharp
builder.Services.AddEventTransports(transports =>
    transports.AddAzureServiceBus("azureservicebus", opts =>
    {
        opts.ConnectionString = connectionString;
        opts.QueueName = "dispatch-orders";
        opts.MaxConcurrentCalls = 10;
        opts.PrefetchCount = 50;
    }));
```

### Routing Rules

Messages are routed to Azure Service Bus based on type:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.WithRoutingRules(rules =>
        rules.AddRule<OrderPlacedEvent>((_, _) => "azureservicebus"));
});
```

### Outbox Pattern

The sample uses the outbox pattern for reliable messaging:

```csharp
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddOutboxHostedService();
```

## Project Structure

```
AzureServiceBus/
├── Messages/
│   └── OrderPlacedEvent.cs       # Domain event definition
├── Handlers/
│   └── OrderPlacedEventHandler.cs # Message handler
├── Program.cs                     # Application entry point
├── appsettings.json              # Configuration
└── README.md                     # This file
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `AzureServiceBus:ConnectionString` | Service Bus connection string | Required |
| `AzureServiceBus:QueueName` | Queue name for messaging | `dispatch-orders` |
| `AzureServiceBus:MaxConcurrentCalls` | Max concurrent message processing | `10` |
| `AzureServiceBus:PrefetchCount` | Messages to prefetch | `50` |

## Key Concepts

### Queues vs Topics

- **Queues**: Point-to-point messaging. One consumer receives each message.
- **Topics/Subscriptions**: Publish-subscribe pattern. Multiple subscribers can receive each message.

This sample uses queues. For topic-based messaging, see advanced samples.

### CloudEvents

Messages are formatted according to the [CloudEvents specification](https://cloudevents.io/) for interoperability.

### Dead Letter Queue

Failed messages are automatically moved to the dead letter queue (DLQ) for later analysis. Access via Azure Portal or Service Bus Explorer.

### Sessions

For ordered message processing, enable sessions on your queue and configure session handling in options.

## Cleanup

### Azure Resources

```bash
az group delete --name dispatch-samples --yes --no-wait
```

## Troubleshooting

### Connection Failed

1. Verify connection string is correct
2. Check firewall rules allow your IP
3. Ensure namespace exists and is active

### Queue Not Found

1. Verify queue name matches configuration
2. Create the queue if it doesn't exist:
   ```bash
   az servicebus queue create --name dispatch-orders --namespace-name YOUR_NAMESPACE --resource-group YOUR_RG
   ```

### Authentication Failed

1. Check SharedAccessKeyName and SharedAccessKey
2. Verify the key has Send and Listen permissions
3. Consider using Managed Identity for production

### Message Not Delivered

1. Check if queue has messages (Azure Portal > Service Bus > Queue > Messages)
2. Verify consumer is running and connected
3. Check dead letter queue for failed messages

## Security Best Practices

1. **Never commit connection strings** - Use environment variables or Azure Key Vault
2. **Use Managed Identity** in production for credential-free authentication
3. **Implement retry policies** for transient failures
4. **Monitor dead letter queues** for failed message handling

## Related Samples

- [RabbitMQ](../RabbitMQ/) - RabbitMQ transport
- [Kafka](../Kafka/) - Apache Kafka transport
- [AwsSqs](../AwsSqs/) - AWS SQS transport

## Learn More

- [Azure Service Bus Documentation](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Excalibur.Dispatch.Transport.AzureServiceBus Package](../../../src/Dispatch/Excalibur.Dispatch.Transport.AzureServiceBus/)
- [CloudEvents Specification](https://cloudevents.io/)
- [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer)
