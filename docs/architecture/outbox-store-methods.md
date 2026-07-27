# IOutboxStore Method Split

## Core Interface (5 methods)

| Method | Purpose |
|--------|---------|
| `StageMessageAsync` | Stage a message for later delivery |
| `EnqueueAsync` | Enqueue with dispatch context |
| `GetUnsentMessagesAsync` | Retrieve batch of unsent messages |
| `MarkSentAsync` | Mark individual message as sent |
| `MarkFailedAsync` | Mark individual message as failed |

## Admin Interface (IOutboxStoreAdmin, 4 methods)

| Method | Purpose |
|--------|---------|
| `GetFailedMessagesAsync` | Retrieve failed messages for retry |
| `GetScheduledMessagesAsync` | Retrieve scheduled messages |
| `CleanupAllTenantsSentMessagesAsync` | Purge old sent messages |
| `GetStatisticsAsync` | Outbox statistics for health/monitoring |

## Batch Extensions (default interface methods)

`MarkBatchSentAsync`, `MarkBatchFailedAsync`, `TryMarkSentAndReceivedAsync` -- default implementations fall back to individual calls; stores that support batch ops can override.

## Segregated Capability Interfaces (composition, not inheritance)

Optional capabilities are kept off `IOutboxStore` (so the core stays within the ISP threshold). `IOutboxStore` implements `System.IServiceProvider`, and a consumer discovers a capability by asking for it — `store.GetService(typeof(IDeadLetterableOutboxStore))` — rather than by an `is`-cast on the concrete instance. This is the Microsoft `IServiceProvider` / `GetService` escape-hatch pattern (as used by `IApplicationBuilder.ApplicationServices`). A store that omits a capability returns `null` from `GetService`, and the processor falls back to the core path.

Discovery through `GetService` (rather than a direct cast) is what lets a **decorator honour its own invariant** instead of leaking the inner store's raw capability:

- **`OutboxStoreDecorator`** is the base for transparent decorators (e.g. telemetry). Its `GetService` forwards to `Inner.GetService`, so a capability survives the decorated chain unchanged.
- **`IsolatingOutboxStoreDecorator`** is the base for **transforming / guarding** decorators. It **denies unknown capabilities by default** (`GetService` returns `null` unless the capability is in `ForwardableCapabilities`) and wraps the rest via `WrapCapability`. `EncryptingOutboxStoreDecorator` derives from it and **wraps every payload-bearing capability** so a caller cannot obtain a raw, plaintext-bypassing view of the inner store. Forwarding is **opt-in per capability, fail-closed by default** — a capability whose safety through the decorator is not proven is simply not obtainable through it (returns `null`), never forwarded raw.

| Interface | Method | Purpose | Implemented by | Fallback when absent |
|-----------|--------|---------|----------------|----------------------|
| `IFencedOutboxStore` | `GetUnsentMessagesAsync(batchSize, long fencingToken, ct)`, `MarkSentAsync(messageId, long fencingToken, ct)` | Fenced drain/mark for leader-elected deployments — the caller passes its current fencing token so a superseded leader's writes are rejected. The token is a non-nullable `long` (absence of fencing is modelled by not implementing this capability, never an in-band `0`). On **PostgreSQL and Oracle** both fenced operations are a **single-statement atomic compare-and-swap** (the token check and the mutation execute under one DB statement — a wCTE on Postgres, a PL/SQL block on Oracle), so there is no check-then-act window a demoted leader can interleave (Sprint 888, `uw1nv4`/`f5zutu`); those stores hold the high-water mark in a dedicated fence surface. **SQL Server implements this interface but does not currently satisfy its contract in full**: it derives the high-water from `MAX(FencingToken)` over the message rows and overwrites rather than raises it, so cleanup, a drained-empty outbox, or a later write can lower or reset it. | SQL Server (partial — see note), PostgreSQL, Oracle | Core `IOutboxStore.GetUnsentMessagesAsync`/`MarkSentAsync` (no fence) |
| `IOutboxStoreCapabilities` | `SupportsSentTracking` (property) | Reports whether the store retains a successfully-sent message as a countable, cleanup-eligible `OutboxStatus.Sent` row. `true` for tracking stores; `false` for the **delete-on-sent** relational stores (PostgreSQL, Oracle) that remove the row on mark-sent — so statistics and cleanup don't assume one uniform storage model. Data-shaped capability in the BCL idiom (`Stream.CanSeek`), sibling to `IInboxStoreCapabilities` (Sprint 888, `y1moc0`). | PostgreSQL, Oracle declare `false`; others default to `true` | Treated as a tracking store (`true`) |
| `IDeadLetterableOutboxStore` | `MarkDeadLetteredAsync` | Terminal `OutboxStatus.DeadLettered` transition that the claim predicate structurally excludes | all shipped stores | Message stays `Failed` |
| `IBackoffSchedulableOutboxStore` | `MarkFailedWithBackoffAsync` | Records the per-message `NextAttemptAt` so the computed exponential backoff actually throttles re-claim | SQL Server, PostgreSQL | `MarkFailedAsync` (immediate re-eligibility) |

`IBackoffSchedulableOutboxStore.MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, ct)` is called by the processor only on a genuine delivery failure; a circuit-breaker-open short-circuit uses the plain `MarkFailedAsync` path (no backoff) so the message stays immediately retryable. The claim predicate then excludes the message via `WHERE NextAttemptAt IS NULL OR NextAttemptAt <= @now`. Remaining providers (Redis, MongoDB, Elasticsearch, DynamoDB, Cosmos DB) retain the fail-open immediate-retry path and are tracked as follow-ups.

The inbox has a parallel capability, `IBackoffSchedulableInboxStore.MarkFailedWithBackoffAsync(messageId, handlerType, error, retryCount, nextAttemptAt, ct)` (Sprint 850), implemented by the SQL Server inbox store and forwarded through the same telemetry/encrypting inbox decorators — same fail-open contract.

## Rationale

Follows Microsoft IDistributedCache pattern: core interface minimal, admin operations separate, optional features segregated behind capability interfaces with graceful fallback. ISP applied in Sprint 553; capability interfaces added in Sprints 841 (`IDeadLetterableOutboxStore`), 849 (`IBackoffSchedulableOutboxStore`), and 850 (`IBackoffSchedulableInboxStore` + PostgreSQL outbox backoff).
