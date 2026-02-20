---
sidebar_position: 2
title: Message Context
description: Learn how to use the message context to pass metadata through the Dispatch pipeline.
---

# Message Context

The message context carries metadata through the pipeline, enabling correlation, multi-tenancy, and custom data propagation.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [actions and handlers](./actions-and-handlers.md) and [pipeline concepts](../pipeline/index.md)

## What is Message Context?

Every message dispatch has an associated `IMessageContext` that flows through the pipeline. It contains:

- **CorrelationId** - Unique identifier linking related operations
- **CausationId** - ID of the operation that caused this one
- **Items** - Key-value store for transport-specific and custom data
- **Properties** - Alias for Items, for middleware compatibility

## Accessing Context

### In Handlers

Use `IMessageContextAccessor` to access the current context:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IMessageContextAccessor _contextAccessor;

    public CreateOrderHandler(IMessageContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var context = _contextAccessor.MessageContext;

        // Access correlation ID
        var correlationId = context.CorrelationId;

        // Access custom items
        var tenantId = context.GetItem<string>("TenantId");
        var userId = context.GetItem<Guid>("UserId");

        // Use in logging
        _logger.LogInformation(
            "Processing order for tenant {TenantId} with correlation {CorrelationId}",
            tenantId,
            correlationId);
    }
}
```

### In Middleware

Middleware receives context directly:

```csharp
public class LoggingMiddleware : IDispatchMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var correlationId = context.CorrelationId;

        _logger.LogInformation(
            "[{CorrelationId}] Handling {MessageType}",
            correlationId,
            message.GetType().Name);

        return await nextDelegate(message, context, cancellationToken);
    }
}
```

## Context Items

Use context items to pass custom data through the pipeline.

### Setting Items

Items are typically set early in the pipeline (middleware):

```csharp
public class TenantMiddleware : IDispatchMiddleware
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantMiddleware(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        // Extract tenant from HTTP header
        var tenantId = _httpContextAccessor.HttpContext?.Request
            .Headers["X-Tenant-Id"]
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId))
        {
            context.SetItem("TenantId", tenantId);
        }

        return await nextDelegate(message, context, cancellationToken);
    }
}
```

### Reading Items

```csharp
// Get with type
var tenantId = context.GetItem<string>("TenantId");
var userId = context.GetItem<Guid>("UserId");

// Check existence and get value
if (context.ContainsItem("TenantId"))
{
    var tenant = context.GetItem<string>("TenantId");
    // Use tenant
}

// Or use GetItem with default value
var tenantWithDefault = context.GetItem<string>("TenantId", "default-tenant");

// Get all items
var items = context.Items;
```

### Type-Safe Context Extensions

Define extension methods for type-safe access:

```csharp
public static class MessageContextExtensions
{
    private const string TenantIdKey = "TenantId";
    private const string UserIdKey = "UserId";

    public static string? GetTenantId(this IMessageContext context)
        => context.GetItem<string>(TenantIdKey);

    public static void SetTenantId(this IMessageContext context, string tenantId)
        => context.SetItem(TenantIdKey, tenantId);

    public static Guid? GetUserId(this IMessageContext context)
        => context.GetItem<Guid?>(UserIdKey);

    public static void SetUserId(this IMessageContext context, Guid userId)
        => context.SetItem(UserIdKey, userId);
}

// Usage
var tenantId = context.GetTenantId();
context.SetUserId(userId);
```

:::info Tenant ID as a DI Service
In addition to context items, `ITenantId` is available as a scoped DI service registered via `TryAddTenantId()`. When no tenant is explicitly configured, the framework uses `TenantDefaults.DefaultTenantId` (`"Default"`) automatically — single-tenant applications work without any tenant setup.

```csharp
// Single-tenant: ITenantId.Value is "Default" automatically
services.AddExcaliburA3Services(SupportedDatabase.Postgres);

