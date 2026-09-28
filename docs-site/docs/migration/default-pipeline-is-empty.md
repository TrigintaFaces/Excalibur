---
sidebar_position: 7
title: The Default Pipeline No Longer Seats Middleware
description: AddDispatch() used to add four middleware to every host. It now adds none, and you name the ones you want.
---

# The default pipeline no longer seats middleware

**What changed.** `AddDispatch()` used to seat four middleware on the `default` pipeline profile:
`TenantIdentityMiddleware`, `TimeoutMiddleware`, `MetricsLoggingMiddleware` and
`OutboxStagingMiddleware`. The `default` profile is now **empty**. A host that configures nothing
dispatches and does nothing else.

All four are still **registered** by `AddDispatch()`, so naming any of them on a pipeline resolves
without extra work. What changed is what runs by default, not what is available.

## Do I need to do anything?

**Almost certainly not, and the one population that does is narrow.** Check yourself against this
before reading further:

| if your host… | then |
|---|---|
| calls `UseOutbox()` | **unaffected.** `UseOutbox()` seats the staging stage itself and always did |
| has **no** `IOutboxStore` registered | **unaffected.** A handler writing to the outbox already threw before this change — the middleware skipped itself when no store was present and the writer threw on the missing context |
| **registers an `IOutboxStore` but never calls `UseOutbox()`** | **this is the one. Action required — see below** |
| relies on the default **timeout** | name `TimeoutMiddleware`, or handle timeouts yourself |
| relies on **tenant identity** being resolved for every message | name `TenantIdentityMiddleware` |
| relies on the default **metrics logging** | name `MetricsLoggingMiddleware` |

### If you register a store but never call `UseOutbox()`

You are affected because we told you that was enough. The previous version's own error text said:

> *"Outbox staging runs on the default pipeline automatically once a store is present, so no further
> pipeline configuration is needed."*

That is no longer true, and it is the contract this change breaks. Add the stage:

```csharp
services.AddDispatch(builder => builder.UseOutbox());
```

`UseOutbox()` also registers a startup check that fails fast if the store is missing, which is why it
is the call to reach for. If you want only the stage, `pipeline.Use<OutboxStagingMiddleware>()` does
that and nothing else.

**You will not lose a write silently.** A handler that writes to the outbox on a pipeline with no
staging stage throws at dispatch, and the message names both halves — the `IOutboxStore` registration
and the stage.

## Restoring the previous behaviour

```csharp
services.AddDispatch(builder => builder.ConfigurePipeline("Default", pipeline => pipeline
    .ForMessageKinds(MessageKinds.All)
    .Use<TenantIdentityMiddleware>()
    .Use<TimeoutMiddleware>()
    .Use<MetricsLoggingMiddleware>()
    .Use<OutboxStagingMiddleware>()));
```

Or name only the ones you actually use — which is the point of the change.

## Why

Two reasons, and the second is the one that mattered.

**Cost.** Measured on the warm path, against the same benchmark used for the published comparison
figures, with three of your own middleware in the pipeline:

| default profile | per dispatch |
|---|---|
| four middleware (before) | 1,836 ns / 1,960 B |
| one semantically-inert middleware | 419 ns / 688 B |
| empty (now) | **71.5 ns / 240 B** |

Those four cost roughly **25x the entire remaining cost of a dispatch**, on hosts that had asked for
none of them. Note the middle row: even a single stage that does nothing when its capability is absent
— outbox staging without a store — costs **+346 ns and +448 B per dispatch**. Semantically inert is not
free, and that measurement is what removed the last candidate for a default.

**Behaviour.** Cost alone would not have justified a breaking change. Two of the four changed what your
code does. `TimeoutMiddleware` can cancel a handler on a deadline you never chose. `TenantIdentityMiddleware`
resolves a tenant identity for a host that never asked to be multi-tenant. Behaviour that arrives
because you called a registration method is behaviour you cannot find by reading your own code, and
cannot remove without knowing it is there.

This brings the framework in line with how ASP.NET Core treats the same problem: a registration call
seats infrastructure, and behaviour is something you add. `AddAuthorization()` does not authorize
anything; you add the middleware.

## When

Pre-RC. The API is frozen at `10.0.0-rc.1`, so this lands before then; after `10.0.0` a change of this
shape is reserved for a new major line.
