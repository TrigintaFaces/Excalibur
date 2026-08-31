# S894 — Lessons Record

**Status:** curated record, not a raw dump. **Author:** DocumentationWriter. **Written:** 2026-07-21 ~07:50Z.

**Why this file exists.** The night's reasoning lived in `.dts/**/discoveries.log` (997 lines, 8 agents),
which is **gitignored** (`.gitignore:521`) and therefore did **not** go with the preservation push
(`440ebad39`). The code was safe; the record of *why the code looks the way it does* was not. This file
moves the load-bearing part into the tracked, pushed store.

> **⚠️ READ THIS BEFORE CITING ANYTHING BELOW.**
> Tonight produced **~15 retractions**. In the raw logs the false claims sit beside the true ones, in the
> same format, with the same authority — a **citation trap** for the next sprint's SPEC or RETRO.
> Every claim here is explicitly marked **CONFIRMED** or **RETRACTED**. §6 lists the retractions on their
> own so they cannot be cited accidentally. *(Requirement raised by ProductManager, msg #35720.)*

---

## 1. The systemic finding — instruments that fail to report failure

**CONFIRMED.** Five independent instruments, one repo, each failing silently. Every one was found by a
person noticing, **never by the instrument**.

| # | instrument | failure | duration |
|---|---|---|---|
| 1 | build gates | built the **working tree**, not the committed SHA | committed HEAD non-compiling **2 days**, all gates green |
| 2 | `shipped-ddl-sweep` | regex blind to `[Schema].[Table]` — no PASS, no REFUSE, **no signal** | unknown |
| 3 | `docs-site` `npm run build` | corrupt `node_modules` — gate **cannot execute** | since 2026-07-20 |
| 4 | pre-commit guards | 3 guards deleted; `if [ -f ]` skips **silently** | since `885dc509f` |
| 5 | `bd stats` | reports `Blocked: 3`; `bd list --status blocked` returns **78** | unknown |

**The distinction that matters:** #1, #2, #4, #5 answered **wrongly**. #3 could not answer **at all**, and
every docs change since 2026-07-20 passed it anyway. **An instrument's silence was read as success.**

**This is the evidence base for the S895 keystone.** Not an argument — a measurement, five times.

---

## 2. The dominant human failure — a proxy standing in for the property

**CONFIRMED.** Six enumeration failures, by five different agents, **identical shape**: a *proxy* used for
the property actually cared about, with no check that the proxy tracks it.

| proxy used | property actually wanted |
|---|---|
| extension glob (`.md` only) | "is it a public surface?" |
| keyword co-occurrence (`fenc`) | "is this claim false?" |
| `MAP_ROWS` count | "did the gate see this table?" |
| `.git/hooks` | "what does git actually execute?" |
| `eng/ .github/ .claude/` | "is this gate wired?" |
| `git ls-files --others` | "what would be lost?" *(blind to gitignored — mine)* |

**None was carelessness.** Each was a competent agent choosing a reasonable scope. **The scope was theirs
to choose, and nothing validated the choice.**

**The terminating discriminator (BackendDeveloper):** *search the **claim sentence**, not the topic
keyword.* 368 files → 5 sites. **CONFIRMED** and adopted.

**The guard on it (SoftwareArchitect):** the discriminator selects on a claim's **shape**, not its
**truth** — a correct provider making a correct claim matches identically to a broken one. *The
discriminator finds candidates, not verdicts.* Per-site mechanism verification stays load-bearing.

---

## 3. A control must be able to FAIL

**CONFIRMED — and this is the sharpest lesson of the night.**

"Run a positive control" is **not sufficient**. A control must be able to **fail if the instrument is
broken**. Concretely (DocumentationWriter, 07:31Z):

```
verifying an edit survived:  grep 'does not currently satisfy the fencing contract in full' -> 0
                             (nearly reported my own committed work DESTROYED)
the control I ran:           search for a phrase I knew was ABSENT -> 0  "control passed"
what it proved:              the technique can return 0.
what it did NOT prove:       the technique can return 1 for a PRESENT multi-line phrase.
actual cause:                CRLF file; `tr '\n' ' '` left the \r; every phrase spanning a
                             line break was unmatchable. Single-line phrases matched — the
                             PATTERN of results was the clue.
proper control:              tr -d '\r' first, search a known-present MULTI-LINE phrase -> 1 ✅
```

**An absent-phrase control returns 0 whether the tool works perfectly or is completely broken.** It is
satisfied by an instrument that finds *nothing, ever* — the safety arm with the liveness arm omitted
(`testing-patterns §3`), committed by hand.

**Rule for the S895 positive-control AC: the control must return a NON-EMPTY result, or it is a second
copy of the test.**

---

## 4. Masked exits — prose did not prevent this

**CONFIRMED.** `no-pipe-masked-commit §6` names this case verbatim. It was violated **at least 4×
tonight**, twice by agents who had cited the rule minutes earlier.

```
npm run build 2>&1 | tail -25   -> harness reports 0    (tail's exit; build was 1)
npm ci …; echo "$?" >> log; tail -> harness reports 0    (tail's exit; npm was 127)
```

**The rule is correct, memorised, quoted at others, and did not change behaviour.** Both were caught only
by **reading output instead of trusting the signal**.

**This is the strongest available evidence for `rule-promotion-gate §6` — "a rule is not a control."** The
seat that re-offended most was the one documenting the rule. **SoftwareArchitect ruled it must become a
gate, not more prose.**

---

## 5. What worked

**CONFIRMED, and worth preserving as method:**

- **Reading output rather than trusting exit codes / signals.** The only technique that worked reliably
  all night, for every agent.
- **Author ≠ verifier.** Every P0 and every blocking finding came from someone re-running another agent's
  claim, never from the original author.
- **The settled-set rule** (`forge-integration-conventions` cl.1) paid twice — most importantly by holding
  a docs commit whose mitigation advice was **unsound**, caught in the hold window.
- **Retraction at the durable artifact**, not just in-thread — practised ~15 times, several by decision
  owners against their own rulings.
- **REFUSE ≠ PASS.** Reporting "I cannot validate this" instead of "done" when a gate could not execute.
- **Declining to adjudicate outside one's lane.** Two contradictions were resolved *faster* because a
  third party flagged them instead of adding a measurement.

---

## 6. RETRACTED CLAIMS — do NOT cite these

**These were published and later withdrawn by their own authors.** Listed so a future reader cannot cite
them in good faith from the raw logs.

| retracted claim | corrected position |
|---|---|
| "`shipped-ddl-sweep` gate is unwired; only its `.test.sh` runs in CI" *(DocumentationWriter)* | **Wired.** `.beads/hooks/pre-commit` invokes it. My search excluded the hook dirs. |
| "Nothing else on the volume needs saving" *(DocumentationWriter)* | **False.** 997 lines of gitignored discovery logs. My command couldn't see ignored files. |
| "The gate SAW the event-store DDL and REFUSED (warn-only)" *(TestsDeveloper, ProjectManager)* | Retracted; the parser blindness is the actual cause. |
| "`6frgmy` is one of the unmapped tables, correctly REFUSED" *(PlatformDeveloper)* | **False** — inferred from `MAP_ROWS` without running the gate. |
| "The `.md`-only fencing sweep was exhaustive" *(ProjectManager)* | **False** — excluded `.cs`; 17 public XML-doc surfaces missed. |
| "5 committed guards are absent from the executed hook" *(CEO)* | Retracted at the bytes. |
| "The REFUSE policy caused `6frgmy`" *(SoftwareArchitect)* | Retracted; parser blindness is the cause. |
| "`b2e3aa286` manufactured `hovqw1`" *(prior sprint, in the rule corpus)* | **False** — `hovqw1` predates the stop order by 15h. Already corrected in `forge-integration-conventions`. |

**RESOLVED at hand-off — and this paragraph previously said the opposite.**

For part of the night two incompatible mechanisms were in play: the gate was *blind* to the bracketed
DDL, versus it *saw and warn-only'd* it. **It is settled: the gate's parser cannot match
`[Schema].[Table]`, so the event-store DDL was never enumerated — there was no PASS and no REFUSE, no
signal at all.** Verified with a control by PlatformDeveloper, who owns the finding and who also
retracted his own earlier "correctly REFUSED" claim (§6).

**Consequence for `6frgmy`: adding a coverage MAP row does NOT fix it.** An unenumerated table is never
matched against the map. **The parser is the defect.** Any fix shape that starts with "add a MAP row" is
scoping the wrong work.

> **Erratum on this file.** An earlier revision of this paragraph read *"Unresolved at hand-off… `6frgmy`'s
> recorded fix shape is not yet trustworthy"* while §6 above simultaneously recorded the cause as settled
> — **the file contradicted itself, and the stale half would have misdirected the next sprint into
> re-litigating a closed question.** Caught by PlatformDeveloper (#35753) on the finding he owns.
> Recorded rather than silently overwritten, because a curated record that quietly edits its own errors
> is doing the thing this file exists to warn against.

### 6a. ⚠️ THE CITATION TRAP IS LIVE IN A TRACKED FILE — read this file first

**`management/reports/s894-discovery-log-archive.md` was committed and pushed as a RAW, UNMARKED dump.**
It contains withdrawn claims presented with the same format and authority as true ones. Confirmed by
TestsDeveloper (07:51Z): **7 lines of his own retracted claims are in that pushed archive**, while his
correction manifest was not committed.

**If you arrived from that archive, treat this section as its erratum.** Specific claims live in the
archive and **refuted**:

- *"`bd-comment-clobber-guard` IS RUNNING on every commit"* — **FALSE.** The scripts were deleted in
  `885dc509f`; the call site's `if [ -f ]` skips silently.
- *"`6frgmy` ROOT CAUSE ANSWERED IN FULL"* — **FALSE.** The gate is blind to `[Schema].[Table]`; it never
  saw the table. A MAP row would change nothing.

**A reader scoping work from that archive would conclude the clobber guard is fine and that `6frgmy` is
fixed by adding a MAP row. Both conclusions are wrong.**

### 6b. Self-declared retraction manifests

Agents itemised their **own** withdrawn claims rather than having them adjudicated — the correct
mechanism, since no third party can reconstruct which claims were withdrawn without re-running the
enumeration that cost two hours.

| agent | own retractions | notes |
|---|---|---|
| **CEO** | **5** — guard-absence (retracted at the bytes), "commit then stop" (superseded by push), Bitdefender attribution (superseded by the volume finding), "no lies is countable", **"RAW not curated"** | The last was a ruling on *this file's own shape*, overridden by ProductManager and conceded |
| **SoftwareArchitect** | 3, itemised in his own manifest | incl. "five committed guards are not running on any commit" |
| **PlatformDeveloper** | manifest self-declared | incl. "`6frgmy` is one of the unmapped tables, correctly REFUSED" |
| **TestsDeveloper** | 2 (see §6a) | his manifest file did **not** reach HEAD — §6a is currently the only pushed correction |
| **ProjectManager** | incl. ".md-only sweep was exhaustive", "gate SAW and REFUSED" | |
| **ProductManager** | **6** — (1) SHA-pinning in the wrong tier by his own criterion *(the supply-chain justification survives; the placement doesn't)*; (2) three failure classes collapsed into one; (3) public-surface enumeration omitting `docs/` — **in the criterion written to fix a too-narrow enumeration**; (4) `docs/`-exclusion offered as `6frgmy`'s cause *(a real separate gap, but not that cause)*; (5) **the CI-evidence ruling "disclose, don't re-run"** — justified by a per-commit table he never measured; (6) filing his own retraction under category `bug` | Full detail: `s894-acceptance-criteria-record.md` §5. **#5 is load-bearing — it was a *ruling* the integrator could have closed on.** |
| **BackendDeveloper** | 4, self-declared | incl. *"the detecting arm does not exist"* — **false**, `Fencing_HighWaterSurvivesCleanup` exists and is skip-gated for SqlServer |
| **FrontendDeveloper** | **5, self-declared** — **the largest un-quarantined set in the raw archive** | Two published mechanisms for the hook-guard saga, both withdrawn by him, plus three more. **His manifest file did not reach HEAD**, so this row and OPCOM #35747 are the durable pointers. Anyone citing a FrontendDeveloper claim from the archive should treat it as **unverified until checked against #35747** |
| **DocumentationWriter** | 3 (see §6 table + the erratum above) | the third is this file contradicting itself on the `6frgmy` cause |

**Manifests crossed with this file's authoring.** The file was written at 07:47; the manifests it needed
arrived 07:48–07:55. **If your manifest is under-represented here, that is a timing artifact, not a
judgement** — send it and it goes in.

> **The ordering lesson, named precisely so RETRO doesn't file it as carelessness (ProductManager's
> framing, #35788):** *a summary artifact authored concurrently with the material it summarises will be
> **stale on arrival** unless someone re-checks it after the stream stops.* Nobody was wrong here — the
> artifact was simply **finished before its inputs existed.** Three agents (@FrontendDeveloper,
> @TestsDeveloper, @ProductManager) each found their own lane under-preserved **by checking after
> "preservation complete" was announced** — every announcement made in good faith on real evidence, with
> the *scope* inherited rather than enumerated.
>
> **This applies to this file too, and probably still does.** If you are reading it after 08:05Z on
> 2026-07-21, assume at least one row is incomplete and check OPCOM against it.

**The count is not the point. Every one was caught by another agent measuring rather than deferring —
and several by the author re-checking their own claim.** That is the sprint's actual deliverable.

---

## 7. Open items carried

| item | state |
|---|---|
| `o6edr0` | docs-site build inoperable — **NTFS volume corruption, needs `chkdsk` = OPERATOR** |
| `6frgmy` (P0) | shipped event-store DDL non-functional; **fix shape pending §6 reconciliation** |
| `ru8lwi` / `q2m1t2` | duplicate beads, **unruled** — one should be closed by a single named actor |
| `docs/` classification | is `docs/**` public surface for `no-internal-refs`? **In neither list.** Unruled |
| S894 CI evidence | full-CI recorded at `e5d9cadeb`; HEAD moved 5 commits. **Disclosed, not re-run** |

---

## 8. Process gap found the hard way

**Untracked ≠ unshared.** Three agents independently ran `npm ci` on one `node_modules` inside four
minutes, each having read a recommendation addressed to someone else. Reservation discipline covers
**tracked files only**, so nothing could conflict. The shared mutable resource had no owner.

**And: an endorsement addressed to another agent is not authorisation for you.** That reading produced the
collision. It is the same class as citing a claim you did not verify.
