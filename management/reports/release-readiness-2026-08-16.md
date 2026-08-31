# Release readiness — what must be fixed before the next alpha, and before 10.0.0

**Date:** 2026-08-16 | **Author:** ProjectManager | **Basis:** 778 open beads, 116 release-classified.
Every item cited below was re-verified against HEAD rather than read from its bead description.

---

## The short answer

**Yes, there are must-fix items, and they are concentrated in one place.**

**Five of the six release-blocking P0s are in the audit/compliance subsystem.** Four of those five were
found today, by an independent contract review, in code that ~112,000 automated tests pass over. That
concentration is the finding. This is not a long tail of small defects; it is one subsystem materially
weaker than the rest of the product, and it is the subsystem carrying the product's strongest promises.

The messaging core — dispatch, pipeline, transports, outbox — is in good shape and is not what should
delay a release.

---

## The deadline that actually binds

There are three dates, and the one people reach for is the wrong one.

| deadline | what it gates | can it move? |
|---|---|---|
| next alpha | nothing structural; alphas are for feedback | yes |
| **10.0.0-rc.1** | **every breaking change** | **no** |
| 10.0.0 stable | polish, docs, tag hygiene | yes |

Our own published policy freezes the API at RC (`docs-site/docs/migration/version-upgrades.md:33`), and
after 10.0.0 a breaking change is reserved for a new major line, which we tie to a new .NET major.

**So "before 10.0.0" is the wrong frame for anything that changes a signature. The real question is
"before rc.1", and that is much sooner.** Everything in Class C is breaking: free today, a major version
after RC.

---

## Class A — the audit trail does not do what it says (blocks the next alpha)

This is the class I would not ship past. Not because an alpha consumer hits it tomorrow, but because the
failure is silent, the artifact goes to a third party, and we publish a conformance number attesting it.

| bead | defect |
|---|---|
| `s90ysc` | `InMemoryAuditStore` never checks chain linkage. **Delete a record from the middle of the trail and it reports Valid.** Each survivor validates against its own *claim* about a predecessor; nothing checks that predecessor is still there. Deletion is the likeliest form of audit tampering. |
| `kzyiww` | The two SQL audit stores build one hash chain **per (tenant, application)** but verify linkage **estate-wide with no partitioning term**. With two tenants — or one tenant and two apps — adjacent rows belong to different chains, so **verification reports tampering on a perfectly intact trail**. The documented consumer response is `LogCritical("AUDIT TAMPERING DETECTED")`. A verifier that cries wolf on healthy data gets switched off, taking the real detections with it. |
| `cphbb1` | The `Excalibur.Data.Postgres` audit store hashes with **bare unkeyed SHA-256 over four fields**. Our own stated invariant is *"producing a tag without the key is impossible — there is no unkeyed fallback."* This store is that fallback: anyone with write access can edit a row and recompute the whole downstream chain with a public algorithm. `Outcome` sits outside the digest, so flipping Denied to Allowed is undetectable by construction. It also creates the table it then attests — point it at a fresh database and it certifies the emptiness it just manufactured. |
| `fkzzz3` | **4 of 6 shipped SQL compliance stores have zero test files**, while docs-site publishes a conformance pass count as evidence a consumer hands an external auditor. LegalHold has zero coverage on both engines. |
| `4aqy6u` + `v3yz74` | Every provider returns `Valid(0)` over a scope it never examined — and **the shipped conformance kit requires that behaviour**, so a consumer whose store reported honestly would fail our kit. The payoff: `AuditLogControlValidator` writes *"Audit log integrity verification: Passed"* into a SOC2 evidence record from a quiet 24-hour window. |

**A sixth, found late and in a different package — `xacjmb`, escalated P3 → P1.**
`Excalibur.Data.ElasticSearch`'s `FieldEncryptor.ValidateIntegrityAsync` returns **`true` for any
well-formed Base64 string**. It never authenticates the GCM tag; it checks that the tag is present and
parses. Its own comment says *"For demonstration, we'll check if the authentication tag is present and
valid format."* Both the interface and the implementation are in `PublicAPI.Shipped.txt` (`:2189`,
`:2151`), so a consumer resolves it, asks *"has this field been tampered with?"*, and is told yes about a
value nothing examined — including one an attacker substituted. **Bounded honestly:** the *decrypt* path
calls it only as a pre-check and then performs real AES-GCM, which does fail on a bad tag. **Data flowing
through decryption is protected; the standalone public API is decorative** — and the standalone API is
what a consumer calls precisely when they want to verify *without* decrypting, which is the audit use
case. It sat at P3 for the same reason the others were missed: nobody had grouped it with them.

