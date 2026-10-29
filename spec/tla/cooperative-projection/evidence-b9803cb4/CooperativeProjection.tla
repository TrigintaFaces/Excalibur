------------------------ MODULE CooperativeProjection ------------------------
EXTENDS Naturals, FiniteSets, Sequences, TLC

(***************************************************************************
SCOPE
Three actors: API and Background share a process-local FIFO page gate; Jobs
runs in a second process. Two required projection rows and a finite two-event
stream. A page is one event: it reads a checkpoint, reads/folds/CASes each row,
AWAITS SEARCH VISIBILITY for the rows it observed, and only then CASes the
checkpoint. A crash can occur between any two durable operations and releases
only the crashed actor's local gate. One cancellation, one crash and one
barrier fault per actor bound failures.

CORRESPONDENCE, ONE LINE PER ACTION. Stated per action rather than per concern
because a per-concern list hides a missing step: if every action names its
production site, an unmodeled step has nowhere to appear and the gap announces
itself. Verified against the tree at the SHA that introduced both this model and
the implementation it describes; re-verify before relying on a line number.

  Bind          SubscriptionDefinitionValidation.Validate, reached from
                AsyncProjectionProcessingHost.cs:267-308; the durable binding is
                on the public contract at ISubscriptionCheckpointStore.cs:52
  Acquire       the process-local page gate taken before a page is processed
  Page          the checkpoint read that establishes base and target
  Load          the projection row read taken alongside its stored position
  Apply         PositionedProjectionWriter.cs:179-181 (UpsertAtPositionAsync)
  Barrier       AsyncProjectionProcessingHost.cs:460 (await EnsureVisibleAsync),
                contract at IProjectionVisibilityBarrier.cs:9-12
  Commit        AsyncProjectionProcessingHost.cs:232-233 (AdvanceCheckpointAsync),
                over ISubscriptionCheckpointStore; sampled implementation
                SqlServerSubscriptionCheckpointStore.cs:208. Oracle, Postgres and
                Sqlite were NOT read, and that is a gap, not a claim.
  Release       returning the page gate
  Crash / CancelWaiter / BarrierFault   bounded fault injection, not production
                sites

*** CORRESPONDENCE WITHDRAWN FOR THE MANIFEST BINDER. READ THIS BEFORE CITING
*** ANY ARM OF THIS MODEL AS EVIDENCE ABOUT THE SHIPPED SYSTEM.

The manifest mechanism this model checks NO LONGER EXISTS in production. Here,
`manifest` is a single scalar for the whole subscription, bound first-writer-wins,
and an actor whose Descriptor differs is REFUSED -- see the `accepted` definition
in Admit. Production now binds ONE ROW PER PROJECTION, and an actor hosting a
DIFFERENT SET OF PROJECTIONS IS ADMITTED rather than refused; what it may not do
is attest freshness over a member it does not host. So the shipped arm
(MixedManifest = FALSE) models an agreement the system no longer requires, and
the MixedManifest = TRUE arm asserts a refusal the system no longer performs.

Every OTHER property in this model -- the page gate, row CAS, prefix, checkpoint
coverage and the visibility barrier -- is unaffected by that change and its arms
remain meaningful. It is ONLY the manifest/admission half that is out of
correspondence. The distinction matters: this is not a dead model, it is a model
with one stale mechanism, and deleting it would discard checked properties that
still hold.

What a replacement owes, stated here so it is not re-derived: `manifest` becomes
a SET of bound members rather than a scalar; admission no longer depends on set
agreement; and the invariant that matters is that no actor reports freshness
while a bound member outside its own set sits below the attested position --
NoFalseFresh. That invariant does not exist in this model, because the design it
checks made the state it forbids unreachable by refusing admission instead.

A proof is valid only inside the state space it models. This block exists so the
next reader cannot inherit a correct proof about a design that is gone.

An earlier revision of this block said "This is a DESIGN model; no production
correspondence is asserted yet" and called the manifest binder "new"; a later one
replaced that with "THE MODEL AND THE IMPLEMENTATION LANDED TOGETHER, so this is
a correspondence and not a proposal." Each was true when written. Both are quoted
so a reader who inherited either recognises which claim they are holding.

THE ROW CAS IS MODELED AS BINARY; PRODUCTION IS FOUR-VALUED. Apply either
writes or does not. PositionedProjectionWriter.cs:265-286 dispatches on four
outcomes, and the mapping is uneven in a way a reader must be told:
  - Applied     -> modeled, the writing branch of Apply
  - Superseded  -> modeled, the settled-without-writing branch; production
                   settles it only when CurrentPosition >= attemptedPosition,
                   which is exactly the model's rowPos[row] >= target guard
  - Vanished    -> NOT MODELED. Production treats a row that disappeared under
                   it as settled; the model has no deletion, so the state is
                   unreachable here rather than handled.
  - Unplaceable -> NOT EXPRESSIBLE. It is the state of a row that exists while
                   carrying NO position, it is TERMINAL ("THIS WILL NOT RESOLVE
                   ON RETRY", :192-209, where it THROWS before IsSettled is
                   consulted), and rowPos \in [Rows -> 0..MaxEvent] has no null
                   to represent it. It is excluded rather than overlooked: it
                   arises from unconditional or unnumbered writes, which the
                   positioned-writes requirement below already puts outside this
                   guarantee. A reader could not have deduced that from the
                   abstraction note alone, so it is said here.

VISIBILITY IS THE PROPERTY THIS MODEL EXISTS FOR, so it is modeled rather
than abstracted. A row CAS makes a write ACCEPTED; it does not make the write
READABLE. `rowValue` is what the write path accepted; `visible` is what a
reader can observe. The checkpoint is an acknowledgement to the outside world,
so acknowledging a position whose rows are not yet readable is the silent
loss: the page is never replayed and the reader never sees those events.
`CheckpointCoverage` (over `rowValue`) is therefore NOT the property that
matters; `CheckpointVisibility` (over `visible`) is. Both are checked, because
the weaker one still catches a different defect.

THE BARRIER IS AWAITED EVEN WHEN THIS ACTOR WROTE NOTHING. A page that
skipped every write as duplicate or superseded observed a COMPETING writer's
state, which may have been accepted but not yet refreshed. Skipping the
barrier in that case is a reachable silent loss and is mutant
SkipBarrierWhenNotWriting, not a theoretical concern.

ABSTRACTED: serialization, network failures (crash/retry stands for failed
calls), clocks, unbounded streams, batches >1, dynamic routing, erasure/rebuild,
and external handler effects. Each callback and storage operation terminates
as one atomic model step. Row read and row write remain separate steps. A row
value is the SET of applied event ordinals; a valid fold contains its prefix.
Real consumer folds must be deterministic and immutable between read and CAS.
Every participating registration MUST use positioned writes; unconditional
fallbacks and non-transactional external effects are outside this guarantee.

ABSTRACTED ABOUT VISIBILITY, stated separately because these are the limits
of the property above: the barrier is ONE atomic step that publishes every
required row, so per-row and per-index refresh granularity, partial refresh,
and refresh cost are not modeled. A reader's own staleness AFTER a successful
barrier (replica lag behind the refreshed primary) is not modeled. The model
therefore establishes the ORDERING obligation -- no acknowledgement before
visibility -- and NOT any bound on how stale a reader may be once the barrier
has returned.

LIVENESS ASSUMPTIONS: finite committed stream; eventually no crashes,
cancellations or barrier faults (explicit finite budgets here); terminating
storage/callbacks; weak fairness of each actor's ordinary steps; FIFO
admission within process. No claim about fairness of SemaphoreSlim follows
from this FIFO abstraction. Manifest compatibility includes source/scope/
filter, destination, work set and consumer-managed semantic revision,
abstracted as one immutable descriptor. The first atomic binder establishes
that descriptor; mismatch is rejected.

MUTANTS: each changes exactly one enforcement mechanism. MixedManifest supplies
a legitimate mismatching Jobs descriptor, independently of bypassing binding.
SkipBarrier drops the barrier entirely. SkipBarrierWhenNotWriting drops it only
on a page that wrote nothing. BarrierFaultAcknowledges lets a FAILED barrier
proceed to the checkpoint, which the contract forbids.
***************************************************************************)

