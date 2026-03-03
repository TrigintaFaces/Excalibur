---
sidebar_position: 3
title: Minimal API Hosting Bridge
description: Map HTTP endpoints directly to Dispatch messages with zero boilerplate
---

# Minimal API Hosting Bridge

The `Excalibur.Dispatch.Hosting.AspNetCore` package bridges ASP.NET Core Minimal APIs to the Dispatch pipeline. Instead of writing manual endpoint handlers that resolve `IDispatcher` and convert results, you declare a mapping from HTTP to message and the bridge handles the rest.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

## Setup

Register Dispatch on `WebApplicationBuilder`:

```csharp
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

`builder.AddDispatch()` delegates to `services.AddDispatch()` and returns the builder for chaining.

## Endpoint Routing Extensions

All methods extend `IEndpointRouteBuilder` and return `RouteHandlerBuilder` for chaining (`.WithTags()`, `.RequireAuthorization()`, `.Produces<T>()`, etc.).

### POST Endpoints

```csharp
// With request DTO → message factory (no response body → 202 Accepted)
endpoints.DispatchPostAction<TRequest, TAction>(
    "/route",
    (request, httpContext) => new MyCommand(request.Name));

// With request DTO → message factory + typed response (200 OK with body)
endpoints.DispatchPostAction<TRequest, TAction, TResponse>(
    "/route",
    (request, httpContext) => new MyCommand(request.Name));

// Direct message (request IS the message, bound via [AsParameters])
endpoints.DispatchPostAction<TAction>("/route");

// Direct message with typed response
endpoints.DispatchPostAction<TAction, TResponse>("/route");
```

### GET Endpoints

```csharp
// With request DTO → query factory + typed response
endpoints.DispatchGetAction<TRequest, TAction, TResponse>(
    "/route/{id:guid}",
    (request, httpContext) => new GetItemQuery(request.Id));

// Direct message with typed response
endpoints.DispatchGetAction<TAction, TResponse>("/route");
```

### PUT Endpoints

Same 4 overloads as POST, using `DispatchPutAction`.

### DELETE Endpoints

Same 4 overloads as POST, using `DispatchDeleteAction`.

### Event Endpoints

```csharp
// POST with request DTO → event factory
endpoints.DispatchPostEvent<TRequest, TEvent>(
    "/route",
    (request, httpContext) => new OrderShipped(request.OrderId));
```

### Type Constraints

| Parameter | Constraint |
|-----------|-----------|
| `TRequest` | `class` |
| `TMessage` | `class, IDispatchAction` or `class, IDispatchAction<TResponse>` |
| `TResponse` | `class` (use wrapper classes for value types like `Guid`) |
| `TEvent` | `class, IDispatchEvent` |

:::tip TResponse Must Be a Class
The hosting bridge requires `TResponse : class`. If your handler returns a value type like `Guid`, wrap it in a result class:

```csharp
public sealed class CreatePatientResult
{
    public required Guid PatientId { get; init; }
}

public record CreatePatientCommand(string Name)
    : IDispatchAction<CreatePatientResult>;
```
:::

## Request DTO to Message Factory

The factory lambda `Func<TRequest, HttpContext, TAction>` receives the request DTO (bound by ASP.NET Core via `[AsParameters]`) and the `HttpContext`. Use it to map API concerns to domain messages:

```csharp
group.DispatchPutAction<UpdatePatientRequest, UpdatePatientCommand>(
    "/{id:guid}",
    (request, httpContext) => new UpdatePatientCommand(
        Guid.Parse(httpContext.GetRouteValue("id")!.ToString()!),
        request.Email));
```

For GET endpoints, annotate the request DTO with binding attributes:

```csharp
public sealed class GetPatientRequest
{
    [FromRoute(Name = "id")]
    public Guid Id { get; init; }
}
```

## HTTP Response Mapping

The bridge automatically converts `IMessageResult` to HTTP responses using ProblemDetails-aware failure mapping:

| Condition | Minimal API Result | Status Code |
|-----------|-------------------|-------------|
| Authorization failed | `Results.Forbid()` | 403 |
| Validation failed | `Results.BadRequest(validationResult)` | 400 |
| ProblemDetails with Status | `Results.Problem(...)` | ProblemDetails.Status |
| Other failure | `Results.Problem("Failed to process the request")` | 500 |
| Success (no return value) | `Results.Accepted()` | 202 |
| Success (with return value) | `Results.Ok(returnValue)` | 200 |

When a handler returns a failure with `IMessageProblemDetails.Status` set (e.g., via `MessageProblemDetails.NotFound(...)` which sets Status = 404), the bridge produces an RFC 7807 Problem response with that status code, title, detail, type, and instance. This enables handlers to communicate precise HTTP semantics without coupling to ASP.NET Core.

### Custom Response Handler

Override the default mapping with a custom response handler:

```csharp
endpoints.DispatchPostAction<CreateOrderRequest, CreateOrderCommand, OrderResult>(
    "/orders",
    (request, _) => new CreateOrderCommand(request.Items),
    responseHandler: (httpContext, result) =>
        result.Succeeded
            ? Results.Created($"/orders/{result.ReturnValue!.OrderId}", result.ReturnValue)
            : Results.Problem("Order creation failed"));
