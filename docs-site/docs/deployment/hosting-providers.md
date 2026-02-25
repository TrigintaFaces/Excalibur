---
sidebar_position: 2
title: Hosting Providers
description: Per-hosting-model setup for ASP.NET Core, AWS Lambda, Azure Functions, and Google Cloud Functions.
---

# Hosting Providers

Dispatch provides dedicated hosting packages for each deployment model. Each package handles framework-specific lifecycle, DI integration, and cold-start optimization.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and your target hosting model

## Hosting Packages

| Package | Target | Use Case |
|---------|--------|----------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | ASP.NET Core | Web APIs, background services, long-running hosts |
| `Excalibur.Dispatch.Hosting.AwsLambda` | AWS Lambda | Event-driven serverless on AWS |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Azure Functions | Event-driven serverless on Azure |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Google Cloud Functions | Event-driven serverless on GCP |
| `Excalibur.Dispatch.Hosting.Lambda` | Generic Lambda | Shared Lambda abstractions |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | All serverless | Shared serverless base types |

---

## ASP.NET Core

The primary hosting model for web APIs and long-running services.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

### Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Dispatch with ASP.NET Core integration
builder.AddDispatch(configure: dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();
app.Run();
```

The `AddDispatch()` extension on `WebApplicationBuilder` integrates Dispatch with the ASP.NET Core host, registering middleware, health checks, and background services.

### Features

- Full middleware pipeline integration
- Background service hosting for outbox processors, leader election
- Health check registration
- OpenTelemetry tracing integration
- Request-scoped dispatcher via dependency injection

---

## AWS Lambda

Event-driven serverless hosting for AWS workloads.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AwsLambda
```

### Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

// In your Lambda startup
services.AddAwsLambdaServerless();

// Or with configuration
services.AddAwsLambdaServerless(options =>
{
    // Configure serverless-specific options
});
```

### Cold Start Optimization

Lambda cold starts require careful initialization:

- **Minimize DI registrations** — Only register what the function needs
- **Use static initialization** — Pre-warm the DI container outside the handler
- **Avoid leader election** — Not applicable in serverless (no persistent instances)
- **Use manual outbox processing** — `IOutboxProcessor` for on-demand trigger instead of background services

### Example Handler

```csharp
public class OrderFunction
{
    private readonly IDispatcher _dispatcher;

    public OrderFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        var command = JsonSerializer.Deserialize<CreateOrderCommand>(request.Body);
        var result = await _dispatcher.DispatchAsync(command!, CancellationToken.None);
        return new APIGatewayProxyResponse { StatusCode = 200 };
    }
}
```

For a detailed deployment guide, see [AWS Lambda Deployment](./aws-lambda.md).

---

## Azure Functions

Event-driven serverless hosting for Azure workloads.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AzureFunctions
```

### Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

// In your Functions startup
services.AddAzureFunctionsServerless();

// Or with configuration
services.AddAzureFunctionsServerless(options =>
{
    // Configure serverless-specific options
});
```

### Example Function

```csharp
public class OrderFunction
{
    private readonly IDispatcher _dispatcher;

    public OrderFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Function("CreateOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var command = await req.ReadFromJsonAsync<CreateOrderCommand>();
        await _dispatcher.DispatchAsync(command!, CancellationToken.None);

        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}
```

### Lifecycle Considerations

- Azure Functions uses `IHost` — Dispatch integrates via standard DI
- **Durable Functions** can coordinate long-running workflows alongside Dispatch sagas
- Use `IOutboxProcessor` for manual trigger in consumption plan (no background services)

For a detailed deployment guide, see [Azure Functions Deployment](./azure-functions.md).

---

## Google Cloud Functions

Event-driven serverless hosting for Google Cloud workloads.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.GoogleCloudFunctions
```

### Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

// In your Cloud Functions startup
services.AddGoogleCloudFunctionsServerless();

// Or with configuration
services.AddGoogleCloudFunctionsServerless(options =>
{
    // Configure serverless-specific options
});
```

### Example Function

```csharp
public class OrderFunction : IHttpFunction
{
    private readonly IDispatcher _dispatcher;

    public OrderFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task HandleAsync(HttpContext context)
    {
        var command = await context.Request.ReadFromJsonAsync<CreateOrderCommand>();
        await _dispatcher.DispatchAsync(command!, context.RequestAborted);
        context.Response.StatusCode = 200;
    }
}
```

For a detailed deployment guide, see [Google Cloud Functions Deployment](./google-cloud-functions.md).

---

## Serverless Considerations

All serverless hosting models share common constraints:

| Concern | Recommendation |
|---------|---------------|
| **Background services** | Not available — use `IOutboxProcessor` for on-demand processing |
| **Leader election** | Not applicable — no persistent instances |
| **Long-running sagas** | Use external orchestration (Step Functions, Durable Functions) |
| **Cold starts** | Minimize DI graph, use static initialization |
| **Connection pooling** | Use connection string pooling, avoid `IUnitOfWork` across requests |
| **Idempotency** | Critical — serverless may retry invocations |

### Integration with Leader Election

Leader election is designed for multi-instance deployments (Kubernetes, App Service), not serverless:

```csharp
// ASP.NET Core — use leader election for background processing
services.AddExcaliburLeaderElection();

// Serverless — use manual outbox processing instead
// The function invocation IS the processing trigger
```

## See Also

- [AWS Lambda Deployment](./aws-lambda.md) — Full AWS Lambda deployment guide
- [Azure Functions Deployment](./azure-functions.md) — Full Azure Functions deployment guide
- [Google Cloud Functions Deployment](./google-cloud-functions.md) — Full GCP deployment guide
- [Kubernetes Deployment](./kubernetes.md) — Container orchestration deployment
- [Leader Election](../leader-election/index.md) — Background processing coordination
