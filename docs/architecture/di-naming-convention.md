# DI Naming Convention: AddExcalibur* vs AddDispatch*

> **Sprint 804 update (ADR-321/325):** The family of per-subsystem `AddExcalibur{X}()` aggregators (Audit, Saga, EventSourcing, Outbox, LeaderElection, ProjectionCaching, JobHost) has been **internalized**. The canonical public surface is now the unified `services.AddExcalibur(excalibur => excalibur.Add{X}(...))` builder path — the historical per-subsystem entries are retained below for archival context only.

## Convention

| Package Family | DI Prefix | Example |
|---------------|-----------|---------|
| `Excalibur.Dispatch.*` (Dispatch packages) | `AddDispatch*` | `AddDispatch()`, `AddDispatchPipeline()`, `AddDispatchHandlers()` |
| `Excalibur.*` (Excalibur packages) | Unified builder | `services.AddExcalibur(x => x.AddEventSourcing(...))`, `... x.AddOutbox(...)`, `... x.AddSaga(...)` (per ADR-321/325) |

## Rationale

The prefix indicates which framework layer the registration belongs to:

- **`AddDispatch*`**: Core message dispatching, pipeline, middleware, handlers, and transport-level concerns.
- **`AddExcalibur*`**: Application framework concerns (event sourcing, sagas, hosting, data access, compliance).

Consumers searching for DI methods should use the prefix matching their dependency:
- Using `Excalibur.Dispatch`? Search for `AddDispatch`.
- Using `Excalibur.EventSourcing`? Search for `AddExcalibur`.

## Internal Cross-Registration

Methods in `Excalibur.*` packages may *call* `AddDispatch*()` internally to register
Dispatch-layer dependencies (e.g., `AddExcaliburEventSourcing()` calls `AddDispatch()` internally).
This is expected -- the prefix convention applies to the method the consumer calls, not internal wiring.
