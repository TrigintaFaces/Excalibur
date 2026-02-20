---
sidebar_position: 2
---

# Migrating from MassTransit

A comprehensive guide for migrating from MassTransit to Excalibur.Dispatch, covering message bus patterns, sagas, outbox, and distributed messaging.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An existing application using MassTransit
- Familiarity with [getting started](../getting-started/index.md) and [transports](../transports/choosing-a-transport.md)

## Overview

While MassTransit is a powerful distributed application framework, Excalibur offers a **simpler, more focused approach** for teams that need messaging, event sourcing, and DDD patterns without the complexity of full-featured service bus infrastructure.

## When to Migrate

Consider Excalibur if you:
- Want to reduce infrastructure complexity (no RabbitMQ/Azure Service Bus required)
- Need built-in event sourcing and outbox patterns
- Prefer direct database-backed messaging over message brokers
- Want simpler saga/process manager patterns
- Need lower operational overhead

**Keep MassTransit if you:**
- Heavily use distributed message brokers (RabbitMQ, Azure Service Bus)
- Need complex routing and topology
- Require publish/subscribe across multiple services

## Key Differences

| Feature | MassTransit | Excalibur |
|---------|-------------|-------------------|
| **Message Transport** | RabbitMQ, Azure Service Bus, Amazon SQS | Database (SQL Server) + optional message bus |
| **Outbox** | Optional (via Entity Framework) | Built-in, first-class support |
| **Sagas** | Automatonymous state machines | Simple process managers |
| **Event Sourcing** | Not included | Built-in with `AggregateRoot` |
| **Pub/Sub** | Native broker support | Outbox + external publisher |
| **Request/Response** | Via message broker | Direct dispatcher pattern |
| **Complexity** | High (full-featured service bus) | Low (focused on core patterns) |

## Side-by-Side Comparison

### Publishing Messages

**MassTransit:**
```csharp
// Message contract
public record OrderCreated
{
    public string OrderId { get; init; }
    public string CustomerId { get; init; }
    public decimal TotalValue { get; init; }
}

// Publisher
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        // Create order
        var orderId = Guid.NewGuid().ToString();

        // Publish event via message broker
        await _publishEndpoint.Publish(new OrderCreated
        {
            OrderId = orderId,
            CustomerId = request.CustomerId,
            TotalValue = request.TotalValue
        });
    }
}
```

**Excalibur.Dispatch:**
```csharp
// Domain event
public record OrderCreatedEvent(
    string OrderId,
    string CustomerId,
    decimal TotalValue) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderCreatedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Publisher (via event sourcing)
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(action.OrderId, action.CustomerId);

        // Save aggregate (events published via outbox automatically)
        await _repository.SaveAsync(order, cancellationToken);
    }
}
```

**Key Differences:**
- MassTransit: Immediate broker publish (at-least-once via broker)
- Dispatch: Transactional outbox (exactly-once semantics via database)

### Consuming Messages

**MassTransit:**
```csharp
// Consumer
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}",
            message.OrderId,
            message.CustomerId);

        // Process message
    }
}

// Registration
cfg.ReceiveEndpoint("order-created", e =>
{
    e.ConfigureConsumer<OrderCreatedConsumer>(context);
});
```

**Excalibur.Dispatch:**
```csharp
// Event handler
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}",
            @event.OrderId,
            @event.CustomerId);

        // Process event
    }
}

// Registration (automatic assembly scanning)
builder.Services.AddDispatch(typeof(Program).Assembly);
```

**Key Differences:**
- MassTransit: `IConsumer<T>` with `ConsumeContext<T>`
- Dispatch: `IEventHandler<T>` with direct event access
- MassTransit: Queue configuration required
- Dispatch: Automatic handler discovery

### Sagas / Process Managers

**MassTransit (Automatonymous):**
```csharp
// State machine
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted);
        Event(() => PaymentProcessed);
        Event(() => OrderShipped);

        Initially(
            When(OrderSubmitted)
                .Then(context => context.Instance.OrderId = context.Data.OrderId)
                .TransitionTo(Submitted));

        During(Submitted,
            When(PaymentProcessed)
                .TransitionTo(Paid),
            When(PaymentFailed)
                .TransitionTo(Cancelled));

        During(Paid,
            When(OrderShipped)
                .TransitionTo(Shipped)
                .Finalize());
    }

    public State Submitted { get; private set; }
    public State Paid { get; private set; }
    public State Shipped { get; private set; }
    public State Cancelled { get; private set; }

    public Event<OrderSubmittedEvent> OrderSubmitted { get; private set; }
    public Event<PaymentProcessedEvent> PaymentProcessed { get; private set; }
    public Event<OrderShippedEvent> OrderShipped { get; private set; }
}

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public string OrderId { get; set; }
}
```

