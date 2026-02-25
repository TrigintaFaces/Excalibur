---
sidebar_position: 3
title: Ultra-Local Dispatch
description: Use the ultra-local ValueTask path for lowest local command/query overhead.
---

# Ultra-Local Dispatch

Ultra-local dispatch is the lowest-overhead local command/query execution path in Dispatch. It is designed for in-process scenarios where you want MediatR-style local handling with minimal allocations.

## Before You Start

- .NET 8.0+ (or .NET 9/10)
- `Excalibur.Dispatch` and `Excalibur.Dispatch.Abstractions`
- Familiarity with [Actions and Handlers](../core-concepts/actions-and-handlers.md) and [Message Context](../core-concepts/message-context.md)

## Two Ways It Is Used

### 1. Automatic through `DispatchAsync(...)`

For top-level local actions, `DispatcherContextExtensions` will use the ultra-local path automatically when eligible.

```csharp
var result = await dispatcher.DispatchAsync(new CreateOrderAction(...), ct);
```

### 2. Explicit through `IDirectLocalDispatcher`

Dispatch also exposes a direct ValueTask API:

```csharp
public interface IDirectLocalDispatcher
{
    ValueTask DispatchLocalAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IDispatchAction;

    ValueTask<TResponse?> DispatchLocalAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IDispatchAction<TResponse>;
}
```

If you need explicit control, resolve `IDispatcher` and cast:

```csharp
var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
if (dispatcher is IDirectLocalDispatcher direct)
{
    var response = await direct.DispatchLocalAsync<GetOrderQuery, OrderDto>(query, ct);
}
```

## Eligibility and Fallback

Dispatch uses ultra-local/direct-local only when the message can stay on the local fast path. If not, it falls back to the full dispatch pipeline automatically.

Common fallback triggers:

- middleware/pipeline requirements for that message
- non-local routing decision
- local retry mode that requires richer execution path
- operations that require full context-bound semantics

## Configuration

Configure through `DispatchOptions.CrossCutting.Performance`:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.ConfigureOptions<DispatchOptions>(options =>
    {
        options.CrossCutting.Performance.DirectLocalContextInitialization =
            DirectLocalContextInitializationProfile.Lean; // default

        options.CrossCutting.Performance.EmitDirectLocalResultMetadata = false; // default
    });
});
```

### `DirectLocalContextInitialization`

- `Lean` (default): minimizes initialization work on direct-local path.
- `Full`: forces eager message-type initialization on direct-local path.

Exact behavior on direct-local path:

| Context field/state | `Lean` | `Full` |
|---|---|---|
| `context.Message` | Set | Set |
| `context.CorrelationId` (when correlation enabled and missing) | Generated | Generated |
| `context.CausationId` (when missing and correlation present) | Set from correlation | Set from correlation |
| `context.MessageType` (when missing) | Not populated | Populated |

Notes:

- Existing values are preserved in both profiles (Dispatch only fills missing values).
- If `context.MessageType` is already set before dispatch, both profiles keep it.
- Direct-local initialization is for local hot paths. Transport-binding metadata is part of richer routed paths.

### `EmitDirectLocalResultMetadata`

- `false` (default): minimal success result shape on direct-local path.
- `true`: include full result metadata on direct-local success.

`appsettings.json` example:

```json
{
  "Dispatch": {
    "CrossCutting": {
      "Performance": {
        "DirectLocalContextInitialization": "Lean",
        "EmitDirectLocalResultMetadata": false
      }
    }
  }
}
```

## Which Profile Should You Use?

- Use `Lean` for MediatR-style local command/query performance targets.
- Use `Full` if downstream middleware/handlers require eager `MessageType` initialization on local fast paths.

## Correlation, Causation, and Child Dispatch

Ultra-local is primarily a top-level local optimization. For nested dispatch from handlers, use `DispatchChildAsync(...)` to preserve child-message lineage (correlation/causation semantics).

## See Also

- [Performance Overview](./index.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
- [Core Configuration](../core-concepts/configuration.md)
- [Results and Errors](../core-concepts/results-and-errors.md)
