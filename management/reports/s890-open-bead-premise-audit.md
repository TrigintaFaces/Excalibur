# S890 — Open-Bead Premise Audit (pilot)

**Author:** DocumentationWriter · **Date:** 2026-07-15 · **Status:** pilot, 6 of 394 open beads
**Closes nothing.** Every verdict below is a measurement handed to the integrator, who owns closure.

---

## Why this exists

ProjectReviewer's law, found by measuring the closed backlog: *a closed bead whose title asserts a
**relationship** ("X is not wired to Y") is suspect and mechanically re-checkable.* He proposed the
audit and said *"say the word."* Nobody said the word.

Run **forward** — against the **open** backlog — the same law holds with the sign flipped: **an open
bead whose title asserts a measurable state is equally suspect, and equally cheap to check.** The
tracker drifts in **both** directions:

| direction | example | cost |
|---|---|---|
| **closed, NOT done** | `jxp2yq` — its own title still true 4 days after closure | 10h of re-derivation by 3 agents |
| **done, NOT closed** | `ufv8ij` — **verified**: fix at HEAD (`StageMessageAsync` ×2, phantom `OutboxStatus.Pending` **0**), bead still `open`. Control: 9510 beads parsed from committed HEAD. | an unplannable 394-bead backlog |

> **🔻 CORRECTED 02:13 — this row originally read `69je5c` `w1u1c9` `0p6z0v` `ufv8ij`. I put `0p6z0v`
> in the "done" column and it does not belong there.** *Its title is **"Agent hangs mid-turn after a
> clean REWAKE — mechanism unknown."* Tonight we **characterised** that hang (the three-state model;
> `REWAKE`-without-`START` as the discriminator; measured to 3h52m with zero intervention) — **we never
> explained it. The mechanism is still unknown. Characterised ≠ solved**, and I filed it as delivered
> because the sprint produced a lot of writing about it.*
>
> *`69je5c` / `w1u1c9` are **unverified by me** and are removed from the row rather than relabelled —
> I have not measured their deliverables at HEAD, so I have no business putting them in either column.*
>
> **Caught by applying FrontendDeveloper's correction of the integrator's table to my own, one minute
> later.** *He found one bead mis-columned in someone else's artifact; the same query found one in mine.
> **A table with four items and one verified item is not a finding — it is three guesses wearing a
> measurement's clothes.***

The embarrassing direction got ten hours. The harmless-looking direction quietly inflates the number
we are asked to drive to zero. **Same root, and PR measured it: *"it was never the status field —
nobody reads the tracker."***

## Method

For each bead, extract the falsifiable claim from its **title**, then measure it — **with a positive
control on every negative** (a zero from a query that cannot match is not a finding).

## Results — 6 audited, ~5 minutes

| bead | P | title claim | verdict | evidence |
|---|---|---|---|---|
| `iz4rly` | P1 | "committed 3212 vs worktree 9482 — 6270 uncommitted, 5 days" | **STALE → close candidate** | committed **9491** vs worktree **9506** → gap **15**. `032eecacd` restored the 3212 hours ago. Trend, not one point: 9482 → 9486 → 9491. |
| `34k958` | P1 | "docs-site outbox.md DDL omits `error_message`" | **AC1 SATISFIED → partial close** | `error_message` present (**2** hits; control: 6 `CREATE TABLE`). Fixed by `0533e4753`. **AC2/AC3 remain open.** |
| `vmy75v` | P1 | "fencing fail-fast is in the OutboxProcessor CTOR, not a startup validator" | **HOLDS → real work** | `OutboxProcessor.cs:281`, inside the ctor block: `if (_fencingActive && ...GetService(typeof(IFencedOutboxStore)) is null) throw new InvalidOperationException`. |
| `exhkgt` | P1 | "f5-sweep is NOT wired to any real trigger" | **HOLDS → real work, but NOT for the reason the title implies** | See the refinement below — my first reading was true and would have licensed a wrong fix. |
| `nu00yn` | P1 | "gate-wiring ARM1 is BLIND to `.claude/harness/*.harness-lock.sh`" | **HOLDS → real work** | `gate-wiring.sh:41` — `LOCK_DIRS="${GATE_WIRING_LOCK_DIRS:-$CI_DIR $HOOK_DIR}"`. `.claude/harness` is **never enumerated**. |
| `3owddx` | P1 | "`bd update --notes` is a DESTRUCTIVE OVERWRITE not an append" | **PARTIAL — premise true, guarded** | `bd`'s behaviour is unchanged, but `pre-tool-use.sh` now **denies** it with a teaching reason + `BD_NOTES_OVERWRITE_ACK=1` escape (**3** refs; control: 11 `permissionDecision`). |

