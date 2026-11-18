---
sidebar_position: 9
title: Sagas & Workflows
description: Orchestrate multi-step business processes with event-driven coordination, timeout handling, and compensation using Excalibur.Saga.
---

# Sagas & Workflows

Sagas coordinate multi-step business processes where failure in one step requires compensating (rolling back) previously completed steps. Excalibur provides an **event-driven saga model** built on `SagaBase<T>` and `ISagaCoordinator`.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Saga
  dotnet add package Excalibur.Saga.SqlServer  # for persistence
  ```
- Familiarity with [handlers](../handlers.md) and [dependency injection](../core-concepts/dependency-injection.md)

## How Sagas Work

Each saga is a class that extends `SagaBase<TState>`. The framework routes incoming events to saga instances via `ISagaCoordinator`. Handlers update saga state between deliveries. Optimistic concurrency can run competing handlers before a losing commit is refused; it does not serialize handler execution across nodes. Persisted state survives restarts, while durability of outgoing commands, events and configured timers depends on the selected profile described below. External operations must remain idempotent.

```
Event arrives → SagaHandlingMiddleware → SagaCoordinator → SagaBase<T>.HandleAsync()
                                                         → ISagaStore.SaveAsync()
```

This event-driven model works across independently deployed microservices, long-running workflows, and modular monoliths alike.

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Saga` | Core saga engine, coordinator, timeout infrastructure |
| `Excalibur.Saga.SqlServer` | SQL Server saga state persistence |
| `Excalibur.Saga.Postgres` | PostgreSQL saga state persistence |
| `Excalibur.Saga.CosmosDb` | Azure Cosmos DB saga state persistence |
| `Excalibur.Saga.DynamoDb` | AWS DynamoDB saga state persistence |
| `Excalibur.Saga.MongoDB` | MongoDB saga state persistence |
| `Excalibur.Saga.Firestore` | Google Firestore saga state persistence |

**Saga state persistence and timeout persistence are separate choices.** SQL Server and Oracle ship
durable saga timeout stores; selecting PostgreSQL, Cosmos DB, DynamoDB, MongoDB or Firestore saga
state persistence does not supply a matching durable timeout store. `WithTimeouts()` defaults to
process-local in-memory timeout storage unless another timeout store has been registered; it does not
make timers durable merely because saga state is durable. Select a supported durable timeout
composition explicitly when timers must survive process loss.

## Quick Start

### 1. Define Saga State

```csharp
using Excalibur.Saga.Orchestration;
using Excalibur.Dispatch.Messaging;

public class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    // Populated as steps complete
    public string? ReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? ShipmentTrackingNumber { get; set; }
    public string? FailureReason { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
}
```

### 2. Define Saga Events

Events must implement `ISagaEvent` so the coordinator can route them:

```csharp
using Excalibur.Dispatch;

// Start event — creates a new saga instance
public record StartOrderProcessing(
    string SagaId, string OrderId, string CustomerId, decimal TotalAmount) : ISagaEvent
{
    public string? StepId => nameof(StartOrderProcessing);
}

// Step completion events
public record InventoryReserved(string SagaId, string ReservationId) : ISagaEvent
{
    public string? StepId => nameof(InventoryReserved);
}
public record PaymentProcessed(string SagaId, string TransactionId) : ISagaEvent
{
    public string? StepId => nameof(PaymentProcessed);
}
public record OrderShipped(string SagaId, string TrackingNumber) : ISagaEvent
{
    public string? StepId => nameof(OrderShipped);
}

// Failure events
public record PaymentFailed(string SagaId, string Reason) : ISagaEvent
{
    public string? StepId => nameof(PaymentFailed);
}
```

### 3. Implement the Saga

