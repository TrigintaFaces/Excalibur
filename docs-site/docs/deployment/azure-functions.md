---
title: Azure Functions Deployment
description: Run Excalibur on Azure Functions with the isolated worker model, Service Bus triggers, timer-driven outbox processing and Durable Functions sagas.
---
# Azure Functions Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** Azure Functions (Serverless)
**Last Updated:** 2026-01-01

---

The compiler-checked examples on this page use the optional dependency/compiler profile of the existing `samples/05-serverless/AzureFunctions/AzureFunctions.csproj` project, together with the normal Dispatch runtime references. They do not borrow its application types or demonstrate a deployed cloud host or successful NativeAOT publication. The isolated HTTP integration requires `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`; the scheduled outbox requires `Microsoft.Azure.Functions.Worker.Extensions.Timer`.

The order examples declare an application-owned `IOrderApplication`; register your real implementation in the host before startup. These snippets supply contracts, not a simulated business service.

## Before You Start

- **.NET 10.0**
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

## Invocation lifecycle and dependencies

`AddAzureFunctionsServerless()` registers the platform adapter, cold-start optimizer and validated hosting options; it does not compose messaging, persistence, health endpoints or trigger settlement. The `AddExcaliburAzureFunctionsServerless()` entry point forwards to those same services. Configure `AddDispatch(...)` and the domain/provider services your function uses explicitly. See the [registration boundary](https://github.com/TrigintaFaces/Excalibur/blob/main/src/Dispatch/Excalibur.Dispatch.Hosting.AzureFunctions/README.md).

| Concern | Application and host responsibility |
|---|---|
| Connection reuse | Keep the application container and thread-safe SDK clients for the host instance rather than rebuilding them per message. The selected transport owns its registered clients; do not dispose them after each invocation. Open and dispose database connections per operation so the driver's pool can reuse connections. Size each pool against maximum active instances and per-instance concurrency: scale-out creates more pools, not one shared application-wide limit. See [pool tuning](../operations/performance-tuning.md#connection-pool-tuning) and [Azure Functions connection guidance](https://learn.microsoft.com/en-us/azure/azure-functions/manage-connections). |
| Health | Hosting registration does not register an endpoint or a dependency probe. With the corresponding transport registry configured, opt in to `AddHealthChecks().AddTransportHealthChecks()`; with a claim-check provider configured, `AddClaimCheckHealthCheck()` exercises a real store/retrieve/delete probe. Route protected health requests through the platform's HTTP surface or run explicit checks from monitoring. A platform process-health signal is not proof that the broker, database or claim-check storage is usable. |
| Invocation limits | Set timeout, memory and concurrency in the platform deployment. `ServerlessHostOptions.MemoryLimitMB` validates a supplied value; it does not allocate platform memory. `ExecutionTimeout` only caps the budget when invoking through `IServerlessHostProvider.ExecuteAsync`, which combines cancellation with the context's remaining time and cleanup reserve. Calling `IDispatcher` directly does not automatically use that wrapper. Propagate cancellation, await required work before returning, and make effects idempotent for trigger redelivery. |
| Freeze, resume and replacement | Treat in-memory state and timers as temporary. A host may stop scheduling your code or replace the instance; completing an invocation must not depend on a later in-process timer. Persist essential work before returning and drive durable outbox work from a platform trigger with background polling disabled. Reused clients may need their documented stale-connection recovery; initialization/warmup is not proof a connection is still live after an idle period. See [Azure Functions reliability guidance](https://learn.microsoft.com/en-us/azure/azure-functions/performance-reliability). |
| Invocation scope and failures | Resolve scoped handler dependencies from the current invocation/message scope; do not cache them in a shared function object. Dispose application-owned scopes after awaited work, using async disposal when their services require it. The SDK trigger adapter owns acknowledgement, retries and batch responses. Check dispatch results before reporting success when using a result-returning path; a caught exception and a failed result are different observations. |

The repository's local adapter and sample tests demonstrate their selected DI, cancellation and handler paths; they do not simulate a cloud freeze, service settlement or scale-out. In the AWS SQS sample, partial failures are expressed with the SDK's `SQSBatchResponse`; Azure and Google trigger retry behavior comes from their host configuration. Application command/event types, handlers, business services and resource names below are illustrative application code, not extra services installed by the hosting registration.

## Quick Start

### HTTP-Triggered Function

```csharp
using Excalibur.Dispatch.Delivery;
// Function.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Excalibur.Dispatch;
using System.Net;

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

        var command = await req.ReadFromJsonAsync<CreateOrderCommand>()
            ?? throw new InvalidOperationException("An order command is required.");
        var result = await _dispatcher.DispatchAsync(command, context.CancellationToken);

        var response = req.CreateResponse(result.Succeeded ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new { succeeded = result.Succeeded });
        return response;
    }
}

public sealed record CreateOrderCommand(Guid OrderId) : IDispatchAction;

// Application-owned contract: register your real implementation before starting the host.
public interface IOrderApplication
{
    Task CreateAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class CreateOrderCommandHandler(IOrderApplication orders) : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken) =>
        orders.CreateAsync(message.OrderId, cancellationToken);
}
```

### Program.cs (Isolated Worker Model)

```csharp
// Program.cs
using Microsoft.Azure.Functions.Worker;
using Excalibur.Outbox.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Add Dispatch
        services.AddDispatch(options =>
        {
            options.AddHandlersFromAssembly(typeof(CreateOrderCommandHandler).Assembly);
        });

        // Add handlers
        services.AddScoped<Excalibur.Dispatch.Delivery.IActionHandler<CreateOrderCommand>, CreateOrderCommandHandler>();

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is required.");

        // The outbox BUILDER is what registers IOutboxProcessor. AddSqlServerOutboxStore registers the
        // store only, so resolving the processor after it fails with "No service for type
        // IOutboxProcessor" -- the builder is not a stylistic preference here.
        //
        // The dead-letter queue is REQUIRED, not optional: the processor resolves IDeadLetterQueue and
        // throws when none is registered. To discard exhausted messages instead, register
        // NullDeadLetterQueue.Instance explicitly, so discard is a choice rather than an accident.
        services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
        {
            outbox.UseSqlServer(sql => sql.ConnectionString(connectionString));
            outbox.WithSqlServerDeadLetterQueue(dlq => dlq.ConnectionString = connectionString);
        }));
    })
    .Build();

await host.RunAsync();
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.51.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.3.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.7" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.0" />

    <PackageReference Include="Excalibur.Dispatch" Version="10.0.0" />
    <PackageReference Include="Excalibur.Outbox.SqlServer" Version="10.0.0" />
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

### Message size, and the claim check

Every broker here caps the size of a single message, and the cap has moved: Service Bus caps it per tier, and the
tiers differ by more than an order of magnitude. So **read the current service quota rather than any number written in a document**, this one
included — that is why no figure is quoted here.

What matters for composition is that the cap exists and that exceeding it fails the send, not the handler.
The framework ships the claim-check pattern for it: the payload goes to object storage and the message
carries a reference, so message size stops tracking payload size.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddAzureBlobClaimCheck(options =>
{
    options.ConnectionString = blobConnectionString;
    options.ContainerName = "payloads";
});
```

Two consequences worth planning for rather than discovering: the handler's storage read is now on the
critical path of every oversized message, and the retention policy on that bucket becomes part of your
delivery guarantee — a payload collected by a lifecycle rule before its message is consumed is a message
that can no longer be processed. Set the bucket's retention longer than your queue's maximum retention plus
your dead-letter window.


### Azure Service Bus Integration

```csharp ignore
// ServiceBusFunction.cs
using Microsoft.Azure.Functions.Worker;
using Excalibur.Dispatch;

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
            var result = await _dispatcher.DispatchAsync(integrationEvent, context.CancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Integration event dispatch failed.");
            }

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
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Microsoft.Extensions.DependencyInjection;

public static class IntegrationEventRegistration
{
    public static void Configure(IServiceCollection services)
    {
        services.AddDispatch(options => options.AddHandlersFromAssembly(typeof(OrderCreatedIntegrationEventHandler).Assembly));
        services.AddScoped<IEventHandler<OrderCreatedIntegrationEvent>, OrderCreatedIntegrationEventHandler>();
        services.AddScoped<IEventHandler<PaymentProcessedIntegrationEvent>, PaymentProcessedIntegrationEventHandler>();
    }
}

public sealed record OrderCreatedIntegrationEvent(Guid OrderId) : IDispatchEvent;
public sealed record PaymentProcessedIntegrationEvent(Guid OrderId) : IDispatchEvent;

// Application-owned handlers delegate to the application's real side-effect implementation.
public interface IIntegrationEventApplication
{
    Task OnOrderCreatedAsync(Guid orderId, CancellationToken cancellationToken);
    Task OnPaymentProcessedAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class OrderCreatedIntegrationEventHandler(IIntegrationEventApplication application) : IEventHandler<OrderCreatedIntegrationEvent>
{
    public Task HandleAsync(OrderCreatedIntegrationEvent message, CancellationToken cancellationToken) =>
        application.OnOrderCreatedAsync(message.OrderId, cancellationToken);
}

public sealed class PaymentProcessedIntegrationEventHandler(IIntegrationEventApplication application) : IEventHandler<PaymentProcessedIntegrationEvent>
{
    public Task HandleAsync(PaymentProcessedIntegrationEvent message, CancellationToken cancellationToken) =>
        application.OnPaymentProcessedAsync(message.OrderId, cancellationToken);
}
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
using Excalibur.Dispatch;
using Microsoft.Extensions.Logging;

public class OutboxProcessorFunction
{
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly ILogger<OutboxProcessorFunction> _logger;

    public OutboxProcessorFunction(
        IOutboxProcessor outboxProcessor,
        ILogger<OutboxProcessorFunction> logger)
    {
        _outboxProcessor = outboxProcessor;
        _outboxProcessor.Init($"serverless-outbox-{Guid.NewGuid():N}");
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

Install `Excalibur.EventSourcing.CosmosDb` and select the existing provider in the function application's service configuration:

```csharp
using Excalibur.EventSourcing.CosmosDb;
using Microsoft.Extensions.DependencyInjection;

public static class CosmosEventStoreRegistration
{
    public static void Configure(IServiceCollection services, string connectionString)
    {
        services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
            es.UseCosmosDb(cosmos => cosmos
                .ConnectionString(connectionString)
                .DatabaseName("EventStore")
                .ContainerName("Events"))));
    }
}
```

Follow [Cosmos DB provider setup](../event-sourcing/providers.md#azure-cosmos-db) for client ownership, partition keys, provisioning and repository registration. The provider implements the framework's current `IEventStore`; an application-specific document writer with only `AppendAsync` and `GetEventsAsync` does not. A Cosmos DB trigger is a separate platform binding. Follow [projection composition](../event-sourcing/projections.md) for the event-store projection path rather than assuming a hand-written change-feed document shape matches the provider's persisted format.

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

**Consumption Plan:** Earlier unverified illustration: ~1-3 second cold starts. Measure the deployed workload.
**Premium Plan:** Always-ready and prewarmed instances to reduce cold-start latency. See [Microsoft Premium plan guidance](https://learn.microsoft.com/en-us/azure/azure-functions/functions-premium-plan).
**Dedicated Plan:** Full VM control

#### Run the framework's cold-start optimizer

Everything above is platform configuration. The framework also ships a cold-start optimizer for Azure Functions,
and **nothing invokes it for you** — registering it seats the service, it does not run the warmup. Left
uncalled, dependency injection, JIT and SDK initialization are all paid on the first invocation of each new
instance.

```csharp
using Excalibur.Dispatch.Hosting.Serverless;
using Microsoft.Extensions.DependencyInjection;