**Score: 2 close candidates · 3 hold · 1 partial.**

## Batch 2 — 4 more, ~4 minutes (all open P0/P1)

| bead | P | title claim | verdict | evidence |
|---|---|---|---|---|
| `jxp2yq` | P0 | "No pipeline invokes ANY of the four gates" | **SPLIT — half stale, half TRUE** | See below. **The four are wired to the pre-commit hook (tracked *and* installed, verified) — and to ZERO of 20 CI workflows.** |
| `dqxsgt` | P1 | "`_run_gate` conflates REFUSE with FAIL: `if ! bash $s` blocks on ANY nonzero" | **HOLDS → real work** | `eng/hooks/pre-commit:123` — `if ! bash "$REPO_ROOT/$s"; then` — verbatim. Control: 617 lines read. |
| `hv8fjh` | P0 | "bd-flush-guard's caller swallows every genuine flush failure" | **INCONCLUSIVE — do not act on this line** | `:304` reads `… --verify-staged && vstaged_rc=0 \|\| vstaged_rc=$?` — that **captures** the exit correctly. Whether a later branch discards `vstaged_rc` is unread. **Title not reproduced at the cited seam; needs the full caller traced before anyone "fixes" it.** |
| `wqd1w1` | P0 | "11 of 14 bd-reading gates are on the lossy daemon path" | **NUMBERS DO NOT REPRODUCE — my query, not theirs** | Mine: **9** bd-reading scripts, **7** `--no-daemon`, **2** lossy. Theirs: 14/11. **Different query, not a refutation.** The *kind*-claim (lossy-daemon reads exist) reproduces; **the count does not, and I am not publishing mine as a correction of theirs.** |

### `jxp2yq` — the sharpest result in either batch, and it inverts on one word

```
                       CI workflow      installed pre-commit hook
f5-sweep                    0                     2
blocking-bead-gate          0                     3
hooks-wiring                0                     6
gate-wiring                 0                     6
CONTROL: 20 workflow files present; 17 contain 'dotnet' -> the search REACHES CI.  The zeros are real.
```

**The bead says *pipeline*.** *By that word the premise is **TRUE and P0**: **not one** of these gates runs in CI. They are **local-commit-time only** — they gate the machine of whoever happens to commit, and **nothing** re-checks them on the branch.*

**By the word *invoked* the premise is stale — all four are wired, and I verified the wire on the bytes git actually runs.** *This is `exhkgt`'s lesson recurring: a title-level check tells you whether the premise **reproduces**, never **what to do about it**.*

**🔻 This paragraph had to be rewritten 50 minutes after it was written, BY THE DIVERGENCE IT DECLARED OVER.**

> **AT 02:00 I WROTE:** *"`tracked == installed` is **YES** right now. The 617-vs-508 divergence recorded in `dev-team-6.md` has since been closed by someone re-installing. That doctrine's own headline example is no longer live."* — **Every word was true when written. All of it was false 50 minutes later.**

```
AT 02:52 (found by ProjectReviewer, reproduced here):
  tracked (worktree) : 634     committed (HEAD) : 634     INSTALLED (RUNS) : 617   *** DIVERGED ***
  core.hooksPath -> .git/hooks   ->  git runs the INSTALLED copy

  bd-file-readback-nodaemon   installed: 0   tracked: 2    <- TONIGHT'S WIRE. INERT.
  CONTROL: staged-secret-scan installed: 9   tracked: 9    <- the grep works. The 0 is REAL.
```

> ### **A lane closed tonight on BOTH halves — impl, and a lock PROVEN TO FIRE — and the wire sits in the file git does not run. It is at HEAD. HEAD is not what executes. COMMITTED, NOT LIVE.**

**That is `dev-team-6.md`'s doctrine, in the exact words it uses, on the night that doctrine was committed** — *and I wrote those words, measured the divergence closed at 02:00, and cited the closure as evidence **in this report**. The re-divergence took **50 minutes**, and I would have shipped the stale claim.*

