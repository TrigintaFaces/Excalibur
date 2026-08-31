# Sprint 873 — VERIFY/TRACEPOINT Spec-Verify Traceability Matrix

**Committed HEAD:** `f1af8cec5308de2468709d8891bf3bab10309142`
**Suite state:** GREEN on HEAD (TEST phase passed 10/10 shards, 0 regressions) — so all coverage that exists is passing.
**Scope:** the 17 delivered in-sprint S873 lanes (per `management/sprints/sprint-873-plan.md` SPEC output). `maf76z` was carved to next sprint (not delivered) and is excluded.

## Traceability matrix

| Lane / bead | AC summary | Test file(s) | Coverage | Notes |
|---|---|---|---|---|
| `dudtjx` [P2] | SqlServer inbox **builder path** enforces SQL-identifier allowlist (parity with options path) | `tests/unit/Excalibur.Data.SqlServer.Tests/SqlServer/Inbox/SqlServerInboxBuilderPathValidationShould.cs` | **COVERED** | Theories reject malicious schema **and** table names via the builder path (`OptionsValidationException`) + a positive control. Failure-path locked. |
| `7n7hsb` [P2] | CachingMiddleware value-type result covariance gap — value-type `IMessageResult<int>` now extracted & cached | `tests/unit/Excalibur.Dispatch.Caching.Tests/CachingMiddlewareCacheDecisionShould.cs`; F-5: `tests/unit/Excalibur.Dispatch.Middleware.Tests/Caching/CachingCoverageBoostShould.cs` | **COVERED** | `Cache_ValueTypeResult_…` (handler runs once, 2nd served from cache) + reference-type positive control. F-5 flip asserts the described caching decision (Value extracted), not just non-null. |
| `w4181o` [P2] | `ICacheable<T>.ShouldCache` honored on the interface path | `…/CachingMiddlewareCacheDecisionShould.cs`; F-5: `…/CachingCoverageBoostShould.cs` | **COVERED** | `NotCache_WhenICacheableShouldCacheIsFalse_w4181o` — `ShouldCache=false` suppresses caching (both requests execute). F-5 asserts `ShouldCache=false` factory path. |
| `li1miu` [P2] | DoD CloudEvent validator OR→AND across 5 transports; every required field enforced | `tests/unit/Excalibur.Dispatch.Transport.Tests/CloudEvents/DoDComplianceValidatorShould.cs` | **COVERED** | Theories ×5 transports: accept-all-present + reject on missing correlationId / userId / traceParent + the regression-targeted `Reject_WhenOnlyTraceParentPresent` (old OR behavior). |
| `ma1jc7` [P3] | Saga auto-cleanup WIRE — `SagaOptions.EnableAutomaticCleanup` is a real end-to-end driver, resolvable from DI | `tests/unit/Excalibur.Dispatch.Patterns.Tests/Sagas/Services/SagaCleanupBackgroundServiceShould.cs`; `tests/unit/Excalibur.Saga.Tests/DependencyInjection/SagaDefaultStoreRegistrationShould.cs` | **COVERED** | Purge-when-enabled / not-purge-when-disabled (TimeProvider-driven) **and** `ResolveSagaCleanupService_FromDIContainer` proves real resolve-through (not merely registered) — satisfies `enforce-invariants-structurally` WIRE bar. DI test adds fail-fast + opt-in registration. |
| `xh4jru` [P3] | DecorrelatedJitter enum + calculator + retry-loop threading (attempt-derived, durable/stateless) | `tests/unit/Excalibur.Dispatch.Tests/Messaging/Resilience/DecorrelatedJitterBackoffCalculatorShould.cs`; F-5: `tests/unit/Excalibur.Dispatch.Tests/Resilience/RetryMiddlewareBackoffMultiplierShould.cs` | **COVERED** | Floor-at-baseDelay (vs Full-Jitter zero), first-attempt `[base,base·3]` window, and previous-actual-delay threading (geometric growth) — pins the arm so an attempt-derived impl goes RED. F-5 confirms multiplier path unchanged + `previousDelayMs` wired. |
| `63elhi` [P3] **CUT** | OTel CB/DLQ metrics facade cut (no emitter) + meter names repointed to real emitters | `tests/unit/Excalibur.Dispatch.Observability.Tests/Metrics/OpenTelemetryExtensionsShould.cs` | **COVERED** | `AllMeterNames` boundary: registers all expected meters, ≥19 count, no-duplicates. Commit `8b199cfd0!` cut facade + repointed. |
| `yk8dq5` / `f62rwx` / `iz3wwu` [P2] | Firestore/DynamoDb/Cosmos projection stores source the canonical projection serializer (no bypass) | `tests/architecture/Boundary.Tests/EventStoreCanonicalSerializerGuardTests.cs` (`EveryProjectionStore_SourcesProjectionCanonicalSerializerOptions_NoBypass`) | **COVERED** | Structural guard scans every projection store for a `JsonSerializer` bypass of `ProjectionSerializationDefaults.CreateReadModelOptions()`; allowlist self-checks (files still exist + still violate); non-vacuity test flags inline-bag + zero-options bypass. Converged in `c07637f7c`. |
| `fvvqn8` [P2] | DispatchJsonSerializer reader-side sources `EventSerializationDefaults.Canonical` (single source of truth) | `tests/architecture/Boundary.Tests/EventStoreCanonicalSerializerGuardTests.cs` (`EveryEventStore_SourcesCanonicalSerializerOptions_NoBypass`) | **COVERED** | Same guard family for the event-store canonical factory (`EventSerializationDefaults`); non-vacuity + allowlist locks present. Converged in `c07637f7c`. |
| `6v59o0` [P2] | Confluent receive-path framing strip (5-byte magic+schema-id header removed when decoding enabled) | `tests/unit/Excalibur.Dispatch.Transport.Tests/Kafka/KafkaConfluentFramingStripShould.cs` | **COVERED** | Strip-when-enabled + pass-through-when-disabled (non-vacuity) + non-framed-passthrough-when-enabled (edge). Impl in `2539a00be`. |
| `hl7tzr` [P3] **CUT** | Cut unwired AwsSqs DeadLetterQueue scaffolding (`DlqProcessor` + impl-less interfaces) + PublicAPI + F-5 + ADR-113 correction | Removal-verified: `DlqProcessor` absent from src (`417c3cbbc`); orphan tests deleted (`a3e08d972`); `tests/unit/Excalibur.Dispatch.Transport.Tests/CrossTransport/DlqUniversalityShould.cs` | **COVERED** | `FourTransports_ImplementIDeadLetterQueueManager` + `AwsSqs_HasNoIDeadLetterQueueManager_ByDesign` structurally certify the cut is intentional; conformance retained. F-5 orphan-test sweep done. |
| `c4anmz` [P3] | Emit `dispatch.exactlyonce.duplicates.suppressed` counter on suppressed duplicate | `tests/unit/Excalibur.Dispatch.Messaging.Tests/Messaging/Delivery/InMemoryDeduplicatorMetricsShould.cs` | **COVERED** | First sighting → counter 0; 2nd (unexpired) sighting → counter increments. Non-load-bearing observability lane, fully locked. |
| `30y2xd` [P3] **CUT** | Remove dead `SqlServerInboxStore.DeserializeMetadata` (internal, no PublicAPI) | Removal-verified: absent from `src/Excalibur/Excalibur.Inbox.SqlServer/SqlServerInboxStore.cs` (`601d7933c`) | **COVERED** | Verified by removal. (Other `DeserializeMetadata` occurrences in Snapshot/Cosmos/Mongo stores are unrelated live code — correctly untouched.) |
| `bg9abl` [P3] DOC | ConfluentJsonDeserializer intentional canonical-serializer deviation marker | `src/Dispatch/Excalibur.Dispatch.Transport.Kafka/SchemaRegistry/ConfluentJsonDeserializer.cs:65` | **COVERED** | Doc-only lane; marker present ("Intentional bespoke-interop deviation … NOT the event canonical STJ contract"). No behavioral lock required (`edf04ed3e`). |
| `4qtxn5` [P?] | TelemetryInboxStoreDecorator lease-claim overload emits operation telemetry | `tests/unit/Excalibur.Data.SqlServer.Tests/Diagnostics/TelemetryInboxDecoratorLeaseClaimTelemetryShould.cs` | **COVERED** | `EmitTryClaimLeaseTelemetry_OnTheLeaseClaimOverload` asserts the `try_claim_lease` operation tag is recorded on the path `InboxMiddleware` full-mode uses (`cd7519e0b`). |
| `21v7wk` [P3] | Complete partial Kafka/Routing CUT — verified already-satisfied | (no new work) | **N/A — already satisfied** | Closed with no new commit; prior sprints already completed the CUT. Tracker status `closed`. Not a coverage gap. |

## Summary

- **Total delivered lanes assessed:** 17 (16 requiring verification + `21v7wk` already-satisfied)
- **COVERED: 16** — every AC maps to an existing, passing test (or removal-verification for CUT lanes), with failure/edge dimensions present where applicable.
- **PARTIAL: 0**
- **GAP: 0**
- **N/A (already-satisfied, no new work): 1** (`21v7wk`)

## Gaps / Partials

**None.** Every delivered S873 lane has traceable, passing verification:
- WIRE lanes (`ma1jc7`, `6v59o0`) structurally prove consumer resolve/read-through, not mere registration.
- CUT lanes (`63elhi`, `hl7tzr`, `30y2xd`) are verified by removal + the F-5 orphan-test/sample/sibling sweep + design-intent structural locks.
- Bug-fix lanes (`dudtjx`, `7n7hsb`, `w4181o`, `li1miu`) each carry a failure-path regression lock plus a positive control.
- Serializer family (`yk8dq5`/`f62rwx`/`iz3wwu`/`fvvqn8`) is covered by the non-vacuous, allowlist-self-checking canonical-serializer boundary guards (both projection-store and event-store variants).
- Doc lane (`bg9abl`) verified by marker presence.

No test or source file was modified in producing this report.
