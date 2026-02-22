---
sidebar_position: 5
title: From ASP.NET Core Eventing Proposal
description: How Excalibur fulfills the requirements from the ASP.NET Core Eventing Framework proposal (dotnet/aspnetcore#53219).
---

# From ASP.NET Core Eventing Proposal

In 2024, the ASP.NET Core team proposed an [Eventing Framework Epic for .NET 9](https://github.com/dotnet/aspnetcore/issues/53219) to create a unified framework for processing messages from various queue providers. The proposal was **closed as "not planned"** in October 2025, leaving a gap in the .NET ecosystem.

**Excalibur fills this gap** by providing production-ready implementations of everything the community requested.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [getting started](../getting-started/index.md) and [transports](../transports/choosing-a-transport.md)

---

## The Proposed API

The ASP.NET Core proposal outlined an API style like this:

```csharp title="Proposed ASP.NET Core Eventing Framework (dotnet/aspnetcore#53219)"
var builder = Host.CreateApplicationBuilder();

builder.Services.AddEventQueues(queues => {
    queues.AddAzureStorageEventQueue("orders");
    queues.AddTimerEventQueue("cron", "*/5 * * * *");
});

var app = builder.Build();

app.UseRouting();
app.UseExceptionHandler();

var orders = app.WithEventQueue("orders");
orders.MapEvent("order-received", (Order order, ILogger<Program> logger) =>
{
    // Handler logic
});

var cron = app.WithEventQueue("cron");
cron.MapEvent((TimerInfo timer) =>
{
    // Cron handler logic
});

app.Run();
```

---

## Excalibur Equivalent

```csharp title="Excalibur.Dispatch"
var builder = Host.CreateApplicationBuilder();

// Register prerequisites
builder.Services.AddSingleton<ICronScheduler, CronScheduler>();

// Azure Storage Queue - full consumer with polling, visibility timeout, DLQ
builder.Services.AddAzureStorageQueueTransport("orders", options =>
{
    options.ConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    options.QueueName = "orders";
    options.PollingIntervalMs = 1000;
    options.MaxRetries = 3;
    options.EnableDeadLetterQueue = true;
});

// Define typed timer marker
public struct ScheduledJobTimer : ICronTimerMarker { }

// Cron timer - scheduled message generation with typed routing
builder.Services.AddCronTimerTransport<ScheduledJobTimer>("*/5 * * * *", options =>
{
    options.TimeZone = TimeZoneInfo.Utc;
    options.PreventOverlap = true;
});

// Register handlers via DI (type-safe, testable)
builder.Services.AddDispatch(typeof(Program).Assembly);

var app = builder.Build();
app.Run();

// Strongly-typed handler for queue messages
public class OrderReceivedHandler : IActionHandler<OrderReceived>
{
    private readonly ILogger<OrderReceivedHandler> _logger;

    public OrderReceivedHandler(ILogger<OrderReceivedHandler> logger)
        => _logger = logger;

    public Task HandleAsync(OrderReceived message, CancellationToken ct)
    {
        _logger.LogInformation("Order received: {OrderId}", message.OrderId);
        return Task.CompletedTask;
    }
}

// Strongly-typed handler for timer triggers - no manual filtering needed!
public class CronJobHandler : IActionHandler<CronTimerTriggerMessage<ScheduledJobTimer>>
{
    private readonly ILogger<CronJobHandler> _logger;

    public CronJobHandler(ILogger<CronJobHandler> logger)
        => _logger = logger;

    public Task HandleAsync(CronTimerTriggerMessage<ScheduledJobTimer> timer, CancellationToken ct)
    {
        _logger.LogInformation("Timer {Name} fired at {Time}", timer.TimerName, timer.TriggerTimeUtc);
        return Task.CompletedTask;
    }
}
```

---

## Feature-by-Feature Comparison

### Queue Provider Support

| Proposed Feature | Excalibur Equivalent | Implementation |
|------------------|----------------------|----------------|
| `AddAzureStorageEventQueue()` | `AddAzureStorageQueueTransport()` | `AzureStorageQueueConsumer` - full consumer with CloudEvents, visibility timeout, DLQ, exponential backoff |
| `AddAzureServiceBusEventQueue()` | `AddAzureServiceBusTransport()` | `SimpleServiceBusConsumer`, `SessionServiceBusConsumer` - sessions, peek-lock, auto-complete |
| `AddAwsSqsEventQueue()` | `AddAwsSqsTransport()` | `AwsSqsConsumer` - long polling, FIFO support, visibility timeout |
| `AddGooglePubSubEventQueue()` | `AddGooglePubSubTransport()` | `GooglePubSubConsumer` - streaming pull, batch receiving, ack deadline |
| `AddKafkaEventQueue()` | `AddKafkaTransport()` | `KafkaChannelConsumer` - consumer groups, offset management, exactly-once |
| `AddRabbitMqEventQueue()` | `AddRabbitMQTransport()` | `RabbitMqChannelConsumer` - ack modes, prefetch, DLX |
| `AddTimerEventQueue()` | `AddCronTimerTransport<TTimer>()` | `CronTimerTransportAdapter` - cron scheduling, overlap prevention, time zones, typed handlers |

### Handler Registration

| Proposed Approach | Excalibur Approach | Advantage |
|-------------------|--------------------|-----------|
| `MapEvent("name", lambda)` | `IActionHandler<T>` classes | Type-safe, testable, full DI support |
| String-based event names | Strongly-typed message classes | Compile-time safety, refactoring support |
| Inline lambdas in Program.cs | Dedicated handler classes | Separation of concerns, unit testable |

### Framework Primitives

| Proposed Feature | Excalibur Status | Implementation |
|------------------|------------------|----------------|
| Middleware/Filters | **Native** | `IDispatchMiddleware` with full pipeline |
| DI Support | **Native** | Constructor injection in all handlers |
| Serialization APIs | **Native** | Pluggable: MemoryPack, JSON, MessagePack, Protobuf |
| Host Lifecycle | **Native** | `ITransportAdapter.StartAsync/StopAsync` |

---

## Community Requests Fulfilled

The GitHub issue received significant community feedback requesting features beyond the initial proposal. Here's how Excalibur addresses each request:

### Outbox Pattern (46 upvotes)

> *"With outbox feature, please."*

**Excalibur** provides full outbox support across two package families:

- `Excalibur.Dispatch` provides abstractions (`IOutboxStore`, `IOutboxPublisher`) and middleware (`OutboxMiddleware`, `OutboxBackgroundService`)
- `Excalibur.EventSourcing.*` provides concrete store implementations for SQL Server, PostgreSQL, MongoDB, CosmosDB, DynamoDB, Firestore, Redis, and in-memory

```csharp
// Install both packages for full outbox support
// dotnet add package Excalibur.Dispatch
// dotnet add package Excalibur.EventSourcing.SqlServer  # or your database provider

// Outbox pattern - messages saved transactionally with domain changes
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
services.AddSqlServerOutboxStore(options =>
{
    options.ConnectionString = connectionString;
    options.DefaultBatchSize = 100;
});
```

:::note Package Architecture
`Excalibur.Dispatch.*` focuses on messaging (dispatching, pipelines, middleware). `Excalibur.EventSourcing.*` provides persistence implementations (event stores, outbox stores, repositories). This separation keeps the messaging core lightweight while offering production-ready database integrations as optional packages.
:::

### Abstraction Layer Like ILogger (70 upvotes)

> *"Introduce a common abstraction layer that can be used by third party tools, much like the `ILogger` abstraction."*

**`Excalibur.Dispatch`:** Clean abstraction layer with:

- `ITransportAdapter` - transport abstraction
- `IActionHandler<T>` / `IDispatchHandler<T>` - handler abstractions
- `IDispatcher` - dispatch abstraction
- `IMessageContext` - context abstraction

### Database-Backed Queues with SKIP LOCKED

> *"Support for database-backed queues with SKIP LOCKED in postgres."*

**`Excalibur.EventSourcing.*`:** SQL Server and PostgreSQL implementations use SKIP LOCKED for concurrent processing. Install `Excalibur.EventSourcing.SqlServer` or `Excalibur.EventSourcing.Postgres` to use these database-backed stores.

### Lightweight Handlers

> *"Don't force all existing handlers to use custom interfaces."*

**`Excalibur.Dispatch`:** Simple, minimal interface:

```csharp
public interface IActionHandler<TAction>
    where TAction : IDispatchAction
{
    Task HandleAsync(TAction action, CancellationToken cancellationToken);
}
```

### Source Generator Support (48 upvotes)

> *"Support for Roslyn source generators similar to MediatR and Wolverine."*

**`Excalibur.Dispatch`:** Full source generator package (`Excalibur.Dispatch.SourceGenerators`) with:

- `HandlerRegistrySourceGenerator` - Auto-discover and register handlers
- `HandlerInvokerSourceGenerator` - Zero-reflection handler invocation
- `MessageTypeSourceGenerator` - Message type registration
- `ServiceRegistrationSourceGenerator` - Auto-generate DI registrations
- `JsonSerializationSourceGenerator` - AOT-compatible JSON serialization
- `RoutingRuleSourceGenerator` - Compile-time routing rules

```csharp
// Source generators enable AOT compilation with no runtime reflection
builder.Services.AddDispatch(typeof(Program).Assembly);  // Handlers discovered at compile time
```

### Schema Registry Support (3 upvotes)

> *"Integration with schema registries for message validation."*

**`Excalibur.Dispatch`:** Full schema registry integration for all major cloud platforms:

```csharp
// Kafka with Confluent Schema Registry
services.AddConfluentSchemaRegistry(options =>
{
    options.Url = "http://localhost:8081";
    options.AutoRegisterSchemas = true;
    options.DefaultCompatibility = CompatibilityMode.Backward;
});

// Dispatch observability (covers all transports including Google Pub/Sub)
services.AddDispatchObservability();

// AWS Glue Schema Registry
services.AddAwsGlueSchemaRegistry(options =>
{
    options.RegistryName = "my-registry";
    options.Region = RegionEndpoint.USEast1;
    options.DefaultCompatibility = AwsGlueCompatibilityMode.Backward;
    options.DataFormat = AwsGlueDataFormat.Json; // Also supports Avro, Protobuf
});
```

### Health Checks

**`Excalibur.Dispatch`:** Built-in `ITransportHealthChecker` for all transports with ASP.NET Core Health Checks integration.

```csharp
builder.Services.AddHealthChecks()
    .AddTransportHealthChecks();

app.MapHealthChecks("/health");
```

### Observability (Metrics/Tracing)

**`Excalibur.Dispatch`:** OpenTelemetry instrumentation on all transports automatically.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Excalibur.Dispatch.Observability"))
    .WithMetrics(b => b.AddDispatchMetrics());
```

---

## Why the Proposal Was Closed

The ASP.NET Core team closed the issue as "not planned" after community feedback raised concerns:

- *"Why not support existing OSS tools...instead of building from scratch?"* (83 upvotes)
- Concerns about complexity reminiscent of WCF
- Questions about supporting existing libraries (MediatR, Wolverine, MassTransit)
- Source generator chaining limitations

**Excalibur addresses these concerns by:**

1. Building on proven patterns
2. Keeping the API simple and focused
3. Using source generation for AOT compatibility
4. Providing full middleware pipeline without WCF-like complexity

---

## Full Consumer Support

Unlike a proposal, Excalibur has **working implementations** with full consumer capabilities:

### Azure Storage Queue Consumer

```csharp
// AzureStorageQueueConsumer features:
// - CloudEvents in both structured and binary content modes
// - MessageContext handling with full envelope propagation
// - Queue polling with configurable intervals
// - Message visibility timeout management
// - Proper acknowledgment through message deletion
// - Automatic retry with exponential backoff
// - Dead letter queue support after max retries
```

### AWS SQS Consumer

```csharp
// AwsSqsConsumer features:
// - Long polling support (WaitTimeSeconds)
// - FIFO queue support with deduplication
// - Visibility timeout management
// - Message attributes preservation
// - CloudEvents support
// - Batch receive operations
```

### Google Pub/Sub Consumer

```csharp
// GooglePubSubConsumer features:
// - Streaming pull for low-latency
// - Batch receiving for high throughput
// - Acknowledgment deadline management
// - Ordering key support
// - Compression (Gzip/Snappy)
```

### Kafka Consumer

```csharp
// KafkaChannelConsumer features:
// - Consumer group management
// - Offset commit strategies
// - Partition assignment
// - Schema registry integration
// - Exactly-once semantics
```

### RabbitMQ Consumer

```csharp
// RabbitMqChannelConsumer features:
// - Multiple ack modes (auto, manual, none)
// - Prefetch configuration
// - Dead letter exchange (DLX)
// - Publisher confirms
// - Retry policies
```

---

## Comparison Summary

| Aspect | Proposed API | Excalibur |
|--------|--------------|-----------|
| **Status** | Closed, not implemented | Production-ready, ~36,000 tests |
| **Queue Consumers** | Proposed only | Full implementations for 6 providers (`Excalibur.Dispatch.Transport.*`) |
| **Timer/Cron** | Proposed only | Native `AddCronTimerTransport<TTimer>()` with typed handlers |
| **Type Safety** | String-based event names | Strongly-typed `IActionHandler<T>` |
| **Testability** | Inline lambdas | Handler classes with full DI |
| **Pipeline** | Planned | Full `IDispatchMiddleware` pipeline |
| **Health Checks** | Planned | Built-in `ITransportHealthChecker` |
| **Observability** | Planned | OpenTelemetry instrumentation |
| **Source Generators** | Community requested | Full `Excalibur.Dispatch.SourceGenerators` package |
| **Schema Registry** | Community requested | Kafka Confluent + Google Pub/Sub + AWS Glue |
| **Outbox Pattern** | Community requested | Abstractions in `Excalibur.Dispatch`, stores in `Excalibur.EventSourcing.*` |
| **Database Queues** | Community requested | SQL Server/PostgreSQL with SKIP LOCKED (`Excalibur.EventSourcing.*`) |
| **AOT Support** | Mentioned | Source generation, no runtime reflection |

---

## Migration Path

If you were waiting for the ASP.NET Core Eventing Framework, here's how to adopt Excalibur:

### 1. Install Packages

```bash
# Core messaging (required)
dotnet add package Excalibur.Dispatch

# Add transport packages as needed (choose your provider)
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus  # Azure Service Bus, Event Hubs, Storage Queues
dotnet add package Excalibur.Dispatch.Transport.AwsSqs           # AWS SQS
dotnet add package Excalibur.Dispatch.Transport.GooglePubSub     # Google Pub/Sub
dotnet add package Excalibur.Dispatch.Transport.Kafka            # Apache Kafka
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ         # RabbitMQ

# For outbox pattern (optional)
dotnet add package Excalibur.EventSourcing.SqlServer   # or Postgres, MongoDB, etc.
```

:::note Azure Transport Package
The `Excalibur.Dispatch.Transport.AzureServiceBus` package includes support for Azure Service Bus, Azure Event Hubs, and Azure Storage Queues. You only need one package for all Azure messaging services.
:::

### 2. Configure Transports

```csharp
// Register prerequisites for cron timers
builder.Services.AddSingleton<ICronScheduler, CronScheduler>();

// Configure transports using standard single entry points
builder.Services.AddAzureStorageQueueTransport("orders", options => { /* ... */ });

// Typed timer with marker interface (recommended)
public struct ScheduledJobTimer : ICronTimerMarker { }
builder.Services.AddCronTimerTransport<ScheduledJobTimer>("*/5 * * * *");

// Or named timer without marker
builder.Services.AddCronTimerTransport("scheduled-jobs", "*/5 * * * *");

builder.Services.AddDispatch(typeof(Program).Assembly);
```

### 3. Create Handlers

```csharp
// Define your message type
public record MyMessage(string Content) : IDispatchAction;

// Implement the handler
public class MyHandler : IActionHandler<MyMessage>
{
    public Task HandleAsync(MyMessage message, CancellationToken ct)
    {
        // Your logic here
        return Task.CompletedTask;
    }
}
```

### 4. Add Observability (Optional)

```csharp
builder.Services.AddHealthChecks().AddTransportHealthChecks();
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Excalibur.Dispatch.Observability"))
    .WithMetrics(b => b.AddDispatchMetrics());
```

### 5. Add Outbox Pattern (Optional)

```csharp
// Requires: dotnet add package Excalibur.EventSourcing.SqlServer
builder.Services.AddSqlServerOutboxStore(options =>
{
    options.ConnectionString = connectionString;
    options.DefaultBatchSize = 100;
});
```

---

## Package Architecture

Excalibur is organized into focused package families:

| Package Family | Responsibility |
|----------------|---------------|
| `Excalibur.Dispatch.*` | Messaging: dispatching, pipelines, handlers, middleware, transports |
| `Excalibur.Domain` | Domain modeling: aggregates, entities, value objects |
| `Excalibur.EventSourcing.*` | Persistence: event stores, snapshots, repositories |
| `Excalibur.Saga.*` | Workflows: sagas, process managers |

**Start with `Excalibur.Dispatch`:**
- Message dispatching (alternative to MediatR)
- Cloud queue consumers (Azure, AWS, GCP)
- Scheduled jobs with cron expressions
- Pipeline middleware and observability

**Add more packages as needed:**
- `Excalibur.Domain` for domain-driven design patterns
- `Excalibur.EventSourcing` for event sourcing with aggregates
- `Excalibur.Saga` for reliable multi-step workflows

---

## See Also

- [Transports Overview](../transports/index.md) -- All supported transports
- [Cron Timer Transport](../transports/cron-timer.md) -- Scheduled message triggering
- [Outbox Pattern](../patterns/outbox.md) -- Reliable message publishing
- [Middleware](../middleware/index.md) -- Pipeline behaviors
- [Health Checks](../observability/health-checks.md) -- Transport monitoring