> **🔻 AND THIS PARAGRAPH WAS WRONG TOO, for eight minutes, in the opposite direction.** *It originally
> read: "ProjectReviewer read 579/595; I read 617/634. **Both correct — commits landed between the two
> reads.** Third demonstration tonight of this report's own rule: every count is a photograph." **That
> explanation is FALSE.** Measured:*
>
> ```
> eng/hooks/pre-commit    wc -l (total): 634     grep -c . (non-empty): 595     <- THE SAME FILE.
> difference = 39 blank lines.        I ran `grep -c ''` (ALL lines). He ran `grep -c .` (non-empty).
> ```
> **Nobody's number was stale. Nobody's file was different. We ran two different counters and I
> explained the gap with the night's favourite lesson instead of measuring it.** *ProjectReviewer found
> this by retracting his own accusation that the integrator's control was fabricated — it was his
> counter, not the integrator's number.*
>
> ### **I reached for "every count is a photograph" because it was TRUE ALL NIGHT and it FIT. It was a narrative, not a measurement. A plausible lesson applied to an unmeasured discrepancy is exactly the failure this report catalogues — and it is the only one I committed while WRITING THE CATALOGUE.**

**What survives, unchanged and independently confirmed by three people:** *the installed hook **was**
diverged (`617/579` total/non-empty vs tracked `634/595`) and **tonight's wire was in `tracked: 2` /
`installed: 0`** — inert. The integrator re-installed; all three surfaces now agree. **The divergence
finding was always real. Only my explanation of the two reviewers' differing numbers was invented.***

**Batch-2 score: 1 split (half P0-true) · 1 hold · 1 inconclusive · 1 count-not-reproduced.**
**Two of four came back "I cannot settle this from the title" — which is the audit working, not failing.**

## Batch 3 — 3 more (2 product-code, 1 gate)

| bead | P | title claim | verdict | evidence |
|---|---|---|---|---|
| `0iwn8h` | P1 | "premise-triage gate has a self-test trigger but NO functional trigger and is not in CI: it has never run" | **HOLDS — exactly as titled** | See below. **The gate is invoked by nothing.** |
| `dvp6ve` | P1 | "DecorateSnapshotStore/DecorateEventStore bind only `services.LastOrDefault(ISnapshotStore)`" | **HOLDS → real work** | `EventSourcingUtilitiesServiceCollectionExtensions.cs:368` `services.LastOrDefault(sd => sd.ServiceType == typeof(ISnapshotStore))`; **:404** same for `IEventStore`. Control: `LastOrDefault` in 8 src files. |
| `lz7us9` | P1 | "Outbox family has THREE divergent MarkFailed post-conditions" | **NOT SETTLEABLE FROM THE TITLE** | 43 files touch `MarkFailed`. A 3-way post-condition divergence needs each implementation read, not grepped. **Deliberately left unaudited rather than guessed.** |

### `0iwn8h` — the whole finding is a one-token diff from its own sibling

```
WIRED     :136   _run_gate "f5-sweep"        eng/ci/f5-sweep.sh  eng/ci/f5-sweep.test.sh
                                             ^^^^^^^^^^^^^^^^^^  the GATE, plus its self-test

UNWIRED   :138   _run_gate "premise-triage"  eng/ci/premise-triage.test.sh
                                             ^^^^ the SELF-TEST ONLY. The gate is never named.

CI workflows naming premise-triage: 0        (control: 20 workflow files exist)
```

**`premise-triage.sh` is invoked by nothing.** *Its self-test fires only when someone edits the gate — so
the artifact is **alive** (a green appears, a name prints) while the gate **has never run against its
subject even once.** Compare `exhkgt`, where the gate at least reaches `_run_gate` and merely inherits
the wrong trigger; here it never reaches the argument list at all.*

> ### **A gate that has never run, in the gate directory, during a sprint about gate honesty — and its own self-test goes green, which is precisely why nobody noticed.**

*This is the advertised-but-unwired class (ADR-336) applied to the enforcement layer itself, and it is
the third distinct instance this audit has found in `eng/ci/` — after `exhkgt` (wrong trigger) and
`jxp2yq` (no CI at all).*

**Batch-3 score: 2 hold · 1 declined-as-unsettleable.**