**Why this class is different from an ordinary bug.** The consumer of an audit report is not a developer
who notices something odd at runtime. It is an auditor reviewing a customer's controls, months later, with
no way to distinguish a real attestation from a vacuous one. Most defects are eventually found by someone.
These are designed not to be.

**The crux, verbatim from the contract review:** *"An abstraction that admits both of these stores as
equals is not generalizing over implementations — it is generalizing over guarantees, which is the one
thing a specification must never do."*

One root cause underlies all five: the operation's contract was never written down, so four careful people
implemented four different ones. A specification now exists.

**In flight:** the result-type reshape and the empty-scope fix. `s90ysc`, `kzyiww`, `cphbb1` are unassigned.

---

## Class B — cross-tenant and data loss (blocks the next alpha)

| bead | defect | status |
|---|---|---|
| `jps1cf` | DLQ retention purge deletes on age alone. **Severity downgraded after re-measurement** — it is declared only on the operator-privileged admin interface, registered separately, deliberately estate-wide. A tenant reaches it only through host misuse. **Architecture ruled against the tenant-scoping fix**: that fix would have deleted estate-wide purge outright in multi-tenant hosts, since the ambient scope always resolves when a tenant context is registered. Landing instead as a rename putting the reach in the name, following the convention nine outbox providers already ship. |
| `pw967a` | Three transport-delivery UPDATE paths not scoped to the caller's tenant. | open |
| ~~`7uywu9` / `jcztob` / `m4ge1j`~~ | **WITHDRAWN — the claim is literally true and the defect is unreachable.** The saga idempotency PK really does omit the tenant, and all four request classes really do carry zero tenant terms. But they are `internal sealed` with **zero references**, their provider was deleted in `f6d00a276` ("delete Model B sagas"), there is no DI entry point, and one of the two `InternalsVisibleTo` friend assemblies **does not exist**. `internal` bounds the search space to the assembly plus its friend list, so that is a proof rather than a failed search. The live dedup is state-based (`SagaState.ProcessedEventIds`) and the saga row load *is* tenant-filtered, so tenant A's and tenant B's `Order-123` load different rows with different processed sets. Being cut, not fixed: the shipped `SagaIdempotency.sql` creates a table nothing writes, documented against an options type with zero occurrences in `src/`. | withdrawn |
| `plfroc` | Outbox statistics disclose estate-wide message volume to any tenant, on the plain store interface. | open |
| ~~`evsgdz`~~ | **WITHDRAWN — the claim is false at HEAD and this line was wrong when written.** The inbox *is* tenant-scoped, across all 9 capable providers. The mechanism varies, which is why a single-spelling search misses it: Postgres/SqlServer/Oracle use an `AND tenant_id = @TenantId` predicate; Cosmos/ElasticSearch/Firestore/MongoDB compose the tenant *into the dedup document id*, making isolation structural rather than predicated; Redis uses a key segment; DynamoDb puts it on the key. `InMemory` has no scoping and correctly does **not** claim the capability, so it is refused at startup rather than leaking. The released `v3.0.0-alpha.216` genuinely did have the defect — but that tag's docs mention the inbox zero times, so it was never advertised and there is **nothing to disclose**. What remains is one doc sentence, in the *opposite* direction — see Class D. | withdrawn |

---

**Also here, and it is a first-contact failure for an entire engine.** Five beads describe one situation
nobody had grouped: `0r5tw8` (Outbox Oracle + Postgres), `1uxju1` (Oracle snapshot), `jrqzyk` (Saga
Oracle), `wbbj9q` (Oracle outbox columns that exist only in a *test fixture*, so a consumer hits
`ORA-00904`), and `kckdnm` (the shipped `Excalibur.Saga` README gives DDL for a `saga.SagaState` table
**no provider uses** — the real table is `dispatch.sagas`). Together: **an Oracle consumer cannot
provision the schema for outbox, snapshots, or sagas from anything we ship**, and the one README that
does supply DDL supplies the wrong table. Also `kusexf` — ElasticSearch/OpenSearch tenant projection
resolvers fall back to the **shared default node** when a mapped shard omits its connection string,
silently landing one tenant's projections on shared infrastructure.

