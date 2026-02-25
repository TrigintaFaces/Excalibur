---
sidebar_position: 3
---

# Migrating from NServiceBus

A comprehensive guide for migrating from NServiceBus to Excalibur, covering messaging patterns, sagas, outbox, and enterprise integration.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An existing application using NServiceBus
- Familiarity with [getting started](../getting-started/index.md) and [transports](../transports/choosing-a-transport.md)

## Overview

NServiceBus is a mature, enterprise-grade messaging platform with extensive features. Excalibur offers a **lighter-weight, domain-focused alternative** for teams that need messaging and event sourcing without the complexity and licensing costs of NServiceBus.

## When to Migrate

Consider Excalibur if you:
- Want to eliminate NServiceBus licensing costs
- Need simpler infrastructure (database-backed vs. message broker)
- Prefer built-in event sourcing over separate implementation
- Want lower operational complexity
- Don't need advanced enterprise features (saga timeouts, critical errors, etc.)

**Keep NServiceBus if you:**
- Require advanced enterprise patterns (delayed retries, circuit breakers)
- Need comprehensive monitoring and ServiceControl/ServicePulse
- Have complex distributed transactions
- Rely on Particular Platform ecosystem

## Key Differences

| Feature | NServiceBus | Excalibur |
|---------|-------------|-------------------|
| **License** | Commercial (per endpoint) | Open source (free) |
| **Message Transport** | RabbitMQ, Azure Service Bus, MSMQ, SQL | Database (SQL Server) + optional broker |
| **Outbox** | Requires persistence configuration | Built-in, first-class support |
| **Sagas** | Full-featured with timeouts | Simple process managers |
| **Event Sourcing** | Not included | Built-in with `AggregateRoot` |
| **Retries** | First-level + second-level retries | Simple retry via middleware |
| **Monitoring** | ServiceControl + ServicePulse | Standard observability (metrics, logs, traces) |
| **Complexity** | High (enterprise-grade) | Low (focused on core patterns) |

## Side-by-Side Comparison

### Message Handlers

**NServiceBus:**
```csharp
// Message
public class PlaceOrder : ICommand
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

// Handler
public class PlaceOrderHandler : IHandleMessages<PlaceOrder>
{
    private readonly IOrderRepository _repository;

    public async Task Handle(
        PlaceOrder message,
        IMessageHandlerContext context)
    {
        // Create order
        var order = new Order(message.OrderId, message.CustomerId);

        await _repository.SaveAsync(order);

        // Publish event
        await context.Publish(new OrderPlaced
        {
            OrderId = message.OrderId,
            CustomerId = message.CustomerId
        });
    }
}
```

**Excalibur.Dispatch:**
```csharp
// Action
public record PlaceOrderAction(
    string OrderId,
    string CustomerId,
    List<OrderItem> Items) : IDispatchAction;

// Handler
public class PlaceOrderHandler : IActionHandler<PlaceOrderAction>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        PlaceOrderAction action,
        CancellationToken cancellationToken)
    {
        // Create order (raises OrderPlacedEvent internally)
        var order = Order.Create(action.OrderId, action.CustomerId);

        // Save aggregate (events published via outbox automatically)
        await _repository.SaveAsync(order, cancellationToken);
    }
}

// Aggregate
public class Order : AggregateRoot
{
    public static Order Create(string orderId, string customerId)
    {
        var order = new Order { Id = orderId };
        order.RaiseEvent(new OrderPlacedEvent(orderId, customerId));
        return order;
    }
}
```

**Key Differences:**
- NServiceBus: Explicit `context.Publish()` for events
- Dispatch: Events raised in aggregate, published via outbox automatically
- NServiceBus: `ICommand` marker interface
- Dispatch: `IDispatchAction` or `IDispatchAction<TResult>` with optional return value

### Event Handlers

**NServiceBus:**
```csharp
// Event
public class OrderPlaced : IEvent
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal TotalValue { get; set; }
}

// Handler
public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
{
    private readonly IEmailService _emailService;

    public async Task Handle(
        OrderPlaced message,
        IMessageHandlerContext context)
    {
        await _emailService.SendOrderConfirmationAsync(
            message.CustomerId,
            message.OrderId);
    }
}
```