CONSTANTS MixedManifest, SkipManifestCheck, BlindStaleWrite,
          PrematureCheckpoint, CancelWaiterReleases,
          SkipBarrier, SkipBarrierWhenNotWriting, BarrierFaultAcknowledges

Actors == {"API", "Background", "Jobs"}
Processes == {"Web", "Worker"}
Rows == {1, 2}
MaxEvent == 2
Process(a) == IF a = "Jobs" THEN "Worker" ELSE "Web"
Descriptor(a) == IF MixedManifest /\ a = "Jobs" THEN 1 ELSE 0
Required(d) == IF d = 1 THEN {1} ELSE Rows
RowCount(d) == Cardinality(Required(d))
Prefix(n) == {e \in 1..MaxEvent : e <= n}
Max(x, y) == IF x >= y THEN x ELSE y
Remove(s, a) == SelectSeq(s, LAMBDA x : x # a)

VARIABLES manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh, visible,
          phase, queue, holders, permits, base, target, row,
          readPos, readValue, wrote, crashed, cancelled, barrierFaulted

vars == <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh, visible,
          phase, queue, holders, permits, base, target, row,
          readPos, readValue, wrote, crashed, cancelled, barrierFaulted>>

Init ==
    /\ manifest = 2
    /\ admitted = {}
    /\ cp = 0
    /\ cpHigh = 0
    /\ rowPos = [r \in Rows |-> 0]
    /\ rowValue = [r \in Rows |-> {}]
    /\ rowHigh = [r \in Rows |-> 0]
    /\ visible = [r \in Rows |-> {}]
    /\ phase = [a \in Actors |-> "Bind"]
    /\ queue = [p \in Processes |-> <<>>]
    /\ holders = [p \in Processes |-> {}]
    /\ permits = [p \in Processes |-> 1]
    /\ base = [a \in Actors |-> 0]
    /\ target = [a \in Actors |-> 0]
    /\ row = [a \in Actors |-> 1]
    /\ readPos = [a \in Actors |-> 0]
    /\ readValue = [a \in Actors |-> {}]
    /\ wrote = [a \in Actors |-> FALSE]
    /\ crashed = {}
    /\ cancelled = {}
    /\ barrierFaulted = {}

Bind(a) ==
    /\ phase[a] = "Bind"
    /\ LET accepted == manifest = 2 \/ manifest = Descriptor(a) \/ SkipManifestCheck
       IN /\ manifest' = IF manifest = 2 THEN Descriptor(a) ELSE manifest
          /\ admitted' = IF accepted THEN admitted \cup {a} ELSE admitted
          /\ phase' = [phase EXCEPT ![a] = IF accepted THEN "Wait" ELSE "Rejected"]
          /\ queue' = IF accepted
                        THEN [queue EXCEPT ![Process(a)] = Append(@, a)]
                        ELSE queue
    /\ UNCHANGED <<cp, cpHigh, rowPos, rowValue, rowHigh, visible, holders, permits,
                    base, target, row, readPos, readValue, wrote, crashed,
                    cancelled, barrierFaulted>>

Acquire(a) ==
    /\ phase[a] = "Wait"
    /\ queue[Process(a)] # <<>>
    /\ Head(queue[Process(a)]) = a
    /\ permits[Process(a)] > 0
    /\ holders' = [holders EXCEPT ![Process(a)] = @ \cup {a}]
    /\ permits' = [permits EXCEPT ![Process(a)] = @ - 1]
    /\ phase' = [phase EXCEPT ![a] = "Page"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh, visible,
                    queue, base, target, row, readPos, readValue, wrote, crashed,
                    cancelled, barrierFaulted>>

Page(a) ==
    /\ phase[a] = "Page"
    /\ base' = [base EXCEPT ![a] = cp]
    /\ target' = [target EXCEPT ![a] = cp + 1]
    /\ row' = [row EXCEPT ![a] = 1]
    /\ wrote' = [wrote EXCEPT ![a] = FALSE]
    /\ phase' = [phase EXCEPT ![a] = IF cp = MaxEvent THEN "Finish" ELSE "Load"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh, visible,
                    queue, holders, permits, readPos, readValue, crashed,
                    cancelled, barrierFaulted>>

Load(a) ==
    /\ phase[a] = "Load"
    /\ readPos' = [readPos EXCEPT ![a] = rowPos[row[a]]]
    /\ readValue' = [readValue EXCEPT ![a] = rowValue[row[a]]]
    /\ phase' = [phase EXCEPT ![a] = "Apply"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh, visible,
                    queue, holders, permits, base, target, row, wrote, crashed,
                    cancelled, barrierFaulted>>

Apply(a) ==
    /\ phase[a] = "Apply"
    /\ LET writes == readPos[a] < target[a]
                       /\ (BlindStaleWrite \/ rowPos[row[a]] = readPos[a])
           settled == writes \/ rowPos[row[a]] >= target[a]
           wroteAny == wrote[a] \/ writes
           lastRow == row[a] = RowCount(Descriptor(a)) \/ PrematureCheckpoint
           skipsBarrier == SkipBarrier \/ (SkipBarrierWhenNotWriting /\ ~wroteAny)
       IN /\ rowPos' = IF writes
                         THEN [rowPos EXCEPT ![row[a]] = target[a]] ELSE rowPos
          /\ rowValue' = IF writes
                           THEN [rowValue EXCEPT ![row[a]] = readValue[a] \cup {target[a]}]
                           ELSE rowValue
          /\ rowHigh' = IF writes
                          THEN [rowHigh EXCEPT ![row[a]] = Max(@, target[a])]
                          ELSE rowHigh
          /\ wrote' = [wrote EXCEPT ![a] = wroteAny]
          /\ phase' = [phase EXCEPT ![a] =
                IF ~settled THEN "Release"
                ELSE IF lastRow
                     THEN (IF skipsBarrier THEN "Commit" ELSE "Barrier")
                     ELSE "Load"]
          /\ row' = [row EXCEPT ![a] =
                IF settled /\ row[a] < RowCount(Descriptor(a)) /\ ~PrematureCheckpoint
                   THEN @ + 1 ELSE @]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, visible, queue, holders, permits,
                    base, target, readPos, readValue, crashed, cancelled,
                    barrierFaulted>>

