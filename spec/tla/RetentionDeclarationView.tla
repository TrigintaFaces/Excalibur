------------------- MODULE RetentionDeclarationView -------------------
(***************************************************************************)
(* SCOPE: read this before citing any result from this model.              *)
(*                                                                         *)
(* STATUS: the shipped configuration HOLDS. That is NOT sufficient to      *)
(* claim R4 for the compliance-erasure seam. See LIMITS. R4 remains        *)
(* UNVERIFIED in eng/governance/rigor-ladder.yaml.                         *)
(*                                                                         *)
(* WHAT IS MODELLED                                                        *)
(*   The decision each instance makes about whether it may irreversibly    *)
(*   destroy an aggregate type's key material, given that retention        *)
(*   declarations live in each instance's own CODE and a deployment-wide   *)
(*   record is the only thing any instance can read about the others.      *)
(*                                                                         *)
(*   Corresponds to:                                                       *)
(*     ErasureRetentionStalenessGate.DescribeRefusalAsync                  *)
(*       src/Excalibur/Excalibur.Compliance/Erasure/                       *)
(*         ErasureRetentionStalenessGate.cs                                *)
(*     ErasureRetentionAdvanceLatch.EnsureAdvancedAsync                    *)
(*       src/Excalibur/Excalibur.Compliance/Erasure/                       *)
(*         ErasureRetentionAdvanceLatch.cs                                 *)
(*                                                                         *)
(* TWO INDEPENDENT MECHANISMS, TWO CONSTANTS, because they fail            *)
(* differently and an arm that moved both would isolate neither:           *)
(*                                                                         *)
(*   UseStrictSubset  FALSE is shipped: refuse iff the record holds a live *)
(*                    declaration this instance does not hold             *)
(*                    (CONTAINMENT). TRUE models the SUPERSEDED predicate: *)
(*                    refuse iff this instance's set is a STRICT SUBSET   *)
(*                    of the record.                                      *)
(*                                                                         *)
(*   LatchOnPrefix    FALSE is shipped: an instance counts as advanced     *)
(*                    only when EVERY one of its declarations reached the  *)
(*                    record. TRUE models closing the latch on a PREFIX.   *)
(*                                                                         *)
(* WHAT IS ABSTRACTED AWAY -- none of this is checked here                 *)
(*   * WITHDRAWALS and tombstones. The predicate reads the record's LIVE   *)
(*     set; this model has no withdrawal action, so nothing here says      *)
(*     anything about tombstone handling. That is unit-armed instead.      *)
(*   * TENANCY. One implicit tenant. The real predicate is per tenant and  *)
(*     the record's key leads with it.                                     *)
(*   * The erasure lifecycle: claims, legal holds, certificates, retries,  *)
(*     the scheduler, and the distinction between the pre-claim and the    *)
(*     pre-destruction comparison. Both comparisons read the same record   *)
(*     and the record only grows, so the earlier one can only refuse LESS; *)
(*     this model checks ONE comparison, the later and only safety-bearing *)
(*     one. The earlier one is a liveness device and is out of scope.      *)
(*   * Process failure, message loss, clocks, and the database.            *)
(*   * The GRANULARITY of the destruction. One Destroy step destroys one   *)
(*     type's material atomically with its own gate check. The split       *)
(*     check-then-act across N keys is a DIFFERENT property, already       *)
(*     modelled and already REFUTED in SubjectKeyObligationPartition.tla   *)
(*     (arms _P_LoopPerKey / _P_LoopNoFence). Do not read a green here as  *)
(*     covering it.                                                       *)
(*                                                                         *)
(* LIMITS -- why a clean run is not R4                                     *)
(*   The declaration sets are fixed at initialisation, so this models ONE  *)
(*   deploy generation reaching a fleet, not a SEQUENCE of deploys. A      *)
(*   rollback, or a second deploy arriving while the first is still        *)
(*   rolling, is OUTSIDE the modelled state space. The counterexample that *)
(*   refuted the superseded predicate IS reachable here, because it needs  *)
(*   only a partial advance and not a second deploy -- but a reader must   *)
(*   not read "holds" as covering deploy sequences.                        *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Types,              \* the aggregate types a declaration can name
    Instances,          \* the live processes
    UseStrictSubset,    \* FALSE = containment (shipped); TRUE = the superseded predicate
    LatchOnPrefix       \* FALSE = latch on complete success (shipped); TRUE = latch on a prefix

VARIABLES
    decl,       \* [Instances -> SUBSET Types] each instance's declarations, from its code
    recorded,   \* SUBSET Types -- the deployment-wide record. GROWS ONLY.
    advanced,   \* SUBSET Instances -- instances counting as having recorded their declarations
    destroyed,  \* SUBSET Types -- irreversibly destroyed. GROWS ONLY.
    breach      \* HISTORY VARIABLE. See InvNoRetainedDestroyed -- it IS the invariant.

vars == <<decl, recorded, advanced, destroyed, breach>>

TypeOK ==
    /\ decl \in [Instances -> SUBSET Types]
    /\ recorded \subseteq Types
    /\ advanced \subseteq Instances
    /\ destroyed \subseteq Types
    /\ breach \subseteq Types

(***************************************************************************)
(* Every instance's declarations are fixed for its lifetime: the in-box    *)
(* registry builds an immutable set in its constructor from options bound  *)
(* at startup. The record starts empty; nothing has advanced or gone.      *)
(***************************************************************************)
Init ==
    /\ decl \in [Instances -> SUBSET Types]
    /\ recorded = {}
    /\ advanced = {}
    /\ destroyed = {}
    /\ breach = {}

(***************************************************************************)
(* THE PREDICATE. Shipped: this instance may destroy only while it holds   *)
(* every declaration the record holds.                                     *)
(*                                                                         *)
(* The superseded form adds a second conjunct that is not a safety         *)
(* condition -- that this instance knows nothing the record does not --    *)
(* and that conjunct OPENS the gate exactly when an instance is behind in  *)
(* one type and ahead in another.                                          *)
(***************************************************************************)
MayDestroy(i) ==
    IF UseStrictSubset
      THEN ~ (decl[i] \subseteq recorded /\ decl[i] # recorded)
      ELSE recorded \subseteq decl[i]

(***************************************************************************)
(* An instance records its declarations. ANY SUBSET may reach the record,  *)
(* which is what a per-pair advance leaves behind when one pair cannot be  *)
(* recorded -- reachable by construction, since neither provider has a     *)
(* single-statement insert-if-absent over N pairs.                         *)
(*                                                                         *)
(* Shipped: it counts as advanced only when the WHOLE set got there, so a  *)
(* prefix leaves the latch open and the next caller retries.               *)
(***************************************************************************)
Advance(i) ==
    /\ i \notin advanced
    /\ \E written \in SUBSET decl[i] :
         /\ recorded' = recorded \union written
         /\ advanced' = IF LatchOnPrefix \/ written = decl[i]
                          THEN advanced \union {i}
                          ELSE advanced
    /\ UNCHANGED <<decl, destroyed, breach>>

(***************************************************************************)
(* An instance destroys the material of a type it does not itself declare  *)
(* retained, if the gate lets it. An instance always spares its own        *)
(* declarations, so the hazard is only ever about OTHER instances.         *)
(*                                                                         *)
(* The same step records whether the hazard was live AT THAT INSTANT: some *)
(* other instance declared this type AND had already recorded its          *)
(* declarations, so the record was carrying the very information that      *)
(* should have stopped this destruction.                                   *)
(***************************************************************************)
Destroy(i) ==
    /\ \E t \in Types :
         /\ t \notin decl[i]
         /\ t \notin destroyed
         /\ MayDestroy(i)
         /\ destroyed' = destroyed \union {t}
         /\ breach' = IF \E j \in advanced : t \in decl[j]
                        THEN breach \union {t}
                        ELSE breach
    /\ UNCHANGED <<decl, recorded, advanced>>

Next ==
    \/ \E i \in Instances : Advance(i)
    \/ \E i \in Instances : Destroy(i)

Spec == Init /\ [][Next]_vars

(***************************************************************************)
(* SAFETY. No type's material is destroyed while the record already        *)
(* carried a live declaration of it from an instance that had advanced.    *)
(*                                                                         *)
(* WHY IT IS A HISTORY VARIABLE AND NOT A STATE PREDICATE. The obvious     *)
(* phrasing -- "nothing destroyed is declared by an advanced instance" --  *)
(* is VIOLATED BY THE SHIPPED DESIGN, and correctly so: an instance may    *)
(* advance AFTER a destruction that was legitimate when it happened. That  *)
(* is the published window, and it is a property of the problem rather     *)
(* than of this design. Before any instance has recorded anything, "the    *)
(* pair was declared and nothing recorded it" and "the pair was never      *)
(* declared" differ in no variable any process can observe, so no          *)
(* predicate over the record can separate them.                           *)
(*                                                                         *)
(* So the invariant is evaluated where the decision is made. What the      *)
(* mechanism buys is everything after the first advance, and this states   *)
(* that property exactly -- no stronger, which would make the shipped arm  *)
(* red for an honest reason, and no weaker.                               *)
(***************************************************************************)
(***************************************************************************)
(* IS THE SHIPPED ARM VACUOUS? NO -- and this suite asks, because it has    *)
(* retired an arm for the opposite shape.                                   *)
(*                                                                          *)
(* `breach` is never written under the shipped configuration. That is the    *)
(* theorem, not an artefact of a disabled action: if an instance j has       *)
(* advanced then decl[j] is contained in `recorded`, `recorded` only grows,  *)
(* and a destroyer i passed `recorded \subseteq decl[i]`, so t would have to *)
(* be in decl[i] -- which Destroy forbids.                                  *)
(*                                                                          *)
(* The distinction from a vacuous arm is WHICH action writes the variable.   *)
(* SubjectKeyObligationPartition retired a green arm whose recording         *)
(* variable was written only by an action a constant had switched OFF, so no *)
(* design could make it fail. Here the writer is Destroy, which is enabled   *)
(* in every arm; whether its branch fires depends on the PREDICATE and the   *)
(* LATCH, which are the things under test, and two arms do fire it. The      *)
(* non-vacuity evidence is those two RED arms, never this comment.          *)
(*                                                                          *)
(* ADEQUACY. The diameter MOVES with the instance count -- 5 with one        *)
(* instance, 7 with two -- so interleaving between instances is exercised    *)
(* rather than granted. Contrast EventStoreGlobalPosition, whose shipped arm *)
(* GRANTS serialisation and whose diameter does not move with the writer     *)
(* count: there the premise does the work. Nothing is granted here.          *)
(***************************************************************************)

InvNoRetainedDestroyed == breach = {}

(***************************************************************************)
(* NON-VACUITY, checked as an invariant whose REQUIRED ANSWER IS A         *)
(* VIOLATION. A gate that refused every destruction would satisfy the      *)
(* safety invariant above and erase nothing -- which is its own            *)
(* regulator's finding, since an erasure never performed is a subject      *)
(* never erased. This asserts destruction never happens, so the arm MUST   *)
(* report it violated.                                                     *)
(***************************************************************************)
InvNothingIsEverDestroyed == destroyed = {}

=============================================================================
