# S889 REVIEW_ARCH (ORACLE) — Architect Review

- **Reviewer:** SoftwareArchitect
- **Mission:** 237 / sprint-889 — Design-Audit Hardening (Dijkstra/Liskov/Metz) + S888 correctness carry
- **Reviewed SHA:** `c75e74571` (= `8535990b9` + one docs-only evidence commit; **no remediation landed at review time**)
- **Baseline:** `6cc8e6bf4` — 94 `.cs` files, +2945/-652
- **Pass type:** SPLIT (see below)

## Split-pass declaration

REVIEW_ARCH was dispatched while remediation for REVIEW_CODE's 4 BLOCKING findings was in flight. Reviewing
bytes that are being rewritten produces a report stale on arrival, so this pass is deliberately split:

- **Covered now (stable under remediation):** seam/boundary architecture, the fold-exclusivity class, ADR
  conformance, public surface, security, AOT.
- **Deferred to a post-remediation delta re-verify:** `g3do61`, `9u1s94`, `41dbu7`, `von6yn` — I re-verify
  those on the post-remediation committed HEAD, not on `c75e74571`.

---

## ARCH-1 — The D1 tenant-scoping seam is HONEST but NOT EXCLUSIVE

**Severity: P1 architectural (not S889-blocking — consequence today is fail-closed, not a leak).**
**Tracked:** `y9ytup` (P2 — under-scoped; this is the architectural root). Related: `xdcr3t` (P1).

### Verified at src on `c75e74571`

```
4 declarations of `internal sealed class TenantScopingCapabilityMarker<T>`:
  Excalibur.Dispatch.Abstractions/DependencyInjection/TenantScopedStoreServiceCollectionExtensions.cs:102  ← sanctioned (emitted by the fold at :91-92)
  Excalibur.EventSourcing/DependencyInjection/TenantScopingCapabilityMarker.cs:16                          ← per-assembly copy
  Excalibur.EventSourcing.Postgres/DependencyInjection/TenantScopingCapabilityMarker.cs:15                 ← per-assembly copy
  Excalibur.EventSourcing.SqlServer/DependencyInjection/TenantScopingCapabilityMarker.cs:15                ← per-assembly copy

The gate — MultiTenancyServiceCollectionExtensions.cs:236:
  if (!services.Any(static d => d.ServiceType == typeof(ITenantScopingCapability<TContract>))) throw ...
  → a PURE PRESENCE check.

The token — ITenantScopingCapability.cs:33:
  public interface ITenantScopingCapability<TContract>;      ← PUBLIC and EMPTY (zero members)
```

`AddTenantScopedStore` makes marker⊗wiring inseparable **when used** (`:77` resolves `ITenantContext` and
threads it into construction; `:78` emits the marker). That half is correct and is the S886 `rw2ull` fix
working. But **nothing makes `AddTenantScopedStore` the only way to construct the store**, and nothing makes
the marker unforgeable: the interface is public and empty, so a bare marker is a one-liner, and each provider
assembly already declares its own copy. **Enforcement is convention, not structure.**

### Root cause: the seam is UNDER-EXPRESSIVE — this reframes the fix

The 3 surviving bare markers are **not** author sloppiness. They are shapes
`AddTenantScopedStore<TContract, TStore>` **structurally cannot express**:

| Site | Shape | Why the fold can't express it |
|---|---|---|
| `EventSourcingBuilderExtensions.cs:237` | `IEventStoreErasure` | Attests a **contributor** honors tenancy (`BeginScope` + fail-closed erase/is-erased requests). **There is no store to build** — the fold requires constructing a `TStore`. |
| `PostgresProjectionStoreExtensions.cs:182` | `IProjectionStore<object>` | An explicit **family-level token** — its own doc: *"not the shape of any individual projection store"*. The fold binds **one** contract to **one** store; it cannot express an open-generic family. |
| `SqlServerProjectionStoreExtensions.cs:230` | `IProjectionStore<object>` | Same. |

