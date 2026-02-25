---
sidebar_position: 1
---

# Health Checks

Implementing comprehensive health checks for Excalibur applications to enable monitoring, load balancing, and automated recovery.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
  ```
- Familiarity with [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) and [observability](./index.md)

## Overview

Health checks are critical for:
- **Load Balancer Management**: Route traffic only to healthy instances
- **Orchestration**: Kubernetes liveness/readiness probes
- **Monitoring**: Alert on service degradation
- **Automated Recovery**: Restart unhealthy instances

## Basic Health Check

### ASP.NET Core Health Checks

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

var app = builder.Build();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

**Endpoint Response:**
```http
GET /health HTTP/1.1
Host: api.example.com

HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy"
}
```

## Dispatcher Health Check

Monitor Excalibur pipeline health:

```csharp
public class DispatcherHealthCheck : IHealthCheck
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<DispatcherHealthCheck> _logger;

    public DispatcherHealthCheck(
        IDispatcher dispatcher,
        ILogger<DispatcherHealthCheck> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send a test ping command
            var ping = new PingCommand();
            await _dispatcher.DispatchAsync(ping, cancellationToken);

            return HealthCheckResult.Healthy("Dispatcher is operational");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatcher health check failed");
            return HealthCheckResult.Unhealthy(
                "Dispatcher failed to process test command",
                ex);
        }
    }

    // Ping action for health checks
    private record PingAction : IDispatchAction;

    // Handler that always succeeds
    private class PingHandler : IActionHandler<PingAction>
    {
        public Task HandleAsync(
            PingAction action,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

// Register in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<DispatcherHealthCheck>("dispatcher");
```

## Database Health Check

Monitor SQL Server event store connectivity:

```csharp
public class EventStoreHealthCheck : IHealthCheck
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventStoreHealthCheck> _logger;

    public EventStoreHealthCheck(
        IEventStore eventStore,
        ILogger<EventStoreHealthCheck> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to query event store metadata
            // This validates connection without loading data
            var testAggregateId = Guid.NewGuid().ToString();
            var events = await _eventStore.LoadAsync(
                testAggregateId,
                "HealthCheck",
                cancellationToken);

            return HealthCheckResult.Healthy("Event store is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event store health check failed");
            return HealthCheckResult.Unhealthy(
                "Event store is not accessible",
                ex);
        }
    }
}

// Using AspNetCore.HealthChecks.SqlServer
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("EventStore"),
        name: "event-store-db",
        tags: new[] { "db", "sql" });
```

## Outbox Health Check

The built-in `OutboxHealthCheck` monitors the outbox background service using `BackgroundServiceHealthState`. Register it with the health check builder:

```csharp
builder.Services.AddHealthChecks()
    .AddOutboxHealthCheck(); // Uses built-in OutboxHealthCheck
```

The health check evaluates:
- **Healthy**: Service is running, processing normally with acceptable failure rate
- **Degraded**: Service is running but has elevated failure rate or recent inactivity
- **Unhealthy**: Service is not running, has high failure rate, or no activity beyond timeout

Configure thresholds via `OutboxHealthCheckOptions`:

```csharp
builder.Services.AddHealthChecks()
    .AddOutboxHealthCheck(options =>
    {
        options.UnhealthyInactivityTimeout = TimeSpan.FromMinutes(5); // Default: 5 min
        options.DegradedInactivityTimeout = TimeSpan.FromMinutes(2);  // Default: 2 min
        options.UnhealthyFailureRatePercent = 20.0;                   // Default: 20%
        options.DegradedFailureRatePercent = 5.0;                     // Default: 5%
    });
```

The health check reads from `BackgroundServiceHealthState`, which is updated by the outbox background service during processing cycles. It tracks `IsRunning`, `TotalProcessed`, `TotalFailed`, `TotalCycles`, and `LastActivityTime`.

## Redis Health Check

For applications using Redis for caching or leader election:

```csharp
// Using AspNetCore.HealthChecks.Redis
builder.Services.AddHealthChecks()
    .AddRedis(
        redisConnectionString: builder.Configuration["Redis:ConnectionString"],
        name: "redis",
        tags: new[] { "cache", "redis" });
