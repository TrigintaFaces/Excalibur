---
sidebar_position: 3
title: Runtime Contract
description: Canonical runtime semantics for Excalibur execution paths
---

# Runtime Contract

This guide defines the runtime guarantees that Excalibur provides during message execution.

## Execution Modes

Dispatch runs in one of two modes:

- **Local mode**: message is handled in-process.
- **Transport mode**: message is routed through configured transport adapters.

Routing is resolved before pipeline execution.

## Pipeline Contract

Execution order is:

1. Dispatcher receives message + caller cancellation token.
2. Route decision is resolved.
3. Middleware executes in registration order.
4. Final handler/transport execution occurs.
5. Result is returned to caller.

## Cancellation Contract

- Cancellation tokens are part of the API contract and must propagate end-to-end.
- `DispatchChildAsync` inherits correlation and cancellation budget.
- Canceled requests must not continue work in the same request pipeline.

## Context Contract

When context is materialized, these fields are expected:

- `CorrelationId`
- `CausationId`
- message identity/type metadata

Lean local paths can defer full context creation, but correlation/causation semantics must remain correct whenever context is requested.

## Retry and Poison Contract

- Retry behavior is policy/profile based.
- Exhausted retries route to poison/dead-letter handling when configured.
- Dead-letter records must retain actionable failure metadata.

## Release Enforcement

This contract is release-blocked by:

- transport conformance tests,
- release-blocking CI test governance gate,
- architecture/governance validation.

## See Also

- [Reliability Guarantees](reliability-guarantees.md)
- [SLO, SLI, and Telemetry](slo-sli-telemetry.md)
- [Incident Runbooks](incident-runbooks.md)
