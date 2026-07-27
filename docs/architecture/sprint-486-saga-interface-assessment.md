# Saga Interface Assessment

> **HISTORICAL (Sprint 832 / ADR-333):** The Model B types assessed in this document (`ISagaOrchestrator`, `ISagaStateStore`, `ISagaDefinition<T>`) were deleted in Sprint 832 after determining they had zero concrete implementations. Only Model A (`SagaBase<T>`, `ISagaCoordinator`, `ISagaStore`) ships. See `management/architecture/adr-333-saga-model-unification.md`.

**Task:** S486.4
**Author:** SoftwareArchitect
**Date:** 2026-01-28
**Status:** COMPLETE — *Superseded by ADR-333 (Sprint 832)*

---

## Executive Summary

**DECISION: KEEP SEPARATE - Excalibur.Dispatch.Abstractions saga interfaces are CORRECT and serve a different purpose than Excalibur.Saga interfaces.**

The Excalibur.Dispatch.Abstractions saga interfaces (`ISaga`, `ISagaStore`, `ISagaCoordinator`) define the **messaging layer contract** for saga integration with the Dispatch pipeline. The Excalibur.Saga package **implements** these interfaces, following the correct dependency direction.

---

## Assessment Questions

### Q1: Are Excalibur.Dispatch.Abstractions saga interfaces used anywhere?

**YES** - They are implemented by `Excalibur.Saga`:

| Excalibur.Dispatch.Abstractions Interface | Excalibur.Saga Implementation |
|--------------------------------|-------------------------------|
| `ISagaCoordinator` | `SagaCoordinator` (Orchestration/) |
| `ISaga<TSagaState>` | `SagaBase<TSagaState>` (Orchestration/) |
| `ISagaStore` | Used by `SagaCoordinator` constructor |
| `SagaState` | Base class for `SagaBase<TSagaState>` |

**Evidence from code:**

```csharp
// SagaCoordinator.cs:25-26
public sealed partial class SagaCoordinator(..., ISagaStore sagaStore, ...)
    : ISagaCoordinator

// SagaBase.cs:23-26
public abstract partial class SagaBase<TSagaState>(...)
    : Excalibur.Dispatch.Abstractions.Messaging.ISaga<TSagaState>
    where TSagaState : Excalibur.Dispatch.Abstractions.Messaging.SagaState
```

### Q2: Per CLAUDE.md, should ALL saga interfaces be in Excalibur.Saga?

**NO** - CLAUDE.md states:

> - Dispatch = "HOW messages flow through the system"
> - Excalibur = "WHAT gets persisted and domain modeling"

The current architecture is correct:

| Layer | Responsibility | Interfaces |
|-------|---------------|------------|
| **Excalibur.Dispatch.Abstractions** | Messaging contracts for saga interaction | `ISaga`, `ISagaStore`, `ISagaCoordinator` |
| **Excalibur.Saga** | Implementation + orchestration patterns | `ISagaOrchestrator`, `ISagaStateStore`, `ISagaDefinition<T>` |

### Q3: What is the canonical design for Saga/Dispatch integration?

**Pattern: Dispatch defines contracts, Excalibur implements**

```
                    +-------------------------------------+
                    |       Excalibur.Dispatch.Abstractions         |
                    |   (Messaging Layer Contracts)       |
                    |                                     |
                    |  ISaga<TSagaState>                  |
                    |  ISagaStore                         |
                    |  ISagaCoordinator                   |
                    |  SagaState (base class)             |
                    +-------------------------------------+
                                    ^
                                    | implements
                                    |
                    +-------------------------------------+
                    |         Excalibur.Saga              |
                    |   (Implementation + Patterns)       |
                    |                                     |
                    |  SagaCoordinator : ISagaCoordinator |
                    |  SagaBase<T> : ISaga<T>             |
                    |  ISagaOrchestrator (high-level API) |
                    |  ISagaStateStore (extended store)   |
                    |  ISagaDefinition<T> (step-based)    |
                    +-------------------------------------+
```

This is the **same pattern** as:
- `IDomainEvent` in Excalibur.Dispatch.Abstractions -> Used by Excalibur.EventSourcing
- `IEventStore` in Excalibur.EventSourcing.Abstractions -> Implemented by SqlServer

---

## Interface Comparison

### Excalibur.Dispatch.Abstractions (Messaging Contracts)

| Interface | Purpose | Methods |
|-----------|---------|---------|
| `ISaga` | Core saga instance contract | `Id`, `IsCompleted`, `HandlesEvent()`, `HandleAsync()` |
| `ISaga<TSagaState>` | Typed saga with state access | `State` property |
| `ISagaStore` | Basic persistence contract | `LoadAsync<T>()`, `SaveAsync<T>()` |
| `ISagaCoordinator` | Event routing contract | `ProcessEventAsync()` |

### Excalibur.Saga (Implementation Layer)

| Interface | Purpose | Methods |
|-----------|---------|---------|
| `ISagaOrchestrator` | High-level saga management | `CreateSaga()`, `GetSagaAsync()`, `ListActiveSagasAsync()`, `CancelSagaAsync()` |
| `ISagaStateStore` | Extended state store with queries | `SaveStateAsync()`, `GetStateAsync()`, `UpdateStateAsync()`, `DeleteStateAsync()`, `GetByStatusAsync()`, `MarkExpiredSagasAsync()` |
| `ISagaDefinition<T>` | Step-based saga definition | Defines saga steps and transitions |

**Key Insight:** The interfaces serve different abstraction levels:
- Excalibur.Dispatch.Abstractions = Low-level messaging integration
- Excalibur.Saga = High-level orchestration patterns

---

## Naming Consideration

There is one minor concern: `SagaState` exists in two places:

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `SagaState` | `Excalibur.Dispatch.Abstractions.Messaging` | Base class for `ISaga<TSagaState>` |
| `SagaState` | `Excalibur.Saga.Models` | Extended saga state for `ISagaStateStore` |

**Assessment:** This is acceptable because:
1. `SagaBase<TSagaState>` explicitly uses `Excalibur.Dispatch.Abstractions.Messaging.SagaState`
2. The Excalibur version may extend the Dispatch version (needs verification)
3. No runtime ambiguity exists due to explicit namespace qualification

---

## Recommendation

### Decision: KEEP SEPARATE (No Consolidation)

The current architecture is **correct** and follows CLAUDE.md separation of concerns:

1. **Excalibur.Dispatch.Abstractions** defines the messaging-layer contracts
2. **Excalibur.Saga** implements these contracts + provides orchestration patterns

### Actions Required: NONE

- No code changes needed
- No interface movement needed
- Update qi2si Beads task with findings

### Documentation Update

Add to Canonical Type Locations:

```markdown
| Type Category | Canonical Package | Rationale |
|--------------|-------------------|-----------|
| Saga Contracts (ISaga, ISagaStore) | Excalibur.Dispatch.Abstractions | Messaging layer contracts |
| Saga Implementations | Excalibur.Saga | Per CLAUDE.md boundary |
| Saga Orchestration | Excalibur.Saga.Abstractions | High-level patterns |
```

---

## Conclusion

The Excalibur.Dispatch.Abstractions saga interfaces are **intentionally separate** from Excalibur.Saga interfaces. They follow the correct architectural pattern where:

- Dispatch defines **how sagas integrate with the messaging pipeline**
- Excalibur implements **saga persistence and orchestration logic**

This is NOT a duplication issue - it's proper separation of concerns per the Dispatch-Excalibur Boundary Contract.

---

*SoftwareArchitect | S486.4 Complete*
