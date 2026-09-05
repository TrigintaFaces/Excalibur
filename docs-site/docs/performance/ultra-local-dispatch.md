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

No -- you gain a little. The ultra-local fast path is an internal optimization, not an API, and it
is unchanged: when a local action has no middleware applicable to its type, `DispatchAsync` takes
the short path automatically, exactly as it did before. The explicit method was never the faster of
the two. It measured *slower* than the standard call while doing strictly less work -- it created no
message context and returned no `IMessageResult` -- so removing it makes the public call faster,
not slower.

## Tuning the fast path

The fast path is still configurable through `DispatchOptions.CrossCutting.Performance`. It is used
only when the message can stay local; otherwise Dispatch falls back to the full pipeline
automatically. Common fallback triggers:

- middleware applies to that message type
- a non-local routing decision
- a local retry mode that requires the richer execution path
- operations that require full context-bound semantics

### `DirectLocalContextInitialization`

- `Lean` (default): minimizes initialization work on the local fast path.
- `Full`: forces eager message-type initialization on the local fast path.

| Context field/state | `Lean` | `Full` |
|---|---|---|
| `context.Message` | Set | Set |
| `context.CorrelationId` (when correlation enabled and missing) | Generated | Generated |
| `context.CausationId` (when missing and correlation present) | Set from correlation | Set from correlation |
| `context.MessageType` (when missing) | Not populated | Populated |

Existing values are preserved in both profiles -- Dispatch only fills missing values.

### `EmitDirectLocalResultMetadata`

- `false` (default): minimal success result shape on the local fast path.
- `true`: include full result metadata on local fast-path success.

See [Core Configuration](../core-concepts/configuration.md) for the registration snippet and the
`appsettings.json` equivalent.

## See Also

- [Performance Overview](./index.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
- [Results and Errors](../core-concepts/results-and-errors.md)