---

**Three of the five items in this class withdrew on re-measurement**, and that is worth more than any one
of them. `jps1cf` was downgraded (declared on the operator interface, not the tenant-facing one),
`evsgdz` was false (the inbox *is* scoped, by four different mechanisms), and the saga cluster was true
but unreachable (the code is dead). In each case the bead's *measurement* was accurate and its
*conclusion* was not — the gap was always a question nobody had asked: which interface declares this,
which spelling does this provider use, does anything call this at all.

**Class A did the opposite.** Every audit item got worse under scrutiny, and four of the five P0s there
did not exist as beads this morning. The difference is that Class A was examined by someone deriving the
contract from scratch, and Class B by people checking whether a stated defect reproduced.

---

## Class C — breaking, so the deadline is rc.1 and gone after

Behaviour bugs can ship in a patch. These cannot.

`nv61sk` public const carrying tenant identity · `py7p5h` three of four documented options are inert ·
`8v1ldz` Polly adapter advertises three options it silently ignores · `w6be00` transport DI hand-copies
options and can drop one · `o98puy` / `m9hmp6` capability gating · the `AuditIntegrityResult` reshape ·
the DLQ purge rename · `xacjmb` (below).

**The cache-key defects are a cluster of three, and an earlier draft of this report named only one.**
`6znedx` is the `ICacheable` path — the key omits the type, so two message types with matching
`GetCacheKey()` share an entry. `g8csbg` is the `[CacheResult]` path — the key is built from the
*serialized action*, and `[JsonIgnore]`d state is excluded from serialization, so two actions differing
only in ignored state collide and are served each other's result. **Fixing `6znedx` alone leaves the other
path serving wrong responses.** `9xm50l` is the third: cache fail-open re-runs a handler that threw,
double-executing side effects. Three defects, three priority bands, nothing connecting them until now.
**`9xm50l` is now fixed** — and the design was forced by a quality gate: CA1506 refused any new type on a
class already at its coupling ceiling, which pushed the fix from a type-based discriminator to a
positional one that generalises to *every* handler exception instead of accumulating exclusions.

> ### ⚠️ Do not "fix" `6znedx` by prefixing the type into the cache key
>
> It is the obvious one-line repair and it would **convert a latent defect into a live, silent,
> unbounded one.** `ICacheInvalidator.GetCacheKeysToInvalidate()` returns **raw, type-agnostic**
> strings — an `UpdateUserCommand` invalidates `GetUserQuery`'s entry by naming `"user:42"` without
> knowing that type — and both the store path and the invalidation path currently fold through the same
> transform, so the keys match today. Prefix the store key alone and **invalidation stops matching.**
> Invalidation is **fail-open**: it catches and logs, so nothing throws, no test fails, and consumers
> serve stale data indefinitely with no error anywhere.
>
> `6znedx` today is latent — **zero `ICacheable` consumer types exist in `src/` or `samples/`.** The
> repair would be strictly worse than the defect.
>
> The recommendation, escalated to architecture and **not** adopted: make keys type-scoped and move
> cross-type invalidation onto **tags**, which already exist for exactly this purpose. Keys become
> precise identity; tags stay the deliberately cross-cutting mechanism. That changes the semantics of a
> public interface, so it is breaking — before rc.1, or it waits for a major.
>
> One test arm currently asserts the store/invalidation key coupling. **It is the only thing in the repo
> that would catch this, and it is green today precisely because nobody has made the change yet.**

---

## Class D — first contact (cheap, high leverage)

`u1ge5q` **fixed and committed today.** The landing page claimed support for .NET 8 and 9 while shipping
projects single-target `net10.0` — a .NET 8 consumer following it could install nothing at all. Four counts
corrected alongside it. Three of the four defects originally filed against that file were already fixed;
re-measuring first was the whole job.

Still open: `xb85z4` Cosmos sample pins an emulator tag that starts and cannot create a database ·
`bdllus` docs site serves a pre-correction build · `guxroj` ADR-108 still mandates the three-framework
matrix the code abandoned.

