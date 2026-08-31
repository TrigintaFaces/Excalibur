---
sidebar_position: 40
title: Multi-Tenancy
description: Dispatch-side ambient tenancy — resolve, carry, and fail-fast enforce a per-message tenant across the dispatch pipeline.
---

# Multi-Tenancy

`Excalibur.Dispatch` provides **dispatch-side ambient tenancy**: an async-flow-local tenant identifier that
flows with a logical operation, and an explicit fail-fast control (`RequireTenant`) that refuses to run an
unscoped operation when a tenant is mandatory. The
tenant is exposed as a read-only ambient value through [`ITenantContext`](#the-ambient-tenant-model), so
consumers can scope their work to the current tenant without a setter to tamper with.

:::info Part of the core package
The `Tenancy` types (`ITenantContext`, `TenantContextOptions`, `TenantContextHolder`,
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

Register the ambient tenant context with `AddTenantContext`. Options are validated at
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
- a validated `TenantContextOptions` with its `IValidateOptions<TenantContextOptions>` and `ValidateOnStart()`.

`ITenantContext` is registered with `Replace` semantics: the ambient context deliberately wins over the
fail-closed single-tenant default registered on the core store path, regardless of composition order. To
supply your own `ITenantContext`, register it **after** `AddTenantContext`. The `configure` callback is
optional — call `services.AddTenantContext()` to register with defaults.

### `TenantContextOptions`

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `RequireTenant` | `bool` | `false` | Selects the deployment mode. When `true`, a tenant-required path that cannot determine its tenant fails fast (throws `TenantRequiredException`) instead of running unscoped. `AddMultiTenancy` sets this for you. When `false`, the host is single-tenant and an unscoped operation is allowed. |
| `DefaultTenantId` | `string?` | `null` | The explicit tenant identity a single-tenant host operates as. When set, it must not be empty or whitespace (enforced at startup). |

## The ambient tenant model

Tenancy has one consumer-facing collaborator:

- **`ITenantContext`** — a read-only ambient accessor for the current tenant. It exposes `TenantId`
  (`string?`, `null` when no tenant is resolved) and `HasTenant` (`bool`). It has **no mutator**: the ambient
  tenant is structurally immutable through this contract and can only be established by the resolving scope, so
  a consumer can read the current tenant but cannot reassign it.

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

### Establishing the ambient tenant

The ambient tenant is established by the host, at the boundary where the tenant becomes known, by opening a
`TenantContextHolder.BeginScope` for the duration of the operation. Everything inside that scope — handlers,
stores, decorators — reads the tenant through `ITenantContext` without threading it through call signatures.

For an ASP.NET Core host, `UseTenantIdMiddleware()` (in `Excalibur.Hosting.Web`) does this for each request.
For a dispatched message, the tenant travels on the message's identity feature: `TenantIdentityMiddleware`
resolves it from the configured source and stamps it, and `context.GetTenantId()` reads it back. A host that
wants the ambient tenant established per message wraps the pipeline the same way:

```csharp
using (TenantContextHolder.BeginScope(context.GetTenantId()))
{
    return await next(message, context, ct).ConfigureAwait(false);
}
```

:::warning Establish the scope from a trusted tenant, not a raw inbound header
`TenantIdentityMiddleware` derives the tenant from a transport header (`X-Tenant-ID` by default). An
ambient scope selects the partition every tenant-aware store reads, so opening one directly from an
unvalidated inbound header lets a caller name another tenant's data. Keep `ValidateTenantAccess`
enabled, or establish the scope from a tenant you have authorised (a validated claim), before wrapping
the pipeline.
:::

## `RequireTenant`: fail-fast tenant isolation

:::warning Security isolation control
`RequireTenant` is a **security isolation guarantee**, not a convenience toggle. It declares the deployment
multi-tenant, and every tenant-aware store reads it: when `RequireTenant` is `true` and an operation reaches a
tenant-required path with no ambient tenant established, it throws **`TenantRequiredException`** rather than
silently running work that would cross — or escape — tenant boundaries.
:::

This mirrors the storage-side fail-fast (`TenantShardNotFoundException`): a tenant-required path that cannot
determine its tenant fails loudly instead of degrading to an unscoped operation. `TenantRequiredException`
derives from `InvalidOperationException` and carries a descriptive message.

Behaviour summary for the "no ambient tenant established" case:

| `RequireTenant` | Result |
|-----------------|--------|
| `false` | The host is single-tenant; the operation runs as `DefaultTenantId` (or unscoped when unset). |
| `true` | Throws **`TenantRequiredException`** — fail fast. |

`RequireTenant = true` is the strict configuration: it requires every operation to establish its own tenant,
and any that does not is rejected rather than degraded.

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
| `RowDiscriminator` | Applies tenant scoping to the contracts listed below — **wrapping some and only verifying others**, see the coverage note. Any other registered contract declared tenant-owned is **refused at startup** unless its provider attests a tenant mechanism. **It does not add a tenant predicate to a store for you.** | At least one decoratable, **tenant-aware** store must be registered before the call. |
| `Sharding` | Routes each tenant to its own storage shard (delegates to the same routing as `EnableTenantSharding`, so the two seams do not fork). Registered tenant-owned contracts must attest a tenant mechanism here too — sharding confines tenants only while your tenant-to-shard mapping is injective, which is your configuration and not something startup can check, so a store that confines nothing is no safer here. | `AddEventSourcing(es => es.EnableTenantSharding(...))` must have run first. |

:::warning Fail-closed by construction
`AddMultiTenancy` refuses to register a false-isolation configuration:

- An unset or invalid `Strategy` throws `InvalidOperationException`.
- `RowDiscriminator` with **no decoratable store registered**, or a store that is **not tenant-aware**, throws
  rather than silently leaving stores unscoped.
- A registered **tenant-owned** contract whose provider presents no tenant capability throws, naming the store.
- `Sharding` without tenant-aware routing enabled throws.

The `Strategy` check and the tenant-capability requirement both run again at startup. The capability
requirement has to: reading the service collection answers for the registrations present at the instant
`AddMultiTenancy` is called, and a store you register on the *next* line is not among them. Re-asking the
same question once the collection is complete is what makes the outcome independent of the order you make
the two calls in — for every strategy, not only `RowDiscriminator`. The composition-time check is kept
because it fails at the call site with your registration in view, which is a better diagnostic; the startup
one is what guarantees the check happens at all.

The remaining composition-time checks (an unset strategy, no decoratable store) are enforced when
`AddMultiTenancy` is called and not repeated later. The call is idempotent — a second `AddMultiTenancy` is a
no-op, so decorators are never double-wrapped.
:::

:::danger `RowDiscriminator` does not write a tenant predicate for every store you register

Tenant scoping applies to the contracts the framework declares **tenant-owned**. Which contracts those are is
read from the declaration on each contract rather than from a fixed list, so one added later — including one
you declare yourself — is covered as soon as it is declared, not when someone remembers to extend a manifest.
Being tenant-owned is not the whole story: `AddMultiTenancy` does **two different things** to the contracts
below, and which one applies to your store decides who is responsible for the tenant predicate.

| Contract | Treatment | Who applies the tenant predicate |
|---|---|---|
| `IEventStore` · `IProjectionStore<T>` · `ISagaStore` | **Decorated** — wrapped in a fail-closed tenant-scoping decorator | The framework |
| `IInboxStore` · `IOutboxStore` · `IEventStoreErasure` · `IErasureStore` · `ILegalHoldStore` | **Gated only** — verified at registration, never wrapped | **Your store** |

**If your store is in the second row, writing it tenant-unaware is a startup failure, not a silent leak.**
`AddMultiTenancy` requires each gated contract to present a capability that the provider's registration
emits, and refuses to start when one is missing. There are **two** capabilities, because these contracts keep
tenants apart by two different mechanisms:

- `IInboxStore`, `IEventStoreErasure`, `IErasureStore` and `ILegalHoldStore` must present **ambient tenant
  scoping** — the store applies the ambient tenant discriminator to every operation.
- `IOutboxStore` must present **row-carried tenancy** — the store persists the tenant discriminator on each
  message and hands it back on drain, so the owning tenant is re-established from the row. It deliberately
  does **not** claim ambient scoping: one drain pass carries every tenant's messages, so a store that
  filtered on the ambient tenant would find none, claim the empty set, and stall the drain.

Be precise about what that check is, because it is easy to read as more than it is. It reads a capability the
provider's registration emitted alongside the store — it does not inspect your queries. It guarantees that a
provider which never routes through a tenant-aware registration is rejected at startup. It cannot verify that
the predicate you wrote is correct, and it will not add the predicate for you: a custom `IInboxStore` or
`IOutboxStore` must carry its own.

The five are unwrapped deliberately, not by omission. The outbox drain is cross-tenant by design: a single
pass carries every tenant's messages and scopes each one individually, so a decorator would find no ambient
tenant, claim the empty set, and stall the drain permanently — while still passing any test that only asserts
one tenant cannot see another's rows. The inbox already filters on its composite
`(TenantId, MessageId, HandlerType)` key, so a decorator would add a second filter without repairing a first.
Erasure, erasure-request and legal-hold sweeps run from background services with **no** ambient tenant, where
a decorator would either refuse every erasure or widen one tenant's erasure across all of them.

**Writing your own provider? There is one registration verb, not two.** A store attests either
capability through `AddTenantAwareStore<TContract, TStore>()` — a single method, never a choice between
an "ambient" call and a "row-carried" call. It inspects your store's public constructor for an
`ITenantContext` parameter: when one is present, it resolves the context fail-closed before construction
and emits the ambient-scoping capability; when none is present, it emits the row-carried capability
instead. Which mechanism your store uses is a structural fact about its constructor, not a decision you
make at the call site — the same one-verb registration handles the inbox, erasure and legal-hold stores
above (ambient) and the outbox (row-carried) identically, because it derives the answer from `TStore`
rather than from which of two methods you remembered to call.

A tiered **cold** store (`IColdEventStore`) is handled separately — see the note at the end of this block.

**A store outside the wrapped and verified sets is still not scoped by this call — but it is no longer
ignored.** Any contract the framework declares tenant-owned — including the audit store, the compliance and
data-inventory stores, the snapshot store and the dead-letter queue — must present a tenant capability from
its provider's registration. One that presents none is **refused at startup, by name**, instead of being
registered unscoped with no error.

Read that as what it is. The refusal is a **registration** check, not confinement: it establishes that the
provider claims a tenant mechanism, and it neither adds a predicate nor verifies the predicate you wrote.
For a store that does attest, isolation remains whatever that store implements. Some scope only when the
**caller** supplies a tenant on each call, which means an omitted tenant widens the result set instead of
failing. Check the specific store's documentation — [audit logging](./compliance/audit-logging.md) and
[dead-letter](./patterns/dead-letter.md) both carry their own notes — and test cross-tenant behaviour against
your real database rather than inferring it from this registration.

:::caution Current limitation: three contracts have no capable provider yet
No provider shipped with the framework currently attests a tenant capability for **`IAuditStore`**,
**`IDeadLetterQueue`**, or **`IDataInventoryStore`**. Registering any of them in a multi-tenant container
therefore fails at startup with a message naming the store, and there is no in-framework registration that
satisfies the check today.

Until a capable provider ships, either **do not register those stores in a multi-tenant container**, or
**select a different tenant-isolation strategy**. This is a deliberate trade: before this check existed, the
same configuration started cleanly and read across tenants. A refusal you can see is the safer failure.
:::

:::info `ICloudNativeEventStore` was on that list and is not any more
The document-database event store contract — under which the Cosmos DB, DynamoDB and Firestore
event-sourcing registrations place their store — used to attest nothing, because those stores composed
their document keys from the aggregate type and id with no tenant term. The MongoDB event store had the
same composition and was refused by a different route, on its keyed `IEventStore`.

All four now compose the owning tenant into the document key and attest it, so a multi-tenant container may
register any of them under either isolation strategy. **The saga stores on the same four providers changed
the same way**, for the same reason: a saga identifier is a business correlation key two tenants
legitimately share.

**The stored key shape changed with it** — documents written by an earlier version are not addressable by
the new key and must be re-keyed, and these stores **refuse rather than reporting a false absence** once
they find one. This applies whether or not you enable multi-tenancy, because the key carries a reserved
single-tenant or untenanted segment either way. See [Cosmos DB, DynamoDB, Firestore and MongoDB keys carry
the tenant](./migration/nosql-tenant-key-rekey.md).
:::

**Tiered/cold storage is explicitly gated.** Combining `RowDiscriminator` with a tiered setup whose cold leg
is not tenant-aware is a known, unclosed isolation gap on the cold tier, so that combination **fails fast at
startup** rather than running unsafely. It is refused, not silently degraded.
:::

## Estate-wide operations are named, never implied by absence

A tenant-scoped contract's per-request surface — `LoadAsync`, `SaveAsync`, and the rest — has no code
path that reaches every tenant's rows. Omitting a tenant does not widen a query on that surface; it
either resolves to the reserved `__untenanted__` partition or throws (see `RequireTenant` and keyed
stores, above). There is no "leave the tenant blank to see everything" mode.

Where a store genuinely needs a cross-tenant operation, that operation is a **separate, differently
named member** on the contract, never a side effect of an absent parameter. The framework's own stores
follow this consistently:

| Contract | Estate-wide member | What it does |
|---|---|---|
| `IOutboxStore` | `CleanupAllTenantsSentMessagesAsync`, `BulkCleanupAllTenantsSentMessagesAsync` | Retention: purge sent messages across every tenant |
| `IOutboxStore` | `GetAllTenantsTransportDeliveriesAsync` | The drain's cross-tenant read of a message's transport deliveries |
| `ISagaStore` | `PurgeAllTenantsCompletedBeforeAsync` | Retention: purge completed sagas across every tenant |
| `IDeadLetterQueue` | `PurgeAllTenantsEntriesOlderThanAsync` | Retention: purge dead-lettered entries across every tenant |

The name is the safety control, not documentation of one. These members exist for operator tooling and
background sweeps — the drain, the retention job, the cleanup service — not for request-scoped
application code. If you find yourself reaching for a member whose name says `AllTenants` from inside a
per-request handler, that is very likely a mistake: the tenant-scoped member you actually want is the
one without it.

Design your own estate-wide operations, on your own tenant-owned contracts, the same way: name the
cross-tenant path explicitly rather than reaching it by leaving a tenant argument out.

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

There is no absent inhabitant, no public constructor, and no default, so a keyed request that carries no tenant term cannot be constructed. `KeyedTenantPartition.FromScope(scope)` projects a `TenantScope` onto this family, and `KeyedTenantPartition.FromContext(tenantContext)` derives it from an ambient context that you must supply.

**Registering a tenant context and then failing to resolve a tenant is a hard error, not a fallback.** With a context present but its tenant blank, `FromContext` throws `TenantRequiredException` rather than quietly returning `Untenanted` — so a multi-tenant host cannot silently write a tenant's rows into the untenanted partition because resolution failed on one request.

**`FromContext` requires a context; it will not invent one.** The parameter is non-nullable and a `null` argument throws `ArgumentNullException`. There is deliberately no null-accepting form, on either `KeyedTenantPartition` or `TenantScope`.

This is the one case the framework refuses to decide for you, and the reason is worth a sentence. *"No tenant context is registered here"* and *"this row belongs to no tenant"* are different statements, and a conversion that turned the first into the second produced two defects that looked unrelated: an outbox statistics read compared every row against a constant sentinel and reported an empty backlog while the table filled, and a saga retention sweep filtered on a tenant no row could match, deleting nothing forever while reporting success. Same conversion, two callers, two different wrong answers.

So if your context is optional — because multi-tenancy may not be registered in that deployment — decide at your own call site what its absence means for that store, and write the decision down where a reviewer can see it:

```csharp
// No ambient context means this host is not multi-tenant, so every archived row belongs to
// the reserved untenanted partition. The framework will not make that call for you.
_tenant = tenantContext is null
    ? KeyedTenantPartition.Untenanted
    : KeyedTenantPartition.FromContext(tenantContext);
```

`Untenanted` is the right answer for a genuinely single-tenant host. It is **not** automatically the right answer for a store whose context merely failed to be wired — that is a registration bug, and choosing `Untenanted` there is how a tenant's rows end up in the untenanted partition. If you cannot state which of the two situations you are in, treat it as the bug.

The same rule applies to `TenantScope`. Defaulting a missing context to the untenanted partition turns "multi-tenancy was never registered here" into "this row belongs to no tenant" — those are not synonyms, and conflating them is what produced the unscoped read above.

This is why keyed schemas declare their tenant column `NOT NULL` and include it in the unique key: a single-tenant host stores the `__untenanted__` sentinel rather than `NULL`, because a `NULL` discriminator cannot participate in a unique constraint and does not match an equality predicate. When migrating an existing table, backfill legacy `NULL` tenants to the sentinel **before** adding the constraints, or those rows become unreadable. See [event store setup](./configuration/event-store-setup.md) and [the outbox schema](./patterns/outbox.md) for the concrete DDL.

`TenantScope` is the column-agnostic family for append-log-shaped requests. Like `KeyedTenantPartition` it has no absent inhabitant: `TenantScope.TenantId` is total, and `default(TenantScope)` **is** `TenantScope.Untenanted` rather than a missing term. Whether a deployment applies tenant confinement at all is a property of the deployment, which a store reads from its own configuration — it is never inferred from a scope that carries no tenant.

## The storage contract: what your tenant column must declare

Isolation is enforced by a `WHERE` clause, so it is only as strong as the column that clause compares. Two properties of the **schema** are load-bearing, and both fail silently when they are missing — no exception, no warning, just a query that returns the wrong set. If you create these tables yourself, override the default table names, or write your own migration, they are your responsibility.

### 1. The tenant column must pin a binary collation

```sql
[TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
```

SQL Server's default collation is typically **case-insensitive**, under which `'Acme' = 'acme'` is true. Every tenant-scoped read is an equality comparison on this column, so without an explicit binary collation a tenant whose identifier differs from another's only by case reads that tenant's rows. The comparison **fails open** — it matches more than it should, and nothing errors.

Where the tenant column is part of a key rather than only a filter, the consequence is worse than a leak. The snapshot store's `MERGE` matches on `(AggregateId, AggregateType, TenantId)`; under a case-insensitive collation one tenant's save can **match and overwrite** another tenant's snapshot.

The framework compares tenant terms with ordinal semantics, so the column must agree or the guarantee is lost in storage rather than in code. PostgreSQL and Oracle compare their text types case-sensitively already and need no equivalent clause.

### 2. Untenanted is a value, not an absence

A single-tenant deployment stores the reserved `__untenanted__` sentinel — never `NULL`, and never the empty string. The empty string is specifically excluded because **Oracle folds `''` to `NULL`**, so identical intent would become a different value on that provider. The sentinel is rejected as a real tenant name, so it cannot collide with one of yours.

### Why the event table carries a default and the snapshot table does not

Neither column is nullable. `TenantId` is `NOT NULL` on both tables, so the sentinel is the only representation of "untenanted" in either. The two differ in one thing — whether the column carries a `DEFAULT` — and that difference is deliberate:

| | `EventStoreEvents` | `EventStoreSnapshots` |
|---|---|---|
| `TenantId` | `NOT NULL`, defaulted to the sentinel | `NOT NULL`, no default |
| Where it appears | a `UNIQUE` constraint | the **primary key** |
| Read predicate | `COALESCE(TenantId, '__untenanted__') = @TenantId` | `TenantId = @TenantId` |

Events are the system of record and predate tenancy in existing deployments, so an append-only log can contain rows written before the tenant column existed. Those rows are not left as a second spelling of "untenanted": the shipped migration backfills them to the sentinel and then makes the column `NOT NULL`. Totality is worth that migration because `TenantId` participates in the event table's `UNIQUE` constraint, and a nullable column in a `UNIQUE` constraint is compared under three-valued logic — which would leave the optimistic-concurrency guarantee weaker for untenanted streams than for tenanted ones. With the column total, one rule covers both.

The `COALESCE` in the event read path is retained and is now a no-op over a total column. It is left in place deliberately: removing it is a separate, behaviour-visible change, and it costs nothing where it stands.

Snapshots omit the default because their tenant term is a component of **identity** rather than a filter — it is part of the primary key, and you do not default a key column. With a default, a save that forgot to supply the tenant would land silently in the untenanted partition, making "I forgot the tenant" indistinguishable from "this row is deliberately untenanted". Without one, that statement fails outright. The snapshot store's `MERGE` matches on exactly that triple; declaring anything narrower would produce a silent cross-tenant overwrite rather than an error.

For the same reason, do **not** rewrite the snapshot predicate as `COALESCE(TenantId, …)` to save the sentinel's storage. It cannot help — the column cannot hold `NULL` while it is in the key — and it demotes `TenantId` out of the index seek into a residual filter, so a lookup reads every tenant's row for that aggregate instead of landing on one. The sentinel costs 28 bytes against a snapshot payload usually measured in kilobytes. If snapshot storage is a concern at scale, prune old snapshots instead; they regenerate.

### Running the scripts

Execute the shipped `.sql` with `QUOTED_IDENTIFIER` **ON**. Several schemas declare filtered indexes (`CREATE INDEX … WHERE`), which SQL Server refuses to create without it, and `sqlcmd` defaults it **off** — the index is then simply absent, leaving a database that works but scans where it should seek. The shipped scripts set it themselves; if you split, reorder, or hand-copy them into your own migration tooling, set it there too.

Every convergence script that refuses on ambiguous data (a named tenant present, a collision under the new key) is self-contained: it opens its own transaction, so a refusal rolls back everything the script has done so far.

**Apply each SQL Server script on a single connection.** It opens its transaction in its first batch and commits it in its last, and a transaction belongs to a session — a runner that reconnects between `GO` batches loses it at the first one. Rather than let that run on unprotected, the script checks for it immediately after opening the transaction and refuses, naming the cause. `sqlcmd` holds one connection by default; a migration runner may need configuring.

Stopping after a refusal is handled inside the script itself, in plain SQL, and the two dialects need different amounts of help. PostgreSQL needs none beyond the transaction: the whole file is one statement stream on one connection, so a refusal aborts the transaction and every later statement in the file fails against it — nothing after the refusal can take effect, and the trailing `COMMIT` becomes a rollback. SQL Server needs more, because `GO` is a *client* batch separator: each batch reaches the server as its own unit, so every batch opens with `IF @@TRANCOUNT = 0 SET NOEXEC ON;` and, once a refusal has rolled the transaction back, every later batch is sent but never executed.

**The shipped scripts contain no client meta-commands at all** — no `:on error exit`, no `:setvar`, no `\set`. Those are commands to `sqlcmd` and `psql` rather than statements, so any other runner sends them to the server and the script dies on that line having done nothing: `Incorrect syntax near ':'` from SqlClient, `42601 syntax error at or near "\"` from Npgsql. Keeping them out is what lets you apply these files with whatever your deployment already uses — Npgsql, JDBC, Flyway, Liquibase, DbUp, EF migrations, or your own connection loop. If you split a script apart or splice its statements into your own migration tooling, keep the transaction boundaries **and**, on SQL Server, those per-batch guards with the pieces that follow the refusal, or a partial run stops applying but does not undo what already ran.

One thing the scripts cannot do for you is set the **process exit code**, and that is a client setting by nature rather than an omission. On a refusal `sqlcmd` still exits `0` unless you pass `-b`, and `psql` still exits `0` unless it is told to stop on error — it prints the error, runs out the rest of the file, rolls back, and reports success. If a pipeline branches on the exit code, run them as:

```bash
sqlcmd -b -i <script>                  # SQL Server
psql -v ON_ERROR_STOP=1 -f <script>    # PostgreSQL
```

Without those flags the pipeline will read a refused, no-op migration as a success. Nothing is kept either way — the transaction still rolls back — but the pipeline is told the wrong thing.

### The compliance and data-inventory stores converge the other direction

The event-store migration above moves rows **from** the untenanted sentinel **onto** the single-tenant
identity, because those stores never had a tenant concept at all before tenancy shipped. The Postgres
compliance store and the data-inventory stores (SQL Server, Postgres) hit the opposite defect: their
registration helper always supplies a tenant context, so a single-tenant deployment of these stores
always bound the single-tenant identity `__default__` and never the reserved sentinel
`__untenanted__` — the reverse mistake, needing the reverse convergence.

If you run `PostgresComplianceStore` or either data-inventory store on a single-tenant host, run the
shipped convergence migration **before** upgrading:
`Excalibur.Compliance.Postgres/Scripts/004_ConvergeDefaultToUntenanted.sql` and, on SQL Server,
`Excalibur.Compliance.SqlServer/Scripts/007_ConvergeDefaultToUntenanted.sql`. Each moves every row from
`__default__` to `__untenanted__` and refuses — naming the affected table and the offending tenant —
if the deployment already holds a row under a named tenant, so a genuinely multi-tenant host is left
untouched rather than guessed at.

## Cold/archive storage binds a tenant term

`IColdEventStore` takes a `KeyedTenantPartition` on every method — `WriteAsync`, both `ReadAsync` overloads, and `HasArchivedEventsAsync`. Cold storage object keys are composed from that partition, so events archived under one tenant are not addressable from another tenant's read or watermark check, and the keyed guarantee above extends to the cold tier.

A single-tenant host archives under the reserved `__untenanted__` sentinel, exactly as keyed hot stores do, so there is no unpartitioned cold key.

:::warning Archived objects written before the cold tier was tenant-partitioned
Objects archived by an earlier version were keyed without a tenant segment, so they are not addressable under the current key composition. Re-archive or re-key those objects before relying on cold read-through for them.
:::

## What's Next

- [Tenant Sharding](./event-sourcing/tenant-sharding.md) — the persistence-side counterpart: routing each
  tenant to its own storage shard
