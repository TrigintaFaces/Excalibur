# Serverless Samples

Serverless hosting patterns for Azure Functions, AWS Lambda, and Google Cloud Functions.

## Choosing a Serverless Platform

| Platform | Best For | Local Dev | Cold Start | Pricing Model |
|----------|----------|-----------|------------|---------------|
| **[Azure Functions](AzureFunctions/)** | Azure-native apps, .NET ecosystem | Azure Functions Core Tools + Azurite | ~1-2s (Consumption) | Pay-per-execution |
| **[AWS Lambda](AwsLambda/)** | AWS-native apps, SAM/CDK workflows | SAM CLI + LocalStack | ~1-2s (managed .NET 8) | Pay-per-execution |
| **[Google Cloud Functions](GoogleCloudFunctions/)** | GCP-native apps, Pub/Sub integration | Functions Framework | ~2-3s (gen2) | Pay-per-execution |

## Samples Overview

| Sample | Platform | Triggers | Local Dev |
|--------|----------|----------|-----------|
| **[AzureFunctions](AzureFunctions/)** | Azure Functions v4 | HTTP, Queue Storage, Timer | `func start` |
| **[AwsLambda](AwsLambda/)** | AWS Lambda (.NET 8) | API Gateway, SQS, EventBridge | `sam local start-api` |
| **[GoogleCloudFunctions](GoogleCloudFunctions/)** | GCF Gen2 (.NET 8) | HTTP, Pub/Sub, Cloud Scheduler | `dotnet run` |

## Platform Comparison

### Trigger Types

| Trigger Type | Azure Functions | AWS Lambda | Google Cloud Functions |
|--------------|-----------------|------------|------------------------|
| **HTTP** | HttpTrigger | API Gateway | HTTP |
| **Queue** | QueueTrigger, ServiceBusTrigger | SQS, SNS | Pub/Sub |
| **Scheduled** | TimerTrigger | EventBridge | Cloud Scheduler |
| **Storage** | BlobTrigger | S3 Events | Cloud Storage |

### Runtime & Performance

| Feature | Azure Functions | AWS Lambda | Google Cloud Functions |
|---------|-----------------|------------|------------------------|
| **Runtime** | .NET 8 Isolated | .NET 8 Managed | .NET 8 |
| **Max Timeout** | 10 min (Consumption) | 15 min | 9 min (HTTP), 60 min (Gen2) |
| **Memory** | 128 MB - 14 GB | 128 MB - 10 GB | 128 MB - 32 GB |
| **Cold Start** | ~1-2s | ~1-2s | ~2-3s |
| **Provisioned Concurrency** | Premium Plan | Yes | Min Instances |

### Local Development

| Tool | Platform | Command |
|------|----------|---------|
| Azure Functions Core Tools | Azure | `func start` |
| Azurite | Azure (storage emulator) | `azurite --silent` |
| SAM CLI | AWS | `sam local start-api` |
| LocalStack | AWS (full emulator) | `docker run localstack/localstack` |
| Functions Framework | GCP | `dotnet run` |

## Quick Start

### Azure Functions

```bash
cd samples/05-serverless/AzureFunctions

# Start local storage emulator (separate terminal)
azurite --silent --location ./azurite

# Run locally
func start

# Test
curl -X POST http://localhost:7071/api/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-001","customerId":"CUST-100","totalAmount":99.99}'
```

### AWS Lambda

```bash
cd samples/05-serverless/AwsLambda

# Build with SAM
sam build

# Run locally
sam local start-api

# Test
curl -X POST http://127.0.0.1:3000/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-001","customerId":"CUST-100","totalAmount":99.99}'
```

### Google Cloud Functions

```bash
cd samples/05-serverless/GoogleCloudFunctions

# Run locally with Functions Framework
dotnet run

# Test
curl -X POST http://localhost:8080/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-001","customerId":"CUST-100","totalAmount":99.99}'
```

## Key Concepts

### Dispatch Integration

All samples use the same Dispatch messaging patterns:

```csharp
// Create dispatch context for serverless
var context = DispatchContextInitializer.CreateDefaultContext();

// Dispatch events/commands through the pipeline
await dispatcher.DispatchAsync(orderEvent, context);
```

### Cold Start Optimization

Each hosting package includes cold start optimization:

```csharp
// Azure Functions
services.AddExcaliburAzureFunctionsServerless(opts =>
{
    opts.EnableColdStartOptimization = true;
});

// AWS Lambda
services.AddExcaliburAwsLambdaServerless(opts =>
{
    opts.EnableColdStartOptimization = true;
});

// Google Cloud Functions
services.AddExcaliburGoogleCloudFunctionsServerless(opts =>
{
    opts.EnableColdStartOptimization = true;
});
```

### Best Practices

1. **Minimize dependencies** - Only include necessary packages
2. **Use .NET 8** - Best cold start performance across all platforms
3. **Lazy initialization** - Defer heavy operations until first request
4. **Connection pooling** - Reuse connections for databases/messaging
5. **Keep functions warm** - Use health check pings for critical functions

## Prerequisites

| Platform | Required Tools |
|----------|---------------|
| Azure Functions | [.NET 8 SDK](https://dotnet.microsoft.com/download), [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local), [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) |
| AWS Lambda | [.NET 8 SDK](https://dotnet.microsoft.com/download), [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/), [Docker](https://www.docker.com/) |
| Google Cloud Functions | [.NET 8 SDK](https://dotnet.microsoft.com/download), [Google Cloud SDK](https://cloud.google.com/sdk/docs/install) |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Azure Functions hosting integration |
| `Excalibur.Dispatch.Hosting.AwsLambda` | AWS Lambda hosting integration |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Google Cloud Functions hosting integration |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | Shared serverless abstractions |

## What's Next?

- [02-messaging-transports/](../02-messaging-transports/) - Transport providers (RabbitMQ, Kafka, etc.)
- [04-reliability/](../04-reliability/) - Sagas and distributed transactions
- [07-observability/](../07-observability/) - Logging, tracing, and metrics

---

*Category: Serverless | Sprint 430*