\* The barrier publishes what the write path has ACCEPTED for every required row,
\* including writes this actor did not make. That is what a real index refresh
\* does, and it is why a page that wrote nothing must still await it.
Barrier(a) ==
    /\ phase[a] = "Barrier"
    /\ visible' = [r \in Rows |->
            IF r \in Required(Descriptor(a)) THEN rowValue[r] ELSE visible[r]]
    /\ phase' = [phase EXCEPT ![a] = "Commit"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh,
                    queue, holders, permits, base, target, row, readPos,
                    readValue, wrote, crashed, cancelled, barrierFaulted>>

\* A failed or cancelled barrier MUST NOT acknowledge the page. The page is
\* retried from the unchanged checkpoint, which is safe because the row CASes
\* are idempotent against their positions.
BarrierFault(a) ==
    /\ phase[a] = "Barrier"
    /\ a \notin barrierFaulted
    /\ barrierFaulted' = barrierFaulted \cup {a}
    /\ phase' = [phase EXCEPT ![a] =
            IF BarrierFaultAcknowledges THEN "Commit" ELSE "Release"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh,
                    visible, queue, holders, permits, base, target, row,
                    readPos, readValue, wrote, crashed, cancelled>>

Commit(a) ==
    /\ phase[a] = "Commit"
    /\ cp' = IF cp = base[a] THEN target[a] ELSE cp
    /\ cpHigh' = Max(cpHigh, cp')
    /\ phase' = [phase EXCEPT ![a] = "Release"]
    /\ UNCHANGED <<manifest, admitted, rowPos, rowValue, rowHigh, visible, queue,
                    holders, permits, base, target, row, readPos, readValue,
                    wrote, crashed, cancelled, barrierFaulted>>