Because the seam has no sanctioned form for these, the authors reached for the only escape hatch available —
their own internal marker copy — which is **precisely the `rw2ull` pattern the fold exists to eliminate**.

**Therefore the fix is NOT "delete the 3 bypasses."** That would remove capability the framework legitimately
needs. The seam must first be able to *say* these things.

### Structural direction (S890, rule together with `owxhc8`/`su6232`)

1. Give the seam **sanctioned forms** for the two missing shapes — a contributor-honored contract, and an
   open-generic family — still **dep-gated on `ITenantContext`** so the attestation cannot be made without
   the dependency it attests.
2. **Then** delete the 3 per-assembly marker copies.
3. Make `ITenantScopingCapability<T>` **`internal` to Abstractions + `InternalsVisibleTo` MultiTenancy.** This
   makes a bare marker **inexpressible** — you cannot implement an inaccessible interface — converting
   convention into structure per `enforce-invariants-structurally`. Per `internal-first-api`, the public+empty
   marker has **no documented consumer scenario**; the burden of proof is on public, and it isn't met.

### ARCH-1a — the family token is independently unsound

`ITenantScopingCapability<IProjectionStore<object>>` is **one token vouching for N closed
`IProjectionStore<T>` that nobody checked**. That is the *mechanism* by which `xdcr3t`'s DOA family passes
the gate: the marker says "my projection stores are tenant-capable" while all 5 construction sites omit the
`tenantContext` ctor arg. A presence signal that generalizes across an open generic cannot be sound —
it attests for types that do not exist yet.

**Fails closed (`ArgumentNullException`), does not leak** — hence P1, not P0. Tracked in `xdcr3t`.

---

## ARCH-1b — API DESIGN: `ITenantContext? tenantContext = null` is the shape that manufactures this class

**Severity: P1 architectural (S890 seam work — greenfield, so the correct contract is free).**

The `/api-design-reviewer` pass over the `PublicAPI` baseline delta found the sprint's real surface change is
not the marker — it is a **repeated optional trailing dependency**:

```
Excalibur.Inbox.Oracle.OracleInboxStore(..., ITenantContext? tenantContext = null)
Excalibur.Outbox.SqlServer.SqlServerOutboxStore(..., ITenantContext? tenantContext = null)
  (+ the same shape at the projection-store ctors and SagaBuilderSqlServerExtensions:146)
```

**The framework already has a non-null way to say "no multi-tenancy":**

```csharp
// SingleTenantContext.cs — internal sealed, a textbook Null Object
internal sealed class SingleTenantContext : ITenantContext
{
    internal const string DefaultTenantId = "__default__";
    public string? TenantId => DefaultTenantId;
    public bool HasTenant => true;
}
```

`AddDefaultTenantContext()` exists precisely so `GetRequiredService<ITenantContext>()` **always resolves**.

So `ITenantContext? = null` is a **second, redundant, and unsafe way to express "single tenant"** — and it
defeats the Null Object the framework already ships. Its two failure modes are the sprint's two headline bugs:

| null flows to | Result | Bug |
|---|---|---|
| `TenantScope.FromContext(null)` → `None` → no row predicate | silent **cross-tenant leak** | S886 `rw2ull` |
| `ThrowIfNullOrWhiteSpace(tenantId)` | **DOA** (`ArgumentNullException`) | `xdcr3t` |

Per `microsoft-first`, ctor params are for **dependencies**, and a dependency that silently defaults to
"unscoped" is not a dependency — it is a footgun with a default. **Making the parameter required makes
"built without a tenant context" inexpressible**, which is `enforce-invariants-structurally` exactly:
`SingleTenantContext` becomes the *only* way to say single-tenant, it is non-null, and it is safe.

That one change collapses the whole family: `xdcr3t`'s DOA, `:146`'s dead-end, and the `rw2ull` leak mode all
become unrepresentable — the S885 `bsdbtc` `TenantScope` precedent applied one layer up, at construction.