## Batch 4 — product code. Two findings, both needing files READ, not grepped.

| bead | P | verdict | evidence |
|---|---|---|---|
| `j1wfzu` | P1 | **HALF HOLDS — and the half that holds is a family-parity miss** | below |
| `xdcr3t` | P1 | **HOLDS — and it is `rw2ull` (S886) verbatim, one family over** | below |

### `j1wfzu` — the premise is half true, and the fix it implies is unsafe

```
MarkMessageDeadLettered   SET Status=5, …, LeasedAt=NULL, LeasedBy=NULL     ✅ clears
MarkMessageFailed         SET Status=3, LastError=…, RetryCount=…, …        ❌ no clear   <- TRUE half
MarkMessageSent           SET Status=2, SentAt=…, LastError=NULL, …         ❌ no clear   <- kills the
                                                                                 "all 3 siblings do" half
```
**Title says *"while all 3 sibling terminal transitions do."* Only ONE does.** *"Make Failed match its
siblings" is therefore the wrong prescription — **the siblings don't agree with each other.***

**Severity, corrected by ProjectReviewer before it reached a bead** — my first framing said *"may never
retry"* and that is **false**:
```
GetUnsentMessagesRequest:65   AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)          <- the backoff
:66                           AND (LeasedAt IS NULL OR LeasedAt < now - @LeaseTimeout)      <- the lease
```
**Both gates apply, so the LONGER one wins.** *Retry is **delayed** to `max(NextAttemptAt, LeasedAt +
LeaseTimeout)`, not lost — **the computed backoff schedule is silently discarded**. No data loss. And
`MarkFailed` is the only transition that both sets a backoff and leaves a lease behind.*

**Family parity, measured at HEAD by BackendDeveloper against his own lane:** Postgres **1** (his S889
fix), Oracle **1** (S889 — *its commit message says "Liskov family parity"*), **SqlServer 0.** *Two of
three fixed; the word "parity" written in the second one's commit message; the third left.*

### `xdcr3t` — a rule promoted three days ago did not prevent its own defect class

```
PostgresProjectionStoreExtensions.cs
  :45  services.TryAddScoped<IProjectionStore<TProjection>>(sp =>    <- EXPLICIT factory: no auto-resolve
  :52      return new PostgresProjectionStore<TProjection>(
  :56          options.Value.JsonSerializerOptions);   <- 4 args. tenantContext (5th) NOT PASSED.
PostgresProjectionStore.cs
  :72  ITenantContext? tenantContext = null            <- optional; silently defaults
  :124 ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);   -> THROWS on GetById/Upsert/Delete
  :182 TryAddSingleton<ITenantScopingCapability<IProjectionStore<object>>, …>
       ^^^ the MARKER, registered SEPARATELY + UNCONDITIONALLY, attesting a wiring the factory omits
CONTROL: 184 lines read · ITenantContext in 87 src files · ThrowIfNullOrWhiteSpace ×7 in the store
```
**A mention-count would have MISSED this** — a *type-based* registration would auto-resolve the arg.
Only reading the factory shows it doesn't.

**`enforce-invariants-structurally.md`, promoted 2026-07-13 from `rw2ull`:** *"A capability marker MUST
be emitted by the SAME seam that performs the wiring it attests — never registerable on its own."*
**That rule names this exact shape, is in every agent's context, and did not prevent it in a sibling
family three days later.**

**Honest severity difference from `rw2ull`:** *that one **leaked silently**; this one **throws** — fails
closed, a **dead** family, not a leaking one. Lower severity, **worse in one way**:
`RequireTenantScopingCapability` **passes** on the bare marker, so `AddMultiTenancy` admits a family that
cannot serve a request. **The fail-closed check certifies the DOA.***

**Batch-4 score: 1 half-hold · 1 hold. Both required reading 4 files; neither was settleable by grep.**

## Batch 5 — a duplicate pair, and the stale one is the one still asking the question

| bead | P | verdict |
|---|---|---|
| `su6232` | P1 | **PREMISE CONFIRMED → real work. Proposed canonical.** |
| `x3pr0n` | P1 | **DUPLICATE of `su6232` → close candidate. Owner rules; this report closes nothing.** |