```csharp
using Excalibur.Dispatch;
using Excalibur.Saga;
using Excalibur.Saga.Orchestration;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.Logging;

public sealed partial class OrderFulfillmentSaga(
    OrderSagaState initialState,
    IDispatcher dispatcher,
    ILogger<OrderFulfillmentSaga> logger)
    : SagaBase<OrderSagaState>(initialState, dispatcher, logger)
{
    public override bool HandlesEvent(object eventMessage)
    {
        return eventMessage is StartOrderProcessing
            or InventoryReserved
            or PaymentProcessed
            or OrderShipped
            or PaymentFailed;
    }

    public override async Task<SagaEventOutcome> HandleAsync(
        object eventMessage, CancellationToken cancellationToken)
    {
        switch (eventMessage)
        {
            case StartOrderProcessing start:
                State.OrderId = start.OrderId;
                State.CustomerId = start.CustomerId;
                State.TotalAmount = start.TotalAmount;
                LogSagaStarted(State.SagaId, start.OrderId);
                break;

            case InventoryReserved reserved:
                State.ReservationId = reserved.ReservationId;
                State.CompletedSteps.Add("ReserveInventory");
                break;

            case PaymentProcessed paid:
                State.PaymentTransactionId = paid.TransactionId;
                State.CompletedSteps.Add("ProcessPayment");
                break;

            case OrderShipped shipped:
                State.ShipmentTrackingNumber = shipped.TrackingNumber;
                State.CompletedSteps.Add("ShipOrder");
                LogSagaCompleted(State.SagaId, State.OrderId);
                await MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
                break;

            case PaymentFailed failed:
                State.FailureReason = failed.Reason;
                MarkCompleted();
                break;
            default:
                return SagaEventOutcome.Declined;
        }
        return SagaEventOutcome.Handled;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Saga {SagaId} started for order {OrderId}")]
    private partial void LogSagaStarted(Guid sagaId, string orderId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Saga {SagaId} completed for order {OrderId}")]
    private partial void LogSagaCompleted(Guid sagaId, string orderId);
}
```

### 4. Register and Configure

This tutorial explicitly selects volatile in-memory saga state for one process. It does not survive restart or coordinate multiple instances. Select the persistent provider shown below for a durable deployment.

```csharp
using Excalibur.Saga.Orchestration;
using Excalibur.Dispatch.Messaging;
using Microsoft.Extensions.DependencyInjection;

public static class SagaRegistrationExample
{
    public static void Run(IServiceCollection services)
    {
        services.AddInMemorySagaStore(); // Explicit local tutorial storage; not durable.
        // Register saga coordination + timeout delivery
        services.AddExcaliburOrchestration();
        services.AddSagaTimeoutDelivery();

        // Register your saga type
        services.AddSaga<OrderFulfillmentSaga, OrderSagaState>();

        // Map events to saga instances
        SagaRegistry.Register<OrderFulfillmentSaga, OrderSagaState>(info =>
        {
            info.StartsWith<StartOrderProcessing>();
            info.Handles<InventoryReserved>();
            info.Handles<PaymentProcessed>();
            info.Handles<OrderShipped>();
            info.Handles<PaymentFailed>();
        });
    }
}
```

Or use the builder pattern with a persistence provider:

```csharp
services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
{
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts();
}));
```

## Declarative Timeouts with ISagaTimeout&lt;T&gt;

Sagas can declare strongly-typed timeout handlers using `ISagaTimeout<T>`. When a timeout fires and the saga implements a matching interface, the framework routes directly to `HandleTimeoutAsync` instead of the general `HandleAsync`:

```csharp
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;

public sealed class PaymentTimeout : ISagaEvent
{
    public string SagaId { get; set; } = string.Empty;
    public string? StepId => "PaymentTimeout";
}

public sealed partial class OrderFulfillmentSaga
    : SagaBase<OrderSagaState>,
      ISagaTimeout<PaymentTimeout>
{
    // Schedule a timeout in HandleAsync
    // State.TimeoutId = await RequestTimeoutAsync<PaymentTimeout>(
    //     TimeSpan.FromMinutes(5), cancellationToken);

    public Task HandleTimeoutAsync(
        PaymentTimeout message, CancellationToken cancellationToken)
    {
        State.FailureReason = "Payment confirmation timed out";
        MarkCompleted();
        return Task.CompletedTask;
    }
}
```

A saga can implement multiple `ISagaTimeout<T>` interfaces for different timeout types. This follows the NServiceBus `IHandleTimeouts<T>` pattern.

## Idempotent Event Replay

`SagaState` remembers which deliveries a saga has already processed, so a redelivery does not run a step twice. **The identity it remembers is the envelope message ID that the delivery carries** — `IMessageContext.MessageId` — not a value composed from the event type, the saga ID or `StepId`. `StepId` does not affect deduplication.

On each delivery the `SagaCoordinator` asks `SagaState.HasProcessedEvent(messageId)`:

- `false` — the delivery is new; run the handler. If the handler **acts**, the identity is recorded and persisted with the state it produced. If the handler declines under its own guard, nothing is recorded and the event stays deliverable.
- `true` — the same delivery already ran; skip it without invoking the handler and without writing state.

Two limits are part of the contract rather than implementation detail:

- **The remembered set is bounded to 1,000 identities per saga instance, evicted oldest-first.** Past that bound a redelivery of an evicted identity **runs the step again**. This is a bounded window, not exactly-once. For unbounded deduplication, put the transactional inbox in front of the saga.
- **A delivery that carries no message ID is processed and not deduplicated.** Every send this framework makes stamps an ID, so this applies only to inbound messages from a producer that sends none. For those the saga is at-least-once and **your handlers must be idempotent**. The first such delivery of each event type logs a warning naming the type, and every one increments the `excalibur.saga.undeduplicable_deliveries` counter (tagged by event type), so a producer that never sends an ID is visible in your metrics and not only in a startup log line. The framework does not invent an identity to fill the gap.

:::warning Upgrading a saga that is already running

Identities persisted by earlier releases were composed from the event type, saga ID and `StepId`. Those entries can no longer be produced, so they are left in place and ignored rather than translated — a saga mid-flight across the upgrade may run one step a second time. See [Resolved issues](../resolved-issues.md) for the full upgrade note, including why a saga that already dropped an event cannot be repaired automatically.
:::

## Optimistic Concurrency

Saga persistence enforces **optimistic concurrency** so two events arriving for the same saga (for example, a business event racing a timeout) cannot silently overwrite each other.

- `SagaState.Version` is the concurrency token — the version the saga was **loaded** at (a brand-new saga is `0`). You perform **no version arithmetic**; the store owns the increment (EF-style).
- On save, the store performs a versioned compare-and-set: it persists the new state only if the stored row is still at the loaded version, bumping `Version` to `loadedVersion + 1`.
- If a concurrent write already advanced the row, the save matches no rows and the store throws `ConcurrencyException` — exactly one of the racing saves wins, and no update is lost.
- **No-resurrect:** a stale-version save targeting a saga that no longer exists (e.g. since deleted/completed) also throws `ConcurrencyException` and does **not** re-create the row — a since-completed saga cannot be resurrected into a zombie by a late event holding an old version.

