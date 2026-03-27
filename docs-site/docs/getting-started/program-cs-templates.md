---
sidebar_position: 4
title: "Program.cs Templates"
description: Copy-paste-ready Program.cs files for the most common Excalibur scenarios -- just paste, install packages, and run.
---

# Program.cs Templates

Copy-paste-ready starting points for the most common scenarios. Each template is a complete, working `Program.cs` that you can paste into a new project and run immediately.

:::tip How to use
1. Create a new project: `dotnet new web -n MyProject`
2. Install the listed packages
3. Replace the generated `Program.cs` with the template below
4. Run: `dotnet run`
:::

## Dispatch Only (MediatR Replacement)

In-process messaging with commands, queries, and events. No infrastructure dependencies.

**Packages:**
```bash
dotnet add package Excalibur.Dispatch
```

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

// Command endpoint (no return value)
app.MapPost("/greet", async (GreetAction action, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(action, ct);
    return result.IsSuccess ? Results.Ok("Greeted!") : Results.BadRequest(result.ErrorMessage);
});

// Query endpoint (with return value)
app.MapGet("/greet/{name}", async (string name, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<GetGreetingQuery, string>(
        new GetGreetingQuery(name), ct);
    return result.IsSuccess ? Results.Ok(result.ReturnValue) : Results.NotFound();
});

app.Run();

// --- Messages ---
public record GreetAction(string Name) : IDispatchAction;
public record GetGreetingQuery(string Name) : IDispatchAction<string>;
public record GreetedEvent(string Name) : IDispatchEvent;

// --- Handlers ---
public class GreetHandler(IDispatcher dispatcher) : IActionHandler<GreetAction>
{
    public async Task HandleAsync(GreetAction action, CancellationToken ct)
    {
        Console.WriteLine($"Hello, {action.Name}!");
        await dispatcher.DispatchAsync(new GreetedEvent(action.Name), ct);
    }
}

public class GetGreetingHandler : IActionHandler<GetGreetingQuery, string>
{
    public Task<string> HandleAsync(GetGreetingQuery action, CancellationToken ct)
        => Task.FromResult($"Hello, {action.Name}!");
}

public class GreetedEventHandler : IEventHandler<GreetedEvent>
{
    public Task HandleAsync(GreetedEvent @event, CancellationToken ct)
    {
        Console.WriteLine($"[Event] {DateTime.UtcNow}: {@event.Name} was greeted");
        return Task.CompletedTask;
    }
}
```

---

## Event Sourcing with SQL Server

Aggregates, event store, projections, and read models backed by SQL Server.

**Packages:**
```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
```

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=MyApp;Trusted_Connection=true;";

// Dispatch (messaging)
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Event Sourcing (aggregates + repository)
builder.Services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<CounterAggregate, Guid>(id => new CounterAggregate(id));
});

// SQL Server persistence
builder.Services.AddSqlServerEventSourcing(opts => opts.ConnectionString = connectionString);

var app = builder.Build();

// Create a counter
app.MapPost("/counters", async (IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<CreateCounterAction, Guid>(
        new CreateCounterAction(), ct);
    return result.IsSuccess
        ? Results.Created($"/counters/{result.ReturnValue}", new { Id = result.ReturnValue })
        : Results.BadRequest(result.ErrorMessage);
});

// Increment a counter
app.MapPost("/counters/{id:guid}/increment", async (
    Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new IncrementCounterAction(id), ct);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
});

// Get counter value
app.MapGet("/counters/{id:guid}", async (
    Guid id, IEventSourcedRepository<CounterAggregate, Guid> repo, CancellationToken ct) =>
{
    var counter = await repo.GetByIdAsync(id, ct);
    return counter is null
        ? Results.NotFound()
        : Results.Ok(new { counter.Id, counter.Value });
});

app.Run();

// --- Messages ---
public record CreateCounterAction() : IDispatchAction<Guid>;
public record IncrementCounterAction(Guid CounterId) : IDispatchAction;
public record CounterCreated(Guid CounterId) : DomainEvent
{
    public override string AggregateId => CounterId.ToString();
}
public record CounterIncremented(Guid CounterId) : DomainEvent
{
    public override string AggregateId => CounterId.ToString();
}

// --- Aggregate ---
public class CounterAggregate : AggregateRoot<Guid>
{
    public int Value { get; private set; }

    public CounterAggregate() { }
    public CounterAggregate(Guid id) : base(id) { }

    public void Create() => RaiseEvent(new CounterCreated(Id));
    public void Increment() => RaiseEvent(new CounterIncremented(Id));

    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        CounterCreated => true,
        CounterIncremented => ApplyIncrement(),
        _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
    };

    private bool ApplyIncrement() { Value++; return true; }
}

// --- Handlers ---
public class CreateCounterHandler(
    IEventSourcedRepository<CounterAggregate, Guid> repo) : IActionHandler<CreateCounterAction, Guid>
{
    public async Task<Guid> HandleAsync(CreateCounterAction action, CancellationToken ct)
    {
        var counter = new CounterAggregate(Guid.NewGuid());
        counter.Create();
        await repo.SaveAsync(counter, ct);
        return counter.Id;
    }
}

public class IncrementCounterHandler(
    IEventSourcedRepository<CounterAggregate, Guid> repo) : IActionHandler<IncrementCounterAction>
{
    public async Task HandleAsync(IncrementCounterAction action, CancellationToken ct)
    {
        var counter = await repo.GetByIdAsync(action.CounterId, ct)
            ?? throw new InvalidOperationException($"Counter {action.CounterId} not found.");
        counter.Increment();
        await repo.SaveAsync(counter, ct);
    }
}
```

