# Azure Functions Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** Azure Functions (Serverless)
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An Azure account with Functions App provisioned
- Azure Functions Core Tools installed locally
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

Deploy Excalibur applications to Azure Functions for serverless, event-driven workloads with automatic scaling and pay-per-execution pricing.

**Use Azure Functions when:**
- Event-driven processing (HTTP, queues, timers, blobs)
- Variable or unpredictable load patterns
- Cost optimization for low-traffic scenarios
- Rapid deployment without infrastructure management

---

## Quick Start

### HTTP-Triggered Function

```csharp
// Function.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Excalibur.Dispatch.Abstractions;

public class DispatchFunction
{
    private readonly IDispatcher _dispatcher;

    public DispatchFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Function("ProcessCommand")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessCommand");

        var command = await req.ReadFromJsonAsync<CreateOrderCommand>();
        var result = await _dispatcher.DispatchAsync(command, context.CancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
```

### Program.cs (Isolated Worker Model)

```csharp
// Program.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Add Dispatch
        services.AddDispatch(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        // Add handlers
        services.AddScoped<CreateOrderCommandHandler>();

        // Add SQL Server Outbox (optional)
        services.AddSqlServerOutboxStore(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        });
    })
    .Build();

await host.RunAsync();
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="4.0.0" />

    <PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
    <PackageReference Include="Excalibur.Outbox.SqlServer" Version="1.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
  </ItemGroup>
</Project>
```

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "Server=localhost;Database=AppDb;Trusted_Connection=True;"
  }
}
```

### Deploy

```bash
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4

# Run locally
func start

# Deploy to Azure
func azure functionapp publish your-function-app
```

---

## Queue-Triggered Integration Event Processing

### Azure Service Bus Integration

```csharp
// ServiceBusFunction.cs
using Microsoft.Azure.Functions.Worker;
using Excalibur.Dispatch.Abstractions;

public class IntegrationEventProcessor
{
    private readonly IDispatcher _dispatcher;

    public IntegrationEventProcessor(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Function("ProcessIntegrationEvent")]
    public async Task Run(
        [ServiceBusTrigger("integration-events", Connection = "ServiceBusConnection")]
        string messageBody,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessIntegrationEvent");
        logger.LogInformation("Processing integration event: {MessageBody}", messageBody);

        try
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(messageBody);
            await _dispatcher.DispatchAsync(integrationEvent, context.CancellationToken);

            logger.LogInformation("Integration event processed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process integration event");
            throw;  // Triggers retry policy
        }
    }
}
```

### Configuration

```csharp
// Program.cs
services.AddDispatch(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// Integration event handlers
services.AddScoped<OrderCreatedIntegrationEventHandler>();
services.AddScoped<PaymentProcessedIntegrationEventHandler>();
```

### Application Settings (Portal or Azure CLI)

```bash
az functionapp config appsettings set \
  --name your-function-app \
  --resource-group your-rg \
  --settings \
    ServiceBusConnection="Endpoint=sb://..."
```

---

## Timer-Triggered Outbox Processing

### Scheduled Outbox Processor

```csharp
// OutboxProcessorFunction.cs
using Microsoft.Azure.Functions.Worker;
using Excalibur.Outbox;

public class OutboxProcessorFunction
{
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly ILogger<OutboxProcessorFunction> _logger;

    public OutboxProcessorFunction(
        IOutboxProcessor outboxProcessor,
        ILogger<OutboxProcessorFunction> logger)
    {
        _outboxProcessor = outboxProcessor;
        _logger = logger;
    }

    [Function("ProcessOutbox")]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo,  // Every minute
        FunctionContext context)
    {
        _logger.LogInformation("Outbox processor triggered at: {Now}", DateTime.UtcNow);

        try
        {
            var processedCount = await _outboxProcessor.DispatchPendingMessagesAsync(
                context.CancellationToken);

            _logger.LogInformation("Processed {Count} outbox messages", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox processing failed");
            throw;
        }
    }
}
```

**CRON schedule formats:**

```csharp
// Every minute
[TimerTrigger("0 */1 * * * *")]

// Every 5 minutes
[TimerTrigger("0 */5 * * * *")]

// Every hour at :30
[TimerTrigger("0 30 * * * *")]

// Daily at 2 AM
[TimerTrigger("0 0 2 * * *")]
```

---

## Durable Functions for Sagas

### Order Processing Saga

```csharp
// OrderSagaOrchestration.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class OrderSagaOrchestration
{
    [Function("OrderSaga")]
    public async Task<OrderResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var order = context.GetInput<CreateOrderCommand>();
        var logger = context.CreateReplaySafeLogger("OrderSaga");

        try
        {
            // Step 1: Reserve inventory
            var inventoryReserved = await context.CallActivityAsync<bool>(
                "ReserveInventory", order.Items);

            if (!inventoryReserved)
            {
                logger.LogWarning("Inventory reservation failed for order {OrderId}", order.OrderId);
                return OrderResult.Failed("Insufficient inventory");
            }

            // Step 2: Process payment
            var paymentProcessed = await context.CallActivityAsync<bool>(
                "ProcessPayment", order.Payment);

            if (!paymentProcessed)
            {
                // Compensate: Release inventory
                await context.CallActivityAsync("ReleaseInventory", order.Items);
                logger.LogWarning("Payment failed for order {OrderId}", order.OrderId);
                return OrderResult.Failed("Payment declined");
            }

            // Step 3: Create shipment
            var shipmentId = await context.CallActivityAsync<string>(
                "CreateShipment", order);

            logger.LogInformation("Order {OrderId} completed successfully", order.OrderId);
            return OrderResult.Success(shipmentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order saga failed for {OrderId}", order.OrderId);

            // Compensate: Release inventory and refund payment
            await context.CallActivityAsync("ReleaseInventory", order.Items);
            await context.CallActivityAsync("RefundPayment", order.Payment);

            throw;
        }
    }

    [Function("ReserveInventory")]
    public async Task<bool> ReserveInventory(
        [ActivityTrigger] OrderItem[] items,
        FunctionContext context)
    {
        // Call inventory service
        return true;
    }

    [Function("ProcessPayment")]
    public async Task<bool> ProcessPayment(
        [ActivityTrigger] PaymentInfo payment,
        FunctionContext context)
    {
        // Call payment gateway
        return true;
    }

    [Function("CreateShipment")]
    public async Task<string> CreateShipment(
        [ActivityTrigger] CreateOrderCommand order,
        FunctionContext context)
    {
        // Call shipping service
        return Guid.NewGuid().ToString();
    }

    [Function("StartOrderSaga")]
    public async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext context)
    {
        var order = await req.ReadFromJsonAsync<CreateOrderCommand>();
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "OrderSaga", order);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId });
        return response;
    }
}
```

**Package reference:**

```xml
<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.DurableTask" Version="2.0.0" />
```

---

## Event Sourcing with Cosmos DB

### Cosmos DB Event Store

```csharp
// Program.cs
services.AddSingleton<IEventStore>(sp =>
{
    var cosmosClient = new CosmosClient(
        Environment.GetEnvironmentVariable("CosmosDbConnectionString"));

    return new CosmosDbEventStore(cosmosClient, "EventStore", "Events");
});

