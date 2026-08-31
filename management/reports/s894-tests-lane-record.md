# S894 — TestsDeveloper (CRUCIBLE / TRACEPOINT) Lane Record

**Author:** TestsDeveloper · **Written:** 2026-07-21 07:47Z · **Pushed HEAD at time of writing:** `440ebad39`

**Why this file exists:** the per-agent discovery log (`.dts/sessions/*/discoveries.log`) is **gitignored**
(`.gitignore:521:.dts/`). My 16 entries / 27,444 bytes — every finding, retraction and correction below —
existed **only** on the `D:` volume the OS reports as *Full Repair Needed*, and did **not** travel with the
187-commit push. This is the tracked copy.

---

## 1. Phase deliverables (S894)

- **TEST (CRUCIBLE, task 2677)** — completed `2026-07-21T01:23:45Z`.
- **VERIFY (TRACEPOINT, task 2678)** — completed `2026-07-21T01:28:41Z`; report at
  `management/reports/s894-verify-traceability.md` (was untracked until flagged; now in `440ebad39`).
- Full-CI evidence: 11 shards at `e5d9cadeb`. **See §3 — this is a known, disclosed gap.**

## 2. Findings that held

| # | finding | status |
|---|---|---|
| F1 | **`6frgmy` severity raised** — shipped event-store DDL is not "column drift" but **total non-function**. Code writes `EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp` + `OUTPUT INSERTED.Position`; published DDL (`docs-site/docs/configuration/event-store-setup.md:280`) declares `SequenceNumber, StreamId, Version, EventType, Data, Metadata, Timestamp`. **3 columns absent, 2 renamed, `OUTPUT` references a non-existent column.** A consumer following our documented setup gets an event store where *every append throws on first write*. | HELD |
| F2 | **30 shipped `CREATE TABLE` surfaces** across `docs-site/` + `samples/`, **zero test coverage** of any of them. | HELD |
| F3 | **No live clobber** — `comments.jsonl` worktree == HEAD (9556/9556, delta 0) while `issues.jsonl` grew +48 (control proving the method detects deltas). Converted a panic into a scheduled fix. | HELD |
| F4 | **Stale CI evidence (my own instrument)** — sprint plan records full CI at `e5d9cadeb`; HEAD had moved 5 commits, **including all three REVIEW_CODE blocker fixes**. The sprint's green did not cover what it was closing on. | HELD → disclosed |
| F5 | **Fencing coverage gap** — `sicpvm` (durability) is covered by the existing arm; **`25mij8` (monotonicity) is not.** The existing arm only ever drives the *rejected* path; `25mij8` lives on the *accepted* path. Two beads, two locks, one unwritten. | HELD |
| F6 | **My VERIFY deliverable was untracked** (`??`, 8KB) on the failing volume — the one category `git fsck` cannot vouch for, since an untracked file has no prior version in the object store. Now committed. | HELD → resolved |

## 3. Retractions — I was wrong four times

Recorded in full because a lane record that keeps only the wins is the dishonesty this sprint existed to fix.

| # | I claimed | truth | caught by |
|---|---|---|---|
| R1 | "`bd-comment-clobber-guard` **runs** on every commit" — told the PM to stand down on a data-loss alarm | **It does not run.** All five guard scripts were deleted in `885dc509f`; the call sites are wrapped in `if [ -f ]` → silent no-op. I verified the *call site* existed and never checked the *script* existed. | PlatformDeveloper |
| R2 | "Two independent verifiers agree" (me + FrontendDeveloper) | **Correlated error, not corroboration.** We ran different greps against the same wrong artifact; neither of us `stat`'d the file. I gave a false finding the vocabulary of consensus. | self, after R1 |
| R3 | "`6frgmy` root cause **ANSWERED IN FULL** — the gate saw it and REFUSED (warn-only)" | **The gate never saw it.** `event-store-setup.md` appears nowhere in a completed sweep. I matched the word `events` in my own output to the event store; it was a different table in `migrations.md`. The gate is **blind to `[Schema].[Table]`**. Adopted as root-cause-of-record by two people before I caught it. | self, after PlatformDeveloper's finding |
| R4 | "Risk is low — `d8b5c6185` is docs-only on a compile-verified tree" | **Not docs-only.** 6 src files including **both outbox fence write paths**, plus **+79 lines in the shared conformance base** every provider suite inherits. I asserted a risk level before measuring it. | self, 15 min later |

**Common shape of all four: reading an adjacent signal as the thing itself** — a name in a list read as a
call site, a call site read as an execution, a word in output read as a table, a commit range assumed to be
docs. Each was caught; none reached a commit; every correction was chased to the artifact, not left in a thread.

## 4. Proposed S895 arms (test lane)

1. **Shipped-DDL enumeration arm** — the sweep's enumerated-table count **must equal** the repo's actual
   shipped `CREATE TABLE` count (today **13 vs ~30**, RED). RED against a planted **bracketed**, **quoted**
   and **bare** table. *Not* a coverage/`MAP_ROWS` arm — a complete MAP would still miss every bracketed
   table while the unmapped count read **zero**, i.e. a metric that gets greener as the gate goes blinder.
2. **Guard-inventory arm** — every guard the hook invokes has an existing script; **absence FAILS LOUD.**
   `if [ -f guard.sh ]` with no `else` is fail-open by construction.
3. **`25mij8` monotonicity arm** — safety: stored high-water never decreases after an *accepted* write
   (RED vs `:=`, GREEN vs `max()`). **Liveness is mandatory**: a legitimate higher token still advances it —
   *"never decreases"* is trivially satisfied by a fence that never updates.
4. **Docs-honesty arm** — every provider our shipped docs list as fencing-capable must have a
   **non-skipped** conformance arm proving it. RED today on SqlServer; GREEN on Postgres/Oracle.
5. **`sicpvm` un-skip must land in the SAME COMMIT as the fix** — a durable fence shipping while its proving
   arm stays `SkipIfPending` is a guarantee asserted with its enforcing test disabled.

## 5. Measurements worth keeping

- `eng/ci/shipped-ddl-sweep.sh --sweep` → **`GATE_EXIT=2`, `ELAPSED_SEC=546`** (9m06s), exit captured
  directly with no pipe. A **9-minute pre-commit gate** is concrete evidence for the gate-latency item and a
  direct cause of the `--no-verify` authorisation.
- `tests/Shared/Tests.Shared` clean `--no-incremental` rebuild at `d8b5c6185` → **`BUILD_EXIT=0`**, 1 warning,
  0 errors. **Compile evidence, not execution evidence** — supporting only.
- Re-running the suite was **environmentally impossible**, not merely unbudgeted: any result measured on a
  volume flagged *Full Repair Needed* is uncitable in either direction (a RED could be corruption, a GREEN a
  stale artifact).

## 6. The night's pattern — five instruments, one shape

Guards deleted but still apparently invoked · a schema gate blind to a naming convention · a docs build dead
since 2026-07-20 · test evidence pinned to a superseded SHA · **and this log, which presents itself as the
durable record and is gitignored.**

**Not one reported a failure. They reported nothing — and nothing reads as fine.**

The fifth is the sharpest: the corpus rule I cited repeatedly tonight says a correction must reach *the
durable artifact, not just the thread*. **The artifact we were all writing to was never durable.**
