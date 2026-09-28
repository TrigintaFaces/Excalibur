--------------------- MODULE EventStoreGlobalPosition ---------------------
(***************************************************************************)
(* SCOPE -- read this before citing any result from this model.            *)
(*                                                                          *)
(* STATUS: the shipped configuration is CLEAN, and that result is NOT yet   *)
(* sufficient to claim R4 for the event-sourcing seam. See LIMITS below.    *)
(* R4 remains UNVERIFIED in eng/governance/rigor-ladder.yaml.               *)
(*                                                                          *)
(* WHAT IS MODELLED                                                         *)
(*   Concurrent appends to the event store's GLOBAL POSITION sequence, and  *)
(*   only that. Each writer allocates a position and either commits or      *)
(*   aborts.                                                                *)
(*                                                                          *)
(* TWO INDEPENDENT MECHANISMS, TWO CONSTANTS. An earlier version of this    *)
(* module folded both into one flag, which meant no arm ever isolated       *)
(* either, and the counterexample it produced was evidence about a          *)
(* different mechanism than the one the design document argues for. They    *)
(* are separate here because they fail differently:                         *)
(*                                                                          *)
(*   SerialisedToCommit  -- TRUE when the allocating statement holds its    *)
(*                          row lock until the transaction commits, so at   *)
(*                          most one writer holds an allocation at a time.  *)
(*                                                                          *)
(*   CounterRollsBack    -- TRUE when the counter's new value becomes       *)
(*                          visible only at COMMIT, so an abort returns the *)
(*                          position to the pool. FALSE models IDENTITY /   *)
(*                          a sequence: the value is consumed when handed   *)
(*                          out and an abort burns it.                      *)
(*                                                                          *)
(*   The shipped design is (TRUE, TRUE). The other three arms are run as    *)
(*   non-vacuity evidence, and each fails for its OWN reason -- which is    *)
(*   the point of separating them:                                          *)
(*                                                                          *)
(*     (TRUE,  FALSE)  InvariantJ violated -- a HOLE, from a burned value.  *)
(*                     This is the counterexample the schema's written      *)
(*                     argument is actually about.                          *)
(*     (FALSE, TRUE)   InvariantJ HOLDS, InvUnique violated -- two writers  *)
(*                     allocate the SAME position. Not a hole: a DUPLICATE. *)
(*                     J alone would have reported this design clean.       *)
(*     (FALSE, FALSE)  InvariantJ violated -- a hole from interleaving      *)
(*                     commits, with no failure involved at all.            *)
(*                                                                          *)
(* WHICH SEAM THIS CLAIMS TO CORRESPOND TO                                  *)
(*   src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/              *)
(*     AllocateAndInsertEventsRequest.cs -- the UPDATE ... WITH (ROWLOCK)   *)
(*   on the counter row, inside the caller's transaction under              *)
(*   SET XACT_ABORT ON. `SerialisedToCommit = TRUE` is the claim that this  *)
(*   row lock is held to commit; `CounterRollsBack = TRUE` is the claim     *)
(*   that the increment is inside that same transaction.                    *)
(*                                                                          *)
(* LIMITS -- a pass here is NOT evidence about any of the following, and    *)
(* the first two are why this is not yet an R4 artifact:                    *)
(*                                                                          *)
(*   1. CONCURRENCY IS BARELY EXERCISED IN THE SHIPPED ARM. With            *)
(*      SerialisedToCommit = TRUE the reachable state space is exactly      *)
(*      (MaxAppends + 1) + MaxAppends * |Writers|, and the diameter does    *)
(*      not change when the writer count does -- the extra states are name  *)
(*      permutations. The invariants ENUMERATE the reachable states rather  *)
(*      than carving out a subset of them. What this arm establishes is the *)
(*      implication "given mutual exclusion, advancing the counter only at  *)
(*      commit yields a prefix". That implication is real and it is what    *)
(*      the design rests on -- but the premise does the work, and the       *)
(*      premise is the thing the implementation could get wrong.            *)
(*                                                                          *)
(*   2. SERIALISATION IS ASSUMED, NOT DERIVED. The model GRANTS the counter *)
(*      row mutual exclusion. It does not derive it from SQL Server's       *)
(*      locking, so it cannot detect a lock hint being dropped, an          *)
(*      isolation level changed, or the UPDATE being moved out of the       *)
(*      transaction. Those are exactly the edits that would break the       *)
(*      premise, and this model is blind to all of them.                    *)
(*                                                                          *)
(*   3. The per-stream version check and its concurrency conflict. Modelled *)
(*      writers contend only on the global counter, never on a stream.      *)
(*                                                                          *)
(*   4. Outbox staging and its ordering relative to allocation.             *)
(*                                                                          *)
(*   5. THE LOST-ACKNOWLEDGEMENT CASE -- a COMMIT that succeeds at the      *)
(*      engine while the client observes failure. Modelled aborts are true  *)
(*      rollbacks. This is a REAL defect in the shipped code and the model  *)
(*      is deliberately blind to it. Do not read a pass as covering it.     *)
(*                                                                          *)
(*   6. Crash, recovery, log flushing, durability. Commit is atomic and     *)
(*      instantaneous here.                                                 *)
(*                                                                          *)
(*   7. Subscribers, checkpoints and projections. J is the property they    *)
(*      depend on; none of them is modelled.                                *)
(*                                                                          *)
(* WHY J MATTERS: a hole in the committed sequence that fills in AFTER a    *)
(* subscriber has read past it is silent, permanent event loss. Nothing     *)
(* downstream ever learns of it. That silence is the discriminator for a    *)
(* catastrophic blast radius, and it is why this seam owes a model at all.  *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Writers,             \* the set of concurrent appending writers
    MaxAppends,          \* bound on positions, to keep the search finite
    SerialisedToCommit,  \* TRUE = the counter row's lock is held to commit
    CounterRollsBack     \* TRUE = the increment is inside the transaction

VARIABLES
    counter,    \* the allocator's currently visible value
    committed,  \* the set of global positions that have COMMITTED
    pc,         \* writer -> "idle" | "holding"
    held        \* writer -> the position it has allocated but not committed

vars == <<counter, committed, pc, held>>

TypeOK ==
    /\ counter \in Nat
    /\ committed \subseteq (1..MaxAppends)
    /\ pc \in [Writers -> {"idle", "holding"}]
    /\ held \in [Writers -> Nat]

Init ==
    /\ counter = 0
    /\ committed = {}
    /\ pc = [w \in Writers |-> "idle"]
    /\ held = [w \in Writers |-> 0]

SomeoneHolding == \E w \in Writers : pc[w] = "holding"

Allocate(w) ==
    /\ pc[w] = "idle"
    /\ counter < MaxAppends
    /\ SerialisedToCommit => ~SomeoneHolding
    /\ pc'     = [pc   EXCEPT ![w] = "holding"]
    /\ held'   = [held EXCEPT ![w] = counter + 1]
    /\ counter' = IF CounterRollsBack THEN counter ELSE counter + 1
    /\ UNCHANGED committed

Commit(w) ==
    /\ pc[w] = "holding"
    /\ committed' = committed \cup {held[w]}
    /\ counter'   = IF CounterRollsBack THEN held[w] ELSE counter
    /\ pc'        = [pc   EXCEPT ![w] = "idle"]
    /\ held'      = [held EXCEPT ![w] = 0]

Abort(w) ==
    /\ pc[w] = "holding"
    /\ pc'   = [pc   EXCEPT ![w] = "idle"]
    /\ held' = [held EXCEPT ![w] = 0]
    /\ UNCHANGED <<counter, committed>>

Next == \E w \in Writers : Allocate(w) \/ Commit(w) \/ Abort(w)

Spec == Init /\ [][Next]_vars /\ WF_vars(Next)

(***************************************************************************)
(* INVARIANT J -- the committed positions form a contiguous prefix 1..k.    *)
(***************************************************************************)
InvariantJ ==
    \A p \in 1..MaxAppends :
        (\E q \in committed : q >= p) => (p \in committed)

(***************************************************************************)
(* InvUnique -- no two writers hold the same position at the same time.     *)
(*                                                                          *)
(* THIS EXISTS BECAUSE J DOES NOT IMPLY IT, and the gap is not academic.    *)
(* Under (SerialisedToCommit = FALSE, CounterRollsBack = TRUE) two writers  *)
(* both read the same counter and both allocate counter+1. They commit the  *)
(* SAME position. `committed` is a set, so the duplicate collapses silently *)
(* and J reports the design CLEAN while two distinct events share a global  *)
(* position. A subscriber then sees one of them, forever.                   *)
(*                                                                          *)
(* An invariant that a wrong design satisfies is the reason to add a second *)
(* one, not a reason to trust the first.                                    *)
(***************************************************************************)
InvUnique ==
    \A v, w \in Writers :
        (v # w /\ pc[v] = "holding" /\ pc[w] = "holding") => held[v] # held[w]

(***************************************************************************)
(* InvA -- the auxiliary invariant that explains WHY J holds for the        *)
(* shipped design, and the one an implementer can actually break by moving  *)
(* the allocation outside the transaction.                                  *)
(***************************************************************************)
InvA ==
    (SerialisedToCommit /\ CounterRollsBack) =>
        /\ Cardinality({w \in Writers : pc[w] = "holding"}) =< 1
        /\ \A w \in Writers : pc[w] = "holding" => held[w] = counter + 1
===============================================================================
