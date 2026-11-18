---------------------------- MODULE TokenFingerprintLedger ----------------------------
(***************************************************************************)
(* SCOPE: read this before citing anything this model establishes.         *)
(*                                                                         *)
(* WHAT IS MODELLED. Several processes start against one token-keyed        *)
(* compliance store. Each process has its own data-subject token function.  *)
(* Every durable record in that store -- erasure requests, the             *)
(* key-destruction ledger, the key-mint ledger an erasure enumerates from,  *)
(* legal holds, the data inventory -- is keyed by a pseudonymisation token  *)
(* that function produces. So two processes whose token functions differ    *)
(* must not both write token-keyed records into one store: the second       *)
(* orphans every key the first wrote, and nothing throws or logs.          *)
(*                                                                         *)
(* Each process claims a FINGERPRINT of its token function in a durable     *)
(* ledger, reads back what the ledger now holds, and proceeds only if the   *)
(* read-back equals its own. That is the shipped mechanism.                *)
(*                                                                         *)
(* CORRESPONDENCE, and it is a REGRESSION LOCK rather than a description    *)
(* of present code. The defect this models was fixed at 7ce9400aca. Both    *)
(* switches below are OFF in the shipped configuration, and each is ON in   *)
(* exactly one arm, so an arm going green would mean the fix regressed --   *)
(* never that the defect is live today.                                     *)
(*                                                                         *)
(*   UpdateAllowed              -> the claim OVERWRITES instead of          *)
(*                                 inserting-if-absent. Fixed by a database *)
(*                                 trigger both provisioning paths install; *)
(*                                 the contract additionally forbids any    *)
(*                                 update or delete path by any route,      *)
(*                                 including an administrative one.         *)
(*   FingerprintIsConfiguration -> the fingerprint is derived from the      *)
(*                                 CONFIGURED key rather than from the      *)
(*                                 REGISTERED hasher's OUTPUT. This was my  *)
(*                                 own first ruling and adversarial review  *)
(*                                 reversed it: IDataSubjectHasher is       *)
(*                                 public, its docs invite consumer         *)
(*                                 implementations, and it is registered    *)
(*                                 first-one-wins -- so a consumer's hasher *)
(*                                 can be the one producing every token     *)
(*                                 while the configured key is read by      *)
(*                                 nobody.                                  *)
(*                                                                         *)
(* Seam: src/Excalibur/Excalibur.Compliance.Abstractions/Erasure/           *)
(*       IDataSubjectTokenFingerprintLedger.cs:100-107 states the           *)
(*       insert-if-absent obligations this model formalises.                *)
(*       Derivation: Erasure/DataSubjectPepperFingerprint.cs.               *)
(*                                                                         *)
(* WHAT IS ABSTRACTED AWAY -- none of this is established by any arm here:  *)
(*   - the HKDF derivation itself. A fingerprint is an opaque value; this   *)
(*     model cannot speak to collision resistance or to the canary's        *)
(*     choice, and a derivation that mapped two different token functions   *)
(*     onto one fingerprint would satisfy every invariant below while       *)
(*     violating the real guarantee.                                        *)
(*   - the store's own atomicity. The claim is modelled as one indivisible  *)
(*     step because the contract REQUIRES one; an implementation whose      *)
(*     insert-if-absent is not atomic in the substrate is outside this      *)
(*     model, and that is exactly the obligation the contract spells out    *)
(*     in prose and no arm here checks.                                     *)
(*   - the one-ledger-per-store rule. A deployment with three token-keyed   *)
(*     databases holds three fingerprints and makes three claims; this      *)
(*     models ONE store. A verifier that resolved only one ledger would     *)
(*     report a green covering a third of a consumer's records, and that    *)
(*     defect is invisible here.                                            *)
(*   - the key-NAME gap. Per-subject encryption keys are named by a vault   *)
(*     handle with nowhere to put a row beside it, so a changed token       *)
(*     function stays undetected for key NAMES. Out of scope and real.      *)
(*   - legal holds, erasure execution, and the Article 17(3) consequence.   *)
(*     This model's harm is "two token functions wrote into one store"; it  *)
(*     does not model what an erasure then destroys.                        *)
(*                                                                         *)
(* A PASSING RUN MEANS EACH ARM GAVE ITS REQUIRED ANSWER. It does not mean  *)
(* the model is ADEQUATE. The abstractions above are where inadequacy would *)
(* live.                                                                    *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Processes,                  \* the processes starting against one store
    Fingerprints,               \* the distinct token-function fingerprints available
    UpdateAllowed,              \* DEFECT ARM: the claim overwrites rather than insert-if-absent
    FingerprintIsConfiguration  \* DEFECT ARM: fingerprint the configured key, not the hasher's output

\* Not a Fingerprint. An unbounded CHOOSE is unevaluable by TLC, so this is a plain
\* value outside the Fingerprints set rather than something derived from it.
NoFp == "none"

VARIABLES
    configFp,      \* each process's CONFIGURED key, as its options see it
    behaveFp,      \* each process's REGISTERED hasher's actual output -- what really keys its records
    recorded,      \* what the durable ledger holds, or NoFp
    firstRecorded, \* the value the FIRST claim wrote; auxiliary, for the write-once invariant
    proceeded,     \* processes that went on to write token-keyed records
    refused,       \* processes the ledger refused
    done           \* processes that have run their claim

vars == <<configFp, behaveFp, recorded, firstRecorded, proceeded, refused, done>>

\* The value a process claims. The whole content of the second ruling reversal is
\* which of the two this reads.
Claimed(p) == IF FingerprintIsConfiguration THEN configFp[p] ELSE behaveFp[p]

TypeOK ==
    /\ configFp \in [Processes -> Fingerprints]
    /\ behaveFp \in [Processes -> Fingerprints]
    /\ recorded \in (Fingerprints \cup {NoFp})
    /\ firstRecorded \in (Fingerprints \cup {NoFp})
    /\ proceeded \subseteq Processes
    /\ refused \subseteq Processes
    /\ done \subseteq Processes
    /\ proceeded \cap refused = {}

Init ==
    \* Every assignment of configured keys and actual hasher outputs is explored,
    \* including the ones where they disagree -- which is the consumer-hasher case.
    /\ configFp \in [Processes -> Fingerprints]
    /\ behaveFp \in [Processes -> Fingerprints]
    /\ recorded = NoFp
    /\ firstRecorded = NoFp
    /\ proceeded = {}
    /\ refused = {}
    /\ done = {}

\* One process starts up: claim, read back, decide. The test and the write are ONE
\* step, which is what the contract requires and what makes a lost race a success.
Claim(p) ==
    /\ p \notin done
    /\ done' = done \cup {p}
    /\ UNCHANGED <<configFp, behaveFp>>
    /\ IF recorded = NoFp
         THEN \* Nothing on file. This process's insert wins; the read-back is its own value.
              /\ recorded' = Claimed(p)
              /\ firstRecorded' = Claimed(p)
              /\ proceeded' = proceeded \cup {p}
              /\ UNCHANGED refused
         ELSE IF UpdateAllowed
                THEN \* THE DEFECT. Without write-once the claim is a splittable
                     \* check-then-act: this process overwrites and then its own
                     \* read-back matches, so it proceeds -- and a process with a
                     \* DIFFERENT function that already proceeded is now orphaned.
                     /\ recorded' = Claimed(p)
                     /\ UNCHANGED firstRecorded
                     /\ proceeded' = proceeded \cup {p}
                     /\ UNCHANGED refused
                ELSE IF recorded = Claimed(p)
                       THEN \* A lost race is a SUCCESS: the read observed the winner's
                            \* value and it agrees, so this process proceeds.
                            /\ UNCHANGED <<recorded, firstRecorded>>
                            /\ proceeded' = proceeded \cup {p}
                            /\ UNCHANGED refused
                       ELSE \* Refused, naming the setting to restore. Nothing is written.
                            /\ UNCHANGED <<recorded, firstRecorded>>
                            /\ UNCHANGED proceeded
                            /\ refused' = refused \cup {p}

\* Terminating stutter, so TLC has a complete behaviour rather than a deadlock.
Finished == /\ done = Processes
            /\ UNCHANGED vars

Next == (\E p \in Processes : Claim(p)) \/ Finished

Spec == Init /\ [][Next]_vars

-------------------------------------------------------------------------------
\* THE SAFETY PROPERTIES
-------------------------------------------------------------------------------

\* THE ONE THAT MATTERS. Every process that wrote token-keyed records into this
\* store used the SAME token function -- stated over behaveFp, because what keys a
\* record is the hasher that actually ran, never the configuration that was read.
\* This is the invariant a configuration fingerprint cannot establish.
InvProceedersAgreeOnBehaviour ==
    \A p \in proceeded : \A q \in proceeded : behaveFp[p] = behaveFp[q]

\* WRITE-ONCE. A recorded fingerprint never changes. The claim above DEPENDS on
\* this rather than merely benefiting from it: the claim is an insert-if-absent
\* followed by a read, so if any statement anywhere could update the value, two
\* processes can interleave so the one with the CORRECT function refuses while the
\* one with the WRONG function proceeds.
InvRecordedNeverChanges ==
    (recorded = NoFp) \/ (recorded = firstRecorded)

\* NO FALSE REFUSAL -- the liveness-shaped half, and it is what stops the fix being
\* "refuse everything". A deployment whose processes genuinely share one token
\* function must have nobody refused. Without this an arm could pass by refusing
\* every process, which would discharge nothing.
InvNoFalseRefusal ==
    (\A p \in Processes : \A q \in Processes : behaveFp[p] = behaveFp[q])
        => (refused = {})

\* NON-VACUITY. Somebody always gets through: the first claim can never be refused,
\* because an empty ledger agrees with everyone. If this ever failed, the model
\* would be refusing startup on a correctly configured deployment -- the failure
\* mode the contract's prose explicitly forbids.
InvSomebodyProceeds ==
    (done # {}) => (proceeded # {})

===============================================================================
