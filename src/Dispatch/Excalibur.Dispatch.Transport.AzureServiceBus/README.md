# Excalibur.Dispatch.Transport.AzureServiceBus

Azure messaging transport implementation for the Excalibur framework, providing integration with Azure Service Bus, Event Hubs, and Storage Queues.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.Azure` | Starter | + Resilience (Polly) + Observability |

> **Tip:** If you are getting started, install `Excalibur.Dispatch.Azure` instead of this package directly. It includes production-ready defaults.

## Overview

This package provides Azure messaging integration for Excalibur.Dispatch, enabling:

- **Azure Service Bus**: Enterprise messaging with queues, topics, and sessions
- **Azure Event Hubs**: High-throughput event streaming with partitions
- **Azure Storage Queues**: Simple, cost-effective queue storage
- **CloudEvents Support**: Standards-compliant structured and binary event formatting. Registering the bundled mapper is annotated for trimming and ahead-of-time builds (it serializes payloads with reflection-based JSON); supply your own `ICloudEventMapper<TTransportMessage>` over a source-generated serializer to avoid the requirement.
- **Managed Identity**: Passwordless authentication with Azure AD
- **Dead Letter Handling**: Built-in dead letter queue support

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus
```

## Configuration

### Service Bus

#### Using Connection String

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.ConnectionString = "Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=...";
    options.Sender.DefaultEntityName = "my-queue";
});
```

#### Using Managed Identity (Recommended)

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.Sender.DefaultEntityName = "my-queue";
});
```

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.Authentication.UseManagedIdentity = true;
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
});
```

#### Environment Variables

```bash
AZURE_SERVICEBUS_CONNECTIONSTRING=Endpoint=sb://...
AZURE_SERVICEBUS_QUEUENAME=my-queue
```

```csharp
services.Configure<AzureServiceBusOptions>(configuration.GetSection("Azure:ServiceBus"));
```

### Event Hubs

#### Connection String

```csharp
services.AddAzureEventHubsTransport(eh => eh
    .ConnectionString("Endpoint=sb://mynamespace.servicebus.windows.net/;...")
    .EventHubName("my-eventhub"));
```

#### Managed Identity

```csharp
services.AddAzureEventHubsTransport(eh => eh
    .FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
    .UseManagedIdentity()
    .EventHubName("my-eventhub"));
```

### Storage Queues

#### Connection String

```csharp
services.Configure<AzureStorageQueueOptions>(options =>
{
    options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=...";
    options.QueueName = "my-queue";
});
```

#### Managed Identity

```csharp
services.Configure<AzureStorageQueueOptions>(options =>
{
    options.StorageAccountUri = new Uri("https://mystorageaccount.queue.core.windows.net/");
    options.QueueName = "my-queue";
});
```

### Authentication

#### Managed Identity (Production Recommended)

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.Authentication.UseManagedIdentity = true;
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
});
```

Required Azure RBAC roles:
- **Service Bus**: `Azure Service Bus Data Sender`, `Azure Service Bus Data Receiver`
- **Event Hubs**: `Azure Event Hubs Data Sender`, `Azure Event Hubs Data Receiver`
- **Storage Queues**: `Storage Queue Data Contributor`

#### Service Principal

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.Authentication.TenantId = "your-tenant-id";
    options.Authentication.ClientId = "your-client-id";
    options.Authentication.ClientSecret = "your-client-secret";
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
});
```

#### Key Vault Integration

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.KeyVaultUrl = new Uri("https://mykeyvault.vault.azure.net/");
    options.Authentication.UseManagedIdentity = true;
});
```

### Message Configuration

#### Service Bus Settings

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    // Connection
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.TransportType = ServiceBusTransportType.AmqpTcp;  // or AmqpWebSockets

    // Sending
    options.Sender.DefaultEntityName = "my-queue";

    // Receiving
    options.Processor.MaxConcurrentCalls = 10;   // Concurrent message processing
    options.Processor.PrefetchCount = 50;        // Messages to prefetch
});
```

#### Event Hubs Settings

```csharp
services.AddAzureEventHubsTransport("telemetry", eh => eh
    .FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
    .UseManagedIdentity()
    .EventHubName("my-eventhub"));