services.AddAzureFunctionsServerless();
```

Then drive it once from your function's construction or initialization path, not per invocation:

```csharp
var optimizer = provider.GetRequiredService<IColdStartOptimizer>();
await optimizer.OptimizeAsync();
```

`OptimizeAsync` is a no-op off-platform: the optimizer reports itself disabled unless `AZURE_FUNCTIONS_ENVIRONMENT` is set, which the Functions host sets for you, so the same
code is safe to run locally and in tests without a guard of your own.

Unlike AWS Lambda there is no snapshot-and-restore mechanism here to attach a warmup to, so this runs
during instance initialization rather than at image-capture time. That makes it worth less than SnapStart
and more than nothing: the work moves off the first request and onto instance startup, which is where
Azure Functions's own prewarming and minimum-instance settings above are already paying.


### ReadyToRun

```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
</PropertyGroup>
```

**Earlier unverified estimate:** 30-50% faster cold starts; this is not a current framework measurement. ReadyToRun includes IL and precompiled native code and still uses JIT compilation for some methods; it is separate from Native AOT. Measure the deployed workload. See [Microsoft ReadyToRun guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run).

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
  --runtime-version 10 \
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
          dotnet-version: '10.0.x'

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

Configure the function timeout in `host.json` within the limits of your selected [hosting plan](https://learn.microsoft.com/en-us/azure/azure-functions/functions-host-json#functiontimeout). This is a host setting, not a `Timeout` attribute on an isolated-worker function:

```json
{
  "version": "2.0",
  "functionTimeout": "00:05:00"
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