**The premise, verified at the bytes:**
```
SqlServer  GetUnsentMessagesRequest.cs:68   ORDER BY PartitionKey, SequenceNumber ASC   <- HONORS it
SqlServer  InsertOutboxMessageRequest.cs    SequenceNumber mentions: 3                  <- PERSISTS it
Postgres   InsertOutboxMessage.cs           sequence_number mentions: 0                 <- DROPS IT
CONTROL: the same grep finds 3 in SqlServer's INSERT -> it reaches these files. The 0 is real.
```
*A consumer sets a per-partition `SequenceNumber`; SqlServer persists it and drains in that order;
Postgres silently discards it. Same advertised guarantee, two providers, opposite behaviour.*

**The duplication — same split, 60 seconds apart:**
```
su6232   04:42   FULL description. Carries the PdM RULING ("…IS an advertised guarantee… Align UP…
                 Down-align PROHIBITED"), the file:line evidence, and the scope bounds
                 (per-partition strictly-increasing, NOT global total order).
x3pr0n   04:43   No description. Its TITLE restates the same subject as an OPEN QUESTION —
                 "PdM rules guarantee scope (align UP … vs documented per-provider difference)"
                 — a decision su6232 records as ALREADY MADE.
both     [discovered-from] owxhc8
```
> **A bead whose title asks a question its own sibling — created one minute earlier, by the same split
> — already answered. Nobody read the sibling.** *ProjectReviewer's law with the sign flipped once more.*

**Not closed here, deliberately:** *`coordinate-before-parallel-work` — dedup is single-actor, canonical
agreed FIRST by the owner. And `forge-integration-conventions` cl.8 — a close-as-dup needs the
canonical's **delivered** scope to cover the dup's deliverable; **neither is delivered**, so this is a
scope merge for the ProjectManager, not bookkeeping.*

**Batch-5 score: 1 confirmed · 1 dedup candidate (−1 bead, if the owner agrees).**

---

## Running total: 17 of 394 audited

**7 hold · 2 close-candidates · 1 split · 3 declined (inconclusive / count-not-reproduced / needs-reading).**

> **The declines are the reason to trust the holds.** *An audit that always returns a verdict is not
> measuring — it is generating. **Three of thirteen came back "I cannot settle this from the title,"
> and each one names what it would take to settle it.***

## Refinement on `exhkgt` — my verdict was true and would have licensed the WRONG FIX

**Credit: ProjectReviewer, who separated two things I had merged.** My first reading — *"self-guarded,
never runs on a C# diff"* — is **true**, and a reader acting on it would remove the self-guard and
**break a correct, documented design.**

```sh
:135   case "$staged_paths" in *"eng/ci/f5-sweep."*)
:136       _run_gate "f5-sweep"  eng/ci/f5-sweep.sh  eng/ci/f5-sweep.test.sh ;; esac
                                 ^^^ THE GATE         ^^^ ITS SELF-TEST
                                 both under ONE trigger
```

**`_run_gate`'s own text says what it is for:**

```
:120   echo "Staged ${label} — proving its gate self-test(s)..."
:124   "✗ COMMIT REJECTED: ${s} is RED. You changed a gate; its self-test no longer holds."
:113   "a lock that fires when you change the thing it guards is a lock that gets read;
        running the whole set on every commit would be slow and get bypassed"
```

**The staged-subject trigger is deliberate, documented, and CORRECT — for self-tests.** The defect is
that the **gate** was passed in the same argument list as its **self-test**, so it inherited a trigger
that is right for one and wrong for the other:

| script | its actual subject | staged-subject trigger is |
|---|---|---|
| `f5-sweep.test.sh` | **f5-sweep.sh itself** | ✅ **CORRECT** — fires when its subject changes |
| `f5-sweep.sh` | **a C# diff** | ❌ **WRONG** — never fires on its subject |

**The fix is to SPLIT the trigger, not to remove the self-guard.** *"Self-guarded"* is the right
observation and the wrong prescription — **one predicate (`is this arg a `.test.sh`?`) separates a
correct design from a real defect**, and my table had flattened them into one verdict.

**This is the audit's own limit, demonstrated on the audit:** a title-level check tells you *whether
the premise reproduces*, **never *what to do about it***. `exhkgt`'s premise reproduces. Its title's
implied remedy is wrong.

## The property that makes it trustworthy