**Attribution note:** `ITenantScopingCapability` is **pre-existing public**, not a S889 surface change
(verified: no `+`/`-` for it in the Abstractions `PublicAPI` diff). ARCH-1(c) is therefore a change to
*existing* public API — free under the greenfield/no-consumers constraint, but not something S889 introduced.

## ARCH-2 — Dispatch↔Excalibur boundary: CLEAN

Verified across all 94 changed files:

```
Dispatch.Abstractions → Excalibur.{Domain,EventSourcing,Data,Saga,Outbox,Inbox,Compliance} imports:  NONE
  (control: the files carry 2 usings, so the grep reaches them)
changed src/Dispatch/**.cs importing Excalibur.{Domain,EventSourcing,Saga,Outbox,Inbox}:              NONE
```

`AddTenantScopedStore<TContract, TStore>` lives in `Dispatch.Abstractions` and is fully generic — it names no
Excalibur type. Excalibur→Dispatch direction intact. No violation.

---

## ARCH-1c — the seam's own XML doc OVERCLAIMS its mechanism

**Severity: P2 (doc/behavior divergence, not a vulnerability). NEW — surfaced by the security pass.**

`TenantScopedStoreServiceCollectionExtensions.cs:25-33` claims the seam is *"structurally incapable"* of
building a store without a tenant context, because `GetRequiredService<ITenantContext>` *"fails closed when
no `ITenantContext` is registered."*

**The fail-closed arm never fires on 11 of the 14 paths.** Providers call `AddDefaultTenantContext()`
unconditionally *first* (e.g. `OutboxBuilderPostgresExtensions.cs:107`), so an `ITenantContext` is **always**
registered and `GetRequiredService` always succeeds. The real runtime protection is `TenantScope.FromContext`
(fail-closed when multi-tenancy is active but unresolved), **not** DI resolution. No leak — the default
context yields `TenantScope.None`, the correct non-MT behaviour — but the doc's *structural* claim is
stronger than the mechanism delivers.

**This reinforces ARCH-1b and localises the real hole.** Because `AddDefaultTenantContext` guarantees a
non-null context on the folded paths, the **only** way `null` reaches a store is the
`ITenantContext? tenantContext = null` optional ctor param — i.e. exactly the paths that *bypass* the fold.
Make the parameter required and `null` becomes unreachable by construction.

Note the irony that makes `41dbu7` legible: the doc's claim is **accidentally true for saga only**, because
saga is the one family that *forgot* `AddDefaultTenantContext` — so there `GetRequiredService` really does
fail closed, and takes single-tenant liveness down with it.

## AOT + public surface: CLEAN (0 new findings)

Specialist pass over 2,606 added lines (1,195 in `src/`), every negative control-calibrated.

- **AOT:** zero added hits for `Activator.CreateInstance` / `Type.GetType` / `MakeGenericType` /
  `GetProperties()` / `Expression.Compile` / `new Regex(` / reflection `JsonSerializer` /
  `UnconditionalSuppressMessage`. Three non-zero hits adjudicated non-findings: a `SuppressMessage` in a
  **test** project (not shipped); `ServiceDescriptor.Describe(typeof(ISnapshotStore), <factory delegate>, …)`
  (AOT-safe — instance comes from a delegate, `typeof()` is only a key token); and
  `outboxStore.GetService(typeof(IFencedOutboxStore))`, the ISP escape hatch `microsoft-first` explicitly
  endorses.
- **Public surface:** internal-first held — all new non-contract types are `internal sealed`. New public
  symbols all have documented consumer scenarios (`AddTenantScopedStore`; the `SingleActiveWriter`/
  `AsSingleWriter` fencing opt-out; `CanonicalAuditProjection`, justified as cross-assembly-consumed by sink
  packages, which is what an `.Abstractions` package is for).
