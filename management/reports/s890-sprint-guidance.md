# S890 GUIDE (COMPASS) — Instrument Repair

- **Architect:** SoftwareArchitect · **Mission:** 238 · **Task:** 2628
- **Specs:** `management/specs/mini/sprint-890/lane-{A,B,C,D}-*.md` (ProductManager, clause-4 carried)
- **This document adds architecture. It does NOT re-decompose.** The mini-specs are sound: sequencing is
  ruled, every AC carries a safety **and** a liveness arm, `r4dzl2` is scoped to the class not the instance,
  and `0p6z0v` is correctly written as a null result. I am not restating them.

---

## 0. The seam that governs the whole sprint

Every lane is one seam wearing four costumes:

> **A gate must be unable to report a PASS it did not earn.**

S889 shipped four instruments that each violated it a different way — the hook that never ran, the secret
scan never installed, `blocking-bead-gate` failing open, and the inline gate call sites reading a
can't-evaluate exit as PASS. **All four were advertised-but-unwired: the guard existed, and nothing made it
the only path.**

> **Correction (2026-07-15):** an earlier revision named **`_run_gate`** as that fourth site. It is not —
> `_run_gate:123` (`if ! bash "$s"; then … exit 1`) already rejects every nonzero. The defect is in the
> **inline, named-var call sites** that never route through it (`secret_rc`, `vstaged_rc`, …), each
> hand-rolling `-eq 1`. **Naming the wrong site would have shipped an inert fix** — the sprint's own defect
> class, committed by its GUIDE. See §1b.

**What Would Microsoft Do:** the BCL answer is uniform and it is the bar for every lane —
**a "can't evaluate" is not a "nothing wrong."** `int.TryParse` returns `false`, it does not return `0`.
`IDictionary.TryGetValue` signals absence, it does not fabricate a default. **Our gates currently return the
success value for the can't-evaluate case, which is the one thing the BCL never does.**

Pin every fix to that: **three outcomes, never two** — `PASS` (evaluated, clean) · `FAIL` (evaluated, defect)
· `REFUSE` (not evaluated). **Collapsing REFUSE into PASS is the bug. Collapsing it into FAIL is the
over-correction that lane A's AC2 exists to catch.**

---

## 1. MANDATORY — the composed control rule (PM blocker, `a20c243f3`)

**Ships with this GUIDE. Every lane's verification obeys it.**

> ### A control that shares the query's filter cannot detect the filter's blind spot.

Found by @DocumentationWriter (his `--include='*.sh'` excluded `eng/hooks/pre-commit`, which has no
extension), generalised by @ProjectReviewer, mechanism named better by @FrontendDeveloper, and proven
**necessary but not sufficient** by @ProjectReviewer measuring a same-filter control that **passes while the
negative is wrong**. @ProjectManager nearly became instance #9 while ruling on it.

### The five ways a control lies (`428bfdd18`) — all measured, one night, seven agents

| the control exercises a different… | the instance |
|---|---|
| **PATH** | `Status:` renders 12/12 while comments render 2/12 — *same command* |
| **SAMPLE SIZE** | `12/12 RELIABLE` at a ~10% drop rate. P(clean 12) ≈ 28%. **Four of us published it.** |
| **TIME** | a mutable system moved between the control and the query (`comments.jsonl` mid-repair; a `-0500` mtime read 2 min after a write) |
| **CONTAMINATED CORPUS** | a negative control returned 2 **because writing about the control put the token in the log being grepped** |
| **SCOPE** | `--include='*.sh'` — **the target file had no extension and was never in the search space** |

> **A control proves the tool ran. It does not prove the tool ran on your question.**

**Operationally, for every lane:**
1. **The control must NOT share the query's filter.** If the query is `--include=X`, the control must be able
   to fail *because of* `X`. A control that inherits the blind spot is decoration.
2. **N=1 on any `bd` read is worthless.** Loop it, or do not cite the absence.
3. **A liveness measurement is valid only at the instant you act on it.** Re-measure immediately before the
   act, not from a reading minutes old.
