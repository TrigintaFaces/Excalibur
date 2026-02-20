# Advanced Samples

Advanced patterns and sophisticated real-world scenarios including streaming handlers, distributed coordination, validation, projections, event sourcing providers, and schema evolution.

## Streaming Handlers (Sprint 436)

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [StreamingHandlers](StreamingHandlers/) | All four streaming handler patterns | IAsyncEnumerable, backpressure, progress |

### Streaming Handler Types

| Handler | Pattern | Use Case |
|---------|---------|----------|
| `IStreamingDocumentHandler<TDocument, TOutput>` | Document → Stream | CSV parsing, PDF page extraction |
| `IStreamConsumerHandler<TDocument>` | Stream → Sink | Batch imports, ETL sinks |
| `IStreamTransformHandler<TInput, TOutput>` | Stream → Stream | Data enrichment, filtering |
| `IProgressDocumentHandler<TDocument>` | Progress reporting | Long-running exports |

See [Streaming Documentation](../../docs/streaming/) for detailed guides.

## Distributed Coordination

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [LeaderElection](LeaderElection/) | Distributed leader election | Redis, TTL leases, callbacks |

### Leader Election Provider Comparison

| Provider | Best For | Infrastructure | Failover Speed |
|----------|----------|----------------|----------------|
| **Redis** | High availability, fast failover | Redis cluster | Sub-second |
| **SQL Server** | Existing SQL infrastructure | SQL Server | 5-15 seconds |
| **Kubernetes** | K8s-native deployments | Kubernetes API | 15-30 seconds |
| **Consul** | Service mesh integration | Consul cluster | 1-5 seconds |

## Validation Patterns

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [FluentValidationSample](FluentValidationSample/) | Pipeline validation integration | FluentValidation, Middleware |

### Validation Decision Matrix

| Pattern | Use Case | Complexity |
|---------|----------|------------|
| **Basic Rules** | Required fields, length limits | Low |
| **Conditional** | Optional fields, context-dependent | Medium |
| **Cross-Field** | Multi-field constraints | Medium |
| **Async** | External service validation | High |

## CQRS & Projections

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [ProjectionsSample](ProjectionsSample/) | Read model generation | Checkpoint tracking, rebuild |

### Projection Patterns

| Pattern | Best For | Consistency |
|---------|----------|-------------|
| **Inline** | Strong consistency | Synchronous |
| **Async** | High throughput | Eventual |
| **Multi-Stream** | Cross-aggregate queries | Eventual |
| **Checkpoint** | Rebuild support | At-least-once |

## Event Sourcing Providers

Production-ready event store implementations with database-specific optimizations.

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [SqlServerEventStore](SqlServerEventStore/) | SQL Server event persistence | Dapper, Docker, Transactions |
| [CosmosDbEventStore](CosmosDbEventStore/) | Cosmos DB with partition strategies | Azure SDK, Change Feed |
| [SnapshotStrategies](SnapshotStrategies/) | Aggregate snapshot optimization | Interval, Time, Size, Composite |
| [EventUpcasting](EventUpcasting/) | Event schema evolution | V1->V2->V3, BFS Path Finding |

### Provider Selection Guide

| Provider | Best For | Consistency | Scaling |
|----------|----------|-------------|---------|
| **SQL Server** | Enterprise, ACID transactions | Strong | Vertical |
| **Cosmos DB** | Global distribution, high throughput | Tunable | Horizontal |
| **In-Memory** | Testing, development | Strong | N/A |

### Snapshot Strategy Decision Matrix

| Strategy | When to Use | Configuration |
|----------|-------------|---------------|
| **Interval** | High-velocity aggregates | Every 50-100 events |
| **Time-Based** | Long-running, read-heavy | Every 1-4 hours |
| **Size-Based** | Large aggregate state | Above 10-50 KB |
| **Composite** | Production belt-and-suspenders | Interval + Time (Any mode) |
| **None** | Testing, small aggregates | - |

### Event Upcasting Patterns

| Pattern | Scenario | Complexity |
|---------|----------|------------|
| **Field Addition** | Add new optional field | Low |
| **Field Splitting** | Address -> Street, City, Zip | Medium |
| **Field Merging** | FirstName + LastName -> FullName | Low |
| **Type Rename** | Rename event class | Medium |
| **Schema Transform** | Restructure event shape | High |

## Background Processing

| Sample | Description | Complexity |
|--------|-------------|------------|
| [BackgroundServices](BackgroundServices/) | Various background service patterns | Advanced |
| [JobWorkerSample](JobWorkerSample/) | Job worker pattern with multiple job types | Intermediate |
| [MinimalJobSample](MinimalJobSample/) | Minimal job worker setup | Intermediate |
| [WebWorkerSample](WebWorkerSample/) | Web-based worker patterns | Intermediate |

## Integration Patterns

