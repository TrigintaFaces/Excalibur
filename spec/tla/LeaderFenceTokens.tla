--------------------------- MODULE LeaderFenceTokens ---------------------------
EXTENDS Naturals, TLC

CONSTANTS Candidates,          \* the contending processes
          MaxTenures,          \* bound on acquisitions, to keep the space finite
          SharedMark,          \* TRUE: one high-water mark at the arbiter. FALSE: one per process.
          ZeroIsToken,         \* TRUE: a mark of 0 counts as A VALUE rather than UNSET.
          MarkCanRewind,       \* TRUE: an unreadable mark is rendered as 0, and a mint may follow it.
          UnsetReportsStart    \* TRUE: a never-minted mark reports the sequence START value, not UNSET.

(*
SCOPE:

WHAT IS MODELLED. One fenced resource. A bounded set of candidates acquiring and
relinquishing a leadership tenure at a single arbiter, each capturing one fencing
token at the moment it acquires and pinning that token for the whole tenure (the
capture-pin contract the provider's remarks instruct). A fenced write is admitted
only when the provider's validate answers true, where validate is
  (the mark is SET) AND (the presented token >= the observed mark),
which is the relation this subsystem ships and the one ruled correct.

Correspondence to the implementation seam. The validate relation is
  src/Dispatch/Excalibur.Dispatch.LeaderElection.Abstractions/Fencing/IFencingTokenProvider.cs
  (ValidateTokenAsync), as implemented identically by all seven providers,
and the admitting consumer is
  src/Dispatch/Excalibur.Dispatch.LeaderElection.Abstractions/Fencing/FencingTokenMiddleware.cs:76.

THE FOUR DEFECT SWITCHES ARE REGRESSION LOCKS, NOT DESCRIPTIONS OF CURRENT CODE.
Each reproduces a defect that WAS shipped and is now FIXED at a named commit. Read
them as "this must never come back", never as "the provider does this" -- a model
whose correspondence claim has gone stale is worse than no model, because it is
defended. Each row names the fix, so the claim is checkable rather than inherited:

  MarkCanRewind     WAS ConsulFencingTokenProvider.ParseValue rendering an
                    unreadable payload as 0, after which the mint advanced from 0
                    through a CAS that succeeded.
                    FIXED b033d0d93b -- ParseValue now throws InvalidDataException
                    on a present-but-empty and a present-but-unparseable payload.
  ZeroIsToken       WAS KubernetesFencingTokenProvider reporting a Lease at
                    leaseTransitions 0 as a mark rather than as UNSET.
                    FIXED 891157009d -- `transitions > 0 ? transitions : null`.
                    (That SHA's MESSAGE is about build coordination and says nothing
                    about fencing: the author's staged work was swept into an
                    unrelated commit of mine. `git log -S` finds the real commit
                    whatever the message claims, which is why the citation here is
                    the one the code is in rather than the one that reads right.)
  UnsetReportsStart WAS SqlServerFencingTokenProvider reading
                    sys.sequences.current_value, which is NOT NULL and reports
                    START WITH for a never-drawn sequence.
                    FIXED eb397b6274 -- reads last_used_value, which is nullable.
  SharedMark FALSE  InMemoryFencingTokenProvider, and this one is NOT a defect and
                    is NOT fixed: a per-process counter is correct for its stated
                    single-process scope (ARCHITECTURE.md). It is modelled because
                    it satisfies every per-process clause while fencing nothing
                    across processes, and the interface cannot distinguish it from
                    a shared-mark provider -- which is why the contract must say
                    H(r) is SHARED.

Raised by the lane that fixed the Consul defect, who declined to read this file
and reasoned from the arm filenames instead: if a SCOPE statement pins a provider
at a pre-fix SHA, the model is checking a defect that no longer exists, and that
is cheap to catch now and expensive to discover after the model is cited as a rung.

WHAT IS ABSTRACTED AWAY. Time, leases, heartbeats and grace periods: a tenure
ends by a nondeterministic Relinquish rather than by expiry, which is strictly
more permissive than any timing discipline and so cannot hide a violation. The
mint is one atomic action, because each provider's substrate supplies an atomic
step (INCR, nextval, findAndModify, a CAS, an Interlocked); this model therefore
does NOT check that those steps are atomic, only what follows if they are.
Multiple resources, tenancy, token exhaustion, network partition, replica lag on
the mark read, and the resource's own durability are all out of scope.

WHAT A PASS DOES NOT ESTABLISH. That the providers implement validate as written,
that the mint really is atomic in each substrate, or that a consumer pins its
token correctly. Those are code properties and this is a protocol model.
*)

Unset == 0
Tokens == 0 .. (MaxTenures + 1)
\* A string rather than an unbounded CHOOSE: TLC cannot evaluate the latter, and the
\* candidates are model values, so no string can collide with one.
NoHolder == "none"

VARIABLES arbiter,      \* the shared high-water mark; Unset until a first mint
          own,          \* per-candidate mark, used only when SharedMark is FALSE
          held,         \* the candidate holding the tenure, or NoHolder
          tenure,       \* count of acquisitions so far; the true tenure identity
          pinTok,       \* the token each candidate captured at its acquisition
          pinTen,       \* the tenure at which it captured
          admitTen,     \* tenure of the most recently admitted fenced write
          regressed,    \* set TRUE if an admitted write came from a superseded tenure
          issuedAny,    \* has ANY token ever been minted for this resource
          admittedAny   \* has ANY fenced write ever been admitted

vars == <<arbiter, own, held, tenure, pinTok, pinTen, admitTen, regressed,
          issuedAny, admittedAny>>

Init ==
    /\ arbiter = Unset
    /\ own = [c \in Candidates |-> Unset]
    /\ held = NoHolder
    /\ tenure = 0
    /\ pinTok = [c \in Candidates |-> Unset]
    /\ pinTen = [c \in Candidates |-> 0]
    /\ admitTen = 0
    /\ regressed = FALSE
    /\ issuedAny = FALSE
    /\ admittedAny = FALSE

\* What c's provider REPORTS when asked for the mark. This is the only place the
\* defect switches enter: each one makes the report differ from the truth.
RawMark(c) == IF SharedMark THEN arbiter ELSE own[c]
Observed(c) ==
    IF UnsetReportsStart /\ RawMark(c) = Unset
    THEN 1                      \* a never-drawn sequence reporting its START value
    ELSE RawMark(c)

\* The provider's own test for "a token was ever issued".
MarkIsSet(c) == IF ZeroIsToken THEN TRUE ELSE Observed(c) > 0

\* ValidateTokenAsync, as all seven providers implement it.
ValidateOKWith(c, t) == MarkIsSet(c) /\ t >= Observed(c)
ValidateOK(c) == ValidateOKWith(c, pinTok[c])

\* ---------------------------------------------------------------------------
\* Actions
\* ---------------------------------------------------------------------------

\* An acquisition is ONE atomic action at the arbiter: it advances the mark and
\* hands the new value to the acquiring candidate, which pins it for the tenure.
Acquire(c) ==
    /\ tenure < MaxTenures
    /\ held = NoHolder
    /\ LET next == (IF SharedMark THEN arbiter ELSE own[c]) + 1 IN
        /\ next \in Tokens
        /\ arbiter' = IF SharedMark THEN next ELSE arbiter
        /\ own' = IF SharedMark THEN own ELSE [own EXCEPT ![c] = next]
        /\ pinTok' = [pinTok EXCEPT ![c] = next]
    /\ held' = c
    /\ tenure' = tenure + 1
    /\ pinTen' = [pinTen EXCEPT ![c] = tenure + 1]
    /\ issuedAny' = TRUE
    /\ UNCHANGED <<admitTen, regressed, admittedAny>>

\* A tenure ends. The candidate KEEPS its pinned token, which is the whole point:
\* a stale leader that has not detected its loss still holds a real token.
Relinquish(c) ==
    /\ held = c
    /\ held' = NoHolder
    /\ UNCHANGED <<arbiter, own, tenure, pinTok, pinTen, admitTen, regressed,
                   issuedAny, admittedAny>>

\* An unreadable mark rendered as a value. Only reachable under MarkCanRewind.
Corrupt ==
    /\ MarkCanRewind
    /\ arbiter # Unset
    /\ arbiter' = Unset
    /\ UNCHANGED <<own, held, tenure, pinTok, pinTen, admitTen, regressed,
                   issuedAny, admittedAny>>

\* A fenced write. Any candidate holding a pinned token may attempt one, whether
\* or not it still holds the tenure -- that is exactly the stale-leader case.
Write(c) ==
    /\ pinTen[c] > 0
    /\ ValidateOK(c)
    /\ regressed' = (regressed \/ pinTen[c] < admitTen)
    /\ admitTen' = IF pinTen[c] > admitTen THEN pinTen[c] ELSE admitTen
    /\ admittedAny' = TRUE
    /\ UNCHANGED <<arbiter, own, held, tenure, pinTok, pinTen, issuedAny>>

(***************************************************************************)
(* A WRITE PRESENTING A TOKEN THE PROVIDER NEVER ISSUED.                    *)
(*                                                                          *)
(* Three shipped defects are reachable ONLY through this action, and the     *)
(* first version of this model had no such action -- so two of its four      *)
(* defect arms could not fail, which is the "generator cannot produce the    *)
(* failing shape" defect this project treats as discharging nothing. The     *)
(* writer here is not a candidate and holds no tenure: it is a caller that   *)
(* supplies a token value, which is exactly what FencingTokenMiddleware does *)
(* when it reads the token out of message context (and yields 0 when the     *)
(* item is absent or wrongly typed).                                        *)
(***************************************************************************)
Forge(c, t) ==
    /\ ValidateOKWith(c, t)
    /\ admittedAny' = TRUE
    /\ UNCHANGED <<arbiter, own, held, tenure, pinTok, pinTen, admitTen,
                   regressed, issuedAny>>

Next ==
    \/ \E c \in Candidates : Acquire(c)
    \/ \E c \in Candidates : Relinquish(c)
    \/ \E c \in Candidates : Write(c)
    \/ \E c \in Candidates, t \in Tokens : Forge(c, t)
    \/ Corrupt

Spec == Init /\ [][Next]_vars

\* ---------------------------------------------------------------------------
\* Properties
\* ---------------------------------------------------------------------------

TypeOK ==
    /\ arbiter \in Tokens
    /\ own \in [Candidates -> Tokens]
    /\ held \in Candidates \cup {NoHolder}
    /\ tenure \in 0 .. MaxTenures
    /\ pinTok \in [Candidates -> Tokens]
    /\ pinTen \in [Candidates -> 0 .. MaxTenures]
    /\ admitTen \in 0 .. MaxTenures
    /\ regressed \in BOOLEAN
    /\ issuedAny \in BOOLEAN
    /\ admittedAny \in BOOLEAN

(***************************************************************************)
(* THE SAFETY PROPERTY A FENCE EXISTS FOR.                                 *)
(*                                                                          *)
(* Once a write from some tenure has been admitted, no write from an EARLIER *)
(* tenure may ever be admitted again. That is what "a superseded leader is   *)
(* rejected" means operationally: not that the stale process is stopped, but *)
(* that its writes stop landing once a successor's have.                     *)
(*                                                                          *)
(* This is the invariant each defect switch violates, which is what makes    *)
(* the arms non-vacuous: four independently shipped defects, in four          *)
(* substrates, all caught by one property.                                   *)
(***************************************************************************)
InvNoSupersededWrite == ~regressed

(***************************************************************************)
(* A weaker companion, stated separately because it localises the cause.     *)
(* No two distinct candidates may ever hold the SAME pinned token from       *)
(* tenures that both began. Under a shared, monotone mark this is immediate; *)
(* under a per-process counter it fails at once, and the failure explains    *)
(* why the first invariant then fails too.                                   *)
(***************************************************************************)
InvTokensUniquePerTenure ==
    \A c1, c2 \in Candidates :
        (c1 # c2 /\ pinTen[c1] > 0 /\ pinTen[c2] > 0) => pinTok[c1] # pinTok[c2]

(***************************************************************************)
(* THE UNSET STATE MUST REFUSE EVERYTHING.                                  *)
(*                                                                          *)
(* Before any token has been issued for a resource, NO write may be admitted *)
(* -- not a plausible-looking small value, not zero, not anything. This is   *)
(* the property three shipped providers violated, each by rendering "nobody  *)
(* ever minted here" as a low real-looking mark, after which the at-or-above *)
(* relation admits every token at or above it.                               *)
(*                                                                          *)
(* It is stated SEPARATELY from InvNoSupersededWrite on purpose. A fence     *)
(* that admits a token ABOVE its mark is CORRECT under the relation this     *)
(* subsystem ships (a caller who fabricates a value above the counter has    *)
(* left the scheme, which no relation can defend against), so "no unissued   *)
(* token is ever admitted" would be false for the shipped code BY DESIGN.    *)
(* The honest property is narrower and is exactly the defect: the UNSET      *)
(* state must be refusing, not merely low.                                   *)
(***************************************************************************)
InvUnsetRefusesEverything == admittedAny => issuedAny

================================================================================
