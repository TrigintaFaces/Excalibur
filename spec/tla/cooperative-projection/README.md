# Cooperative projection design model

The full canonical run on 2026-10-29 checked the current visibility-barrier model after dead-scratch-state normalization. All nine protocol arms and the checker self-test returned their expected results. `checked-design-summary.json` records the exact model and toolchain hashes; compare its model hash with the source before citing it. Earlier results for the pre-barrier model are superseded.

**Scope remains limited:** the historical manifest binder no longer matches production admission. Its mismatch/refusal arms are historical evidence, not a claim about today's per-projection roster. Current binding lifetime checks live in `../subscription-roster/BindingLifetime.tla`. This model does not establish external side-effect uniqueness, arbitrary provider behavior, or an R4 claim for every event-sourcing guarantee. Read the module's correspondence and limits before using its results.

This is a finite design model, checked before the corresponding production concurrency change. It replaces leadership exclusion with cooperating readers, positioned projection writes, and an expected-position checkpoint CAS. It does not implement leadership-token fencing or prove uniqueness of external handler effects.

**What the model is for.** A row CAS makes a write *accepted*; it does not make the write
*readable*. The checkpoint is an acknowledgement to the outside world, so acknowledging a position
whose rows are not yet readable is the silent loss: the page is never replayed and the reader never
sees those events. `CheckpointCoverage` (over `rowValue`, what the write path accepted) is
therefore **not** the property that matters — `CheckpointVisibility` (over `visible`, what a reader
can observe) is. Both are checked, because the weaker one catches a different defect.

**The barrier is awaited even on a page that wrote nothing.** A page that skipped every write as
duplicate or superseded observed a *competing* writer's state, which may be accepted but not yet
refreshed. That is `skip-barrier-when-not-writing`, and it is a reachable silent loss rather than a
theoretical concern.

Run the nine protocol checks and checker self-test with an existing Java runtime and TLC jar:

```powershell
python check-cooperative-projection.py --java '<java.exe>' --jar '<tla2tools.jar>' --output '<new evidence directory>'
```

Exit 0 means every requested arm returned its expected answer. Exit 1 means an unexpected answer. Exit 2 means the toolchain was unavailable, a run timed out, or Java exhausted its heap; none is a pass. An observed invariant failure remains a failure even if another arm cannot finish. Evidence includes complete logs, generated configurations, TLC state artifacts, Java version, jar SHA-256, and model SHA-256. A subset selected with `--only` is evidence for that subset only.

| Arm | Required answer |
| --- | --- |
| healthy | All invariants and temporal `Completion` hold |
| mixed-manifest-rejected | All invariants and temporal `Completion` hold; mismatched participants are refused |
| skip-manifest-check | `ManifestAgreement` violation |
| blind-stale-row-write | `RowNeverRewinds` violation |
| premature-checkpoint | `CheckpointCoverage` **or** `CheckpointVisibility` violation |
| cancel-waiter-overrelease | `GateOwnership` violation |
| skip-barrier | `CheckpointVisibility` violation |
| skip-barrier-when-not-writing | `CheckpointVisibility` violation |
| barrier-fault-acknowledges | `CheckpointVisibility` violation |
| checker-self-test | `NeverThree` violation |

**Why `premature-checkpoint` accepts either invariant, and why that is not a weakening.** One broken
mechanism can violate more than one invariant, and TLC reports whichever reachable violation it
encounters first — which is not stable across worker counts. Asserting a single name would make a
correct arm flake; asserting nothing would accept *any* violation, including one caused by a modelling
error rather than by the planted mutant. The runner therefore takes a **set** of predicted invariants
per arm and requires the violation to be one of them: the mutant must break something, and it must be
something we predicted.

**What the self-test does and does not establish.** `checker-self-test` runs `NeverThree` in a separate
toy module, so it proves only that TLC can report a violation *at all* on this toolchain. It says
nothing about whether *this* model's invariants can go red. That is what the six mutant arms establish,
each against a named invariant of the real model — and `CheckpointVisibility` in particular would
otherwise be satisfiable by a model that simply never acknowledges anything, which is the
inaction-passes-safety trap.

**A non-vacuity argument that needs no toolchain.** A recorded `RowNeverRewinds` violation is itself
proof that two actors were concurrently inside the read/CAS window: a lone actor recomputes
`target = cp + 1` from the current checkpoint on every page, so it can never carry a target below a
row's high-water mark, and a rewind is therefore unreachable for one actor by construction.

**The precondition for `skip-barrier-when-not-writing`, stated exactly, because a looser statement of
it makes the arm look vacuous.** The naive description — *"an actor reads a row another writer already
advanced, skips the write, and acknowledges"* — is **wrong**, and anyone reproducing it would find no
violation and conclude the invariant is unreachable. `skipsBarrier` is consulted only inside the
`lastRow` branch, and `lastRow` requires `row[a] = RowCount(Descriptor(a))`, which is
`Cardinality({1,2}) = 2`. So on row 1 the actor never reaches the test at all: it goes to `Load` with
`row' = 2`.

**The competing writer must therefore have settled the ENTIRE required row set before the skipping
actor's first `Load`.** If it had settled only row 1, then at row 2 the skipping actor finds
`rowPos[2] = 0 = readPos` with `readPos < target`, so `writes` is TRUE, `wroteAny` becomes TRUE,
`skipsBarrier` is FALSE — **and it takes the barrier.** No violation. The arm lives or dies on the
competing writer having settled every required row first, which is why it needs two actors in two
processes and no faults rather than merely two writers.