// CosmosDbEventStore.cs
public class CosmosDbEventStore : IEventStore
{
    private readonly Container _container;

    public CosmosDbEventStore(CosmosClient client, string databaseName, string containerName)
    {
        _container = client.GetContainer(databaseName, containerName);
    }

    public async Task AppendAsync(
        string aggregateId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var batch = _container.CreateTransactionalBatch(new PartitionKey(aggregateId));

        foreach (var @event in events)
        {
            var eventDocument = new EventDocument
            {
                Id = Guid.NewGuid().ToString(),
                AggregateId = aggregateId,
                EventType = @event.EventType,
                EventData = JsonSerializer.Serialize(@event),
                Version = @event.Version,
                OccurredAt = @event.OccurredAt
            };

            batch.CreateItem(eventDocument);
        }

        var response = await batch.ExecuteAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ConcurrencyException(
                $"Concurrency conflict for aggregate {aggregateId}");
        }
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(
        string aggregateId,
        CancellationToken cancellationToken)
    {
        var query = _container.GetItemQueryIterator<EventDocument>(
            new QueryDefinition("SELECT * FROM c WHERE c.aggregateId = @aggregateId ORDER BY c.version")
                .WithParameter("@aggregateId", aggregateId));

        var events = new List<IDomainEvent>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync(cancellationToken);
            foreach (var doc in response)
            {
                var eventType = Type.GetType(doc.EventType);
                var @event = JsonSerializer.Deserialize(doc.EventData, eventType) as IDomainEvent;
                events.Add(@event);
            }
        }

        return events;
    }
}
```

### Cosmos DB Change Feed Projections

```csharp
// ProjectionFunction.cs
using Microsoft.Azure.Functions.Worker;

public class ProjectionFunction
{
    private readonly IProjectionService _projectionService;

    public ProjectionFunction(IProjectionService projectionService)
    {
        _projectionService = projectionService;
    }

    [Function("ProcessProjections")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "EventStore",
            containerName: "Events",
            Connection = "CosmosDbConnectionString",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<EventDocument> input,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessProjections");
        logger.LogInformation("Processing {Count} events", input.Count);

        foreach (var eventDoc in input)
        {
            var eventType = Type.GetType(eventDoc.EventType);
            var @event = JsonSerializer.Deserialize(eventDoc.EventData, eventType) as IDomainEvent;

            await _projectionService.ProjectAsync(@event, context.CancellationToken);
        }
    }
}
```

---

## Configuration and Secrets

### Azure Key Vault Integration

```csharp
// Program.cs
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        if (context.HostingEnvironment.IsProduction())
        {
            var builtConfig = config.Build();
            var keyVaultName = builtConfig["KeyVaultName"];

            config.AddAzureKeyVault(
                new Uri($"https://{keyVaultName}.vault.azure.net/"),
                new DefaultAzureCredential());
        }
    })
    .ConfigureServices(services =>
    {
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
        });
    })
    .Build();