**Excalibur.Dispatch:**
```csharp
// Process manager (state machine)
public class OrderProcessManager : ProcessManager<OrderProcessData>
{
    public OrderProcessManager(
        OrderProcessData data,
        IDispatcher dispatcher,
        ILogger<OrderProcessManager> logger)
        : base(data, dispatcher, logger)
    {
        Initially(s => s
            .When<OrderSubmittedEvent>(h => h
                .TransitionTo("PaymentPending")
                .Then(ctx => ctx.Data.OrderId = ctx.Message.OrderId)));

        During("PaymentPending", s => s
            .When<PaymentProcessedEvent>(h => h
                .TransitionTo("Shipping"))
            .When<PaymentFailedEvent>(h => h
                .TransitionTo("Cancelled")));

        During("Shipping", s => s
            .When<OrderShippedEvent>(h => h
                .Complete()));
    }
}

public class OrderProcessData : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CurrentStateName { get; set; } = "Initial";
}
```

**Key Differences:**
- MassTransit: Declarative state machine (Automatonymous) with `State` properties
- Dispatch: Declarative state machine via `ProcessManager<TData>` with `Initially()`, `During()`, `Finally()` fluent API
- MassTransit: `CorrelationId`-based saga correlation
- Dispatch: Automatic correlation via event `AggregateId`

## Migration Strategies

### Strategy 1: Hybrid Approach (Recommended)

Keep MassTransit for inter-service communication, use Dispatch for domain logic:

```csharp
// MassTransit consumer bridges to Dispatch
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly IDispatcher _dispatcher;

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        // Convert MassTransit message to Dispatch command
        var command = new ProcessOrderCommand(
            message.OrderId,
            message.CustomerId,
            message.TotalValue);

        // Process via Dispatch
        await _dispatcher.DispatchAsync(command, context.CancellationToken);
    }
}

// Dispatch handler processes domain logic
public class ProcessOrderHandler
    : IActionHandler<ProcessOrderAction>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        ProcessOrderAction action,
        CancellationToken cancellationToken)
    {
        // Domain logic with event sourcing
        var order = await _repository.GetByIdAsync(
            action.OrderId,
            cancellationToken);

        order.Process();

        await _repository.SaveAsync(order, cancellationToken);
    }
}
```

**Benefits:**
- Leverage MassTransit's distributed messaging
- Use Dispatch for domain logic and event sourcing
- Best of both worlds

### Strategy 2: Replace with Outbox + External Publisher

Remove MassTransit broker, use Dispatch outbox with custom publisher:

```csharp
// Use Dispatch outbox with external transport
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});

// For external broker integration, use the transport layer
// (RabbitMQ, Azure Service Bus, etc.) via transport packages
builder.Services.AddAzureServiceBusTransport(options =>
{
    options.ConnectionString = serviceBusConnectionString;
});
```

**Benefits:**
- Remove MassTransit dependency
- Keep existing message broker (Azure Service Bus, RabbitMQ)
- Simpler configuration

### Strategy 3: Database-Only Messaging

Remove message broker entirely, use database for messaging:

```csharp
// Database-backed messaging via outbox
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});

// Background service processes outbox
builder.Services.AddOutboxHostedService();
```

**Benefits:**
- Simplest deployment (no broker infrastructure)
- Transactional consistency via database
- Good for monoliths or simple distributed systems

## Step-by-Step Migration

### Step 1: Install Packages

```bash
dotnet remove package MassTransit
dotnet remove package MassTransit.RabbitMQ  # or Azure.ServiceBus, etc.

dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Abstractions
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
```

### Step 2: Update Configuration

**Before (MassTransit):**
```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("order-created", e =>
        {
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });
    });
});
```

**After (Dispatch):**
```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});
builder.Services.AddOutboxHostedService();
```

### Step 3: Migrate Message Contracts

**Before:**
```csharp
public record OrderCreated
{
    public string OrderId { get; init; }
    public decimal TotalValue { get; init; }
}
```

**After:**
```csharp
public record OrderCreatedEvent(
    string OrderId,
    decimal TotalValue) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderCreatedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

### Step 4: Migrate Consumers

**Before:**
```csharp
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        // Process message
    }
}
```

**After:**
```csharp
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        // Process event (same logic)
    }
}
```

### Step 5: Migrate Publishers

**Before:**
```csharp
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        // Create order
        await _publishEndpoint.Publish(new OrderCreated
        {
            OrderId = orderId,
            TotalValue = totalValue
        });
    }
}
```

**After:**
```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(action.OrderId, action.CustomerId);

        // Events published via outbox automatically
        await _repository.SaveAsync(order, cancellationToken);
    }
}

