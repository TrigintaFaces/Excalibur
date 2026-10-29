---
sidebar_position: 10
title: Evolving projections
description: Keep subscription names stable, populate versioned projections, and switch readers safely.
---

# Evolving projections

Keep your subscription name stable. Adding a projection does not require renaming the subscription or resetting existing projections. Each projection generation owns a durable binding and checkpoint; adding another generation starts only that generation from the beginning of the event stream.

## Adding a projection

Register the new projection alongside the existing projections. The processor binds its member and catches it up from zero. Existing members retain their binding identities and checkpoints.

```csharp
es.AddProjection<OrderSummary>(p => p.Async().When<OrderPlaced>(/* handler */));
es.AddProjection<CustomerActivity>(p => p.Async().When<OrderPlaced>(/* handler */));
es.EnableProjectionProcessing(options =>
{
    options.ProjectionName = "orders-read-models"; // Keep this stable.
    options.SubscriptionScope = "orders/events";
});
```

A change to an existing member's definition, destination or storage scope is refused. Use a new projection version for changed semantics. `SubscriptionRevision` and `SubscriptionScope` affect every member: changing those values deliberately changes the processing definition of the entire subscription.

Removing a registration does not silently retire its durable member. Another application instance might still host it. Stop the old processors and explicitly retire the member when it is no longer required.

## Versioning a projection

```csharp
[ProjectionVersion(2)]
public sealed class OrderSummary
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
```

No attribute and `[ProjectionVersion(1)]` use the same initial destination. Version 2 changes both the member key and physical destination. SQL Server and PostgreSQL use a suffix such as `_v2`; document and search providers use `-v2` where appropriate for the provider's addressing model.

The normal projection processor populates the new generation from the event stream. You do not need an in-place rebuild to populate a new generation. Keep the previous deployment processing its generation while the new one catches up if you need uninterrupted reads or a rollback window.

Writers remain pinned to the generation declared by their projection type. Changing a reader pointer does not redirect writers.

## Switching framework readers

Register a stable logical reader independently of the projection version:

```csharp
services.AddProjectionGenerationReader<OrderSummary>(
    subscriptionName: "orders-read-models",
    readerName: "orders",
    compatibleProjectionKeys:
    [
        SubscriptionKeys.ProjectionKey(typeof(OrderSummary), 1),
        SubscriptionKeys.ProjectionKey(typeof(OrderSummary), 2)
    ]);
```

For inline processing, use `services.AddInlineProjectionGenerationReader<OrderSummary>("orders", compatibleKeys)` instead. It selects the same checkpoint store, event source and subscription name as the inline processor, including automatic SQL Server configuration. The background registration above selects the publicly registered checkpoint store and event query. Both register a paired `ProjectionGenerationControl<OrderSummary>` for administration; it cannot accidentally target another subscription. Register one reader per projection type. A second registration, including mixing inline and background sources for the same type, fails explicitly.

Only list generations the deployed reader can actually deserialize and query. A renamed JSON property, changed partition discriminator, incompatible field mapping, or different query semantics can require a separate reader deployment. Compatibility is explicit; a generation number does not provide a schema conversion.

After the target generation has caught up, capture the current pointer revision and request cutover:

```csharp
var control = services.GetRequiredService<ProjectionGenerationControl<OrderSummary>>();
var previous = await control.ReadGenerationAsync(token);
var result = await control.CutOverAsync(
    SubscriptionKeys.ProjectionKey(typeof(OrderSummary)),
    expectedRevision: previous?.Revision,
    maxAcceptableLag: 0,
    token);
```

Cutover compares the captured pointer revision and atomically checks the target binding identity and minimum checkpoint. A concurrent retirement or different cutover cannot leave a dangling pointer. `Contested` means the preconditions changed: reread the state and decide whether the operation is still wanted. Do not blindly retry a stale administrative decision.

An absent checkpoint does not qualify even when the stream is empty. Processing an empty prefix explicitly acknowledges checkpoint zero. A failed checkpoint write cannot produce a successful freshness attestation.

The returned lag is measured against the stream head captured for this request. Producers may append after that measurement. Cutover selects a generation; it does not promise that every subsequent read includes all future writes.

Resolve `ProjectionGenerationReader<OrderSummary>` for reads:

```csharp
var reader = services.GetRequiredService<ProjectionGenerationReader<OrderSummary>>();
var order = await reader.GetByIdAsync(orderId, token);
```

Each operation resolves the current generation, opens that exact destination, verifies the storage scope of its actual client, and checks the pointer revision again after reading. A changed revision causes a retry, including a switch away and back to the same destination. Three consecutive changes fail explicitly. Reader handles are disposed on success, retry, cancellation and failure.

The reader exposes identifier lookup, query and count. Native SDK queries, dashboards, full-text APIs, and existing `IProjectionStore<T>` reads do not automatically follow this pointer. Adopt the generation reader at the application read boundary you intend to switch.

### NativeAOT

Use the generated metadata overloads:

```csharp
var order = await reader.GetByIdAsync(orderId, ProjectionJson.Default.OrderSummary, token);
var orders = await reader.QueryAsync(filters, options, ProjectionJson.Default.OrderSummary, token);
```

The configured outer store must expose `IAotProjectionGenerationReaderFactory<T>`. There is no reflective fallback. Elasticsearch requires the same generated JSON context in its configured source serializer and the reader call. A provider's conventional serializer support does not imply NativeAOT support; BSON and JSON serializers are not interchangeable.

Generation reader capabilities differ by provider:

| Projection destination | Conventional reads | Generated JSON reads |
| --- | --- | --- |
| SQL Server, PostgreSQL | Supported | Supported |
| Elasticsearch | Supported | Supported with the configured generated source serializer |
| OpenSearch, MongoDB, Cosmos DB | Supported | Not advertised |
| DynamoDB | Supported; resolves table metadata asynchronously and pins data requests to its account-qualified ARN | Not advertised |
| Firestore | Supported for clients created through the framework's project/endpoint configuration; unknown externally supplied client endpoints are refused | Not advertised |

This table describes generation-reader capabilities, not NativeAOT certification of an entire provider or application. Validate the complete published application and its dependencies. Direct projection reads and processing remain available independently of generation routing.

Tenant and encryption decorators must mediate reader capabilities. An unsupported capability fails explicitly rather than bypassing a decorator. The conventional field-encryption decorator does not advertise generated reader support.

### Storage identity

A destination identifies the exact table, index, container or collection. Its separate storage scope identifies the configured service/database boundary. Both belong to the immutable binding lifetime. Two clusters with the same index name are different destinations for routing purposes.

DynamoDB resolves the table ARN and actual request endpoint before binding or reading. Auto-create runs before metadata resolution for writers; generation handles never create tables. Successful metadata resolution is cached for the store lifetime and data requests use the resolved ARN, so credential refresh cannot redirect an already-bound store to another account. Failed and cancelled resolution can be retried. Short-lived generation handles share a bounded metadata cache; after eviction a new handle re-resolves its target and routed reads reject any scope change. Existing handles retain their captured ARN. Readers require permission to describe the target table as well as read its data.

Firestore records endpoint metadata on the exact client created by `AddExcaliburFirestore` using project/endpoint configuration. An existing or externally supplied client does not inherit that metadata merely because the registration was called.

If a provider cannot establish the actual client's scope, routed reads refuse it. Do not infer a supplied client's identity from unrelated options. Equivalent endpoint configurations must be consistent across hosts. These identities describe configured storage addresses; they do not detect an administrator deleting and recreating a physical resource at the same address.

## Rollback and retirement

Rollback is another compare-and-set transition with a fresh revision. Keep the previous generation processing during the rollback window; it must satisfy the lag gate before switching back.

An active reader pins its target binding. Releasing a pinned member returns `false`. Multiple readers can pin the same member; removing one pointer does not release the others' pins.

After switching readers, stop and drain the old generation's processors. Keep its physical destination until the rollback window ends and in-flight readers have drained. Then retire its captured binding:

```csharp
var checkpoints = services.GetRequiredService<ISubscriptionCheckpointStore>();
var roster = await checkpoints.ReadSubscriptionAsync("orders-read-models", token);
var oldKey = SubscriptionKeys.ProjectionKey(typeof(OrderSummary), 1);
var old = roster.Members.Single(member => member.ProjectionKey == oldKey);
bool released = await checkpoints.ReleaseSubscriptionMemberAsync(
    "orders-read-models", oldKey, old.BindingId, token);
```