This behavior is identical whether the saga is driven through `SagaManager` or `SagaCoordinator`. Handle `ConcurrencyException` by reloading the saga and replaying the event (the [idempotent replay](#idempotent-event-replay) guard makes reprocessing safe):

```csharp
try
{
    await sagaStore.SaveAsync(state, cancellationToken);
}
catch (ConcurrencyException)
{
    // A concurrent event advanced this saga. Reload and let the event be re-delivered;
    // the replay guard recognises the redelivery by its message ID and prevents duplicate dispatch.
}
```

:::info Behavior change

The SQL saga store previously issued an unconditional last-writer-wins `UPDATE` that ignored `Version`, so concurrent saves for one saga could lose updates. The save path now always enforces the version check; there is no save path that ignores `Version`.
:::

:::info Behavior change

Optimistic concurrency **and** the no-resurrect guard now hold **uniformly across every saga store provider** — In-Memory, PostgreSQL, MongoDB, Cosmos DB, DynamoDB, and Firestore — not just the SQL stores. Each provider performs the version-gated compare-and-set with its native mechanism (Postgres `WHERE version = @ExpectedVersion`, Cosmos `If-Match` ETag, DynamoDB conditional write, Mongo filtered update, Firestore transaction), and the store owns the version increment.
:::

:::info Behavior change

When an event arrives for a saga that is **already completed** (`SagaState.Completed`), `SagaManager` now short-circuits at load time — it skips **both** the handler invocation and the save — instead of re-running the handler against a finished workflow and re-persisting it (which would spuriously bump the version). The event that *itself* completes the saga still proceeds and persists its completion. This matches the existing `SagaCoordinator` behavior, so the already-completed no-resurrect guarantee is now identical across both drivers.
:::

## State-only and atomic durable transitions

Ordinary registration buffers commands/events and timer intents before saving state. With a
configured timeout store, the coordinator persists the original timer journal with state and
processed-delivery evidence, then applies it through that selected store's
`ISagaTimerRecoveryStore` facet. Recovery precedes completed/deduplicated-delivery checks and
reuses the original timer IDs. A wrapper must mediate the facet to its actual selected backend;
missing receiver capability or schema is refused before a timer-owning state commit. Cancellation
and claim retirement retain authority so replay cannot resurrect cancelled or retired timers.

This ordinary timer recovery does **not** make outgoing commands/events atomic with state.
A lost save acknowledgement or process failure after state commit can still leave processed-delivery
evidence without all command/event dispatches. Use the explicit primary SQL atomic profile below
when those effects must share the state transaction. Delivery remains at least once; handlers and
consumers must be idempotent.

Before deploying the new state/coordinator writers, apply the matching base schemas and state
migrations: SQL Server `05-SagaTimerRecoveryState.sql`, PostgreSQL and Oracle
`02-SagaTimerRecoveryState.sql`. Configured SQL Server timeout recovery also requires
`06-SagaTimerRecovery.sql`; Oracle requires `03-SagaTimerRecovery.sql`. In-memory state and
timeouts are process-local. Quiesce old writers during migration and follow each provider README
for selected-table registration. The framework does not auto-migrate. Retained compact receiver
authority has no automatic TTL eviction; coordinate its lifecycle with queued work and state recovery.
These contracts do not imply that every cloud provider or SQL driver has passed runtime/NativeAOT
verification.

For the primary SQL atomic profile, explicitly select durable preparation **in addition to**
the SQL saga store, SQL outbox writer and any configured participating timeout store:

```csharp
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

public static class DurableSagaRegistration
{
    public static void Run(IServiceCollection services)
    {
        // Configure saga routes and the selected SQL stores using their normal registrations first.
        services.AddDurableSagaTransitions(SagaJsonContext.Default);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OrderSagaState))]
internal partial class SagaJsonContext : JsonSerializerContext;
```

The configured payload codec must also support actual emitted command/event types. This
registration does not make an arbitrary reflective codec NativeAOT-safe. Applications that
intentionally use runtime serialization instead select
`services.AddDurableSagaTransitionsUsingRuntimeSerialization()` and accept its explicit
trimming/dynamic-code requirements. Generated callers use the typed
`ProcessAtomicEventAsync<TSaga, TState, TEvent>` entry; this does not certify every fluent
state-machine or reflective routing path under NativeAOT.

The selected atomic store commits state, processed-delivery identity, original prepared
command/event rows, configured timer mutations and its receipt in one SQL transaction.
The existing outbox publisher drains those rows after commit; it is not called inside the
transaction. Unsupported selected stores or timer/preparation compositions refuse instead
of falling back to volatile postcommit dispatch. With no selected timeout store and no timer
intents, no artificial participant is required; configured completion includes cancel-all.

A committed redelivery does not run the handler again. An uncertain commit retains the same
prepared transition and identities for receipt reconciliation; it does not prove rollback.
Delivery remains at least once. Per-saga effect identity/ordinal metadata does not promise
global FIFO or completion order across producers. Continue using idempotent consumers and
avoid irreversible external operations inside a handler whose optimistic commit can lose.

## Handling Events for Missing Sagas

When a correlated event arrives for a saga instance that does not exist (already completed, expired, or never started), the coordinator invokes the registered `ISagaNotFoundHandler<TSaga>` instead of silently dropping the event. A default `LoggingNotFoundHandler<TSaga>` is registered out of the box, so the orphaned continuation is always logged.

Register a custom handler to dead-letter, park, or compensate the orphaned event:

```csharp
public sealed class OrderSagaNotFoundHandler : ISagaNotFoundHandler<OrderSaga>
{
    public Task HandleAsync(object message, string sagaId, CancellationToken cancellationToken)
    {
        // e.g. route to a dead-letter queue, raise a compensation, or alert
        return Task.CompletedTask;
    }
}

// Registration (ISagaBuilder fluent API):
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithNotFoundHandler<OrderSaga, OrderSagaNotFoundHandler>()));
```

`WithNotFoundHandler<TSaga>()` (no handler type) registers the default logging handler explicitly. Registration uses `TryAdd` semantics, so your custom handler replaces the default only when registered first. If no handler is resolvable, the coordinator falls back to a warning log (fail-open).

:::info Behavior change
`ISagaNotFoundHandler<TSaga>` existed but was never invoked — the saga-not-found branch only logged and returned. It is now resolved and called.
:::

## Recovering held timeout deliveries

Enable hosted delivery through the existing provider registration and `WithTimeouts()`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Excalibur.Saga.SqlServer.DependencyInjection;

public static class TenantSagaRegistration
{
    public static void Run(Microsoft.Extensions.DependencyInjection.IServiceCollection services, string connectionString)
    {
        services.AddTenantContext(options => options.RequireTenant = true); // Use the authorized ambient tenant for recovery operations.
        services.AddExcalibur(x => x.AddSagas(saga =>
            saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
                .WithCoordination()
                .WithTimeouts()));
    }
}
```

`AddTenantContext()` selects the framework ambient tenant context used by `TenantContextHolder.BeginScope`. Without it, the default single-tenant context does not switch to an arbitrary tenant ID merely because a scope is opened. Keep the configured tenant mode consistent with stored rows and authorize the tenant before establishing the scope.

Provision the selected physical timeout table before starting the host. Existing SQL Server tables require `07-SagaTimeoutFailureHolds.sql`; existing Oracle tables require `04-SagaTimeoutFailureHolds.sql`. Fresh provider `SagaTimeouts.sql` includes the hold fields. Configure each migration for the actual selected table, including custom tables, and follow its offline rollout instructions. These migrations supplement the state/ordinary timer recovery migrations described in the provider README; they do not replace them. The framework does not apply migrations automatically.

Before pumping, hosted delivery resolves `ISagaTimeoutFailureStore` from the selected `ISagaTimeoutStore` and validates usable storage. A custom store must expose and implement that capability; otherwise startup refuses with an actionable error. Registering an unrelated capability beside an unsupported selected store does not satisfy this gate. Existing standalone manual store APIs remain available on their documented legacy schemas.

Permanent, poison and Unknown failures retain the original timeout payload, identity and due date in held state. An unsuccessful result without typed failure evidence is Unknown; diagnostic text does not establish retryability. Transient and Uncertain classifications keep their existing retry behavior. Held rows are excluded from normal claims, while diagnostic reads remain available. A failed or uncertain hold write is not a delivery acknowledgment: the worker reconciles it, bounds local pending observations by `BatchSize`, and defers new dispatch when that capacity is full.

After repairing the underlying cause, an authorized operator can query and conditionally resume one held item. The following uses running host (`host`), `tenantId`, `timeoutId` and `cancellationToken` variables. Persist the chosen command ID and expected token for reconciliation rather than generating another command after an acknowledgment loss.

```csharp
using Excalibur.Saga.Abstractions;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

public static class HeldTimeoutRecoveryExample
{
    public static async Task Run(IServiceProvider serviceProvider, string tenantId, string timeoutId, CancellationToken cancellationToken)
    {

        using var tenantScope = TenantContextHolder.BeginScope(tenantId);
        var selected = serviceProvider.GetRequiredService<ISagaTimeoutStore>();
        var failures = ((IServiceProvider)selected).GetService(typeof(ISagaTimeoutFailureStore))
            as ISagaTimeoutFailureStore
            ?? throw new InvalidOperationException("Selected timeout store lacks hold support.");
        await failures.ValidateFailureStorageAsync(cancellationToken);
        var page = await failures.GetHeldFailuresAsync(tenantId, null, 100, null, cancellationToken);
        var expected = page.Items.Single(item => item.Timeout.TimeoutId == timeoutId);
        // Inspect expected.Kind, expected.Reason and expected.Timeout; authorize recovery.
        var commandId = Guid.NewGuid(); // Retain this ID with expected.Token before submitting.
        var resumed = await failures.TryResumeHeldFailureAsync(
            expected.Token, commandId, cancellationToken);
    }
}
```

Queries are bounded to 1–256 items per page and one exact tenant, with an optional exact saga ID. Follow the returned opaque cursor for subsequent pages; cursors are target/filter-bound and weakly consistent. Tokens and cursors are expected-state handles, not authorization credentials. Establish authorization and the correct tenant scope separately.

A null resume result means refusal, not success. Successful resume preserves original payload/ID/due and rotates the delivery cycle once; later normal claiming obtains a new lease. Repeat the same command and token to reconcile an uncertain acknowledgment. Old tokens cannot resurrect canceled, replaced or newer held work. Receipts are bounded to the retained source/latest cycle, not an indefinite command ledger.

SQL Server and Oracle store holds in their selected database table; the in-memory implementation reports `HoldsSurviveProcessRestart == false` and loses this state on process exit. A crash before the first hold commit can still permit redelivery: handlers need idempotency. This is at-least-once delivery, not exactly once. This feature does not establish full SQL/Oracle driver NativeAOT support or certify arbitrary backend alias, restore or failover equivalence.

## Persistence Providers

Each provider plugs into the `ISagaBuilder` fluent API:

```csharp
// SQL Server
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseSqlServer(sql => sql.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts()));

// PostgreSQL
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UsePostgres(pg => pg.ConnectionString(connectionString))
        .WithCoordination()
        .WithTimeouts()));

