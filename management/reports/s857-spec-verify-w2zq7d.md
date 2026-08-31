# Spec Verification — Traceability Matrix: EPIC w2zq7d (Migration Tooling)

**Spec:** `management/specs/w2zq7d-migration-tooling-spec.md`
**Verifier:** TestsDeveloper (TRACEPOINT / VERIFY phase) · **Date:** 2026-06-28 · **HEAD:** `3c69635b`
**Test status:** migration-tooling lane 32/32 GREEN; full 10-shard CI GREEN.

## Acceptance Criteria

| AC | Requirement | Test(s) | Status |
|----|-------------|---------|--------|
| AC-1 | Namespace-swap compiles | `SendConformanceShould.CompileAgainstCompatSurface_WhenOnlyTheNamespaceIsSwapped` + all fixtures compile under compat ns | ✅ FULL |
| AC-2 | Send returns unwrapped response | `ReturnUnwrappedResponse_WhenSendIsCalled` + `…SendObjectOverloadIsCalled` (C3) | ✅ FULL |
| AC-3 | Publish invokes all handlers | `PublishConformanceShould.InvokeAllRegisteredHandlers…` + `InvokeHandlers_WhenGenericPublishOverloadIsCalled` (C3) | ✅ FULL |
| AC-4 | AOT: no IL trim/AOT warnings, no reflection | `c37y1v` AOT gate (`Invoke-AotPublishValidation.ps1`) — 0 IL2069 verified (FE); NFR-1 | ✅ FULL |
| AC-5 | Pipeline order A→B→C→handler→C→B→A | `PipelineOrderingConformanceShould.ExecuteBehaviorsInNestedRegistrationOrder_AroundTheHandler` | ✅ FULL |
| AC-6 | CreateStream yields sequence | `StreamConformanceShould.YieldHandlerSequence_WhenCreateStreamIsCalled` (1,2,3); sample `RunCountdownStream` (3,2,1) | ✅ FULL |
| AC-7 | Handler-not-found exception shape | `Throw_WhenNoHandlerRegisteredForRequest` → `HandlerNotFoundException` | ✅ FULL |
| AC-8 | Duplicate handler fail-fast | `DuplicateHandlerConformanceShould.FailFast_WhenARequestTypeHasTwoHandlers` → `DuplicateRequestHandlerException` (isolated dup-fixture assembly) | ✅ FULL |
| AC-9 | Analyzer EXMIG diag + code-fix (registration) | `AddMediatRRegistrationAnalyzerShould.ReportEXMIG0001…` + `AddMediatRRegistrationCodeFixShould.RewriteToAddMediatRCompat_PreservingTheAssemblyArgument` | ✅ FULL |
| AC-10 | Informational diag, no silent skip | `NonDeterministicConstructAnalyzerShould.ReportEXMIG0002_ForANonPortableConstruct` | ✅ FULL |
| AC-11 | MassTransit guide + feasible shim | `ConsumerShimShould.DispatchToConsumer…` (consumer shim); saga = guide-only (OS-3) | ✅ FULL |
| AC-12 | New packages in sln/CI/manifest | `MigrationToolingCiSyncTests` (FE, packable-aware) — GREEN @3c69635b | ✅ FULL |
| AC-13 | Ported sample builds + tests pass | `Excalibur.MediatRMigration.Tests` (3/3) + sample runtime-verified (FE) | ✅ FULL |
| AC-14 | Perf within ~5% latency / no extra alloc | MediatR comparison benchmarks exist; **bead `2tf65w` OPEN** | ⚠️ PARTIAL |
| AC-15 | using-swap code-fix, no dup/orphan | `MediatRUsingDirectiveCodeFixShould.SwapMediatRUsingToCompatNamespace` + `RemoveRedundantMediatRUsing…` (EC-8) | ✅ FULL |
| AC-16 | Handler-signature diag/fix | `HandlerSignatureCodeFixShould.RenameHandleAsyncToHandle` (EXMIG0004) | ✅ FULL |

## Edge Cases

