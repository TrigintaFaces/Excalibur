---
sidebar_position: 1
title: Dispatch Only
description: Use Excalibur.Dispatch as a standalone MediatR replacement — just two packages, no event sourcing or sagas required.
---

# Dispatch Only

Need a modern MediatR replacement and nothing else? This page is for you. One package, zero infrastructure dependencies, production-ready in minutes.

:::tip New to Dispatch?
If you haven't used Dispatch before, start with the [Getting Started tutorial](./index.md) for a step-by-step walkthrough. This page is a focused reference for teams that want messaging only.
:::

## What You Need

```bash
dotnet add package Excalibur.Dispatch
```

That's it — one package. `Excalibur.Dispatch.Abstractions` is included as a transitive dependency. No event sourcing, no sagas, no compliance packages, no `AddExcalibur()`.

## What You Don't Need

You can safely ignore all of these unless your requirements grow:

- `Excalibur.Domain` — aggregates and entities
- `Excalibur.EventSourcing.*` — event stores and snapshots
- `Excalibur.Saga.*` — long-running workflows
- `Excalibur.Hosting` — unified builder (`AddExcalibur()`)
- `Excalibur.Dispatch.Compliance.*` — regulatory compliance
- Any transport package — messages dispatch in-process by default

## Complete Working Example

This is a production-ready ASP.NET Core application with commands, queries, events, and pipeline middleware:

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch — this is the ONLY registration you need
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

// Command (no return value)
app.MapPost("/orders", async (CreateOrderRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new CreateOrderAction(req.CustomerId, req.Items), ct);
    return result.IsSuccess ? Results.Created() : Results.BadRequest(result.ErrorMessage);
});

// Query (with return value)
app.MapGet("/orders/{id}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderQuery, OrderDto>(new GetOrderQuery(id), ct);
    return result.IsSuccess ? Results.Ok(result.ReturnValue) : Results.NotFound();
});

app.Run();

// --- Actions ---
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;
public record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderDto>;
public record OrderCreatedEvent(Guid OrderId) : IDispatchEvent;

// --- Handlers ---
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    public Task HandleAsync(CreateOrderAction action, CancellationToken cancellationToken)
    {
        // Your business logic here
        return Task.CompletedTask;
    }
}

public class GetOrderHandler : IActionHandler<GetOrderQuery, OrderDto>
{
    public Task<OrderDto> HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
    {
        return Task.FromResult(new OrderDto(action.OrderId, "sample-customer", new List<string> { "item-1" }));
    }
}

public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        // React to the event — send email, update read model, etc.
        return Task.CompletedTask;
    }
}

// --- DTOs ---
public record CreateOrderRequest(string CustomerId, List<string> Items);
public record OrderDto(Guid Id, string CustomerId, List<string> Items);
```

All messages dispatch **in-process** — no broker, no database, no infrastructure needed.

## Optional Enhancements

Each enhancement below is independent. Add only what you need, when you need it.

### Validation — when you need input validation

Add `Excalibur.Dispatch.Validation.FluentValidation` to validate actions before they reach handlers:

```bash
dotnet add package Excalibur.Dispatch.Validation.FluentValidation
```

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddDispatchValidation().WithFluentValidation();
});
```

### Resilience — when you need retry and circuit breaker

Add `Excalibur.Dispatch.Resilience.Polly` for automatic retries and circuit breaking:

```bash
dotnet add package Excalibur.Dispatch.Resilience.Polly
```

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.AddDispatchResilience();
});
```

### Observability — when you need OpenTelemetry tracing and metrics

Add `Excalibur.Dispatch.Observability` for automatic tracing, metrics, and logging:

```bash
dotnet add package Excalibur.Dispatch.Observability
```

```csharp
builder.Services.AddDispatchObservability();
```

### Transport — when you need to send messages to a broker

Add a transport package when you need to route messages to RabbitMQ, Kafka, Azure Service Bus, or others. Your handlers don't change — only registration code changes:

```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

See [Choosing a Transport](../transports/choosing-a-transport.md) for broker comparison and setup.

## When to Consider More

| If you need... | Add... | What it gives you |
|----------------|--------|-------------------|
| Domain aggregates and entities | `Excalibur.Domain` | `AggregateRoot`, value objects, domain events |
| Event replay and audit trail | `Excalibur.EventSourcing` | Event stores, snapshots, projections |
| Multi-step workflows | `Excalibur.Saga` | Saga orchestration, compensation |
| Unified builder | `Excalibur.Hosting` | `AddExcalibur()` entry point for all subsystems |

If none of these apply, **you're done** — `Excalibur.Dispatch` is a complete, production-ready messaging framework on its own.
