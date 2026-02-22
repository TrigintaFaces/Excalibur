---
sidebar_position: 2
title: Getting Started
description: Install Excalibur.Dispatch and create your first message handler in 5 minutes
---

# Getting Started with Excalibur

This guide gets you up and running with Excalibur.Dispatch in under 5 minutes. By the end, you'll have a working message handler processing commands through the messaging pipeline.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An IDE (Visual Studio, VS Code, or Rider)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Abstractions
  ```

## Step 1: Define an Action

Actions are messages that trigger handlers. They can be commands (no return value) or queries (with return value).

```csharp
using Excalibur.Dispatch.Abstractions;

// Action without return value
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

// Action with return value
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;
```

## Step 2: Create a Handler

Handlers process actions. Use `IActionHandler<TAction>` for commands or `IActionHandler<TAction, TResult>` for queries.

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

// Handler for action without return value
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items
        };

        await _repository.SaveAsync(order, cancellationToken);
    }
}

// Handler for action with return value
public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Order> HandleAsync(
        GetOrderAction action,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(action.OrderId, cancellationToken);
    }
}
```

## Step 3: Register Services

Configure Excalibur.Dispatch in your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Dispatch with fluent configuration (recommended)
builder.Services.AddDispatch(dispatch =>
{
    // Automatically discover and register handlers from your assembly
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Register your dependencies
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

var app = builder.Build();
```

## Step 4: Dispatch Messages

Inject `IDispatcher` and send messages. No explicit context is needed — the framework manages context automatically:

```csharp
using Excalibur.Dispatch.Abstractions;

public class OrderController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OrderController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var action = new CreateOrderAction(request.CustomerId, request.Items);

        // No context needed - Dispatch creates one automatically
        var result = await _dispatcher.DispatchAsync(action, cancellationToken);

        if (result.IsSuccess)
            return Ok();

        return BadRequest(result.ErrorMessage);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var action = new GetOrderAction(orderId);

        // Context-less dispatch for queries too
        var result = await _dispatcher.DispatchAsync<GetOrderAction, Order>(
            action, cancellationToken);

        if (result.IsSuccess)
            return Ok(result.ReturnValue);

        return NotFound(result.ErrorMessage);
    }
}
```

## Complete Example

Here's a complete minimal example:

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

app.MapPost("/greet", async (
    GreetRequest request,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    var action = new GreetAction(request.Name);

    // Simple dispatch - no context needed!
    var result = await dispatcher.DispatchAsync<GreetAction, string>(action, ct);

    return result.IsSuccess ? Results.Ok(result.ReturnValue) : Results.BadRequest();
});

app.Run();

// Action with return value
public record GreetAction(string Name) : IDispatchAction<string>;

// Handler
public class GreetHandler : IActionHandler<GreetAction, string>
{
    public Task<string> HandleAsync(GreetAction action, CancellationToken ct)
    {
        return Task.FromResult($"Hello, {action.Name}!");
    }
}

// Request DTO
public record GreetRequest(string Name);
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `IDispatchAction` | Base interface for actions (commands without return value) |
| `IDispatchAction<TResult>` | Base interface for actions with return value |
| `IActionHandler<TAction>` | Handler for actions without return value |
| `IActionHandler<TAction, TResult>` | Handler for actions with return value |
| `IDispatcher` | Central dispatcher for sending messages |
| `IMessageContext` | Context for message metadata (correlation, tenant, etc.) - managed automatically |
| `IMessageResult` | Result wrapper containing success/failure and errors |

## Context Management

Excalibur.Dispatch automatically manages message context for you:

- **Top-level dispatch**: A new context is created with a unique `CorrelationId`
- **Nested dispatch**: Use `DispatchChildAsync` to propagate context in handlers
- **Ambient context**: The current context is available via `IMessageContextAccessor`

```csharp
// From a controller (top-level) - context created automatically
await _dispatcher.DispatchAsync(action, cancellationToken);

