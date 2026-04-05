---
sidebar_position: 8
title: Configuration - Environments & Transports
description: Configure Dispatch for different environments, transports, health checks, and observability
---

# Configuration: Environments & Transports

Configure Dispatch for different deployment environments, transport brokers, health monitoring, and observability.

## Before You Start

- Completed [Configuration](./configuration.md) basics
- Familiarity with [.NET environments](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments)

## Configuration from appsettings.json

Bind configuration from your settings file. `DispatchOptions` supports nested options for cross-cutting concerns:

```json title="appsettings.json"
{
  "Dispatch": {
    "DefaultTimeout": "00:00:30",
    "EnableMetrics": true,
    "Security": {
      "EnableEncryption": false,
      "EnableValidation": true
    },
    "Observability": {
      "Enabled": true,
      "EnableTracing": true,
      "EnableMetrics": true
    },
    "Resilience": {
      "DefaultRetryCount": 3,
      "EnableCircuitBreaker": false
    },
    "Caching": {
      "Enabled": false,
      "DefaultExpiration": "00:05:00"
    }
  }
}
```

```csharp
// Program.cs
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Bind all Dispatch options from appsettings (including nested options)
builder.Services.Configure<DispatchOptions>(
    builder.Configuration.GetSection("Dispatch"));
```

## Transport Configuration

Configure transports through the `AddDispatch()` builder using `Use{Transport}()` methods:

### Single Transport

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure transport through the builder (recommended)
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
});
```

All five transports follow the same pattern:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseAzureServiceBus(asb => asb.ConnectionString("..."));
    dispatch.UseAwsSqs(sqs => sqs.Region("us-east-1"));
    dispatch.UseGooglePubSub(pubsub => pubsub.ProjectId("my-project"));
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
});
```

:::tip Standalone methods still work
You can also register transports directly on `IServiceCollection` if preferred:
```csharp
builder.Services.AddKafkaTransport(options => { /* ... */ });
```
The builder `Use{Transport}()` methods are thin wrappers that delegate to these standalone methods.
:::

See [Transports](../transports/index.md) for transport-specific setup guides.

### Named Transports

Register multiple instances of the same transport with different names:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseKafka("analytics", kafka => kafka.BootstrapServers("analytics:9092"));
});
```

### Multi-Transport Routing

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Transports
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));

    // Configure routing
    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Route<PaymentProcessedEvent>().To("rabbitmq")
            .Default("rabbitmq");
    });
});
```

## Environment-Specific Configuration

### Development vs Production

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    if (builder.Environment.IsDevelopment())
    {
        // Development: add debug middleware
        dispatch.UseMiddleware<DebugMiddleware>();
    }
});

// Environment-specific transport
if (builder.Environment.IsDevelopment())
{
    // In development: messages dispatch in-process (no broker needed)
    // No transport registration means in-process dispatch by default
}
else
{
    builder.Services.AddKafkaTransport(options =>
    {
        options.BootstrapServers = builder.Configuration["Kafka:Servers"];
    });
}
```

### Using IConfiguration

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Bind from configuration
builder.Services.AddOptions<DispatchOptions>()
    .Bind(builder.Configuration.GetSection("Dispatch"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Health Checks

Add health checks for Dispatch components:

```csharp
builder.Services.AddHealthChecks()
    .AddTransportHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
```

## Observability Configuration

### OpenTelemetry Integration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch");
        tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        metrics.AddOtlpExporter();
    });
```

### Logging Configuration

```csharp
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Excalibur.Dispatch": "Debug",
      "Excalibur.Dispatch.Pipeline": "Trace"
    }
  }
}
```

## See Also

- [Configuration](./configuration.md) -- Basic setup and builder API
- [Advanced Configuration](./configuration-advanced.md) -- Validation, patterns, and API reference
- [Transports](../transports/index.md) -- Transport-specific configuration guides
- [Observability](../observability/index.md) -- Tracing, metrics, and monitoring
