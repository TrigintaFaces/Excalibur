---
sidebar_position: 2
title: Deployment Guide
description: Cloud-native deployment patterns for Dispatch and Excalibur
---

# Deployment Guide

This guide covers production deployment patterns for Excalibur applications across Kubernetes, Azure, AWS, and traditional hosting environments.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Hosting.Web
  ```
- Familiarity with [ASP.NET Core deployment](../deployment/aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

Excalibur supports multiple deployment scenarios from simple web applications to complex distributed systems:

| Option | Best For | Packages |
|--------|----------|----------|
| ASP.NET Core | Web APIs, microservices | `Excalibur.Dispatch`, `Excalibur.Hosting.Web` |
| Azure Functions | Serverless, event-driven | `Excalibur.Dispatch`, `Excalibur.Hosting.Serverless` |
| AWS Lambda | Serverless, event-driven | `Excalibur.Dispatch`, `Excalibur.Hosting.Serverless` |
| Background Services | Job processing, workers | `Excalibur.Dispatch`, `Excalibur.Hosting.Jobs` |
| Kubernetes | Container orchestration | All packages |

---

## Kubernetes

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/MyApp/MyApp.csproj", "src/MyApp/"]
RUN dotnet restore "src/MyApp/MyApp.csproj"
COPY . .
WORKDIR "/src/src/MyApp"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER app
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### Health Check Endpoints

Configure ASP.NET Core health checks for Kubernetes probes:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddCheck<DispatchHealthCheck>("dispatch")
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<MessageBrokerHealthCheck>("messagebroker");

var app = builder.Build();

// Liveness probe - is the process running?
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // No dependency checks
});

// Readiness probe - can the app accept traffic?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Full health check for monitoring
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dispatch-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dispatch-api
  template:
    metadata:
      labels:
        app: dispatch-api
    spec:
      containers:
      - name: dispatch-api
        image: myregistry/dispatch-api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Default
          valueFrom:
            secretKeyRef:
              name: dispatch-secrets
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
```

### Horizontal Pod Autoscaling

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: dispatch-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: dispatch-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### ConfigMap for Settings

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: dispatch-config
data:
  appsettings.Production.json: |
    {
      "Dispatch": {
        "DefaultTimeout": "00:00:30",
        "MaxRetries": 3,
        "EnableMetrics": true
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft": "Warning"
        }
      }
    }
```

---

## Azure

### Azure App Service

```csharp
// Program.cs - Azure App Service configuration
var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault for secrets
if (builder.Environment.IsProduction())
{
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Dispatch services
builder.Services.AddDispatch();
builder.Services.AddAzureServiceBusTransport("servicebus", sb =>
{
    sb.ConnectionString(builder.Configuration["ServiceBus:ConnectionString"]!);
});
```

### Azure Functions

```csharp
// Startup.cs
[assembly: FunctionsStartup(typeof(Startup))]

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddDispatch();
        // Configure Azure Service Bus via options pattern
        builder.Services.Configure<AzureServiceBusOptions>(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
        });
    }
}

// ServiceBusTriggerFunction.cs
public class ServiceBusTriggerFunction
{
    private readonly IDispatcher _dispatcher;

    public ServiceBusTriggerFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [FunctionName("ProcessMessage")]
    public async Task Run(
        [ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")]
        string message,
        CancellationToken ct)
    {
        var command = JsonSerializer.Deserialize<MyCommand>(message);
        await _dispatcher.DispatchAsync(command, ct);
    }
}
```

### Bicep Deployment

```bicep
param location string = resourceGroup().location
param appName string

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      healthCheckPath: '/health'
      alwaysOn: true
    }
  }
}
```

---

## AWS

### AWS Lambda

```csharp
// Function.cs
public class Function
{
    private readonly IDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Function).Assembly);
            dispatch.UseAwsSqs(sqs =>
            {
                sqs.Region("us-east-1");
            });
        });

        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            var command = JsonSerializer.Deserialize<MyCommand>(record.Body);
            await _dispatcher.DispatchAsync(command, CancellationToken.None);
        }
    }
}
```

### SAM Template

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31

Resources:
  DispatchFunction:
    Type: AWS::Serverless::Function
    Properties:
      Handler: MyApp::MyApp.Function::FunctionHandler
      Runtime: dotnet9
      MemorySize: 512
      Timeout: 30
      Events:
        SQSEvent:
          Type: SQS
          Properties:
            Queue: !GetAtt DispatchQueue.Arn
            BatchSize: 10

  DispatchQueue:
    Type: AWS::SQS::Queue
    Properties:
      QueueName: dispatch-queue
      VisibilityTimeout: 60
```