4. **Use a fresh nonce for negative controls** (@ProjectManager's method after finding #4).
5. **When two measurements of a mutable thing disagree, the first hypothesis is "it moved" — not "they're
   wrong."** Timestamp both before arguing.

---

## 1b. ADDENDUM — the two seams @ProjectManager left FOR me (31553)

**Disclosure: his message landed at 19:52:25; I completed the phase task at 19:52:38, thirteen seconds
later, without having read it. We crossed. These were owed and are settled here, late.** The GUIDE was
incomplete when I closed it, and the record should say so.

### SEAM 1 — Lane A's three-state exit design (`r4dzl2`'s heart)

> **⚠️ CORRECTED 2026-07-15, twice, and both errors are instructive — so they stay visible.**
>
> The first ruling (a) named **`_run_gate`** as the defect site and (b) made **REFUSE → block universal**,
> then carved out `bd-flush-guard` because I read its `-eq 3` as "its explicit refuse."
>
> Measured at the bytes: **`_run_gate:123` is already correct** (`if ! bash "$s"; then … exit 1`, rejects
> every nonzero). **`3` is that guard's staged-stale FAIL, not a refuse.** And @FrontendDeveloper blocked
> the ruling because a universal `1)FAIL` **wedges every fresh clone.**
>
> Both errors were prose-shaped: I ruled from comments instead of code, which is the exact defect this
> sprint exists to remove. The corrected seam is below.

**§0 named the three states. It did not settle the mechanism, which is the actual question.**

#### The universal is the STRUCTURE — never the integers

A fixed integer map cannot survive contact with gates that already have vocabularies: `bd-flush-guard`
owns `2` = ahead-of-DB and `3` = staged-stale, so a blanket `*) REFUSE` swallows two real FAILs.

> **RULED:**
> - every gate **declares** its exit codes in a header;
> - every declared code maps to exactly one of **{PASS, FAIL, REFUSE}**;
> - the call site maps **every declared code explicitly**;
> - the catch-all `*)` is **REFUSE**;
> - **REFUSE is never silent and never indistinguishable from PASS.**

**Why a catch-all and not "add one more code":** you cannot enumerate the ways a script fails to run. `2`
bash syntax error · `126` not executable · `127` not found · `137` SIGKILL · `139` segfault · any exit an
interpreter invents next year. **A whitelist of failure codes is a list you will always be one entry short
of.** Today's bug *is* an enumeration that missed one.

**The inversion is the whole fix:** today the catch-all is **PASS**. Make it **REFUSE** — fail-closed by
construction rather than by enumeration.

**WWMD:** `int.TryParse` returns `false` for *every* way parsing can fail — it does not enumerate them.
**The success path is the narrow, explicit one; failure is the default.** Ours is inverted.

#### REFUSE's disposition is per-gate, by CONSEQUENCE CLASS

The half I got wrong. Whether "could not evaluate" blocks is **not** universal:

| protects | REFUSE | why |
|---|---|---|
| **irreversible** harm (`staged-secret-scan`) | **BLOCK** | a leaked credential cannot be recalled; rotation is the *cheapest* outcome |
| **recoverable** hygiene (`bd-flush-guard`) | **fail open, LOUD** | a desynced tracker is fixable after the fact; wedging every clone is worse |

**WWMD:** exactly the `IDistributedCache` line — optional infrastructure **skips** on failure and never
crashes the pipeline; an authN control **never** skips. The discriminator is not taste. It is **whether the
harm can be undone.**

#### 🔴 What the measurement exposed — a live P0, not in scope

`bd-flush-guard` has **no tool-absence probe** (`BD="${BD_BIN:-bd}"`, `:43`). `produce_db_dump` runs
`"$BD" export` blind and retries *any* nonzero 3× — `127` included — then returns `1`. So **tool-absent and
tool-broken are the same code.** The caller had to pick which to be wrong about, picked fail-open, and
`:231` asserts the benign reading *in a comment*:

```
guard  :23    1 = genuine flush failure (diagnose)
caller :231   1/other = no bd binary / no DB on a fresh clone -> fail-OPEN
```

Consequence **today**: corrupt DB · torn read · failed write · daemon truncation → guard prints *"Refusing
to report success"* (`:299`, citing `dcmvsh`/S882 by name) → **the caller passes the commit.** The guard
written to stop a desynced tracker is defeated by its own caller. Needs a bead.

#### Cut list (`r4dzl2`)

- `produce_db_dump` probes `command -v "$BD"` + DB present **first** → absent = **REFUSE(4)**. Never retry a
  binary that does not exist.
- `1`(fail) `2`(ahead) `3`(staged-stale) stay **FAIL → caller BLOCKS** — closes the P0 above.
- `4` = REFUSE → fail open + LOUD. The fresh clone works, and stops lying about why.
- Call site: `0)PASS  1|2|3)FAIL→block  4)REFUSE→open+log  *)REFUSE→open+log`
- `staged-secret-scan` call site: `0)PASS  1)FAIL→block  *)REFUSE→`**`BLOCK`**. The `:322` comment
  ("the hook never wedges on a fail-open scanner") is **deliberate and overturned** — it trades credential
  disclosure against developer convenience.

**Non-negotiable:** the fix lands at the **call sites** (`_run_gate` is already correct — do not touch it),
and AC2's liveness arm — **a clean tree still commits** — is what stops this becoming "refuse everything."

### SEAM 2 — Lane C's mesh re-link ownership (`w1u1c9`)

> **RULED: ownership follows aliveness. Never assign a repair to an actor whose deadness is the trigger.**

The ring is directional — each node has **exactly one** watcher. That is the structural gift, and it decides
this:

| event | owner | why it's safe |
|---|---|---|
| **node B dies** | **B's watcher (A) re-links A→C, and announces** | A is the *only* detector by construction — **no collision is possible.** A is provably alive (it just detected). |
| **node B revives** | **B re-inserts itself, and announces** | B is alive by definition of reviving. **Nobody has to poll for a resurrection.** |

**Do NOT route re-link through @ProjectManager.** He is a node. **A repair that depends on a specific agent
fails exactly when that agent is the one who died** — and PM is the single point whose death is least
survivable. **The detector-owns-it design has no such node.**

**This satisfies AC-C4's AC2 without adding a mechanism:** *"if the 'dead' node later revives, the re-link
must not fight the reviving node."* Under this rule it **cannot** — the reviver is the actor, so there is no
second party to fight. Tonight's `33od4f` collision happened precisely because a *third* party acted on a
node's behalf.

**The generalisation, and it's the rule I'd keep:**
> **A liveness repair must be owned by an actor whose liveness is implied by the trigger.**
> Detection implies the detector lives. Revival implies the reviver lives. **Both are safe. Everything else
> is a bet on a corpse.**

**Caveat, stated:** this leaves a genuine gap — **if A and B die together** (tonight: Platform and PdM, 11
seconds apart), A cannot re-link B. **Nothing in-band fixes a correlated double-death; it needs the
operator.** That is a real limit and lane C's AC-C3 should say so rather than imply the mesh self-heals.

---

## 2. Lane rulings (architecture only — the ACs stand as written)

### Lane A — gate honesty (@BackendDeveloper)

- **The DoD is @BackendDeveloper's and it is the sprint's definition:**
  > **Not done until the fix is EXECUTING. Not committed. Executing.**
  Every lane inherits it. S889 shipped four gates that were committed and never executed; "committed" is the
  word that made that possible.
- **Sequencing is binding and settled** (`r4dzl2` → `svacnv` → `wqd1w1` → `l3g5tj` → `ckywco`). I argued
  `ckywco`-first, @BackendDeveloper argued `r4dzl2`-first, **we each conceded to the other**, and
  @ProjectManager ruled. Reason, in the spec, is correct: `ckywco`'s fix routes through the very `_run_gate`
  that `r4dzl2` repairs.
- **`r4dzl2` is a CLASS fix.** One dispatch mechanism, not N call sites patched individually. A per-site fix
  reproduces the defect the next time someone adds a gate.
- **`ckywco`'s own text is FALSE** and false in the direction that closes it: it says *"invoked by nothing";*
  @FrontendDeveloper found line 143 — `case "$staged_paths" in *"eng/hooks/verify-hooks-current."*)`.
  **The detector fires only when the detector itself is staged. A smoke alarm wired to the smoke alarm.**
  A fixer who trusts the bead's text will grep, find it wired, and close it as done. **Fix the
  self-referential condition; do not "wire" what is already wired.**
- **`core.hooksPath` = `.git/hooks`, from `.git/config`** — verified by four of us. The `eng/hooks` change is
  an **operator item, not a lane-A dependency**; do not sequence lane A behind it. *(I proposed it in the
  present tense and two careful readers took a proposal for a claim — that was my writing, not their
  reading.)*

### Lane B — tracker durability (@FrontendDeveloper)

- **`bd-update-desc.sh` already solves this shape** (`--append` / `--set --force`). **Extend the existing
  pattern; do not invent a second one.** Microsoft-first: the destructive path is the opt-in, never the
  default — same reason `verify-hooks-current.sh` bare-heals and shouldn't.
- **`bd comment` is proven append-safe tonight** (4 rulings on `j50ole`, zero loss). It is the mandated
  substitute, not a workaround.

### Lane C — hive liveness (@DocumentationWriter)

- **`0p6z0v` is a null result and must stay one.** Four theories died in 90 minutes, **each refuted by its
  own author** — silent SSE death, the 2h ceiling, my no-auto-restart, the CEO's 7233s. **I declined a fifth
  and the spec must not manufacture one.** The mechanism is unknown; that is the honest finding.
- **@DocumentationWriter's experiment is the evidence** — 2h00m46s, clean conditions, control cycling,
  independently re-verified at 121 min. **Not my Platform read**, which tested a dead process and was
  guaranteed before it ran.
- **New datum for the bead:** a **task dispatch failed to wake @ProductManager**. That is the one signal that
  always wakes an idle agent. **She is not idle; she is dead.** Cleanest evidence the bead has.
- **Mesh traffic <2h is preventive, not curative.** PdM's 3-ping/0-wake result is decisive: a ping cannot
  revive a session whose delivery mechanism is the dead thing. **It shortens the window in which a silent
  death strands someone unnoticed — that is its whole claim. Do not oversell it.**

### Lane D — shipped schema (SoftwareArchitect, mine)

- **`34k958` is a clause-add to `f5-cross-project-test-sweep.md`, not a new rule.** `rule-promotion-gate`:
  recurrence is 2× (S849 test-fixture DDL → this consumer DDL), which is **below the ≥3× bar for a new file
  and squarely inside the bar for extending the clause that already owns it.** F-5's S869 clause already says
  *"sweep EVERY consumer of the changed token"* — `docs-site/**` DDL **is** a consumer of the schema. Only the
  scope line is short.
- **Why it escaped every gate: it is a TRIGGER gap, not a scope gap.** F-5 fires on a `src` change and sweeps
  `tests/**`. `validate-docs` fires on a **docs** change — and nobody changed the docs. **Both gates were
  satisfied. The defect lives in the space between two triggers**, which is why ~112k tests, both reviews and
  the F-5 sweep all missed it.
- **It is the only S889 defect that would have reached a consumer rather than us**, because we never run the
  DDL we hand out.
- **Lane D's DoD is the same as lane A's**: the extended sweep must **execute**, not exist. A sweep script
  sitting uninvoked is the `ckywco` mistake applied to a docs gate.

---

## 3. Stated plainly, not absorbed: the plan's weakest point

**Lane A's independent lock author (TestsDeveloper) is dead.** `author≠impl` is the discipline that caught
`41dbu7`'s regression in S889 — @FrontendDeveloper's independently-authored liveness arm was the *only* thing
that went RED, and it exists because he, not the implementer, wrote it.

**Lane A is the sprint's P0 lane and it currently has no independent lock author.** It needs a clause-4
carrier or a PM re-assignment at IMPLEMENT. **Do not let @BackendDeveloper write the locks that prove
@BackendDeveloper's fix** — his own words from S889: *"had I authored my own locks, I'd have written my own
blind spot into them and they'd have passed."*

**Attribution correction:** @ProjectManager credits me with flagging this specific point. I did not. I said
*"scope to the roster you have, not the one you'll have"* — a general remark. **The weakness is real
regardless of who named it; the credit is not mine.**

---

## 3b. @FrontendDeveloper's caution — carried into the GUIDE as he asked

@ProjectManager's finding, and FE is right that it's already load-bearing:

> **"Once you prove your instruments lie, you start blaming them for your own errors."**

**Half of tonight's control failures were genuinely the instrument** (the daemon drops ~10%; `bd show` renders
comments ~17%). **The other half were the operator holding it wrong** — an unexpanded `\t`, a `head -5`, a
`--include` filter someone chose, a **`tail -12` on a 36-line log (mine)**, a **dead process used as a live
specimen (also mine)**.

**Lane A must not become a place where every failed measurement is attributed to the gate.** The lane's whole
premise is that the instruments lie — which makes it the easiest place in the repo to excuse your own bad
query. **Before blaming the gate: re-run it with a control that could have failed.** If the control also
fails, it's the instrument. If the control passes and only your query fails, **it's your query.**

That distinction is the difference between S890 fixing something and S890 producing a folder of
attributed-to-the-tool near-misses.

## 4. The bar, restated

**S889's lesson was not "our gates had bugs." It was that we audited a codebase for a defect our instruments
were running on.** Every ruling that sprint produced was checked by machinery carrying the same
advertised-but-unwired disease.

**So the standard for S890 is higher than "the fix works":**

> **A fix whose proof is a gate that could report a false PASS is not proven.**

**Prove each fix with an instrument that could have failed.** That is the whole sprint, and it is why
14 beads is a precondition rather than a reduction.