**New, and it understates a consumer obligation rather than overstating our guarantee.**
`docs-site/docs/configuration/event-store-setup.md:434-436` says `AddMultiTenancy` wraps six contracts
"with its tenant-scoping decorator." **Three of the six are not decorated** — `IInboxStore`,
`IOutboxStore` and `IEventStoreErasure` are *gated*, and scope themselves. The registration file says so
in its own comments. Why it matters: a consumer writing a **custom** `IInboxStore` reads that the
framework wraps it and concludes their store need not be tenant-aware. It must be — and if it isn't they
get a startup throw they were told nothing about.

**Downgraded after re-measurement — `5z5dvq`.** It claimed *"a consumer browsing nuget.org sees 76
published versions with no indication that the API is unstable."* Not so: every version carries a SemVer
pre-release suffix, which is the canonical metadata signal, and NuGet acts on it — such packages are
marked pre-release and excluded from default search and `dotnet add package` resolution. That exclusion is
precisely why `nnuhhn` exists, so the two beads are in tension: `nnuhhn`'s premise is that the signal works
so well it makes our documented install commands miss the packages entirely. What survives is a much
smaller ask — the package *description* carries no prose stating what pre-release means here — and it is
adjacent enough to a standing operator ruling that I have not acted on it.

---

## Class E — not blockers, and one already settled

- `nnuhhn` (published install commands resolve to stable and miss the prerelease) is **operator-ruled and
  parked.** The prescription was withdrawn; the gap closes by itself when a stable 10.x publishes.
  **Do not re-raise it.**
- `lttyrs` no git tags for published packages, `9drxpt` CHANGELOG attribution. Real, fixable any time.

---

## What is best for our consumers

**Ship the messaging core with confidence. Do not ship the compliance surface at the same confidence.**

The uncomfortable version: the packages carrying our strongest promises — audit integrity, GDPR erasure,
tenant isolation, SOC2 evidence — are the least tested and most defective in the product. That correlation
is not a coincidence. Those subsystems have the most invariants, the least observable failure modes, and,
until today, no written specification for the one operation their value rests on.

Three options, in the order I would consider them:

1. **Fix Class A and B, then ship everything.** Best outcome, and the audit work is well understood now
   that the contract exists. Cost is real but bounded.
2. **Ship the compliance packages as explicitly-labelled preview** while the core goes stable. Honest, and
   it stops us attesting what we cannot yet support. Requires deciding what "preview" means for packages we
   have already published 80 versions of.
3. **Ship as-is.** Not recommended. The one thing I would not do under any option is keep publishing a
   conformance pass count over stores with no tests behind it — that is the single defect here with a named
   third-party audience.

**This is a product call, not mine.** My input is the measurement; the scope decision is the operator's.

---

## Tracker hygiene, because it was blocking the question

The release gate reads release-classified beads. **103 open P1s carried no classification at all**, so they
were invisible to it. 95 are now classified and verified by read-back; the gate's P1 view roughly doubled
(35 to 73). Five strategic epics were labelled `strategy` rather than forced into the binary — recording a
ruling already written in one of their own bodies (*"Independent track, does NOT gate core release"*),
rather than making a new one.

**That sweep then continued through the rest of the backlog.** 454 of the remaining 465 are classified,
each verified by read-back; the 11 genuine ambiguities were escalated with both readings stated and have
been ruled. The whole backlog is now readable by class:

```
        release   process  strategy
P0            6         3         0
P1           72       107         5
P2          172       207        15
P3          104        72         8
TOTAL       354       389        28
```

Two boundary rulings worth recording, because they will recur. **A shipped guarantee with no enforcing
test is `release`** — we are asserting something to consumers that nothing checks — while test-suite
speed, flakiness and hygiene are `process`. And **internal refs in `eng/` are `release`**, not process:
the operator's recorded directive is that `eng/ci` is published to a public repository, so a bead id in a
gate script is a leak onto a consumer-visible surface.

**Four quiet blockers surfaced from below P1** during that sweep, all verified in code, none of which
would have been found by triaging downward from P0: `xacjmb` (now P1, above), `g8csbg` (the second cache
defect), `kusexf`, and the five-bead Oracle DDL group. **Every one had sat at P2 or P3.** The lesson is
not that the priorities were wrong; it is that nobody reads down there, so a quiet consumer-facing defect
filed at P3 is functionally invisible.

**Backlog accuracy:** roughly 32% of audited beads were already fixed. The backlog lags the code by about a
sprint. It is not rotting — but a bead is not evidence, and every item above was re-measured before it was
listed here.
