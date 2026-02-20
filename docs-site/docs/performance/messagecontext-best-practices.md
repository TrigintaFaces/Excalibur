---
sidebar_position: 2
title: MessageContext Best Practices
description: Performance best practices for IMessageContext usage
---

# MessageContext Best Practices

This guide covers performance optimization patterns for `IMessageContext` usage in high-throughput scenarios.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [message context](../core-concepts/message-context.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Property Access Performance

### Use Direct Properties for Hot-Path Data

Direct properties on `IMessageContext` provide ~10x better performance than the Items dictionary:

| Access Method | Latency | Use Case |
|---------------|---------|----------|
| Direct property | 1-3ns | Core framework data, frequently accessed |
| Items dictionary | 30-50ns | Transport-specific, user-defined data |
| `GetItem<T>()` | 40-60ns | Same as Items + type cast |

**DO:**

```csharp
// Fast - direct property access
context.ProcessingAttempts++;
context.ValidationPassed = true;
var isRetry = context.IsRetry;
```

**DON'T:**

```csharp
// Slow - dictionary access with boxing
context.Items["ProcessingAttempts"] = attempts;
var passed = (bool)context.Items["ValidationPassed"];
```

### Available Direct Properties

Use these properties instead of Items for common patterns:

```csharp
// Retry tracking
context.ProcessingAttempts   // int
context.FirstAttemptTime     // DateTimeOffset?
context.IsRetry              // bool

// Validation
context.ValidationPassed     // bool
context.ValidationTimestamp  // DateTimeOffset?

// Transactions
context.Transaction          // object?
context.TransactionId        // string?

// Timeout
context.TimeoutExceeded      // bool
context.TimeoutElapsed       // TimeSpan?

// Rate limiting
context.RateLimitExceeded    // bool
context.RateLimitRetryAfter  // TimeSpan?
```

## Items Dictionary Patterns

### When to Use Items

Items dictionary is appropriate for:

1. **Transport-specific metadata** - Data that only exists for certain transports
2. **User-defined headers** - HTTP headers, AMQP headers with unpredictable keys
3. **Infrequently accessed data** - Setup once, read once

### Key Naming Conventions

Use consistent prefixes to avoid collisions:

```csharp
// Transport-specific (prefix with transport name)
context.Items["rabbitmq.exchange"] = exchange;
context.Items["rabbitmq.deliveryTag"] = deliveryTag;

// Internal framework (prefix with "Dispatch:")
context.Items["Dispatch:OriginalResult"] = result;

// CloudEvents (prefix with "ce.")
context.Items["ce.type"] = eventType;

// Custom application (prefix with app name)
context.Items["MyApp.CustomData"] = data;
```

### Avoid Boxing for Value Types

If you must use Items with value types, consider caching:

```csharp
// Slow - boxes int on every access
context.Items["counter"] = count;
var c = (int)context.Items["counter"];

// Better - use direct property if available
context.ProcessingAttempts = count;
var c = context.ProcessingAttempts;
```

## Middleware Patterns

### Read Once, Use Multiple Times

Don't repeatedly access the same property in a loop:

```csharp
// Good - read once
var tenantId = context.TenantId;
foreach (var item in items)
{
    Process(item, tenantId);
}

// Bad - repeated property access (though minimal cost for direct properties)
foreach (var item in items)
{
    Process(item, context.TenantId);
}
```

### Short-Circuit Early

Check conditions before expensive operations:

```csharp
public async ValueTask<IMessageResult> InvokeAsync(
    IDispatchMessage message, IMessageContext context,
    DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
{
    // Fast check first
    if (context.ValidationPassed)
    {
        return await nextDelegate(message, context, cancellationToken);
    }

    // Expensive validation only if needed
    var isValid = await ValidateAsync(message);
    context.ValidationPassed = isValid;
    context.ValidationTimestamp = DateTimeOffset.UtcNow;

    if (isValid)
    {
        return await nextDelegate(message, context, cancellationToken);
    }

    return MessageResult.Empty;
}
```

### Use Null-Coalescing Assignment

```csharp
// Good - only sets if null
context.FirstAttemptTime ??= DateTimeOffset.UtcNow;

// Unnecessary - always writes
if (context.FirstAttemptTime == null)
{
    context.FirstAttemptTime = DateTimeOffset.UtcNow;
}
```

## Context Propagation

### Automatic Propagation

`CreateChildContext()` automatically propagates cross-cutting concerns:

```csharp
var childContext = context.CreateChildContext();
// Propagated: CorrelationId, TenantId, UserId, SessionId,
//             WorkflowId, TraceParent, Source
// Set: CausationId = parent.MessageId
// NOT copied: Items, hot-path properties
```

### What's NOT Propagated

Hot-path properties reset for each context:
- `ProcessingAttempts` starts at 0
- `ValidationPassed` starts at false
- `Transaction` starts at null

This is intentional - each message tracks its own processing state.

## Memory Considerations

### Don't Store Large Objects in Items

Items dictionary values are stored by reference, but large object graphs:
- Increase memory pressure
- Slow down context pooling (clearing takes longer)
- May prevent objects from being collected

```csharp
// Bad - storing large objects
context.Items["FullResponse"] = largeResponseObject;

// Better - store reference/ID and fetch when needed
context.Items["ResponseId"] = responseId;
```

### Clear Temporary Data

If you add temporary Items, consider removing them:

```csharp
try
{
    context.Items["temp.data"] = tempData;
    await ProcessAsync(context);
}
finally
{
    context.Items.Remove("temp.data");
}
```

## Benchmarks

Typical performance at scale (100K messages/second):

| Pattern | CPU Cost per Second |
|---------|---------------------|
| 1 direct property read | ~0.2ms |
| 1 Items dictionary read | ~3.5ms |
| 10 direct property reads | ~2ms |
| 10 Items dictionary reads | ~35ms |

For middleware accessing 5-10 properties per message, direct properties save ~30ms of CPU time per second at 100K msg/s throughput.

## Summary

1. **Use direct properties** for ProcessingAttempts, ValidationPassed, IsRetry, etc.
2. **Use Items** for transport-specific and user-defined data only
3. **Prefix Items keys** to avoid collisions
4. **Read properties once** if used multiple times
5. **Short-circuit early** to avoid unnecessary work
6. **Don't store large objects** in Items

## See Also

- [Message Context](../core-concepts/message-context.md) - Core concepts and API reference for IMessageContext
- [Auto-Freeze](./auto-freeze.md) - Automatic FrozenDictionary cache optimization on startup
- [Performance Overview](./index.md) - Full performance guide and optimization strategies

## Next Steps

- [MessageContext Design](../architecture/messagecontext-design.md) - Architecture details
- [MessageContext Items Usage](../architecture/messagecontext-items-usage.md) - Items dictionary guidance
