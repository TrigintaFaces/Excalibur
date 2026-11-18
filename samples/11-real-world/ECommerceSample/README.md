# ECommerce Order Processing Sample

**Location:** `samples/11-real-world/ECommerceSample/`

A realistic order-processing scenario running on the framework's shipping
`IInboxStore`, `IOutboxStore` and `IScheduleStore` contracts, backed by their
in-memory providers. Orders are deduplicated through the inbox, confirmation
e-mails are staged in the outbox and delivered by a background drain that
retries a transient send failure, and inventory checks are scheduled and then
executed from the schedule payload.

The sample owns no messaging store implementations. The default profile composes shipping volatile
stores and keeps a sample-specific conditional order-publication checkpoint. All of that state is lost
on process restart. The separate `--sql-orders` profile commits order state, inbox completion and
notification intent together on one SQL Server transaction; changing only a provider line cannot
enlist business writes. The default volatile registrations are:

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
| Completed-order deduplication | `IInboxStore` + `ILeasedInboxStore` | Failed attempts can retry; only a matching completed order is called a duplicate |
| Failure recording | `IInboxStore` | An invalid order is marked failed and is never persisted |
| Reliable notifications | `IOutboxStore` + `IClaimScopedOutboxStore` | Stages raw notification bytes; the drain freezes acquisition evidence and reports typed completion |
| Delivery retry | `IClaimScopedOutboxStore` | A genuine pre-acceptance failure is reported under its captured claim; an applied failure observes the provider visibility floor |
| Scheduled work | `IScheduleStore` | `InventoryService` stores the check; `InventoryCheckProcessor` deserializes the payload, executes, completes |
| Observability | `AddDispatchTelemetry` + OpenTelemetry | Spans and metrics per operation (`--trace` to export to the console) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` | `StoreHealthCheck` round-trips each store; `BusinessLogicHealthCheck` probes the repositories |

## Run locally

```bash
dotnet run             # runs the scenario, prints the checks, exits 0 or 1
dotnet run -- --trace  # also exports OpenTelemetry spans and metrics to the console
```

## Worth copying

Both sample workers wait their fixed250ms polling interval after successful cycles and
non-shutdown failures, preventing a store outage from turning into a busy retry/log loop.
Shutdown interrupts either work or that delay. The optional TimeProvider controls polling
only; ordinary runs use system time, and tests advance Microsoft's FakeTimeProvider without
changing business timestamps or retry budgets.

- **Capture input, then acquire the current lease.** `CreateEntryAsync` retains auditable input;
  row existence alone proves neither completed processing nor permission to publish.
  `ILeasedInboxStore.TryAcquireLeaseAsync` establishes the current inbox term, while the
  conditional business-state transition separately grants publication permission.
- **Distinguish a lease from a transaction.** Volatile order state captures one immutable confirmation
  plan and grants publication permission with a conditional business-state transition. A successor
  cannot steal an in-flight publication merely because its inbox lease changed. A positive stage
  receipt is retained before callbacks; a fresh inbox holder can finish without staging again.
- **Keep uncertainty visible.** A failed stage can have an unknown acknowledgement. The volatile
  demonstration allows one caller-driven redrive with the same unique intent ID; another exception
  stops automatic staging and returns `NeedsReconciliation`. A duplicate-key exception is not
  acceptance. A retained outbox row remains drainable. This is not durable quarantine or atomicity.
- **Retain acquisition evidence.** The drain snapshots every batch row's durable ID,
  opaque claim, attempt count and payload before callbacks. It requires the real store's
  `IClaimScopedOutboxStore` through `GetService`, with no ID-only fallback.
- **Separate delivery from settlement.** A pre-acceptance failure is reported under
  that captured claim. After the sender accepts, a settlement fault or refusal never
  becomes another delivery failure in the same cycle. Only `Applied` updates sent/retry
  metrics; other outcomes are logged, and lease recovery retains at-least-once semantics.
- **State the deployment topology.** This custom sample drain has one active worker
  and no leader-election branch. A single process can still reclaim expired leases,
  so acquisition ownership remains required. This is not durable-fencing certification
  for every provider; use the framework worker for its complete leadership/recovery policy.
- **Put what the worker needs in the payload.** The schedule identifier is an
  opaque handle. Everything the inventory check needs travels in the message body.
- **Distinguish store operations from scheduler admission.** This sample's custom
  inventory worker demonstrates the `IScheduleStore` lifecycle. The framework
  scheduler additionally uses `IScheduleExecutionStore` for atomic occurrence
  admission and fenced progress when the configured store supports it. A custom
  worker must use that capability to prevent stale completion after cancellation
  or replacement; a read followed by an administrative write does not provide it.
  Provider authors can start from `ScheduleExecutionStoreConformanceTestKit` and
  wire all inherited and execution arms. Its controls do not prove durable restart.
- **A health probe should not claim.** Reading the outbox with
  `GetUnsentMessagesAsync` leases the batch it returns, so a probe using it to
  "verify staging" takes real undelivered messages away from the drain.

## Related samples

- `11-real-world/EnterpriseOrderProcessing/` — full multi-package reference app
- `04-reliability/Outbox/` — outbox pattern focus with SQL Server
- `04-reliability/InboxConsumer/` — inbox deduplication focus


## Durable SQL order and intent profile

This is an explicit **single-tenant managed SQL Server** order-submission example. It does not certify
SQL Native AOT, multi-tenant deployments, or durable inventory/scheduling. The default scenario retains
its existing local inventory and scheduling demonstration.

Use a dedicated sample database and provision the shipping outbox schema from
`src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql` before running.
The SQL profile uses that script's default `dbo.OutboxMessages` and associated tables. Also provision `src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.sql` for this
closed single-tenant profile. The inbox does not create its schema at runtime; the application creates
its immutable `dbo.CommerceOrders` table.
Set `ECOMMERCE_SQL_CONNECTION` through your local environment or secret provider; do not commit it.

```bash
dotnet run --project samples/11-real-world/ECommerceSample -- --sql-orders
dotnet run --project samples/11-real-world/ECommerceSample -- --sql-orders --stage-only
dotnet run --project samples/11-real-world/ECommerceSample -- --sql-orders --drain-only
```

`--stage-only` commits without starting a drain. `--drain-only` starts the unchanged notification worker
in a new process and reads existing durable intent; it does not resubmit the order. Running these two
commands separately checks committed-intent restart behavior when verified. It does not by itself test
termination during an uncommitted transaction. Keep those fault controls separate.

The transaction belongs to `ITransactionalInboxStore`. Order `DataRequest` commands use its supplied
connection and transaction, and `ITransactionalOutboxWriter` stages the confirmation on that same
transaction. Missing effective capabilities fail composition. Database/commit-acknowledgement failures
are surfaced; a caller retries the same logical identity instead of assuming rollback.

The immutable business row retains a canonical fingerprint and the original committed intent identity.
The processed-duplicate path checks this authoritative record even though the inbox skips its callback.
Different inputs return `Conflict`; missing evidence returns `NeedsReconciliation`; read failures are
not success. Normal outbox cleanup may remove a delivered physical row: the retained business commit
marker still prevents restaging after inbox expiry. It proves the intent was committed, **not** that an
external email was delivered. The sample retains immutable business identity; a production retention
policy must preserve equivalent identity evidence for its documented replay horizon.

Implementation availability is not release certification. Real SQL rollback, interrupted transaction,
retention and process-restart controls must be run against the exact candidate; the local in-memory
controls establish their narrower admission/publication behavior only.