| Sample | Description | Complexity |
|--------|-------------|------------|
| [CdcAntiCorruption](CdcAntiCorruption/) | Anti-corruption layer for CDC integration | Advanced |
| [CrossLangSample](CrossLangSample/) | Cross-language messaging (Python, JavaScript) | Advanced |
| [DistributedScheduling](DistributedScheduling/) | Distributed scheduling examples | Advanced |

## Versioning & Evolution

| Sample | Description | Complexity |
|--------|-------------|------------|
| [EventUpcasting](EventUpcasting/) | Event schema evolution (V1->V2->V3) | Advanced |
| [Versioning.Examples](Versioning.Examples/) | Event versioning and upcasting | Advanced |

## Code Examples

### Leader Election

```csharp
// Register Redis leader election
services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

services.AddRedisLeaderElection("myapp:leader", options =>
{
    options.LeaseDuration = TimeSpan.FromSeconds(30);
    options.RenewInterval = TimeSpan.FromSeconds(10);
    options.GracePeriod = TimeSpan.FromSeconds(15);
});

// Use leadership status
if (leaderElection.IsLeader)
{
    await ProcessBackgroundJobsAsync();
}
```

### FluentValidation Integration

```csharp
// Register validation
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
}).WithFluentValidation();

services.AddValidatorsFromAssemblyContaining<CreateUserCommandValidator>();

// Validation runs automatically in pipeline
var result = await dispatcher.DispatchAsync(command, context, ct);
if (!result.Succeeded)
{
    foreach (var error in result.ProblemDetails.Errors)
        Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value)}");
}
```

### Projections

```csharp
// Projection handler
public class ProductCatalogProjectionHandler
{
    public async Task HandleAsync(ProductCreated @event, CancellationToken ct)
    {
        var projection = new ProductCatalogProjection
        {
            Id = @event.ProductId.ToString(),
            Name = @event.Name,
            Price = @event.Price,
        };
        await _store.UpsertAsync(projection.Id, projection, ct);
    }
}

// Query projections with filters
var electronics = await store.QueryAsync(
    new Dictionary<string, object> { ["Category"] = "Electronics" },
    new QueryOptions(Skip: 0, Take: 10, OrderBy: "Price"),
    ct);
```

### SQL Server Event Store

```csharp
// Configure SQL Server event store
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.RegisterHealthChecks = true;
});

// Use the repository
var repository = provider.GetRequiredService<
    IEventSourcedRepository<BankAccountAggregate, Guid>>();

var account = await repository.LoadAsync(accountId, ct);
account.Deposit(500m, "Paycheck");
await repository.SaveAsync(account, ct);
```

## Running the Samples

### Sprint 434 Samples

```bash
# Leader Election (requires Docker for Redis)
cd samples/09-advanced/LeaderElection
docker-compose up -d
dotnet run

# FluentValidation
cd samples/09-advanced/FluentValidationSample
dotnet run

# Projections
cd samples/09-advanced/ProjectionsSample
dotnet run
```

### Event Sourcing Samples

```bash
# SQL Server (requires Docker)
cd samples/09-advanced/SqlServerEventStore
docker-compose up -d
dotnet run

# Cosmos DB (requires Emulator or Azure)
cd samples/09-advanced/CosmosDbEventStore
dotnet run

# Snapshot Strategies
cd samples/09-advanced/SnapshotStrategies
dotnet run

# Event Upcasting
cd samples/09-advanced/EventUpcasting
dotnet run
```

### Other Samples

```bash
# Job Worker
dotnet run --project samples/09-advanced/JobWorkerSample

# CDC Anti-Corruption
dotnet run --project samples/09-advanced/CdcAntiCorruption

# Cross-Language (requires Python/Node)
cd samples/09-advanced/CrossLangSample
dotnet run &
python python_consumer.py
```

## Prerequisites

| Sample | Requirements |
|--------|--------------|
| LeaderElection | Docker (Redis) |
| SqlServerEventStore | Docker Desktop |
| CosmosDbEventStore | Cosmos DB Emulator or Azure account |
| CrossLangSample | Python 3, Node.js |
| BackgroundServices | Docker (for some variants) |

## Related Categories

- [04-reliability/](../04-reliability/) - Outbox pattern, retry, circuit breaker
- [08-serialization/](../08-serialization/) - Protobuf, MessagePack, MemoryPack
- [10-real-world/](../10-real-world/) - Production-style examples
- [01-getting-started/ExcaliburCqrs/](../01-getting-started/ExcaliburCqrs/) - Basic CQRS patterns

## Learn More

- [Event Sourcing Documentation](../../docs-site/docs/event-sourcing/)
- [FluentValidation Docs](https://docs.fluentvalidation.net/)
- [Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/)
- [Event Sourcing Patterns](https://learn.microsoft.com/azure/architecture/patterns/event-sourcing)

---

*Category: Advanced | Sprint 434*
