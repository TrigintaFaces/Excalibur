---
sidebar_position: 6
title: Results to HTTP Responses
description: Convert IMessageResult to an ASP.NET Core IResult in Minimal API endpoints using terminal operators
---

# Results to HTTP Responses

This guide shows how to turn an `IMessageResult` into an HTTP response at the edge of an ASP.NET Core Minimal API, without breaking the functional composition chain. For the result type itself -- how to create, inspect, and compose results -- see [Results and Errors](../core-concepts/results-and-errors.md).

## Before You Start

- **.NET 10.0**
- Install the hosting package -- the terminal operators live here, not in `Excalibur.Dispatch`:
  ```bash
  dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
  ```
- Familiarity with [results and errors](../core-concepts/results-and-errors.md)

## Minimal API Integration

When using ASP.NET Core Minimal APIs, Dispatch provides terminal operators that convert `IMessageResult` directly to `IResult` — enabling fluent ROP chains without breaking the pipeline.

### Terminal Operators

| Method | Input | HTTP Response |
|--------|-------|---------------|
| `ToApiResult()` | `Task<IMessageResult>` | 202 Accepted |
| `ToApiResult<T>()` | `Task<IMessageResult<T>>` | 200 OK with value |
| `ToNoContentResult()` | `IMessageResult` or `Task<IMessageResult>` | 204 No Content |
| `ToCreatedResult<T>(location)` | `IMessageResult<T>` or `Task<IMessageResult<T>>` | 201 Created with location |
| `ToCreatedResult<T>(factory)` | `Task<IMessageResult<T>>` | 201 Created with dynamic location |
| `ToHttpResult()` | `IMessageResult` or `IMessageResult<T>` | 202 Accepted or 200 OK (sync) |

All methods share a ProblemDetails-aware failure mapping:

| Failure Condition | HTTP Response |
|-------------------|---------------|
| Authorization failed | 403 Forbidden |
| Validation failed | 400 Bad Request |
| ProblemDetails with Status | RFC 7807 Problem with that status |
| Other failure | 500 Internal Server Error |

### Complete Example: Minimal API Endpoints

```csharp ignore
using Excalibur.Dispatch.Hosting.AspNetCore;

var app = builder.Build();

// Query — 200 OK with value
app.MapGet("/orders/{id}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderAction, Order>(new GetOrderAction(id), ct)
        .Map(order => new OrderResponse(order))
        .ToApiResult());

// Command — 201 Created with location
app.MapPost("/orders", (CreateOrderRequest request, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<CreateOrderAction, Order>(
            new CreateOrderAction(request.CustomerId, request.Items), ct)
        .ToCreatedResult(order => $"/orders/{order.Id}"));

// Command — 204 No Content
app.MapDelete("/orders/{id}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync(new DeleteOrderAction(id), ct)
        .ToNoContentResult());
```

Notice the difference from an MVC controller, which awaits the dispatch and then branches: the async terminal operators (`ToApiResult`, `ToCreatedResult`, `ToNoContentResult`) accept `Task<IMessageResult>` directly, so there's no need to `await` and break the chain.

### Custom Response Mapping with Match

`ToApiResult()` uses convention-based status codes. When you need full control over the HTTP response for each outcome, use `.Match()` instead — it's the same ROP operator from [Functional Composition](../core-concepts/results-and-errors.md#functional-composition), and it works as a terminal operator in Minimal APIs because `Match<TIn, IResult>` returns `Task<IResult>` directly:

```csharp
// Full control — .Match() replaces .ToApiResult()
app.MapGet("/orders/{id}", (Guid id, IDispatcher dispatcher, ILogger<Program> logger, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderAction, Order>(new GetOrderAction(id), ct)
        .Map(order => new OrderDto(order))
        .Tap(dto => logger.LogInformation("Retrieved order {OrderId}", dto.Id))
        .Match(
            onSuccess: dto => Results.Ok(dto),
            onFailure: problem => problem?.Status switch
            {
                404 => Results.NotFound(),
                400 => Results.BadRequest(problem),
                _ => Results.Problem(detail: problem?.Detail)
            }));
```

**When to use which terminal:**

| Terminal | Use when |
|----------|----------|
| `.ToApiResult()` | Convention-based mapping is sufficient (auto 200/202/403/400/500) |
| `.ToCreatedResult()` | You need 201 Created with a location URI |
| `.ToNoContentResult()` | You need 204 No Content for commands |
| `.Match()` | You need custom per-status response logic (e.g., 404 → `NotFound()`) |

`.Tap()` works anywhere in the chain as a pass-through side effect — use it for logging, metrics, or notifications without affecting the result.

### Sync vs Async Usage

The sync variants (`ToHttpResult`, `ToNoContentResult`, `ToCreatedResult`) work on `IMessageResult` directly — useful when you already have an awaited result:

```csharp
app.MapGet("/orders/{id}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderAction, Order>(
        new GetOrderAction(id), ct);

    return result.ToHttpResult(); // Sync — returns IResult
});
```

The async variants accept `Task<IMessageResult>` and enable full pipeline chaining:

```csharp
app.MapGet("/orders/{id}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderAction, Order>(new GetOrderAction(id), ct)
        .Map(order => new OrderResponse(order))
        .ToApiResult()); // Async — returns Task<IResult>
```

## See Also

- [Results and Errors](../core-concepts/results-and-errors.md) — The result type, composition, and when to use exceptions instead
- [Result API Reference](../reference/result-api.md) — Signatures for every ROP extension and exception type
- [Exception Mapping](exception-mapping.md) — Convert thrown exceptions to Problem Details before they reach the edge
- [Minimal API Bridge](../deployment/minimal-api-bridge.md) — Endpoint routing extensions that dispatch without hand-written endpoints
