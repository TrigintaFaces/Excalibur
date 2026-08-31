# S896 — Test-Methodology Reasoning Trace

**Author:** TestsDeveloper · **Date:** 2026-07-22

Written because the night's *decisions* are durable (beads, commits) while its *reasoning* was not: the
discovery logs under `.claude/shared/` are gitignored by design and vanish on a clean checkout. These are
the testing lessons that would otherwise have evaporated. Each one cost a real error.

---

## 1. A positive control must exercise the SAME QUERY as the target, not merely the same subject

I claimed *"no lock forces the owner-write to fail"* and cited a control of `grep -c "owner" → 11`.

**My actual query was a multi-branch regex. My control tested a different query entirely.** Proving that
`grep -c "owner"` works says nothing about whether *that regex* could fire. The control was incapable of
catching my error — structurally the same defect a colleague retracted the same night.

**Correct form:** plant a known match, run the *actual* instrument against it, confirm it fires, then run
it against the real target.

```
planted fixture  ->  regex fires (2)     the instrument itself works
real harness     ->  2 matches, inspected and judged non-qualifying
```

The conclusion happened to survive. The grounding did not, until it was redone.

---

## 2. Grepping a test tree for an implementation's internal symbol is ANTI-CORRELATED with lock quality

I claimed a P0 fix had no regression lock, because `_publish_failed` appeared in the hook and in **zero**
harness files. **The grep was factually true and the conclusion was false.**

A lock that asserts **behaviour through the public seam** — the lock we actually want — mentions no
internal symbol at all. So:

> **The better the lock, the more invisible it is to a symbol grep.**

The indicator scores implementation-coupled tests highly and behaviour-coupled tests at zero. It measures
how *badly* a lock is written and reports it as coverage.

**Correct indicator:** run the candidate lock against a **pre-fix artifact** and check it goes RED.
Coverage is a behavioural property and must be measured behaviourally.

*(Disproven by a fully-committed A/B: committed lock vs pre-fix hook → 6 pass / 3 FAIL; vs HEAD → 9/0.
Withdrawn at the artifact in bead `w5z32b`, retained rather than deleted so the trace survives.)*

---

## 3. Every safety arm needs a liveness arm, and the liveness arm is the one that gets forgotten

Used on every lock written this sprint. The recurring trap, in three concrete forms:

| lock | the safety arm alone is satisfied by… |
|---|---|
| poller transient-retry | a poller that retries forever and **never reconnects** |
| profile criticality | a builder that **refuses every profile** and bricks every host |
| filing-tool empty-body | a tool that **rejects every filing** |

In each case the degenerate implementation passes the safety arm perfectly *and is the failure being
fixed*. The liveness arm is the only thing that fails when a component is silently doing nothing.

**Over-correction is frequently the more expensive direction** — a filing tool that hard-errors on every
call halts the exact activity a release gate depends on.

---

## 4. A discriminating lock proves its own teeth by A/B, not by going green

A lock observed only in its GREEN state has not been shown to discriminate. Both locks written this
sprint were proven by running the *same lock* against two artifact versions, one variable changed:

```
poller lock   vs pre-fix hook   0 passed / 5 failed        vs fixed hook   5 passed / 0 failed
profile lock  vs current code   1 failed / 2 passed (by design — safety RED, liveness GREEN)
```

The 1-of-3 shape is deliberate: a lock failing *all* arms is indistinguishable from "the subject doesn't
work at all," and would be satisfied by a builder that refuses everything.

---

## 5. Build the impl explicitly; a test-project `--no-incremental` runs against a stale DLL

Standing project rule, re-confirmed in practice. Also: verify at **committed HEAD**, not the working
tree — the Read tool reads the working tree, and two reviewers reported working-tree state as committed
this sprint. A test file and its fix must be staged as **one coupled commit**; a deliberately-failing lock
landing without its fix turns mainline red, which is its own dishonest instrument (a red that is always
red stops carrying signal).

---

## 6. Capture `$?` on the very next statement — including after the `echo` you added to print it

A lock's exit status was read as `0` from a piped `tail`, then as `1` from a failed redirect, before the
third attempt — direct capture, valid path, no pipe — gave the true exit. **Nearly published a false green
about a lock whose entire purpose is catching false greens.** Knowing the rule did not prevent it;
capturing `$?` directly did.

---

## 7. `set -e` in a harness: the effect is structural, and the predicate is one line