// Multi-tenant: resolve per-request before A3
services.TryAddTenantId(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return httpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()
        ?? TenantDefaults.DefaultTenantId;
});
services.AddExcaliburA3Services(SupportedDatabase.Postgres);
```

The `TenantIdentityMiddleware` in the pipeline sets the context-level tenant from HTTP headers, message properties, or a configured default. The DI-registered `ITenantId` is used by A3 authorization, grants, and audit logging.
:::

## Correlation ID

The correlation ID links all related operations together.

### Automatic Generation

When dispatching without an existing context, a new `CorrelationId` is generated:

```csharp
// Top-level dispatch - new CorrelationId generated
await dispatcher.DispatchAsync(action, cancellationToken);
```

### Preserving Correlation

When dispatching from within a handler, use `DispatchChildAsync` to preserve correlation:

```csharp
public class OrderHandler : IActionHandler<CreateOrderAction>
{
    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        // Child dispatch - same CorrelationId
        await _dispatcher.DispatchChildAsync(new NotifyAction(), ct);
    }
}
```

### External Correlation

Propagate correlation from HTTP headers:

```csharp
public class CorrelationMiddleware
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IMessageContextAccessor contextAccessor)
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"]
            .FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Set correlation ID on current context
        var context = contextAccessor.MessageContext;
        context.CorrelationId = correlationId;

        // Add to response headers
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        await _next(httpContext);
    }
}
```

## Causation Chain

Track what caused what with causation IDs:

```csharp
// Original action
// CorrelationId: abc123
// CausationId: (none - this is the root)

// Child action dispatched from handler
// CorrelationId: abc123 (same)
// CausationId: original-message-id
```

This creates an audit trail:
- All related actions share the same `CorrelationId`
- Each action knows what caused it via `CausationId`

## Context in Distributed Systems

When messages cross service boundaries:

### Outgoing Messages

```csharp
public class OutboxMiddleware : IDispatchMiddleware
{
    private readonly IOutboxStore _outboxStore;

    public OutboxMiddleware(IOutboxStore outboxStore)
    {
        _outboxStore = outboxStore;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        var result = await nextDelegate(message, context, cancellationToken);

        // Include context in outbox message
        var outboxMessage = new OutboxMessage
        {
            CorrelationId = context.CorrelationId,
            Metadata = context.Items
        };

        await _outboxStore.SaveAsync(outboxMessage, cancellationToken);

        return result;
    }
}
```

### Incoming Messages

```csharp
public class MessageConsumer
{
    public async Task HandleMessageAsync(TransportMessage message)
    {
        // Restore context from incoming message
        var context = new MessageContext
        {
            CorrelationId = message.CorrelationId,
            CausationId = message.MessageId
        };

        foreach (var item in message.Metadata)
        {
            context.SetItem(item.Key, item.Value);
        }

        // Dispatch with restored context
        await _dispatcher.DispatchAsync(action, context, cancellationToken);
    }
}
```

## Best Practices

### Do

- Use type-safe extension methods for common items
- Propagate context in child dispatches
- Include correlation in all logging
- Preserve context across service boundaries

### Don't

- Store large objects in context items
- Use context for data that should be in the action
- Assume context items exist without checking
- Modify context in handlers (do it in behaviors)

## What's Next

- [Results and Errors](results-and-errors.md) - Handle success and failure
- [Configuration](configuration.md) - Configure Dispatch options
- [Pipeline](../pipeline/index.md) - Add behaviors that interact with context
- [Middleware](../middleware/index.md) - Cross-cutting concerns using context

## See Also

- [Actions and Handlers](./actions-and-handlers.md) — Define the messages that flow through the context pipeline
- [MessageContext Design](../architecture/messagecontext-design.md) — Architectural decisions behind the MessageContext design
- [MessageContext Items Usage](../architecture/messagecontext-items-usage.md) — Patterns and conventions for using context items across the framework
- [Custom Middleware](../middleware/custom.md) — Build middleware that reads and writes context items
