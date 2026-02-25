# Health Checks Sample

This sample demonstrates how to implement **health checks** for Kubernetes liveness/readiness probes and monitoring integration with **Excalibur.Dispatch**.

## Features

- **Liveness Probe** - Is the application alive?
- **Readiness Probe** - Can the application receive traffic?
- **Custom Health Checks** - Dispatch pipeline status
- **Detailed Response** - JSON output with diagnostics

## Running the Sample

```bash
# Build
dotnet build

# Run
dotnet run
```

## Endpoints

| Endpoint | Purpose | Kubernetes Use |
|----------|---------|----------------|
| `/health` | All health checks | Monitoring/dashboards |
| `/health/live` | Liveness checks | `livenessProbe` |
| `/health/ready` | Readiness checks | `readinessProbe` |

## Testing

```bash
# Full health check
curl http://localhost:5000/health

# Liveness probe
curl http://localhost:5000/health/live

# Readiness probe
curl http://localhost:5000/health/ready
```

## Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "dispatch_pipeline": {
      "status": "Healthy",
      "description": "Dispatch pipeline is healthy",
      "data": {
        "dispatcher_type": "Dispatcher",
        "dispatcher_registered": true,
        "check_time": "2026-01-22T00:00:00.000Z"
      }
    },
    "memory": {
      "status": "Healthy"
    }
  }
}
```

## Kubernetes Configuration

### Deployment YAML

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dispatch-app
spec:
  template:
    spec:
      containers:
        - name: dispatch-app
          image: your-image:tag
          ports:
            - containerPort: 8080
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          startupProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 0
            periodSeconds: 5
            failureThreshold: 30
```

## Health Check Categories

### Liveness Checks (`live` tag)

Checks that indicate the application process is alive:

- **Memory**: Process memory allocation
- **Deadlock detection**: (optional) Thread state

If liveness fails, Kubernetes restarts the container.

### Readiness Checks (`ready` tag)

Checks that indicate the application can handle requests:

- **Dispatch Pipeline**: Dispatcher is configured
- **Disk**: Sufficient disk space
- **External APIs**: Dependencies are reachable

If readiness fails, Kubernetes removes the pod from load balancer.

## Custom Health Checks

### Creating a Custom Check

```csharp
public class MyHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isHealthy = // your logic here

        if (isHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Check passed",
                new Dictionary<string, object>
                {
                    ["metric"] = 42
                }));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            "Check failed",
            data: new Dictionary<string, object>
            {
                ["reason"] = "Something went wrong"
            }));
    }
}
```

### Registering the Check

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MyHealthCheck>(
        "my_check",
        tags: ["ready", "custom"]);
```

## Common Health Check Packages

| Package | Checks |
|---------|--------|
| `AspNetCore.HealthChecks.System` | Memory, Disk, Process |
| `AspNetCore.HealthChecks.Uris` | HTTP endpoints |
| `AspNetCore.HealthChecks.SqlServer` | SQL Server |
| `AspNetCore.HealthChecks.Redis` | Redis |
| `AspNetCore.HealthChecks.NpgSql` | PostgreSQL |
| `AspNetCore.HealthChecks.RabbitMQ` | RabbitMQ |
| `AspNetCore.HealthChecks.Kafka` | Kafka |

## Best Practices

### DO

- Keep liveness checks fast and simple
- Use readiness checks for dependencies
- Add meaningful diagnostic data
- Set appropriate timeouts
- Use tags to categorize checks

### DON'T

- Put database queries in liveness checks
- Use liveness to check external services
- Set very short probe intervals
- Ignore health check failures in logs

## Monitoring Integration

### Prometheus

```csharp
// Add this to expose metrics
builder.Services.AddHealthChecks()
    .AddPrometheusMetrics();
```

### Application Insights

```csharp
// Publish health check results to App Insights
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(30);
    options.Period = TimeSpan.FromMinutes(1);
});

builder.Services.AddSingleton<IHealthCheckPublisher, ApplicationInsightsPublisher>();
```

## Troubleshooting

### Health check always fails

1. Check if services are registered:
   ```csharp
   var service = serviceProvider.GetService<IMyService>();
   ```

2. Verify connection strings

3. Check timeout settings

### Kubernetes restarts container frequently

1. Increase `failureThreshold`
2. Check `initialDelaySeconds` is sufficient
3. Review liveness check complexity

## Related Samples

- [OpenTelemetry Sample](../OpenTelemetry/) - Distributed tracing
- [Audit Logging Sample](../../06-security/AuditLogging/) - Security logging

## Learn More

- [ASP.NET Core Health Checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Health Checks UI](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