---

## Dispatch with Middleware Pipeline

Commands and queries with validation, resilience, and observability middleware.

**Packages:**
```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Observability
dotnet add package Excalibur.Dispatch.Resilience.Polly
```

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

var builder = WebApplication.CreateBuilder(args);

// Dispatch with middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddDispatchResilience(); // Retry + circuit breaker
});

// Observability (tracing + metrics)
builder.Services.AddDispatchObservability();

// OpenTelemetry (optional -- export to your collector)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Dispatch.*"))
    .WithMetrics(metrics => metrics.AddMeter("Dispatch.*"));

var app = builder.Build();

app.MapPost("/process", async (ProcessRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new ProcessDataAction(req.Data), ct);
    return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result.ErrorMessage);
});

app.Run();

// --- Messages ---
public record ProcessRequest(string Data);
public record ProcessDataAction(string Data) : IDispatchAction;
public record DataProcessedEvent(string Data) : IDispatchEvent;

// --- Handler ---
public class ProcessDataHandler(IDispatcher dispatcher) : IActionHandler<ProcessDataAction>
{
    public async Task HandleAsync(ProcessDataAction action, CancellationToken ct)
    {
        // Your business logic -- retries and circuit breaking handled by middleware
        Console.WriteLine($"Processing: {action.Data}");
        await dispatcher.DispatchAsync(new DataProcessedEvent(action.Data), ct);
    }
}