- **Internal refs in shipped XML docs:** exactly one — the known `von6yn`. The other 26 `S886` hits are `//`
  implementation comments, an explicitly permitted sink. No others.

> **Integrator note (`forge-integration-conventions`):** the working tree carries an **uncommitted fix** to
> `von6yn` — the S886 parenthetical is gone on disk but **still present at committed HEAD:22**. That delta
> needs staging before the tree is final.

## Security: CLEAN (0 BLOCKING, 0 P1)

Specialist OWASP pass over the added lines. **Extraction control exact:** the scanned added-line set totals
**2,945 — matching the diffstat's `2945 insertions(+)`**, so the scan covered the whole diff, not a subset.

| Check | Result | Control |
|---|---|---|
| SQL injection | **Clean** | 2 interpolation hits, both safe. `OracleInboxStore.cs:268` interpolates only `{Table}` + `{tenantPredicate}`; all user data bound (`:MessageId`, `:TenantId`). `{Table}` allowlisted `^[a-zA-Z0-9_]+$` via `[GeneratedRegex]` — **and the validator was verified to actually run** (`ValidateOnStart()` + `TryAddEnumerable`, `OracleInboxExtensions.cs:34-35`). `{tenantPredicate}` is a ternary of two literals. |
| Crypto/RNG | **Clean** | 2 `Guid.NewGuid()`, both test-only ids — no key/secret/token/salt/IV/nonce material |
| Discarded crypto / weak hashes | **Clean** | 0 hits; grep engine proven by the `Guid.NewGuid` matches |
| Secrets in source | **Clean** | 0 hits; 54 string-literal assignments reachable |
| PII in logs/exceptions | **Clean** | 0 hits; only 3 log/throw lines added |
| Deserialization | **Clean** | 0 `BinaryFormatter`/`TypeNameHandling`; `public`=139, `Async`=122 prove reach |
| DoS / regex | **Clean** | 0 unbounded `new Regex(`; `commandTimeout` set on all 5 new Oracle/Postgres commands |
| ASP.NET authz/CSRF/CORS | **Clean** | 0 hits — **232 added lines from controller/AspNetCore files** prove the grep reaches them, so the zero is real |

**Net security assessment: this diff is an improvement.** It deletes 8 standalone provider marker files and
routes outbox/inbox/saga/event-store registration through a single dep-gated seam that hands the resolved
`ITenantContext` into the store factory — closing the "marker registered without the tenant dependency" leak
path. Spot-checked at `OutboxBuilderPostgresExtensions.cs:118`: the factory does receive and use
`tenantContext`.

### Attribution correction to my own ARCH-1

The security pass established what my pass did not: **the 3 bare markers are PRE-EXISTING, not S889
regressions.** Introduced in `75cf9ec91` (Jul 11) and `c1c132413` (Jul 7) — **both predate baseline
`6cc8e6bf4`**. My ARCH-1 stands as an architectural finding, but S889 did not introduce it; S889 *reduced*
the bare-marker count from 13 to 3 and made the residue visible. Correcting because "this sprint introduced
it" would be false and would misdirect S890 scoping.

The specialist flagged **UNVERIFIED** on whether those 3 leak. **I resolve that: they do not.** They fail
closed — `xdcr3t` proves the projection path throws `ArgumentNullException(tenantId)` rather than returning
cross-tenant rows, and the erasure path fails closed on a null discriminator. Wrong-but-closed, not a leak.

## Verdict

**CHANGES REQUESTED — concurring with REVIEW_CODE, on its 4 BLOCKING, not on new architectural blockers.**

I add **no new blocking findings**. The architectural finding (ARCH-1) is real and load-bearing but its
consequence today is **fail-closed**, so it does not gate S889 — it is S890 seam work, ruled together with
`owxhc8`/`su6232`/`z0yczw` and `:146`.

### Ordering constraint (carried from my `41dbu7` ruling)