Release(a) ==
    /\ phase[a] \in {"Release", "Finish"}
    /\ holders' = [holders EXCEPT ![Process(a)] = @ \ {a}]
    /\ permits' = [permits EXCEPT ![Process(a)] = @ + 1]
    /\ queue' = [queue EXCEPT ![Process(a)] =
                    IF phase[a] = "Finish" THEN Remove(@, a)
                    ELSE Append(Remove(@, a), a)]
    /\ phase' = [phase EXCEPT ![a] = IF @ = "Finish" THEN "Done" ELSE "Wait"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh,
                    visible, base, target, row, readPos, readValue, wrote,
                    crashed, cancelled, barrierFaulted>>

Crash(a) ==
    /\ a \notin crashed
    /\ a \in holders[Process(a)]
    /\ crashed' = crashed \cup {a}
    /\ holders' = [holders EXCEPT ![Process(a)] = @ \ {a}]
    /\ permits' = [permits EXCEPT ![Process(a)] = @ + 1]
    /\ queue' = [queue EXCEPT ![Process(a)] = Append(Remove(@, a), a)]
    /\ phase' = [phase EXCEPT ![a] = "Wait"]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh,
                    visible, base, target, row, readPos, readValue, wrote,
                    cancelled, barrierFaulted>>

CancelWaiter(a) ==
    /\ phase[a] = "Wait"
    /\ a \notin cancelled
    /\ cancelled' = cancelled \cup {a}
    /\ permits' = IF CancelWaiterReleases
                    THEN [permits EXCEPT ![Process(a)] = @ + 1] ELSE permits
    /\ queue' = [queue EXCEPT ![Process(a)] = Append(Remove(@, a), a)]
    /\ UNCHANGED <<manifest, admitted, cp, cpHigh, rowPos, rowValue, rowHigh,
                    visible, phase, holders, base, target, row, readPos,
                    readValue, wrote, crashed, barrierFaulted>>