A released binding cannot be recreated by a delayed checkpoint advance. Rebinding the same member key creates a new binding identity. A stale release cannot delete that replacement.

Retirement does not delete the physical destination. Physical cleanup is a separate administrative action. Do not delete a destination while readers or writers may still use it.

### Native Elasticsearch and OpenSearch aliases

`IProjectionGenerationRepoint` is a separate alias administration capability. It checks the expected old destination and preserves alias filter, routing, visibility and write-index settings. Coordinate alias administration with the lifecycle of its physical indexes.

Generation destinations must be concrete indexes. The framework verifies their identity against the search service and rejects aliases, data streams and index expressions. It caches successful checks within the configured client lifetime; deleting or recreating an index at the same name requires stopping and rebinding its consumers.

A native alias change and the checkpoint database are different systems. The framework's atomic generation cutover does not claim a transaction across them. If direct search clients must follow an alias, manage that operation explicitly and test those clients separately.

Serialize native alias administration across all administrators and hosts. The expected destination
check does not detect an intervening A-to-C-to-A change, so a paused A-to-B operation could overwrite
a later decision. This limitation applies to native aliases; framework reader pointers use revisions.

For direct search clients, swap the alias, read its target back, and verify those clients before
retiring the previous generation. If the swap or verification is uncertain, retain both generations
and reconcile the alias first. Keep the old generation available through the rollback window and
drain its readers and processors before explicitly releasing its binding. An alias swap alone never
authorizes deleting the old index.

## In-place repair is offline

Prefer a new generation for online evolution. `IProjectionRebuildService` repairs an existing destination and requires `ProjectionRebuildOptions.OfflineRebuildAcknowledged = true` after all application writers, projection processors and other rebuild processes have stopped and drained for the entire operation.

`ProjectionRebuildOptions.MaxParallelism` limits active rebuild calls on the registered rebuild service (default `1`). Additional calls wait before reading the source or releasing a subscription member; cancellation removes a waiting call without consuming capacity. The permit covers replay and destination writes, and is released after success, failure, or cancellation. Calls for the same projection are rejected while an earlier call is running or waiting. This local limit works together with the shared storage admission limits below. Budget the total across processes against available database capacity. `BatchSize` and `BatchDelay` control each rebuild’s read batches and pacing.


Before repair, stop and drain readers too, then remove every reader pointer that pins this generation. Use `ProjectionGenerationControl<T>.ReadGenerationAsync` to capture each revision and `DecommissionAsync(revision, token)` to remove that pointer conditionally. A stale revision returns `false`; reread before deciding again. The rebuild refuses to release a binding that still has any reader pointers.

This acknowledgement is an operator prerequisite, not an enforced distributed lock. Checkpoint fencing alone cannot prevent a destination write that was already in flight. Do not run an in-place rebuild concurrently with a writer.

An in-place rebuild resets that member's cursor, folds events from an empty state and repairs the rows produced by the replay. It can rewrite an already-current row without advancing its ordinal. It does not enumerate and remove rows whose keys no longer occur in the retained stream. Use a new empty generation when a complete replacement is required.

## Shared storage capacity

Projection registration installs shared admission automatically. Mutable and immutable processing, generated-metadata processing, session and generation reads, offline replay, checkpoint operations, metadata discovery, and search visibility refreshes share one governor per root service provider. Projection handlers run outside storage admission, so a handler can read another projection without holding a database permit.

The defaults are four reads and four writes per configured storage resource, one offline rebuild per resource, and 16 storage operations across all resources. Rebuild admission is separate: a rebuild does not hold a storage permit while invoking handlers. Each admission queue holds at most 256 waiting operations in arrival order. A cancelled waiter does not release another operation's permit. Cancellation of executing work retains the permit until that storage call actually completes. Queue rejection is an explicit failure; it never certifies checkpoint progress.

Most applications need no additional configuration. For a constrained database or several hosts sharing capacity, tune the application budget with ordinary options:

```csharp
services.Configure<ProjectionCapacityOptions>(options =>
{
    options.MaxConcurrentOperations = 4;
    options.MaxConcurrentReads = 2;
    options.MaxConcurrentWrites = 2;
    options.MaxConcurrentRebuilds = 1;
    options.QueueLimit = 64;
});
```

