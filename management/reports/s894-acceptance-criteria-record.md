# Sprint 894 — Acceptance Criteria Record (ProductManager)

**Status:** authoritative record of the acceptance rulings S894's close depends on.
**Why this file exists:** every ruling below was previously held only in the discovery log
(`.dts/**`, which is **gitignored** — zero tracked files) and in OPCOM messages. Neither is
git-tracked, so neither survives the volume. This is the S890 *working ≠ shipped* lesson applied
to the acceptance record itself.

**Author:** ProductManager (AC owner). **Date:** 2026-07-21.

---

## 1. Definition of Done (adopted from CEO, ruled by ProjectManager)

> **We may ship a known GAP. We may not ship a false CLAIM.**

### 1.1 Operational form — the terminating condition

The test is **not** whether the defect is fixed. It is whether the **shipped artifact tells the
truth about itself**. Every item therefore has **two** discharge paths:

1. **Fix the artifact** so the claim becomes true, or
2. **Correct the claim** so it matches reality — the gap becomes *stated*, and stating it is what
   makes it a gap rather than a lie.

**S894 closes when no shipped artifact asserts something untrue — not when every underlying
defect is repaired.**

### 1.2 Guard against path 2 becoming a rubber stamp

**The correction must be as prominent as the claim it qualifies, on the same surface, at the same
reading depth.** A confident heading with a caveat three paragraphs below is still a false claim in
practice — the reader who acts on the heading never reaches the footnote. If the claim is in a
capability table, the correction goes **in that table cell**.

### 1.3 The line binds in BOTH directions

**An over-correction that turns a TRUE statement FALSE is also shipping a false claim.** Direction is
irrelevant to the line.

- Every edit must be verified against the **specific mechanism** the statement refers to.
- **Per-file verification, not per-pattern substitution.** An unverified edit is not a correction; it
  is a new unverified claim.
- A file that cannot be verified in the time available is **left untouched and listed as unaudited**.
  Untouched-and-disclosed is honest; edited-on-a-guess is not.

*(Context: this repo has three distinct fencing mechanisms. Most `.cs` files mentioning fencing +
SQL Server are TRUE statements about a different mechanism.)*

### 1.4 The meta-claim is a claim

**"S894 shipped no false claims" is itself a claim** and must meet the same bar. Asserting a
completeness we did not earn is the unearned-PASS defect, committed in the closing sentence.

**Corollary, equally binding: understating confidence is the same defect pointed the other way.**
"We don't really know" is also a claim. If the evidence no longer supports it, asserting it ships a
falsehood about our own rigour. Do not ship a pessimism we have outgrown.

### 1.5 Distinction from the operator's "no document-the-limitation deferrals" bar

- **Prohibited:** "the feature doesn't work here — note it and move on", with **no tracked work**.
- **Permitted:** the fix is **tracked, prioritised and owned**, and the only thing landing now is our
  published surface ceasing to misrepresent it.

We are not blessing the gap; we are refusing to misrepresent it while we still owe it. That is the
difference between a deferral and honest sequencing.

---

## 2. Scope rulings

### 2.1 Honesty ACs bind every consumer-visible surface

An honesty/caveat AC binds **every** consumer-visible surface that asserts the guarantee — XML docs,
`docs-site/**`, package README/release notes, `samples/**`, **and `docs/**`** — not whichever surface
the criterion happened to name.

**Superseded by 2.2.** Enumerating surfaces failed repeatedly (see §4).

### 2.2 Invert the default (RECOMMENDED to SoftwareArchitect, rule shape is his call)

> **Every committed artifact is PUBLIC surface unless it appears on an explicit internal-sink
> denylist** (`management/**`, `.claude/**`, `.dts/**`, `tests/**`, commit messages, tracker files,
> OPCOM). **No allowlist of public locations.**

Rationale: an **allowlist fails silently by omission; a denylist fails loudly by inclusion.** Under
this shape "we forgot to enumerate a surface" becomes inexpressible.

### 2.3 ARCHITECTURE.md — four sites, not one

A guarantee contract with surviving false statements **fails the close outright**. Fixing one named
site while three remain is not a partial pass. (SoftwareArchitect's `kmqxni` enumeration supersedes
any narrower lane assignment.)

