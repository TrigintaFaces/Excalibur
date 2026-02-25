---
sidebar_position: 3
title: Global Exception Handling
description: Standardized RFC 7807 problem details responses with environment-aware behavior, validation errors, and OpenAPI support.
---

# Global Exception Handling

`Excalibur.Hosting.Web` provides a production-ready global exception handler that converts unhandled exceptions into standardized [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807) JSON responses. It integrates with ASP.NET Core's `IExceptionHandler`, supports FluentValidation errors, provides environment-aware stack traces, and includes an OpenAPI schema for Swagger documentation.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An ASP.NET Core web application
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [error handling patterns](../patterns/error-handling.md)

## Installation

```bash
dotnet add package Excalibur.Hosting.Web
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register the global exception handler
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

// Enable the exception handler middleware
app.UseExcaliburWebHost();
// Or, if you only want exception handling without the full middleware stack:
// app.UseExceptionHandler();

app.Run();
```

`AddGlobalExceptionHandler()` registers `GlobalExceptionHandler` as an `IExceptionHandler`, configures ASP.NET Core's `ProblemDetailsService`, and binds `ProblemDetailsOptions` from configuration.

`UseExcaliburWebHost()` adds the exception handler middleware along with tenant ID, correlation ID, ETag, and client address middleware.

## How It Works

When an unhandled exception reaches the middleware pipeline:

1. **Status code extraction** — The handler inspects the exception for a `StatusCode` property (via reflection with caching) or falls back to `500 Internal Server Error`.
2. **Exception ID** — If the exception is an `ApiException`, its `Id` is used. Otherwise, a new UUID v7 is generated for correlation.
3. **Trace ID** — Pulled from `Activity.Current?.Id` (OpenTelemetry) or `HttpContext.TraceIdentifier`.
4. **Problem Details construction** — A `ProblemDetails` object is built with localized MDN type URI, HTTP reason phrase, instance URN, and trace/exception IDs.
5. **Environment-aware response** — In production (non-Development), 5xx errors are sanitized: generic title, no stack trace, no error codes. In Development, full details are included.
6. **Response writing** — Attempts `IProblemDetailsService` first (for customization hooks), falls back to `WriteAsJsonAsync` with `application/problem+json` content type.

## Response Format

### Development Environment (full details)

```json
{
  "type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Order with ID 'abc-123' was not found.",
  "instance": "urn:my-api:error:019478a2-3b4c-7d8e-9f01-234567890abc",
  "traceId": "00-abcdef1234567890-abcdef12-01",
  "errorCode": 1042,
  "stack": "OrderNotFoundException at OrderService.GetByIdAsync(...) ..."
}
```

### Production Environment (sanitized)

```json
{
  "type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500",
  "title": "An unhandled exception has occurred.",
  "status": 500,
  "detail": "Oops, something went wrong",
  "instance": "urn:my-api:error:019478a2-3b4c-7d8e-9f01-234567890abc",
  "traceId": "00-abcdef1234567890-abcdef12-01"
}
```

For non-5xx errors (e.g., 400, 404, 409), the full detail message is shown in all environments.

### Validation Errors (FluentValidation)

When a `FluentValidation.ValidationException` is thrown, the response includes a `validationErrors` array:

```json
{
  "type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed: ...",
  "instance": "urn:my-api:error:...",
  "traceId": "...",
  "validationErrors": [
    { "propertyName": "Email", "errorMessage": "'Email' must not be empty." },
    { "propertyName": "Name", "errorMessage": "'Name' must be between 2 and 100 characters." }
  ]
}
```

## Custom Exception Status Codes

The handler uses `ExceptionExtensions.GetStatusCode()` which searches for status codes in this order:

1. A `StatusCode` property on the exception type
2. A `StatusCode` entry in the exception's `Data` dictionary
3. Inner exceptions (recursively)

```csharp
// Option 1: Property-based (recommended)
public class OrderNotFoundException : Exception
{
    public int StatusCode => 404;

    public OrderNotFoundException(string orderId)
        : base($"Order '{orderId}' was not found.") { }
}

// Option 2: Data dictionary
var ex = new InvalidOperationException("Not allowed");
ex.Data["StatusCode"] = 403;
throw ex;
```

Similarly, `GetErrorCode()` extracts application-specific error codes using the same lookup strategy (`ErrorCode` property or `Data` dictionary entry).

## Configuration