**Excalibur.Dispatch:**
```csharp
// Domain Event
public record OrderPlacedEvent(
    string OrderId,
    string CustomerId,
    decimal TotalValue) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderPlacedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Handler
public class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IEmailService _emailService;

    public async Task HandleAsync(
        OrderPlacedEvent @event,
        CancellationToken cancellationToken)
    {
        await _emailService.SendOrderConfirmationAsync(
            @event.CustomerId,
            @event.OrderId);
    }
}
```

**Key Differences:**
- NServiceBus: Simple POCO events
- Dispatch: Rich `IDomainEvent` interface with metadata
- NServiceBus: `IMessageHandlerContext` for sending/publishing
- Dispatch: Direct dependency injection

### Sagas

**NServiceBus:**
```csharp
// Saga
public class OrderProcessingSaga : Saga<OrderProcessingSagaData>,
    IAmStartedByMessages<OrderPlaced>,
    IHandleMessages<PaymentReceived>,
    IHandleMessages<OrderShipped>
{
    protected override void ConfigureHowToFindSaga(
        SagaPropertyMapper<OrderProcessingSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.OrderId)
            .ToMessage<OrderPlaced>(msg => msg.OrderId)
            .ToMessage<PaymentReceived>(msg => msg.OrderId)
            .ToMessage<OrderShipped>(msg => msg.OrderId);
    }

    public async Task Handle(
        OrderPlaced message,
        IMessageHandlerContext context)
    {
        Data.OrderId = message.OrderId;
        Data.CustomerId = message.CustomerId;

        // Request payment
        await context.Send(new ProcessPayment
        {
            OrderId = message.OrderId,
            Amount = message.TotalValue
        });
    }

    public async Task Handle(
        PaymentReceived message,
        IMessageHandlerContext context)
    {
        Data.PaymentReceived = true;

        // Request shipping
        await context.Send(new ShipOrder
        {
            OrderId = message.OrderId
        });
    }

    public async Task Handle(
        OrderShipped message,
        IMessageHandlerContext context)
    {
        Data.OrderShipped = true;
        MarkAsComplete();
    }
}

// Saga data
public class OrderProcessingSagaData : ContainSagaData
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public bool PaymentReceived { get; set; }
    public bool OrderShipped { get; set; }
}
```

**Excalibur.Dispatch:**
```csharp
// Process Manager (state machine)
public class OrderProcessManager : ProcessManager<OrderProcessData>
{
    public OrderProcessManager(
        OrderProcessData data,
        IDispatcher dispatcher,
        ILogger<OrderProcessManager> logger)
        : base(data, dispatcher, logger)
    {
        Initially(s => s
            .When<OrderPlacedEvent>(h => h
                .TransitionTo("PaymentPending")
                .Then(ctx =>
                {
                    ctx.Data.OrderId = ctx.Message.OrderId;
                    ctx.Data.CustomerId = ctx.Message.CustomerId;
                })));

        During("PaymentPending", s => s
            .When<PaymentReceivedEvent>(h => h
                .TransitionTo("Shipping")));

        During("Shipping", s => s
            .When<OrderShippedEvent>(h => h
                .Complete()));
    }
}

public class OrderProcessData : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CurrentStateName { get; set; } = "Initial";
}
```

**Key Differences:**
- NServiceBus: `Saga<T>` with `ContainSagaData`
- Dispatch: `ProcessManager<TData>` with `SagaState` data class
- NServiceBus: `ConfigureHowToFindSaga()` for correlation
- Dispatch: Automatic correlation via event `AggregateId`
- NServiceBus: Saga timeouts supported
- Dispatch: No built-in timeouts (use Hangfire)

## Migration Strategies

### Strategy 1: Gradual Migration (Recommended)

Run both frameworks side-by-side, migrating endpoint by endpoint:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Keep NServiceBus for external messaging
var endpointConfiguration = new EndpointConfiguration("MyService");
endpointConfiguration.UseTransport<RabbitMQTransport>();
endpointConfiguration.EnableInstallers();

var endpointInstance = await Endpoint.Start(endpointConfiguration);
builder.Services.AddSingleton<IMessageSession>(endpointInstance);

// Add Dispatch for domain logic
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});

