# Excalibur.Data.ElasticSearch

Elasticsearch data provider for the Excalibur framework, providing enterprise-grade document storage, full-text search, and projection management with comprehensive resilience, performance, and monitoring capabilities.

## Overview

This package provides Elasticsearch integration for Excalibur applications, enabling:

- **Document Storage**: Type-safe repository pattern with CRUD operations
- **Full-Text Search**: Rich query DSL support with aggregations
- **Index Management**: Lifecycle management, templates, and schema evolution
- **Projections**: Event sourcing projection support with rebuild capabilities
- **Resilience**: Circuit breaker, retry policies, and dead letter handling
- **Performance**: Multi-level caching, connection pooling, and query optimization
- **Monitoring**: OpenTelemetry integration, metrics, and health checks
- **Security**: API key, certificate, and basic authentication

## Installation

```bash
dotnet add package Excalibur.Data.ElasticSearch
```

**Dependencies:**
- `Elastic.Clients.Elasticsearch` (8.x)
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Options`

## Configuration

### Basic Connection

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    // Single node
    options.Url = new Uri("https://localhost:9200");

    // Or multiple nodes
    options.Urls = new[]
    {
        new Uri("https://node1:9200"),
        new Uri("https://node2:9200"),
        new Uri("https://node3:9200")
    };
});
```

### Elastic Cloud

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    options.CloudId = "my-deployment:dXMtY2VudHJhbDE...";
    options.ApiKey = "your-api-key";
});
```

### Authentication Options

#### API Key (Recommended)

```csharp
options.ApiKey = "your-api-key";
// Or Base64-encoded
options.Base64ApiKey = "base64-encoded-api-key";
```

#### Basic Authentication

```csharp
options.Username = "elastic";
options.Password = "your-password";
```

#### Certificate Fingerprint

```csharp
options.CertificateFingerprint = "A1:B2:C3:...";
options.DisableCertificateValidation = false;  // Keep false in production
```

### Environment Variables

```bash
ELASTICSEARCH__URL=https://localhost:9200
ELASTICSEARCH__APIKEY=your-api-key
ELASTICSEARCH__USERNAME=elastic
ELASTICSEARCH__PASSWORD=your-password
```

```csharp
services.Configure<ElasticsearchConfigurationSettings>(
    configuration.GetSection("Elasticsearch"));
```

### Connection Settings

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    // Timeouts
    options.RequestTimeout = TimeSpan.FromSeconds(30);
    options.PingTimeout = TimeSpan.FromSeconds(5);

    // Connection pooling
    options.ConnectionPoolType = ConnectionPoolType.Sniffing;
    options.MaximumConnectionsPerNode = 80;

    // Node discovery
    options.EnableSniffing = true;
    options.SniffingInterval = TimeSpan.FromHours(1);
});
```

## Index Management

### Index Lifecycle Management

```csharp
// Inject IIndexLifecycleManager
public class MyService
{
    private readonly IIndexLifecycleManager _lifecycleManager;

    public async Task ConfigureIndexAsync()
    {
        var policy = new IndexLifecyclePolicy
        {
            Hot = new HotPhaseConfiguration
            {
                RolloverConditions = new RolloverConditions
                {
                    MaxAge = TimeSpan.FromDays(7),
                    MaxSize = "50gb"
                }
            },
            Warm = new WarmPhaseConfiguration
            {
                MinAge = TimeSpan.FromDays(30)
            },
            Delete = new DeletePhaseConfiguration
            {
                MinAge = TimeSpan.FromDays(90)
            }
        };

        await _lifecycleManager.CreatePolicyAsync("my-policy", policy);
    }
}
```

### Index Templates

```csharp
// Inject IIndexTemplateManager
var template = new IndexTemplateConfiguration
{
    Name = "my-template",
    IndexPatterns = new[] { "logs-*" },
    Priority = 100
};

await _templateManager.CreateTemplateAsync(template);
```

## Projections

### Projection Store

```csharp
public class OrderProjection
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal Total { get; set; }
    public DateTime LastModified { get; set; }
}

// Configure projection store
services.AddElasticSearchProjectionStore<OrderProjection>(options =>
{
    options.IndexPrefix = "orders";
});
```

