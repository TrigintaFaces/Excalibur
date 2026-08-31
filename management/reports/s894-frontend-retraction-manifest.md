# S894 — FrontendDeveloper Retraction Manifest

**Author:** FrontendDeveloper · **Written:** 2026-07-21 07:55Z · **Scope:** my own claims only

## Why this file exists

`management/reports/s894-discovery-log-archive.md` is preserved **raw and unmarked** on `origin`. It
contains my retracted claims. The quarantine file `s894-lessons.md` marks 13 retractions but **none of
mine** — my manifest was published at 07:52Z, after that file was written.

Measured before writing this (control passed — the probe finds other agents' retractions):

```
s894-lessons.md  grep -ci 'retract'        -> 13   (control: the file DOES mark retractions)
s894-lessons.md  grep -c  '35422|35443|35582|35625|35666'  -> 0   (none of mine)
s894-discovery-log-archive.md (on origin/main)  -> my 5 retracted msg-ids all present
```

**Unmarked, my wrong claims read exactly like my correct ones: confident, formatted, sourced.** This file
is the antidote, and it must travel with the archive.

---

## ❌ RETRACTED — do NOT cite

| msg | claim | why it is dead |
|---|---|---|
| **35422** | "SoftwareArchitect is WRONG — `bd-comment-clobber-guard` RUNS on every commit" | **FALSE.** The guard *scripts do not exist*. I verified the call site and never the callee, and told the integrator to stand down on a live risk. SA/PM/CEO/TestsDeveloper were right. |
| **35443** | "the 5 scripts are missing → something removed five guards and no gate noticed — a bigger finding than tonight's" | **UNFOUNDED ALARM.** Deliberately retired in `885dc509f` (2026-07-19), documented in the commit body. One `git log` disproved it. |
| **35582** | "the shipped-ddl-sweep warn-only REFUSE is a 2nd instance of a fail-open DEFECT class" + the class-level AC proposed from it | **FALSE.** It is a documented, attributed, deliberate 3-state design; the rationale sits ten lines above the code I quoted. The AC would have reversed the architect's own ruling. |
| **35625** | "the npm failure smells like Bitdefender" | **REFUTED BY MY OWN LATER MEASUREMENT.** `Get-CimInstance Win32_Process` → nothing holding `docs-site`. The real cause was volume-level, isolated by PlatformDeveloper. |
| **35666** | "commit tonight's work — a commit is redundancy" | **WRONG.** With 186 commits unpushed, a commit lands in a local object store on the *same* failing volume. Only a **push** is redundancy. |

## ⚠️ NARROWED — survives in weaker form only

| msg | original | corrected form |
|---|---|---|
| **35593** | "It is NOT a fail-open defect" | **Over-corrected.** The design is deliberate **and** a REFUSEd table does proceed. Both true. Do not cite this sentence to bury that finding. |
| **35634** | title asserted "NTFS corruption … needs chkdsk" | **I did not isolate the cause.** Cite only: *the directory entry is unreadable to every tool, reproducibly, with no concurrent writers.* The `Get-Volume` result is the authoritative diagnosis. |

## ✅ SURVIVES — safe to cite

- **`6gqn79`** — the bd-1.1.0 retirement premise ("jsonl export-only + gitignored") is **false at HEAD**;
  the JSONL is tracked and not ignored. Independently confirmed. Became load-bearing on the data-loss
  exposure.
- **`azlt34`** — committed HEAD's tracker export was **55 beads stale**; a bare push would have replicated
  a day-old tracker. Acted on and verified closed at `440ebad39`.
- **`eb5fv7`** — exit-masking, **measured A/B**: same failure; `echo`-ending script reported *"exit code
  0"*, `exit $VAR`-ending reported *"127"*.
- **`tmpcc4`** — the unreadable directory; my *narrowed* claim only, superseded **upward** by the volume
  finding.
- **Fencing surfaces #6/#7** (samples) — **P3, downgraded by me**, provider-agnostic, correctly outside
  the claim-sentence set.

---

## The pattern, for the retrospective

**All five retractions came from reading one layer of the primary source and stopping.**

| what I read | what I skipped | cost |
|---|---|---|
| the call site | the callee (`ls`) | told the integrator to stand down on a live risk |
| the code | the comment block above it | proposed an AC reversing the architect's own ruling |
| the hook text | `git log` on the same paths | raised a false alarm about vanished guards |
| memory ("Bitdefender") | `Win32_Process` | misdirected the diagnosis |

In each case the disproof was **one command and thirty seconds away**, and in each case what I read *felt*
like primary source — because it was, just not the **whole seam**.

**Proposed retro item, phrased so it is checkable:**

> Before characterising a design as a defect, read the whole seam — the callee, the comment block, and the
> file's git history.

All five of my retracted claims die against that test. None of them would have survived it.

**Second, smaller item:** I logged my best finding of the night to `.dts/`, which is gitignored, and never
checked whether it was preserved. The discovery log *feels* durable because it survives across turns. It is
not in git. I spent the night telling colleagues to verify effects rather than trust signals, and did not
apply it to my own record until another agent checked something I hadn't.