public class DataProcessedHandler : IEventHandler<DataProcessedEvent>
{
    public Task HandleAsync(DataProcessedEvent @event, CancellationToken ct)
    {
        Console.WriteLine($"[Event] Processed: {@event.Data}");
        return Task.CompletedTask;
    }
}
```

---

## CQRS with Event Sourcing and Projections

Full Command Query Responsibility Segregation: write via aggregates, read via projections.

**Packages:**
```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
```

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=CqrsApp;Trusted_Connection=true;";

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<TodoAggregate, Guid>(id => new TodoAggregate(id));

    // Inline projection: updated synchronously during SaveAsync()
    es.AddProjection<TodoView>(p => p
        .Inline()
        .When<TodoCreated>((view, e) =>
        {
            view.Id = e.TodoId;
            view.Title = e.Title;
            view.IsComplete = false;
        })
        .When<TodoCompleted>((view, _) => { view.IsComplete = true; }));
});

builder.Services.AddSqlServerEventSourcing(opts => opts.ConnectionString = connectionString);
builder.Services.AddSqlServerProjectionStore<TodoView>(opts =>
{
    opts.ConnectionString = connectionString;
});

var app = builder.Build();

// Write side: commands go through aggregate
app.MapPost("/todos", async (CreateTodoRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<CreateTodoAction, Guid>(
        new CreateTodoAction(req.Title), ct);
    return result.IsSuccess
        ? Results.Created($"/todos/{result.ReturnValue}", new { Id = result.ReturnValue })
        : Results.BadRequest(result.ErrorMessage);
});

app.MapPost("/todos/{id:guid}/complete", async (
    Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new CompleteTodoAction(id), ct);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
});

// Read side: queries read from projection store (denormalized, fast)
app.MapGet("/todos/{id:guid}", async (
    Guid id, IProjectionStore<TodoView> store, CancellationToken ct) =>
{
    var todo = await store.GetByIdAsync(id.ToString(), ct);
    return todo is null ? Results.NotFound() : Results.Ok(todo);
});

app.MapGet("/todos", async (IProjectionStore<TodoView> store, CancellationToken ct) =>
{
    var todos = await store.QueryAsync(null, null, ct);
    return Results.Ok(todos);
});

app.Run();

// --- Messages ---
public record CreateTodoRequest(string Title);
public record CreateTodoAction(string Title) : IDispatchAction<Guid>;
public record CompleteTodoAction(Guid TodoId) : IDispatchAction;
public record TodoCreated(Guid TodoId, string Title) : DomainEvent
{
    public override string AggregateId => TodoId.ToString();
}
public record TodoCompleted(Guid TodoId) : DomainEvent
{
    public override string AggregateId => TodoId.ToString();
}

// --- Read Model (Projection) ---
public class TodoView
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

// --- Aggregate ---
public class TodoAggregate : AggregateRoot<Guid>
{
    public string Title { get; private set; } = string.Empty;
    public bool IsComplete { get; private set; }

    public TodoAggregate() { }
    public TodoAggregate(Guid id) : base(id) { }

    public void Create(string title) => RaiseEvent(new TodoCreated(Id, title));
    public void Complete()
    {
        if (IsComplete) throw new InvalidOperationException("Already complete.");
        RaiseEvent(new TodoCompleted(Id));
    }

    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        TodoCreated e => ApplyCreated(e),
        TodoCompleted => ApplyCompleted(),
        _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
    };

    private bool ApplyCreated(TodoCreated e) { Title = e.Title; return true; }
    private bool ApplyCompleted() { IsComplete = true; return true; }
}

// --- Command Handlers ---
public class CreateTodoHandler(
    IEventSourcedRepository<TodoAggregate, Guid> repo) : IActionHandler<CreateTodoAction, Guid>
{
    public async Task<Guid> HandleAsync(CreateTodoAction action, CancellationToken ct)
    {
        var todo = new TodoAggregate(Guid.NewGuid());
        todo.Create(action.Title);
        await repo.SaveAsync(todo, ct);
        return todo.Id;
    }
}

public class CompleteTodoHandler(
    IEventSourcedRepository<TodoAggregate, Guid> repo) : IActionHandler<CompleteTodoAction>
{
    public async Task HandleAsync(CompleteTodoAction action, CancellationToken ct)
    {
        var todo = await repo.GetByIdAsync(action.TodoId, ct)
            ?? throw new InvalidOperationException($"Todo {action.TodoId} not found.");
        todo.Complete();
        await repo.SaveAsync(todo, ct);
    }
}
```

## What's Next

- [Getting Started](./index.md) -- Step-by-step tutorial walkthrough
- [Dispatch Only](./dispatch-only.md) -- Full reference for dispatch-only scenarios
- [Order System Tutorial](./order-system-tutorial.md) -- Build a complete order management API
- [Event-Sourced Tutorial](./event-sourcing-tutorial.md) -- Add event sourcing to the order system
