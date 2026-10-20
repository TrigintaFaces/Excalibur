---
sidebar_position: 3
title: Global Exception Handling
description: Standardized RFC 9457 problem details responses with environment-aware behavior, validation errors, and OpenAPI support.
---

# Global Exception Handling

`Excalibur.Hosting.Web` provides a global exception handler that converts unhandled exceptions into [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457.html) JSON responses. It integrates with ASP.NET Core's `IExceptionHandler`, supports FluentValidation errors, provides environment-aware stack traces, and includes an OpenAPI schema for Swagger documentation. RFC 9457 supersedes RFC 7807 and permits extension members such as `traceId` and validation `errors`.

## Before You Start

- **.NET 10.0**
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

`AddGlobalExceptionHandler()` registers `GlobalExceptionHandler` as an `IExceptionHandler`, configures ASP.NET Core's `ProblemDetailsService`, and applies the optional `ProblemDetailsOptions` callback.

Dispatch-only applications can use `Excalibur.Dispatch.Hosting.AspNetCore` and
`builder.Services.AddDispatchExceptionHandler()` with `app.UseExceptionHandler()`. Both entry points
share status mapping and response construction. In Development, the Excalibur entry point preserves
demystified stack traces; the lean Dispatch entry point uses standard exception diagnostic text.

`UseExcaliburWebHost()` adds the exception handler middleware along with tenant ID, correlation ID, ETag, and client address middleware.

The handler also covers ordinary exceptions from controllers, minimal endpoints, services and middleware downstream of `UseExceptionHandler()`, without requiring a Dispatch pipeline. Place that middleware before the components it should protect. It cannot rewrite an already-started response, catch exceptions upstream of it, or handle background-worker exceptions. Returned error statuses without a thrown exception use separate response handling.

## How It Works

When an unhandled exception reaches the middleware pipeline:

