# Google Cloud Functions Sample

This sample demonstrates how to use **Excalibur.Dispatch** messaging in Google Cloud Functions with various trigger types.

## Features

- **HTTP Triggers** - REST API endpoints for order operations
- **Pub/Sub Triggers** - Event-driven processing with Cloud Pub/Sub
- **Cloud Scheduler Triggers** - Periodic scheduled tasks
- **Dispatch Integration** - Full messaging pipeline with handlers
- **Cold Start Optimization** - Configured for optimal serverless performance

## Project Structure

```
GoogleCloudFunctions/
├── Functions/
│   ├── HttpFunction.cs          # HTTP-triggered function
│   ├── PubSubFunction.cs        # Pub/Sub-triggered function
│   └── ScheduledFunction.cs     # Cloud Scheduler-triggered function
├── Handlers/
│   ├── OrderCreatedEventHandler.cs   # Order event handler
│   └── ScheduledTaskHandler.cs       # Scheduled task handler
├── Messages/
│   └── OrderMessages.cs         # Event definitions
├── Program.cs                   # Entry point
├── Startup.cs                   # DI configuration
├── deploy.sh                    # Deployment script
└── README.md                    # This file
```

## Prerequisites

1. **Google Cloud SDK**
   ```bash
   # Install from: https://cloud.google.com/sdk/docs/install
   gcloud --version
   ```

2. **.NET 8 SDK**
   ```bash
   dotnet --version
   ```

3. **Google Cloud Project**
   ```bash
   # Authenticate
   gcloud auth login

   # Set project
   gcloud config set project YOUR_PROJECT_ID
   ```

4. **Enable Required APIs**
   ```bash
   gcloud services enable cloudfunctions.googleapis.com
   gcloud services enable cloudbuild.googleapis.com
   gcloud services enable pubsub.googleapis.com
   gcloud services enable cloudscheduler.googleapis.com
   ```

## Local Development

### Run Locally with Functions Framework

```bash
# From sample directory
dotnet run

# The function will be available at http://localhost:8080
```

### Test HTTP Function

```bash
# Create an order
curl -X POST http://localhost:8080/orders \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-001",
    "customerId": "CUST-100",
    "totalAmount": 99.99
  }'

# Get an order
curl http://localhost:8080/orders/ORD-001
```

## Deployment

### Using the Deployment Script

```bash
# Set your project ID (optional if already configured)
export GCP_PROJECT_ID=your-project-id
export GCP_REGION=us-central1  # Optional, defaults to us-central1

# Deploy all functions
./deploy.sh deploy

# Deploy specific function
./deploy.sh http
./deploy.sh pubsub
./deploy.sh scheduled

# Cleanup all resources
./deploy.sh cleanup
```

### Manual Deployment

```bash
# Deploy HTTP function
gcloud functions deploy http-order-function \
  --gen2 \
  --runtime=dotnet8 \
  --region=us-central1 \
  --source=. \
  --entry-point=GoogleCloudFunctionsSample.Functions.HttpFunction \
  --trigger-http \
  --allow-unauthenticated

# Deploy Pub/Sub function
gcloud functions deploy pubsub-order-processor \
  --gen2 \
  --runtime=dotnet8 \
  --region=us-central1 \
  --source=. \
  --entry-point=GoogleCloudFunctionsSample.Functions.PubSubFunction \
  --trigger-topic=orders

# Deploy scheduled function
gcloud functions deploy scheduled-task-function \
  --gen2 \
  --runtime=dotnet8 \
  --region=us-central1 \
  --source=. \
  --entry-point=GoogleCloudFunctionsSample.Functions.ScheduledFunction \
  --trigger-topic=scheduled-tasks
```

## Testing Deployed Functions

### HTTP Function

```bash
# Get the function URL
FUNCTION_URL=$(gcloud functions describe http-order-function \
  --gen2 --region=us-central1 --format='value(serviceConfig.uri)')

# Create an order
curl -X POST "$FUNCTION_URL/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-001",
    "customerId": "CUST-100",
    "totalAmount": 99.99
  }'
```

### Pub/Sub Function

```bash
# Publish a message to the orders topic
gcloud pubsub topics publish orders \
  --message='{
    "orderId": "ORD-002",
    "customerId": "CUST-200",
    "totalAmount": 149.99
  }'

# View function logs
gcloud functions logs read pubsub-order-processor --gen2 --region=us-central1
```

### Scheduled Function

```bash
# Trigger the scheduler job manually
gcloud scheduler jobs run daily-report --location=us-central1

# View function logs
gcloud functions logs read scheduled-task-function --gen2 --region=us-central1
```

## Configuration

### Startup.cs

The `Startup` class configures Dispatch and Google Cloud Functions:

```csharp
public class Startup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        // Configure Dispatch messaging
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Startup).Assembly);
            _ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
        });

        // Configure serverless hosting
        services.AddExcaliburGoogleCloudFunctionsServerless(opts =>
        {
            opts.EnableColdStartOptimization = true;
            opts.EnableDistributedTracing = true;
            opts.GoogleCloudFunctions.Runtime = "dotnet8";
        });
    }
}
```

### Cold Start Optimization

The sample includes cold start optimization settings:

- Minimal service registration
- Lazy initialization where possible
- Efficient serializer configuration

For production, consider:
- Setting minimum instances for critical functions
- Using Cloud Run for more control over instance lifecycle
- Implementing connection pooling for database connections

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   HTTP Client   │     │   Pub/Sub       │     │ Cloud Scheduler │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  HttpFunction   │     │ PubSubFunction  │     │ScheduledFunction│
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │    IDispatcher         │
                    │  (Dispatch Pipeline)   │
                    └───────────┬────────────┘
                                │
                                ▼
                    ┌────────────────────────┐
                    │   Event Handlers       │
                    │ (OrderCreatedHandler,  │
                    │  ScheduledTaskHandler) │
                    └────────────────────────┘
```

## Cost Considerations

Google Cloud Functions pricing is based on:
- **Invocations**: First 2 million/month free
- **Compute Time**: Based on memory and CPU allocated
- **Networking**: Egress charges may apply

Tips for cost optimization:
1. Use minimum memory allocation (256MB is often sufficient)
2. Set appropriate timeouts
3. Use Pub/Sub for high-volume event processing
4. Consider Cloud Run for predictable workloads

## Related Samples

- [Azure Functions Sample](../AzureFunctions/) - Azure serverless
- [AWS Lambda Sample](../AwsLambda/) - AWS serverless

## Learn More

- [Google Cloud Functions Documentation](https://cloud.google.com/functions/docs)
- [Functions Framework for .NET](https://github.com/GoogleCloudPlatform/functions-framework-dotnet)
- [Cloud Pub/Sub](https://cloud.google.com/pubsub/docs)
- [Cloud Scheduler](https://cloud.google.com/scheduler/docs)
- [Excalibur.Dispatch Documentation](../../../docs/)
