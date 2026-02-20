---
sidebar_position: 2
title: ASP.NET Core
description: Host Excalibur applications in ASP.NET Core
---

# ASP.NET Core Deployment

ASP.NET Core is the most common hosting model for Excalibur applications, combining web API capabilities with background processing.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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
        es.AddRepository<OrderAggregate, OrderId>();
        es.UseIntervalSnapshots(100);
    });

    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(connectionString);
        outbox.EnableBackgroundProcessing();
    });
});

// Add SQL Server event sourcing provider (event store, snapshot store, outbox store)
builder.Services.AddSqlServerEventSourcing(connectionString);

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

        return result.Match<ActionResult<OrderId>>(
            success: id => CreatedAtAction(nameof(GetOrder), new { id }, id),
            failure: error => BadRequest(error));
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

```csharp
app.UseExceptionHandler(error =>
{
    error.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = exception switch
        {
            ConcurrencyException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        await context.Response.WriteAsJsonAsync(new
        {
            error = exception?.Message ?? "An error occurred"
        });
    });
});
```

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
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
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

See [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) for guidance and a [working healthcare sample](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/12-vertical-slice-api).

## See Also

- [Minimal API Hosting Bridge](./minimal-api-bridge.md) — Map HTTP endpoints to Dispatch messages with zero boilerplate
- [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) — Organize features as self-contained slices
- [Worker Services](../deployment/worker-services.md) — Deploy dedicated background workers for event processing
- [Kubernetes Deployment](../deployment/kubernetes.md) — Container orchestration patterns for Excalibur applications
- [Getting Started](../getting-started/index.md) — Quick start guide for Excalibur
- [Dependency Injection](../core-concepts/dependency-injection.md) — Service registration and DI patterns
- [Global Exception Handling](../deployment/global-exception-handling.md) — RFC 7807 problem details and error handling middleware