1. **Status code extraction** — The handler reads the status code from the exception (see [Custom Exception Status Codes](#custom-exception-status-codes)), or falls back to `500 Internal Server Error`.
2. **Exception ID** — If the exception is an `ApiException`, its `Id` is used. Otherwise, a new UUID v7 is generated for correlation.
3. **Trace ID** — Pulled from `Activity.Current?.Id` (OpenTelemetry) or `HttpContext.TraceIdentifier`.
4. **Problem Details construction** — A `ProblemDetails` object is built with localized MDN type URI, HTTP reason phrase, instance URN, and trace/exception IDs.
5. **Environment-aware response** — Outside Development, all responses omit internal exception messages, stack traces and error codes. Public field validation errors remain available. The public reason title remains stable across environments, and the mapped HTTP status and body status remain identical. In Development, diagnostic details are included.
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
  "errorCode": "1042",
  "stack": "OrderNotFoundException at OrderService.GetByIdAsync(...) ..."
}
```

### Production Environment (sanitized)

```json
{
  "type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "The request could not be completed.",
  "instance": "urn:my-api:error:019478a2-3b4c-7d8e-9f01-234567890abc",
  "traceId": "00-abcdef1234567890-abcdef12-01"
}
```

The HTTP reason phrase remains the title in every environment. Internal exception messages are still
suppressed outside Development. A mapped 503 remains 503 in both the HTTP response and JSON body.

### Validation Errors (FluentValidation)

Dispatch validation exceptions map to 400 and include an `errors` dictionary. The Excalibur handler
also extracts errors from directly thrown FluentValidation exceptions. A FluentValidation exception
needs explicit status metadata (for example `exception.Data["StatusCode"] = 400`) to map to 400;
otherwise it retains the default 500 status. Here is a production validation response with status 400:

```json
{
  "type": "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "urn:my-api:error:...",
  "traceId": "...",
  "errors": {
    "Email": ["'Email' must not be empty."],
    "Name": ["'Name' must be between 2 and 100 characters."]
  }
}
```

## Custom Exception Status Codes

The handler uses `ExceptionExtensions.GetStatusCode()`, which resolves the status code in this order:

1. `DispatchException.DispatchStatusCode`, when it is set
2. `ApiException.StatusCode` — `DispatchException` and the data-access exceptions derive from
   `ApiException`, so `ResourceNotFoundException` answers 404 and `ConflictException` and
   `ConcurrencyException` answer 409
3. An `int` stored under the `"StatusCode"` key of the exception's `Data` dictionary
4. The inner exception, recursively

It does **not** look for a property named `StatusCode` on other exception types. An exception you
define yourself answers 500 unless it derives from `ApiException` or carries a `Data` entry:

```csharp
using Excalibur.Dispatch;

// Option 1: derive from ApiException (recommended)
public sealed class OrderNotFoundException : ApiException
{
    public OrderNotFoundException(string orderId)
        : base(404, $"Order '{orderId}' was not found.", null) { }
}

// Option 2: Data dictionary, for an exception type you cannot change
var ex = new InvalidOperationException("Not allowed");
ex.Data["StatusCode"] = 403;
throw ex;
```

Similarly, `GetErrorCode()` returns the error code as text. A `DispatchException` answers with its own `ErrorCode`; any other exception answers with an `"ErrorCode"` entry in its `Data` dictionary, then its inner exceptions (every inner exception of an `AggregateException`). A numeric code is rendered as text.

## Extending the handler

`GlobalExceptionHandler` exposes two `protected virtual` members. Everything else about the response —
status mapping, the `type` link, `traceId`, the production/development split — is fixed, so these are the
only two seams, and they are the ones the Excalibur handler itself uses.

| member | called | override it to |
|---|---|---|
| `GetValidationErrors(Exception)` | always | publish per-property validation errors from a library the base does not recognize |
| `FormatDiagnosticStack(Exception)` | Development only | change how the exception is rendered into the `stack` member |

```csharp
public sealed class MyExceptionHandler : GlobalExceptionHandler
{
    public MyExceptionHandler(IHostEnvironment environment,
        IOptions<ProblemDetailsOptions> options, ILogger<GlobalExceptionHandler> logger)
        : base(environment, options, logger) { }

    protected override IDictionary<string, string[]>? GetValidationErrors(Exception exception)
    {
        // Let the framework's own validation exception win, then add your library.
        return base.GetValidationErrors(exception)
            ?? (exception is MyLibraryValidationException v ? v.ToDictionary() : null);
    }
}

// Replace the registered handler with your subclass.
builder.Services.AddDispatchExceptionHandler();
builder.Services.AddSingleton<IExceptionHandler, MyExceptionHandler>();
```

:::danger `GetValidationErrors` output reaches production responses unsanitized
The `errors` member is written whenever this method returns anything, in **every** environment — it is
not inside the development-only branch that guards `detail`, `errorCode` and `stack`. The handler copies
the dictionary before serializing it but does not filter it, so whatever you return is what the caller
receives. Return only what an unauthenticated caller may see: property names and validation messages, not
internal identifiers, SQL, or values echoed back from the record being validated.

`FormatDiagnosticStack` is the opposite case and is safe by construction: it is called only while the
environment is Development, so an override there cannot widen what a production response discloses.
:::

**The shipped Excalibur handler is the worked example of both.** `Excalibur.Hosting.Web`'s
`GlobalExceptionHandler` overrides `GetValidationErrors` to fall back to FluentValidation — calling `base`
first, then grouping `ValidationFailure` by `PropertyName` — which is where the behavior described under
[Validation Errors](#validation-errors-fluentvalidation) comes from. It overrides `FormatDiagnosticStack`
to return a demystified stack trace, which is why the two entry points differ in development output while
sharing one status mapping.

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

This registers the standard fields plus `traceId`, optional development-only string `errorCode` and
`stack`, and optional `errors` containing arrays of messages by field name. Exception identity is
encoded in `instance`, rather than a separate `exceptionId` response member. Custom problem-details
writers may add extensions.

Regenerate clients produced from the older schema: it incorrectly described an integer `errorCode`,
a `validationErrors` array and a separate `exceptionId`, and the Swagger mapping omitted `type`.
The corrected schema describes the payload the handler actually emits.

### OpenAPI YAML Schema

A reusable OpenAPI 3.1 schema is embedded in the `Excalibur.Hosting.Web` assembly. Access it programmatically:

```csharp
// Get the YAML content as a string
string yaml = ProblemDetailsOpenApi.GetYaml();

// Or get a stream for efficient processing
using var stream = ProblemDetailsOpenApi.GetYamlStream();
```

## Structured Logging

The handler logs every exception using source-generated `[LoggerMessage]` with structured fields:

```
[APPL]==> /api/orders/abc-123
[APPL]<== ERROR 404: Order 'abc-123' was not found.
```

Log entries include `TraceId` and `ExceptionId` in the logging scope, enabling correlation across distributed traces and support tickets. The event ID is `162510`.

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

A complete example:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExcalibur(excalibur => excalibur
    .ScanAssemblies(typeof(PingCommand).Assembly));
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();
app.UseExcaliburWebHost();

app.MapPost("/ping", async (PingCommand command, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync<PingCommand, string>(command, ct);
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