// Azure Cosmos DB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString("AccountEndpoint=...;AccountKey=...")
              .DatabaseName("myapp")
              .ContainerName("sagas");
    })));

// AWS DynamoDB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseDynamoDb(options =>
    {
        options.Connection.Region = "us-east-1";
        options.TableName = "sagas";
    })));

// MongoDB
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseMongoDB(mongo =>
    {
        mongo.ConnectionString("mongodb://localhost:27017")
             .DatabaseName("myapp")
             .CollectionName("sagas");
    })));

// Google Firestore
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseFirestore(options =>
    {
        options.ProjectId = "my-project";
        options.CollectionName = "sagas";
    })));
```

### On the document stores, the tenant is part of the saga's key

:::danger Upgrading a Cosmos DB, DynamoDB, Firestore or MongoDB saga store: the key shape changed
Saga documents written by an earlier version are **not addressable** by this one. Drain your in-flight
sagas before upgrading, or re-key them — see [Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the
tenant](../migration/nosql-tenant-key-rekey.md). This applies **even if you never enabled
multi-tenancy**: the key carries a reserved single-tenant or untenanted segment either way.
:::

A saga is correlated by a **business key** — an order number, a customer reference — so two tenants
legitimately run a saga at the same identifier. Keyed on that identifier alone they would be one
document, and the check that correctly refuses a cross-tenant overwrite would also refuse the second
tenant a saga of its own: an isolation control degenerating into an estate-wide uniqueness constraint,
surfacing to the second tenant as a concurrency failure on a saga it is creating for the first time.

The owning tenant is therefore part of the stored key on every provider:

| Provider | Identity as stored |
| --- | --- |
| SQL Server | `PRIMARY KEY CLUSTERED (TenantId, SagaId)` |
| PostgreSQL | `PRIMARY KEY (tenant_id, saga_id)` |
| Oracle | `PRIMARY KEY (TenantId, SagaId)` |
| MongoDB | `_id` = `t:{tenantId}:{sagaId}` |
| Cosmos DB | `id` = `t:{tenantId}:{sagaId}`; the partition key remains the saga type |
| DynamoDB | `PK` = `SAGA#t:{tenantId}:{sagaId}`; the sort key remains the saga type |
| Firestore | document id = `t:{tenantId}:{sagaId}_{sagaType}` |