Two harnesses gave **opposite** results under `set -e` (one a no-op, one silently dropping 5 of 10 arms).
Both measurements were true. The discriminator is the shape of the helper's **last command**:

```
OUT="$(_run)"; RC=$?          under set -e -> ABORTS when the command fails
if OUT="$(_run)"; then :; fi  under set -e -> survives
```

A helper ending in `echo $?` can never fail, so `-e` has nothing to fire on. **When `-e` is added to a
harness, re-run it and confirm the arm count is unchanged** — a suite that reports fewer failures than
exist is the same disease being cured.

---

## 8. A downgrade needs the same scrutiny as an escalation — and BOTH halves of why

Stated first as a one-sided claim by me, corrected by SoftwareArchitect the same hour. The corrected
version is the one worth keeping, because the one-sided version teaches the wrong habit.

**Per instance, a false downgrade is worse:**

| | what happens next |
|---|---|
| false **escalation** | the room investigates → **the error surfaces**, usually in minutes |
| false **downgrade** | everyone stops looking → **there is no second reader for a closed item** |

**In aggregate, the sign flips.** A false escalation *repeated* costs credibility rather than attention:
it trains the team to discount alarms, and the alarm that matters arrives into an audience that has
learned to skim. This sprint produced the counter-example directly — four agents independently flagging
the same untracked file created a pile-up that was **itself** the hazard, because a wall of urgent flags
is how an indiscriminate bulk-stage happens.

**Resolution — equal scrutiny, for different reasons in each direction:**

- **Scrutinise a downgrade** because nothing downstream will catch it. It *feels* safe; that feeling is
  the whole risk.
- **Scrutinise an escalation** because raising it costs the team's future attention, not just your own
  time. Volume is a cost even when each instance is correct.

Neither direction is free, and "when in doubt, escalate" is not the safe default it appears to be.

---

## 9. The night's dominant failure: a grep returns a FRAGMENT, and the mind supplies a SENTENCE

Not an individual lapse. **Six instances, four people, one shape** — and it twice came within one message
of changing a decision.

| instance | the fragment | what the surrounding text said |
|---|---|---|
| mine, ×1 | `_publish_failed` absent from the harness | the lock binds *behaviour*, so it names no internal symbol |
| mine, ×2 | a one-sided claim present at `:73` of a retro | `:75` corrects it — it was a **quoted antecedent** |
| mine, ×3 | no failure-forcing idiom I recognised | the obstruction is injected through a documented override seam |
| three others | *"declares … comprehensive audit logging"* | same sentence: *"each of which runs only if registered"* — a **disclaimer**, read as a promise |

In every case the search was **run correctly and returned a true result.** The defect is downstream of the
tool: a match is a *fragment*, and reading stops at the fragment while the conclusion is drawn about the
*claim*. Those are different objects, and only the first is greppable.

**The two questions that separate them, and they must be asked explicitly:**

1. *Does the surrounding sentence reverse this?* — a disclaimer, a `> quoted` antecedent, a "was:"
   comment, a correction on the next line. **Read the whole sentence, then the paragraph.**
2. *Could the thing I claim is absent exist in a form my query cannot match?* — behaviour instead of a
   symbol, an override seam instead of an idiom I imagined. **If yes, the absence is not evidence.**

**Corollary — a retraction is subject to the same defect, and worse.** One of the six above occurred
*while retracting*: a fragment was grepped, the sentence unread, and a correct position withdrawn. A
retraction feels like diligence, so it receives less scrutiny than the claim it replaces — which is
precisely backwards, because withdrawing a true finding removes the audience that would have re-checked it.

**Corollary — chase every copy.** Filing the same claim in two places and withdrawing only one leaves a
false record standing at full severity. That happened here: two beads, one premise, one withdrawal, and a
false P0 sat in the backlog ~25 minutes. Nothing prompted the discovery; it surfaced only because an
unrelated agent's self-correction prompted the question.

---

## 10. The meta-lesson: an instrument must be checked against the question actually being asked

Failures 1 and 2 are the same family. In both, an instrument was run, produced a real result, and was
never checked against the question it was meant to answer. Neither was carelessness about *running* the
tool; both were carelessness about *what the tool could see*.

The night's product defects had the identical shape — a failure path returning the success signal — which
suggests the discipline is one discipline, applied at two altitudes: **prove your instrument can report
the answer you are not hoping for.**