### ECS Fargate

```yaml
# task-definition.json
{
  "family": "dispatch-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "containerDefinitions": [
    {
      "name": "dispatch-api",
      "image": "123456789.dkr.ecr.us-east-1.amazonaws.com/dispatch-api:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3
      },
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/dispatch-api",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      }
    }
  ]
}
```

---

## Native AOT {#native-aot}

Dispatch supports Native AOT compilation for serverless and edge deployment scenarios. AOT-compiled applications start faster and have smaller memory footprints.

### Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  </PropertyGroup>
</Project>
```

### Source-Generated JSON Serialization

For AOT, use source-generated JSON serialization:

```csharp
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(OrderCreatedEvent))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderDto))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}

// Configure in Program.cs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
```

### AOT-Safe Handler Patterns

All Dispatch handlers are AOT-compatible when using source generators:

```csharp
// AOT-compatible - discovered at compile time
[AutoRegister]
public class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(
        CreateOrderCommand message,
        CancellationToken cancellationToken)
    {
        // Handler implementation - IActionHandler<T> returns Task (void)
        return Task.CompletedTask;
    }
}
```

### AOT Annotations

Dispatch uses proper AOT annotations for reflection-heavy code paths:

| Annotation | Usage |
|------------|-------|
| `[DynamicallyAccessedMembers]` | Types requiring member discovery |
| `[RequiresDynamicCode]` | Methods requiring runtime code generation |
| `[RequiresUnreferencedCode]` | Methods that may fail after trimming |

:::note Source Generator Coverage
Dispatch's 9 source generators provide complete AOT coverage for handlers, middleware, and pipelines. Manual handler registration requires additional annotations.
:::

### Publishing for AOT

```bash
# Windows x64
dotnet publish -c Release -r win-x64

# Linux x64
dotnet publish -c Release -r linux-x64

# macOS ARM64
dotnet publish -c Release -r osx-arm64
```

:::warning Visual Studio Requirement
Full native AOT linking on Windows requires the Visual Studio C++ build tools. The `dotnet publish` command performs C# compilation; native linking requires additional tools.
:::

### AOT Sample Project

A complete AOT sample is available at `samples/11-aot/Excalibur.Dispatch.Aot.Sample/`:

```bash
cd samples/11-aot/Excalibur.Dispatch.Aot.Sample
dotnet build -c Release
dotnet run -c Release
```

The sample demonstrates:
- Command/query/event dispatch patterns
- Source-generated JSON serialization
- C# 12 interceptors
- Zero trimming warnings

---

## Observability

### OpenTelemetry Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Excalibur.Dispatch")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://otel-collector:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("Dispatch")
        .AddOtlpExporter());
```

### Structured Logging

```csharp
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "DispatchApi")
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.Seq("http://seq:5341"));
```

---

## Configuration Best Practices

### Environment-Specific Settings

```csharp
// Use environment variables for secrets
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);
```

### Connection Resilience

```csharp
// Register connection factory using Func<SqlConnection> pattern (no EntityFramework)
builder.Services.AddSingleton<Func<SqlConnection>>(() =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    return new SqlConnection(connectionString);
});

// Configure retry policy for transient failures
builder.Services.AddPollyResilience(resilience =>
{
    resilience.ConfigureRetry(retry =>
    {
        retry.MaxRetryAttempts = 5;
        retry.Delay = TimeSpan.FromSeconds(2);
        retry.BackoffType = DelayBackoffType.Exponential;
    });
});
```

---

## Related Documentation

- [Security Guide](security.md) - Security hardening
- [Testing Guide](testing.md) - Testing strategies
- [Compliance](../compliance/index.md) - Regulatory requirements

## See Also

- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Hosting Dispatch in ASP.NET Core applications
- [Kubernetes Deployment](../deployment/kubernetes.md) — Container orchestration patterns and health checks
- [Docker Deployment](../deployment/docker.md) — Containerizing Dispatch applications with Docker
- [Worker Services](../deployment/worker-services.md) — Background service and job processing deployment