The tenant segment is always present: a host with no tenancy resolves the framework single-tenant
default, and a genuinely untenanted saga resolves the reserved untenanted value.

**Each tenant gets its own saga document, and a cross-tenant write is unaddressable rather than merely
refused.** The ownership comparison each store performs after a fetch is retained on top of this — it is
what still applies to a document the store did not itself write — but it is not what makes coexistence
work.

A saga's tenant is bound at creation. **Saving an existing saga under a different tenant is refused, not
merged** — treat that failure as an escalation, not something to retry.

## Retention & Cleanup

Completed sagas remain in the store after they finish so you can audit or correlate them. To stop the store growing without bound, purge completed sagas older than a retention window. Purge-by-age is a first-class capability on **every** saga store — the in-memory store, the relational providers (SQL Server, PostgreSQL), MongoDB, and the document stores **Azure Cosmos DB, AWS DynamoDB, and Google Firestore**. Only sagas whose completion instant is older than the threshold are removed; in-flight sagas are never touched.

### Automatic background cleanup

`SagaOptions` drives a hosted background service that periodically purges completed sagas. It is off by default — opt in and it self-gates on the flag:

```csharp
services.AddExcalibur(x => x.AddSagas(saga =>
    saga.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString("AccountEndpoint=...;AccountKey=...")
              .DatabaseName("myapp")
              .ContainerName("sagas");
    })));

services.Configure<SagaOptions>(options =>
{
    options.EnableAutomaticCleanup = true;                 // default: false (no-op when disabled)
    options.SagaRetentionPeriod = TimeSpan.FromDays(30);   // delete completed sagas older than this
    options.CleanupInterval = TimeSpan.FromHours(1);       // how often the background service runs
});
```