ActorStep(a) == Bind(a) \/ Acquire(a) \/ Page(a) \/ Load(a) \/ Apply(a)
                \/ Barrier(a) \/ Commit(a) \/ Release(a)
Next == \E a \in Actors :
            ActorStep(a) \/ Crash(a) \/ CancelWaiter(a) \/ BarrierFault(a)
Spec == Init /\ [][Next]_vars /\ (\A a \in Actors : WF_vars(ActorStep(a)))

TypeOK ==
    /\ manifest \in 0..2
    /\ cp \in 0..MaxEvent
    /\ cpHigh \in 0..MaxEvent
    /\ rowPos \in [Rows -> 0..MaxEvent]
    /\ rowValue \in [Rows -> SUBSET (1..MaxEvent)]
    /\ rowHigh \in [Rows -> 0..MaxEvent]
    /\ visible \in [Rows -> SUBSET (1..MaxEvent)]
    /\ admitted \subseteq Actors
    /\ crashed \subseteq Actors
    /\ cancelled \subseteq Actors
    /\ barrierFaulted \subseteq Actors
    /\ wrote \in [Actors -> BOOLEAN]
    /\ holders \in [Processes -> SUBSET Actors]
    /\ permits \in [Processes -> Nat]
GateOwnership == \A p \in Processes :
    /\ Cardinality(holders[p]) <= 1
    /\ permits[p] <= 1
    /\ permits[p] + Cardinality(holders[p]) = 1
ManifestAgreement == \A a \in admitted : Descriptor(a) = manifest
RowNeverRewinds == \A r \in Rows : rowPos[r] = rowHigh[r]
CheckpointNeverRewinds == cp = cpHigh
RowIsPrefix == \A r \in Rows : rowValue[r] = Prefix(rowPos[r])
CheckpointCoverage == manifest = 2 \/
    (\A r \in Required(manifest) : Prefix(cp) \subseteq rowValue[r])
\* A reader can never observe more than the write path accepted.
VisibleNeverExceedsWritten == \A r \in Rows : visible[r] \subseteq rowValue[r]
\* THE property: nothing is acknowledged that a reader cannot yet see.
\*
\* WHY IT HOLDS IN THE HEALTHY ARM. A reader will want this and cannot easily
\* reconstruct it, and it is where Barrier's publish-every-required-row choice
\* becomes load-bearing rather than merely convenient.
\*
\*   1. An actor reaches Barrier only from the settled /\ lastRow branch of
\*      Apply. A row that does not settle sends the page to Release instead, and
\*      row advances only on settling, so reaching lastRow means EVERY required
\*      row settled during this page -- i.e. rowPos[r] >= target[a] for all of
\*      them.
\*   2. rowPos never decreases (Apply only raises it; RowNeverRewinds), so that
\*      holds still at the Barrier step.
\*   3. RowIsPrefix gives rowValue[r] = Prefix(rowPos[r]), and rowPos[r] >=
\*      target[a], so rowValue[r] \supseteq Prefix(target[a]).
\*   4. Barrier assigns visible[r] = rowValue[r] for every required r, hence
\*      visible[r] \supseteq Prefix(target[a]).
\*   5. Commit advances cp to target[a] only when cp = base[a], so
\*      Prefix(cp') = Prefix(target[a]) \subseteq visible[r]. No action ever
\*      shrinks visible, so the property is preserved afterwards.
\*
\* STEP 4 IS WHY THE BARRIER PUBLISHES ROWS THIS ACTOR DID NOT WRITE. Were it to
\* publish only its own writes, an actor whose every write was superseded would
\* publish NOTHING and its Commit would violate this invariant -- which is
\* exactly the SkipBarrierWhenNotWriting mutant, arrived at by a different
\* route. Narrowing the barrier to the actor's own writes would therefore make
\* the healthy arm fail, not make the model stricter.
CheckpointVisibility == manifest = 2 \/
    (\A r \in Required(manifest) : Prefix(cp) \subseteq visible[r])
Completion == <> (\A a \in Actors : phase[a] \in {"Done", "Rejected"})

=============================================================================
