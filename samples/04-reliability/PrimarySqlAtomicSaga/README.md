# Primary SQL atomic saga

This managed console example opts into the primary SQL atomic profile. It preserves the existing
`SagaOrchestration` in-memory demonstration: that demonstration is single-process and non-durable.
It does not convert other template/provider variants to this profile.

Set `SAGA_SQL_CONNECTION` to a dedicated, already migrated SQL database. State, configured timeout
storage and the selected outbox must use the same logical server/catalog. Prepare and apply the
provider scripts in their documented order, including `01-SagaSchema.sql`, `SagaTimeouts.sql`,
`001_CreateOutboxSchema.sql`, `03-SagaDurableEffects.sql` and `04-SagaTimeoutAuthority.sql`.
For the nonce-aware state writer also apply the idempotent `05-SagaTimerRecoveryState.sql`
after the base state schema (fresh `01` already includes its fields). To provision interoperability
with ordinary timer recovery on the same selected timeout table, apply `06-SagaTimerRecovery.sql`
after the timeout/authority schemas and before starting writers. Its registered physical target must
match the selected SQL timeout table. Coordinate schema deployment with all participating writers.
The application does not create a database, auto-migrate, enroll a global ordering domain or alter
schema. Use the exact scripts from the same source/package revision as the application.

From the repository root, after the reviewed shared/Core/SQL source join has been adopted:

```powershell
dotnet restore samples/04-reliability/PrimarySqlAtomicSaga/PrimarySqlAtomicSaga.csproj
dotnet build samples/04-reliability/PrimarySqlAtomicSaga/PrimarySqlAtomicSaga.csproj -c Release --no-restore /m:1 /warnaserror /v:normal
dotnet run --project samples/04-reliability/PrimarySqlAtomicSaga/PrimarySqlAtomicSaga.csproj -c Release --no-build --no-restore
```

The bounded run uses generated state/payload/header metadata, the public typed coordinator entry and
`MessageBusOutboxPublisher`, the existing drain. It checks that a transition schedules a SQL timeout,
emits two original commands, and that a same-delivery replay produces no replacement work. A second
transition completes the saga and atomically cancels its timer. Read-only future-time queries observe
timer presence/removal; they do not claim or deliver timers. This example deliberately does not claim
an elapsed-time timeout delivery proof. For a long-running application, register the SQL timeout store
before the existing `AddSagaTimeoutDelivery`/`WithTimeouts` service and configure the saga pipeline/
registered timeout handler; keep the original claimed-timeout metadata on that delivery path.

Expected receipts are `ATOMIC_STATE_COMMANDS_AND_TIMEOUT_COMMITTED`, two `COMMAND` lines,
`ORIGINAL_COMMANDS_DRAINED_WITHOUT_REPLACEMENT`, and `COMPLETION_CANCELLED_TIMEOUT`.
Their source checks are useful consumer smoke controls, not an independently measured fault proof.
The real SQL regression fixture separately covers commit fault/receipt recovery, same-delivery
handler deduplication, rollback and backend mismatch. This sample does not exercise ordinary timer
journal replay or certify its new receiver migrations; run the matching provider controls separately.
Ordinary timer recovery does not close ordinary command/event postcommit dispatch gaps.

The local in-memory bus is an explicit development sink. Its acknowledgement is local enqueue, not
remote durable acceptance or downstream business completion. The SQL outbox remains durable until
the publisher completes the row according to the selected bus acknowledgement. A production host
must configure an appropriate transport, operational dead-letter policy, and idempotent consumers.
Do not infer production end-to-end durability from this local sink.

Atomic scope is state + delivery receipt + original command/event rows + configured timer changes.
Optimistic compare-and-swap may run competing handlers; losing commits are refused. Handler-side
external calls are outside that transaction and must be idempotent. Uncertain commits reuse/query the
original immutable transition; they are not treated as known rollback. Stable membership ordinals and
identities are persisted for the saga transition. Existing publisher/transport ordering remains in force:
there is no new global FIFO/head engine, downstream completion order or exactly-once guarantee.
Consumer PartitionKey/GroupKey/SequenceNumber values are not overwritten to manufacture order.

Only a selected store exposing the required mediated atomic and preparation capabilities can use
this profile. An unsupported selected saga/outbox/timer store is refused rather than silently falling
back to ordinary save-then-dispatch. Ordinary provider registrations remain available with their
ordinary guarantees. No PostgreSQL/in-memory atomic profile is certified here.

Generated JSON metadata is selected explicitly. This managed example also uses current managed
registration and timeout convenience APIs; it is not a NativeAOT certification of the whole application,
fluent state machine, transport or arbitrary custom codec.

## Optional timeout hold/recovery storage demo

After the timeout schemas above, explicitly apply `07-SagaTimeoutFailureHolds.sql` to the selected table. The default two-command workflow is unchanged. On an idle dedicated database with no other due timers or running timeout pump, run:

```powershell
dotnet run --project samples/04-reliability/PrimarySqlAtomicSaga/PrimarySqlAtomicSaga.csproj -c Release --no-build --no-restore -- --timeout-recovery-demo
```

This separate mode enables the framework `AddTenantContext()` registration and establishes its own ambient demo tenant. The default workflow retains its original single-tenant configuration. Opening `TenantContextHolder.BeginScope` alone does not change the configured single-tenant context; tenant mode and authorized scope must agree.

This separate mode uses only public selected-store APIs. It validates usable storage, schedules one unowned source, explicitly simulates a Poison observation from its real claim, commits a hold, queries the actual retained bytes, and conditionally resumes/replays the same command. A fresh claim must preserve source/payload and change delivery cycle. It prints `TIMEOUT_POISON_HELD_AND_QUERIED` and `TIMEOUT_RESUMED_WITH_ORIGINAL_PAYLOAD_NEW_CYCLE`, then cancels its own source with bounded cleanup. It does not claim a handler ran, delivery succeeded or hosted retry classification was exercised. No rows, tokens or receipts are fabricated. Receipt reconciliation in an operational tool must retain the command ID and expected token durably before submitting.

For hosted delivery, Permanent/Poison/Unknown failures are held; Transient/Uncertain retain existing retry behavior. The selected store must expose `ISagaTimeoutFailureStore` and pass startup validation before pumping. Custom implementations must preserve source/delivery epochs, current claim/cancellation authority, typed outcomes and expected-token recovery; registering an unrelated capability does not satisfy the selected-store gate. See the saga guide’s recovery example and provider README. In-memory holds are process-local. Crashes before hold commit can still redeliver, so handlers remain idempotent. This managed storage demo is not NativeAOT, hosted end-to-end or universal HA certification.
