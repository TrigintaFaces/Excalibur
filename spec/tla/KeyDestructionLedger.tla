---------------------------- MODULE KeyDestructionLedger ----------------------------
(***************************************************************************)
(* SCOPE -- read this before trusting any result from this model.          *)
(*                                                                         *)
(* WHAT IS MODELLED. One key handle, a set of key generations provisioned  *)
(* at it over time, one erasure that destroys the handle, and the two      *)
(* durable writes around that destruction. The property under test is the  *)
(* one a crypto-shred READ depends on: when may a field read answer `null` *)
(* -- a tombstone the consumer reads as a discharged Article 17 erasure.   *)
(*                                                                         *)
(* THE ONE MODELLING DECISION THAT CARRIES THE RESULT. After the           *)
(* destruction, the generation identifier is UNREADABLE. It is backend     *)
(* material in every provider we ship -- the Azure opaque version id       *)
(* (AzureKeyVaultProvider.cs:975), the AWS CMK id (AwsKmsProvider.cs:1219),*)
(* the Vault KV sidecar marker (VaultKeyProvider.cs:119), the in-memory    *)
(* entry (InMemoryKeyManagementProvider.cs:551) -- and the destruction     *)
(* takes it with the material. So `Record` can only ever write what was    *)
(* STAGED: `ledger' = ledger \cup intents`. That single equation is why    *)
(* the no-staging design cannot be repaired by a retry, and if it is wrong *)
(* the whole model is wrong.                                              *)
(*                                                                         *)
(* A DESTRUCTION DESTROYS A SET. Key Vault has no per-version delete: a    *)
(* delete takes the key with every version it holds                        *)
(* (AzureKeyVaultProvider.cs:552-555). `Destroy` therefore moves EVERY     *)
(* generation at the handle to "destroyed" in one step, which is what makes*)
(* the record-only-the-current-generation design reachable as a defect.    *)
(*                                                                         *)
(* WHAT IS ABSTRACTED AWAY, and therefore NOT established here:            *)
(*   - the backends themselves. No vault, no 404, no soft-delete window.   *)
(*     The provider-level defects (Excalibur_Dispatch-ehp87m,              *)
(*     Excalibur_Dispatch-rr3gvy) are ABSENCE-AS-EVIDENCE faults that this *)
(*     design removes by construction -- by not asking a backend -- so they*)
(*     are out of frame rather than refuted. A real-vault conformance arm  *)
(*     is their evidence, not this model.                                  *)
(*   - the tenant dimension, the request id, and the key handle. The read  *)
(*     predicate names none of them, deliberately (ADR-345), so modelling  *)
(*     them would model a predicate we are not building.                   *)
(*   - escrow. `RevokeEscrowAsync` precedes destruction in the             *)
(*     implementation and its SUCCESS SIGNAL is not evidence of its effect;*)
(*     that premise is stated in the spec and is not checked here.         *)
(*   - crash RECOVERY. Processes stop; nothing resumes. That is deliberate:*)
(*     the question is whether a stopped erasure leaves a state a retry    *)
(*     COULD repair, which is what InvRepairable measures.                 *)
(*   - THE DEFERRED CONFIRM PATH, and this omission cost something, so it  *)
(*     is recorded rather than merely listed. `Destroy` here completes in  *)
(*     one step. The implementation has a second path: a destruction that  *)
(*     returns ScheduledIrreversible completes LATER, when a separate      *)
(*     confirm observes the backend attesting the handle is gone. Because  *)
(*     this model has no such state, it could not show that the confirm    *)
(*     wrote no ledger row -- so a subject erased on Azure's DEFAULT       *)
(*     posture (purge protection on) ended with a certificate, no row, and *)
(*     reads failing forever. InvRepairable would have caught it had the   *)
(*     state existed to reach. Found by a human reading the service, not   *)
(*     by this model, and it is the clearest illustration available of the *)
(*     rule that a proof is valid only inside what you modeled. The ruling *)
(*     is spec section 8a; extending the model to cover it is owed.        *)
(*                                                                         *)
(* SEAM THIS CLAIMS TO CORRESPOND TO:                                      *)
(*   src/Excalibur/Excalibur.Compliance/Erasure/ErasureService.cs:968-999  *)
(*     (destroy, then record, gated on KeyDestructionState.Completed)      *)
(*   src/Excalibur/Excalibur.Compliance/CryptoShredding/FieldEncryptor.cs  *)
(*     :139-141 (the predicate, and the tombstone it produces)             *)
(*   management/specs/key-destruction-ledger-spec.md sections 3, 4, 4a      *)
(*                                                                         *)
(* STATUS: written at DESIGN time, before the implementation exists. A     *)
(* model written afterwards restates the code instead of checking it.      *)
(***************************************************************************)
EXTENDS FiniteSets

CONSTANTS
    Gens,                 \* generations provisioned at this handle, oldest..newest
    Current,              \* the generation an ordinary read of the handle returns
    StageFirst,           \* TRUE: stage the intent durably BEFORE destroying
    RecordAll,            \* TRUE: stage every generation; FALSE: only Current
    RecordBeforeDestroy   \* TRUE: write the ledger row before destroying (the unsafe order)

ASSUME Current \in Gens

VARIABLES
    material,   \* generation -> "live" | "destroyed"
    intents,    \* generations staged durably, invisible to the read predicate
    ledger,     \* generations the ledger asserts were destroyed. A ROW'S EXISTENCE IS THE STATEMENT.
    pc          \* "start" -> "staged" -> "destroyed" -> "recorded"

vars == <<material, intents, ledger, pc>>

\* What the erasure intends to write down. Only Current is readable as "the" generation of
\* the handle; RecordAll models enumerating every version instead.
Targeted == IF RecordAll THEN Gens ELSE {Current}

TypeOK ==
    /\ material \in [Gens -> {"live", "destroyed"}]
    /\ intents \subseteq Gens
    /\ ledger \subseteq Gens
    /\ pc \in {"start", "staged", "destroyed", "recorded"}

Init ==
    /\ material = [g \in Gens |-> "live"]
    /\ intents = {}
    /\ ledger = {}
    /\ pc = "start"

\* A1' -- stage the intent durably, outside the ledger, where the predicate cannot see it.
Stage ==
    /\ pc = "start"
    /\ intents' = IF StageFirst THEN Targeted ELSE {}
    /\ ledger' = IF RecordBeforeDestroy THEN ledger \cup Targeted ELSE ledger
    /\ material' = material
    /\ pc' = "staged"

\* A2 -- the destruction. Irreversible, and it takes EVERY generation at the handle.
Destroy ==
    /\ pc = "staged"
    /\ material' = [g \in Gens |-> "destroyed"]
    /\ intents' = intents
    /\ ledger' = ledger
    /\ pc' = "destroyed"

\* A3 -- record. THE LOAD-BEARING LINE: only what was staged can be written, because the
\* destruction destroyed the generation identifier along with the material.
Record ==
    /\ pc = "destroyed"
    /\ ledger' = ledger \cup intents
    /\ material' = material
    /\ intents' = intents
    /\ pc' = "recorded"

Done == pc = "recorded" /\ UNCHANGED vars

Next == Stage \/ Destroy \/ Record \/ Done

Spec == Init /\ [][Next]_vars

(***************************************************************************)
(* S1 -- CATASTROPHIC AND SILENT. A tombstone is published only over       *)
(* material that is actually destroyed. Violating this asserts a lawful    *)
(* erasure over recoverable personal data, and nothing downstream ever     *)
(* learns otherwise.                                                       *)
(***************************************************************************)
InvS1 == \A g \in ledger : material[g] = "destroyed"

(***************************************************************************)
(* REPAIRABLE -- the liveness property, expressed as a safety property so  *)
(* TLC can check it without fairness. Every destroyed generation is either *)
(* already recorded or still STAGEABLE, i.e. its identifier survives       *)
(* somewhere a retry can reach. A violation is a subject whose data is     *)
(* genuinely gone and whose reads throw FOREVER, with no repair available  *)
(* -- which is the defect that broke the first version of this design.     *)
(***************************************************************************)
InvRepairable == \A g \in Gens : material[g] = "destroyed" => (g \in ledger \/ g \in intents)

(***************************************************************************)
(* J -- the invariant the guarantee rests on. A ledger row exists only     *)
(* where the destruction it names has already completed. This is S1 read   *)
(* from the ledger's side, and it is what makes "a row's existence IS the  *)
(* statement" sound.                                                       *)
(***************************************************************************)
InvJ == (ledger # {}) => (\A g \in ledger : material[g] = "destroyed")

=============================================================================