```

## Fluent ROP Terminal Operators

In addition to the endpoint routing extensions above, the package provides terminal operators for converting `IMessageResult` to `IResult` at the end of a [functional composition](../core-concepts/results-and-errors.md#functional-composition) chain. These are useful when writing manual Minimal API endpoints (not using the bridge) and want fluent ROP chaining:

```csharp
using Excalibur.Dispatch.Hosting.AspNetCore;

// Query — 200 OK with value (async, chains from Task<IMessageResult<T>>)
app.MapGet("/orders/{id}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderAction, Order>(new GetOrderAction(id), ct)
        .Map(order => new OrderResponse(order))
        .ToApiResult());

// Command — 201 Created with dynamic location
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

### Available Terminal Operators

| Method | Input | HTTP Response |
|--------|-------|---------------|
| `ToApiResult()` | `Task<IMessageResult>` | 202 Accepted |
| `ToApiResult<T>()` | `Task<IMessageResult<T>>` | 200 OK with value |
| `ToNoContentResult()` | `IMessageResult` or `Task<IMessageResult>` | 204 No Content |
| `ToCreatedResult<T>(location)` | `IMessageResult<T>` or `Task<IMessageResult<T>>` | 201 Created |
| `ToCreatedResult<T>(factory)` | `Task<IMessageResult<T>>` | 201 Created (dynamic) |
| `ToHttpResult()` | `IMessageResult` or `IMessageResult<T>` | 202 or 200 (sync) |

All terminal operators use the same ProblemDetails-aware failure mapping described above.

### Custom Response Mapping with Match

When convention-based status codes aren't enough, use `.Match()` from the [ROP extensions](../core-concepts/results-and-errors.md#functional-composition) as a terminal operator instead. Since `Match<TIn, IResult>` returns `Task<IResult>`, Minimal APIs handle it natively:

```csharp
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
                409 => Results.Conflict(problem),
                _ => Results.Problem(detail: problem?.Detail)
            }));
```

Use `.ToApiResult()` for convention-based mapping, `.Match()` when you need per-status control.

## Route Groups

Compose endpoints into groups per feature:

```csharp
var api = app.MapGroup("/api");

// Each feature registers its own routes
api.MapGroup("/patients").WithTags("Patients")
    .DispatchPostAction<RegisterPatientRequest, RegisterPatientCommand, RegisterPatientResult>(
        "/", (req, _) => new RegisterPatientCommand(req.FirstName, req.LastName));

// Or use extension methods for cleaner composition
api.MapPatientsEndpoints();
api.MapAppointmentsEndpoints();
```

## HttpContext Extensions

The bridge extracts contextual data from `HttpContext` into the Dispatch `MessageContext`:

| Data | Source | Extraction |
|------|--------|------------|
| CorrelationId | `X-Correlation-Id` header, or new GUID | Always set |
| CausationId | `X-Causation-Id` header | Optional |
| TenantId | Header, route, query, claim, or subdomain | Multi-source resolution |
| UserId | `ClaimTypes.NameIdentifier` | From authenticated user |
| ETag | `If-Match` / `If-None-Match` headers | Optional |

All HTTP request headers are copied into `context.Items` for middleware access.

## Authorization Bridge

Bridge ASP.NET Core `[Authorize]` attributes into the Dispatch pipeline:

```csharp
builder.AddDispatch(dispatch =>
{
    dispatch.UseAspNetCoreAuthorization(options =>
    {
        options.RequireAuthenticatedUser = true;  // Reject unauthenticated
        options.DefaultPolicy = "MyPolicy";       // Default policy name
    });
});
```

Then use standard `[Authorize]` and `[AllowAnonymous]` on message or handler types:

```csharp
[Authorize(Roles = "Physician")]
public record CreatePrescriptionCommand(Guid PatientId, string Medication)
    : IDispatchAction<CreatePrescriptionResult>;

[AllowAnonymous]
public record GetPublicInfoQuery() : IDispatchAction<PublicInfoResult>;
```

The middleware reads `[Authorize]` from both message and handler types, evaluates policies via `IAuthorizationService`, and returns 403 Forbidden on failure.

### Options

| Property | Default | Description |
|----------|---------|-------------|
| `Enabled` | `true` | Enable/disable the middleware |
| `RequireAuthenticatedUser` | `true` | Reject unauthenticated requests |
| `DefaultPolicy` | `null` | Default authorization policy name |

## MVC Controller Support

The package also provides extensions for traditional MVC controllers:

```csharp
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        return await this.DispatchMessageAsync<CreateOrderCommand, OrderResult>(
            () => new CreateOrderCommand(request.Items), ct);
    }
}
```

Result mapping for controllers: same status code logic, but returns `IActionResult` (`Forbid()`, `BadRequest()`, `Problem()`, `Accepted()`, `Ok()`).

## See Also

- [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) -- Organize features as slices
- [Healthcare API Sample](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/12-vertical-slice-api) -- Full working example
- [ASP.NET Core Deployment](./aspnet-core.md) -- General hosting guide
- [Authorization Middleware](../middleware/authorization.md) -- Full authorization middleware docs

