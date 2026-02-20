# Excalibur.Dispatch.Transport.AzureServiceBus

Azure messaging transport implementation for the Dispatch messaging framework, providing integration with Azure Service Bus, Event Hubs, and Storage Queues.

## Overview

This package provides Azure messaging integration for Excalibur.Dispatch, enabling:

- **Azure Service Bus**: Enterprise messaging with queues, topics, and sessions
- **Azure Event Hubs**: High-throughput event streaming with partitions
- **Azure Storage Queues**: Simple, cost-effective queue storage
- **CloudEvents Support**: Standards-compliant structured and binary event formatting
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
    options.QueueName = "my-queue";
});
```

#### Using Managed Identity (Recommended)

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.QueueName = "my-queue";
});

services.Configure<AzureProviderOptions>(options =>
{
    options.UseManagedIdentity = true;
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
services.Configure<AzureEventHubOptions>(options =>
{
    options.ConnectionString = "Endpoint=sb://mynamespace.servicebus.windows.net/;...";
    options.EventHubName = "my-eventhub";
    options.ConsumerGroup = "$Default";
});
```

#### Managed Identity

```csharp
services.Configure<AzureEventHubOptions>(options =>
{
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.EventHubName = "my-eventhub";
    options.ConsumerGroup = "my-consumer-group";
});
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
    options.UseManagedIdentity = true;
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
    options.TenantId = "your-tenant-id";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
});
```

#### Key Vault Integration

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.KeyVaultUrl = new Uri("https://mykeyvault.vault.azure.net/");
    options.UseManagedIdentity = true;
});
```

### Message Configuration

#### Service Bus Settings

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    // Connection
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.QueueName = "my-queue";
    options.TransportType = ServiceBusTransportType.AmqpTcp;  // or AmqpWebSockets

    // Performance
    options.MaxConcurrentCalls = 10;     // Concurrent message processing
    options.PrefetchCount = 50;          // Messages to prefetch

    // CloudEvents
    options.CloudEventsMode = CloudEventsMode.Structured;  // or Binary

    // Error handling
    options.DeadLetterOnRejection = true;  // Send rejected messages to DLQ

    // Security
    options.EnableEncryption = false;
});
```

#### Event Hubs Settings

```csharp
services.Configure<AzureEventHubOptions>(options =>
{
    // Connection
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.EventHubName = "my-eventhub";
    options.ConsumerGroup = "$Default";

    // Performance
    options.PrefetchCount = 300;         // Events to prefetch
    options.MaxBatchSize = 100;          // Max events per batch

    // Processing
    options.StartingPosition = EventHubStartingPosition.Latest;  // or Earliest

    // Security
    options.EnableEncryption = false;
    options.EncryptionProviderName = null;

    // Debugging
    options.EnableVerboseLogging = false;
});
```

#### Storage Queue Settings

```csharp
services.Configure<AzureStorageQueueOptions>(options =>
{
    // Connection
    options.StorageAccountUri = new Uri("https://mystorageaccount.queue.core.windows.net/");
    options.QueueName = "my-queue";

    // Processing
    options.MaxConcurrentMessages = 10;              // Concurrent processing
    options.MaxMessages = 10;                        // Messages per poll (max 32)
    options.PollingInterval = TimeSpan.FromSeconds(1);
    options.VisibilityTimeout = TimeSpan.FromMinutes(5);

    // Dead letter handling
    options.DeadLetterQueueName = "my-queue-dlq";
    options.MaxDequeueCount = 5;                     // Retries before DLQ

    // Security
    options.EnableEncryption = false;

    // Debugging
    options.EnableVerboseLogging = false;
    options.EmptyQueueDelayMs = 1000;
});
```

### Retry Policies

```csharp
services.Configure<AzureProviderOptions>(options =>
{
    options.RetryOptions = new AzureRetryOptions
    {
        MaxRetries = 3,                              // Retry attempts
        Delay = TimeSpan.FromSeconds(1),             // Initial delay
        MaxDelay = TimeSpan.FromSeconds(10),         // Max delay
        Mode = RetryMode.Exponential                 // or Fixed
    };
});
```

