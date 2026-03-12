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

## Core Properties vs Features vs Items

`IMessageContext` has three levels of data access with different performance characteristics:

| Access Method | Latency | Use Case |
|---------------|---------|----------|
| Core property (e.g., `CorrelationId`) | 1-3ns | 8 core properties on the interface |
| Feature extension (e.g., `GetTenantId()`) | 10-30ns | Cross-cutting concerns via typed features |
| Items dictionary (`GetItem<T>()`) | 30-60ns | Transport-specific and user-defined data |

### Core Properties (Direct on Interface)

These 8 properties are on the interface and have the fastest access:

```csharp
context.MessageId           // string?
context.CorrelationId       // string?
context.CausationId         // string?
context.Message             // IDispatchMessage?
context.Result              // object?
context.RequestServices     // IServiceProvider
context.Items               // IDictionary<string, object>
context.Features            // IDictionary<Type, object>
```

### Feature Extensions (Cross-Cutting Concerns)

Cross-cutting concerns are accessed via typed feature interfaces. Cache the feature reference when accessing multiple properties:

```csharp
using Excalibur.Dispatch.Abstractions.Features;

// Good - cache the feature reference
var processing = context.GetOrCreateProcessingFeature();
processing.ProcessingAttempts++;
processing.IsRetry = processing.ProcessingAttempts > 1;
processing.FirstAttemptTime ??= DateTimeOffset.UtcNow;

// Good - single read via convenience extension
var isRetry = context.GetIsRetry();

// Avoid - repeated feature lookups in a loop
for (int i = 0; i < items.Count; i++)
{
    // Each call does a dictionary lookup
    Process(items[i], context.GetTenantId()); // Avoid in tight loops
}

// Better - cache outside the loop
var tenantId = context.GetTenantId();
for (int i = 0; i < items.Count; i++)
{
    Process(items[i], tenantId);
}
```

### Items Dictionary (Transport-Specific)

Use Items for transport-specific and user-defined data only:

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

## Middleware Patterns

### Short-Circuit Early

Check conditions before expensive operations:

```csharp
public async ValueTask<IMessageResult> InvokeAsync(
    IDispatchMessage message, IMessageContext context,
    DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
{
    // Fast check first via feature extension
    if (context.GetValidationPassed())
    {
        return await nextDelegate(message, context, cancellationToken);
    }

    // Expensive validation only if needed
    var isValid = await ValidateAsync(message);
    var validation = context.GetOrCreateValidationFeature();
    validation.ValidationPassed = isValid;
    validation.ValidationTimestamp = DateTimeOffset.UtcNow;

    if (isValid)
    {
        return await nextDelegate(message, context, cancellationToken);
    }

    return MessageResult.Empty;
}
```

### Cache Feature References

When a middleware reads and writes multiple feature properties, get the feature once:

```csharp
// Good - single feature lookup
var processing = context.GetOrCreateProcessingFeature();
processing.ProcessingAttempts++;
processing.IsRetry = processing.ProcessingAttempts > 1;
processing.FirstAttemptTime ??= DateTimeOffset.UtcNow;
```

## Context Propagation

### Automatic Propagation

`CreateChildContext()` automatically propagates cross-cutting concerns:

```csharp
var childContext = context.CreateChildContext();
// Propagated: CorrelationId, IMessageIdentityFeature, IMessageRoutingFeature.Source
// Set: CausationId = parent.MessageId, new MessageId
// NOT copied: Items, processing/validation/timeout features
```

### What's NOT Propagated

Feature state resets for each child context:
- Processing feature starts fresh (attempts = 0, isRetry = false)
- Validation feature starts fresh (passed = false)
- Transaction feature starts null

This is intentional -- each message tracks its own processing state.

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
| 1 core property read | ~0.2ms |
| 1 feature extension read | ~2ms |
| 1 Items dictionary read | ~3.5ms |
| 10 core property reads | ~2ms |
| 10 feature extension reads | ~20ms |
| 10 Items dictionary reads | ~35ms |

For middleware accessing 5-10 properties per message, caching feature references saves significant CPU at high throughput.

## Summary

1. **Use core properties** for MessageId, CorrelationId, CausationId (direct on interface)
2. **Use feature extensions** for cross-cutting concerns (identity, processing, validation, etc.)
3. **Cache feature references** when accessing multiple properties from the same feature
4. **Use Items** for transport-specific and user-defined data only
5. **Prefix Items keys** to avoid collisions
6. **Short-circuit early** to avoid unnecessary work
7. **Don't store large objects** in Items

## See Also

- [Message Context](../core-concepts/message-context.md) - Core concepts and API reference for IMessageContext
- [Auto-Freeze](./auto-freeze.md) - Automatic FrozenDictionary cache optimization on startup
- [Performance Overview](./index.md) - Full performance guide and optimization strategies

## Next Steps

- [MessageContext Design](../architecture/messagecontext-design.md) - Architecture details
- [MessageContext Items Usage](../architecture/messagecontext-items-usage.md) - Items dictionary guidance