var app = builder.Build();
```

**Bridge NServiceBus to Dispatch:**
```csharp
// NServiceBus handler delegates to Dispatch
public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
{
    private readonly IDispatcher _dispatcher;

    public async Task Handle(
        OrderPlaced message,
        IMessageHandlerContext context)
    {
        // Convert NServiceBus message to Dispatch event
        var @event = new OrderPlacedEvent(
            message.OrderId,
            message.CustomerId,
            message.TotalValue);

        // Process via Dispatch
        await _dispatcher.DispatchAsync(@event, CancellationToken.None);
    }
}
```

### Strategy 2: Replace Transport

Keep NServiceBus contract conventions, replace transport with Dispatch outbox:

```csharp
// Bridge: NServiceBus handler delegates to Dispatch internally
public class NServiceBusBridgeHandler : IHandleMessages<OrderPlaced>
{
    private readonly IDispatcher _dispatcher;

    public async Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        // Convert NServiceBus message to Dispatch event
        var @event = new OrderPlacedEvent(message.OrderId, message.CustomerId);
        await _dispatcher.DispatchAsync(@event, context.CancellationToken);
    }
}

// Use Dispatch outbox for reliable local messaging
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});
```

**Benefits:**
- Keep existing NServiceBus infrastructure
- Use Dispatch for domain logic and outbox
- Gradual internal migration

### Strategy 3: Full Replacement

Remove NServiceBus entirely, replace with Dispatch + external broker:

```csharp
// Dispatch with outbox and transport integration
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});
builder.Services.AddOutboxHostedService();

// For broker-backed transport, add the appropriate transport package
builder.Services.AddRabbitMqTransport(options =>
{
    options.HostName = "localhost";
});
```

## Step-by-Step Migration

### Step 1: Install Packages

```bash
# Optional: Keep NServiceBus for gradual migration
# dotnet add package NServiceBus

dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Abstractions
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
```

### Step 2: Update Configuration

**Before (NServiceBus):**
```csharp
var endpointConfiguration = new EndpointConfiguration("MyService");

var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();
transport.ConnectionString("host=localhost");
transport.UseConventionalRoutingTopology();

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(connectionString));

endpointConfiguration.EnableOutbox();
endpointConfiguration.EnableInstallers();

var endpointInstance = await Endpoint.Start(endpointConfiguration);
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

### Step 3: Migrate Commands

**Before:**
```csharp
public class PlaceOrder : ICommand
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
}

public class PlaceOrderHandler : IHandleMessages<PlaceOrder>
{
    public async Task Handle(PlaceOrder message, IMessageHandlerContext context)
    {
        // Handler logic
    }
}
```

**After:**
```csharp
public record PlaceOrderAction(string OrderId, string CustomerId)
    : IDispatchAction;

public class PlaceOrderHandler : IActionHandler<PlaceOrderAction>
{
    public async Task HandleAsync(
        PlaceOrderAction action,
        CancellationToken cancellationToken)
    {
        // Handler logic (same)
    }
}
```

### Step 4: Migrate Events

**Before:**
```csharp
public class OrderPlaced : IEvent
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
}

// Publishing
await context.Publish(new OrderPlaced
{
    OrderId = orderId,
    CustomerId = customerId
});
```

**After:**
```csharp
public record OrderPlacedEvent(string OrderId, string CustomerId)
    : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderPlacedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Publishing (via aggregate)
var order = Order.Create(orderId, customerId);
order.RaiseEvent(new OrderPlacedEvent(orderId, customerId));
await _repository.SaveAsync(order, cancellationToken);
```

### Step 5: Migrate Sagas

**Before:**
```csharp
public class OrderProcessingSaga : Saga<OrderProcessingSagaData>,
    IAmStartedByMessages<OrderPlaced>,
    IHandleMessages<PaymentReceived>
{
    protected override void ConfigureHowToFindSaga(
        SagaPropertyMapper<OrderProcessingSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.OrderId)
            .ToMessage<OrderPlaced>(msg => msg.OrderId)
            .ToMessage<PaymentReceived>(msg => msg.OrderId);
    }

    public async Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        Data.OrderId = message.OrderId;
        await context.Send(new ProcessPayment { OrderId = message.OrderId });
    }

    public async Task Handle(PaymentReceived message, IMessageHandlerContext context)
    {
        MarkAsComplete();
    }
}
```

