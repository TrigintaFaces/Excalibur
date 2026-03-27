---
sidebar_position: 2
title: Message Context
description: Learn how to use the message context to pass metadata through the Dispatch pipeline.
---

# Message Context

The message context carries metadata through the pipeline, enabling correlation, multi-tenancy, and custom data propagation.

:::info Why Should I Care?
Without message context, every handler needs to manually thread correlation IDs, tenant info, and user identity through method parameters. Message context solves this by providing a **per-request ambient bag** that all middleware and handlers can read and write -- similar to `HttpContext` in ASP.NET Core but for any dispatch pipeline (HTTP, queue, background job).
:::

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [actions and handlers](./actions-and-handlers.md) and [pipeline concepts](../pipeline/index.md)

## What is Message Context?

Every message dispatch has an associated `IMessageContext` that flows through the pipeline. It contains:

- **MessageId** - Unique identifier for this message
- **CorrelationId** - Unique identifier linking related operations
- **CausationId** - ID of the operation that caused this one
- **Items** - Key-value store for transport-specific and custom data
- **Features** - Typed feature collection for cross-cutting concerns (identity, routing, processing state, etc.)

## Accessing Context

### In Handlers

Use `IMessageContextAccessor` to access the current context:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;

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

        // Core property (direct on interface)
        var correlationId = context.CorrelationId;

        // Cross-cutting concerns via feature extensions
        var tenantId = context.GetTenantId();
        var userId = context.GetUserId();

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

## Core Properties vs Features

`IMessageContext` has 8 core properties plus a typed `Features` dictionary for cross-cutting concerns:

### Core Properties (Direct on Interface)

| Property | Type | Purpose |
|----------|------|---------|
| `MessageId` | `string?` | Unique message identifier |
| `CorrelationId` | `string?` | Links related operations |
| `CausationId` | `string?` | Links to causing message |
| `Message` | `IDispatchMessage?` | The message payload |
| `Result` | `object?` | Handler result |
| `RequestServices` | `IServiceProvider` | Scoped DI container |
| `Items` | `IDictionary<string, object>` | Transport metadata and custom data |
| `Features` | `IDictionary<Type, object>` | Typed feature collection |

### Feature Interfaces

Cross-cutting concerns are accessed via typed feature interfaces:

| Feature | Properties | Use Case |
|---------|-----------|----------|
| `IMessageIdentityFeature` | UserId, TenantId, SessionId, WorkflowId, ExternalId, TraceParent | Identity and multi-tenancy |
| `IMessageProcessingFeature` | ProcessingAttempts, IsRetry, FirstAttemptTime, DeliveryCount | Retry and delivery tracking |
| `IMessageValidationFeature` | ValidationPassed, ValidationTimestamp | Validation state |
| `IMessageTimeoutFeature` | TimeoutExceeded, TimeoutElapsed | Timeout tracking |
| `IMessageRateLimitFeature` | RateLimitExceeded, RateLimitRetryAfter | Rate limiting |
| `IMessageRoutingFeature` | RoutingDecision, PartitionKey, Source | Routing decisions |
| `IMessageTransactionFeature` | Transaction, TransactionId | Transaction context |

```csharp
using Excalibur.Dispatch.Abstractions.Features;

// Read via convenience extensions
var tenantId = context.GetTenantId();
var isRetry = context.GetIsRetry();
var partitionKey = context.GetPartitionKey();

// Write via feature instance
var identity = context.GetOrCreateIdentityFeature();
identity.TenantId = "acme-corp";
identity.UserId = currentUser.Id;

// Processing state
var processing = context.GetOrCreateProcessingFeature();
processing.ProcessingAttempts++;
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
        // Extract tenant from HTTP header and set on identity feature
        var tenantId = _httpContextAccessor.HttpContext?.Request
            .Headers["X-Tenant-Id"]
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId))
        {
            var identity = context.GetOrCreateIdentityFeature();
            identity.TenantId = tenantId;
        }

        return await nextDelegate(message, context, cancellationToken);
    }
}
```

### Reading Items

```csharp
// Get with type (for custom/transport-specific data)
var customValue = context.GetItem<string>("MyCustomKey");

// Check existence and get value
if (context.ContainsItem("MyCustomKey"))
{
    var value = context.GetItem<string>("MyCustomKey");
    // Use value
}

// Get all items
var items = context.Items;
```

### Type-Safe Context Extensions

Define extension methods for type-safe access to custom data:

```csharp
public static class OrderContextExtensions
{
    private const string OrderIdKey = "Order.OrderId";

    public static string? GetOrderId(this IMessageContext context)
        => context.GetItem<string>(OrderIdKey);

    public static void SetOrderId(this IMessageContext context, string orderId)
        => context.SetItem(OrderIdKey, orderId);
}

// Usage
context.SetOrderId("ORD-12345");
var orderId = context.GetOrderId();
```

:::info Tenant ID as a DI Service
In addition to the identity feature, `ITenantId` is available as a scoped DI service registered via `TryAddTenantId()`. When no tenant is explicitly configured, the framework uses `TenantDefaults.DefaultTenantId` (`"Default"`) automatically — single-tenant applications work without any tenant setup.

```csharp
// Single-tenant: ITenantId.Value is "Default" automatically
services.AddExcaliburA3()
    .UsePostgres();

// Multi-tenant: resolve per-request before A3
services.TryAddTenantId(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return httpContext?.Request.Headers["X-Tenant-ID"].FirstOrDefault()
        ?? TenantDefaults.DefaultTenantId;
});
services.AddExcaliburA3()
    .UsePostgres();
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

## Child Context Creation

Create child contexts for cascading messages:

```csharp
var childContext = context.CreateChildContext();
```

**Propagated automatically:**
- `CorrelationId` - Maintains distributed tracing
- `IMessageIdentityFeature` (TenantId, UserId, SessionId, WorkflowId, ExternalId, TraceParent)
- `IMessageRoutingFeature.Source` - Preserves origin tracking

**Set automatically:**
- `CausationId` - Set to parent's `MessageId`
- `MessageId` - New unique identifier

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

- Use feature extensions for cross-cutting concerns (`GetTenantId()`, `GetUserId()`)
- Use type-safe extension methods for custom Items data
- Propagate context in child dispatches
- Include correlation in all logging
- Preserve context across service boundaries

### Don't

- Store large objects in context items
- Use context for data that should be in the action
- Assume Items exist without checking
- Modify context in handlers (do it in middleware)
- Use Items for data that has a feature interface (identity, routing, processing state)

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
