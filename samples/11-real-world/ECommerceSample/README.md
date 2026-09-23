# ECommerce Order Processing Sample

**Location:** `samples/11-real-world/ECommerceSample/`

A realistic order-processing scenario running on the framework's shipping
`IInboxStore`, `IOutboxStore` and `IScheduleStore` contracts, backed by their
in-memory providers. Orders are deduplicated through the inbox, confirmation
e-mails are staged in the outbox and delivered by a background drain that
retries a transient send failure, and inventory checks are scheduled and then
executed from the schedule payload.

The sample owns no store implementations. It composes the shipping ones through
their public entry points, so the only line that changes for a persistent
deployment is the provider:

```csharp
services.AddExcaliburInbox(inbox => inbox.UseInMemory());
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseInMemory()));
services.AddDispatchScheduling();
```

## The run checks itself

`dotnet run` is not a demo loop. It submits a fixed workload, waits for the
background workers to quiesce, asserts the result, prints a pass/fail table, and
**exits non-zero if any assertion fails**:

```
  [PASS] orders persisted (expected 5, actual 5)
  [PASS] duplicates suppressed (expected 2, actual 2)
  [PASS] orders failed (expected 1, actual 1)
  [PASS] inbox entry ORD-2026-006 is failed with an error (status=Failed, error=...)
  [PASS] notifications sent (expected 5, actual 5)
  [PASS] transient send failure was retried (retries=1)
  [PASS] outbox messages left unsent (expected 0, actual 0)
  [PASS] inventory checks executed (expected 3, actual 3)
  [PASS] inventory check recorded for laptop-pro-15 (1 result(s))
  ...
All 25 checks passed.
```

Each assertion reads observable state — what the repository holds, what the
inbox entry's status is, what the outbox has left, what the inventory history
records — rather than whether a service was registered. Sever any one of the
paths and the corresponding checks turn red.

## What the sample demonstrates

| Area | Contract | Exercised by |
|------|----------|--------------|
| Exactly-once order handling | `IInboxStore` | `OrderProcessingService` claims `(orderId, handler)`; a repeat submission is suppressed |
| Failure recording | `IInboxStore` | An invalid order is marked failed and is never persisted |
| Reliable notifications | `IOutboxStore` | `NotificationService` stages inside the claim; `NotificationDrainService` claims, sends, marks sent |
| Delivery retry | `IOutboxStore` | A transient send failure is reported with `MarkFailedAsync` and redelivered after the backoff window |
| Scheduled work | `IScheduleStore` | `InventoryService` stores the check; `InventoryCheckProcessor` deserializes the payload, executes, completes |
| Observability | `AddDispatchTelemetry` + OpenTelemetry | Spans and metrics per operation (`--trace` to export to the console) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` | `StoreHealthCheck` round-trips each store; `BusinessLogicHealthCheck` probes the repositories |

## Run locally

```bash
dotnet run             # runs the scenario, prints the checks, exits 0 or 1
dotnet run -- --trace  # also exports OpenTelemetry spans and metrics to the console
```

## Worth copying

- **Claim before you work.** `CreateEntryAsync` is the deduplication claim, not a
  log line written afterwards. Checking a flag and then acting on it leaves a
  window open when two submissions arrive together.
- **Stage inside the claim.** The confirmation is staged in the same flow that
  persists the order, so a confirmation exists for every persisted order and for
  no other.
- **Report the send outcome.** A failed send is reported with `MarkFailedAsync`
  so the store can redeliver it; swallowing the exception loses the message.
- **Put what the worker needs in the payload.** The schedule identifier is an
  opaque handle. Everything the inventory check needs travels in the message body.
- **A health probe should not claim.** Reading the outbox with
  `GetUnsentMessagesAsync` leases the batch it returns, so a probe using it to
  "verify staging" takes real undelivered messages away from the drain.

## Related samples

- `11-real-world/EnterpriseOrderProcessing/` — full multi-package reference app
- `04-reliability/Outbox/` — outbox pattern focus with SQL Server
- `04-reliability/InboxConsumer/` — inbox deduplication focus