// From within a handler (nested) - use child context for proper tracing
await _dispatcher.DispatchChildAsync(action, cancellationToken);
```

See [Handlers](../handlers.md#context-propagation) for more details on nested dispatch patterns.

## Excalibur.Dispatch vs MediatR

If you're coming from MediatR, here's how concepts map:

| MediatR | Excalibur.Dispatch |
|---------|----------|
| `IRequest` | `IDispatchAction` |
| `IRequest<TResponse>` | `IDispatchAction<TResult>` |
| `IRequestHandler<TRequest>` | `IActionHandler<TAction>` |
| `IRequestHandler<TRequest, TResponse>` | `IActionHandler<TAction, TResult>` |
| `INotification` | `IDispatchEvent` |
| `INotificationHandler<T>` | `IEventHandler<TEvent>` |
| `IMediator` | `IDispatcher` |

## Adding More Packages

`Excalibur.Dispatch` is the messaging core. Add more Excalibur packages as your architecture grows:

| Need | Package |
|------|---------|
| Domain modeling (aggregates, entities) | `Excalibur.Domain` |
| Event sourcing with persistence | `Excalibur.EventSourcing` |
| SQL Server event store | `Excalibur.EventSourcing.SqlServer` |
| ASP.NET Core hosting integration | `Excalibur.Hosting` |

```bash
# Add hosting (includes unified entry point for all subsystems)
dotnet add package Excalibur.Hosting

# Add domain modeling
dotnet add package Excalibur.Domain

# Add event sourcing
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
```

### Unified Registration

Use `AddExcalibur()` as the single entry point for domain, event sourcing, and saga subsystems. It registers messaging primitives with sensible defaults:

```csharp
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString))
        .AddCdc(cdc => cdc.TrackTable<Order>())
        .AddSagas(opts => opts.EnableTimeouts = true)
        .AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(30));
});
```

Need custom messaging configuration (transports, pipelines, middleware)? Call `AddDispatch` with a builder action:

```csharp
// Configure Dispatch with transports and middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.AddObservability();
});

// Configure Excalibur subsystems
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

You can also bind Excalibur options from `appsettings.json`:

```json title="appsettings.json"
{
  "Excalibur": {
    "EventSourcing": { "Enabled": true, "SnapshotFrequency": 100 },
    "Outbox": { "Enabled": true, "PollingInterval": "00:00:05" },
    "Saga": { "Enabled": false },
    "LeaderElection": { "Enabled": false },
    "Cdc": { "Enabled": false }
  }
}
```

```csharp
builder.Services.Configure<ExcaliburOptions>(
    builder.Configuration.GetSection("Excalibur"));
```

See the **[Package Guide](../package-guide)** for the complete package selection framework with migration paths and code examples.

## Step 5: Add a Transport (Optional)

The examples above dispatch messages **in-process** (no broker needed). When you're ready to send messages to a real broker, add a transport package and configure routing:

```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

```csharp
// Register the transport with destination mapping
services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString("amqp://guest:guest@localhost:5672/")
       .MapQueue<CreateOrderAction>("orders-queue");
});

// Configure routing rules
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<CreateOrderAction>().To("rabbitmq")
            .Default("local");  // Keep unmatched messages in-process
    });
});
```

Your handlers don't change — only the registration code changes. See [Choosing a Transport](../transports/choosing-a-transport.md) to pick the right broker and [Message Routing](../patterns/routing.md) for the full routing API.

## What's Next

- [Your First Event](./first-event.md) - Create and handle domain events
- [Choosing a Transport](../transports/choosing-a-transport.md) - Pick a broker and configure destinations
- [Message Routing](../patterns/routing.md) - Understand the three-layer routing model
- [Project Templates](./project-templates.md) - Scaffold new projects
- [Samples](./samples.md) - Browse working examples
- [Handlers](../handlers.md) - Learn about action and event handlers
- [Pipeline](../pipeline/index.md) - Understand middleware and behaviors

## See Also

- [Project Templates](./project-templates.md) — Scaffold new Excalibur projects with dotnet new templates
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) — Deep dive into action types, handler patterns, and result handling
- [Dependency Injection](../core-concepts/dependency-injection.md) — Configure Dispatch services and handler registration in the DI container