**After:**
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
            .When<OrderPlacedEvent>(h => h
                .TransitionTo("PaymentPending")
                .Then(ctx => ctx.Data.OrderId = ctx.Message.OrderId)));

        During("PaymentPending", s => s
            .When<PaymentReceivedEvent>(h => h
                .Complete()));
    }
}
```

### Step 6: Migrate Outbox

NServiceBus outbox is automatically replaced by Dispatch outbox when using event sourcing.

**Before (NServiceBus):**
```csharp
endpointConfiguration.EnableOutbox();

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.ConnectionBuilder(() => new SqlConnection(connectionString));
```

**After (Dispatch):**
```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString);
});
```

## Feature Mapping

### Retries

**NServiceBus:**
```csharp
endpointConfiguration.Recoverability().Immediate(
    immediate => immediate.NumberOfRetries(3));

endpointConfiguration.Recoverability().Delayed(
    delayed =>
    {
        delayed.NumberOfRetries(5);
        delayed.TimeIncrease(TimeSpan.FromSeconds(10));
    });
```

**Dispatch:**
```csharp
// Simple retry middleware
public class RetryMiddleware : IDispatchMiddleware
{
    private const int MaxRetries = 3;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await nextDelegate(message, context, cancellationToken);
            }
            catch (Exception) when (attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                // Retry
            }
        }

        throw; // Final attempt failed
    }
}
```

### Delayed Messages

**NServiceBus:**
```csharp
var sendOptions = new SendOptions();
sendOptions.DelayDeliveryWith(TimeSpan.FromHours(24));
await context.Send(new SendReminderEmail(), sendOptions);
```

**Dispatch:**
```csharp
// Use Hangfire for scheduling
BackgroundJob.Schedule<IDispatcher>(
    dispatcher => dispatcher.DispatchAsync(
        new SendReminderEmailCommand(),
        CancellationToken.None),
    TimeSpan.FromHours(24));
```

### Message Routing

**NServiceBus:**
```csharp
var routing = transport.Routing();
routing.RouteToEndpoint(
    typeof(PlaceOrder),
    "Sales");
```

**Dispatch:**
```csharp
// Routing is handled via handler registration — each message type
// is automatically routed to its registered handler(s)
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// For external transport routing, use transport-specific configuration
// (e.g., topic-based routing in RabbitMQ or Azure Service Bus)
```

## Common Migration Issues

### Issue 1: Missing Saga Timeouts

**Problem:** Dispatch doesn't support saga timeouts natively.

**Solution:** Use Hangfire for delayed commands:

```csharp
// In your startup, schedule the timeout using Hangfire alongside the process manager
// The process manager handles the timeout command like any other message:

public class OrderProcessManager : ProcessManager<OrderProcessData>
{
    public OrderProcessManager(
        OrderProcessData data,
        IDispatcher dispatcher,
        ILogger<OrderProcessManager> logger)
        : base(data, dispatcher, logger)
    {
        Initially(s => s
            .When<OrderPlacedEvent>(h => h
                .TransitionTo("AwaitingPayment")
                .Then(ctx => ctx.Data.OrderId = ctx.Message.OrderId)));
    }
}

// Schedule timeout externally via Hangfire
BackgroundJob.Schedule<IDispatcher>(
    dispatcher => dispatcher.DispatchAsync(
        new OrderTimeoutCommand(orderId),
        CancellationToken.None),
    TimeSpan.FromHours(24));
```

### Issue 2: No ServiceControl

**Problem:** Dispatch doesn't include ServiceControl/ServicePulse equivalent.

**Solution:** Use standard observability:

```csharp
// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Or Prometheus + Grafana
builder.Services.AddPrometheusMetrics();

