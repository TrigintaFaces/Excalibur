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
- `Excalibur.Compliance.*` — regulatory compliance
- Any transport package — messages dispatch in-process by default

## Complete Working Example

This is a production-ready ASP.NET Core application with commands, queries, events, and pipeline middleware:

```csharp title="Program.cs"
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch — auto-discovers handlers from the entry assembly.
// Handlers in another project? Name it: AddDispatch(typeof(MyHandler).Assembly)
builder.Services.AddDispatch();

var app = builder.Build();

// Command (no return value)
app.MapPost("/orders", async (CreateOrderRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new CreateOrderAction(req.CustomerId, req.Items), ct);
    return result.IsSuccess
        ? Results.Created()
        : Results.Problem(result.ErrorMessage, statusCode: result.ProblemDetails?.Status);
});

// Query (with return value)
app.MapGet("/orders/{id}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderQuery, OrderDto>(new GetOrderQuery(id), ct);
    return result.IsSuccess
        ? Results.Ok(result.ReturnValue)
        : Results.Problem(result.ErrorMessage, statusCode: result.ProblemDetails?.Status);
});

app.Run();

// --- Actions ---
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;
public record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderDto>;
[MessageName("Contoso.Orders.OrderCreated")]
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

:::caution Do not map a failed result to 400

A failed `IMessageResult` means your handler **ran** and reported a failure — not, by default, that
the caller sent a bad request. Hard-coding `Results.BadRequest(...)` reports a server-side fault to
the caller as their own mistake, so they retry a request that can never succeed, or abandon one that
would have.

`result.ProblemDetails.Status` carries the status the framework determined for that failure. With
the pipeline's exception mapping configured (`UseExceptionMapping()`), a validation failure arrives
as **400** and an authorization failure as **403**; a handler that threw with nothing mapping it
arrives as **500**. When no status was determined, `ProblemDetails` is `null` and `Results.Problem`
falls back to 500 — the safe direction, and never the caller's fault by accident.

The `Excalibur.Dispatch.Hosting.AspNetCore` package does the whole mapping in one call:
`return result.ToHttpResult();` — it honours an authorization failure (403) and a validation failure
(400) first, then `ProblemDetails.Status`, then falls back to 500. `ToNoContentResult()`,
`ToCreatedResult(location)` and the `Task`-chaining `ToApiResult()` cover the other success shapes.
:::

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
    dispatch.UseValidation().WithFluentValidation();
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
    dispatch.UseResilience();
});
```

### Observability — when you need OpenTelemetry tracing and metrics

Add `Excalibur.Dispatch.Observability` for automatic tracing, metrics, and logging:

```bash
dotnet add package Excalibur.Dispatch.Observability
```

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseObservability();
});
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
