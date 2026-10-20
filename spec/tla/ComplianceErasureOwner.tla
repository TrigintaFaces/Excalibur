----------------------- MODULE ComplianceErasureOwner -----------------------
EXTENDS TLC

CONSTANTS Tenants, A, B, TenantQualified, EnforceOwner, PairLedger
ASSUME /\ Tenants = {A, B} /\ A # B

(*
SCOPE:
Bounded greenfield safety: two tenants share one subject; each owns a retained
and an unretained g1 key. Only A's unretained key is erased and may receive g2.
The host independently supplies expected ownership. Generation labels may
repeat under different handles. Destruction is atomic; stage and record are not.
One read is chosen nondeterministically from every owner/kind/generation choice.
Other reads have no shared writes in this abstraction, so projecting them out
preserves the chosen read's observations. This does not model parallel caches,
provider implementation effects, metadata forgery, arbitrary retention policy,
crash recovery, legacy deployment ordering or eventual-progress guarantees.
*)

Kinds == {"subject", "retained"}
Gens == {"g1", "g2"}
Handle(t, k) == <<IF TenantQualified THEN t ELSE "shared", "subject-id", k>>
Pair(t, k, g) == <<Handle(t, k), g>>
AllPairs == {Pair(t, k, g) : t \in Tenants, k \in Kinds, g \in Gens}
InitialPairs == {Pair(t, k, "g1") : t \in Tenants, k \in Kinds}
TargetHandle == Handle(A, "subject")
ReadKeys == Tenants \X Tenants \X Kinds \X Gens

VARIABLES material, destroyed, intents, ledger, erasePhase, crashed,
          envelopes, readKey, readPhase, outcome, observedDestroyed
vars == <<material, destroyed, intents, ledger, erasePhase, crashed,
          envelopes, readKey, readPhase, outcome, observedDestroyed>>

Init ==
    /\ material = InitialPairs /\ destroyed = {} /\ intents = {} /\ ledger = {}
    /\ erasePhase = "ready" /\ crashed = FALSE
    /\ envelopes = {<<t, k, "g1">> : t \in Tenants, k \in Kinds}
    /\ readKey \in ReadKeys /\ readPhase = "new"
    /\ outcome = "none" /\ observedDestroyed = FALSE

Stage ==
    /\ ~crashed /\ erasePhase = "ready"
    /\ intents' = intents \cup {p \in material : p[1] = TargetHandle}
    /\ erasePhase' = "staged"
    /\ UNCHANGED <<material, destroyed, ledger, crashed, envelopes,
                   readKey, readPhase, outcome, observedDestroyed>>
Destroy ==
    /\ ~crashed /\ erasePhase = "staged"
    /\ destroyed' = destroyed \cup {p \in material : p[1] = TargetHandle}
    /\ material' = {p \in material : p[1] # TargetHandle}
    /\ erasePhase' = "destroyed"
    /\ UNCHANGED <<intents, ledger, crashed, envelopes,
                   readKey, readPhase, outcome, observedDestroyed>>
Record ==
    /\ ~crashed /\ erasePhase = "destroyed"
    /\ ledger' = ledger \cup intents /\ erasePhase' = "recorded"
    /\ UNCHANGED <<material, destroyed, intents, crashed, envelopes,
                   readKey, readPhase, outcome, observedDestroyed>>
Crash ==
    /\ ~crashed /\ erasePhase \in {"staged", "destroyed"}
    /\ crashed' = TRUE
    /\ UNCHANGED <<material, destroyed, intents, ledger, erasePhase,
                   envelopes, readKey, readPhase, outcome, observedDestroyed>>
Remint ==
    /\ erasePhase \in {"destroyed", "recorded"}
    /\ <<A, "subject", "g2">> \notin envelopes
    /\ material' = material \cup {Pair(A, "subject", "g2")}
    /\ envelopes' = envelopes \cup {<<A, "subject", "g2">>}
    /\ UNCHANGED <<destroyed, intents, ledger, erasePhase, crashed,
                   readKey, readPhase, outcome, observedDestroyed>>

EnvelopePair == Pair(readKey[2], readKey[3], readKey[4])
LedgerAnswersDestroyed == IF PairLedger THEN EnvelopePair \in ledger
                         ELSE \E p \in ledger : p[2] = readKey[4]
Guard ==
    /\ <<readKey[2], readKey[3], readKey[4]>> \in envelopes
    /\ readPhase = "new"
    /\ IF EnforceOwner /\ readKey[1] # readKey[2]
          THEN /\ readPhase' = "done" /\ outcome' = "rejected"
          ELSE /\ readPhase' = "authorized" /\ UNCHANGED outcome
    /\ UNCHANGED <<material, destroyed, intents, ledger, erasePhase,
                   crashed, envelopes, readKey, observedDestroyed>>
CheckLedger ==
    /\ readPhase = "authorized"
    /\ observedDestroyed' = (EnvelopePair \in ledger)
    /\ IF LedgerAnswersDestroyed
          THEN /\ readPhase' = "done" /\ outcome' = "tombstone"
          ELSE /\ readPhase' = "checked" /\ UNCHANGED outcome
    /\ UNCHANGED <<material, destroyed, intents, ledger, erasePhase,
                   crashed, envelopes, readKey>>
Decrypt ==
    /\ readPhase = "checked" /\ readPhase' = "done"
    /\ outcome' = IF EnvelopePair \in material THEN "plaintext" ELSE "unavailable"
    /\ UNCHANGED <<material, destroyed, intents, ledger, erasePhase,
                   crashed, envelopes, readKey, observedDestroyed>>
Next == Stage \/ Destroy \/ Record \/ Crash \/ Remint \/ Guard
        \/ CheckLedger \/ Decrypt \/ UNCHANGED vars

TypeOK ==
    /\ material \subseteq AllPairs /\ destroyed \subseteq AllPairs
    /\ intents \subseteq AllPairs /\ ledger \subseteq AllPairs
    /\ erasePhase \in {"ready", "staged", "destroyed", "recorded"}
    /\ crashed \in BOOLEAN /\ envelopes \subseteq (Tenants \X Kinds \X Gens)
    /\ readKey \in ReadKeys
    /\ readPhase \in {"new", "authorized", "checked", "done"}
    /\ outcome \in {"none", "rejected", "plaintext", "tombstone", "unavailable"}
    /\ observedDestroyed \in BOOLEAN
InvOtherOwner == \A k \in Kinds : Pair(B, k, "g1") \in material
InvRetained == \A t \in Tenants : Pair(t, "retained", "g1") \in material
InvReadOwner == outcome \in {"plaintext", "tombstone"} => readKey[1] = readKey[2]
InvNoFalseTombstone == outcome = "tombstone" => EnvelopePair \in destroyed
InvLedgerSound == ledger \subseteq destroyed
InvRepairable == destroyed \subseteq (ledger \cup intents)
InvRecordedOldRead ==
    (readKey = <<A, A, "subject", "g1">> /\ observedDestroyed /\ readPhase = "done")
    => outcome = "tombstone"
Spec == Init /\ [][Next]_vars
=============================================================================