`41dbu7` (add `AddDefaultTenantContext()` to the 3 saga files) **must land before** any `:146` fold. The
already-folded saga paths `:43`/`:90` are broken for single-tenant *today*, which makes `:146`'s un-folded
residue the **only working single-tenant SqlServer saga path**. Folding it first kills the last one standing.

---

## Tracking

| Finding | Disposition | Bead |
|---|---|---|
| ARCH-1 fold exclusivity / under-expressive seam | Non-blocking → S890 seam ruling | **`y9ytup`** (existing; ruling appended — under-scoped at P2) |
| ARCH-1a family-token unsoundness → DOA family | Non-blocking (fails closed) | **`xdcr3t`** (existing, P1) |
| ARCH-2 boundary | Clean — no action | — |

**No new beads filed:** both findings were already tracked by the Reviewer. Per `issue-accountability`, the
correct action was to append the architectural ruling to the existing beads, not to duplicate them.

## Durability note — **CORRECTED 2026-07-15, and the correction is the point**

**My original note was wrong on all three of its mechanisms. Every one was an N=1 read of a flaky tool.**
It is corrected here rather than quietly edited, because a review report justified by fabricated mechanism
is the exact defect this sprint kept finding in everyone else's work.

**What I originally wrote:** the ruling "is NOT in the git-tracked `.beads/comments.jsonl`" and
"`bd show --json` exposes no comments field at all," therefore "a tracker comment is not durable."

**What measurement actually shows** (looped, after @DocumentationWriter's 2/12 reproduction of `j50ole`):

| Claim (mine, N=1) | Measured | Verdict |
|---|---|---|
| "`bd show` does not display comments" | **1/12 renders** | **FALSE** — flaky, not absent. My zero was a coin flip. |
| "not in `.beads/comments.jsonl` ⇒ not durable" | **present (1 hit)**; control `mcvr5m`=5 | **FALSE** — I grepped *before a flush*. It is durable. |
| "`bd show --json` has no comments field" | 8 runs → `True ERR True False False False True False` | **FALSE** — present ~37%, absent ~50%, **JSON unparseable ~12%** |

**New information for `j50ole`:** the defect is **not** confined to the comment-render path. `bd show --json`
**also** intermittently omits the comments key **and intermittently emits unparseable JSON** (the `ERR`
above). A structured-output path that sometimes returns malformed JSON is strictly worse than a flaky
renderer — every consumer parsing it inherits a silent, intermittent false negative.

**SECOND CORRECTION — my first correction over-generalised too.** I wrote *"the `bd` read path is currently
unverifiable."* **Also wrong**, and wrong in the same shape: I measured **one surface** and generalised to
*the read path*. The team's measured matrix (@ProjectManager, @DocumentationWriter, @ProjectReviewer, CEO —
four independent reproductions):

| surface | rate | verdict |
|---|---|---|
| `bd show <id>` (daemon — **the default**) | **~2/12 (≈17%)** | **BROKEN for comments** |
| `bd show <id> --no-daemon` (direct storage) | **40/40 at N=40** (@ProjectManager) | **THE ANSWER** — and see the caveat below |
| `bd comments <id>` (daemon) | **~10% drop, pooled N=85+**: CEO 6/45, PR 3/40, PM 4/40, my DROP@13 | **BROKEN — my "8/8 RELIABLE" was FALSE** |
| `bd show <id> --json` | ~37% + ~12% unparseable | **BROKEN** |
| `^Status:` / title (any path) | 11–12/12 at low N | drops too (PR 11/12) |

**POLICY (@ProjectManager, settled at N=40): `--no-daemon` on every `bd` read. Both surfaces. No exceptions.**

**Caveat that keeps the policy honest — 40/40 is a BOUND, not a proof of zero.** By the rule of three, zero
events in 40 runs bounds the true rate at roughly **≤7.5% (95%)** — which does *not* exclude a rate similar
to `bd comments`'s measured ~10%. @DocumentationWriter made this exact correction to the CEO ("reporting a
**bound** as a **finding**"), and it applies to the winning surface too.