```

## Message Bus Health Check

Monitor Azure Service Bus or RabbitMQ:

```csharp
// Azure Service Bus
builder.Services.AddHealthChecks()
    .AddAzureServiceBusQueue(
        connectionString: builder.Configuration["ServiceBus:ConnectionString"],
        queueName: "commands",
        name: "servicebus-commands",
        tags: new[] { "messaging", "servicebus" });

// RabbitMQ
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: builder.Configuration["RabbitMQ:ConnectionString"],
        name: "rabbitmq",
        tags: new[] { "messaging", "rabbitmq" });
```

## Health Check UI

Add a detailed health check UI for development:

```bash
dotnet add package AspNetCore.HealthChecks.UI
dotnet add package AspNetCore.HealthChecks.UI.InMemory.Storage
```

```csharp
// Program.cs
builder.Services
    .AddHealthChecks()
    .AddCheck<DispatcherHealthCheck>("dispatcher")
    .AddCheck<EventStoreHealthCheck>("event-store")
    .AddCheck<OutboxHealthCheck>("outbox")
    .AddSqlServer(builder.Configuration.GetConnectionString("EventStore"), name: "sql-server")
    .AddRedis(builder.Configuration["Redis:ConnectionString"], name: "redis");

// Add UI
builder.Services
    .AddHealthChecksUI(setup =>
    {
        setup.SetEvaluationTimeInSeconds(30); // Refresh every 30 seconds
        setup.MaximumHistoryEntriesPerEndpoint(50);
    })
    .AddInMemoryStorage();

var app = builder.Build();

// Map endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
});
```

Access UI at: `https://localhost:5001/health-ui`

## Detailed Health Check Response

Return detailed JSON with component status:

```csharp
app.MapHealthChecks("/health/detailed", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration,
                exception = e.Value.Exception?.Message,
                data = e.Value.Data
            })
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(result);
    }
});
```

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "checks": [
    {
      "name": "dispatcher",
      "status": "Healthy",
      "description": "Dispatcher is operational",
      "duration": "00:00:00.0123456",
      "exception": null,
      "data": {}
    },
    {
      "name": "event-store",
      "status": "Healthy",
      "description": "Event store is accessible",
      "duration": "00:00:00.0456789",
      "exception": null,
      "data": {}
    },
    {
      "name": "outbox",
      "status": "Degraded",
      "description": "Oldest outbox message is 3.2 minutes old",
      "duration": "00:00:00.0234567",
      "exception": null,
      "data": {
        "OldestMessageAge": "00:03:12",
        "OldestMessageId": "abc-123"
      }
    }
  ]
}
```

## Aggregated Health Checks

Instead of registering individual health checks one by one, use `AddDispatchHealthChecks()` to register all available checks in a single call. The extension conditionally registers only the checks whose prerequisite services are present in the DI container.

### AddDispatchHealthChecks()

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register all available Dispatch health checks
builder.Services.AddHealthChecks()
    .AddDispatchHealthChecks();
```

To exclude specific checks, pass a configuration action:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register all except leader election
builder.Services.AddHealthChecks()
    .AddDispatchHealthChecks(options =>
    {
        options.IncludeLeaderElection = false;
    });
```

### DispatchHealthCheckOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IncludeOutbox` | `bool` | `true` | Register outbox health check when `IOutboxPublisher` is in DI |
| `IncludeInbox` | `bool` | `true` | Register inbox health check when `IInboxStore` is in DI |
| `IncludeSaga` | `bool` | `true` | Register saga health check when `ISagaMonitoringService` is in DI |
| `IncludeLeaderElection` | `bool` | `true` | Register leader election health check when `ILeaderElection` is in DI |

### Conditional Registration

`AddDispatchHealthChecks()` scans the `IServiceCollection` for prerequisite service registrations. A health check is added only when **both** conditions are met:

1. The corresponding `DispatchHealthCheckOptions` flag is `true` (all default to `true`).
2. The prerequisite service type is registered in the DI container.

This means you can safely call `AddDispatchHealthChecks()` in every application. Services that are not registered are silently skipped:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Only outbox is registered, so only the outbox health check is added
builder.Services.AddExcaliburOutbox(options => { /* ... */ });

builder.Services.AddHealthChecks()
    .AddDispatchHealthChecks(); // Only adds outbox check
```

### Combining with Custom Health Checks

`AddDispatchHealthChecks()` returns `IHealthChecksBuilder`, so you can chain it with additional checks:

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddHealthChecks()
    .AddDispatchHealthChecks()
    .AddSqlServer(connectionString, name: "sql-server", tags: new[] { "db" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache" })
    .AddCheck("self", () => HealthCheckResult.Healthy());
```

## Kubernetes Integration

### Liveness Probe

Determines if pod should be restarted:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: dispatch-api
spec:
  containers:
  - name: api
    image: myregistry.azurecr.io/dispatch-api:latest
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 10
      timeoutSeconds: 5
      failureThreshold: 3
```

**Liveness endpoint:**
```csharp
// Only check critical components
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck<DispatcherHealthCheck>("dispatcher");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live") || check.Name == "dispatcher"
});
```

### Readiness Probe

Determines if pod should receive traffic:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: dispatch-api
spec:
  containers:
  - name: api
    image: myregistry.azurecr.io/dispatch-api:latest
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 5
      timeoutSeconds: 3
      successThreshold: 1
      failureThreshold: 3
```

**Readiness endpoint with aggregated checks:**
```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddHealthChecks()
    .AddDispatchHealthChecks()
    .AddSqlServer(connectionString, tags: new[] { "ready" })
    .AddRedis(redisConnectionString, tags: new[] { "ready" });

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

### Startup Probe

Gives slow-starting pods more time:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: dispatch-api
spec:
  containers:
  - name: api
    image: myregistry.azurecr.io/dispatch-api:latest
    startupProbe:
      httpGet:
        path: /health/startup
        port: 8080
      initialDelaySeconds: 0
      periodSeconds: 10
      timeoutSeconds: 3
      failureThreshold: 30  # 5 minutes total
```

## Azure App Service Health Check

Enable health check monitoring in Azure:

```bash
az webapp config set \
  --resource-group myResourceGroup \
  --name myapp \
  --health-check-path "/health"
```

**appsettings.json:**
```json
{
  "HealthChecks": {
    "Enabled": true,
    "Path": "/health",
    "Port": null,
    "FailureThreshold": 3,
    "SuccessStatusCodes": "200-299",
    "Interval": "00:01:00"
  }
}
```

## AWS Application Load Balancer

Configure target group health checks:

```hcl
# Terraform
resource "aws_lb_target_group" "api" {
  name     = "dispatch-api-tg"
  port     = 80
  protocol = "HTTP"
  vpc_id   = aws_vpc.main.id

  health_check {
    enabled             = true
    path                = "/health"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }
}
```

## Google Cloud Load Balancer

Configure backend service health check:

```bash
gcloud compute health-checks create http dispatch-api-health \
  --port=8080 \
  --request-path=/health \
  --check-interval=30s \
  --timeout=5s \
  --healthy-threshold=2 \
  --unhealthy-threshold=3

gcloud compute backend-services create dispatch-api-backend \
  --health-checks=dispatch-api-health \
  --global
```

## Custom Health Check

Create domain-specific health checks:

```csharp
public class AggregateLoadHealthCheck : IHealthCheck
{
    private readonly IEventSourcedRepository<Order> _repository;
    private readonly ILogger<AggregateLoadHealthCheck> _logger;

    public AggregateLoadHealthCheck(
        IEventSourcedRepository<Order> repository,
        ILogger<AggregateLoadHealthCheck> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try loading a known test aggregate
            var testOrderId = "health-check-order";
            var order = await _repository.GetByIdAsync(
                testOrderId,
                cancellationToken);

            // If aggregate doesn't exist, create it
            if (order == null)
            {
                order = Order.Create(testOrderId, "Health Check Order");
                await _repository.SaveAsync(order, cancellationToken);
            }

            return HealthCheckResult.Healthy(
                "Aggregate load/save is operational");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregate load health check failed");
            return HealthCheckResult.Unhealthy(
                "Failed to load/save aggregate",
                ex);
        }
    }
}
```

## Materialized View Health Check

Monitor materialized view infrastructure health:

```csharp
// Registration via fluent builder
services.AddMaterializedViews(builder =>
{
    builder.AddBuilder<OrderSummaryView, OrderSummaryViewBuilder>()
           .UseSqlServer(connectionString)
           .WithHealthChecks(options =>
           {
               options.Name = "materialized-views";
               options.Tags = new[] { "ready", "event-sourcing" };
               options.StalenessThreshold = TimeSpan.FromMinutes(5);
               options.FailureRateThresholdPercent = 10.0;
               options.IncludeDetails = true;
           });
});
```

**Health Check Evaluation:**

| State | Condition |
|-------|-----------|
| **Healthy** | All views current, failure rate acceptable |
| **Degraded** | Views stale OR failure rate exceeds threshold |
| **Unhealthy** | No views registered OR store unavailable |

**Response:**
```json
{
  "name": "materialized-views",
  "status": "Healthy",
  "description": "3 materialized views healthy.",
  "data": {
    "registeredViews": 3,
    "viewNames": ["OrderSummary", "CustomerStats", "ProductAnalytics"],
    "maxStaleness": "00:00:45",
    "failureRatePercent": 0
  }
}
```

See [Materialized Views](../event-sourcing/materialized-views.md) for complete documentation.

## Leader Election Health Check

Monitor leader election status:

```csharp
public class LeaderElectionHealthCheck : IHealthCheck
{
    private readonly ILeaderElection _leaderElection;

    public LeaderElectionHealthCheck(ILeaderElection leaderElection)
    {
        _leaderElection = leaderElection;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var isLeader = _leaderElection.IsLeader;
        var leaderId = _leaderElection.LeaderId;

        return Task.FromResult(HealthCheckResult.Healthy(
            isLeader ? "This instance is the leader" : "Follower instance",
            data: new Dictionary<string, object>
            {
                ["IsLeader"] = isLeader,
                ["LeaderId"] = leaderId ?? "unknown"
            }));
    }
}
```

## Performance Metrics

Add metrics to health check responses:

```csharp
public class PerformanceHealthCheck : IHealthCheck
{
    private readonly ILogger<PerformanceHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        // Measure system metrics
        var process = Process.GetCurrentProcess();
        var cpuUsage = GetCpuUsage(process);
        var memoryUsage = process.WorkingSet64 / (1024 * 1024); // MB
        var threadCount = process.Threads.Count;

        var data = new Dictionary<string, object>
        {
            ["CpuUsagePercent"] = cpuUsage,
            ["MemoryUsageMB"] = memoryUsage,
            ["ThreadCount"] = threadCount,
            ["GCGen0Collections"] = GC.CollectionCount(0),
            ["GCGen1Collections"] = GC.CollectionCount(1),
            ["GCGen2Collections"] = GC.CollectionCount(2)
        };

        // Determine health status based on thresholds
        if (cpuUsage > 90 || memoryUsage > 2048)
        {
            return HealthCheckResult.Degraded(
                "High resource usage detected",
                data: data);
        }

        return HealthCheckResult.Healthy(
            "Performance metrics are normal",
            data: data);
    }

    private double GetCpuUsage(Process process)
    {
        // Simplified CPU usage calculation
        var startTime = DateTime.UtcNow;
        var startCpuUsage = process.TotalProcessorTime;

        Thread.Sleep(500);

        var endTime = DateTime.UtcNow;
        var endCpuUsage = process.TotalProcessorTime;

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;

        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        return cpuUsageTotal * 100;
    }
}
```