// Custom metrics middleware
public class MetricsMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await nextDelegate(message, context, cancellationToken);
            Metrics.RecordSuccess(message.GetType().Name, stopwatch.Elapsed);
            return result;
        }
        catch (Exception)
        {
            Metrics.RecordFailure(message.GetType().Name, stopwatch.Elapsed);
            throw;
        }
    }
}
```

### Issue 3: Different Error Handling

**Problem:** NServiceBus has sophisticated error handling with error queues.

**Solution:** Implement custom error handling:

```csharp
public class ErrorHandlingMiddleware : IDispatchMiddleware
{
    private readonly IErrorQueue _errorQueue;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await nextDelegate(message, context, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log to error queue/table
            await _errorQueue.WriteAsync(new ErrorMessage
            {
                MessageType = typeof(TRequest).Name,
                Payload = JsonSerializer.Serialize(request),
                Exception = ex.ToString(),
                OccurredAt = DateTimeOffset.UtcNow
            });

            throw;
        }
    }
}
```

## Testing Migration

### Unit Tests

**Before:**
```csharp
[Fact]
public async Task Handle_ShouldPlaceOrder()
{
    var handler = new PlaceOrderHandler(_repository);
    var context = new TestableMessageHandlerContext();

    await handler.Handle(
        new PlaceOrder { OrderId = "123" },
        context);

    Assert.Single(context.PublishedMessages);
}
```

**After:**
```csharp
[Fact]
public async Task HandleAsync_ShouldPlaceOrder()
{
    var handler = new PlaceOrderCommandHandler(_repository);

    await handler.HandleAsync(
        new PlaceOrderCommand("123", "customer-1"),
        CancellationToken.None);

    // Verify via repository
}
```

### Integration Tests

**Before:**
```csharp
var endpointConfiguration = new EndpointConfiguration("TestEndpoint");
endpointConfiguration.UseTransport<LearningTransport>();
var endpoint = await Endpoint.Start(endpointConfiguration);

await endpoint.Send(new PlaceOrder { OrderId = "123" });
```

**After:**
```csharp
var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

await dispatcher.DispatchAsync(
    new PlaceOrderCommand("123", "customer-1"),
    CancellationToken.None);
```

## Migration Checklist

- [ ] Evaluate licensing cost savings vs. feature trade-offs
- [ ] Install Dispatch packages
- [ ] Configure outbox with SQL Server
- [ ] Migrate commands: `ICommand` → `IDispatchAction` or `IDispatchAction<T>`
- [ ] Migrate handlers: `IHandleMessages<T>` → `IActionHandler<T>` or `IActionHandler<T, TResult>`
- [ ] Update method signatures: `Handle()` → `HandleAsync()`
- [ ] Migrate events: `IEvent` → `IDomainEvent`
- [ ] Migrate sagas: `Saga<T>` → `ProcessManager<TData>`
- [ ] Replace saga timeouts with Hangfire
- [ ] Implement custom retry logic
- [ ] Set up observability (Application Insights/Prometheus)
- [ ] Migrate error handling
- [ ] Update unit tests
- [ ] Update integration tests
- [ ] Remove NServiceBus packages (if fully migrated)

## Performance Comparison

| Metric | NServiceBus (RabbitMQ) | Excalibur (SQL Outbox) |
|--------|------------------------|--------------------------------|
| Throughput | ~8,000 msg/sec | ~2,000 msg/sec |
| Latency (p99) | ~10ms | ~15ms |
| Infrastructure cost | High (broker + licensing) | Low (database only) |
| Delivery guarantee | At-least-once | Exactly-once (outbox) |

**Conclusion:** NServiceBus offers higher performance but at higher cost. Dispatch provides strong consistency guarantees with simpler infrastructure.

## Getting Help

- **Documentation**: [Dispatch Introduction](../intro.md)
- **GitHub Issues**: [Report Migration Issues](https://github.com/TrigintaFaces/Excalibur/issues)
- **Examples**: See [samples/](https://github.com/TrigintaFaces/Excalibur/tree/main/samples)

## Next Steps

1. Review [Event Sourcing](/docs/event-sourcing)
2. Learn [Outbox Pattern](../patterns/outbox.md)
3. Implement [Sagas](/docs/sagas)
4. Set up [Monitoring](../observability/health-checks.md)
5. Review [Deployment](../deployment/docker.md)

## See Also

- [Migration Overview](index.md) - All migration guides
- [From MassTransit](from-masstransit.md) - MassTransit migration guide
- [Getting Started](../getting-started/index.md) - New project setup from scratch

