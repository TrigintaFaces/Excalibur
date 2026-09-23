---
sidebar_position: 2
title: ASP.NET Core
description: Host Excalibur applications in ASP.NET Core
---

# ASP.NET Core Deployment

ASP.NET Core is the most common hosting model for Excalibur applications, combining web API capabilities with background processing.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Hosting.Web  # for ASP.NET Core integration
  ```
- Familiarity with [getting started](../getting-started/index.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Minimal API Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EventStore")!;

// Add Excalibur services
builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.UseSqlServer(opts => opts.ConnectionString(connectionString));
        es.AddRepository<OrderAggregate, OrderId>();
        es.UseIntervalSnapshots(100);
    });

    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(opts => opts.ConnectionString(connectionString));
        outbox.EnableBackgroundProcessing();
    });
});

// Add controllers or minimal APIs
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();
```

## Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IEventSourcedRepository<OrderAggregate, OrderId> _repository;

    public OrdersController(
        IDispatcher dispatcher,
        IEventSourcedRepository<OrderAggregate, OrderId> repository)
    {
        _dispatcher = dispatcher;
        _repository = repository;
    }

    [HttpPost]
    public async Task<ActionResult<OrderId>> CreateOrder(
        CreateOrderCommand command,
        CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(command, ct);

        return result.Match<OrderId, ActionResult<OrderId>>(
            onSuccess: id => CreatedAtAction(nameof(GetOrder), new { id }, id),
            onFailure: problem => Problem(problem?.Detail, statusCode: problem?.Status));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(
        OrderId id,
        CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);

        if (order is null)
            return NotFound();

        return Ok(OrderDto.FromAggregate(order));
    }
}
```

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("EventStore"),
        name: "database",
        tags: new[] { "ready" })
    .AddCheck<OutboxHealthCheck>("outbox", tags: new[] { "ready" });

var app = builder.Build();

// Liveness - is the app running?
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // No checks
});

// Readiness - is the app ready to serve traffic?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

## Configuration

### appsettings.json

```json
{
    "ConnectionStrings": {
        "EventStore": "Server=localhost;Database=EventStore;Trusted_Connection=True;TrustServerCertificate=True"
    },
    "Excalibur": {
        "Outbox": {
            "BatchSize": 100,
            "PollingInterval": "00:00:05",
            "MaxRetryCount": 5
        },
        "Snapshots": {
            "Interval": 100
        }
    },
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Excalibur": "Debug"
        }
    }
}
```

### Environment-Specific Configuration

```
appsettings.json                 # Base configuration
appsettings.Development.json     # Local development
appsettings.Production.json      # Production overrides
```

## Observability

### Structured Logging

```csharp
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "OrderService");
});
```

### OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("OrderService"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddSqlClientInstrumentation()
               .AddEventSourcingInstrumentation()
               .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddMeter("Excalibur.Outbox.*")
               .AddOtlpExporter();
    });
```

## Error Handling

### Global Exception Handler

Use the framework's handler rather than writing your own. It is an ASP.NET Core `IExceptionHandler`, so
it plugs into the standard exception-handling middleware:

```csharp
// Package: Excalibur.Hosting.Web
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

app.UseExceptionHandler();
```

It returns RFC 9457 Problem Details with a trace ID and an exception ID, and it takes the status code
from the exception itself — so `ConcurrencyException` (409), `ResourceNotFoundException` (404) and the
framework's validation exceptions (400, with their errors under `errors`) need no mapping from you.
**Outside Development it replaces the details of any 5xx response with a generic message**, so an
internal exception message is never sent to a caller.