These values are frozen when the governor is created. `MaxResources` bounds the retained resource registry (default 1024); exceeding it fails explicitly instead of evicting a budget that active work still owns. Zero `QueueLimit` rejects work immediately when a permit is unavailable. Counts and resource limits must be positive.

A resource identifies configured database or cluster scope, excluding projection generation and tenant-qualified checkpoint identity. Checkpoint stores identify their own database, which may differ from the event database. In-memory stores share one application budget; their distinct durable-witness identities remain unchanged. Unknown scopes share a conservative bucket. Firestore currently groups by endpoint. Search and MongoDB seed lists, and relational endpoint aliases, can identify the same physical system differently; the application-wide limit still bounds their combined framework operations. Keep routing and client configuration stable during each operation. A resource change observed after waiting is rejected before the data call. OpenSearch captures its configured node scope once so discovery does not continually create new budgets.

These are **logical-operation limits within one root service provider**. They are not distributed quotas or exact connection counts: an SDK operation can issue several requests, and direct provider/SDK calls or manually constructed readers do not participate. The separate materialized-view transaction pipeline also remains outside this projection governor. Registered typed projections, global-stream projection hosts, and projection lag monitoring share the governor. Defaults are conservative fixed allowances, not automatic measurements of free connection-pool capacity. Budget room for other application queries and every host instance. Tiered reads without a single resource identity use the unknown bucket and application-wide allowance.

Storage callbacks and serializers must not recursively invoke governed storage. Such calls fail explicitly, including across resource keys; perform dependent reads in the projection handler instead. Starting another rebuild from a rebuild callback is also rejected. This avoids hidden deadlocks at capacity one.

The `Excalibur.EventSourcing.ProjectionCapacity` meter emits `projection.capacity.waiting`, `projection.capacity.active`, `projection.capacity.rejected`, and `projection.capacity.wait.duration` (seconds). Admission measurements distinguish `resource` and `process` budgets; `process` means this service-provider instance. Tags contain operation kinds, not connection strings, tenant identifiers, or projection keys. Monitor queue wait and rejection together with database latency, pool waits, and projection lag before increasing concurrency.

## Snapshots and event retention

Aggregate snapshots accelerate loading an aggregate: restore a compatible snapshot, then apply subsequent events. Snapshotting is optional. Without snapshots, or with infrequent snapshots, aggregate loading replays more events; correctness still comes from the retained event history.

Projection population does not use aggregate snapshots. A new projection may derive information absent from an aggregate snapshot, so it reads the event stream from zero. An existing generation resumes from its own durable checkpoint. Frequent aggregate snapshots do not replace projection checkpoints and cannot reconstruct missing historical events.

Retain or archive events in a form the global stream reader can hydrate. A missing prefix, missing non-erased payload, poison event or failed projection write must halt the affected progress rather than skip the event. Deliberately erased event markers are handled by the erasure protocol.

## Rolling deployments and session reads

An older instance may not host a newly added member. It cannot attest freshness for that unhosted member and may return `ProjectionFreshness.BoundExpired`. The events are already committed: do not repeat the command. Pass the save receipt to `IProjectionSessionReader` to perform a bounded consistency read.

Session reads verify the durable member lifetime, checkpoint and destination definition around the read. A generation selection alone is not a session-freshness guarantee.


## Deleting a projection key

Use `IProjectionStore<T>.DeleteAsync(id, cancellationToken)` when a key must stop receiving writes
in the current physical generation. Deletion is idempotent and also retires a key that has never
been created. A later delivery cannot recreate it from only the tail of the event stream.

The store removes the payload and retains minimal retirement metadata. Get, query, count and paging
operations hide that metadata. Positioned reads distinguish retirement from absence; conditional
writers settle it as `Vanished`, while blind writes fail explicitly. No consumer-managed fencing
token or subscription-name change is needed.

To populate that logical key again, build a new projection generation from retained events and use
the normal cutover process. Do not physically delete a row or its retirement metadata, and do not
configure TTL or cleanup jobs to remove retirement markers while writers can still address that
generation. This operation does not erase source events or every retained identifier.

Retirement applies to one destination key. It does not cancel handlers already executing, suppress
external side effects, or prevent an asynchronous handler from discovering another live destination
key. Keep external effects idempotent.