### Projection Rebuild

```csharp
// Inject IProjectionRebuildManager
var request = new ProjectionRebuildRequest
{
    ProjectionType = nameof(OrderProjection),
    SourceIndexName = "orders-v1",
    TargetIndexName = "orders-v2",
    CreateNewIndex = true,
    UseAliasing = true,
    BatchSize = 1000
};

var result = await _rebuildManager.StartRebuildAsync(request);

// Check status
var status = await _rebuildManager.GetRebuildStatusAsync(result.OperationId);
```

### Eventual Consistency Tracking

```csharp
// Inject IEventualConsistencyTracker
var eventId = Guid.NewGuid().ToString();
await _tracker.TrackWriteModelEventAsync(eventId, "order-1", "OrderCreated", DateTime.UtcNow);
await _tracker.TrackReadModelProjectionAsync(eventId, nameof(OrderProjection), DateTime.UtcNow);

// Check lag
var lag = await _tracker.GetConsistencyLagAsync(nameof(OrderProjection));
if (!lag.IsWithinSLA)
{
    _logger.LogWarning("Projection lagging by {Events} events", lag.PendingEvents);
}
```

### Schema Evolution

```csharp
// Inject ISchemaEvolutionHandler
var comparison = await _schemaHandler.CompareSchemaAsync("orders-v1", "orders-v2");

if (!comparison.IsBackwardsCompatible)
{
    var migrationRequest = new SchemaMigrationRequest
    {
        ProjectionType = nameof(OrderProjection),
        SourceIndex = "orders-v1",
        TargetIndex = "orders-v2",
        Strategy = MigrationStrategy.AliasSwitch,
        NewSchema = new Elastic.Clients.Elasticsearch.Mapping.Properties
        {
            { "orderId", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
            { "status", new Elastic.Clients.Elasticsearch.Mapping.KeywordProperty() },
            { "total", new Elastic.Clients.Elasticsearch.Mapping.DoubleNumberProperty() }
        }
    };

    var plan = await _schemaHandler.PlanMigrationAsync(migrationRequest);
    await _schemaHandler.ExecuteMigrationAsync(plan);
}
```

## Resilience

### Circuit Breaker

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    options.Resilience.CircuitBreaker = new CircuitBreakerOptions
    {
        Enabled = true,
        FailureThreshold = 5,          // Open after 5 failures
        MinimumThroughput = 10,        // Minimum requests before evaluation
        BreakDuration = TimeSpan.FromSeconds(30),
        SamplingDuration = TimeSpan.FromSeconds(60),
        FailureRateThreshold = 0.5     // 50% failure rate
    };
});
```

### Retry Policy

```csharp
options.Resilience.Retry = new RetrySettings
{
    MaxRetries = 3,
    InitialDelay = TimeSpan.FromMilliseconds(100),
    MaxDelay = TimeSpan.FromSeconds(30),
    UseExponentialBackoff = true,
    BackoffMultiplier = 2.0,
    UseJitter = true
};
```

### Dead Letter Handling

```csharp
services.Configure<ElasticsearchDeadLetterOptions>(options =>
{
    options.Enabled = true;
    options.IndexName = "dead-letters";
    options.RetentionDays = 30;
    options.MaxRetries = 3;
});
```

## Performance

### Multi-Level Caching

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    options.Performance.Caching = new CachingSettings
    {
        L1 = new L1CacheSettings
        {
            Enabled = true,
            MaxItems = 1000,
            DefaultTtl = TimeSpan.FromMinutes(5)
        },
        L2 = new L2CacheSettings
        {
            Enabled = true,
            MaxSize = 100_000_000,  // 100MB
            DefaultTtl = TimeSpan.FromMinutes(30)
        },
        L3 = new L3CacheSettings
        {
            Enabled = false,
            CacheDirectory = "/tmp/es-cache"
        }
    };
});
```

