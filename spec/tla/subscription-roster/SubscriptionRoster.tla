--------------------- MODULE SubscriptionRoster ---------------------
(* HISTORICAL MODEL: this module preserves counterexamples for the protocol
   before BindingId-fenced checkpoints and snapshot-qualified receipts.
   References below to "shipped" describe that historical implementation.
   Current lifetime/read correspondence is modelled separately in
   BindingLifetime.tla; do not cite a historical passing arm as its proof. *)
(***************************************************************************)
(* SCOPE -- read this before citing any result from this model.             *)
(*                                                                          *)
(* STATUS: a DRAFT until TLC says otherwise, and a pass here is NOT a claim  *)
(* that R4 is discharged for the subscription-roster seam. R4 remains        *)
(* UNVERIFIED in eng/governance/rigor-ladder.yaml. What this module does and *)
(* does not establish is in LIMITS, below, and the shipped configuration     *)
(* VIOLATES one of the four invariants here. That is the finding, not a bug  *)
(* in the model.                                                             *)
(*                                                                          *)
(* WHAT IS MODELLED                                                          *)
(*   Two or more cooperating processing hosts sharing one subscription, each  *)
(*   hosting a DIFFERENT, OVERLAPPING subset of its projections. Per host:    *)
(*                                                                            *)
(*     CLAIM  -- insert-if-absent one roster row per member it hosts, keyed    *)
(*               (SubscriptionName, ProjectionKey). ONE atomic step.           *)
(*     READ   -- read the WHOLE roster back. A SEPARATE step, so its result    *)
(*               can be overtaken before the host uses it.                     *)
(*     FOLD   -- advance the durable cursor of a member it hosts. A host can    *)
(*               NEVER advance a member it does not host; that absence of an    *)
(*               action is the whole reason an unhosted member is unattestable. *)
(*     ATTEST -- report "every member of this subscription is caught up".        *)
(*                                                                              *)
(*   Plus a global COMMIT advancing the committed position, and a RELEASE        *)
(*   unbinding ONE member and dropping its cursor.                              *)
(*                                                                              *)
(* ===================================================================== *)
(* THE DIRECTION OF STALENESS. Get this backwards and the model proves    *)
(* the wrong thing, so it is stated before anything else.                 *)
(*                                                                        *)
(* An attestation is permitted IFF the unattestable set is EMPTY          *)
(* (AsyncProjectionProcessingHost.cs:293, :336, :735), and that set is    *)
(* `roster \ hosted`. Therefore:                                          *)
(*                                                                        *)
(*     roster GROWS   -> unattestable can only GROW                       *)
(*                    -> the attestation is strictly MORE restricted      *)
(*                    -> SAFE                                             *)
(*     roster SHRINKS -> unattestable can only SHRINK                     *)
(*                    -> the attestation is strictly MORE permissive      *)
(*                    -> UNSAFE                                           *)
(*                                                                        *)
(* A stale read SHRINKS the domain, because the only mutation it can miss  *)
(* is an INSERT. An attestation is a sentence universally quantified over  *)
(* the roster, and shrinking the domain of a universal WEAKENS the         *)
(* proposition -- the consumer is handed the weaker sentence under the     *)
(* stronger sentence's label. Staleness is therefore NOT conservative.    *)
(*                                                                        *)
(* The decisive argument, and the reason no arm here asserts the          *)
(* contrary: THE LOCAL REGISTRATION LIST IS THE MAXIMALLY STALE ROSTER    *)
(* READ -- it returns only what this process hosts, which is the smallest *)
(* domain available. Any reasoning that made staleness safe would         *)
(* therefore license the local quantifier the design exists to forbid.    *)
(* Arms _SR_StaleWindow and _SR_LocalQuant are the same defect at two     *)
(* magnitudes, and both VIOLATE.                                          *)
(*                                                                        *)
(* AND THE MISSED MEMBER IS ALWAYS AT THE WORST VALUE. Binding never      *)
(* creates a checkpoint (the spec's bind semantics, step 6), so a         *)
(* newly inserted member's cursor is ABSENT. The window is not merely     *)
(* unsafe; it is unsafe at the maximally-behind value. `cursor` therefore *)
(* carries an explicit "absent" rather than a zero.                       *)
(* ===================================================================== *)
(*                                                                        *)
(* FIVE INDEPENDENT MECHANISMS, FIVE CONSTANTS -- separated because each   *)
(* fails differently. An earlier model of a different seam in this tree    *)
(* folded two mechanisms into one flag, so no arm isolated either and the  *)
(* counterexample it produced concerned a mechanism its design document    *)
(* does not argue for.                                                    *)
(*                                                                        *)
(*   LocalQuantifier  -- TRUE when the attestation quantifies over the     *)
(*                       host's OWN registration list instead of the       *)
(*                       roster read from the durable store. The defect    *)
(*                       the design exists to prevent.                     *)
(*                                                                        *)
(*   UpsertBinding    -- TRUE when binding OVERWRITES an existing member   *)
(*                       row instead of declining. FALSE is the claim.     *)
(*                                                                        *)
(*   ReleaseClearsSubscription                                             *)
(*                    -- TRUE when releasing one member drops EVERY member *)
(*                       and every cursor. FALSE is the scoped unbind.     *)
(*                                                                        *)
(*   AtomicRosterRead -- TRUE when the read is atomic with the attestation.*)
(*                       THE SHIPPED CODE DOES NOT HAVE THIS AND CANNOT:   *)
(*                       the claim and the read-back are separate          *)
(*                       statements in no transaction, and the attestation *)
(*                       is a whole page fold later still. This constant   *)
(*                       exists to MEASURE what the missing atomicity      *)
(*                       costs, never to grant it and print a green. It is *)
(*                       the unimplementable limit, not an option.         *)
(*                                                                        *)
(*   AllowReRead      -- TRUE when a host may re-read the roster           *)
(*                       immediately before attesting. This is the obvious *)
(*                       cheap fix, and the arm that enables it still      *)
(*                       VIOLATES: a fresher read moves the window, it     *)
(*                       does not close it.                               *)
(*                                                                        *)
(*   TokenMode        -- "none" | "membership". "membership" models the    *)
(*                       attestation CARRYING the roster snapshot it was   *)
(*                       quantified over, so staleness becomes DETECTABLE  *)
(*                       instead of silent. See InvNoSilentFalseFresh.     *)
(*                                                                        *)
(* WHICH SEAM THIS CLAIMS TO CORRESPOND TO. Every line below was read at    *)
(* the stated line before being written here. Re-verify before copying     *)
(* any of them forward -- a stale line reference is a false correspondence  *)
(* claim, and these files are under active change.                         *)
(*                                                                        *)
(*   src/Excalibur/Excalibur.EventSourcing/Projections/                    *)
(*     AsyncProjectionProcessingHost.cs                                    *)
(*       :735  UnattestableMembers(roster, registrations) -- the roster     *)
(*             MINUS the locally hosted keys, ordinal. The model's          *)
(*             UnattestableMembers operator mirrors exactly this.           *)
(*       :327  BindDefinitionAsync -- WHERE THE ROSTER IS READ, once per    *)
(*             page, inside ProcessPageAsync.                              *)
(*       :336  the host computes the unattestable set from THAT roster.     *)
(*       :293  a non-empty set denies caught-up, ahead of every other       *)
(*             branch of the predicate.                                    *)
(*       :218  IsCaughtUp -- WHERE THE ATTESTATION IS DECIDED, inside the   *)
(*             page LOOP of ProcessThroughAsync. The window between :327    *)
(*             and :218 spans an entire page fold, and a process may pause  *)
(*             between any two steps for unbounded time. That gap is the    *)
(*             non-atomicity this model declines to grant.                 *)
(*       :681  BindSubscriptionAsync's return value is KEPT -- the roster,  *)
(*             not the local registration list, is the domain.             *)
(*                                                                        *)
(*   src/Excalibur/Excalibur.EventSourcing/Subscriptions/                  *)
(*     ISubscriptionCheckpointStore.cs                                     *)
(*       :82   BindSubscriptionAsync(name, members, ct) -> SubscriptionRoster*)
(*       :116  ReleaseSubscriptionMemberAsync(name, key, ct) -> bool        *)
(*                                                                        *)
(*   src/Excalibur/Excalibur.EventSourcing.Postgres/                       *)
(*     PostgresSubscriptionCheckpointStore.cs                              *)
(*       :84   BindSubscriptionAsync                                        *)
(*       :108  ON CONFLICT (subscription_name, projection_key) DO NOTHING   *)
(*             -- the per-member atomic claim. This is why Claim is ONE step.*)
(*       :128  the read-back SELECT. A SEPARATE statement, in NO            *)
(*             transaction, which is why ReadRoster is a SEPARATE step.     *)
(*       :160  ReleaseSubscriptionMemberAsync -- the scoped unbind.         *)
(*                                                                        *)
(*   Design: management/specs/subscription-roster-workset-evolution-spec.md *)
(*           sections 2.1 and 3.3 (R-QUANTIFIER).                           *)
(*           management/architecture/                                       *)
(*             adr-356-subscription-roster-and-destination-identity.md,     *)
(*           Decisions 1 and 2.                                             *)
(*                                                                        *)
(* WHAT IS ABSTRACTED AWAY -- no result here is evidence about any of these *)
(*                                                                        *)
(*   a. THE DEFINITION-HASH MISMATCH REFUSAL. A bind whose supplied hash    *)
(*      differs from the stored one throws in production. Here a bind       *)
(*      always proceeds, so a host never halts. Modelling the throw would   *)
(*      have made the quantifier defect UNREACHABLE in any arm with         *)
(*      divergent versions -- a halted host never attests -- which is an    *)
(*      accidental vacuity. So the version axis and the quantifier axis     *)
(*      live in SEPARATE arms and the refusal is dropped from both. The     *)
(*      refusal is an availability property; the four invariants here are   *)
(*      safety properties.                                                 *)
(*   b. The required-SUBSET form of the completion predicate. Modelled      *)
(*      hosts must advance EVERY member they host, which is the            *)
(*      `required is null` branch at :297. The subset branch at :301 is a   *)
(*      second axis and is not here.                                       *)
(*   c. Checkpoint compare-and-set and its loss under contention. A fold is *)
(*      an unconditional advance. CONSEQUENTLY this model carries the       *)
(*      absent-versus-zero cursor DISTINCTION but checks nothing about it:  *)
(*      the reason absence must stay distinguishable is the CAS's expected  *)
(*      value, and the CAS is not modelled. An invariant asserting "bind    *)
(*      creates no checkpoint" would be true by construction here, which    *)
(*      discharges nothing, so none is declared.                           *)
(*   d. Destination identity, definition-hash CONTENT, scope, revision and  *)
(*      the whole destination-version axis. A member is an opaque key plus  *)
(*      an opaque version number.                                          *)
(*   e. Crash, recovery, partial commit, lost acknowledgement. Commit is    *)
(*      atomic and instantaneous; a host never dies mid-page.              *)
(*   f. The atomicity of release across the member row and its checkpoint   *)
(*      row. Release is one step here, so whether the store really makes it *)
(*      one transaction is a storage property this model GRANTS.           *)
(*   g. THE CONSUMER SIDE OF THE GENERATION TOKEN. TokenMode = "membership" *)
(*      establishes that the staleness is always WITNESSED by a discrepancy *)
(*      between the host's snapshot and the durable roster at the           *)
(*      attestation instant. It does NOT establish that any party can       *)
(*      observe that discrepancy without a further read of its own. See     *)
(*      LIMITS 6.                                                          *)
(*   h. Page grouping, tenancy, the health minimum, and the global-stream   *)
(*      host.                                                              *)
(*                                                                        *)
(* LIMITS -- what a PASS does NOT establish                                *)
(*                                                                        *)
(*   1. THE SHIPPED SHAPE VIOLATES InvNoFalseFresh, and the arms say so     *)
(*      rather than hiding it. Do not quote "the model passes" without      *)
(*      naming which invariant and which configuration.                   *)
(*                                                                        *)
(*   2. InvNoBlindFresh IS A CONSEQUENCE OF THE ATTEST GUARD, not a deep    *)
(*      theorem: with the store-sourced domain the guard requires the read  *)
(*      roster to be a subset of what this host hosts and every hosted      *)
(*      member to have reached the target, so no member of the read roster  *)
(*      can be behind. What the model EARNS is the other direction -- that  *)
(*      the local-quantifier variant of the same guard is reachably false,  *)
(*      by a concrete eight-state interleaving across two hosts. Cite the   *)
(*      refuted arm, never the holding one.                                *)
(*                                                                        *)
(*   3. MUTUAL EXCLUSION IS NOT GRANTED, BUT SINGLE-STATEMENT ATOMICITY IS. *)
(*      `ON CONFLICT DO NOTHING` on the composite key is one atomic step    *)
(*      because that is what the engine provides for one statement. The     *)
(*      model therefore CANNOT see the claim being rewritten as a read      *)
(*      followed by an insert, the isolation level being changed, or the    *)
(*      key losing a column -- which are exactly the edits that would       *)
(*      break the premise.                                                 *)
(*                                                                        *)
(*   4. THE INVARIANTS ARE SAFETY ONLY. Nothing here says a host eventually *)
(*      attests. A design that never attests satisfies every one of them.   *)
(*                                                                        *)
(*   5. THE BOUNDS ARE SMALL -- three keys, three hosts, two positions. A   *)
(*      violation found is real; an ABSENCE of violation is an absence      *)
(*      within those bounds only.                                          *)
(*                                                                        *)
(*   6. THE TOKEN RESULT IS INFORMATION-THEORETIC, NOT AN IMPLEMENTATION.   *)
(*      It says a witness to the staleness always EXISTS at the attestation *)
(*      instant. Turning that into a mechanism needs the attestation to     *)
(*      CARRY the observed snapshot to whoever consumes it, and the         *)
(*      consumer to compare it against the roster it reads when it acts --  *)
(*      which moves the comparison to the consumer's read rather than       *)
(*      eliminating a read. Neither the carrying nor the comparing is       *)
(*      modelled. This is a lead for the seam's redesign, not a verified    *)
(*      fix.                                                               *)
(*                                                                        *)
(*   7. THE TOKEN DOES NOT FIX THE QUANTIFIER. Arm _SR_TokenLocal shows a   *)
(*      local-quantifier host attesting falsely with a PERFECTLY CURRENT    *)
(*      snapshot, so the token reveals nothing. The two defects are         *)
(*      independent and a token is no substitute for Decision 1.           *)
(*                                                                        *)
(* WHY THIS SEAM OWES A MODEL AT ALL: a false attestation is SILENT. The    *)
(* consumer was told it need not re-check, so nothing downstream ever       *)
(* learns the read model is stale. Silence, not severity, is the            *)
(* discriminator for a catastrophic blast radius.                          *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    NumHosts,                   \* how many cooperating hosts
    Topology,                   \* "subset" = overlapping worksets; "all" = everyone hosts everything
    DivergentVersions,          \* TRUE = host 2 writes a different definition version
    MaxPos,                     \* bound on committed global positions
    LocalQuantifier,            \* TRUE = the attestation domain is the LOCAL registry (the defect)
    UpsertBinding,              \* TRUE = binding overwrites instead of claiming (the defect)
    ReleaseClearsSubscription,  \* TRUE = release drops every member (the defect)
    AtomicRosterRead,           \* TRUE = the read is atomic with the attestation (unimplementable limit)
    AllowReRead,                \* TRUE = a host may re-read the roster just before attesting
    TokenMode,                  \* "none" | "membership"
    NoCursor                    \* the ABSENT-checkpoint sentinel; a TLC MODEL VALUE, see below

Hosts == 1 .. NumHosts
Keys  == {"kA", "kB", "kC"}

(***************************************************************************)
(* THE TOPOLOGY. "subset" is the deployment the design must support, and   *)
(* the spec calls it out: a host hosting a SUBSET of a subscription is a   *)
(* SUPPORTED deployment, not a misconfiguration, which is why the          *)
(* dangerous reading of the quantifier is also the common one.            *)
(*                                                                        *)
(*   host 1 -> {kA, kB}       a STRICT SUBSET of host 2's workset         *)
(*   host 2 -> {kA, kB, kC}   the full set; the ONLY host that can fold kC *)
(*   host 3 -> {kB, kC}       overlaps 1 on kB and 2 on kB,kC; nested in  *)
(*                            neither                                     *)
(*                                                                        *)
(* "all" exists ONLY for the adequacy probe, where key coverage must be    *)
(* held constant while the host count varies. Without that control a       *)
(* diameter change is attributable to more keys becoming bindable rather   *)
(* than to concurrency, which is the confound that makes a diameter probe  *)
(* worthless.                                                             *)
(***************************************************************************)
Hosted ==
    IF Topology = "all"
    THEN [h \in Hosts |-> Keys]
    ELSE [h \in Hosts |->
            CASE h = 1 -> {"kA", "kB"}
              [] h = 2 -> {"kA", "kB", "kC"}
              [] OTHER -> {"kB", "kC"}]

(***************************************************************************)
(* The definition version a host writes for a member it hosts. In          *)
(* production two hosts running the same code agree; they diverge during a  *)
(* rolling deployment that changed a projection's definition. Uniform in   *)
(* every arm that tests the QUANTIFIER, divergent only in the arm that     *)
(* tests roster STABILITY -- see abstraction (a) for why mixing the two    *)
(* axes in one arm manufactures vacuity.                                   *)
(***************************************************************************)
DefVersion == [h \in Hosts |-> IF DivergentVersions /\ h = 2 THEN 2 ELSE 1]

Versions == {DefVersion[h] : h \in Hosts}

(***************************************************************************)
(* NoCursor -- the ABSENT-checkpoint sentinel. NOT a position, and          *)
(* deliberately not zero: binding never creates a checkpoint, so a member   *)
(* another host has just inserted is at the MAXIMALLY BEHIND value, and the *)
(* model carries that as its own value rather than as a number.             *)
(*                                                                          *)
(* A DECLARED CONSTANT, assigned a TLC model value in every .cfg, and the   *)
(* first attempt used a string literal instead. TLC refused it outright --  *)
(* "Attempted to check equality of string with non-string: 1" -- because it *)
(* will not compare a string against an integer even where TLA+ says the    *)
(* answer is FALSE. A model value is unequal to everything but itself, by   *)
(* construction, which is exactly the sentinel semantics wanted. Recorded   *)
(* because the symptom (TLC "unable to fingerprint") names neither the      *)
(* cause nor the fix.                                                       *)
(***************************************************************************)

VARIABLES
    roster,             \* [Keys -> Nat]  0 = unbound; n = bound at definition version n. DURABLE.
    cursor,             \* [Keys -> {NoCursor} \cup 1..MaxPos]  the durable per-member checkpoint
    view,               \* [Hosts -> SUBSET Keys]  each host's LAST roster read. Can be STALE.
    pc,                 \* [Hosts -> {"idle","claimed","bound"}]
    committed,          \* the global position of the last committed event
    rosterOverwritten,  \* history: a bind replaced an existing member row with a different one
    lostProgress,       \* history: a release dropped a sibling's binding or cursor
    blindFresh,         \* history: an attestation was false about a member the host HAD READ
    falseFresh,         \* history: an attestation was false about the DURABLE roster
    silentFalseFresh    \* history: ... and nothing at the attestation instant witnessed it

vars == <<roster, cursor, view, pc, committed,
          rosterOverwritten, lostProgress, blindFresh, falseFresh, silentFalseFresh>>

TypeOK ==
    /\ roster \in [Keys -> {0} \cup Versions]
    /\ cursor \in [Keys -> {NoCursor} \cup (1 .. MaxPos)]
    /\ view   \in [Hosts -> SUBSET Keys]
    /\ pc     \in [Hosts -> {"idle", "claimed", "bound"}]
    /\ committed \in 0 .. MaxPos
    /\ rosterOverwritten \in BOOLEAN
    /\ lostProgress      \in BOOLEAN
    /\ blindFresh        \in BOOLEAN
    /\ falseFresh        \in BOOLEAN
    /\ silentFalseFresh  \in BOOLEAN

Init ==
    /\ roster = [k \in Keys |-> 0]
    /\ cursor = [k \in Keys |-> NoCursor]
    /\ view   = [h \in Hosts |-> {}]
    /\ pc     = [h \in Hosts |-> "idle"]
    /\ committed = 0
    /\ rosterOverwritten = FALSE
    /\ lostProgress      = FALSE
    /\ blindFresh        = FALSE
    /\ falseFresh        = FALSE
    /\ silentFalseFresh  = FALSE

\* The set of currently BOUND members, as the durable store holds it.
Bound(r) == {k \in Keys : r[k] # 0}

\* Position 0 is not a committed position -- Commit advances 0 -> 1 -- so ordering an
\* absent cursor at 0 loses nothing, while keeping NoCursor a distinct VALUE keeps the
\* "absent is not zero" distinction visible in every counterexample trace.
CursorPos(k) == IF cursor[k] = NoCursor THEN 0 ELSE cursor[k]

\* A bound member that has not folded through p.
Behind(k, p) == roster[k] # 0 /\ CursorPos(k) < p

(***************************************************************************)
(* UnattestableMembers -- the model's mirror of                            *)
(* AsyncProjectionProcessingHost.cs:735. A set difference over ordinal     *)
(* keys; nothing more.                                                      *)
(***************************************************************************)
UnattestableMembers(rosterKeys, h) == rosterKeys \ Hosted[h]

-----------------------------------------------------------------------------

(***************************************************************************)
(* COMMIT -- a write lands at the next global position.                     *)
(***************************************************************************)
Commit ==
    /\ committed < MaxPos
    /\ committed' = committed + 1
    /\ UNCHANGED <<roster, cursor, view, pc, rosterOverwritten,
                   lostProgress, blindFresh, falseFresh, silentFalseFresh>>

(***************************************************************************)
(* CLAIM -- insert-if-absent, one row per hosted member, in ONE step.       *)
(*                                                                          *)
(* One step is not an unearned grant: the store does this with a single     *)
(* statement whose ON CONFLICT clause on the composite primary key declines *)
(* rather than overwrites (:108), and a single statement is atomic at every *)
(* isolation level at or above read-committed. What is NOT granted is       *)
(* atomicity between this step and the READ-BACK, which really are two      *)
(* statements in no transaction.                                           *)
(*                                                                          *)
(* NOTE WHAT THIS STEP DOES NOT TOUCH: `cursor`. Binding never creates a    *)
(* checkpoint, so a member another host has just inserted sits at NoCursor  *)
(* -- maximally behind -- for every host that has not folded it.            *)
(*                                                                          *)
(* With UpsertBinding = TRUE the same step writes unconditionally, which    *)
(* makes the roster describe the LAST WRITER rather than the agreed set.    *)
(***************************************************************************)
Claimed(h) ==
    IF UpsertBinding
    THEN [k \in Keys |-> IF k \in Hosted[h] THEN DefVersion[h] ELSE roster[k]]
    ELSE [k \in Keys |-> IF k \in Hosted[h] /\ roster[k] = 0 THEN DefVersion[h] ELSE roster[k]]

Claim(h) ==
    /\ pc[h] = "idle"
    /\ roster' = Claimed(h)
    /\ pc' = [pc EXCEPT ![h] = "claimed"]
    \* PARENTHESISED, AND THE PARENTHESES ARE LOAD-BEARING. `=` binds tighter than
    \* `\/` in TLA+, so `x' = x \/ P` parses as `(x' = x) \/ P` -- a disjunction whose
    \* right branch assigns NOTHING. TLC rejected exactly that here with "the following
    \* variable is not assigned", which is the checker catching a model defect that
    \* reasoning about the model had not.
    /\ rosterOverwritten' =
         (rosterOverwritten \/ (\E k \in Hosted[h] : roster[k] # 0 /\ roster[k] # Claimed(h)[k]))
    /\ UNCHANGED <<cursor, view, committed, lostProgress,
                   blindFresh, falseFresh, silentFalseFresh>>

(***************************************************************************)
(* READ-BACK -- a SEPARATE statement (:128), and the step that admits       *)
(* section 2.1's interleaving: a host can observe a member ABSENT here and  *)
(* find it PRESENT later, because another host claimed it in between.       *)
(* Nothing in this model prevents that, and nothing in the store does.      *)
(***************************************************************************)
ReadRoster(h) ==
    /\ pc[h] = "claimed"
    /\ view' = [view EXCEPT ![h] = Bound(roster)]
    /\ pc'   = [pc EXCEPT ![h] = "bound"]
    /\ UNCHANGED <<roster, cursor, committed, rosterOverwritten,
                   lostProgress, blindFresh, falseFresh, silentFalseFresh>>

(***************************************************************************)
(* RE-READ -- the obvious cheap fix: read the roster again, as late as      *)
(* possible, right before attesting.                                       *)
(*                                                                          *)
(* The arm that enables this STILL VIOLATES InvNoFalseFresh, and that is a  *)
(* measured refutation rather than an argument: every read is a separate    *)
(* step from the attestation, so a fresher read MOVES the window and does   *)
(* not close it. The guard on the view differing is only to keep TLC from   *)
(* generating stutter transitions.                                         *)
(***************************************************************************)
ReRead(h) ==
    /\ AllowReRead
    /\ pc[h] = "bound"
    /\ view[h] # Bound(roster)
    /\ view' = [view EXCEPT ![h] = Bound(roster)]
    /\ UNCHANGED <<roster, cursor, pc, committed, rosterOverwritten,
                   lostProgress, blindFresh, falseFresh, silentFalseFresh>>

(***************************************************************************)
(* FOLD -- advance one member's durable cursor to the committed position.   *)
(* A host can only fold what it hosts. There is NO ACTION by which a host   *)
(* advances a member it does not host, and that absence -- not a guard -- is *)
(* why an unhosted member can never be attested here.                      *)
(***************************************************************************)
Fold(h, k) ==
    /\ pc[h] = "bound"
    /\ k \in Hosted[h]
    /\ roster[k] # 0
    /\ CursorPos(k) < committed
    /\ cursor' = [cursor EXCEPT ![k] = committed]
    /\ UNCHANGED <<roster, view, pc, committed, rosterOverwritten,
                   lostProgress, blindFresh, falseFresh, silentFalseFresh>>

(***************************************************************************)
(* ATTEST -- report that every member of the subscription is caught up.    *)
(*                                                                          *)
(* The DOMAIN of the quantifier is the whole decision under test:          *)
(*                                                                          *)
(*   LocalQuantifier = TRUE   -> Hosted[h], this process's own              *)
(*                              registrations. A member it does not host is *)
(*                              INVISIBLE, so the sentence silently narrows *)
(*                              to "every member I happen to host is caught *)
(*                              up" -- true, useless, and indistinguishable *)
(*                              from the honest version at every call site. *)
(*   AtomicRosterRead = TRUE  -> the durable roster AT THIS INSTANT. The    *)
(*                              unimplementable limit.                      *)
(*   otherwise                -> view[h], the roster as this host last read *)
(*                              it. THE SHIPPED SHAPE.                      *)
(***************************************************************************)
AttestDomain(h) ==
    IF LocalQuantifier
    THEN Hosted[h]
    ELSE IF AtomicRosterRead
         THEN Bound(roster)
         ELSE view[h]

\* Was there, at this instant, anything that could have revealed the attestation's
\* domain to be stale? With a membership token the attestation carries the snapshot it
\* was quantified over, so a snapshot differing from the durable roster is a witness.
StalenessWitnessed(h) == TokenMode = "membership" /\ view[h] # Bound(roster)

Attest(h) ==
    /\ pc[h] = "bound"
    \* A non-empty unattestable set blocks the ATTESTATION. It does not block the WORK:
    \* Fold is independent of this guard, so a partial host still folds what it hosts.
    \* An implementation that blocked the work too would satisfy every invariant below
    \* while making a partial-host deployment unusable -- the inverse defect.
    /\ UnattestableMembers(AttestDomain(h), h) = {}
    /\ \A k \in Hosted[h] : ~Behind(k, committed)
    /\ pc' = [pc EXCEPT ![h] = "idle"]
    \* THE ORACLES. Neither is the host's own domain -- that would be circular. Both
    \* read the DURABLE store, which is what the attestation is a claim about.
    \*   blindFresh:  false about a member this host HAD ALREADY READ. Information held.
    \*   falseFresh:  false about any currently bound member, read or not.
    \*   silentFalseFresh: false, and nothing at this instant witnessed the staleness.
    /\ blindFresh' =
         (blindFresh \/ (\E k \in view[h] : Behind(k, committed)))
    /\ falseFresh' =
         (falseFresh \/ (\E k \in Keys : Behind(k, committed)))
    /\ silentFalseFresh' =
         (silentFalseFresh \/ ((\E k \in Keys : Behind(k, committed)) /\ ~StalenessWitnessed(h)))
    /\ UNCHANGED <<roster, cursor, view, committed, rosterOverwritten, lostProgress>>

(***************************************************************************)
(* RELEASE -- unbind ONE member and drop its cursor, so that member alone   *)
(* re-binds and re-folds from zero. A rebuild resetting its own cursor.     *)
(*                                                                          *)
(* With ReleaseClearsSubscription = TRUE the same request drops every       *)
(* member and every cursor. That variant satisfies every ATTESTATION        *)
(* invariant here -- an empty roster has no member behind it -- while        *)
(* destroying every sibling projection's progress, which is why the sibling *)
(* property has to be checked on its own and cannot be a corollary.         *)
(***************************************************************************)
Release(h, k) ==
    /\ pc[h] = "idle"
    /\ k \in Hosted[h]
    /\ roster[k] # 0
    /\ IF ReleaseClearsSubscription
       THEN /\ roster' = [j \in Keys |-> 0]
            /\ cursor' = [j \in Keys |-> NoCursor]
            /\ lostProgress' =
                 (lostProgress \/ (\E j \in Keys \ {k} : roster[j] # 0 \/ cursor[j] # NoCursor))
       ELSE /\ roster' = [roster EXCEPT ![k] = 0]
            /\ cursor' = [cursor EXCEPT ![k] = NoCursor]
            /\ lostProgress' = lostProgress
    /\ UNCHANGED <<view, pc, committed, rosterOverwritten,
                   blindFresh, falseFresh, silentFalseFresh>>

Next ==
    \/ Commit
    \/ \E h \in Hosts : Claim(h) \/ ReadRoster(h) \/ ReRead(h) \/ Attest(h)
    \/ \E h \in Hosts, k \in Keys : Fold(h, k) \/ Release(h, k)

Spec == Init /\ [][Next]_vars /\ WF_vars(Next)

-----------------------------------------------------------------------------

(***************************************************************************)
(* InvNoBlindFresh -- R-QUANTIFIER, the safety-critical half, and the       *)
(* MEMBERSHIP property.                                                    *)
(*                                                                          *)
(* No attestation was ever false about a member this host HAD ALREADY READ  *)
(* FROM THE DURABLE STORE. The host held the information; the question is   *)
(* only whether its quantifier used it.                                     *)
(*                                                                          *)
(* WHY THIS IS NOT IMPLIED BY TypeOK. TypeOK says blindFresh is a boolean   *)
(* and says nothing about which transitions set it. The flag is set inside  *)
(* Attest by a predicate over `roster` and `cursor` -- variables the Attest  *)
(* guard does not constrain for members outside Hosted[h]. Whether a        *)
(* reachable state has blindFresh = TRUE therefore depends entirely on      *)
(* whether the guard's DOMAIN covers those members, which is precisely the  *)
(* design decision under test.                                              *)
(*                                                                          *)
(* WHY IT IS REACHABLY VIOLABLE. With LocalQuantifier = TRUE, TLC finds     *)
(* eight states, every one of them an ordinary operation:                   *)
(*                                                                          *)
(*   Commit                       committed = 1                             *)
(*   host 1 claims {kA,kB}                                                  *)
(*   host 2 claims {kA,kB,kC}     kC is now a member of the SUBSCRIPTION,   *)
(*                                at NoCursor, and only host 2 can fold it  *)
(*   host 1 reads back            view[1] = {kA,kB,kC}. HOST 1 SEES kC.     *)
(*   host 1 folds kA, folds kB    its own members reach 1                   *)
(*   host 1 attests               domain = Hosted[1] = {kA,kB}, fully       *)
(*                                folded, so the guard passes; kC is at     *)
(*                                NoCursor                                  *)
(*                             -> blindFresh. A reader routed to host 2 sees *)
(*                                kC, which has folded nothing, and was     *)
(*                                told it need not re-check.                *)
(*                                                                          *)
(* If arm _SR_LocalQuant ever reports no violation, this invariant has      *)
(* stopped being able to fail and every green leaning on it is unsupported. *)
(* Do not repair that by deleting the arm.                                  *)
(***************************************************************************)
InvNoBlindFresh == ~blindFresh

(***************************************************************************)
(* InvRosterStable -- Decision 2, and the CONTENTS property.               *)
(*                                                                          *)
(* A bound member's row is never replaced: the loser of a race reads back   *)
(* the winner's row rather than overwriting it. Under UpsertBinding the     *)
(* roster describes the LAST WRITER, so two hosts with divergent            *)
(* definitions overwrite each other in turn -- it is not wrong once, it is  *)
(* wrong differently on every page, and the roster never settles.           *)
(*                                                                          *)
(* THIS IS THE DISTINCTION AT THE HEART OF THE SEAM, so it is stated here   *)
(* rather than left to a reader to notice: a bound fingerprint is IMMUTABLE *)
(* and rows change only by insert and release. That is a genuine            *)
(* monotonicity property and it is what bounds the exposure in             *)
(* InvNoFalseFresh to one page cycle instead of forever. But it governs the *)
(* CONTENTS OF A ROW and never the MEMBERSHIP OF THE SET -- and membership  *)
(* is what the attestation's quantifier ranges over. THE ARMS MEASURE THAT  *)
(* GAP: _SR_Ships holds InvRosterStable and violates InvNoFalseFresh in the *)
(* same configuration, so stability does not imply honest attestation.      *)
(*                                                                          *)
(* Deliberately silent about a RELEASED member: a release legitimately      *)
(* unbinds and a later bind legitimately writes a new version. That is the  *)
(* point of release, and what makes a definition change remediable rather   *)
(* than a permanent refusal. So this says: the ONLY way a binding changes   *)
(* is a deliberate release.                                                 *)
(***************************************************************************)
InvRosterStable == ~rosterOverwritten

(***************************************************************************)
(* InvSiblingProgress -- the other half of Decision 2.                     *)
(*                                                                          *)
(* Releasing one member never unbinds a sibling and never drops a sibling's *)
(* cursor. A release that cleared the subscription would satisfy every      *)
(* attestation invariant above -- an empty roster has nothing behind it --   *)
(* while destroying every other projection's progress.                     *)
(***************************************************************************)
InvSiblingProgress == ~lostProgress

(***************************************************************************)
(* InvNoFalseFresh -- THE STRONGEST FORM, AND THE SHIPPED SHAPE DOES NOT   *)
(* SATISFY IT. Read this before quoting any pass from this module.         *)
(*                                                                          *)
(* No attestation was ever false about ANY member the durable store held at *)
(* that instant -- whether or not the attesting host had read it.           *)
(*                                                                          *)
(* With AtomicRosterRead = TRUE it holds, because the guard's domain IS the *)
(* durable roster at the attestation instant. That is the unimplementable   *)
(* limit, named so the premise doing the work is visible.                   *)
(*                                                                          *)
(* With AtomicRosterRead = FALSE -- the shipped shape, because the claim    *)
(* (:108) and the read-back (:128) are separate statements in no            *)
(* transaction and the attestation (:218) is a whole page fold after the    *)
(* read (:327) -- TLC finds eight states:                                   *)
(*                                                                          *)
(*   Commit                       committed = 1                             *)
(*   host 1 claims {kA,kB}                                                  *)
(*   host 1 reads back            view[1] = {kA,kB}. kC IS NOT YET BOUND,   *)
(*                                so host 1's snapshot is honest when taken *)
(*   host 2 claims {kA,kB,kC}     kC becomes a member HERE, at NoCursor     *)
(*   host 1 folds kA, folds kB                                              *)
(*   host 1 attests               its domain, view[1], is a subset of what  *)
(*                                it hosts, so the guard passes HONESTLY    *)
(*                             -> falseFresh, over a member at NoCursor     *)
(*                                                                          *)
(* NOT THE SAME DEFECT AS SECTION 2.1, AND THEY MUST NOT BE CONFLATED.     *)
(* 2.1 is PERMANENT blindness: a local quantifier can never see a member it *)
(* does not host, so every attestation that host ever makes is unsound over *)
(* that member. This is a BOUNDED WINDOW, bounded because a bound           *)
(* fingerprint is immutable and rows change only by insert and release, so  *)
(* the next page's read closes it. Both are silent; only one is unbounded.  *)
(* They are the same defect at two magnitudes, which is exactly why no      *)
(* reasoning that excuses this one can be allowed to stand.                 *)
(*                                                                          *)
(* Whether the window is acceptable is a CONTRACT decision, not a           *)
(* modelling one. The honest statement of what the shipped design           *)
(* guarantees is InvNoBlindFresh -- "caught-up means every member bound AT  *)
(* THE TIME OF THIS PAGE'S ROSTER READ had folded through the target" --    *)
(* and not this.                                                            *)
(***************************************************************************)
InvNoFalseFresh == ~falseFresh

(***************************************************************************)
(* InvNoSilentFalseFresh -- the generation-token result, and the most       *)
(* actionable thing in this module.                                         *)
(*                                                                          *)
(* A false attestation is SILENT when nothing at the attestation instant    *)
(* witnessed that its domain was stale. Silence is the discriminator for    *)
(* the catastrophic class, so this invariant asks a different question from *)
(* InvNoFalseFresh: not "was it wrong" but "was it wrong AND undetectable". *)
(*                                                                          *)
(* MEASURED RESULT, with the store-sourced domain and the shipped           *)
(* non-atomic read:                                                         *)
(*                                                                          *)
(*   TokenMode = "none"        -> VIOLATED (arm _SR_NoToken)                *)
(*   TokenMode = "membership"  -> HOLDS    (arm _SR_Token)                  *)
(*                                                                          *)
(* The two arms differ in ONE constant, so the second is non-vacuous        *)
(* because the first fails. What makes it hold: with the store-sourced      *)
(* domain, a false attestation requires a bound member that is behind and   *)
(* that the guard let past, and the guard admits only a domain that is a    *)
(* subset of what this host hosts and fully folded -- so the offending      *)
(* member CANNOT be in view[h]. Therefore view[h] # Bound(roster), which is *)
(* the witness. The staleness is never invisible; it is merely never looked *)
(* at.                                                                      *)
(*                                                                          *)
(* AND THE TOKEN DOES NOT RESCUE THE QUANTIFIER. Arm _SR_TokenLocal runs    *)
(* the membership token against LocalQuantifier = TRUE and VIOLATES: there, *)
(* the offending member IS in view[h], so the snapshot can equal the        *)
(* durable roster exactly while the attestation is false. A token is no     *)
(* substitute for Decision 1 and must not be proposed as one.               *)
(*                                                                          *)
(* WHAT THIS IS NOT. It is an existence result about a WITNESS, not an      *)
(* implemented mechanism -- see LIMITS 6. Observing the witness in          *)
(* production means carrying the snapshot to the consumer and comparing it  *)
(* against the roster the consumer reads when it acts, which relocates a    *)
(* read rather than removing one. Treat this as a lead for the redesign.    *)
(***************************************************************************)
InvNoSilentFalseFresh == ~silentFalseFresh
===============================================================================