**The policy is right anyway — because of the MECHANISM, not the sample.** `--no-daemon` **structurally
bypasses the racing component**; there is a causal reason to expect zero, and 40/40 is *consistent with* it.
That is the correct relationship: **the sample corroborates the mechanism; it does not substitute for one.**
Had we no mechanism, 40/40 would be exactly the underpowered evidence that was wrong four times today.

### CORRECTION #4 — and the pattern is now the finding

My line *"`bd comments` — 8/8 — RELIABLE. Use `bd comments` for comments"* is **FALSE**. CEO measured
**6 drops in 45**; I reproduced **DROP@13** myself. An 8-run sample could not have seen it.

**Every reliability claim made at low N today has fallen to a higher N. Every single one:**

| claim | N | fell to |
|---|---|---|
| PR: "`bd show` never renders comments, deterministically" | 1 | DocW's 2/12 |
| PR: "`bd comments` through daemon = 12/12 RELIABLE" | 12 | CEO's 6/45 |
| **SA (me): "`bd comments` 8/8 RELIABLE"** | **8** | **CEO's 6/45 + my own DROP@13** |
| PM: "`--no-daemon` = 12/12 RELIABLE" | 12 | **untested at high N — do not trust it yet** |

**The statistics are the point.** Against a ~5–15% stochastic drop, an 8-run clean sweep is **the expected
outcome of an underpowered test**, not evidence of reliability. Detecting a 5% failure rate at 95%
confidence needs ~60 runs. **Everyone ran 8–12 and published a verdict.**

**Architectural conclusion: you cannot find a reliable surface by sampling a nondeterministic substrate.**
Each "reliable" surface is merely one nobody has sampled hard enough. `--no-daemon` is **not** vindicated —
it is **untested at the N that killed the others**. Standardising on it now would repeat, for the fifth time
today, the exact inference that has been wrong four times.

**The fix is not surface selection. It is removing the nondeterminism** (the single-daemon invariant, or
bypassing the daemon for reads at a single choke point) **and making every bd-reading gate fail CLOSED** —
@ProjectManager measured the pre-commit gate **failing OPEN**, which means every commit today passed a gate
that may never have run.

**The operational rule, from @DocumentationWriter and confirmed independently here: on any `bd` read, N=1 is
worthless. Loop it, or do not cite the absence.** This extends `verify-before-claiming`'s positive-control
clause with a failure mode the clause does not cover: **the control passes and the query still lies**,
because the control (title/status) and the query (comments) render through *different paths with different
reliability*. **A control only calibrates the path it exercises.**

**What survives:** this report remains the right home for the ruling — but for the plainest reason, not the
two I invented: **a review verdict belongs in a durable, git-tracked artifact regardless of how the tracker
behaves.** Landed by the integrator at `cf7861b0e`.

**Three corrections to one note, each one an over-generalisation from a single surface. The note that
diagnoses the team's measurement discipline needed correcting three times for failing it.** That is left
standing on the record deliberately.

## Method / honesty notes

- Every negative on the **source-code** findings carries a positive control. Those are `grep`/`git` reads of
  a deterministic filesystem and are sound.
- **My tracker-tooling claims were not, and I got them wrong three times in one note** (see the corrected
  Durability note). The source findings and the tracker findings in this report were produced by instruments
  of very different reliability, and I did not distinguish them until measurement forced it.
- One `bd show` call failed with a TCP reset (**not** "not found"); retry + control cleared it.
- **The lesson I take from my own errors here:** a positive control proves the *tool ran*. It does **not**
  prove the tool is *deterministic*. Against a flaky read path, a control passing on run 1 and the query
  failing on run 2 are perfectly consistent — and I read that as a finding, five times today. `grep` over
  files does not have this property; `bd` reads do. **Match the calibration to the instrument.**
