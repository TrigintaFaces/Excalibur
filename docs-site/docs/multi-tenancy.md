---
sidebar_position: 40
title: Multi-Tenancy
description: Dispatch-side ambient tenancy — resolve, carry, and fail-fast enforce a per-message tenant across the dispatch pipeline.
---

# Multi-Tenancy

`Excalibur.Dispatch` provides **dispatch-side ambient tenancy**: an async-flow-local tenant identifier that
flows with a logical operation, a resolver that derives the tenant for an inbound message, and an explicit
fail-fast control (`RequireTenant`) that refuses to run an unscoped operation when a tenant is mandatory. The
tenant is exposed as a read-only ambient value through [`ITenantContext`](#the-ambient-tenant-model), so
consumers can scope their work to the current tenant without a setter to tamper with.

:::info Part of the core package
The `Tenancy` types (`ITenantContext`, `ITenantResolver`, `TenantContextOptions`, `TenantContextHolder`,
`TenantRequiredException`) ship in the core `Excalibur.Dispatch` package and its
`Excalibur.Dispatch.Abstractions` contracts. There is **no separate package to install** — the registration
helper is available as soon as you reference `Excalibur.Dispatch`.

This is the **messaging-side** tenancy contract (who a message belongs to as it flows through the pipeline).
The persistence-side counterpart — routing a tenant to its own storage shard — is documented separately under
[tenant sharding](./event-sourcing/tenant-sharding.md).
:::

## Before You Start

- **.NET 10.0**
- A dispatch host with `Excalibur.Dispatch` registered
- Familiarity with the message pipeline and `IMessageContext`

## Registration

Register the ambient tenant context and the default resolver with `AddTenantContext`. Options are validated at
startup (`ValidateOnStart`), so an invalid configuration fails fast with `OptionsValidationException`.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddTenantContext(o =>
{
    // Fail fast when no tenant can be resolved on a tenant-required path.
    o.RequireTenant = true;

    // Optional fallback applied when a message carries no tenant.
    o.DefaultTenantId = "acme";
});
```

`AddTenantContext` registers:

- [`ITenantContext`](#the-ambient-tenant-model) → `AmbientTenantContext` (singleton, read-only view over the
  ambient tenant),
- [`ITenantResolver`](#the-ambient-tenant-model) → the default resolver (singleton),
- a validated `TenantContextOptions` with its `IValidateOptions<TenantContextOptions>` and `ValidateOnStart()`.

All registrations use `TryAdd*` semantics, so you can supply your own `ITenantContext` or `ITenantResolver`
before calling `AddTenantContext` and it will not be overridden. The `configure` callback is optional — call
`services.AddTenantContext()` to register with defaults.

### `TenantContextOptions`

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `RequireTenant` | `bool` | `false` | When `true`, resolution fails fast (throws `TenantRequiredException`) on a tenant-required path where no tenant resolves and no default is configured. When `false`, an unscoped operation is allowed. |
| `DefaultTenantId` | `string?` | `null` | The explicit fallback tenant applied when a message carries no tenant. When set, it must not be empty or whitespace (enforced at startup). |

## The ambient tenant model

Tenancy has two collaborators:

- **`ITenantContext`** — a read-only ambient accessor for the current tenant. It exposes `TenantId`
  (`string?`, `null` when no tenant is resolved) and `HasTenant` (`bool`). It has **no mutator**: the ambient
  tenant is structurally immutable through this contract and can only be established by the resolving scope, so
  a consumer can read the current tenant but cannot reassign it.
- **`ITenantResolver`** — resolves the tenant identifier for an inbound message from its `IMessageContext`, via
  `ValueTask<string?> ResolveAsync(IMessageContext context, CancellationToken cancellationToken)`.

The ambient value flows through the logical call context, established with `TenantContextHolder.BeginScope`:

```csharp
using (TenantContextHolder.BeginScope("acme"))
{
    // Anywhere in this async flow:
    var tenant = tenantContext.TenantId;   // "acme"
    var scoped = tenantContext.HasTenant;  // true
}
// The previous ambient tenant is restored here (scopes nest correctly).
```

A producer or transport carries the tenant into a dispatched message by setting the
`TenantContextHolder.TenantIdItemKey` (`"excalibur.dispatch.tenant-id"`) item on the message context; the
resolver reads that item on the receiving side.

### Resolution order

The default resolver derives the tenant in a fixed order:

1. **From the message** — the `TenantContextHolder.TenantIdItemKey` item on the `IMessageContext.Items`, when
   present and a non-empty string.
2. **Configured default** — otherwise, `TenantContextOptions.DefaultTenantId`.

If neither yields a tenant, the outcome depends on `RequireTenant` (below).

## `RequireTenant`: fail-fast tenant isolation

:::warning Security isolation control
`RequireTenant` is a **security isolation guarantee**, not a convenience toggle. When `RequireTenant` is
`true` **and** no tenant resolves from the message **and** no `DefaultTenantId` is configured, resolution
throws **`TenantRequiredException`**. The pipeline refuses to proceed with an unscoped (false-isolation)
operation rather than silently running work that would cross — or escape — tenant boundaries.
:::

This mirrors the storage-side fail-fast (`TenantShardNotFoundException`): a tenant-required path that cannot
determine its tenant fails loudly instead of degrading to an unscoped operation. `TenantRequiredException`
derives from `InvalidOperationException` and carries a descriptive message.

Behaviour summary for the "no tenant on the message" case:

| `RequireTenant` | `DefaultTenantId` | Result |
|-----------------|-------------------|--------|
| `false` | unset | Resolves to `null` (unscoped operation allowed). |
| `false` | set | Resolves to the default tenant. |
| `true` | set | Resolves to the default tenant. |
| `true` | unset | Throws **`TenantRequiredException`** — fail fast. |

Leaving `DefaultTenantId` unset while `RequireTenant` is `true` is a valid, strict configuration: it requires
every operation to supply its own tenant, and any that does not is rejected at resolve time.

## First-class persistence isolation: `AddMultiTenancy`

Where `AddTenantContext` scopes the **message**, `AddMultiTenancy` scopes the **storage**. A single
`AddMultiTenancy(...)` call selects one tenant-isolation strategy for your persistence stores and wires it,
failing fast at composition time if the selected strategy cannot be satisfied:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Excalibur.MultiTenancy; // TenantIsolationStrategy

// Row-discriminator: one shared store per store type, every row carries a tenant column.
// Register your persistence stores FIRST, then wrap them.
services
    .AddEventSourcing(/* your stores */)
    .AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

// Sharding: route each tenant to its own physical store. Requires tenant-aware routing
// to have been enabled on the event-sourcing builder.
services
    .AddEventSourcing(es => es.EnableTenantSharding(/* shard map */))
    .AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.Sharding);
```

| `TenantIsolationStrategy` | What it does | Requirement |
|---------------------------|--------------|-------------|
| `RowDiscriminator` | Wraps the store types listed below with a tenant-scoped decorator. **It does not cover every store you register** — see the coverage note. | At least one decoratable, **tenant-aware** store must be registered before the call. |
| `Sharding` | Routes each tenant to its own storage shard (delegates to the same routing as `EnableTenantSharding`, so the two seams do not fork). | `AddEventSourcing(es => es.EnableTenantSharding(...))` must have run first. |

:::warning Fail-closed by construction
`AddMultiTenancy` refuses to register a false-isolation configuration:

- An unset or invalid `Strategy` throws `InvalidOperationException`.
- `RowDiscriminator` with **no decoratable store registered**, or a store that is **not tenant-aware**, throws
  rather than silently leaving stores unscoped.
- `Sharding` without tenant-aware routing enabled throws.

The same checks run again at startup via `ValidateOnStart`. The call is idempotent — a second `AddMultiTenancy`
is a no-op, so decorators are never double-wrapped.
:::

:::danger `RowDiscriminator` covers an enumerated set of stores — not everything you register

Tenant scoping applies to a **closed set of contracts** the framework declares tenant-owned. That set is:

`IEventStore` · `IProjectionStore<T>` · `ISagaStore` · `IInboxStore` · `IOutboxStore` · `IEventStoreErasure` · `IErasureStore` · `ILegalHoldStore`

A tiered **cold** store (`IColdEventStore`) is handled separately — see the note at the end of this block.

**Any store outside that set is left unscoped by this call.** That includes the audit store, the
data-inventory store, and the dead-letter queue. Registering multi-tenancy does not make them
tenant-isolated, and no error is raised for the stores it does not cover — the fail-closed checks above
concern the contracts in the set, not the ones outside it.

For those stores, isolation is whatever the store itself implements. Some scope only when the **caller**
supplies a tenant on each call, which means an omitted tenant widens the result set instead of failing. Check
the specific store's documentation — [audit logging](./compliance/audit-logging.md) and
[dead-letter](./patterns/dead-letter.md) both carry their own notes — and test cross-tenant behaviour against
your real database rather than inferring it from this registration.

**Tiered/cold storage is explicitly gated.** Combining `RowDiscriminator` with a tiered setup whose cold leg
is not tenant-aware is a known, unclosed isolation gap on the cold tier, so that combination **fails fast at
startup** rather than running unsafely. It is refused, not silently degraded.
:::

## Keyed stores always bind a tenant term

A store whose unique key includes the tenant column is a **keyed** store. A keyed request routed through `KeyedTenantPartition` always binds a concrete, non-empty tenant term, so a read, erase, or replay matching every tenant's rows is not expressible on that path.

:::caution "Keyed" is a property of a store and an operation, never of a subsystem
Do not read a subsystem name as a guarantee about everything inside it. The inbox, saga, snapshot, dead-letter and event-store subsystems each contain keyed stores, and each also contains **satellite stores that carry no tenant term at all** — for example the saga *timeout* store addresses a saga by its identifier alone and persists its timeout payload without a tenant column, while the main saga request store is keyed. Whether each satellite is a defect or a legitimately estate-wide store is being settled per store; until a given store documents its own guarantee, assume it binds no tenant term.
:::

Even within a keyed store, the guarantee is a property of the **request path**, not of the store as a whole: a keyed store may still expose operations that do not route through `KeyedTenantPartition`, and those operations carry no tenant term. The dead-letter queue is one documented example — its writes and its replay bind a tenant term, but **its read operations do not**, and its administrative purge deliberately spans every tenant. Before treating any individual operation as tenant-bound, check the guarantee documented on that operation — see [dead letter queue](./patterns/dead-letter.md).

`KeyedTenantPartition` has exactly two inhabitants:

| Inhabitant | Term bound |
|------------|-----------|
| `KeyedTenantPartition.Scoped(tenantId)` | The supplied tenant identifier. A `null`, empty, or whitespace value throws `TenantRequiredException`; the reserved sentinel is rejected as a tenant name. |
| `KeyedTenantPartition.Untenanted` | The reserved `__untenanted__` sentinel — a real value, so an untenanted row still emits a real equality term. |

There is no `None` inhabitant, no public constructor, and no default, so a keyed request that carries no tenant term cannot be constructed. `KeyedTenantPartition.FromScope(scope)` projects a column-agnostic `TenantScope` onto this family — a `TenantScope.None` scope becomes `Untenanted` rather than an empty predicate — and `KeyedTenantPartition.FromContext(tenantContext)` derives it from ambient context, yielding `Untenanted` when no tenant context is registered.

**Registering a tenant context and then failing to resolve a tenant is a hard error, not a fallback.** With a context present but its tenant blank, `FromContext` throws `TenantRequiredException` rather than quietly returning `Untenanted` — so a multi-tenant host cannot silently write a tenant's rows into the untenanted partition because resolution failed on one request. The two cases are deliberately different: *no context at all* is a single-tenant deployment and is supported; *a context that resolved nothing* is a bug in your resolver and fails closed.

This is why keyed schemas declare their tenant column `NOT NULL` and include it in the unique key: a single-tenant host stores the `__untenanted__` sentinel rather than `NULL`, because a `NULL` discriminator cannot participate in a unique constraint and does not match an equality predicate. When migrating an existing table, backfill legacy `NULL` tenants to the sentinel **before** adding the constraints, or those rows become unreadable. See [event store setup](./configuration/event-store-setup.md) and [the outbox schema](./patterns/outbox.md) for the concrete DDL.

`TenantScope` remains the column-agnostic family for append-log-shaped requests, where `TenantScope.None` deliberately emits no term.

## Cold/archive storage binds a tenant term

`IColdEventStore` takes a `KeyedTenantPartition` on every method — `WriteAsync`, both `ReadAsync` overloads, and `HasArchivedEventsAsync`. Cold storage object keys are composed from that partition, so events archived under one tenant are not addressable from another tenant's read or watermark check, and the keyed guarantee above extends to the cold tier.

A single-tenant host archives under the reserved `__untenanted__` sentinel, exactly as keyed hot stores do, so there is no unpartitioned cold key.

:::warning Archived objects written before the cold tier was tenant-partitioned
Objects archived by an earlier version were keyed without a tenant segment, so they are not addressable under the current key composition. Re-archive or re-key those objects before relying on cold read-through for them.
:::

## What's Next

- [Tenant Sharding](./event-sourcing/tenant-sharding.md) — the persistence-side counterpart: routing each
  tenant to its own storage shard