// Aggregate raises events
public class Order : AggregateRoot
{
    public static Order Create(string orderId, string customerId)
    {
        var order = new Order { Id = orderId };
        order.RaiseEvent(new OrderCreatedEvent(orderId, customerId));
        return order;
    }
}
```

### Step 6: Migrate Sagas

**Before (MassTransit State Machine):**
```csharp
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    // Complex state machine definition
}
```

**After (Dispatch Process Manager):**
```csharp
public class OrderProcessManager : ProcessManager<OrderProcessData>
{
    public OrderProcessManager(
        OrderProcessData data,
        IDispatcher dispatcher,
        ILogger<OrderProcessManager> logger)
        : base(data, dispatcher, logger)
    {
        Initially(s => s
            .When<OrderSubmittedEvent>(h => h
                .TransitionTo("PaymentPending")
                .Then(ctx => ctx.Data.OrderId = ctx.Message.OrderId)));

        During("PaymentPending", s => s
            .When<PaymentProcessedEvent>(h => h
                .Complete()));
    }
}
```

## Feature Mapping

### Request/Response

**MassTransit:**
```csharp
// Request
public record GetOrderStatus
{
    public string OrderId { get; init; }
}

// Response
public record OrderStatusResponse
{
    public string Status { get; init; }
}

// Client
var client = bus.CreateRequestClient<GetOrderStatus>();
var response = await client.GetResponse<OrderStatusResponse>(
    new GetOrderStatus { OrderId = orderId });
```

**Dispatch:**
```csharp
// Action with return value
public record GetOrderStatusAction(string OrderId)
    : IDispatchAction<OrderStatusResult>;

// Result
public record OrderStatusResult(string Status);

// Handler
public class GetOrderStatusHandler
    : IActionHandler<GetOrderStatusAction, OrderStatusResult>
{
    public async Task<OrderStatusResult> HandleAsync(
        GetOrderStatusAction action,
        CancellationToken cancellationToken)
    {
        // Get status
        return new OrderStatusResult(status);
    }
}

// Usage
var result = await _dispatcher.DispatchAsync<GetOrderStatusAction, OrderStatusResult>(
    new GetOrderStatusAction(orderId),
    cancellationToken);
```

### Publish/Subscribe

**MassTransit:**
```csharp
// Publisher
await _publishEndpoint.Publish(new OrderCreated { OrderId = orderId });

// Subscriber
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context) { }
}
```

**Dispatch:**
```csharp
// Publisher (via aggregate)
var order = Order.Create(orderId);
await _repository.SaveAsync(order, cancellationToken);
// OrderCreatedEvent published via outbox

// Subscriber
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct) { }
}
```

### Scheduling

**MassTransit:**
```csharp
await context.ScheduleSend(
    TimeSpan.FromHours(24),
    new SendOrderReminderEmail { OrderId = orderId });
```

**Dispatch:**
```csharp
// Use Hangfire or similar for scheduling
BackgroundJob.Schedule<SendOrderReminderEmailHandler>(
    x => x.SendAsync(orderId),
    TimeSpan.FromHours(24));
```

**Note:** Dispatch doesn't include built-in scheduling. Use external job scheduler (Hangfire, Quartz.NET).

## Common Migration Issues

### Issue 1: No Message Broker

**Problem:** Dispatch uses database-backed messaging by default.

**Solution:** Use the transport layer for external broker integration:

```csharp
// Use RabbitMQ transport package for broker-backed messaging
builder.Services.AddRabbitMqTransport(options =>
{
    options.HostName = "localhost";
    options.UserName = "guest";
    options.Password = "guest";
});

// Or use the outbox with transport integration
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});
```

### Issue 2: Complex Routing

**Problem:** MassTransit supports complex routing and topology.

**Solution:** Dispatch uses event handlers for routing. Register handlers for different event types:

```csharp
// Each event type is routed to its registered handlers automatically
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Handler for order events
public class OrderEventHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        // Route to orders service
    }
}