**It clears AND it confirms.** Three came back **TRUE** — including two the author had every reason to
want cleared. **An audit that only ever closes things is a rubber stamp with extra steps.** The
`vmy75v` / `exhkgt` / `nu00yn` verdicts are the evidence that the `iz4rly` verdict means something.

## Honest limits

1. **One of the six checks failed silently, in this audit, on this page.**
   `grep -rc 'error_message' src/**/SetOutboxMessageFailed.cs` → **empty** — bash does not expand `**`
   without `globstar`. **A failed query, not a zero.** It was caught only because the *other* half of
   that check already settled the verdict. Had it stood alone, this report would carry a fabricated
   finding. **The audit's own results need the same controls as everything else** — an argument for
   running it, not against it.
2. **Titles only.** A bead's title can be true while its acceptance criteria are wrong, or vice versa.
   `34k958` is exactly that: AC1 satisfied, AC2/AC3 live. **A "close candidate" here means "the
   title's premise no longer reproduces," not "the work is done."**
3. **Closes nothing.** Verdicts are input to the integrator. Verify at HEAD before acting
   (`forge-integration-conventions` cl.6) — every grep above is re-runnable in seconds.

## The yield, and the honest cost

**17 audited · 6 real findings · 4 declines · 3 close-candidates · 4 of my own claims corrected.**

| finding | what it is |
|---|---|
| `jxp2yq` **P0** | 4 gates run in **0 of 20** CI workflows — the quality apparatus is one machine's local copy |
| `0iwn8h` | a gate **invoked by nothing** — its self-test goes green, the gate has never run |
| `xdcr3t` | `rw2ull` verbatim, one family over, **3 days after the rule against it was promoted** — and the guard it defeated was **visibility, not inseparability**: `ITenantScopingCapability<T>` is **public**, so three assemblies re-declared the "internal" marker in 15 lines each (**4 declarations at HEAD**; found by ProjectReviewer) |
| `su6232` | Postgres silently drops the `SequenceNumber` SqlServer persists **and orders the drain by** — same advertised guarantee, opposite behaviour |
| `j1wfzu` | SqlServer left out of a 2-of-3 "family parity" fix — **the word is in the other one's commit message** |
| `dvp6ve` | `LastOrDefault(ISnapshotStore)` binding, confirmed at `:368`/`:404` |

**Four of the six are the same shape**, and it is the shape this whole sprint is named after:
```
0iwn8h   a gate that has never run          -> advertised, unwired
xdcr3t   a marker attesting a wiring nobody performed, unboundable by the guard chosen
j1wfzu   "family parity" written in a commit that achieved it for 2 of 3
su6232   an ordering guarantee one provider honours and another discards
```
> **Not one is a bug in code that was written wrong. Every one is a claim the codebase makes about
> itself that isn't true — which is exactly what a premise audit is FOR, and why the tracker was the
> right place to point it.**

**Cost correction — the pilot's "~50s/bead" does NOT hold.** *The two findings that mattered most
(`xdcr3t`, `j1wfzu`) each took **reading four files**. **Neither was settleable by grep**, and a
grep-only pass would have returned the wrong answer on both — `xdcr3t` looks fine unless you notice the
registration is an explicit factory rather than type-based. **Budget ~2h for the title-level sweep; the
verdicts it produces are triage, and the real ones cost minutes each after that.***

**The four corrections are the point, not the embarrassment:**
```
"7 of 26 gates read live surfaces"      -> 2. My own dividing line shrank it. I published the 7 first.
"3 lossy bd readers"                    -> 0. All three carried `# bd-status-ok` — AN ALLOWLIST MARKER —
                                             on the same line my grep printed. I pasted it unread.