```

The Event Hubs transport publishes through `EventHubProducerClient`; connection and hub
identity are the whole of its configuration surface. Consumer-side tuning (consumer group,
prefetch, batch size, starting position) is not exposed by this transport.

#### Storage Queue Settings

```csharp
services.Configure<AzureStorageQueueOptions>(options =>
{
    // Connection
    options.StorageAccountUri = new Uri("https://mystorageaccount.queue.core.windows.net/");
    options.QueueName = "my-queue";

    // Processing
    options.MaxConcurrentMessages = 10;              // Concurrent processing
    options.Polling.MaxMessages = 10;                // Messages per poll (max 32)
    options.Polling.PollingInterval = TimeSpan.FromSeconds(1);
    options.Polling.VisibilityTimeout = TimeSpan.FromMinutes(5);

    // Dead letter handling
    options.DeadLetterQueueName = "my-queue-dlq";
    options.MaxDequeueCount = 5;                     // Retries before DLQ

    // Security
    options.EncryptionProviderName = null;           // Named encryption provider, or null to disable

    // Debugging
    options.Polling.EnableVerboseLogging = false;
    options.Polling.EmptyQueueDelayMs = 1000;
});
```

### Retry Policies

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.RetryOptions = new AzureRetryOptions
    {
        MaxRetryAttempts = 3,                        // Retry attempts
        Delay = TimeSpan.FromSeconds(1),             // Initial delay
        MaxDelay = TimeSpan.FromSeconds(10),         // Max delay
        Mode = RetryMode.Exponential                 // or Fixed
    };
});
```

## Health Checks

### Registration

The transport adapter implements `ITransportHealthChecker`, so the aggregate transport health check
covers it without any transport-specific registration:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks(
        name: "servicebus",
        tags: ["ready", "messaging"]);
```

### Custom Health Check

To build your own check, inject the public `ITransportHealthChecker` rather than any transport-internal
type:

```csharp
public class ServiceBusHealthCheck(ITransportHealthChecker healthChecker) : IHealthCheck
{
    private readonly ITransportHealthChecker _healthChecker = healthChecker;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _healthChecker.CheckQuickHealthAsync(cancellationToken);
            return result.IsHealthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded(result.Description);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Service Bus unreachable", ex);
        }
    }
}
```

## Production Considerations

### Scaling

#### Service Bus

- Use **multiple processors** with unique instance identifiers
- Enable **sessions** for ordered processing per session ID
- Use **topics with subscriptions** for pub/sub patterns
- Scale out with **competing consumers** on queues

#### Event Hubs

- Scale based on **partition count** (1 consumer per partition max)
- Use **consumer groups** for multiple applications
- Configure appropriate **prefetch count** for throughput
- Consider **Capture** for archival to storage

#### Storage Queues

- Simple horizontal scaling with multiple consumers
- Lower throughput than Service Bus (~2000 messages/sec per queue)
- Cost-effective for simple queue scenarios

### Performance Tuning

#### Service Bus High-Throughput

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.Processor.MaxConcurrentCalls = 32;    // Increase concurrency
    options.Processor.PrefetchCount = 100;        // More prefetch
    options.TransportType = ServiceBusTransportType.AmqpTcp;  // Faster than WebSockets
});
```

### Monitoring and Alerting

Key Azure Monitor metrics:

| Service | Metric | Alert Threshold |
|---------|--------|-----------------|
| Service Bus | `ActiveMessages` | > 10,000 |
| Service Bus | `DeadLetteredMessages` | > 100 |
| Service Bus | `ServerErrors` | > 0 |
| Event Hubs | `IncomingMessages` | Baseline deviation |
| Event Hubs | `ThrottledRequests` | > 0 |
| Storage Queues | `QueueMessageCount` | > 10,000 |

### Security Best Practices

1. **Use Managed Identity** instead of connection strings
2. **Enable Private Endpoints** to restrict network access
3. **Configure RBAC** with least-privilege roles
4. **Enable diagnostic logging** for audit trails
5. **Use Key Vault** for secrets when connection strings are required
6. **Enable encryption** for sensitive data

### Cost Optimization

1. **Choose the right tier**: Basic, Standard, or Premium for Service Bus
2. **Use Standard tier Event Hubs** for most scenarios (Premium for high throughput)
3. **Storage Queues** are cheapest for simple queue patterns
4. **Auto-delete idle resources** to avoid costs
5. **Set appropriate message TTL** to avoid accumulation

## Troubleshooting

### Common Issues

#### Connection Refused

```
Azure.Messaging.ServiceBus.ServiceBusException: The connection was refused
```

**Solutions:**
- Verify connection string format
- Check namespace exists and is accessible
- Verify firewall/network rules allow access
- For managed identity, verify RBAC role assignments