*(Superseded, quoted so a reader who inherited it recognises it: an earlier description of this trace
said the skipping actor "goes straight to Commit" after settling row 1. It does not — `lastRow` is
FALSE there. The correction came from an adversarial review and was verified against the action's phase
transition before being recorded here.)*

The reachable trace is 11 steps, two actors in two processes. B (Jobs/Worker) settles both required
rows to target 1 and parks at `Barrier` without committing, leaving `cp = 0` and `visible` empty. A
(API/Web) then pages with `base = 0, target = 1`; at row 1 it finds `readPos = 1`, so `writes` is FALSE
and `settled` TRUE, but `lastRow` is FALSE so it moves to row 2; at row 2 the same holds, `lastRow` is
now TRUE, `wroteAny` is still FALSE, so the mutant's `skipsBarrier` is TRUE and A goes to `Commit`,
setting `cp = 1` while `visible` is still `[1↦{}, 2↦{}]`. `Prefix(1) = {1} ⊄ {}` — violated.

**Two properties of that trace are worth knowing before anyone edits the model.** It needs **no fault**
— no `Crash`, no `CancelWaiter`, no `BarrierFault` — so the arm is insensitive to any later change in
the fault budgets, which are the part of the model most likely to be edited next. And in that state
**only `CheckpointVisibility` can fire**: the write path is entirely normal, so `CheckpointCoverage`,
`VisibleNeverExceedsWritten`, `RowNeverRewinds`, `RowIsPrefix`, `GateOwnership`, `ManifestAgreement`
and `CheckpointNeverRewinds` all hold. The same is true of `skip-barrier` and
`barrier-fault-acknowledges`, so each barrier arm is a **single-invariant arm in practice** and the
order of the `INVARIANTS` line cannot shadow the one being tested.

**`barrier-fault-acknowledges` is reachable with a single actor**, which makes it much the cheapest of
the three to find: one actor writes both rows, reaches `Barrier`, `BarrierFault` fires, the mutant
routes it to `Commit`, `cp` advances, and `visible` was never published. No concurrency at all.

The model has three actors: API and background polling in one process, plus Jobs in another. A fair FIFO gate admits a finite page in each process. Two committed events affect two projection rows. Read, per-row positioned write, and checkpoint write are independent steps. One invocation crash and one waiter cancellation per actor are allowed, and durable rows/checkpoints survive retries. The manifest abstracts the complete source, scope, work-set, destination, and consumer semantic revision under one stable name. The first atomic binder chooses the descriptor; later mismatches are refused before reading the stream.

Invariants cover permit ownership, manifest compatibility, row monotonicity, valid prefix folds, checkpoint coverage of every required row, checkpoint monotonicity, and — the pair added with the barrier — that a reader never observes more than the write path accepted (`VisibleNeverExceedsWritten`) and that nothing is acknowledged in the checkpoint that a reader cannot yet see (`CheckpointVisibility`). Temporal completion assumes weak fairness of each actor's ordinary steps, finite input, bounded faults, and terminating callbacks/storage. FIFO is a modeled requirement; this result does not establish fairness of a particular semaphore implementation.

Limits: one-event pages, two rows, two events, no dynamic routing, no erasure/rebuild racing with normal folds, no unbounded fault stream, no external side effects, and no actual provider implementation. **On visibility specifically**, the barrier is one atomic step that publishes every required row, so per-row and per-index refresh granularity, partial refresh, and refresh cost are not modeled; and a reader's own staleness *after* a successful barrier (a replica lagging the refreshed primary) is not modeled. The model establishes the **ordering obligation** — no acknowledgement before visibility — and **no bound** on how stale a reader may be once the barrier has returned. All participating production stores must enforce positioned writes; falling back to unconditional upserts invalidates the premises. This is evidence about the stated protocol, not proof of production code or general parameter sizes.

## Discarded page state

`Release` and `Crash` reset the actor's six page-local scratch fields to their initial values. This merges states that differ only in completed or abandoned page history; it does not reduce the three actors, two processes, two rows, two events, or per-actor fault budgets.

| Scratch field | Live use | Overwritten before the next use |
| --- | --- | --- |
| `base` | Checkpoint comparison in `Commit` | `Page` |
| `target` | `Apply` and `Commit` | `Page` |
| `row` | `Load` and `Apply` | `Page` |
| `readPos`, `readValue` | Corresponding `Apply` after `Load` | `Load` |
| `wrote` | Across applications within one page, including the skipped-barrier mutant | `Page` |

Both reset transitions enter `Wait` or `Done`. `Acquire` reads no scratch fields; `Page` and `Load` overwrite them before their next use. The state relation preserves durable state, phases, gates, failure budgets and live scratch values. Enabled transitions and phase progression therefore agree, including `ActorStep` weak fairness and temporal `Completion`. The safety invariants do not inspect discarded scratch values; `TypeOK` only requires `wrote` to remain Boolean, which resetting it to `FALSE` preserves.

Active `Load`–`Apply` windows remain unchanged, preserving the stale-write counterexample. `wrote` survives all applications in the current page, preserving the skipped-barrier controls. Crash, cancellation and barrier-fault budgets are never reset. This optimization still requires the full checker run; a timeout remains a refusal.

The checker defaults to a 1,800-second timeout per arm and a 4,096 MiB Java heap. Override these with `--timeout` and `--heap-mb`, or `TLA_COOPERATIVE_TIMEOUT` and `TLA_COOPERATIVE_HEAP_MB` when using the canonical shell runner. These settings affect resources, not model bounds. TLC documents that an undersized heap can crash exploration and an oversized heap can slow the host; see its [memory options](https://nightly.tlapl.us/doc/model/tlc-options-page.html).
