# Opting into primary SQL atomic transitions

The generated Program remains the ordinary SQL/PostgreSQL/in-memory composition selected when
scaffolding. Merely choosing SQL storage does not opt into atomic command/event/timer transitions.
The in-memory variant is a single-process demonstration and loses state/timers on restart.

For the primary SQL atomic profile, use the complete repository example in
`samples/04-reliability/PrimarySqlAtomicSaga`. Keep the ordinary template variants intact. Add the
`Excalibur.Outbox.SqlServer` package at the same `ExcaliburDispatchVersion` as the existing Saga SQL
package; configure its store and the SQL timeout store to the same logical server/catalog as saga
state before selecting `AddDurableSagaTransitions(AppJsonContext.Default)`. Apply the matching
provider schema scripts explicitly: base state/timeout/outbox schemas, then
`03-SagaDurableEffects.sql`, `04-SagaTimeoutAuthority.sql` and the nonce-aware state upgrade
`05-SagaTimerRecoveryState.sql`. For ordinary timer recovery interoperability on the same timeout
table, also apply `06-SagaTimerRecovery.sql` and register its actual selected physical table before
starting writers. There is no automatic migration. Do not let `WithTimeouts()` supply its in-memory
fallback for this selected profile.

Ordinary PostgreSQL state uses `02-SagaTimerRecoveryState.sql`; Oracle state and timeout recovery
use `02-SagaTimerRecoveryState.sql` and `03-SagaTimerRecovery.sql`. These are provider-specific
ordinary timer contracts, not additional atomic mixed-effects profiles. Ordinary timer journals
preserve original IDs through recovery, cancellation and retirement, but do not make ordinary outgoing
commands/events atomic with state. Keep handlers/consumers idempotent and preserve selected-store
facet forwarding through decorators.

Register generated metadata for every actual state, command/event payload, timer payload and
object-valued header type. Use `SystemTextJsonSerializer.Create(AppJsonContext.Default)` with the
existing `AddPluggableSerializer` registration and `OutboxMetadataSerialization(options => new
AppJsonContext(options))`; state metadata alone does not make an arbitrary payload codec safe.
Use the public typed `SagaCoordinator.ProcessAtomicEventAsync<TSaga,TState,TEvent>` entry for the
explicit generated route and the existing publisher/background drain for committed outbox rows.
The example's local bus is a development acknowledgement sink, not a durable production transport.

Configured timeouts are part of the same SQL transaction. A production timed-delivery host additionally
needs the existing registered saga timeout handler/pipeline and delivery service; do not manually
reconstruct original owned timeout identities or report a read-only due query as delivery.
Unsupported selected stores refuse this profile before handler/commit instead of silently weakening it.

Atomic persistence is not exactly-once downstream processing or cross-node handler serialization.
Keep handlers/consumers idempotent; preserve original delivery IDs on replay. Existing drain/transport
ordering applies, without a new global FIFO guarantee. The template's assembly-scanning handler
registration and fluent/runtime paths are not NativeAOT-certified by generated saga metadata.

The existing `samples/04-reliability/PrimarySqlAtomicSaga` also provides an optional `--timeout-recovery-demo` storage-protocol example. Apply SQL timeout migration `07-SagaTimeoutFailureHolds.sql` first and use its documented idle dedicated database. The default two-command example is unchanged. Hosted timeout delivery requires the selected timeout store to expose usable `ISagaTimeoutFailureStore` before startup; a custom store must implement that contract rather than register an unrelated facet. The demo explicitly simulates Poison and proves hold/query/conditional resume with real store receipts; it is not a hosted dispatch or NativeAOT certification.