### 2.4 Sweep derivation

The close sweep must be **derived mechanically from the claim**, across every committed path except
the internal denylist — **not** from any human's inventory of where they think the claim lives.

**Satisfied by:** BackendDeveloper's *"search the claim sentence, not the topic keyword"* discriminator
(368 topic-mentions → 5 claim-sites), independently cross-checked by a second agent whose broader
pattern found zero additional false claims.

---

## 3. Close wording (the operative requirements)

### 3.1 Fencing false-claim sweep

> "5 false-claim sites identified by **mechanical derivation from the claim sentence** (368
> topic-mentions → 5 claim-sites), independently cross-checked by a second agent using a broader
> pattern, which found **zero additional** false claims. The 88 topic-mentions were **not**
> individually audited — the discriminator supersedes that count rather than leaving it outstanding.
> This is convergent mechanical evidence, **not a proof of exhaustiveness**: the derivation is a
> pattern over claim phrasings, so a claim asserted in a form the pattern doesn't match (e.g. by table
> membership) would not appear. Generalising the discriminator into a standing gate is tracked."

### 3.2 Docs-site build

> "Consumer-doc corrections were verified for content against the implementation. The `docs-site`
> build gate **could not execute** — build status is **UNVERIFIED**, not green. Tracked separately
> (`o6edr0`)."

**Ruling:** a dead build gate does **not** invalidate a mechanism-verified content correction. It only
means we cannot *also* claim the site compiles. The docs build is **NOT a close gate for S894.**

### 3.3 CI evidence

Full CI ran green at `e5d9cadeb`; HEAD moved five commits beyond it, including all three REVIEW_CODE
blocker fixes.

**Ruling history (both states recorded deliberately):**
- I first ruled *"disclose, do not re-run"*, justified by a per-commit evidence table.
- **That ruling was RETRACTED.** TestsDeveloper measured the diff: 14 files, +484/−85, including
  **+79 in the outbox conformance base** that four provider suites derive from. My "targeted evidence"
  did not bound the risk — the locks I cited **run through** the thing that changed. **I characterised
  instead of measuring.**
- Disclosure alone is therefore **not sufficient**; if a run cannot complete, the disclosure must state
  that **the conformance base changed and is unverified**.

### 3.4 Environment

Any close must disclose that the volume holding the repo, tracker and all sessions reported
**"Full Repair Needed"**. Repo integrity was independently verified (`git fsck` clean, object store
intact), and the work was pushed to the remote — but every measurement tonight came off that volume,
and "all green" without the caveat overstates available confidence.

---

## 4. Standing observation for RETRO (evidence, not blame)

**Five enumeration failures in one night, five people, identical shape** — each was an **allowlist of
places**, and in each the defect was a member nobody thought of. Every author was already being
careful; two had invoked this exact principle at others within the preceding two hours.

**A rule instructing carefulness is refuted by its own dataset.** The recommendation is a **gate**, not
a rule file:

> **Scope-completeness check** — for each scope-defining artifact, assert
> `covered_paths ∪ internal_denylist == all_committed_paths`. A committed path in neither set FAILS,
> naming it. Safety arm: a new directory goes RED until classified. Liveness arm: a fully-classified
> repo PASSES.

**Related, and the sharper generalisation:** six instruments failed silently tonight — one answered the
wrong *question*, one about the wrong *file*, one *could not run* and said nothing, one's *evidence was
stale*, one was *blind* to a syntax it never matched, and one *misdescribed its own consequences*.
**Every one reported something true about the wrong thing.**

**A green with no SHA attached is not evidence — it is a memory.**

---

## 5. Author's own corrections (recorded because the record should show them)

Five claims of mine were corrected tonight, four by others, all the same species: **a categorisation or
characterisation offered in place of a measurement.**

1. SHA-pinning placed in a tier my own criterion excluded.
2. Three failure classes collapsed into one.
3. Public-surface enumeration that omitted `docs/` — written *to fix* a too-narrow enumeration.
4. `docs/`-exclusion offered as the cause of the shipped-DDL defect; it is a real separate gap but not
   that cause.
5. The CI-evidence ruling in §3.3, retracted in full.

Recorded deliberately: the acceptance record is more trustworthy with its corrections visible than
without them.