### ProblemDetailsOptions

```csharp
builder.Services.AddGlobalExceptionHandler(options =>
{
    // Customize the base URL for RFC 7807 "type" URIs
    // Default: "https://developer.mozilla.org"
    options.StatusTypeBaseUrl = "https://developer.mozilla.org";
});
```

The `type` field in problem details responses links to localized MDN HTTP status documentation. The locale is automatically selected based on `CultureInfo.CurrentUICulture`, with 30+ supported locales including `en-US`, `fr`, `de`, `ja`, `zh-CN`, `pt-BR`, and more. If the current culture isn't supported, it falls back to `en-US`.

Example type URIs:
- English: `https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404`
- French: `https://developer.mozilla.org/fr/docs/Web/HTTP/Status/404`
- Japanese: `https://developer.mozilla.org/ja/docs/Web/HTTP/Status/404`

## OpenAPI / Swagger Integration

The package includes an OpenAPI schema and a Swagger extension for documenting problem details responses.

### Swagger Schema Registration

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.AddProblemDetailsSchema();
});
```

This registers a standardized `ProblemDetails` schema in your OpenAPI specification with all extension fields (`traceId`, `exceptionId`, `errorCode`, `stack`, `validationErrors`).

### OpenAPI YAML Schema

A reusable OpenAPI 3.1 schema file is included at `openapi/problem-details.openapi.yaml` in the package output. Access it programmatically:

```csharp
var yamlPath = ProblemDetailsOpenApi.GetYamlPath();
```

## Structured Logging

The handler logs every exception using source-generated `[LoggerMessage]` with structured fields:

```
[APPL]==> /api/orders/abc-123
[APPL]<== ERROR 404: Order 'abc-123' was not found.
```

Log entries include `TraceId` and `ExceptionId` in the logging scope, enabling correlation across distributed traces and support tickets. The event ID is `ExcaliburHostingEventId.GlobalExceptionOccurred`.

## Full Web Host Middleware Stack

`UseExcaliburWebHost()` registers the complete Excalibur middleware pipeline:

| Middleware | Purpose |
|-----------|---------|
| `UseExceptionHandler()` | Global exception → Problem Details |
| `UseTenantIdMiddleware()` | Extracts tenant ID from request headers |
| `UseCorrelationIdMiddleware()` | Extracts/generates correlation ID |
| `UseETagMiddleware()` | Manages incoming/outgoing ETags for concurrency |
| `UseClientAddressMiddleware()` | Captures client remote IP address |

You can use individual middleware methods if you don't need the full stack.

## Sample Usage

See `samples/09-advanced/WebWorkerSample/WebHost/Program.cs` for a complete example:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExcaliburWebServices(builder.Configuration, typeof(PingCommand).Assembly);
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();
app.UseExcaliburWebHost();

app.MapPost("/ping", async (PingCommand command, IDispatcher dispatcher) =>
{
    var context = DispatchContextInitializer.CreateDefaultContext();
    var result = await dispatcher.DispatchAsync<PingCommand, string>(command, context);
    return Results.Ok(result.ReturnValue);
});

await app.RunAsync();
```

## Package Reference

| Type | Namespace | Purpose |
|------|-----------|---------|
| `GlobalExceptionHandler` | `Excalibur.Hosting.Web.Diagnostics` | `IExceptionHandler` implementation |
| `ProblemDetailsOptions` | `Excalibur.Hosting.Web.Diagnostics` | Configuration for type URIs and locales |
| `SwaggerGenOptionsExtensions` | `Excalibur.Hosting.Web.Diagnostics` | `.AddProblemDetailsSchema()` extension |
| `ProblemDetailsOpenApi` | `Excalibur.Hosting.Web.Diagnostics` | OpenAPI YAML schema path helper |
| `AddGlobalExceptionHandler()` | `Microsoft.Extensions.DependencyInjection` | DI registration extension |
| `UseExcaliburWebHost()` | `Microsoft.AspNetCore.Builder` | Middleware pipeline extension |
| `ExceptionExtensions` | `Excalibur.Dispatch.Extensions` | `.GetStatusCode()`, `.GetErrorCode()` helpers |

## See Also

- [Error Handling Patterns](../patterns/error-handling.md) — Error handling strategies and best practices
- [Dead Letter Queues](../patterns/dead-letter.md) — Handle failed messages with dead letter patterns
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Host Excalibur applications in ASP.NET Core