// Handler for payment events
public class PaymentEventHandler : IEventHandler<PaymentProcessedEvent>
{
    public async Task HandleAsync(
        PaymentProcessedEvent @event, CancellationToken cancellationToken)
    {
        // Route to payments service
    }
}
```

### Issue 3: Distributed Transactions

**Problem:** MassTransit supports distributed transactions via broker.

**Solution:** Use outbox pattern for local consistency. For idempotent handling, track processed event IDs in your own store:

```csharp
public class IdempotentEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IProcessedEventTracker _tracker;

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        // Check if already processed (implement using your data store)
        if (await _tracker.HasBeenProcessedAsync(@event.EventId, cancellationToken))
        {
            return; // Idempotent - skip duplicate
        }

        // Process event
        // ...

        // Mark as processed
        await _tracker.MarkAsProcessedAsync(@event.EventId, cancellationToken);
    }
}
// Note: IProcessedEventTracker is your own interface — implement using
// your database to track processed event IDs for idempotency.
```

### Issue 4: Missing Scheduling

**Problem:** Dispatch doesn't include message scheduling.

**Solution:** Use Hangfire for background jobs:

```bash
dotnet add package Hangfire
dotnet add package Hangfire.SqlServer
```

```csharp
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// Schedule messages
BackgroundJob.Schedule<IDispatcher>(
    dispatcher => dispatcher.DispatchAsync(
        new SendReminderCommand(orderId),
        CancellationToken.None),
    TimeSpan.FromHours(24));
```

## Testing Migration

### Unit Tests

**Before (MassTransit Consumer):**
```csharp
[Fact]
public async Task Consume_ShouldProcessOrder()
{
    // Arrange
    var consumer = new OrderCreatedConsumer(_repository);
    var context = Mock.Of<ConsumeContext<OrderCreated>>(c =>
        c.Message == new OrderCreated { OrderId = "123" });

    // Act
    await consumer.Consume(context);

    // Assert
    // Verify
}
```

**After (Dispatch Event Handler):**
```csharp
[Fact]
public async Task HandleAsync_ShouldProcessOrder()
{
    // Arrange
    var handler = new OrderCreatedEventHandler(_repository);
    var @event = new OrderCreatedEvent("123", "customer-1");

    // Act
    await handler.HandleAsync(@event, CancellationToken.None);

    // Assert
    // Verify
}
```

### Integration Tests

**Before:**
```csharp
var harness = new InMemoryTestHarness();
var consumerHarness = harness.Consumer<OrderCreatedConsumer>();

await harness.Start();
try
{
    await harness.InputQueueSendEndpoint.Send(new OrderCreated { OrderId = "123" });

    Assert.True(await harness.Consumed.Any<OrderCreated>());
    Assert.True(await consumerHarness.Consumed.Any<OrderCreated>());
}
finally
{
    await harness.Stop();
}
```

**After:**
```csharp
var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

var @event = new OrderCreatedEvent("123", "customer-1");
await dispatcher.DispatchAsync(@event, CancellationToken.None);

// Verify handler processed event
```

## Migration Checklist

- [ ] Evaluate whether to keep MassTransit for inter-service messaging
- [ ] Install Dispatch packages
- [ ] Configure outbox with SQL Server
- [ ] Migrate message contracts to `IDomainEvent`
- [ ] Migrate consumers to `IEventHandler<T>`
- [ ] Migrate publishers to event-sourced aggregates
- [ ] Migrate sagas to process managers
- [ ] Implement custom `IOutboxPublisher` if needed
- [ ] Replace MassTransit scheduling with Hangfire
- [ ] Update unit tests
- [ ] Update integration tests
- [ ] Remove MassTransit packages (if fully migrated)

## Performance Comparison

| Metric | MassTransit (RabbitMQ) | Excalibur (SQL Outbox) |
|--------|------------------------|--------------------------------|
| Publish latency | ~2-5ms | ~3-7ms (includes DB write) |
| Throughput | ~10,000 msg/sec | ~2,000 msg/sec (depends on DB) |
| Message ordering | Per-queue | Per-aggregate (event sourcing) |
| Delivery guarantee | At-least-once | Exactly-once (via outbox) |
| Infrastructure | Requires broker | Database only |

**Conclusion:** MassTransit has higher throughput for high-volume scenarios, but Dispatch provides stronger consistency guarantees and simpler infrastructure.

## Getting Help

- **Documentation**: [Dispatch Introduction](../intro.md)
- **GitHub Issues**: [Report Migration Issues](https://github.com/TrigintaFaces/Excalibur/issues)
- **Examples**: See [samples/](https://github.com/TrigintaFaces/Excalibur/tree/main/samples)

## Next Steps

1. Review [Outbox Pattern](../patterns/outbox.md)
2. Learn [Event Sourcing](/docs/event-sourcing)
3. Implement [Sagas](/docs/sagas)
4. Set up [Monitoring](../observability/health-checks.md)
5. Review [Deployment Options](../deployment/docker.md)

## See Also

- [Migration Overview](index.md) - All migration guides
- [From NServiceBus](from-nservicebus.md) - NServiceBus migration guide
- [Getting Started](../getting-started/index.md) - New project setup from scratch

