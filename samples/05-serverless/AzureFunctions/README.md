# Azure Functions Serverless Sample

This sample demonstrates how to use `Excalibur.Dispatch.Hosting.AzureFunctions` for building serverless applications with Azure Functions using the Dispatch messaging framework.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) (for local storage emulation)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (for deployment)

### Installing Prerequisites

```bash
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Install Azurite for local storage emulation
npm install -g azurite
```

## Quick Start

### 1. Start Local Storage Emulator

```bash
# Start Azurite in a separate terminal
azurite --silent --location ./azurite --debug ./azurite/debug.log
```

### 2. Build the Project

```bash
dotnet build
```

### 3. Run Locally

```bash
func start
```

### 4. Test the Functions

```bash
# Create an order (HTTP trigger)
curl -X POST http://localhost:7071/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-001",
    "customerId": "CUST-100",
    "totalAmount": 99.99,
    "items": [
      {"productId": "PROD-1", "productName": "Widget", "quantity": 2, "unitPrice": 49.99}
    ]
  }'

# Get order status
curl http://localhost:7071/api/orders/ORD-001
```

## What This Sample Demonstrates

### HTTP-Triggered Function

The `HttpOrderFunction` demonstrates REST API operations using Dispatch:

```csharp
[Function("CreateOrder")]
public async Task<HttpResponseData> CreateOrderAsync(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequestData req)
{
    var command = await req.ReadFromJsonAsync<CreateOrderCommand>();
    var context = DispatchContextInitializer.CreateDefaultContext();
    await _dispatcher.DispatchAsync(command, context);
    // ...
}
```

### Queue-Triggered Function

The `QueueOrderProcessor` processes events from Azure Storage Queue:

```csharp
[Function("ProcessOrderEvent")]
public async Task ProcessOrderEventAsync(
    [QueueTrigger("order-events")] string message)
{
    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);
    await _dispatcher.DispatchAsync(orderEvent, context);
}
```

### Timer-Triggered Function

The `ScheduledReportFunction` runs on a schedule:

```csharp
[Function("GenerateDailySalesReport")]
public async Task GenerateDailySalesReportAsync(
    [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
{
    var command = new GenerateReportCommand(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
    await _dispatcher.DispatchAsync(command, context);
}
```

## Project Structure

```
AzureFunctions/
├── Functions/
│   ├── HttpOrderFunction.cs       # HTTP-triggered REST API
│   ├── QueueOrderProcessor.cs     # Queue-triggered event processor
│   └── ScheduledReportFunction.cs # Timer-triggered scheduled tasks
├── Messages/
│   └── OrderMessages.cs           # Command and event definitions
├── Handlers/
│   ├── OrderCommandHandler.cs     # Handles CreateOrderCommand
│   └── OrderEventHandler.cs       # Handles OrderCreatedEvent
├── Program.cs                     # Azure Functions host configuration
├── host.json                      # Azure Functions host settings
├── local.settings.json            # Local development settings
├── deploy.sh                      # Azure deployment script
└── README.md                      # This file
```

## Configuration

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

### host.json Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `functionTimeout` | Max function execution time | 5 minutes |
| `extensions.queues.batchSize` | Messages processed per batch | 16 |
| `extensions.http.maxConcurrentRequests` | Max concurrent HTTP requests | 100 |

## Cold Start Optimization

This sample uses `Excalibur.Dispatch.Hosting.AzureFunctions` which includes cold start optimization:

```csharp
builder.Services.AddExcaliburAzureFunctionsServerless(opts =>
{
    opts.EnableColdStartOptimization = true;
    opts.EnableDistributedTracing = true;
    opts.AzureFunctions.HostingPlan = "Consumption";
});
```

### Best Practices for Cold Start

1. **Use .NET 8+ isolated worker model** - Better startup performance
2. **Minimize dependencies** - Only include necessary packages
3. **Use lazy initialization** - Defer heavy operations
4. **Consider Premium/Dedicated plans** - For latency-sensitive workloads
5. **Keep packages warm** - Use health check pings in production

## Deployment to Azure

### Option 1: Using the Deploy Script

```bash
# Set environment variables (optional)
export RESOURCE_GROUP="my-resource-group"
export FUNCTION_APP="my-function-app"
export LOCATION="eastus"

# Run deployment
./deploy.sh
```

### Option 2: Manual Deployment

```bash
# Login to Azure
az login

# Create resource group
az group create --name dispatch-serverless-rg --location eastus

# Create storage account
az storage account create \
    --name dispatchfuncstore \
    --resource-group dispatch-serverless-rg \
    --sku Standard_LRS

# Create function app
az functionapp create \
    --name dispatch-azure-functions-sample \
    --resource-group dispatch-serverless-rg \
    --storage-account dispatchfuncstore \
    --consumption-plan-location eastus \
    --runtime dotnet-isolated \
    --functions-version 4

# Publish
func azure functionapp publish dispatch-azure-functions-sample
```

## Monitoring

### Application Insights

Enable Application Insights for monitoring:

```bash
az monitor app-insights component create \
    --app dispatch-func-insights \
    --location eastus \
    --resource-group dispatch-serverless-rg

# Get connection string and configure
az functionapp config appsettings set \
    --name dispatch-azure-functions-sample \
    --resource-group dispatch-serverless-rg \
    --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=<connection-string>"
```

### View Logs

```bash
# Real-time logs
az functionapp logs tail \
    --name dispatch-azure-functions-sample \
    --resource-group dispatch-serverless-rg
```

## Troubleshooting

### Function Not Found

Ensure the project builds and functions are discovered:

```bash
func host start --verbose
```

### Storage Connection Issues

Verify Azurite is running or use real Azure Storage:

```bash
# Check Azurite
curl http://127.0.0.1:10000/devstoreaccount1?comp=list

# Or use Azure Storage
az storage account show-connection-string \
    --name yourstorageaccount \
    --resource-group yourresourcegroup
```

### Queue Messages Not Processing

1. Check queue exists: Use Azure Storage Explorer
2. Verify connection string in `local.settings.json`
3. Check poison queue for failed messages

## Related Samples

- [AWS Lambda](../AwsLambda/) - AWS Lambda serverless
- [Google Cloud Functions](../GoogleCloudFunctions/) - GCP serverless

## Learn More

- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
- [Azure Functions .NET Worker](https://docs.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Excalibur.Dispatch.Hosting.AzureFunctions Package](../../../src/Dispatch/Excalibur.Dispatch.Hosting.AzureFunctions/)
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
