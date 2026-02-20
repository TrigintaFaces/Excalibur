---
sidebar_position: 3
title: Worker Services
description: Deploy dedicated background workers for event processing
---

# Worker Services

Worker Services are ideal for dedicated background processing tasks like outbox processing, projections, and CDC handlers. They run continuously without HTTP overhead.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Microsoft.Extensions.Hosting
  ```
- Familiarity with [getting started](../getting-started/index.md) and [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)

## When to Use Worker Services

| Scenario | Worker Service | ASP.NET Core |
|----------|---------------|--------------|
| Outbox processing | ✅ Dedicated | ✅ Integrated |
| Projections | ✅ Best choice | ⚠️ Possible |
| CDC handlers | ✅ Best choice | ⚠️ Possible |
| Saga orchestration | ✅ Best choice | ✅ Integrated |
| Long-running tasks | ✅ Best choice | ❌ Avoid |

## Basic Setup

```csharp
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EventStore")!;

builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.AddRepository<OrderAggregate, OrderId>();
    });

    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(connectionString)
              .EnableBackgroundProcessing()
              .WithProcessing(p =>
              {
                  p.BatchSize(500)
                   .PollingInterval(TimeSpan.FromSeconds(1))
                   .EnableParallelProcessing(4);
              });
    });
});

// Add SQL Server event sourcing provider separately
builder.Services.AddSqlServerEventSourcing(connectionString);

var host = builder.Build();
await host.RunAsync();
```

## Outbox Worker

Excalibur provides `OutboxBackgroundService`, a built-in `BackgroundService` for outbox processing:

```csharp
// The Basic Setup example above already enables the outbox background service
// via EnableBackgroundProcessing(). Here's the detailed configuration:

builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(connectionString)
              .EnableBackgroundProcessing(options =>
              {
                  options.PollingInterval = TimeSpan.FromSeconds(1);
                  options.MaxRetries = 3;
                  options.ProcessScheduledMessages = true;
                  options.RetryFailedMessages = true;
                  options.DrainTimeoutSeconds = 30;
              });
    });
});
```

The built-in `OutboxBackgroundService` provides:
- Polling with configurable intervals
- Pending message processing
- Scheduled message processing
- Automatic retry for failed messages
- Graceful shutdown with drain timeout
- Health state integration
- Metrics recording

## Projection Worker

Excalibur provides `EventStoreDispatcherService`, a built-in `BackgroundService` that dispatches events to projection handlers:

```csharp
// Setup in your worker service
builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.UseEventStore<SqlServerEventStore>();
    });
});

// Register projection handlers via standard DI
builder.Services.AddSingleton<IProjectionEventProcessor, OrderProjectionHandler>();
builder.Services.AddSingleton<IProjectionEventProcessor, CustomerProjectionHandler>();

// Enable background dispatching
builder.Services.AddHostedService<EventStoreDispatcherService>();
builder.Services.Configure<EventStoreDispatcherOptions>(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(1);
});
```

### Custom Projection Handler

Implement `IProjectionEventProcessor` to handle events:

```csharp
public class OrderProjectionHandler : IProjectionEventProcessor
{
    private readonly IProjectionStore<OrderProjection> _store;

    public OrderProjectionHandler(IProjectionStore<OrderProjection> store)
        => _store = store;

    public async Task HandleAsync(object eventData, CancellationToken ct)
    {
        switch (eventData)
        {
            case OrderCreated e:
                await _store.UpsertAsync(e.OrderId.ToString(), new OrderProjection
                {
                    OrderId = e.OrderId,
                    CustomerId = e.CustomerId,
                    Status = "Created"
                }, ct);
                break;

            case OrderShipped e:
                var projection = await _store.GetByIdAsync(e.OrderId.ToString(), ct);
                if (projection != null)
                {
                    projection.Status = "Shipped";
                    await _store.UpsertAsync(e.OrderId.ToString(), projection, ct);
                }
                break;
        }
    }
}
```

## CDC Worker

Excalibur provides `CdcProcessingHostedService`, a built-in `BackgroundService` for CDC processing:

```csharp
// Setup in your worker service
builder.Services.AddCdcProcessor(cdc =>
{
    cdc.UseSqlServer(connectionString)
       .TrackTable("dbo.Orders", t => t.MapAll<OrderChangedEvent>())
       .EnableBackgroundProcessing(options =>
       {
           options.PollingInterval = TimeSpan.FromSeconds(1);
           options.DrainTimeout = TimeSpan.FromSeconds(30);
       });
});
```

### CDC Change Handler

Register handlers for CDC events:

```csharp
public class OrderCdcHandler : ICdcChangeHandler<OrderChangedEvent>
{
    private readonly IDispatcher _dispatcher;

    public OrderCdcHandler(IDispatcher dispatcher)
        => _dispatcher = dispatcher;