#### Unauthorized Access

```
Azure.Identity.AuthenticationFailedException: ManagedIdentityCredential authentication unavailable
```

**Solutions:**
- Enable managed identity on your Azure resource (App Service, VM, AKS)
- Assign correct RBAC roles to the identity
- For local development, use `DefaultAzureCredential` with Azure CLI login

#### Queue Not Found

```
Azure.Messaging.ServiceBus.ServiceBusException: Entity not found
```

**Solutions:**
- Verify queue/topic name is correct (case-sensitive)
- Check entity exists in the namespace
- Verify connection string points to correct namespace

#### Message Lock Lost

```
Azure.Messaging.ServiceBus.ServiceBusException: The lock supplied is invalid
```

**Solutions:**
- Increase message lock duration in queue settings
- Process messages faster
- Use auto-renew lock feature
- Avoid long-running synchronous operations

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Dispatch.Transport.AzureServiceBus": "Debug",
      "Azure.Messaging.ServiceBus": "Information",
      "Azure.Messaging.EventHubs": "Information",
      "Azure.Core": "Warning"
    }
  }
}
```

### Debug Tips

1. **Enable Application Insights** for distributed tracing
2. **Use Service Bus Explorer** to inspect queues/topics
3. **Check Azure Monitor logs** for service-side errors
4. **Test with Azure Portal** to verify queue accessibility
5. **Enable diagnostic settings** on Service Bus namespace

## Complete Configuration Reference

### Service Bus

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    // Connection
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.ConnectionString = null;  // Or use connection string
    options.TransportType = ServiceBusTransportType.AmqpTcp;

    // Sending
    options.Sender.DefaultEntityName = "my-queue";

    // Receiving
    options.Processor.MaxConcurrentCalls = 10;
    options.Processor.PrefetchCount = 50;
});
```

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    // Authentication
    options.Authentication.UseManagedIdentity = true;
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.Authentication.TenantId = "";
    options.Authentication.ClientId = "";
    options.Authentication.ClientSecret = "";

    // Azure metadata
    options.SubscriptionId = "";
    options.ResourceGroup = "";

    // Key Vault
    options.KeyVaultUrl = null;

    // Storage (for checkpointing)
    options.Storage.StorageAccountName = "";
    options.Storage.StorageAccountKey = "";
    options.Storage.StorageAccountUri = null;

    // Settings
    options.MaxMessageSizeBytes = 262144;  // 256 KB
    options.EnableSessions = false;
    options.PrefetchCount = 10;

    // Retry
    options.RetryOptions = new AzureRetryOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Mode = RetryMode.Exponential
    };
});
```

### Event Hubs

```csharp
// Connection string
services.AddAzureEventHubsTransport("telemetry", eh => eh
    .ConnectionString("Endpoint=sb://mynamespace.servicebus.windows.net/;...")
    .EventHubName("my-eventhub"));

// Managed identity
services.AddAzureEventHubsTransport("telemetry", eh => eh
    .FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
    .UseManagedIdentity()
    .EventHubName("my-eventhub"));
```

`AzureEventHubsTransportOptions` carries exactly these settings: `Name`, `ConnectionString`,
`FullyQualifiedNamespace`, `UseManagedIdentity` and `EventHubName`. Reach them directly with
`eh.ConfigureOptions(o => ...)` when binding from configuration.

### Storage Queues

```csharp
services.Configure<AzureStorageQueueOptions>(options =>
{
    // Connection
    options.ConnectionString = null;
    options.StorageAccountUri = new Uri("https://mystorageaccount.queue.core.windows.net/");
    options.QueueName = "my-queue";

    // Processing
    options.MaxConcurrentMessages = 10;
    options.Polling.MaxMessages = 10;
    options.Polling.PollingInterval = TimeSpan.FromSeconds(1);
    options.Polling.VisibilityTimeout = TimeSpan.FromMinutes(5);
    options.Polling.EmptyQueueDelayMs = 1000;

    // Dead letter
    options.DeadLetterQueueName = null;
    options.MaxDequeueCount = 5;

    // Security
    options.EncryptionProviderName = null;

    // Debugging
    options.Polling.EnableVerboseLogging = false;
    options.Polling.CustomProperties["my-key"] = "my-value";
});
```

## See Also

- [Azure Service Bus Documentation](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Azure Event Hubs Documentation](https://docs.microsoft.com/azure/event-hubs/)
- [Azure Storage Queues Documentation](https://docs.microsoft.com/azure/storage/queues/)
