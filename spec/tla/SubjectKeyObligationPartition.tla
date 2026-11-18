---------------------- MODULE SubjectKeyObligationPartition ----------------------
(***************************************************************************)
(* SCOPE: read this before citing any result from this model.              *)
(*                                                                         *)
(* WHAT IS MODELLED. One data subject of one owning tenant. A set of       *)
(* OBLIGATION CLASSES -- each aggregate type the subject's fields may be   *)
(* written into, plus one class for fields written outside any aggregate.  *)
(* A mutable retention registry, read by the write path and by each        *)
(* erasure at whatever instant it happens to read it. A key handle         *)
(* produced by a DERIVATION FUNCTION, two derivations being modelled: the  *)
(* one that ships today (widen onto a per-type handle only for a type      *)
(* declared AT WRITE TIME) and the one proposed (widen unconditionally).   *)
(* A write is TWO store round-trips with an abandon between them. And      *)
(* SEVERAL CONCURRENT ERASURE REQUESTS over the one subject, each with its *)
(* own program counter, its own enumeration and its own spare decision.    *)
(*                                                                         *)
(* THE REGISTRY READ AND THE DESTRUCTION ARE TWO ACTIONS, and that is the  *)
(* second correction that matters. An earlier revision made them ONE, so   *)
(* declAtAct equalled decl at the destruction instant BY CONSTRUCTION and  *)
(* every property written against it held by construction too -- the same  *)
(* vacuity trap this model avoids for the handle tuple, one layer down. It *)
(* also hid the rolling-deploy case entirely: ErasureRetentionRegistry     *)
(* copies IOptions into a field in its constructor, so a declaration       *)
(* change is a DEPLOY, and during a rolling deploy two processes hold two  *)
(* frozen registries while neither can detect the other. Split, a          *)
(* declaration can lawfully arrive BETWEEN a request's read and its        *)
(* destruction, and RegistryFence is what makes that detectable.           *)
(*                                                                         *)
(* EVERY RETAINED-DATA PROPERTY IS STATED OVER THE DESTRUCTION, NEVER OVER *)
(* THE SPARE. InvNoRetainedLoss and InvNoSpareDestroyed quantify over      *)
(* spared[r] -- the request's own decision -- so a request that spares     *)
(* NOTHING satisfies both vacuously, which is how a stale instance could   *)
(* destroy legally retained data with every arm green. They are retained   *)
(* for continuity; InvNoRetainedDestruction is the one to cite, and it     *)
(* quantifies over destroyedBy[r] and justifies against declAtDestroy[r].  *)
(*                                                                         *)
(* THE REQUEST SET IS THE CORRECTION THAT MATTERS. An earlier revision of  *)
(* this model had a single erasure program counter, which GRANTED mutual   *)
(* exclusion between requests -- and the implementation does not provide   *)
(* it. Its claim is conditional on one REQUEST's status while the          *)
(* obligation is over the (tenant, subject) pair. With the grant removed,  *)
(* one request's spare is destroyed by another and the first certifies     *)
(* anyway, BECAUSE the other's destruction satisfies its coverage check.   *)
(* SubjectClaim = FALSE reproduces that; the arm set refutes it.           *)
(*                                                                         *)
(* WHAT IS ABSTRACTED AWAY, AND THE MOST IMPORTANT OMISSION IS THE         *)
(* DESTRUCTION'S OWN OUTCOME. Destroy(r) sets every enumerated,             *)
(* non-spared handle's material to "absent" unconditionally: the model has  *)
(* no NotFound, no irreversibly-scheduled state, no revoke failure and no   *)
(* unreadable generation. So in the model "enumerated and not spared"       *)
(* ENTAILS destroyed, and the implementation's does not.                    *)
(*                                                                         *)
(* THE CONSEQUENCE IS A CASE THIS MODEL CANNOT EXHIBIT. A handle whose row  *)
(* existed before the enumeration and whose key was created after the       *)
(* destruction ATTEMPT is enumerated, acted on, and reported by nothing --  *)
(* the real absent branch is an idempotent no-op, correctly, because a      *)
(* handle whose key never existed has no ciphertext an erasure could have   *)
(* made unreadable. That interleaving produced a Completed certificate over *)
(* live key material in a revision of the implementation, and no arm here   *)
(* could have refuted it, because Destroy cannot fail.                      *)
(*                                                                         *)
(* It is covered instead by a unit arm --                                   *)
(* ACertificateAssertsOnlyWhatTheErasureEstablishedShould                    *)
(* .Not_attest_completion_when_a_handle_it_found_empty_is_minted_before_    *)
(* the_attestation -- which is sampling-class and says so. Modelling a      *)
(* partial destruction outcome is OWED and is the next thing this model     *)
(* needs; until it exists, do not read a green arm here as evidence about   *)
(* a destruction that found nothing to destroy.                             *)
(*                                                                         *)
(* An adversarial review predicted that a faithful shipped-coverage arm     *)
(* would VIOLATE here. It would not, and the reason is this omission: with  *)
(* Destroy total, the shipped and corrected coverage sets are               *)
(* indistinguishable in the model. An arm asserting that hold would be a    *)
(* green that cannot fail, which this suite refuses on principle, so the    *)
(* limit is written here instead of planted as an arm.                       *)
(*                                                                         *)
(* WHICH SEAM IT CLAIMS TO CORRESPOND TO.                                  *)
(*                                                                         *)
(* CITED BY MEMBER, NOT BY LINE, AND THAT CHANGE IS THE POINT. Every       *)
(* line number in this block was stale within one hour of being written,   *)
(* TWICE, both times because a commit to the very seam the model is about  *)
(* moved the code -- and the second time the block already carried a note  *)
(* apologising for the first. A SCOPE statement exists so a reader can     *)
(* check the model against the code; a citation that decays on every        *)
(* commit cannot do that, and a wrong one is worse than none because it     *)
(* sends the reader to code that says something else. Member names are     *)
(* stable under edits, so they are the citation; any line number here is   *)
(* a dated hint and nothing more.                                          *)
(*                                                                         *)
(*   SubjectKeyManager.GetOrCreateKeyAsync -- the write-time derivation.   *)
(*     Derive() IS the handle choice there. WidenAlways = FALSE reproduces *)
(*     the SUPERSEDED conditional form; the shipped code is now            *)
(*     unconditional, which is WidenAlways = TRUE.    (hint: .cs:139)      *)
(*   ErasureService.ExecuteAsync, the loop over _retentions.Declared --    *)
(*     the erasure's re-derivation from the CURRENT registry, and the      *)
(*     spare decision.                                (hint: .cs:624)      *)
(*   ErasureService.ExecuteAsync, the subjectKeyId add -- the plain        *)
(*     subject handle destroyed unconditionally.       (hint: .cs:736)     *)
(*   ErasureService.ExecuteAsync, the InProgress claim -- conditional on   *)
(*     Scheduled, and keyed on the REQUEST. SubjectClaim models keying it  *)
(*     on the (tenant, subject) pair instead.                             *)
(*                                                                         *)
(* WHAT THE IMPLEMENTATION NOW DOES THAT THIS MODEL DOES NOT MODEL. Stated *)
(* because the model is otherwise read as covering the shipped behaviour,  *)
(* and as of 2026-11-06 it does not.                                      *)
(*   IKeyMintLedger -- a durable row written AHEAD of every mint, and the  *)
(*     erasure's enumeration of it (ErasureService.ExecuteAsync, the loop  *)
(*     over _mintLedger.ListHandlesAsync; hint: .cs:679). The shipped      *)
(*     destruction set is the UNION of the derived handles this model has  *)
(*     and the recorded handles it does NOT have.                         *)
(*   This model justified UNCONDITIONAL WIDENING and nothing more. It      *)
(*     refutes the conditional form, which is why the write path changed.  *)
(*     It says NOTHING about whether the enumeration is complete, whether  *)
(*     the write-ahead ordering survives a crash, or whether a lost mint   *)
(*     row is reachable -- those are the properties mechanisms 2 and 3     *)
(*     rest on, and they are UNMODELLED. Do not cite a green run here as   *)
(*     evidence for them.                                                  *)
(*   src/Excalibur/Excalibur.Compliance.Abstractions/Erasure/              *)
(*     ErasureRetention -- IsRetainedForAnyTenant, which is                *)
(*     RetainedForAnyTenant() below, and whose stated specification the    *)
(*     _P_HeadAlgo arm refutes. The member is slated for DELETION: the     *)
(*     proposed derivation reads no registry, so it loses its only caller. *)
(*     (This citation read :219-227 until 2026-11-06. The member had moved *)
(*     and nobody re-checked -- a scope statement whose line range is      *)
(*     stale is a model that cannot be checked against the code, which is  *)
(*     the one thing a SCOPE block exists to make possible.)               *)
(*   management/specs/per-subject-key-handle-obligation-partition-spec.md  *)
(*                                                                         *)
(* THE HANDLE IS DELIBERATELY NOT A TUPLE OF ITS INPUTS. It is whatever a  *)
(* derivation function returns, and the write action chooses among         *)
(* derivations. Modelling it as a tuple of tenant, subject and class would *)
(* make InvPartition implied by its own type constraint and checked        *)
(* vacuously, which is the first failure this ladder's own rule names.     *)
(*                                                                         *)
(* WHAT IS ABSTRACTED AWAY, AND THESE ARE LIMITS ON EVERY RESULT BELOW.    *)
(*                                                                         *)
(*   - KEY GENERATIONS. Destruction here takes a handle's material whole.  *)
(*     This is the reason -- the ONLY sound reason -- that writes are      *)
(*     barred once a request has certified: with no generations in the     *)
(*     state space, a post-certification mint under a deterministic handle *)
(*     is INDISTINGUISHABLE from a failure to destroy, so the model cannot *)
(*     represent the difference and must exclude the states. An earlier    *)
(*     revision justified the same guard by the subject's lifecycle, which *)
(*     was a claim about consumers and therefore contradictable by one.    *)
(*     The generation axis is owned by KeyDestructionLedger.tla.           *)
(*   - THE OWNER AXIS. Owner is a constant, so this model never explores a *)
(*     request that recorded the wrong tenant. That axis is owned by       *)
(*     ComplianceErasureOwner.tla.                                        *)
(*                                                                         *)
(*   - !! THE CONJUNCTION OF THOSE THREE MODELS IS CHECKED BY NOTHING.     *)
(*     This model owns the HANDLE axis, KeyDestructionLedger.tla owns the  *)
(*     GENERATION axis, ComplianceErasureOwner.tla owns the OWNER axis,    *)
(*     and the guarantee a consumer relies on is their conjunction. No     *)
(*     artifact establishes it. Do not read one green as the guarantee.    *)
(*                                                                         *)
(*   - THE FENCE, and the atomicity of Certify. Certify is ONE action      *)
(*     here, and the implementation performs a re-read and then a status   *)
(*     write. So this model does NOT establish that the window between     *)
(*     them is closed -- it is not, and the guarantee states it rather     *)
(*     than claiming it. That disclaimer is LOAD-BEARING on the shipping   *)
(*     arm's result, not a footnote beside it.                             *)
(*   - The hasher: one model subject, so hash collision is out of scope.   *)
(*   - Cryptography, field bytes, certificate contents, telemetry.         *)
(*   - Retention PERIODS. Only a declaration's EXISTENCE participates in   *)
(*     any invariant here.                                                 *)
(*                                                                         *)
(* STATUS: written at DESIGN time, before the implementation exists. A     *)
(* model written afterwards restates the code instead of checking it.      *)
(***************************************************************************)
(***************************************************************************)
(* ⚠ BEFORE YOU EDIT AN ACTION: A WRONG `UNCHANGED` ENTRY IS A GREEN ARM,  *)
(* NOT AN ERROR.                                                            *)
(*                                                                         *)
(* A variable both PRIMED and named in `UNCHANGED` does NOT produce         *)
(* "Successor state is not completely specified". TLC silently DISABLES     *)
(* the action, excludes every state it would have reached, and prints       *)
(* "Model checking completed. No error has been found."                    *)
(*                                                                         *)
(* Demonstrated on this model: two arms differing only in a fence constant  *)
(* returned IDENTICAL distinct-state counts, and that coincidence was the   *)
(* only tell. A MISSING entry raises loudly; a CONTRADICTORY one does not.  *)
(*                                                                         *)
(* The control is one line, and it is worth running after every edit:       *)
(*   for every action, primed-variables INTERSECT unchanged-variables = {}  *)
(* and primed UNION unchanged = all variables (20 now, 16 when this header  *)
(* was written). Both were clean then, and both are clean now. This is the  *)
(* model-checking analogue of a gate that cannot fail: the arm is green     *)
(* BECAUSE the behaviour was removed.                                       *)
(*                                                                         *)
(* RUN IT BY HAND. Automating it was attempted and the attempt is recorded  *)
(* here so nobody repeats it: a regex over this file produced a FALSE       *)
(* failure three times, by three different mechanisms -- a BRANCHING action *)
(* legitimately primes a variable in one arm of an IF and leaves it         *)
(* unchanged in another, so a union over the body shows it as both; an      *)
(* UNCHANGED naming a SUB-TUPLE cannot be expanded, so its members read as  *)
(* unconstrained; and a SUB-ACTION composed into a caller (the shape        *)
(* CooperativeProjection uses) primes variables the caller's own text never *)
(* mentions. Narrowed to what it could decide soundly, it judged 9 of 35    *)
(* actions across the models here -- not worth a committed instrument.      *)
(*                                                                         *)
(* What IS worth knowing: the union form can only OVER-report, never under- *)
(* report. So a union-based pass finding ZERO overlaps and ZERO incomplete  *)
(* frames is a SOUND negative, and that is the check that was run across    *)
(* all 15 actions of this module after the destruction was split.           *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Tenants,              \* two only where the tenant-blind write question is live
    Types,                \* aggregate types a field may be written into
    Bot,                  \* the obligation class of a field written outside any aggregate
    Owner,                \* the tenant that owns this subject, and whose declarations spare
    Requests,             \* concurrent erasure requests over THIS subject
    WidenAlways,          \* TRUE: the handle is per-class unconditionally (proposed)
                          \* FALSE: widen only for a type declared at write time (SHIPS TODAY)
    WriteAhead,           \* TRUE: the ledger row is written BEFORE the mint
    EnumerateFromLedger,  \* TRUE: the erasure enumerates recorded handles
                          \* FALSE: it re-derives candidates and sees an arbitrary inventory
    RefuseSpareOfDestroyed, \* TRUE: a request whose spare collides with a destruction
                          \* record cannot certify -- it reports a CONFLICT instead
    SubjectClaim,         \* TRUE: at most one erasure decision in flight per subject
                          \* FALSE: the claim is per-request, as it SHIPS TODAY
    AttestWholeLedger,    \* MECHANISM 8, and the one this model's own removed grant turned out to
                          \* describe. TRUE: the attestation requires every handle the LEDGER holds
                          \* for this subject to be destroyed or deliberately spared. FALSE: only
                          \* the handles this pass ENUMERATED are checked, which is what ships -- so
                          \* a write completing entirely between the enumeration and the attestation
                          \* is in neither set and is certified over.
    TwoStateRow,          \* MECHANISM 7. TRUE: a ledger row records which side of the mint it
                          \* is on, so a write sitting between its row and its key is OBSERVABLE,
                          \* and an erasure that finds one reports a concurrent write instead of
                          \* certifying. FALSE is every version before that mechanism, and it is
                          \* the configuration the write-during-erasure arm refutes.
    AtomicAttestation,    \* TRUE: the attestation re-read and the certificate are ONE action.
                          \* THIS IS A GRANT, and it is a CONSTANT rather than a property of
                          \* Certify's shape for exactly that reason -- a grant built into the
                          \* structure of an action cannot be switched off, and therefore cannot
                          \* be measured. The implementation performs the two as separate awaited
                          \* calls, so FALSE is the faithful setting and TRUE is the one that has
                          \* to justify itself.
    AtomicDestruction,    \* TRUE: the fence check and the destruction of EVERY handle this request
                          \* owns are ONE action. THIS IS A GRANT, and it is a CONSTANT for the same
                          \* reason AtomicAttestation is: a grant built into the shape of an action
                          \* cannot be switched off, and therefore cannot be measured. The
                          \* implementation checks the fence ONCE and then awaits N separate key
                          \* destructions, so FALSE is the faithful setting and TRUE is the one that
                          \* has to justify itself.
    RegistryFence         \* TRUE: the destruction proceeds only if the retention registry
                          \* has not changed since this request read it, and the request
                          \* reports a CONFLICT if it has. This is mechanism 6.
                          \* FALSE: every shipped version AND every earlier revision of this
                          \* design -- the registry is a per-process constant frozen from
                          \* IOptions, so there is no version to compare and a stale
                          \* instance cannot know that it is stale.

ASSUME Owner \in Tenants
ASSUME Bot \notin Types

Classes == Types \cup {Bot}

\* A handle is an opaque value a derivation returns. Two derivations may return the SAME
\* handle for two different obligation classes -- that possibility is the whole subject.
PlainHandle == <<"plain">>
ClassHandle(c) == <<"class", c>>
Handles == {PlainHandle} \cup {ClassHandle(c) : c \in Classes}

VARIABLES
    decl,        \* declared retentions, as tenant-type pairs. MUTABLE, both directions.
    written,     \* class-handle pairs: this class has ciphertext under this handle
    material,    \* handle -> "live" | "absent". NO "destroyed" state: the provider cannot
                 \* tell a key it destroyed from one it never held, and granting it that
                 \* distinction would grant away the ambiguity in question.
    mintLedger,  \* handles carrying a mint row. A ROW IS NOT EVIDENCE OF COVERAGE.
    minted,      \* the subset of mintLedger whose key creation has been CONFIRMED. The
                 \* difference, mintLedger \ minted, is a write sitting between its row and its
                 \* key. Modelled as LEDGER state and deliberately NOT as the writer's program
                 \* counter: a row whose writer crashed stays pending forever while that writer's
                 \* counter has moved on, and the implementation cannot tell those two apart.
                 \* That indistinguishability IS the property under test, so giving the erasure a
                 \* view of the writer's progress would grant away the subject.
    attested,    \* request -> the pending-mint set this request OBSERVED at attestation. Only
                 \* meaningful when AtomicAttestation is FALSE, where the observation and the
                 \* certificate are separate actions and the ledger may move between them.
    destroyed,   \* handles ANY request recorded a destruction for. Durable, accumulating.
    spared,      \* request -> the handles THAT request deliberately spared
    enumerated,  \* request -> the handles THAT request resolved as candidates
    dac,         \* request -> the destruction records that EXISTED when it certified. A
                 \* history variable, and the only way to say "true at its instant": destroyed
                 \* only grows, so an eternal invariant over it indicts a LATER lawful erasure
                 \* after the retention lapsed. Both invariants below were written eternally
                 \* first, and TLC produced exactly that lawful trace as a counterexample.
    declAtAct,   \* request -> the registry as that request read it. A history variable, so
                 \* a spare can be required to be JUSTIFIED rather than merely intended.
    declVersion, \* a monotone counter the registry bumps on EVERY change. THE DECISION'S
                 \* INPUT MUST BE VERSIONED, and this is mechanism 6. Without it no actor can
                 \* tell that the registry moved underneath it -- which is exactly the
                 \* rolling-deploy case, where two processes hold two frozen registries and
                 \* neither can detect the other.
    verAtRead,   \* request -> declVersion as it stood when that request read the registry.
    declAtDestroy, \* request -> the registry AT ITS DESTRUCTION INSTANT. This is what makes
                 \* SAFETY-2' expressible at all: a property over the DESTRUCTION, rather
                 \* than over a spare the request chose and can therefore discharge itself.
    destroyedBy, \* request -> the handles THAT request destroyed. Quantifying over this
                 \* quantifies over the ACTION; quantifying over spared[r] quantifies over
                 \* the request's INTENT, and intent discharges nothing.
    writtenAtDestroy, \* request -> the class/handle pairs that existed AT ITS DESTRUCTION
                 \* INSTANT. Without this, SAFETY-2' can only be justified against the
                 \* handle's SHAPE -- h = ClassHandle(c) -- which is the OUTPUT of the
                 \* derivation mechanism 1 proposes, so the property would be discharged by
                 \* ADOPTING mechanism 1 rather than by the system being safe. Measured: with
                 \* WidenAlways = FALSE the shape form HOLDS over 2,456,411 states while
                 \* legally retained data is destroyed. An eternal reference to `written`
                 \* would not do either: it only grows, so a later write to a destroyed
                 \* handle would indict a destruction that was lawful when it happened.
    wh,          \* class -> the handle its in-flight write chose
    wpc,         \* class -> "idle" | "step1" | "done" | "dead"
    retainedKill, \* <<request, handle>> pairs where the request destroyed that handle while a
                 \* retention covering data written under it was in force AT THAT INSTANT. It exists
                 \* because the per-key split makes "the instant it destroyed it" differ per handle,
                 \* and re-using declAtDestroy for that would check an EARLIER handle against a
                 \* LATER declaration -- a real but DIFFERENT finding reported under this property's
                 \* name. Only the split path can populate it; under AtomicDestruction it stays
                 \* empty by construction, which is what keeps every existing arm's meaning intact.
                 \*
                 \* WHY IT IS AN OBSERVER AND NOT A PARTICIPANT, which is the test to apply to any
                 \* history variable added here: retainedKill APPEARS IN NO ACTION'S ENABLING
                 \* CONDITION. It is written by DestroyOne and read only by the invariant. A variable
                 \* no guard reads cannot constrain behaviour, so the behaviours of this spec with it
                 \* projected out are exactly the behaviours without it -- which is what makes adding
                 \* it sound rather than a change to the system under test. dac, declAtAct,
                 \* declAtDestroy and writtenAtDestroy all pass the same test. Run it before adding
                 \* the next one; "it is only a history variable" is not the argument, the absence
                 \* from every guard is.
                 \*
                 \* AND NOTE THAT TWO HISTORY VARIABLES HERE HAVE OPPOSITE ETERNALITY PROPERTIES,
                 \* which nothing else in this file says. `destroyed` is a STATE set: it grows
                 \* monotonically and describes the world now, so an eternal invariant over it
                 \* indicts a LATER lawful erasure -- the dac comment below records TLC producing
                 \* exactly that trace. retainedKill grows monotonically too, but each entry is a
                 \* RECORD OF AN INSTANT: it asserts something about a past state and nothing about
                 \* the present, so an eternal invariant over it is correct. Those two shapes look
                 \* identical in the VARIABLES block and behave oppositely under an invariant; decide
                 \* which one you have before you write the property.
    epc          \* request -> "idle" | "enumerated" | "acted" | "certified" | "concurrent"

vars == <<dac, decl, written, material, mintLedger, minted, attested, destroyed, spared,
          enumerated, declAtAct, declVersion, verAtRead, declAtDestroy, destroyedBy,
          writtenAtDestroy, wh, wpc, epc, retainedKill>>

\* A bound on the registry counter, so the state space stays finite. It is NOT a design
\* limit: two changes are enough to interleave a declaration on either side of a read,
\* which is the whole case under test.
MaxVersion == 3

(***************************************************************************)
(* The write path's question, tenant-blind, exactly as the shipped         *)
(* IsRetainedForAnyTenant asks it.                                        *)
(***************************************************************************)
RetainedForAnyTenant(c) == c \in Types /\ \E t \in Tenants : <<t, c>> \in decl

(***************************************************************************)
(* THE DERIVATION. WidenAlways = FALSE is the SUPERSEDED conditional form *)
(* of SubjectKeyManager.GetOrCreateKeyAsync; TRUE is the shipped one.      *)
(***************************************************************************)
Derive(c) ==
    IF WidenAlways
    THEN ClassHandle(c)
    ELSE IF RetainedForAnyTenant(c) THEN ClassHandle(c) ELSE PlainHandle

DerivedHandles == {Derive(c) : c \in Classes}

\* A request holds a decision in flight from the moment it enumerates until it reaches a
\* terminal outcome. This is what a per-SUBJECT claim must exclude a second request from.
\* "destroying" and "attested" are both HERE deliberately, and this set's rule is the comment above:
\* active from enumeration until a TERMINAL outcome. A request part-way through its per-key
\* destruction still holds a decision in flight, and so does one that has attested and not yet
\* certified -- Certify requires "attested" when the attestation grant is off, and Concurrent accepts
\* it, so it is not terminal. "attested" was absent until 2026-11-10 while the set's own comment
\* convicted the omission; it was found four lines from the one being fixed, by the reasoning used to
\* fix it.
\*
\* THE RISK OF ADDING A STATE HERE IS DERIVABLE, AND IT IS ONE-DIRECTIONAL. ActiveRequests is read in
\* exactly ONE place -- Enumerate's guard SubjectClaim => ActiveRequests = {} -- so adding a state
\* only ever NARROWS that guard. A narrowing removes behaviours and can never make a safety invariant
\* reachable, so no expected-HOLD arm can break and no SubjectClaim = FALSE arm is affected. What it
\* CAN do is retire a REFUTATION: the only arms whose answer can move are expected-VIOLATE arms in
\* which the new state is reachable, and a green there is a lost refutation wearing a pass.
\*
\* MEASURED for both additions rather than argued. "destroying" is reachable only under
\* ~AtomicDestruction and every arm predating that constant runs with the grant ON, so the set
\* evaluates over identical states there. "attested" is reachable only under ~AtomicAttestation --
\* Attest carries that guard itself -- which is exactly ONE arm in the suite, _P_SplitAttest; it was
\* run before and after and violates InvNoFalseErasure both times, so the refutation survives.
ActiveRequests == {r \in Requests : epc[r] \in {"enumerated", "read", "destroying", "acted", "attested"}}

TypeOK ==
    /\ decl \subseteq (Tenants \X Types)
    /\ written \subseteq (Classes \X Handles)
    /\ material \in [Handles -> {"live", "absent"}]
    /\ mintLedger \subseteq Handles
    /\ destroyed \subseteq Handles
    /\ minted \subseteq mintLedger
    /\ attested \in [Requests -> SUBSET Handles]
    /\ spared \in [Requests -> SUBSET Handles]
    /\ enumerated \in [Requests -> SUBSET Handles]
    /\ dac \in [Requests -> SUBSET Handles]
    /\ declAtAct \in [Requests -> SUBSET (Tenants \X Types)]
    /\ declVersion \in 0..MaxVersion
    /\ verAtRead \in [Requests -> 0..MaxVersion]
    /\ declAtDestroy \in [Requests -> SUBSET (Tenants \X Types)]
    /\ destroyedBy \in [Requests -> SUBSET Handles]
    /\ writtenAtDestroy \in [Requests -> SUBSET (Classes \X Handles)]
    /\ wh \in [Classes -> Handles]
    /\ wpc \in [Classes -> {"idle", "step1", "done", "dead"}]
    /\ retainedKill \subseteq (Requests \X Handles)
    /\ epc \in [Requests -> {"idle", "enumerated", "read", "destroying", "acted", "attested",
                             "certified", "concurrent", "conflict"}]

Init ==
    /\ decl = {}
    /\ written = {}
    /\ material = [h \in Handles |-> "absent"]
    /\ mintLedger = {}
    /\ minted = {}
    /\ attested = [r \in Requests |-> {}]
    /\ destroyed = {}
    /\ spared = [r \in Requests |-> {}]
    /\ enumerated = [r \in Requests |-> {}]
    /\ dac = [r \in Requests |-> {}]
    /\ declAtAct = [r \in Requests |-> {}]
    /\ declVersion = 0
    /\ verAtRead = [r \in Requests |-> 0]
    /\ declAtDestroy = [r \in Requests |-> {}]
    /\ destroyedBy = [r \in Requests |-> {}]
    /\ writtenAtDestroy = [r \in Requests |-> {}]
    /\ wh = [c \in Classes |-> PlainHandle]
    /\ wpc = [c \in Classes |-> "idle"]
    /\ retainedKill = {}
    /\ epc = [r \in Requests |-> "idle"]

(***************************************************************************)
(* The registry mutates in both directions, at any time. A model with a    *)
(* constant declaration set is a model of a different algorithm.           *)
(***************************************************************************)
AddDeclaration ==
    /\ declVersion < MaxVersion
    /\ \E t \in Tenants, c \in Types :
          /\ <<t, c>> \notin decl
          /\ decl' = decl \cup {<<t, c>>}
    /\ declVersion' = declVersion + 1
    /\ UNCHANGED <<dac, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, epc, minted, attested, retainedKill>>

RemoveDeclaration ==
    /\ declVersion < MaxVersion
    /\ \E p \in decl : decl' = decl \ {p}
    /\ declVersion' = declVersion + 1
    /\ UNCHANGED <<dac, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, epc, minted, attested, retainedKill>>

(***************************************************************************)
(* A WRITE IS TWO STORE ROUND-TRIPS. WriteAhead decides which one comes    *)
(* first; Abandon is a crash between them. Modelling them as one action    *)
(* would grant the cross-store atomicity the design does not have.         *)
(***************************************************************************)
Step1(c) ==
    /\ wpc[c] = "idle"
    \* Barred only once some request has CERTIFIED, and the reason is the GENERATION
    \* abstraction in SCOPE, not the subject's lifecycle: with no generations modelled, a
    \* later mint at a deterministic handle cannot be told from a failure to destroy. A
    \* request that reported a CONCURRENT write issued no certificate, so it attests no
    \* instant and must not freeze the subject's writes -- it is deliberately not named here.
    /\ \A r \in Requests : epc[r] # "certified"
    /\ wh' = [wh EXCEPT ![c] = Derive(c)]
    /\ IF WriteAhead
       THEN /\ mintLedger' = mintLedger \cup {Derive(c)}
            /\ material' = material
            /\ written' = written
       ELSE /\ mintLedger' = mintLedger
            /\ material' = [material EXCEPT ![Derive(c)] = "live"]
            /\ written' = written \cup {<<c, Derive(c)>>}
    /\ wpc' = [wpc EXCEPT ![c] = "step1"]
    /\ UNCHANGED <<dac, decl, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, epc, minted, attested, retainedKill>>

\* DELIBERATELY UNGATED, and this is load-bearing. A write that began before a request
\* certified may COMPLETE after it, which is the only interleaving that justifies
\* FullyCovered's mid-flight conjunct. Adding the Step1 guard here would leave the shipping
\* arm green while silently removing the state that makes that conjunct necessary.
Step2(c) ==
    /\ wpc[c] = "step1"
    /\ IF WriteAhead
       THEN /\ mintLedger' = mintLedger
            /\ material' = [material EXCEPT ![wh[c]] = "live"]
            /\ written' = written \cup {<<c, wh[c]>>}
       ELSE /\ mintLedger' = mintLedger \cup {wh[c]}
            /\ material' = material
            /\ written' = written
    \* THE CONFIRMATION, and it belongs to BOTH branches of the write. Under WriteAhead the row
    \* went in at Step1 and this is where its key is confirmed; without WriteAhead the row goes in
    \* here, by which time the key already exists -- so a row never claims more than it can support
    \* on either ordering, and the pending set is empty by construction in the second.
    /\ minted' = minted \cup {wh[c]}
    /\ wpc' = [wpc EXCEPT ![c] = "done"]
    /\ UNCHANGED <<dac, decl, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, epc, attested, retainedKill>>

Abandon(c) ==
    /\ wpc[c] = "step1"
    /\ wpc' = [wpc EXCEPT ![c] = "dead"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, epc, minted, attested, retainedKill>>

(***************************************************************************)
(* The erasure. EnumerateFromLedger = FALSE re-derives candidates and then *)
(* intersects them with an ARBITRARY inventory -- modelled as any subset,  *)
(* because a consumer's data inventory is not known to be complete. That   *)
(* is the objection SubjectKeyManager.GetOrCreateKeyAsync raises against   *)
(* widening                                                                *)
(* unconditionally, and it is why the ledger exists.                       *)
(*                                                                         *)
(* SubjectClaim is the per-subject claim: a second request cannot take a   *)
(* decision while another holds one. FALSE is what ships today.            *)
(***************************************************************************)
Enumerate(r) ==
    /\ epc[r] = "idle"
    /\ SubjectClaim => ActiveRequests = {}
    /\ \/ /\ EnumerateFromLedger
          /\ enumerated' = [enumerated EXCEPT ![r] = mintLedger]
       \/ /\ ~EnumerateFromLedger
          /\ \E inv \in SUBSET DerivedHandles : enumerated' = [enumerated EXCEPT ![r] = inv]
    /\ epc' = [epc EXCEPT ![r] = "enumerated"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, minted, attested, retainedKill>>

\* The registry is read HERE, once, by the erasure, against the tenant its request recorded.
SparedNow(r) == {h \in enumerated[r] : \E c \in Types : <<Owner, c>> \in decl /\ h = ClassHandle(c)}

(***************************************************************************)
(* THE REGISTRY READ AND THE DESTRUCTION ARE TWO ACTIONS, AND THAT SPLIT   *)
(* IS THE WHOLE POINT OF THIS REVISION. As ONE action, declAtAct equalled  *)
(* decl at the destruction instant BY CONSTRUCTION, so every property      *)
(* written against it was true by construction too -- the same vacuity     *)
(* trap this model already avoids for the handle tuple, one layer down.    *)
(* Split, a declaration can lawfully arrive BETWEEN the read and the       *)
(* destruction, which is the rolling-deploy case: two processes holding    *)
(* two frozen registries, neither able to detect the other.                *)
(***************************************************************************)
ReadRegistry(r) ==
    /\ epc[r] = "enumerated"
    /\ spared' = [spared EXCEPT ![r] = SparedNow(r)]
    /\ declAtAct' = [declAtAct EXCEPT ![r] = decl]
    /\ verAtRead' = [verAtRead EXCEPT ![r] = declVersion]
    /\ epc' = [epc EXCEPT ![r] = "read"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, enumerated,
                   declVersion, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, minted, attested, retainedKill>>

(***************************************************************************)
(* MECHANISM 6 -- THE REGISTRY FENCE. The destruction proceeds only if the *)
(* registry has not moved since this request read it. RegistryFence =      *)
(* FALSE is every shipped version AND every earlier revision of this       *)
(* design: the registry is a per-process constant frozen from IOptions, so *)
(* there is no version to compare and a stale instance cannot know it is   *)
(* stale. That arm is expected to violate SAFETY-2'.                       *)
(***************************************************************************)
Destroy(r) ==
    /\ AtomicDestruction
    /\ epc[r] = "read"
    /\ RegistryFence => declVersion = verAtRead[r]
    /\ material' = [h \in Handles |-> IF h \in (enumerated[r] \ spared[r]) THEN "absent" ELSE material[h]]
    /\ destroyed' = destroyed \cup (enumerated[r] \ spared[r])
    /\ destroyedBy' = [destroyedBy EXCEPT ![r] = enumerated[r] \ spared[r]]
    /\ declAtDestroy' = [declAtDestroy EXCEPT ![r] = decl]
    /\ writtenAtDestroy' = [writtenAtDestroy EXCEPT ![r] = written]
    /\ epc' = [epc EXCEPT ![r] = "acted"]
    /\ UNCHANGED <<dac, decl, written, mintLedger, spared, enumerated, declAtAct,
                   declVersion, verAtRead, wh, wpc, minted, attested, retainedKill>>

(***************************************************************************)
(* THE SHIPPED SHAPE -- the fence is checked ONCE and then each key is      *)
(* destroyed in its own awaited call. Three actions, so another request's   *)
(* destruction, a declaration, and a write can all interleave BETWEEN two   *)
(* of this request's key destructions.                                      *)
(*                                                                          *)
(* This is what makes the fence's sufficiency CHECKABLE rather than assumed. *)
(* While the check and the whole destruction are one action, the fence holds *)
(* by construction and no arm can refute it -- which is the vacuity this     *)
(* model avoids everywhere else. The retired-arm note in the runner said so  *)
(* in as many words and the arm did not exist.                              *)
(*                                                                          *)
(* The set still owed is DERIVED rather than stored: it is this request's    *)
(* own enumeration, less what it spared, less what it has already destroyed. *)
(* A stored copy would be a second source of truth for the same fact.        *)
(***************************************************************************)
Owed(r) == (enumerated[r] \ spared[r]) \ destroyedBy[r]

BeginDestroy(r) ==
    /\ ~AtomicDestruction
    /\ epc[r] = "read"
    /\ RegistryFence => declVersion = verAtRead[r]
    /\ epc' = [epc EXCEPT ![r] = "destroying"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, enumerated,
                   declAtAct, declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy,
                   wh, wpc, minted, attested, retainedKill>>

DestroyOne(r, h) ==
    /\ ~AtomicDestruction
    /\ epc[r] = "destroying"
    /\ h \in Owed(r)
    /\ material' = [material EXCEPT ![h] = "absent"]
    /\ destroyed' = destroyed \cup {h}
    /\ destroyedBy' = [destroyedBy EXCEPT ![r] = destroyedBy[r] \cup {h}]
    \* The justification is taken HERE, at this key's own destruction instant, which is the only
    \* instant the guarantee is about. A retention that arrives between two keys is therefore
    \* visible to the second and not retrospectively charged against the first.
    /\ retainedKill' = retainedKill \cup
           (IF \E c \in Types : <<c, h>> \in written /\ <<Owner, c>> \in decl
            THEN {<<r, h>>}
            ELSE {})
    /\ UNCHANGED <<dac, decl, written, mintLedger, spared, enumerated, declAtAct, declVersion,
                   verAtRead, declAtDestroy, writtenAtDestroy, wh, wpc, epc, minted, attested>>

EndDestroy(r) ==
    /\ ~AtomicDestruction
    /\ epc[r] = "destroying"
    /\ Owed(r) = {}
    \* Recorded at the END, so the EXISTING per-request invariant keeps a well-defined value to
    \* justify against. It is deliberately NOT the per-key record: that is retainedKill's job.
    /\ declAtDestroy' = [declAtDestroy EXCEPT ![r] = decl]
    /\ writtenAtDestroy' = [writtenAtDestroy EXCEPT ![r] = written]
    /\ epc' = [epc EXCEPT ![r] = "acted"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, enumerated,
                   declAtAct, declVersion, verAtRead, destroyedBy, wh, wpc, minted, attested,
                   retainedKill>>

(***************************************************************************)
(* +---------------------------------------------------------------------+ *)
(* | THE TWO GRANTS THIS DEFINITION USED TO MAKE ARE GONE. They are      | *)
(* | recorded here rather than deleted silently, because a reader who    | *)
(* | absorbed the old wording needs to recognise what changed, and       | *)
(* | because the SHAPE of the mistake is the reusable part.              | *)
(* |                                                                     | *)
(* | SUPERSEDED GRANT 1 -- CoverageSet(r) == IF EnumerateFromLedger THEN | *)
(* | mintLedger ELSE enumerated[r]. The first branch read the CURRENT    | *)
(* | ledger at the instant of Certify, while the implementation          | *)
(* | quantifies over the handles THIS pass destroyed, which come from    | *)
(* | the enumeration it performed EARLIER                                | *)
(* | (ErasureService.FindReMintedHandlesAsync takes deletedKeyIds as its | *)
(* | domain). So a row inserted after the enumeration was inside the     | *)
(* | model's coverage check and outside the code's. The faithful         | *)
(* | definition turned out to be one token: the model already carried    | *)
(* | the right set as enumerated[r], which Enumerate(r) populates from   | *)
(* | the ledger, so the branch was pure grant and never a simplification.| *)
(* |                                                                     | *)
(* | SUPERSEDED GRANT 2 -- the conjunct \A c \in Classes : wpc[c] #      | *)
(* | "step1", which had no implementation counterpart whatsoever. The    | *)
(* | sentence that accompanied it -- "what the implementation            | *)
(* | approximates with a re-read as its last action" -- was FALSE, and   | *)
(* | is quoted because it is the kind of sentence that makes a model     | *)
(* | look checked.                                                       | *)
(* |                                                                     | *)
(* | AND IT IS NOT REPLACED BY THE SAME PREDICATE IN ANOTHER COSTUME.    | *)
(* | wpc[c] = "step1" is true only while a write is LIVE. A row whose    | *)
(* | writer CRASHED stays pending forever while that writer's counter    | *)
(* | has moved to "dead" -- and the implementation cannot tell those     | *)
(* | apart, because between a row and its mint a write is invisible to   | *)
(* | both observables an erasure has: the ledger set has not changed     | *)
(* | since its read, and the material reads absent because it genuinely  | *)
(* | is not minted yet. An indistinguishability, not a race. So the      | *)
(* | ledger's OWN state is modelled, as `minted`, and the erasure sees   | *)
(* | the pending SET rather than the writer's progress.                  | *)
(* |                                                                     | *)
(* | WHAT THE REMOVAL COST, stated because it is the finding: with both  | *)
(* | grants gone the shipped-design arm VIOLATES InvNoFalseErasure. That | *)
(* | is correct. The design as modelled did not close a write landing    | *)
(* | after the enumeration; mechanism 7 is what closes it, and the arm   | *)
(* | holds again only once TwoStateRow is on. A green arm now means a    | *)
(* | mechanism, not a grant.                                             | *)
(* |                                                                     | *)
(* | THE GRANT THAT REMAINS, and it is a CONSTANT so that it can be      | *)
(* | measured: AtomicAttestation. Certify is ONE action, so the          | *)
(* | attestation re-read and the certificate write cannot be separated   | *)
(* | by anything -- while the implementation performs them as two        | *)
(* | awaited calls. Set it FALSE and the hole reopens: that is the       | *)
(* | _P_SplitAttest arm, and it is a reachable violation of the shipped  | *)
(* | code, not a hypothetical.                                           | *)
(* +---------------------------------------------------------------------+ *)
(*                                                                         *)
(* A FULL certificate requires that every handle in the set this design    *)
(* actually holds is either gone or deliberately spared, AND that no write *)
(* is mid-flight.                                                          *)
(*                                                                         *)
(* The first conjunct quantifies over the set the design HAS rather than   *)
(* being switched off for a design that lacks a ledger. Each variant then  *)
(* performs the strongest check available to it, and the outcomes differ   *)
(* because the SETS differ -- which is the actual subject -- instead of    *)
(* one arm being granted an exemption and inviting the strawman charge.    *)
(***************************************************************************)
\* THE SET THIS REQUEST ENUMERATED, on every setting of EnumerateFromLedger. Enumerate(r) already
\* populates it from the ledger when that flag is on, so this is faithful AND simpler than the branch
\* it replaces: the arms still differ, because the SETS they enumerate differ, which is the actual
\* subject -- rather than one arm being handed a re-read the code never performs.
CoverageSet(r) == IF AttestWholeLedger THEN enumerated[r] \cup mintLedger ELSE enumerated[r]

(***************************************************************************)
(* AND THIS IS WHERE THE REMOVED GRANT CAME BACK AS A MECHANISM, which is  *)
(* the most useful thing this model has produced and is worth stating      *)
(* plainly. Grant 1 was `CoverageSet(r) == mintLedger` -- the ledger as it *)
(* stands at the attestation. It was removed as unfaithful, correctly: the *)
(* implementation quantifies over the handles THIS pass destroyed, taken   *)
(* from an enumeration it performed earlier. With it gone, both HOLD arms  *)
(* went RED on InvNoFalseErasure.                                          *)
(*                                                                         *)
(* THE RED WAS NOT A MODELLING ERROR. A write whose row AND mint both land *)
(* between the enumeration and the attestation is in no set the            *)
(* implementation looks at: not in the enumerated set, so the coverage     *)
(* check never reaches it; not in the destroyed set, so the re-mint probe  *)
(* never reaches it; and not PENDING, because its mint completed, so the   *)
(* two-state row never reaches it either. The certificate is signed over   *)
(* live material, and nothing anywhere raises.                             *)
(*                                                                         *)
(* So the grant was describing the CORRECT design and the code implements  *)
(* a weaker one. It returns as a CONSTANT rather than as an assumption,    *)
(* which is the difference that matters: FALSE is what ships and has its   *)
(* own refuting arm, TRUE is the design the arms now hold under, and the   *)
(* model says which is which instead of quietly assuming the stronger.     *)
(*                                                                         *)
(* NOTE ON WHAT MECHANISM 8 DOES NOT SUBSUME. A PENDING row is in the      *)
(* ledger and its material probes absent, so the conjunct below is         *)
(* satisfied by it -- and the key may come into existence a moment later.  *)
(* Mechanisms 7 and 8 therefore close different halves and neither         *)
(* replaces the other: 7 catches a write MID-FLIGHT at the attestation, 8  *)
(* catches one that COMPLETED since the enumeration. The arms separate     *)
(* them one mechanism at a time, which is the only way to know that.       *)
(***************************************************************************)

\* A write between its row and its key, as the LEDGER can see it. Not as the writer's counter can:
\* an abandoned write leaves a row here forever, and that is the state the implementation cannot
\* distinguish from a live one. Both must therefore block a certificate.
PendingMint == mintLedger \ minted

\* What a request observed at attestation. Under the atomicity grant it observes the pending set in
\* the same action that signs; without it, what it observed is a separate variable and the ledger may
\* have moved since.
ObservedPending(r) == IF AtomicAttestation THEN PendingMint ELSE attested[r]

FullyCovered(r) ==
    /\ \A h \in CoverageSet(r) : material[h] = "absent" \/ h \in spared[r]
    /\ TwoStateRow => ObservedPending(r) = {}

(***************************************************************************)
(* The terminal outcome for a request that cannot certify: a spare that    *)
(* collided with a destruction record, or -- new in this revision -- a     *)
(* registry that moved while the request held a decision. Reachable from   *)
(* "read" as well as "acted", because a fenced request that finds the      *)
(* registry moved must have somewhere to go or the fence costs liveness    *)
(* with no outcome to report.                                             *)
(***************************************************************************)
Conflict(r) ==
    /\ epc[r] \in {"read", "acted"}
    /\ epc' = [epc EXCEPT ![r] = "conflict"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, minted, attested, retainedKill>>

(***************************************************************************)
(* THE ATTESTATION RE-READ AS ITS OWN ACTION, reachable only when the      *)
(* atomicity grant is off. The implementation reads the ledger again and   *)
(* THEN writes the request's outcome -- two awaited calls -- so a write    *)
(* landing between them is observed by neither. Modelling them as one      *)
(* action grants an atomicity no store provides across two statements.     *)
(***************************************************************************)
Attest(r) ==
    /\ ~AtomicAttestation
    /\ epc[r] = "acted"
    /\ attested' = [attested EXCEPT ![r] = PendingMint]
    /\ epc' = [epc EXCEPT ![r] = "attested"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, minted, destroyed, spared, enumerated,
                   declAtAct, declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy,
                   wh, wpc, retainedKill>>

Certify(r) ==
    /\ IF AtomicAttestation THEN epc[r] = "acted" ELSE epc[r] = "attested"
    /\ FullyCovered(r)
    \* A spare that collides with a destruction record is a retention declared AFTER the
    \* record was already destroyed. It is not certifiable and it is not retryable: the
    \* data is gone and the obligation arrived too late. It needs its OWN terminal
    \* outcome, or a silent safety violation is exchanged for a silent liveness one.
    /\ RefuseSpareOfDestroyed => (spared[r] \cap destroyed = {})
    /\ dac' = [dac EXCEPT ![r] = destroyed]
    /\ epc' = [epc EXCEPT ![r] = "certified"]
    /\ UNCHANGED <<decl, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, minted, attested, retainedKill>>

\* REACHABLE FROM "attested" AS WELL. A request that observed a pending mint must not certify, and
\* it must still have a terminal outcome -- the implementation records a completion-except-concurrent-
\* writes rather than failing. Without this arm the mechanism would buy safety with a liveness hole,
\* and an abandoned write would strand the subject with no reportable outcome at all.
Concurrent(r) ==
    /\ epc[r] \in {"acted", "attested"}
    /\ epc' = [epc EXCEPT ![r] = "concurrent"]
    /\ UNCHANGED <<dac, decl, written, material, mintLedger, destroyed, spared, enumerated, declAtAct,
                   declVersion, verAtRead, declAtDestroy, destroyedBy, writtenAtDestroy, wh, wpc, minted, attested, retainedKill>>

(***************************************************************************)
(* No Done action. Stuttering is already permitted by the box-action       *)
(* formula, so a Done disjunct adds no behaviour -- what it adds is that   *)
(* Next is enabled in every terminal state, which permanently satisfies    *)
(* TLC's deadlock check and discards the one free control it offers.       *)
(***************************************************************************)
Next ==
    \/ AddDeclaration
    \/ RemoveDeclaration
    \/ \E c \in Classes : Step1(c) \/ Step2(c) \/ Abandon(c)
    \/ \E r \in Requests : Enumerate(r) \/ ReadRegistry(r) \/ Destroy(r)
                              \/ BeginDestroy(r) \/ EndDestroy(r)
                              \/ Attest(r) \/ Certify(r) \/ Concurrent(r) \/ Conflict(r)
    \/ \E r \in Requests : \E h \in Handles : DestroyOne(r, h)

Spec == Init /\ [][Next]_vars

HoldsData(h) == \E c \in Classes : <<c, h>> \in written

(***************************************************************************)
(* SAFETY-0 -- THE PARTITION. A handle determines the obligation class of  *)
(* everything encrypted under it. This is what makes the safety properties *)
(* below co-satisfiable, and it is stated nowhere in the code today.       *)
(* WidenAlways = FALSE violates it with no concurrency at all.             *)
(***************************************************************************)
InvPartition == \A h \in Handles : Cardinality({c \in Classes : <<c, h>> \in written}) <= 1

(***************************************************************************)
(* SAFETY-1 -- NO FALSE ERASURE, and the spare must be JUSTIFIED. Where a  *)
(* certificate exists, every handle still holding this subject's data has  *)
(* no key, unless that request spared it AND a declaration it actually     *)
(* read covers it. Binding to the spare alone would be self-certifying:    *)
(* the request's own intent would discharge the property, so a variant     *)
(* that spares the plain handle "to be safe" would pass unchanged.         *)
(***************************************************************************)
InvNoFalseErasure ==
    \A r \in Requests :
        (epc[r] = "certified") =>
            \A h \in Handles :
                (HoldsData(h) /\ material[h] = "live") =>
                    /\ h \in spared[r]
                    /\ \E c \in Types : h = ClassHandle(c) /\ <<Owner, c>> \in declAtAct[r]

(***************************************************************************)
(* SAFETY-2 -- NO ERASURE OF RETAINED DATA. A spared handle that holds     *)
(* data is still readable: a key exists at it and no destruction is on     *)
(* record. Scoped to the key-EXISTENCE half deliberately -- readability is *)
(* a conjunction over the key and the ciphertext, the generation half is   *)
(* owned by KeyDestructionLedger.tla, and the conjunction by nothing. A    *)
(* spared handle holding NO data has no retained data to lose, so the      *)
(* guard is HoldsData rather than membership alone.                        *)
(***************************************************************************)
InvNoRetainedLoss ==
    \A r \in Requests :
        (epc[r] = "certified") =>   \* what a CERTIFICATE claims, not what a decision intends:
            \* at "acted" the outcome is undecided, and refusing to certify is the designed
            \* answer to a spare that collided with a destruction record. Scoping this to
            \* "acted" demands the DECISION never collide, which no erasure can know in
            \* advance; scoping it eternally forbids a LATER lawful erasure after the
            \* retention lapses, which is correct behaviour. Both were tried and both were
            \* wrong, in opposite directions.
            \A h \in spared[r] : HoldsData(h) => h \notin dac[r]

(***************************************************************************)
(* SAFETY-3 -- ENUMERATION COMPLETENESS. Every handle holding key material *)
(* has a ledger row. This is the premise SAFETY-1 rests on once a request  *)
(* enumerates recorded handles, and it is what forces the row to be        *)
(* written AHEAD of the mint: the converse is not required, because a row  *)
(* with no key is benign.                                                  *)
(***************************************************************************)
InvEnumComplete == \A h \in Handles : material[h] = "live" => h \in mintLedger

(***************************************************************************)
(* SAFETY-2' -- NO DESTRUCTION OF RETAINED DATA, STATED OVER THE           *)
(* DESTRUCTION RATHER THAN OVER THE SPARE. This is the invariant the model  *)
(* was missing, and its absence is why every arm could be green while a    *)
(* stale instance destroyed legally retained data.                          *)
(*                                                                         *)
(* SAFETY-2 above quantifies over spared[r] -- the request's OWN decision  *)
(* -- so a request that spares NOTHING satisfies it VACUOUSLY. This model  *)
(* already forbids exactly that reasoning for SAFETY-1 ("binding to the    *)
(* spare alone would be self-certifying") and then committed it on the     *)
(* destroy side. A property conditioned on the actor's own intent is       *)
(* satisfied by the actor's own intent, and is therefore not a property of *)
(* the system.                                                             *)
(*                                                                         *)
(* Here the quantifier is over destroyedBy[r] -- what the request DID --   *)
(* and the justification is declAtDestroy[r], the registry at the instant  *)
(* it did it. No intent appears anywhere in the statement.                 *)
(***************************************************************************)
InvNoRetainedDestruction ==
    \A r \in Requests :
        \A h \in destroyedBy[r] :
            ~(\E c \in Types :
                \* THE DATA, not the handle's SHAPE. `h = ClassHandle(c)` was the output of the
                \* derivation mechanism 1 proposes, so the property was discharged by ADOPTING
                \* mechanism 1 rather than by the system being safe -- measured, the shape form
                \* HOLDS over 2,456,411 states with WidenAlways = FALSE while a declared class's
                \* data sitting under the plain handle is destroyed. This asks the question the
                \* guarantee actually makes: was data of a RETAINED class written under the key
                \* this request destroyed, at the instant it destroyed it.
                /\ <<c, h>> \in writtenAtDestroy[r]
                /\ <<Owner, c>> \in declAtDestroy[r])

(***************************************************************************)
(* SAFETY-3b -- NO RETAINED DESTRUCTION, PER KEY. The sibling above states  *)
(* the property over a request's whole destruction, against one declaration *)
(* set. Once the destruction is split per key, "the instant it destroyed    *)
(* it" differs per handle, and this is the form that survives the split:    *)
(* for every key any request destroyed, no retention covering data written  *)
(* under that key was in force at the moment it went.                       *)
(*                                                                          *)
(* Under AtomicDestruction it is EMPTY BY CONSTRUCTION and proves nothing,  *)
(* which is why it is stated here rather than relied on: cite it only from  *)
(* an arm that turns the grant off.                                         *)
(***************************************************************************)
InvNoRetainedDestructionPerKey == retainedKill = {}

(***************************************************************************)
(* SAFETY-4 -- NO SPARE OVER A DESTROYED HANDLE. A handle any request      *)
(* spared is a handle no request destroyed. This is the mirror of the      *)
(* cross-request disagreement and is unreachable with a per-subject claim. *)
(***************************************************************************)
InvNoSpareDestroyed == \A r \in Requests : (epc[r] = "certified") => \A h \in spared[r] : h \notin dac[r]

(***************************************************************************)
(* THE WITNESS -- required to be VIOLATED, in its own arm. Every safety    *)
(* property above is satisfied by a design that always reports a           *)
(* concurrent write and erases nothing: safe, useless, and indistinguish-  *)
(* able from a correct one in an all-green arm set. A violation of this is *)
(* a TRACE proving a useful certificate is reachable -- a subject that     *)
(* HAD data, certified, with a retained class spared and still readable.   *)
(***************************************************************************)
InvNeverUsefulCertify ==
    ~(\E r \in Requests :
        /\ epc[r] = "certified"
        /\ \E h \in spared[r] : HoldsData(h) /\ material[h] = "live")

=============================================================================