    public async Task HandleAsync(OrderChangedEvent change, CancellationToken ct)
    {
        // Forward changes to integration event
        await _dispatcher.DispatchAsync(new OrderDataChangedIntegration
        {
            OrderId = change.OrderId,
            ChangeType = change.Operation,
            ChangedAt = change.Timestamp
        }, ct);
    }
}
```

## Leader Election

For multi-instance deployments, ensure only one worker processes:

```csharp
public class LeaderElectedWorker : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IOutboxProcessor _processor;
    private readonly ILogger<LeaderElectedWorker> _logger;

    public LeaderElectedWorker(
        ILeaderElection leaderElection,
        IOutboxProcessor processor,
        ILogger<LeaderElectedWorker> logger)
    {
        _leaderElection = leaderElection;
        _processor = processor;
        _logger = logger;

        // Subscribe to leadership events
        _leaderElection.OnBecameLeader += (_, args) =>
            _logger.LogInformation("Became leader: {CandidateId}", args.CandidateId);

        _leaderElection.OnLostLeadership += (_, args) =>
            _logger.LogInformation("Lost leadership: {CandidateId}", args.CandidateId);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Start participating in leader election
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    await _processor.DispatchPendingMessagesAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // Stop participating in election on shutdown
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

## Health Checks

### Liveness and Readiness

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddCheck<OutboxHealthCheck>("outbox")
    .AddCheck<WorkerHealthCheck>("worker");

// Expose health endpoint
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Period = TimeSpan.FromSeconds(30);
});

// For Kubernetes
builder.Services.AddSingleton<IHealthCheckPublisher, TcpHealthCheckPublisher>();
```

### Worker Health Check

```csharp
public class WorkerHealthCheck : IHealthCheck
{
    private static DateTime _lastProcessed = DateTime.UtcNow;

    public static void RecordActivity() =>
        _lastProcessed = DateTime.UtcNow;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var elapsed = DateTime.UtcNow - _lastProcessed;

        if (elapsed > TimeSpan.FromMinutes(5))
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"No activity for {elapsed}"));
        }

        if (elapsed > TimeSpan.FromMinutes(1))
        {
            return Task.FromResult(
                HealthCheckResult.Degraded($"No activity for {elapsed}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
```

## Configuration

### appsettings.json

```json
{
    "ConnectionStrings": {
        "EventStore": "Server=localhost;Database=EventStore;..."
    },
    "Worker": {
        "ProcessorId": "worker-01",
        "BatchSize": 500,
        "PollingInterval": "00:00:01",
        "ParallelProcessors": 4,
        "LeaderElection": {
            "Enabled": true,
            "LeaseDuration": "00:00:30"
        }
    },
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    }
}
```

## Graceful Shutdown

Handle shutdown signals properly:

```csharp
var host = builder.Build();

host.Services.GetRequiredService<IHostApplicationLifetime>()
    .ApplicationStopping.Register(() =>
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Shutdown requested, completing current batch...");
});

await host.RunAsync();
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OutboxWorker.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OutboxWorker.dll"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: outbox-worker
spec:
  replicas: 2  # Multiple for HA with leader election
  selector:
    matchLabels:
      app: outbox-worker
  template:
    metadata:
      labels:
        app: outbox-worker
    spec:
      containers:
        - name: worker
          image: myregistry/outbox-worker:latest
          env:
            - name: ConnectionStrings__EventStore
              valueFrom:
                secretKeyRef:
                  name: db-secrets
                  key: connection-string
            - name: Worker__ProcessorId
              valueFrom:
                fieldRef:
                  fieldPath: metadata.name
          livenessProbe:
            tcpSocket:
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          resources:
            requests:
              memory: "256Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
```

## Scaling Strategies

### Horizontal Scaling with Leader Election

```csharp
// Add SQL Server leader election
builder.Services.AddSqlServerLeaderElection(
    connectionString,
    "outbox-processor",  // Lock resource name
    options =>
    {
        options.LeaseDuration = TimeSpan.FromSeconds(30);
        options.RenewInterval = TimeSpan.FromSeconds(10);
    });
```

### Partitioned Processing

For high-volume scenarios:

```csharp
public class PartitionedOutboxWorker : BackgroundService
{
    private readonly int _partitionCount = 4;
    private readonly int _partitionId;

    public PartitionedOutboxWorker(IConfiguration config)
    {
        _partitionId = int.Parse(config["Worker:PartitionId"] ?? "0");
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Process only messages for this partition
        var processor = new PartitionedOutboxProcessor(
            _partitionId,
            _partitionCount);

        await processor.DispatchPendingMessagesAsync(ct);
    }
}
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Use leader election | Prevent duplicate processing |
| Implement health checks | Kubernetes liveness/readiness |
| Handle graceful shutdown | Complete in-flight work |
| Set resource limits | Prevent resource exhaustion |
| Use unique processor IDs | Debugging and monitoring |
| Log processing metrics | Monitor throughput |

## See Also

- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Host Excalibur applications with web API capabilities
- [Kubernetes Deployment](../deployment/kubernetes.md) — Container orchestration patterns for scaling workers
- [Dependency Injection](../core-concepts/dependency-injection.md) — Service registration and DI patterns for Excalibur
- [Outbox Setup](../configuration/outbox-setup.md) — Configure the transactional outbox for reliable messaging