| EC | Scenario | Test | Status |
|----|----------|------|--------|
| EC-1 | void/Unit request | `CompleteVoidRequest_WhenHandlerReturnsUnit` | ✅ |
| EC-2 | Open-generic handler | `NotAutoBindOpenGenericHandler_DocumentedManualStep` (OS-1 ruling bd-1xeopz) | ✅ (OS-1) |
| EC-3 | Publish zero handlers = no-op | `BeNoOp_WhenNoHandlersRegistered` (Unheard) | ✅ |
| EC-4 | Behavior short-circuits | `NotInvokeHandler_WhenBehaviorShortCircuits` | ✅ |
| EC-5 | Behavior/handler throws | `PropagateException_WhenBehaviorThrows` | ✅ |
| EC-6 | Cancellation → OCE (Send/Publish/stream) | `SurfaceOperationCanceled…` ×3 (Send, Publish, Stream) | ✅ |
| EC-7 | Partially-migrated file, idempotent | analyzer matches only exact `using MediatR;` (qualified compat ns not re-flagged) — idempotent by construction; + EC-8 test | ⚠️ PARTIAL (by-construction; no explicit mixed-file test) |
| EC-8 | Duplicate using after swap | `RemoveRedundantMediatRUsing_WhenCompatAlreadyImported` | ✅ |
| EC-9 | Value-type response no-box | `ResolveValueTypeResponse_WithoutBoxingOnTheHotPath` | ✅ |

## Functional / Non-Functional (key)

| ID | Requirement | Coverage |
|----|-------------|----------|
| FR-1..8 | Compat surface + Send/Publish/stream/behavior/not-found/dup | ✅ (AC-1/2/3/5/6/7/8) |
| FR-9 / NFR-4 | API isolation (canonical baselines unchanged) | ✅ `uwn5g5` `CompatSurfaceIsolationTests` (2) — closed |
| FR-10..14 | Analyzer/code-fix (EXMIG0001-0004) | ✅ Migration.Tests 8/8 |
| FR-15/16 | MassTransit path | ✅ `i5hrxo` + guide |
| FR-17 | CI/CD sync | ✅ AC-12 |
| FR-18 | Runnable sample | ✅ AC-13 |
| NFR-1 | AOT/trim safety (hard gate) | ✅ `c37y1v` 0 IL2069 |
| NFR-2 | Behavioral fidelity | ✅ AC-5 ordering + routing-through-dispatch `95tyq1` |
| NFR-3 | Performance | ⚠️ `2tf65w` open |

## Coverage Summary

| | Total | Covered | Partial | Missing |
|--|-------|---------|---------|---------|
| Acceptance (AC) | 16 | 15 | 1 (AC-14) | 0 |
| Edge Cases (EC) | 9 | 8 | 1 (EC-7) | 0 |
| Functional (FR) | 18 | 18 | 0 | 0 |
| Non-Functional (NFR) | 5 | 4 | 1 (NFR-3) | 0 |

**VERDICT: FULL COVERAGE of all MUST requirements. 2 non-critical (SHOULD-tier) gaps.**

### Non-critical gaps
- **AC-14 / NFR-3 (perf)** — bead `2tf65w` OPEN. MediatR comparison benchmarks exist in `benchmarks/`; the S857 compat-vs-canonical ~5%-latency/no-extra-alloc benchmark assertion is not yet closed. Perf is SHOULD-tier; recommend carrying `2tf65w` (or confirming the existing benchmarks satisfy AC-14).
- **EC-7 (partial-migration idempotency)** — guaranteed by construction (analyzer matches only the exact `using MediatR;` identifier; the swapped qualified compat namespace is not re-flagged) and adjacent EC-8 is tested, but there is no explicit mixed-incumbent+Excalibur-usings idempotency test. Low risk; optional hardening test.

### Orphaned tests
None — every migration-tooling test traces to an AC/EC/FR. (Fixture helpers `Handle`/`Consume`/`ProcessAsync`/`Reset` are test doubles, not orphan tests.)

### Step 1b — Observability staleness
N/A — the compat surface is **additive** (new packages routing onto the canonical pipeline); it does not flip an existing routing/behavior path, so there are no stale OLD-behavior log/metric templates to reconcile.