## Health Checks

### Registration

```csharp
services.AddHealthChecks()
    .AddAzureServiceBusQueue(
        connectionString: "Endpoint=sb://...",
        queueName: "my-queue",
        name: "servicebus",
        tags: new[] { "ready", "messaging" });
```

### Custom Health Check

```csharp
public class ServiceBusHealthCheck : IHealthCheck
{
    private readonly AzureServiceBusHealthChecker _healthChecker;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _healthChecker.CheckHealthAsync(cancellationToken);
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
    options.MaxConcurrentCalls = 32;     // Increase concurrency
    options.PrefetchCount = 100;         // More prefetch
    options.TransportType = ServiceBusTransportType.AmqpTcp;  // Faster than WebSockets
});
```

#### Event Hubs High-Throughput

```csharp
services.Configure<AzureEventHubOptions>(options =>
{
    options.PrefetchCount = 500;         // More prefetch
    options.MaxBatchSize = 100;          // Process in batches
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
    options.QueueName = "my-queue";
    options.ConnectionString = null;  // Or use connection string
    options.TransportType = ServiceBusTransportType.AmqpTcp;

    // Performance
    options.MaxConcurrentCalls = 10;
    options.PrefetchCount = 50;

    // CloudEvents
    options.CloudEventsMode = CloudEventsMode.Structured;

    // Error handling
    options.DeadLetterOnRejection = false;

    // Security
    options.EnableEncryption = false;
});

services.Configure<AzureProviderOptions>(options =>
{
    // Authentication
    options.UseManagedIdentity = true;
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.TenantId = "";
    options.ClientId = "";
    options.ClientSecret = "";

    // Azure metadata
    options.SubscriptionId = "";
    options.ResourceGroup = "";

    // Key Vault
    options.KeyVaultUrl = null;

    // Storage (for checkpointing)
    options.StorageAccountName = "";
    options.StorageAccountKey = "";
    options.StorageAccountUri = null;

    // Settings
    options.MaxMessageSizeBytes = 262144;  // 256 KB
    options.EnableSessions = false;
    options.PrefetchCount = 10;

    // Retry
    options.RetryOptions = new AzureRetryOptions
    {
        MaxRetries = 3,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Mode = RetryMode.Exponential
    };
});
```

### Event Hubs

```csharp
services.Configure<AzureEventHubOptions>(options =>
{
    // Connection
    options.ConnectionString = null;
    options.FullyQualifiedNamespace = "mynamespace.servicebus.windows.net";
    options.EventHubName = "my-eventhub";
    options.ConsumerGroup = "$Default";

    // Performance
    options.PrefetchCount = 300;
    options.MaxBatchSize = 100;

    // Processing
    options.StartingPosition = EventHubStartingPosition.Latest;

    // Security
    options.EnableEncryption = false;
    options.EncryptionProviderName = null;

    // Debugging
    options.EnableVerboseLogging = false;
    options.CustomProperties = new Dictionary<string, string>();
});
```

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
    options.MaxMessages = 10;
    options.PollingInterval = TimeSpan.FromSeconds(1);
    options.VisibilityTimeout = TimeSpan.FromMinutes(5);
    options.EmptyQueueDelayMs = 1000;

    // Dead letter
    options.DeadLetterQueueName = null;
    options.MaxDequeueCount = 5;

    // Security
    options.EnableEncryption = false;
    options.EncryptionProviderName = null;

    // Debugging
    options.EnableVerboseLogging = false;
    options.CustomProperties = new Dictionary<string, string>();
});
```

## See Also

- [Azure Service Bus Documentation](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Azure Event Hubs Documentation](https://docs.microsoft.com/azure/event-hubs/)
- [Azure Storage Queues Documentation](https://docs.microsoft.com/azure/storage/queues/)