For your own exception types, derive from `ApiException` and pass the status code to its constructor, or
set `exception.Data["StatusCode"]`. The handler does not look for a property named `StatusCode` on other
exception types. For the full set of options, see
[Global Exception Handling](./global-exception-handling.md#custom-exception-status-codes).

:::caution Do not write the exception message to the response yourself
A hand-written handler that returns `exception.Message` sends internal detail — connection strings,
SQL, file paths, type names — to every caller in every environment. That is the main thing the
framework handler exists to prevent.
:::

## Background Processing

### Hosted Services

The outbox processor runs as a hosted service:

```csharp
outbox.EnableBackgroundProcessing();  // Adds OutboxBackgroundService
```

### Custom Background Services

```csharp
public class ProjectionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProjectionBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var projector = scope.ServiceProvider.GetRequiredService<OrderProjector>();

            await projector.ProcessAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["OrderService.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'
services:
  orderservice:
    build: .
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__EventStore=Server=db;Database=EventStore;...
    depends_on:
      - db

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

## Production Considerations

### Graceful Shutdown

```csharp
var app = builder.Build();

app.Lifetime.ApplicationStopping.Register(() =>
{
    // Allow outbox processor to complete current batch
    logger.LogInformation("Application stopping, waiting for background tasks...");
});
```

### Connection Resiliency

Excalibur uses Dapper and ADO.NET for all SQL operations. Configure retry policies with Polly:

```csharp
builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString);
    });
}));

// Add resilience via Polly
builder.Services.AddResiliencePipeline("sql-retry", pipeline =>
{
    pipeline.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder().Handle<SqlException>()
    });
});
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 100;
    });
});

app.UseRateLimiter();
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Use health checks | Load balancer integration |
| Configure graceful shutdown | Complete in-flight work |
| Enable structured logging | Better observability |
| Use managed identity | No secrets in code |
| Configure retry policies | Handle transient failures |
| Set timeouts | Prevent resource exhaustion |

## Minimal API with Dispatch Bridge

For Minimal API projects, the `Excalibur.Dispatch.Hosting.AspNetCore` package provides a higher-level bridge that maps HTTP endpoints directly to Dispatch messages with zero boilerplate. Instead of manually resolving `IDispatcher` and converting results, you declare a mapping and the bridge handles request binding, dispatching, and HTTP response conversion.

See [Minimal API Hosting Bridge](./minimal-api-bridge.md) for the full reference.

## Project Organization

For feature-rich APIs, consider organizing code using **vertical slice architecture** -- group files by feature (Patients, Appointments) instead of by technical layer (Controllers, Services, Repositories). This pairs naturally with Dispatch's one-message-per-operation model.

See [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) for guidance and a [working healthcare sample](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/11-real-world/HealthcareApi).

## Content Negotiation

Dispatch provides custom ASP.NET Core formatters that bridge `ISerializerRegistry` with the MVC content negotiation pipeline. Any registered `ISerializer` with a `ContentType` automatically becomes a supported media type for both input and output.

```csharp
// 1. Register serializers
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
// MessagePack for content negotiation (application/x-msgpack)
builder.Services.AddMessagePackSerializer();

// 2. Add content negotiation formatters
builder.Services.AddControllers()
    .AddDispatchContentNegotiation();
```

This adds:
- **`DispatchInputFormatter`** — Deserializes request bodies using the matching `ISerializer` based on the `Content-Type` header
- **`DispatchOutputFormatter`** — Serializes response bodies using the matching `ISerializer` based on the `Accept` header

Clients can then request different formats:

```bash
# JSON (default)
curl -H "Accept: application/json" https://api.example.com/orders/123

# MessagePack
curl -H "Accept: application/x-msgpack" https://api.example.com/orders/123
```

:::note

Content negotiation requires the Dispatch serialization infrastructure, registered with `AddPluggableSerialization()`. Registration **order does not matter** — the formatters are constructed when `MvcOptions` is first materialised, after every serializer registration has run, so a serializer added after `AddDispatchContentNegotiation()` is still picked up. The formatters are inserted at position 0 in the MVC formatter list, so they take priority over the default formatters for matching content types.
:::

## See Also

- [Minimal API Hosting Bridge](./minimal-api-bridge.md) — Map HTTP endpoints to Dispatch messages with zero boilerplate
- [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) — Organize features as self-contained slices
- [Worker Services](../deployment/worker-services.md) — Deploy dedicated background workers for event processing
- [Kubernetes Deployment](../deployment/kubernetes.md) — Container orchestration patterns for Excalibur applications
- [Getting Started](../getting-started/index.md) — Quick start guide for Excalibur
- [Dependency Injection](../core-concepts/dependency-injection.md) — Service registration and DI patterns
- [Global Exception Handling](../deployment/global-exception-handling.md) — RFC 7807 problem details and error handling middleware
