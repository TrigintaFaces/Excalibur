---
sidebar_position: 2
title: What's New
description: What changed in Excalibur 10.0.0, grouped by subsystem, with everything you must do to upgrade.
---

# What's New

## 10.0.0 (pre-release)

**10.0.0 is the first release on the 10.x line.** It single-targets `net10.0`, and the major version tracks the .NET major it targets. The previously published line was `3.0.0-alpha`, so everything on this page arrives in a single step for anyone moving from it.

That is why this page is organised by **subsystem rather than by date**: no part of it shipped separately, so there is no chronology for you to navigate. If you are upgrading, read [Before you upgrade](#before-you-upgrade) first — it is the only section that requires action.

:::warning Known issues in this pre-release
This release has identified defects, including tenant-isolation and coverage gaps that affect what you can safely rely on. **[Read the known issues](./known-issues.md)** before depending on this release in production.
:::

---

## Before you upgrade

Everything on this page that requires you to change code, change a schema, or make a decision — collected in one place. Nothing else in this document needs action.

### Schema changes

These stores do not create their own tables, so an upgrade that skips these steps fails at runtime rather than at startup.

| Store | What to add |
| --- | --- |
| SQL Server outbox | `PartitionKey`, `GroupKey`, `SequenceNumber`, `NextAttemptAt` columns, plus the `IX_OutboxMessages_Claim` index — see the [outbox schema](./patterns/outbox.md#sql-server) |
| SQL Server inbox | `NextAttemptAt DATETIMEOFFSET NULL` — see [retry backoff schedule](./patterns/inbox.md#retry-backoff-schedule) |
| PostgreSQL outbox | `tenant_id` column; without it, staged messages fail with `column "tenant_id" does not exist` |
| Leader-fenced outbox (SQL Server, PostgreSQL, Oracle) | a fence control table (`OutboxFence` / `outbox_fence`) holding one monotonic high-water mark per scope. **Single-instance outboxes need no fence table.** |

### Stored data changes

| Store | What to do |
| --- | --- |
| Firestore inbox, Elasticsearch inbox | The document id that identifies an entry is composed differently, because the previous form could address two different messages with one id and drop the second as a duplicate. Entries written by an earlier version are not found by this version. **Drain the inbox before upgrading, or re-key the existing entries** — see [Firestore and Elasticsearch inbox keys change shape](./migration/inbox-document-id-rekey.md). Nothing fails at startup if you skip this; old entries simply stop being recognised, so a redelivered message is processed again. |
| Cosmos DB, DynamoDB, Firestore, MongoDB **event stores** | These four now compose the owning tenant into the document key, so they confine tenants and a multi-tenant host may register them. **Documents written by an earlier version have no tenant segment and are not addressable by the new key.** Nothing is destroyed — the events are still there and still readable by the earlier package version — but **the store now refuses rather than reading them back as an empty stream**: the first read that would have reported a false absence throws `InvalidOperationException` naming the collection and the offending key, and modifies nothing. That is deliberate: an empty stream would be taken for a new aggregate and appended at version 0, leaving two disjoint histories under one identity. **Re-key existing documents before you upgrade** — see [Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the tenant](./migration/nosql-tenant-key-rekey.md). Only the **event** documents change: the snapshot stores on these backends have been tenant-keyed since an earlier release, and a snapshot that misses is harmlessly rebuilt from the event stream. |
| Cosmos DB, DynamoDB, Firestore, MongoDB **saga stores** | The same change, on the same four providers, for saga documents. A saga document is keyed on the tenant composed with the saga identifier, because a saga identifier is a business correlation key that two tenants legitimately share — keyed on it alone they are one document, and the check that refuses a cross-tenant overwrite also refuses the second tenant a saga of its own. **Saga documents written by an earlier version are not addressable**, and these stores refuse the same way the event stores do: without the guard, a load reporting *no saga in flight* would make your coordinator start the saga again and **re-fire every compensating action and external call it had already performed**. **Drain in-flight sagas before upgrading, or re-key them** — see [the migration guide](./migration/nosql-tenant-key-rekey.md). The relational saga stores are unaffected; their tenant is already part of the primary key. |
| Cosmos DB, DynamoDB, Firestore, MongoDB authorization grants | Grants stored with no tenant were filed under a reserved literal that was part of the partition key or document id, so correcting one is a delete-and-reinsert rather than an update. **Rewrite or delete those grants before you upgrade** — see [Authorization grants require a tenant](./migration/authorization-tenant-required.md). Nothing fails at startup if you skip this; the affected grants simply read back carrying the literal `__null__` as their tenant on Cosmos DB, DynamoDB and Firestore, and a null tenant on MongoDB. Each provider stores grants in **two** containers — the guide names both. |

### A handler that reads the message context must now say so

The dispatcher no longer publishes a message context to handlers that never asked for one — that
publication was measurable overhead on every dispatch, paid in case somebody was reading. A handler
declares that it reads the context by exposing a settable `IMessageContext` property, or by
injecting `IMessageContextAccessor`.

Two things to check before upgrading:

| If your handler… | What happens now | What to do |
| --- | --- | --- |
| injects `IOutboxWriter`, or anything else that reads `IMessageContextAccessor`, but declares no context of its own | throws on first use, with a message naming the missing context | inject `IMessageContextAccessor` (or expose an `IMessageContext` property) on the handler as well |
| calls `DispatchAsync(message, cancellationToken)` for a follow-up message and relies on the causal chain | the follow-up starts a **new root** instead of a child — nothing throws | declare the context as above, or pass the parent explicitly with `DispatchAsync(message, context, cancellationToken)` |

Handlers that already take a context, and everything running through middleware, are unaffected.

### Removed APIs

| Removed | Use instead |
| --- | --- |
| `UseDispatch(...)` | [`AddDispatch(...)`](./handlers.md) — identical behaviour. `Add*` registers services, `Use*` is reserved for pipeline ordering, matching the Microsoft convention |
| `Kind` on `CommandBase`, `JobBase`, `NotificationBase`, `QueryBase<TResponse>`, `MemoryMessage`, `CloudEventMessage`, `GenericDispatchMessage`, `TimerInfo` | a type check — `message is IDispatchEvent` rather than `message.Kind == MessageKinds.Event` |
| `AggregateId` and `Version` on `IDomainEvent` / `DomainEvent` | the aggregate id is an explicit parameter to `AppendAsync`/`LoadAsync`; the version is assigned by the store and surfaced on `HistoricEvent.Version` / `StoredEvent.Version`. Delete any `public override string AggregateId => …` from your event records — there is nothing left to override. See [domain events](./event-sourcing/domain-events.md) |
| `EventType` on `IDomainEvent` / `DomainEvent`, and `EventBuilder.WithEventType(...)` / `TestDomainEvent.EventType` in the testing package | the required `[MessageName]` attribute. Delete any `public override string EventType => …` from your event records and put the name in the attribute instead; where you set a name in a test builder, declare it on the test event type. See [stable message names](./event-sourcing/domain-events.md#stable-message-names) |
| `IOutboxBulkCleanup.BulkCleanupFailedMessagesAsync` | the dead-letter and retry paths. Bulk-deleting failed messages discarded records that still needed inspection. `BulkCleanupAllTenantsSentMessagesAsync` is unchanged |
| `TransportDeliveryStatistics.SendingCount` (always `0`) | `OutboxStatistics.SendingMessageCount`, derived from active leases |
| `IRabbitMqStreamConsumer`, `RabbitMqStreamOptions`, `StreamOffset`, `AddRabbitMqStreamQueues` | the standard RabbitMQ queue transport. These registered no working consumer — if you referenced them, they were non-functional |
| `AddComplianceEncryption<TKeyManagement>(...)`, `AddComplianceEncryptionWithRotation(...)` | one fluent builder: `AddComplianceEncryption(e => e.WithInMemoryKeyManagement().WithEncryption().WithKeyRotation())` |
| `KeyedTenantPartition.FromContext(ITenantContext?)` and `TenantScope.FromContext(ITenantContext?)` — the null-accepting overloads | the non-nullable `FromContext(ITenantContext)`. If your context is optional, choose the fallback yourself: `ctx is null ? KeyedTenantPartition.Untenanted : KeyedTenantPartition.FromContext(ctx)`. The old form silently turned *"no context was wired"* into *"this row has no tenant"* — see [multi-tenancy](./multi-tenancy.md#keyed-stores-always-bind-a-tenant-term) |
| `DispatchHealthCheckOptions.IncludeSaga` | nothing — it referenced a deleted service |
| `MessageContextHolder` (now `internal`) | declare that you want the context instead of reaching for it: a handler exposes a settable `IMessageContext` property or injects `IMessageContextAccessor`; a service that has no dispatch-time parameter injects `IMessageContextAccessor`. Declaring it is what lets the dispatcher skip publishing a context to handlers that never read one — see [how `DispatchAsync` behaves by context](./handlers.md#how-dispatchasync-behaves-by-context) |
| `ISagaReminder` (now `internal`) | `ISagaBuilder` extensions — `.WithReminders()` |
| `ISagaOutboxMediator`, `SagaOutboxOptions`, `.WithOutbox()` | nothing — saga messages are dispatched after the state commit and are not part of it, so an outbox at that seam could not have made them durable |
| `ISagaStateMigrator<TFrom,TTo>`, `SagaVersionAttribute` | nothing — no migrator was ever implemented, and the interface was `internal` so a consumer could not write one |
| `WithOrchestration()` | `WithCoordination()` — the saga model is event-driven coordination, not step-based orchestration |
| Elastic/OpenSearch SDK types on index-management models | `JsonElement?` — serialize SDK objects first: `SettingsJson = JsonSerializer.SerializeToElement(new IndexSettings())` |
| `ITenantResolver`, `DefaultTenantResolver`, `TenantContextHolder.TenantIdItemKey` | the ambient tenant scope — `TenantContextHolder.BeginScope(tenantId)`, read back through `ITenantContext`. **The removed resolver never resolved a tenant**: the default read a message-context item nothing in the framework ever wrote, so it returned `TenantContextOptions.DefaultTenantId` for every message, and a resolver you registered yourself was never called at all. If you registered one, any tenant scoping you believed it was doing was not happening — check the code that depended on it. Authorize the tenant before scoping to it. See [multi-tenancy](./multi-tenancy.md) |
| `IPersistenceProviderFactory`, `PersistenceProviderFactory`, `IConfigurableProvider`, `PersistenceProviderOptions`, `PersistenceProviderType`, `IPersistenceConfiguration`, `PersistenceConfiguration`, `IPersistenceHealthCheck`, and the `AddPersistence(...)` overloads taking a configuration callback or an `IConfiguration` | keyed dependency injection — `GetRequiredKeyedService<IPersistenceProvider>(key)` or `[FromKeyedServices(key)]`, where `key` is the fixed key the provider's package registers (`"sqlserver"`, `"postgres"`, `"inmemory"`). The non-keyed `IPersistenceProvider` still resolves and still forwards to `"default"`, so a single-provider host needs no change. `AddPersistenceHealthCheck` now takes the keyed-DI key — the parameter is renamed to `providerKey`. **Also check your configuration** — see [configuration that stops being read](#configuration-that-stops-being-read) |
| The null-accepting authorization grant tenant — `Grant.TenantId`, `IActivityGroupGrantStore.InsertActivityGroupGrantAsync`, `IActivityGroupStore.CreateActivityGroupAsync` | the same members taking a non-nullable `string`. Constructing a grant without a tenant is now a compile error; supply the tenant, or the explicit untenanted value if the grant genuinely spans tenants. A payload or provisioning request omitting the tenant is rejected rather than filed under a sentinel. **If you already stored grants with no tenant, they need backfilling before you upgrade** — see [Authorization grants require a tenant](./migration/authorization-tenant-required.md) |
| `IInboxStoreAdmin.CleanupAsync` | `CleanupAllTenantsProcessedEntriesAsync`. **Read the note below before you rename this one** — it deletes across every tenant and always did |
| `IInboxStoreAdmin.GetAllEntriesAsync` | `GetAllTenantsEntriesAsync` — returns full entries, payloads included, for every tenant |
| `IInboxStoreAdmin.GetStatisticsAsync` | `GetAllTenantsStatisticsAsync` |
| `IOutboxStoreAdmin.GetStatisticsAsync` | `GetAllTenantsStatisticsAsync` |
| `IOutboxStoreAdmin.GetFailedMessagesAsync` | `GetAllTenantsFailedMessagesAsync` |
| `IOutboxStoreAdmin.GetScheduledMessagesAsync` | `GetAllTenantsScheduledMessagesAsync` |
| The long-parameter-list constructors on `EventSourcedRepository<TAggregate>` and `EventSourcedRepository<TAggregate, TKey>` — the ones taking `IOptions<UpcastingOptions>`, `IOptions<SnapshotUpgradingOptions>` and `OutboxStagingStrategy` as separate parameters | the constructor taking a single `IOptions<EventSourcedRepositoryOptions>`, which carries all three settings (`EnableAutoUpcast`, `EnableAutoSnapshotUpgrade` / `TargetSnapshotVersion`, `OutboxStagingStrategy`). **Only affects hand-construction** — the registration extensions already used the options constructor, so resolving `IEventSourcedRepository<...>` from dependency injection needs no change |
| `IVersionedProjectionStore<T>` and `VersionedProjection<T>` | nothing — no projection store ever implemented the interface, so the documented `store is IVersionedProjectionStore<T>` test never matched and the two versioned methods were unreachable. **If you wrote that test, the branch never ran** — delete it, and check whether code behind it was silently skipped. Optimistic concurrency on a projection read path is not offered in its place: projections are engine-owned, and the engine is the sole writer during event processing. If you edit a projection outside event processing and need a concurrency check, keep a version field on the projection itself and write conditionally through your provider's own client |

**About those six renames.** Estate-wide operations here are reached by *name*, never by passing a wildcard tenant value — a name cannot be arrived at accidentally from a value that flowed in from somewhere else, and a wildcard can. These six did not follow that rule, so code holding one tenant's context could call them and reach every tenant with nothing at the call site to say so. **The scope did not change in this release; only the name did.** Renaming the call is the entire migration — no signature, argument, return type, or behaviour changed, and the compiler finds every site.

`CleanupAsync` is the one worth a second look. It is an unbounded delete of processed entries older than a cutoff, with no tenant term in any provider. If you call it per tenant believing it sweeps only that tenant, it has been sweeping the whole estate on every call — so check the schedule you run it on, not just the call site. If any renamed call turns out to be one you did *not* want estate-wide, the rename surfaced an existing defect rather than creating one.

### Configuration that stops being read

The removals above fail your build. This one does not — it lives in `appsettings.json`, where nothing checks it.

**Check your `appsettings.json` for a `Persistence:Providers` section.** If you have one, delete it — and first confirm the settings in it are also set through each provider's own `Add…` extension. Nothing in that section has ever reached a provider, including `ConnectionString`. Your providers have been running on their own configuration; removing the section changes no behaviour, but it may reveal a setting you believed was applied and never was.

### Changed defaults

Each of these changes behaviour without changing an API, so a build that still compiles can still behave differently.

- **Starting scheduled delivery on the in-memory schedule store is refused at startup.** The compositions that start the scheduler runtime — `AddTimeAwareScheduling`, `AddAdaptiveTimeAwareScheduling`, `AddLightweightTimeAwareScheduling`, `AddThroughputTimeAwareScheduling` — now install a durability gate, so a host backed by the volatile store fails at boot instead of silently forgetting pending schedules on restart. Register a durable store with `AddDurableScheduleStore<T>()`, or accept the volatile one for development and test hosts with `ScheduleDurabilityOptions.AllowVolatileScheduleStore = true`. **Composing scheduling alone with `AddDispatchScheduling` is unaffected and stays gate-free** — the refusal belongs where deliveries start being owed. See [scheduled delivery durability](./core-concepts/configuration-advanced.md#advertised-capabilities-are-wired-or-fail-loud).
- **A durable schedule store registration now wins regardless of composition order.** `AddDurableScheduleStore` registered with `TryAdd`, so composing scheduling first made it a silent no-op while the durability attestation was still emitted — a host that asked for a durable store passed the gate and ran on the volatile one. If you register both, confirm which store you are on.
- **Windows FIPS status is read from the host policy.** Detection previously read a runtime property that .NET Core and later pin to `false`, so every Windows host reported *not FIPS compliant* — including one genuinely running under FIPS policy, and this result feeds a SOC 2 encryption control validator. **Evidence generated on a Windows host you believe is FIPS-enabled should be re-generated.** An unreadable policy is now distinguished from a disabled one: it reports not-enabled with validation details stating compliance is *unconfirmed*. See [FIPS 140-2 compliance](./security/encryption-architecture.md#fips-140-2-compliance).
- **The OpenSearch projection store uses an `IOpenSearchClient` you registered.** `AddOpenSearchProjectionStore` built a client from `NodeUri` unconditionally, so a registered client was ignored and the store quietly addressed the default local node. It now prefers a registered client and falls back to `NodeUri` only when none exists. See [OpenSearch](./data-providers/opensearch.md#client-registration).
- **An audit query filtering on an encrypted field is refused instead of returning nothing.** If you enable `UseAuditLogEncryption()`, `ActorId` and `IpAddress` are encrypted by default, and the cipher is randomized — two records holding the same actor id hold different ciphertext, so no equality comparison can find either. `QueryAsync` and `CountAsync` now throw `NotSupportedException` naming the field and the option, rather than returning an empty list or a zero that reads as *"this actor did nothing"* while the records sit present and unmatchable. **This means that with encryption on, actor-scoped and address-scoped audit queries are gone, not degraded.** Set `AuditEncryptionOptions.EncryptActorId = false` (or `EncryptIpAddress = false`) to store that field in the clear and keep it searchable. `EncryptReason` and `EncryptUserAgent` cost you nothing — there is no query filter over either. See [audit logging → encryption and querying](./compliance/audit-logging.md#encryption-costs-you-the-ability-to-query-by-that-field).
- **Compliance stores verify their schema instead of creating it.** `AutoCreateSchema` now defaults to `false` for the erasure, legal-hold, and data-inventory stores on SQL Server and PostgreSQL — they fail fast when tables are missing, matching an application identity that holds no DDL rights. The setting is **per store**, so set it on each one you want to keep provisioning. See [GDPR erasure → database schema](./compliance/gdpr-erasure.md#database-schema).
- **Event-type assembly scanning is opt-in.** `JsonEventSerializer` no longer scans loaded assemblies to resolve an unregistered event type; an unknown name throws `UnknownEventTypeException` rather than being resolved by an unbounded reflection scan that could land on an attacker-chosen type. Types registered with `AddEventTypes<T>()` are unaffected. Restore the old behaviour with `new JsonEventSerializer(allowAssemblyScan: true)`.
- **Retry classifies failed *results*.** `RetryMiddleware` retries a failed `IMessageResult` only when its RFC 7807 status is transient (`408`, `429`, `5xx`). Previously it retried *every* non-success result, re-running non-idempotent handlers on permanent client errors. A failed result with no `ProblemDetails`/`Status` is treated as permanent. **Exception-based retry is unchanged.** See [retry middleware](./middleware/built-in.md#retry-middleware).
- **`AddDefaultDispatchPipelines()` builds clean out of the box** — it declares no security middleware as `Required`. For a host that refuses to start without authentication, authorization, and validation registered, call **`AddStrictDispatchPipelines()`**.
- **MessagePack deserializes untrusted input safely** — `MessagePackSecurity.UntrustedData` by default, and System.Text.Json enforces a bounded depth.
- **The HashiCorp Vault credential store is no longer registered by default.** `AddSecureCredentialManagement` registers `EnvironmentVariableCredentialStore`, and wires Vault only when `Vault:Url` is configured. Cloud stores move to `AddDispatchSecurityAzure(...)` / `AddDispatchSecurityAws(...)`. **Use `https` for any non-loopback Vault URL** — a plaintext endpoint transmits your token in the clear.
- **Serializers fail loud rather than returning null.** `DispatchJsonSerializer` throws `SerializationException` on an empty payload and on a `null` result for a non-nullable type; write-path failures are wrapped rather than escaping as raw provider exceptions. Claim-check payloads serialize with the framework camelCase policy by default — see [claim check](./patterns/claim-check.md#payload-serialization) and [MessagePack](./middleware/serialization-providers.md#messagepack).
- **`AppendResult.FirstEventPosition` is `long?`** — `null` for stores with no global sequence, instead of an ambiguous sentinel.
- **`DispatchAsync` propagates handler exceptions** instead of silently wrapping them in `MessageResult.Failed()`.
- **The four self-hosted transports refuse a plaintext broker by default.** gRPC, IBM MQ, MQTT and Pulsar all address brokers that can be reached in the clear, and none of them previously let you say that was unacceptable. Each now carries `RequireTls`, on by default, and raises `TransportSecurityException` when the transport is resolved — while the host is starting, not on the first message. To keep an unencrypted connection, opt out on that transport: `GrpcTransportOptions.RequireTls`, `IbmMqOptions.RequireTls`, `MqttOptions.RequireTls`, or `pulsar.RequireTls(false)`. To satisfy it, use an `https` gRPC address, an IBM MQ `SslCipherSpec`, `MqttOptions.UseTls`, or a `pulsar+ssl://` service URL. See [MQTT](./transports/mqtt.md#transport-security), [IBM MQ](./transports/ibm-mq.md#transport-security) and [Pulsar](./transports/pulsar.md#transport-security).

---

## Reliability

### Outbox

- **Messages keep their order.** Each message stores `PartitionKey`, `GroupKey`, and a monotonic `SequenceNumber`, and the claim query selects in `(PartitionKey, SequenceNumber)` order — messages sharing a partition key are delivered in ascending sequence.
- **Retry backoff is genuinely applied.** The computed backoff was previously calculated and never used, so a failed message was re-claimed as soon as its lease expired. The next-attempt time is now recorded and the claim predicate excludes the message until it elapses. A circuit-breaker short-circuit is excluded, since no delivery was attempted. Implemented on SQL Server and PostgreSQL through `IBackoffSchedulableOutboxStore`; other stores fall back to immediate retry. See [ordering and retry scheduling](./patterns/outbox.md#ordering-and-retry-scheduling).
- **A retry-exhausted message reaches a terminal state.** Exhausted messages previously stayed `Failed`, were re-claimed after lease expiry, and were re-delivered and re-dead-lettered indefinitely. They now transition to `OutboxStatus.DeadLettered`, which every store's claim predicate structurally excludes.
- **Fenced drain and mark-sent are atomic** on PostgreSQL and Oracle — a single-statement compare-and-swap, so a demoted leader whose token has been superseded cannot claim or delete between the check and the write. SQL Server records its fencing high-water mark in a durable control table advanced by a serializable `MERGE`, closing the split-brain window that existed when the mark was derived from `MAX(FencingToken)` over rows cleanup could purge. *(Outbox delivery is at-least-once; this removes one duplicate source rather than making it exactly-once.)*
- **Tenant and causation survive the transport hop.** `TenantId` and `CausationId` were dropped when the outbox handed a message to the transport, breaking multi-tenant routing and cause-effect tracing.
- **Elasticsearch cleanup no longer deletes the entire outbox** — the query is bounded to already-sent messages older than the cutoff, replacing a match-all delete that could wipe live unsent messages.
- **The PostgreSQL claim uses `FOR UPDATE SKIP LOCKED`** and the **Redis claim is a single atomic Lua lease-claim**, so concurrent processors cannot double-claim.
- **`SupportsSentTracking`** reports whether a store retains a sent message as a countable row, so statistics and cleanup stop assuming one uniform storage model.

### Inbox

- **Provider-native transactional inbox.** The duplicate check, your handler, and the processed-mark run inside one native transaction, closing the crash window that leaves the two-step claim protocol at at-least-once. **Always on** for SQL Server and PostgreSQL — enlist your own writes with `scope.AsSqlTransaction()`. Opt-in for MongoDB (`EnableTransactions`, replica set) and Cosmos DB (`SharedPartitionKey`), enlisting via `AsMongoSession()` / `AsCosmosBatch()`. Where not configured, the middleware falls back transparently to the idempotent claim path. See [inbox pattern](./patterns/inbox.md#provider-native-transactional-inbox-sql-server-postgresql-mongodb--cosmos-db).
- **The at-most-once guard is live.** The `Processing` status was previously set in memory only, so the concurrency guard and the stuck-processing timeout had no durable state to act on. It is now persisted before your handler runs.
- **Retry honours exponential backoff** instead of a hardcoded five-minute window.
- **Stores that cannot honour an atomic claim fail loud at startup** rather than silently degrading to check-then-act.
- **Elasticsearch cleanup respects the cutoff** — it previously deleted every inbox document regardless of age.
- **The in-memory deduplicator fails closed at capacity.** See [idempotency under load](./patterns/idempotent-consumer.md#idempotency-under-load). At capacity a claim that cannot be tracked is denied and the operation throws `DeduplicationCapacityExceededException`, so the message is redelivered rather than admitted without deduplication. Capacity is `InMemoryDeduplicatorOptions.MaxEntries` (default 100,000; `0` = unbounded).

> **The delivery contract, stated precisely:** **exactly-once for *concurrent* redelivery** (an atomic claim blocks the second caller) and **at-least-once across a *process crash*** where the store is not transactional. Handlers must be idempotent to be safe across a crash. See the [idempotent consumer guide](./patterns/idempotent-consumer.md).

### Sagas

- **Save-then-dispatch.** A saga's emitted commands and events are buffered during `HandleAsync` and dispatched only after state is durably persisted. Previously a command was dispatched first and a persistence failure followed by replay re-dispatched it, duplicating side effects. Per-emit FIFO order is preserved. See [sagas](./sagas/index.md#optimistic-concurrency).
- **Optimistic concurrency and a no-resurrect guard across all providers** — in-memory, SQL Server, PostgreSQL, MongoDB, Cosmos DB, DynamoDB, and Firestore, each using its native mechanism. A conflicting save throws `ConcurrencyException` so exactly one writer wins; a stale-version save against a completed or deleted saga is rejected rather than re-creating a zombie row. Handle it by reloading and replaying — the idempotent-replay guard makes that safe.
- **A completed saga is never re-run**, and **`ISagaNotFoundHandler<TSaga>` is invoked** when an event arrives for a saga that does not exist, so an orphaned continuation can be dead-lettered or compensated instead of dropped.
- **Retention purge works on every store**, document stores included. Configure `EnableAutomaticCleanup`, `SagaRetentionPeriod`, `CleanupInterval`, or drive `PurgeCompletedBeforeAsync` yourself.
- **`ISagaTimeout<TMessage>`** declares strongly-typed timeout handlers, routed directly to `HandleTimeoutAsync`. A saga may implement several for different timeout types.

### Projections

- **A poison event is never silently skipped.** An event that fails to deserialize, deserializes to `null`, or throws from `ApplyAsync` halts the batch and **does not advance the checkpoint**, so it is reprocessed and transient failures self-heal. Previously such events were logged, skipped, and the checkpoint moved past them — silent read-model drift. The one-shot rebuild service fails the rebuild instead of skipping.
- **An *apply* failure in the continuous host is recorded rather than halting**, because many projections share one checkpoint and halting would force the projections that succeeded to re-apply the event.
- **Cursor map is saved before the checkpoint advances**, so a crash cannot leave the checkpoint ahead of a durable cursor map.
- **DynamoDB and Firestore honour query filters.** Both previously ignored the `filters` argument entirely and returned over-broad result sets. An untranslatable filter now throws `NotSupportedException` rather than silently returning everything.
- **Aggregate rehydration fails loud** on an undeserializable event rather than reconstructing a silently-incomplete aggregate. See [projections](./event-sourcing/projections.md) and [global stream projection host](./event-sourcing/global-stream-projection-host.md#error-handling).

### Change data capture

- **Single-active CDC with leadership fencing.** With an `ILeaderElection` provider registered, only the elected leader advances the change feed and every checkpoint write is guarded by a monotonic fencing token; a superseded instance is rejected with `CdcLeadershipSupersededException`. Without a provider, CDC runs single-instance exactly as before. See [change data capture](./patterns/cdc.md).
- **The checkpoint never advances past an unprocessed change** — every provider routes its per-iteration decision through one shared guard.
- **Fatal-error handling is uniform** across all six providers, and **idempotency filtering** is available in-memory or persisted — see [idempotency filtering](./patterns/cdc.md#idempotency-filtering) and [CDC troubleshooting](./operations/cdc-troubleshooting.md).

### Caching

- **`CacheResilienceOptions` is actually wired into the cache pipeline.** The circuit-breaker and fallback settings were advertised configuration that never engaged. Alongside it, negative-result cache poisoning and hit/miss recording are fixed.
- **Caching fails open on tag-store errors.** A tag-store failure during tag registration or poison-marker cleanup is logged and skipped — it never breaks core message dispatch. Tag-tracker registration is atomic. See [caching](./performance/caching.md).
- **`ICacheable.ShouldCache` is honoured**, so a handler result can opt out of caching per invocation instead of being cached unconditionally.

### Resilience

- **The transport circuit breaker registry is bounded at 1024 distinct circuits.** Past the cap, a new key shares one overflow circuit rather than allocating another, so a message-derived key cannot grow the registry without bound. Protection is preserved but coarser — one failing key can open the circuit for every other key in the overflow. If you set `CircuitBreakerOptions.CircuitKeySelector`, return a bounded set of keys: bucket by route family or tenant tier rather than returning a raw tenant id. See [Polly resilience](./operations/resilience-polly.md#transport-circuit-breaker-registry).
- **A rate-limited Elasticsearch cluster is retried instead of failing on the first attempt.** A response the cluster returned unsuccessfully carrying 429, 502, 503 or 504 previously matched no retry rule, so it got no backoff at all — from the client whose job is resilience. It is now judged by the same status-code rule as a thrown transport failure reporting that status.
- **A timed-out Elasticsearch operation reports the timeout as its direct `InnerException`.** It was previously wrapped twice, so `catch (ElasticsearchSearchException ex) when (ex.InnerException is TimeoutException)` could not distinguish a timeout from a query failure. That pattern works now.
- **Retry jitter uses `Random.Shared`**, supplied by the backoff calculators rather than a separate source in the retry middleware. Backoff shape and range are unchanged.
- **Error-rate auto-degradation uses a sliding window.** It previously used process-lifetime totals, so an ever-growing denominator meant a recent burst of failures could no longer move the ratio — auto-degradation effectively stopped firing after warm-up.
- **The distributed circuit breaker recovers from Half-Open to Closed** after `SuccessThresholdToClose` consecutive successes, instead of getting stuck Half-Open.
- **`BulkheadPolicy.MaxQueueLength` is a hard atomic bound** — concurrent callers can no longer overshoot it through a stale check-then-act gate.
- **Backoff can no longer overflow**, and **`DecorrelatedJitter`** joins `FullJitter` for smoother, less-correlated growth.
- **Opt-in auto-dead-letter on retry exhaustion** via `AddDeadLetterOnExhaustion()`. An `IDeadLetterQueue` is required — a host without one throws on first resolve rather than silently discarding. Discarding stays available as an explicit choice. See [dead letter](./patterns/dead-letter.md#auto-dead-letter-on-retry-exhaustion) and [Polly resilience](./operations/resilience-polly.md).

---

## Multi-tenancy

- **An ambient tenant context** via `AddTenantContext()`, resolved from message items with a configurable fallback. `RequireTenant = true` makes a missing tenant fail fast with `TenantRequiredException`.
- **First-class persistence isolation** from one `AddMultiTenancy(o => o.Strategy = …)` call — either `RowDiscriminator` (tenant-scoped decorators over shared stores) or `Sharding` (per-tenant physical stores). **Fail-closed by construction:** selecting a strategy without the stores or routing it needs throws at composition time rather than leaving stores silently unscoped.
- **Tenant identity survives every hop** — first-class on the transport context and copied by every transport mapper independently of headers, and persisted across the outbox stage and scheduled paths.
- **Inbox reads and claims derive their tenant predicate from the ambient context and fail closed** when a tenant is active but unresolved.
- **Erasure requests and legal holds are tenant-scoped**, on SQL Server, PostgreSQL and in-memory alike. The tenant term comes from the ambient context through a single derivation point, and a caller-supplied tenant is **ANDed onto** it rather than replacing it — so the argument can only narrow a result, never widen it. Both contracts are in the set `AddMultiTenancy()` checks, so a multi-tenant host registering an unscoped implementation fails at startup rather than leaking at runtime.

  Reading and mutating a hold are deliberately asymmetric: a tenant **sees** an estate-wide hold, because it blocks that tenant's erasures, but cannot **modify** one — otherwise a tenant could re-home an estate-wide preservation order into its own partition and silently lift it for every other tenant. Background sweeps that expire holds and drain scheduled erasure requests stay estate-wide by design; scoping them would stall erasure for other tenants and make expired holds permanent.
- **Leader-election leases can be tenant-scoped** and fail closed via `CreateTenantScopedElection`.

See [multi-tenancy](./multi-tenancy.md), and the [known issues](./known-issues.md) for the isolation gaps that remain.

---

## Event sourcing and durable execution

### Durable execution (workflows)

Define a replayable workflow whose progress survives process restarts. Steps run through journaled activities, so a crashed workflow resumes without re-running completed steps — exactly-once per step, with single-writer optimistic concurrency.

```csharp
services.AddWorkflows();
services.AddActivity<ChargeCard, ChargeRequest, ChargeResult>("charge");
services.AddWorkflow("checkout", async (ctx, input, ct) =>
{
    var charge = await ctx.CallActivityAsync<ChargeResult>("charge", input, ct);
    return charge;
});
```

`IWorkflowContext` supplies the full determinism surface — journaled time (`UtcNowAsync`), identifiers (`NewGuidAsync`), durable timers (`CreateTimerAsync`), and external signals (`WaitForSignalAsync`) — so non-deterministic work replays deterministically. The opt-in analyzer package flags non-deterministic calls inside a workflow body at build time and rewrites them.

`Excalibur.Workflows.SqlServer` adds a **restart-durable signal inbox**: each `(instanceId, signalId)` persists with idempotent dedup, so a producer's post-restart redelivery is admitted exactly once. `RequireDurableSignalInbox()` fails host startup when only the in-memory inbox is wired, turning "signals silently lost on restart" into a startup error. See [durable execution](./event-sourcing/durable-execution.md).

### Event store

- **Every event declares its own name with `[MessageName]`.** `[MessageName("Contoso.Orders.OrderCreated")]` is now required on every event type you register — registration throws, naming the type, if it is missing. That one name is what the event store writes, what the outbox reports as `MessageType`, and what external subscribers see as the CloudEvents `type`; all three previously derived a name of their own from the CLR type. Your event types are now free to move between namespaces, assemblies and assembly versions without changing the identity of anything already stored or any filter your subscribers wrote. To retire a name, keep it as `[MessageNameAlias("...")]` — repeatable, read-side only, so data written under it stays readable while new events are written under the current name. For a name you cannot put in an attribute, such as the assembly-qualified name an earlier version wrote, register it with `RegisterEventTypeAlias(storedName, typeof(YourEvent))`. See [stable message names](./event-sourcing/domain-events.md#stable-message-names).
- **Transactional event + outbox staging is real.** `OutboxStagingStrategy.Transactional` atomically appends events and stages outbox messages in one transaction on stores that support it. The store owns the connection and transaction, runs the concurrency check, appends, invokes your staging on that *same* transaction, then commits — the transaction never escapes the store, so events and their outbox rows can never land on two different transactions. With SQL Server and an `ITransactionalOutboxWriter` registered, `Auto` resolves to `Transactional`. Selecting `Transactional` without the required infrastructure now fails at startup instead of degrading silently.
- **Aggregate handlers (Decider)** route a dispatched command straight to an event-sourced aggregate — resolve identity, load, decide, save with optimistic concurrency — with no handler class and no reflection. A handler can stage follow-up messages to the outbox by returning a result implementing `ICascade`. See [aggregate handlers](./event-sourcing/aggregate-handlers.md).
- **Right-to-erasure is honoured through the whole decorator chain** — telemetry, metrics, encrypting, and tenant-scoping decorators all delegate the erase inward, so a decorator can no longer strip the capability by not re-implementing it. Under multi-tenancy the erase is fail-closed and refuses an unscoped erase.
- **`WithSearchText`** computes a denormalized search field automatically on update.
- **SQL Server range queries execute against the real schema** — `ReadRangeAsync` referenced a non-existent column and threw at runtime during parallel catch-up, masked by in-memory-only tests.
- **SQLite reports a concurrency conflict** in the same shape every other event store returns, rather than surfacing a lower-level connection exception.

---

## Providers

### Oracle Database

The reliable-persistence subsystems run on Oracle through four opt-in Dapper-based packages behind the existing abstractions — event store with snapshots, outbox, inbox, and saga. Application code is unchanged; only registration differs.

```csharp
services.AddOracleEventStore(() => new OracleConnection(connectionString));
```

Appends read their assigned positions back per row, the outbox round-trips every consumer-supplied field, and saga `Guid` identifiers round-trip as `RAW(16)`. See [Oracle provider](./data-providers/oracle.md).

### Google Cloud Spanner

`Excalibur.Data.Spanner` ships the connection foundation with retryable-transaction support. **Persistence stores on Spanner are not yet available.** See the [data providers index](./data-providers/index.md). See [Spanner](./data-providers/spanner.md).

### Every provider is held to one contract

Snapshot, event, outbox, and inbox stores are each held to a shared conformance suite for their family
— the same behavioural facts (round-trip fidelity, version and concurrency semantics, idempotent claims),
on real infrastructure, using each provider's **default** serializer and client (see
[event sourcing providers](./event-sourcing/providers.md)). This closes the gap where a provider could
compile and pass its own unit tests while diverging from the contract on a real server.

**Coverage is per family, not uniform, and the difference matters when you are choosing a provider.**
Snapshot stores are the broadest: nine providers — SQL Server, PostgreSQL, SQLite, Redis, MongoDB,
Cosmos DB, DynamoDB, Firestore and Oracle — run the snapshot suite. **Outbox stores run a different
suite with a different provider set**: SQL Server, PostgreSQL, Oracle, Redis, MongoDB, Marten and
Elasticsearch. **SQLite, Cosmos DB, DynamoDB and Firestore have no outbox conformance suite at all.**
If you are choosing one of those four for the outbox specifically, that behaviour is unverified by us
and you should verify it against your own deployment.

The suites are also not literally one shared base class across all four families — outbox providers
derive the conformance kit we ship, while snapshot providers derive an internal base that is not part
of the published surface. See [known issues](./known-issues.md) for what that means if you plan to
validate a custom provider against a shipped kit.

**The PostgreSQL dead-letter store can list its messages.** `IDeadLetterStore.GetMessagesAsync` built a
statement whose paging clause ran together with the preceding one, so **every** call failed with a syntax
error — including a call against an empty store, and regardless of what you filtered on. Listing,
filtering, and paging dead-lettered messages on PostgreSQL now work as described in
[dead letter queue](./patterns/dead-letter.md#supported-providers). The other operations on that store
were unaffected, and no other provider had the defect.

Related correctness work: **persisted Cosmos documents are serializer-agnostic** (dual-annotated for System.Text.Json and Newtonsoft, so a consumer-injected `CosmosClient` on the SDK-default serializer still produces correct wire keys); **Elasticsearch and OpenSearch materialized views default to read-your-write**; **the OpenSearch projection store applies query filters**; and **the Redis distributed job lock carries a per-acquisition owner token**, so one instance can no longer release or extend another's lock.

---

## Transports

- **MQTT and IBM MQ** join the family — `AddMqttTransport` (QoS-honoured, MQTT-5 shared subscriptions for competing consumers) and `AddIbmMqTransport` (unit-of-work per message). See [MQTT](./transports/mqtt.md) and [IBM MQ](./transports/ibm-mq.md).
- **Apache Pulsar** ships transport **primitives** — a keyed sender and receiver over DotPulsar via [`AddPulsarTransport`](./transports/pulsar.md).

  :::info First-wave scope
  This package provides the low-level sender and receiver only. Full dispatch-pipeline integration is **not** part of it. For pipeline-integrated messaging today use Kafka, RabbitMQ, Azure Service Bus, AWS SQS, or Google Pub/Sub.
  :::

- **A payload-size guard covers every transport.** A configurable maximum inbound payload is enforced at the receive ingress of all six transports **before the body is deserialized** — an over-limit message is rejected at the boundary and never deserialized, so one oversized message cannot exhaust memory, poison-loop, or strand a batch. Each transport ships a bounded default; `MaxPayloadBytes = null` opts out. See the [payload size contract](./operations/runtime-contract.md#payload-size-contract).
- **Two named registrations of one transport no longer overwrite each other's options.** Registering, say, two AWS SQS transports under different names wrote both configurations to the same unnamed options instance, so whichever registered last silently supplied the settings for both — different queues, regions, or KMS keys collapsed onto one. Each registration now writes a **named** options instance, and the per-name value is what that transport reads. See [transport names](./transports/multi-transport.md#transport-names) for what is per-name and what is still shared.
- **Kafka decodes Confluent Schema Registry framing on consume** — the 5-byte header is stripped before the canonical deserializer sees the payload. Previously the framed bytes were passed downstream and failed to deserialize.
- **gRPC** gains retries and hedging, keep-alive configuration, HTTP/2 connection pooling, and a configurable retryable status set.
- **AWS SQS** gains optional queue provisioning and a visibility-timeout heartbeat that extends in-flight visibility for long-running handlers.
- **Google Pub/Sub** can auto-apply a dead-letter policy at startup; **RabbitMQ** exposes automatic connection recovery and per-queue `MaxPayloadBytes`; **Kafka** exposes the consumer partition-assignment strategy and commits offsets on revocation; **AWS Lambda** adds a SnapStart warm-up hook.
- **`CatchUpPolicy` on the cron timer** controls what happens after a downtime window where scheduled occurrences were missed — `Skip` (default), `FireOnce`, or `FireAll` bounded by `MaxCatchUpOccurrences`.
- **Avro fails closed on schema skew.** Payloads are framed with the writer-schema fingerprint; a mismatch throws `SchemaMismatchException` rather than positionally mis-decoding. Avro still does not perform writer-schema resolution — version your types explicitly. See [serialization providers](./middleware/serialization-providers.md#avro).

---

## Leader election

- **Fencing tokens on every backend.** Consul, Kubernetes, and MongoDB ship token providers, and every backend accepts an optional `IFencingTokenProvider`. Tokens are **strictly monotonic** — a wrapped or reused value could let a stale leader validate as current, so an exhausted token domain throws `FencingTokenExhaustedException` and **fails closed**: leadership cannot be granted or renewed, and a leader that hits exhaustion mid-tenure relinquishes.
- **MongoDB defaults to a durable per-resource counter.** The previous store-arbitrated token lived in the lock document, which is destroyed on graceful release and by the TTL index — resetting to 1 on restart and letting a zombie's stale token validate as current.
- **The relinquish decision was corrected** so clock-skew and grace-backstop conditions are OR-combined, closing a split-brain window; renewal timestamps are read and written lock-free to eliminate a torn multi-field read.
- **`AcquisitionFailed`** fires per failed acquisition attempt, surfacing contention and backend errors a `BecameLeader`-only view would miss — see [observing acquisition failures](./leader-election/index.md#observing-acquisition-failures).

See [leader election](./leader-election/index.md#fencing-tokens).

---

## Security and compliance

- **Per-subject crypto-shredding.** `AddCryptoShredding()` encrypts personal-data fields with a per-subject key; erasing that subject's key destroys **all** its versions, rendering every field under it unrecoverable. The guarantee is bounded by what was encrypted under that key — the inbox and outbox at-rest decorators are not subject-keyed, so message payloads are unaffected. The field cryptor is fail-closed: a type declared to carry personal data that resolves no such fields throws rather than silently persisting plaintext. See [crypto-shredding](./compliance/crypto-shredding.md).
- **Erasure reports `Completed` only when every discovered location is covered.** Coverage is three-state — *Covered*, *Exempt* (a declared, documented retention exemption), or *Uncovered* — and an uncovered location forces `PartiallyCompleted` **even when nothing threw**. The framework will not claim success over a store it never erased. The audit store is `Exempt` by default with its legal basis recorded on the certificate, never a silent skip. See [erasure coverage model](./compliance/gdpr-erasure.md#erasure-coverage-model).
- **Encryption survives key rotation.** The key version is stamped into the ciphertext envelope and decryption resolves by that stored version against a provider retaining prior versions, so a field encrypted before a rotation stays decryptable after it. Envelopes carry a format-version discriminator distinct from the key version; an unknown format version is a surfaced error, never a best-effort parse. Audit-trail integrity is consolidated onto one keyed MAC over a round-trip-stable canonical serialization. See [key lifecycle](./security/encryption-architecture.md#key-lifecycle).
- **Credential stores persist for real.** The Vault and AWS Secrets Manager stores were configuration-fallback placeholders that **silently discarded** every write while logging success. Both now round-trip against the real backend, and a backend failure surfaces as an error.
- **Vault key suspension is enforced.** `SuspendKeyAsync` was a silent no-op; suspension is now a durable provider-side marker and the crypto path refuses the key for both encrypt and decrypt, consistent with the other providers.
- **Audit persistence never silently discards.** `Security:Auditing:StoreType=SQL` fails fast at startup rather than accepting and discarding every event, and archival is cutoff-bound — deleting only documents confirmed written to a flushed archive.
- **Security auditing is PII-safe by default**, data-subject identifiers are pseudonymized with a keyed HMAC requiring a secret pepper (validated at startup, fails closed), and telemetry fingerprints can be upgraded to keyed HMAC-SHA-256 with an optional pepper. Fingerprinting never throws on the telemetry path — see [PII-safe telemetry](./observability/pii-safe-telemetry.md#keyed-fingerprints-pepper).
- **ASP.NET Core authorization faults return 500, not a leaky 403.** An exception during evaluation previously returned 403 carrying the raw exception message — masking a server error as a denial and leaking internal detail across the trust boundary. A genuine denial still returns 403.
- **Master-key backup and recovery** contracts support export and reconstruction with Shamir threshold shares.

---

## Operations and observability

### Operational dashboard

A free, open-source, read-only-by-default dashboard surfacing live state of the subsystems already instrumented — outbox, dead-letter queue, inbox, saga, projection and CDC lag, and leader election — across every configured provider.

```csharp
builder.Services.AddDashboard();
app.MapDashboard();   // read API at /dashboard/api, embedded SPA at /dashboard
```

Absent subsystems **fail open** (report "not configured") rather than erroring. The SPA is served as embedded assets under a strict Content-Security-Policy, and the serving path is trim- and AOT-safe.

**The read API is unauthenticated by default.** Some reads are sensitive — dead-letter exception messages and correlation ids, saga tenant ids — so gate the whole dashboard by mapping it inside a parent `RequireAuthorization` route group. **Mutating actions are opt-in** (`EnableMutatingActions`, default `false`, endpoints not mapped at all) and auth-gated when enabled. List endpoints clamp page size so a caller cannot request an unbounded result set. See [operational dashboard](./operations/dashboard.md).

### Telemetry

- **Circuit-breaker and dead-letter metrics come straight from the middleware** with no opt-in observability service required — subscribe via `AddDispatchInstrumentation()`.
- **A distinct `dispatch.inbox.deduplicated` counter** makes dedup rate independently observable from total throughput, and the **`poison.reason` tag is bounded to the `DeadLetterReason` enum**, eliminating an unbounded-cardinality risk from free-form strings. See the [metrics reference](./observability/metrics-reference.md).
- **W3C `tracestate` propagates symmetrically with `traceparent`** across the outbox, and **B3 propagation** is available via `UseB3TraceContextInjection()` for interop with B3-instrumented services.
- **Serverless hosts emit an honest telemetry signal.** AWS Lambda, Azure Functions, and Google Cloud Functions log that in-process exporters are in use, instead of a silent no-op behind an advertised-but-inert option.

---

## Startup and configuration

- **Startup gates run for host-less containers.** The durability gates and prerequisite checks fire from the host's startup validation, which requires `IHost.StartAsync` — so a consumer who builds an `IServiceProvider` manually left them inert. Such a host can now call **`IServiceProvider.ValidateStartupGates()`** to run every gate at once. Hosts that build an `IHost` and call `StartAsync`, including Azure Functions and AWS Lambda on the isolated-worker model, already run these and need nothing. See [startup prerequisite validation](./core-concepts/dependency-injection.md#host-less-containers-must-trigger-the-gates-explicitly).
- **The production authorization stack fails closed on a volatile grant store**, so a host wired with an in-memory grant store fails fast rather than booting into a state where lost grants silently deny every user. Accept one deliberately with `GrantDurabilityOptions.AllowVolatileGrantStore = true`; the audit-store, key-provider, and schedule-store gates follow the same contract.
- **The default dispatch pipeline runs registered middleware.** `AddDispatch`'s default path previously resolved to an empty profile, so `DispatchAsync` bypassed all middleware and outbox staging silently never ran. Middleware you have not registered are skipped gracefully with a debug log. `UseProfile` on an unknown key now throws at configuration time. See [pipeline profiles](./pipeline/profiles.md).
- **Keyed message handlers are wired correctly on every runtime.** Handlers registered via keyed DI were **silently never wired** on .NET 9 and .NET 10 — not discovered, not lifetime-promoted, and **no error raised**. Handler-lifetime analysis now reads the keyed service accessors, preserving the service key. See [keyed services](./core-concepts/dependency-injection.md#keyed-services).
- **Misconfigured options fail fast** — Kafka dead-letter options are validated at host start across every registration path, and Polly resilience options validate on the convenience overload that previously registered them without validators.
- **Startup prerequisite validators** across six subsystems give actionable errors when `Add*()` is called without a concrete provider, and **non-keyed convenience aliases** let you inject `IEventStore`, `IOutboxStore`, `ISagaStore` and the rest directly without `[FromKeyedServices("default")]`.

---

## Developer experience

- **Native AOT** — 160 of 195 shipped projects declare AOT compatibility; the remainder are blocked by external SDK dependencies rather than by framework code. A consumer-facing AOT sample publishes and runs with `dotnet publish -p:PublishAot=true`.
- **Performance** — ultra-local dispatch at roughly 35 ns and 24 bytes, with zero-allocation handler invocation and activation. `UseLightMode = true` disables correlation-id generation for maximum throughput.
- **A canonical builder pattern** across every SQL Server, PostgreSQL, MongoDB, Cosmos DB, and Redis subsystem package — one `subsystem.UseProvider(Action<IBuilder>)` entry point, with consistent connection overloads per provider and `ValidateOnStart` on every builder.
- **Roslyn analyzers** for common mistakes, **source generators** for AOT-compatible registration, and **`dotnet new` templates** for dispatch, event-sourcing, and saga projects.
- **A container deployment guide** covering Dockerfile recipes for JIT, ReadyToRun and AOT, Kubernetes probes, GC tuning, graceful shutdown, and Azure Container Apps.
- **Interface segregation and options compliance** — public interfaces meet the five-method gate and options types the ten-property gate, split into focused roles. `IMessageChannelAdapter` composes from sender, receiver, acknowledger, and connection roles; the compliance audit store splits into `IAuditQuery` and `IAuditWriter`.

---

## Versioning

Each pre-release publish increments the suffix (`10.0.0-alpha.1`, `10.0.0-alpha.2`, …). The major version matches the targeted .NET major, so `net10.0` maps to `10.x`. See [versioning strategy](./migration/version-upgrades.md) for the full release-stage roadmap and the SemVer policy.

Before upgrading between pre-releases, review [Before you upgrade](#before-you-upgrade) above, compare `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` in the repository at the two tags, and run your
test suite. Those files are build inputs and are not shipped inside the packages, so they cannot be read
from an installed package.

---

## Earlier releases

The published `3.0.0-alpha` line predates this page's reorganisation. Its per-release notes remain on
[GitHub Releases](https://github.com/TrigintaFaces/Excalibur/releases) — and so do the `10.0.0-alpha`
notes. If you are moving between two pre-releases of the current line, the tagged release notes are
where the per-version detail lives; this page carries the cumulative picture rather than a per-alpha diff.

---

## See also

- [Known issues](./known-issues.md) — identified defects in this pre-release
- [Versioning strategy](./migration/version-upgrades.md) — SemVer policy, deprecation rules, upgrade practice
- [Getting started](./getting-started/index.md) — install and build your first handler
- [Package guide](./package-guide.md) — choose the right packages for your scenario