| Option | Default | Meaning |
|--------|---------|---------|
| `EnableAutomaticCleanup` | `false` | Enables the background purge; when `false` the service is registered but a no-op. |
| `SagaRetentionPeriod` | `30 days` | Completed sagas older than this are deleted each cycle. |
| `CleanupInterval` | `1 hour` | How often the background service purges. |

### Manual purge

Call the store directly to drive your own retention schedule (a cron job, an admin endpoint). It returns the exact number of sagas removed, so you can log it or emit a metric:

```csharp
public class SagaRetentionJob(ISagaStore store, ILogger<SagaRetentionJob> logger)
{
    public async Task PurgeAsync(CancellationToken ct)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(-30);
        var removed = await store.PurgeCompletedBeforeAsync(threshold, ct);
        logger.LogInformation("Purged {Count} completed sagas older than {Threshold}", removed, threshold);
    }
}
```

`PurgeCompletedBeforeAsync` is an optional capability: a store that cannot purge by age throws `NotSupportedException` rather than silently returning `0`, so a missing capability fails loud instead of hiding from your retention scheduler.

## Builder Extensions

The `ISagaBuilder` fluent API provides optional capabilities:

| Extension | Purpose |
|-----------|---------|
| `.WithCoordination()` | Registers `SagaCoordinator` + `SagaHandlingMiddleware` |
| `.WithTimeouts()` | Enables timeout scheduling and delivery |
| `.WithInstrumentation()` | Adds OpenTelemetry tracing and metrics |

## What's Next

- [Orchestration vs Choreography](./orchestration-vs-choreography.md) — Compare centralized orchestration and decentralized choreography patterns
- [Outbox Pattern](../patterns/outbox.md) — Reliable message publishing
- [Inbox Pattern](../patterns/inbox.md) — Idempotent message processing
- [Event Sourcing](../event-sourcing/index.md) — Store state as events

## Correlating business keys

Declare typed selectors with `CorrelateBy<OrderSaga, OrderSubmitted>(message => message.OrderId)` on the saga builder. Messages sharing that literal business key reach the same declared saga. A publisher that needs the identifier can call `SagaCorrelation.ToSagaId<OrderSaga>(orderId)`; it uses exactly the same typed scope as the builder. Namespace, nested type, generic arguments and assembly simple name distinguish types, even when their short names and state type are the same. Assembly versions, strong-name keys and load contexts are intentionally excluded: types with the same logical assembly/type structure share this identity. Renaming or moving a saga/assembly changes this scope.

`SagaCorrelation.ToSagaId("explicit-stable-scope", key)` remains available for deliberately supplied scopes. Passing a simple class name is not equivalent to the typed overload; supplying its exact generated structural scope would reproduce the same typed ID. Inputs are case-sensitive, are not trimmed or Unicode-normalized, and may contain control characters such as U+001F. Ill-formed UTF-16 is rejected instead of silently replacing characters. The mapping hashes versioned UTF-8 byte-length frames and produces a UUIDv8; it is deterministic, not a proof of globally unique identifiers.

`CorrelateBy` composes declarations into the framework-owned mutable registry. A conflicting last unkeyed custom registry instance, type or factory now fails registration actionably instead of losing declarations. A custom registry remains supported on its own when it supplies correlation without `CorrelateBy`; keyed registrations stay separate.

### Derivation-version migration

The framed derivation and fully scoped automatic type identity change identifiers from the earlier delimiter/simple-name scheme. Upgrade publishers and coordinators together. If retaining existing state, drain or explicitly re-key in-flight sagas and related messages before switching; there is no silent old-ID fallback because the legacy representation can alias distinct inputs. Explicit identifiers assigned independently of `SagaCorrelation` are unaffected.