## Testing Health Checks

```csharp
public class DispatcherHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_ReturnsHealthy_WhenDispatcherWorks()
    {
        // Arrange
        var dispatcher = A.Fake<IDispatcher>();
        var logger = A.Fake<ILogger<DispatcherHealthCheck>>();
        var healthCheck = new DispatcherHealthCheck(dispatcher, logger);

        A.CallTo(() => dispatcher.DispatchAsync(
            A<PingCommand>._,
            A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("Dispatcher is operational");
    }

    [Fact]
    public async Task HealthCheck_ReturnsUnhealthy_WhenDispatcherFails()
    {
        // Arrange
        var dispatcher = A.Fake<IDispatcher>();
        var logger = A.Fake<ILogger<DispatcherHealthCheck>>();
        var healthCheck = new DispatcherHealthCheck(dispatcher, logger);

        A.CallTo(() => dispatcher.DispatchAsync(
            A<PingCommand>._,
            A<CancellationToken>._))
            .Throws(new InvalidOperationException("Dispatcher error"));

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldContain("failed to process");
        result.Exception.ShouldNotBeNull();
    }
}
```

## Best Practices

### 1. Separate Liveness and Readiness

**Liveness** (restart criteria):
- Application deadlock
- Unrecoverable error state
- Process hangs

**Readiness** (traffic routing criteria):
- Database connection lost
- Dependency unavailable
- Startup incomplete

### 2. Timeout Configuration

```csharp
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(2);
    options.Period = TimeSpan.FromSeconds(30);
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

### 3. Avoid Expensive Operations

```csharp
// BAD - Loads entire aggregate
public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken)
{
    var order = await _repository.GetByIdAsync("test-order", cancellationToken);
    return HealthCheckResult.Healthy();
}

// GOOD - Just checks connectivity
public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken)
{
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync(cancellationToken);
    return HealthCheckResult.Healthy();
}
```

### 4. Include Meaningful Data

```csharp
return HealthCheckResult.Degraded(
    "High memory usage",
    data: new Dictionary<string, object>
    {
        ["MemoryUsageMB"] = memoryUsage,
        ["ThresholdMB"] = threshold,
        ["ProcessId"] = Process.GetCurrentProcess().Id
    });
```

### 5. Cache Health Check Results

For expensive checks, cache results:

```csharp
public class CachedHealthCheck : IHealthCheck
{
    private readonly IHealthCheck _innerCheck;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"health-check-{context.Registration.Name}";

        if (_cache.TryGetValue<HealthCheckResult>(cacheKey, out var cached))
        {
            return cached;
        }

        var result = await _innerCheck.CheckHealthAsync(context, cancellationToken);

        _cache.Set(cacheKey, result, _cacheDuration);

        return result;
    }
}
```

## Monitoring Integration

Health check results feed into:

- **Grafana**: See [Grafana Dashboards](./grafana-dashboards.md)
- **Datadog**: See [Datadog Integration](./datadog-integration.md)
- **Azure Monitor**: See [Azure Monitor](./azure-monitor.md)

## Next Steps

1. Implement basic `/health` endpoint
2. Add component-specific health checks (dispatcher, event store, outbox)
3. Use `AddDispatchHealthChecks()` for one-line aggregated registration
4. Configure Kubernetes probes (liveness, readiness, startup)
5. Add detailed health check UI for development
6. Integrate with monitoring platforms (Grafana, Datadog, Azure Monitor)
7. Set up alerts for degraded/unhealthy states
8. Test failure scenarios and recovery

## See Also

- [Observability Overview](./index.md) - Monitor Dispatch applications with OpenTelemetry, health checks, and integrations
- [Metrics Reference](./metrics-reference.md) - Complete catalog of 100+ available metrics
- [Resilience with Polly](../operations/resilience-polly.md) - Circuit breakers, retries, and resilience patterns