```

### Managed Identity Configuration

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name your-function-app \
  --resource-group your-rg

# Grant Key Vault access
az keyvault set-policy \
  --name your-keyvault \
  --object-id <principal-id> \
  --secret-permissions get list
```

**Reference secrets:**

```bash
# App Settings reference format
SqlConnectionString=@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/SqlConnectionString/)
```

---

## Monitoring and Diagnostics

### Application Insights Integration

```csharp
// Program.cs
services.AddApplicationInsightsTelemetryWorkerService();
services.ConfigureFunctionsApplicationInsights();
```

**host.json:**

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20
      },
      "enableDependencyTracking": true
    },
    "logLevel": {
      "default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Custom Metrics

```csharp
using Microsoft.ApplicationInsights;

public class MetricsFunction
{
    private readonly TelemetryClient _telemetryClient;

    public MetricsFunction(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    [Function("TrackMetrics")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        _telemetryClient.TrackMetric("OrdersProcessed", 42);
        _telemetryClient.TrackEvent("OutboxProcessed", new Dictionary<string, string>
        {
            ["BatchSize"] = "100",
            ["Duration"] = "1.5s"
        });
    }
}
```

---

## Performance Optimization

### Cold Start Mitigation

```json
{
  "version": "2.0",
  "extensionBundle": {
    "id": "Microsoft.Azure.Functions.ExtensionBundle",
    "version": "[4.*, 5.0.0)"
  },
  "functionTimeout": "00:10:00",
  "healthMonitor": {
    "enabled": true,
    "healthCheckInterval": "00:00:10",
    "healthCheckWindow": "00:02:00",
    "healthCheckThreshold": 6,
    "counterThreshold": 0.80
  }
}
```

**Consumption Plan:** ~1-3 second cold starts
**Premium Plan:** Always-warm instances (no cold starts)
**Dedicated Plan:** Full VM control

### ReadyToRun (AOT Compilation)

```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
</PropertyGroup>
```

**Result:** 30-50% faster cold starts

---

## Deployment

### Azure CLI Deployment

```bash
# Create resource group
az group create --name your-rg --location eastus

# Create storage account
az storage account create \
  --name yourstorage \
  --resource-group your-rg \
  --location eastus \
  --sku Standard_LRS

# Create function app (Consumption plan)
az functionapp create \
  --name your-function-app \
  --resource-group your-rg \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 9 \
  --functions-version 4 \
  --storage-account yourstorage

# Deploy code
func azure functionapp publish your-function-app
```

### GitHub Actions CI/CD

```yaml
name: Deploy Azure Function

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build
        run: |
          dotnet build --configuration Release
          dotnet publish --configuration Release --output ./output

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
        with:
          app-name: your-function-app
          package: ./output
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

---

## Troubleshooting

### Function Not Triggering

```bash
# Check function logs
func azure functionapp logstream your-function-app

# View Application Insights logs
az monitor app-insights query \
  --app your-app-insights \
  --analytics-query "traces | where timestamp > ago(1h) | order by timestamp desc"
```

### High Execution Time

```csharp
// Add timeout configuration
[Function("LongRunningTask")]
[Timeout("00:10:00")]  // 10 minutes
public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
{
    // Long-running operation
}
```

### Memory Issues

```bash
# Upgrade to Premium plan for more memory
az functionapp plan create \
  --name your-plan \
  --resource-group your-rg \
  --location eastus \
  --sku EP1  # Elastic Premium

az functionapp update \
  --name your-function-app \
  --resource-group your-rg \
  --plan your-plan
```

---

## Next Steps

- **AWS Lambda:** [AWS Lambda deployment](aws-lambda.md) for multi-cloud
- **Docker:** [Containerize functions](docker.md) for Azure Container Apps
- **Monitoring:** [Application Insights](../observability/azure-monitor.md) deep dive
- **Security:** [Security best practices](security-best-practices.md)

---

## See Also

- [AWS Lambda Deployment](aws-lambda.md) - Deploy to AWS Lambda for multi-cloud serverless workloads
- [Google Cloud Functions Deployment](google-cloud-functions.md) - Deploy to Google Cloud Functions for GCP-based serverless workloads
- [Azure Service Bus Transport](../transports/azure-service-bus.md) - Configure the Azure Service Bus transport for message publishing and consumption

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**Azure Functions:** v4 (Isolated Worker Model)
