# Excalibur.Workflows.Abstractions

Durable workflow execution abstractions for the Excalibur framework.

Durable workflows run business processes that survive process crashes, restarts, and long waits by
recording every non-deterministic decision to an append-only journal and deterministically replaying it.

## Contents

- **`IWorkflowContext`** — the determinism boundary. The only place a workflow body may perform a
  non-deterministic operation (time, identifiers, activity calls, timers, external signals). Every call is
  recorded to the journal and its result replayed deterministically.
- **`IActivity<TInput, TOutput>`** — the at-least-once unit of side-effecting work. Activities must be
  idempotent because a crash between execution and journaling can replay them.
- **Workflow journal event schema** — `WorkflowJournalEvent` and its discriminators
  (`WorkflowStarted`, `ActivityScheduled`, `ActivityCompleted`, `ActivityFailed`, `TimerCreated`,
  `TimerFired`, `SignalReceived`, `WorkflowCompleted`) — the append-only history replayed to reconstruct
  workflow state.
- **`IWorkflowExecutor`** — the replay-engine seam that starts workflows, delivers signals, and reports
  status.
- **`WorkflowAttribute`**, **`WorkflowStatus`**, **`WorkflowOptions`** — the marker, status enum, and
  options.

## Design

- **Deterministic replay.** All non-determinism flows through `IWorkflowContext`; the executor records each
  call and replays the recorded result, so re-executing a workflow body yields the same decisions.
- **At-least-once activities.** Activities are idempotent; journal-native deduplication short-circuits a
  re-execution when a completion is already recorded.
- **Provider → abstraction.** This package depends only on the Excalibur event-sourcing abstractions; the
  replay engine and persistence live in downstream packages.