"0p6z0v is delivered"                   -> its title says "mechanism UNKNOWN". It still is.
"MarkFailed -> may never retry"         -> delayed, not lost. :66 reclaims expired leases.
```
> **Every one was caught by a colleague running the query I should have run, or by me running theirs.
> None was caught by a gate. An audit whose author is wrong four times in two hours is only worth
> anything because five other people were reading it.**

## Rescued from a bd comment, because bd comments are not durable

**`r4dzl2` (REOPENED) — scope re-derivation input. Measured at HEAD 03:32Z. Not a ruling.**

> **🔻 THE RATIONALE IN THIS PARAGRAPH WAS FALSE FOR FIVE MINUTES. It originally read: *"posted as a
> `bd comment` at 03:34 and **can never reach git** — ProjectManager measured `bd export` dropping
> comments entirely, controlled three ways."* **I amplified another agent's claim into my own artifact
> without measuring it. FrontendDeveloper caught it. Measured:***
>
> ```
> .beads/comments.jsonl   TRACKED (git ls-files)   <- comments have their OWN file
> .beads/issues.jsonl     what the export wrote, and what PM grepped — the WRONG FILE
>
> my r4dzl2 comment:  at HEAD 0 · worktree 1        <- present, simply not yet committed
> CONTROL: my 20:00 rq6iry comment AT HEAD -> 1     <- comments DEMONSTRABLY reach git
> CONTROL: comments.jsonl  HEAD 9491 · worktree 9516  (25 uncommitted records)
> ```
> **PM's controls were sound and pointed at the wrong file. `bd export -o issues.jsonl` writes ISSUES;
> comments live elsewhere and are tracked. A `bd comment` reaches git when `comments.jsonl` is staged.**
>
> *Two agents — PM measuring, me amplifying — published "comments can never reach git" within five
> minutes, and **the disproof was one `git ls-files .beads/` away**. My fourteenth error, and the
> second one tonight caused by agreeing with a measurement instead of running it.*

**The rescue below stands anyway, and now for the right reason:** *a finding whose only home is a tracker
comment depends on `comments.jsonl` being staged by an integrator who has a **standing rule against
touching it** (`NEVER git add -A` — the daemon re-clobbers it, `2t6lfx`). **Belt and braces: git holds
this copy unconditionally.***

**The bead's description says `SITE COUNT SETTLED: 6 real`. A widened grep finds SEVEN rc dispatches:**
```
:41-42   freeze_rc   -ne 0                  fail-closed, correct, NOT a `case`
:233     clob_rc     case                   three-state
:261     desc_rc     case                   three-state
:322     vstaged_rc  case                   three-state
:434     secret_rc   case                   three-state
:626     dup_rc      -eq 1                  THE HOLE — site 6, confirmed
:372-373 gate_rc     -ne 0 && -ne 3         fail-closed w/ documented exemption — ***NOT IN THE LIST***
```
**Either the enumeration was six-of-seven, or `:373` is deliberately out of scope. Unresolved — it is
BackendDeveloper's list.** *Flagged because the reopen exists to **re-derive** scope, not re-read it.*

> **✅ RESOLVED, 05:15, by BackendDeveloper — and by the method this paragraph asked for.** *He
> re-enumerated **by VARIABLE**, idiom-independent, rather than by grep pattern:*
> ```
> clob_rc case ✓   desc_rc case ✓   secret_rc case ✓   vstaged_rc case ✓
> freeze_rc -ne 0 ✓ (fail-closed, correctly untouched)
> gate_rc   -ne 0 ✓ (fail-closed + its DECLARED -ne 3 exemption)   <- THE 7TH. Not a gap. Correct.
> dup_rc    case  ✓ <- WAS -eq 1. THE 6TH SITE. Cut.       VIOLATIONS: 0
> ```
> **`:373` was never a defect — it was the second form my grep couldn't see, and the enumeration was
> seven-of-seven once someone stopped enumerating by idiom.** *The disposition came from the gate's own
> declared intent, not from a guess.*
>
> **This is the audit's one closed loop:** *my instrument failed → I published the failure rather than
> the finding → the person who owned the list used a better method → the question resolved in ~100
> minutes. **The value was never my verdict. It was admitting which query I'd run.***

> **And the honest limit on my own instrument, which is the durable part:** *the grep I published as
> "4 three-state / 1 `-eq 1`" **missed `freeze_rc` entirely** — my pattern matched only `case` and
> `-eq 1` and could not see `-ne 0`. **I reported five dispatches in a file whose own bead names six,
> and never noticed the arithmetic didn't close.** Anyone re-deriving this scope must widen past both
> forms to **every `rc` capture site**.*

## If someone wants the rest

**149 of the 394 open beads have titles asserting a measurable state.** *The output is a triage list —
**"the premise reproduces" is not "here is the fix," and three times tonight the title's implied remedy
was wrong** (`exhkgt`, `j1wfzu`, `wqd1w1`). Use it to decide what to look at, never what to do.*
