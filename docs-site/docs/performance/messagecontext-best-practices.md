---
sidebar_position: 2
title: MessageContext Best Practices
description: Performance best practices for IMessageContext usage
---

# MessageContext Best Practices

This guide covers performance optimization patterns for `IMessageContext` usage in high-throughput scenarios.

## Before You Start

- **.NET 10.0**
- Familiarity with [message context](../core-concepts/message-context.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Core Properties vs Features vs Items

`IMessageContext` has three levels of data access with different performance characteristics:

| Access Method | Measured | Use Case |
|---------------|----------|----------|
| Core property (e.g., `CorrelationId`) | ~0.2 ns | 8 core properties on the interface |
| Feature extension (e.g., `GetTenantId()`) | 5.7-7.7 ns | Cross-cutting concerns via typed features |
| Items dictionary (`GetItem<T>()`) | 4.3-4.9 ns | Transport-specific and user-defined data |

Measured 2026-09-04, `MessageContextBenchmarks`, BenchmarkDotNet 0.15.8 in-process on .NET 10.0.11.
All three allocate nothing. The core-property figure is under one CPU cycle and should be read as
effectively free rather than as a precise number.

Note that a feature extension is **not** cheaper than the Items dictionary -- a raw `Items["key"]`
read measures 5.7-6.9 ns and typed `GetItem<T>()` measures 4.3-4.9 ns, so the three non-property
options are all in the same band. Prefer features for type safety and intent, not for speed.

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
using Excalibur.Dispatch.Features;

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

    return MessageResult.Failed("Validation failed");
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

Extrapolated from the measurements above to 100K messages/second. These are arithmetic, not a
throughput benchmark -- no throughput measurement exists for this path:

| Pattern | CPU cost per second at 100K msg/s |
|---------|-----------------------------------|
| 1 core property read | under 0.03 ms |
| 1 feature extension read | ~0.7 ms |
| 1 Items dictionary read | ~0.6 ms |
| 10 core property reads | ~0.2 ms |
| 10 feature extension reads | ~7 ms |
| 10 Items dictionary reads | ~6 ms |

The practical reading: at 100K messages/second, ten feature reads per message cost single-digit
milliseconds of CPU per second. Caching a feature reference when you take several properties off
the same feature is still worth doing, but the saving is smaller than this page used to imply.

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
