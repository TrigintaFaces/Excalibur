---
sidebar_position: 3
title: Migrating off IDirectLocalDispatcher
description: IDirectLocalDispatcher and DispatchLocalAsync have been removed. Call IDispatcher.DispatchAsync instead.
---

# Migrating off `IDirectLocalDispatcher`

`IDirectLocalDispatcher` and both `DispatchLocalAsync` overloads have been removed from the public
surface. If your build broke on either name, this page is the whole migration.

## What to call instead

`IDispatcher.DispatchAsync`. No cast, no opt-in, no configuration:

```csharp
// Before
if (dispatcher is IDirectLocalDispatcher direct)
{
    var response = await direct.DispatchLocalAsync<GetOrderQuery, OrderDto>(query, ct);
}

// After
var result = await dispatcher.DispatchAsync<GetOrderQuery, OrderDto>(query, ct);
var response = result.ReturnValue;
```

One behavioral difference to handle: `DispatchAsync` reports failure through the returned
`IMessageResult`, where `DispatchLocalAsync` threw. A failure you previously caught is now a result
you inspect -- see [Results and Errors](../core-concepts/results-and-errors.md).

## Why it was removed

`DispatchLocalAsync` bypassed middleware unconditionally. Its eligibility check asked whether the
handler invoker was the concrete one and whether local retries were off -- it never asked whether
you had registered any middleware. So validation, authorization and telemetry that you had
configured were silently skipped, with no diagnostic, on an injectable public API. Gating the method
on middleware would only have made every call fall through to the standard path, so the method is
gone instead.

## Are you giving up performance?

The existing internal fast path remains available through `DispatchAsync`: when a local action has
no middleware applicable to its type, dispatch takes the short path automatically. Removing the
explicit API does not itself establish a latency improvement. The older methods performed different
work, including context creation and result reporting, so their timings do not establish a matched
comparison of the supported API before and after removal.

## Tuning the fast path

The fast path is still configurable through `DispatchOptions.CrossCutting.Performance`. It is used
only when the message can stay local; otherwise Dispatch falls back to the full pipeline
automatically. Common fallback triggers:

- middleware applies to that message type
- a non-local routing decision
- a local retry mode that requires the richer execution path
- operations that require full context-bound semantics

### Context initialization on the fast path

| Context field/state | Behavior |
|---|---|
| `context.Message` | Set |
| `context.CorrelationId` (when correlation enabled and missing) | Generated |
| `context.CausationId` (when missing and correlation present) | Set from correlation |
| `context.MessageType` (when missing) | Populated |

Existing values are preserved -- Dispatch only fills missing values. Correlation is governed by
`Features.EnableCorrelation`; when it is on, the fast path initializes it like any other path.

### `EmitDirectLocalResultMetadata`

- `false` (default): minimal success result shape on the local fast path.
- `true`: include full result metadata on local fast-path success.

See [Core Configuration](../core-concepts/configuration.md) for the registration snippet and the
`appsettings.json` equivalent.

## See Also

- [Performance Overview](./index.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
- [Results and Errors](../core-concepts/results-and-errors.md)