### Bulk Operations

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    options.Performance.BulkOperations = new BulkOperationSettings
    {
        MaxBatchSize = 1000,
        MaxBatchBytes = 10_000_000,  // 10MB
        FlushInterval = TimeSpan.FromSeconds(5),
        MaxConcurrentBatches = 4
    };
});
```

### Query Optimization

```csharp
// IQueryOptimizer automatically optimizes queries
var optimized = await _queryOptimizer.OptimizeAsync(searchRequest);
var analysis = await _queryOptimizer.AnalyzeAsync(searchRequest);
```

## Monitoring

### OpenTelemetry Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.Data.ElasticSearch");
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Data.ElasticSearch");
    });
```

### Monitoring Settings

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    options.Monitoring = new ElasticsearchMonitoringOptions
    {
        Metrics = new MetricsOptions { Enabled = true },
        RequestLogging = new RequestLoggingSettings
        {
            Enabled = true,
            LogSlowQueries = true,
            SlowQueryThreshold = TimeSpan.FromSeconds(1)
        },
        PerformanceDiagnostics = new PerformanceDiagnosticsSettings
        {
            Enabled = true,
            SampleRate = 0.1  // 10% sampling
        }
    };
});
```

## Health Checks

### Registration

```csharp
services.AddHealthChecks()
    .AddElasticSearchHealthCheck(tags: new[] { "ready", "elasticsearch" });
```

### Custom Configuration

```csharp
services.AddHealthChecks()
    .AddCheck<ElasticClientHealthCheck>(
        "elasticsearch",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready" },
        timeout: TimeSpan.FromSeconds(10));
```

## Repository Pattern

### Define Repository

```csharp
public class OrderRepository : ElasticRepositoryBase<Order, string>
{
    public OrderRepository(IElasticClient client) : base(client, "orders") { }

    public async Task<IEnumerable<Order>> FindByCustomerAsync(string customerId)
    {
        return await SearchAsync(q => q
            .Match(m => m.Field(f => f.CustomerId).Query(customerId)));
    }
}
```

### Register and Use

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();

// In your service
public class OrderService
{
    private readonly IOrderRepository _orders;

    public async Task<Order> GetAsync(string id)
    {
        return await _orders.GetByIdAsync(id);
    }
}
```

## Troubleshooting

### Common Issues

#### Connection Refused

```
Elasticsearch.Net.ElasticsearchClientException: Connection refused
```

**Solutions:**
- Verify Elasticsearch is running
- Check URL and port configuration
- Verify firewall allows connections
- Check certificate fingerprint for HTTPS

#### Authentication Failed

```
Elasticsearch.Net.ElasticsearchClientException: 401 Unauthorized
```

**Solutions:**
- Verify API key or credentials
- Check user has required permissions
- Ensure authentication method matches server configuration

#### Index Not Found

```
Elasticsearch.Net.ElasticsearchClientException: index_not_found_exception
```

**Solutions:**
- Verify index exists: `GET /_cat/indices`
- Check index name spelling (case-sensitive)
- Create index if using auto-create

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Data.ElasticSearch": "Debug",
      "Elastic.Transport": "Warning"
    }
  }
}
```

## Complete Configuration Reference

```csharp
services.Configure<ElasticsearchConfigurationSettings>(options =>
{
    // Connection
    options.Url = new Uri("https://localhost:9200");
    options.CloudId = null;
    options.ConnectionPoolType = ConnectionPoolType.Static;

    // Authentication
    options.Username = null;
    options.Password = null;
    options.ApiKey = "your-api-key";
    options.CertificateFingerprint = null;
    options.DisableCertificateValidation = false;

    // Timeouts
    options.RequestTimeout = TimeSpan.FromSeconds(30);
    options.PingTimeout = TimeSpan.FromSeconds(5);

    // Connection pool
    options.MaximumConnectionsPerNode = 80;
    options.EnableSniffing = false;
    options.SniffingInterval = TimeSpan.FromHours(1);

    // Resilience
    options.Resilience = new ElasticsearchResilienceOptions();

    // Monitoring
    options.Monitoring = new ElasticsearchMonitoringOptions();

    // Performance
    options.Performance = new ElasticsearchPerformanceSettings();

    // Projections
    options.Projections = new ProjectionSettings();
});
```

## See Also

- [Elasticsearch Official Documentation](https://www.elastic.co/guide/en/elasticsearch/reference/current/index.html)
- [Elastic.Clients.Elasticsearch NuGet](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch)
