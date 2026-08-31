# Architecture — Excalibur.AuditLogging

> **Guarantee contract for the tamper-evident audit trail.** This document is the source of truth for
> *what `VerifyChainIntegrityAsync` establishes, what it does not, and which providers establish it
> today.* It is a contributor + integrator reference. Keep it current: any change to a hashing,
> chain-writing, or verification path updates this file, verified at architectural review.
>
> **Read the provider table before relying on the guarantee.** The guarantee below is the contract the
> subsystem is written against. It is **not** uniformly delivered — the table states, per provider, which
> half is implemented. A provider that implements one half is not "mostly tamper-evident"; it is blind to
> an entire class of tampering, and this document exists to say which class.

## The guarantee

Audit records are **tamper-evident**: an alteration of the stored trail is *detected*, after the fact, by
a verification pass. Tamper-evidence is not tamper-*proofing* — nothing here prevents a writer with
database access from altering rows. It establishes that the alteration cannot pass verification unnoticed.

Tamper-evidence requires that **two distinct properties** hold, D1 and D2 below. They are established by a
single keyed check rather than by two — the MAC covers the record's contents *and* its predecessor's tag
together — but they are stated separately because they fail separately, and a store that establishes one
while omitting the other is blind to a whole class of tampering rather than partly covered. D3 is a third,
narrower check over a value the MAC does not cover. Each is stated so that it can be falsified by a test:

| # | Detection | Falsifiable statement |
|---|---|---|
| **D1** | **Content integrity** | For every record in scope, the stored keyed MAC verifies against that record's **live, re-canonicalized fields** together with its **stored prior tag**. Alter any integrity-covered field of a stored record and verification reports a violation naming it. |
| **D2** | **Chain linkage** | For every record in scope, the stored keyed MAC verifies against the tag of the record **actually preceding it in the store** — not against the predecessor the record itself names. Both edges of the range are pinned the same way: the first record against the record immediately preceding the range, or as the partition's genesis; the last record against the record immediately following the range, where one exists. Insert, delete, or reorder a record within the range, or delete records from either end of it, and verification reports a violation. |
| **D3** | **Stored linkage agreement** | For every record in scope, the prior tag stored on the record equals the tag of the record actually preceding it. Alter that stored value alone and verification reports a violation naming the record. |

### Neither detection alone is tamper evidence

This is the sentence the rest of the document exists to carry.

**D1 without D2 cannot detect a deletion.** A record's tag is computed over the tag of whatever preceded it
at the moment it was written, and that same value is *copied* into the record's own row. Verify each record
against the copy and every survivor of a deletion still verifies: the copy names a record that is gone, and
nothing in the surviving row says so. D1 examines each row in isolation, and there is no isolated row in
which a deletion is visible. A store implementing only D1 will report `Verified` over a trail from which
records have been removed.

**Linkage without D1 cannot detect a mutation.** This is why D2 is a keyed check and not a comparison of
two stored values. A store that established linkage by comparing each record's stored prior tag against its
predecessor's stored tag would be comparing a stored hash to a stored hash: neither value is recomputed from
the record's live content, so altering a covered field without touching either column leaves every link in
agreement, and the store reports `Verified` over a trail whose contents have been rewritten. That shape is
rejected here — D2 verifies the MAC, which is recomputed from the live record every time.

The two failure modes are disjoint. Establishing one property and calling the result tamper-evident
overstates the guarantee by exactly the class of attack the other one covers.

**D3 is not what detects deletion, and must not be mistaken for D2.** The stored prior tag is a *copy* of a
MAC input, not the input itself. Altering the copy moves nothing the MAC covers, so comparing the copy proves
nothing about the chain, and a store that performed only that comparison would be implementing D2 in name
alone. Deletion detection comes entirely from verifying each record against the predecessor actually present.
D3 exists for a narrower and real reason: the stored value is read by auditor-facing exports, and a value
nothing verifies is a value that can be rewritten for free. Before D3, altering it was invisible to
verification while remaining visible to whoever read the export.

**The same comparison also does a second job, and previously did only that one.** Once D2 reports a break,
comparing the record's stored claim against the predecessor actually present is what separates a record whose
*contents* were rewritten from a record whose *predecessor is missing* — a distinction the MAC alone cannot
draw, because it covers both together, and one a reader needs because the two call for different responses.
That classifying role came first. What D3 adds is running the comparison on every record rather than only
after a failure, which turns a classifier into a check: a classifier reached only after a break can say
nothing about a trail where the only thing altered was the value it reads.

### Scope is part of the result, and must be named

A verification result is meaningless without knowing what it examined. Two scopes are coherent, and the
providers below do not agree on which they use:

- **Estate-wide** — every record in the time range, across all tenants and applications.
- **Ambient tenant partition** — only records belonging to the caller's ambient tenant.

**Scope must match the chain partition, or D2 produces false violations.** A store that writes one chain
per tenant but verifies estate-wide will order records from interleaved chains together, compare each
record's prior tag against a neighbour from a *different* chain, and report tampering on a trail that is
intact. The converse — verifying a narrower scope than the chain partition — cannot produce false
violations, but slices the chain, which is what makes the boundary anchoring in D2 load-bearing.

### Three outcomes, not two

Verification reports `AuditIntegrityOutcome`: `Verified`, `ViolationsDetected`, or `NoEventsInScope`.

**`NoEventsInScope` is not a pass.** An operation that examined no records has attested nothing. A window
that is unexpectedly empty may itself be evidence that audit records are not reaching the store — the
condition a compliance reader most needs to see. Collapsing it into `Verified` puts an unearned assurance
in front of an auditor, and the result type makes that collapse unrepresentable: `Verified` rejects a
count below one, so a successful verification over zero events cannot be constructed.

## How it is achieved (the seam)

1. **Canonicalize** — `AuditEventCanonicalizer.Canonicalize`
   (`src/Excalibur/Excalibur.AuditLogging/AuditEventCanonicalizer.cs:33`) renders the integrity-covered
   fields to deterministic, length-prefixed, version-stamped bytes. Seventeen fixed fields plus metadata,
   ordered by key. `TenantId` is covered, so a record cannot be moved between tenants without breaking
   its tag. Fields outside this set are **not** covered and their alteration is not detected.
2. **Tag on write** — `IAuditIntegrityStrategy.ComputeTagAsync`
   (`src/Excalibur/Excalibur.AuditLogging.Abstractions/IAuditIntegrityStrategy.cs`) computes a keyed HMAC
   over the canonical bytes concatenated with the length-prefixed prior tag, emitted as `v1:{keyId}:{mac}`.
   The signing key lives outside the audit store. Key acquisition **fails closed**: there is no unkeyed
   fallback, so a store cannot silently degrade to a forgeable tag.
3. **Chain on write** — each store reads the current head of the chain partition and passes its tag as the
   prior tag, so the new record's MAC is bound to its predecessor.
4. **Verify (D1)** — the store reloads the records in range, re-canonicalizes each one's **live** fields,
   and calls `VerifyAsync`
   (`src/Excalibur/Excalibur.AuditLogging.Abstractions/HmacAuditIntegrityStrategy.cs:55`) with the
   record's stored prior tag and stored tag. Comparison is constant-time; a missing, malformed, or
   unknown-key tag is a violation, never a pass.
5. **Verify (D2)** — the walk carries the prior tag forward from the record that actually precedes each
   record, seeded at the left edge from the anchor. Where the range has a successor, the store supplies it
   and its MAC is verified against the range's last record, pinning the right edge.
6. **Verify (D3)** — the same walk compares each record's own stored prior tag against the tag it carried
   forward, so altering the stored value alone is reported rather than passing unread.
7. **Authorize** — `RbacAuditStore.VerifyChainIntegrityAsync`
   (`src/Excalibur/Excalibur.AuditLogging/RbacAuditStore.cs:149`) requires the Compliance Officer or
   Administrator role, and writes a meta-audit record of the verification itself before delegating.
8. **Encrypt (optional)** — `EncryptingAuditEventStore`
   (`src/Excalibur/Excalibur.AuditLogging/Encryption/EncryptingAuditEventStore.cs:118`) encrypts covered
   fields *before* the inner store tags them, and delegates verification unchanged. Integrity therefore
   covers the encrypted-at-rest representation, and verification needs the signing key but not the
   encryption key.
9. **Refuse (encryption only)** — the same decorator refuses a query filtering on a field it encrypts,
   throwing `NotSupportedException` naming the field, rather than forwarding a comparison it knows cannot
   match. The cipher is randomized, so equal plaintext does not produce equal stored bytes and the inner
   store's equality predicate matches nothing. Forwarding it returned an empty result set — a caller could
   not distinguish "this actor did nothing" from "this query cannot work", which on an audit trail is the
   worst available answer. Two fields have both an encryption switch and a matching query filter, `ActorId`
   and `IpAddress`; both refuse while encrypted, on `QueryAsync` and on `CountAsync` alike.

### The chain primitive that implements D2, and the three stores that drive it

`IAuditIntegrityStrategy.VerifyChainAsync`
(`src/Excalibur/Excalibur.AuditLogging.Abstractions/IAuditIntegrityStrategy.cs:70`, implemented at
`HmacAuditIntegrityStrategy.cs:73`) walks an ordered chain carrying the prior tag **from the preceding
link**, rather than from the record's own stored claim. That is the correct shape for detecting deletion.

Three stores now drive it through the shared `AuditChainVerifier`
(`src/Excalibur/Excalibur.AuditLogging/AuditChainVerifier.cs`): `InMemoryAuditStore`, and the Postgres and
SQL Server stores in `Excalibur.AuditLogging.*`. They previously each carried their own inline comparison,
which is how they came to disagree; consolidating removed the divergence rather than correcting three
copies of it.

A fourth audit store, in `Excalibur.Data.Postgres`, has been **removed**. It hashed with an unkeyed digest
over five fields, wrote into the same table as the Postgres store above, and provisioned that table with a
narrower column set — so which of the two ran first decided whether audit logging worked at all. Repairing
it would have converged it onto the store it duplicated. Consumers use `Excalibur.AuditLogging.Postgres`.

The left edge of a verified range is now anchored rather than assumed. `VerifyChainAsync` takes the tag of
the record immediately preceding the range and seeds the walk with it; passing `null` is the explicit
assertion that the first record in range is the partition's genesis. The parameter is required, so a caller
cannot omit the decision — and deleting records from the front of a range is reported instead of being
indistinguishable from a range that legitimately starts at genesis.

**The right edge is now pinned too, and the asymmetry it removes was the more dangerous one.** Delete records
from the end of a range and the survivors chain perfectly to one another and to the anchor: the walk holds,
and nothing in the records presented mentions the removed suffix, so there is nothing left inside the range
to detect. `VerifyChainAsync` therefore also takes the *successor* — the record immediately following the
range in the same partition — and verifies its MAC against the range's last record. That MAC was computed
over the tag of the record the successor was written to follow, and it cannot be recomputed without the
signing key, so removing anything from the end of the range breaks it. The stores resolve the successor with
the mirror of the anchor query.

This matters more than its symmetry suggests: an attacker who can delete rows deletes the most recent ones,
because those are the ones describing what they just did. What remains unpinned is the case where the range
runs to the end of the chain and there is no successor to supply — see **Known gaps**, which states in plain
terms what verification does and does not establish about records written after the last one present.

## What each provider actually does today

| Store | D1 content | D2 linkage | Verification scope | Chain-write partition | Tag |
|---|---|---|---|---|---|
| `InMemoryAuditStore`<br/>`Excalibur.AuditLogging/InMemoryAuditStore.cs` | yes | yes, via the shared verifier | ambient tenant | tenant | keyed HMAC |
| `PostgresAuditStore`<br/>`Excalibur.AuditLogging.Postgres/PostgresAuditStore.cs` | yes | yes, via the shared verifier | estate-wide, enumerated per partition | tenant + application | keyed HMAC |
| `SqlServerAuditStore`<br/>`Excalibur.AuditLogging.SqlServer/SqlServerAuditStore.cs` | yes | yes, via the shared verifier | estate-wide, enumerated per partition | tenant + application | keyed HMAC |
| `RbacAuditStore` | delegates | delegates | delegates | — | — |
| `EncryptingAuditEventStore` | delegates | delegates | delegates | — | — |

The `D2 linkage` column covers D3 and both edge pins as well: all of them are performed by the same shared
walk, so no provider can have one without the others.

**Every remaining provider now implements these detections through one shared code path**, so they can no
longer drift apart per store. What differs between them is not the algorithm but the evidence: see the
table below for which providers have had that behaviour demonstrated against real infrastructure, and
which have not.

Three consequences follow directly from the table, and each is a behaviour a consumer will observe:

- **Deletion is detected.** Each record's MAC is verified against the prior tag taken from the record that
  actually precedes it, so removing a record is reported as `ViolationsDetected`. Verifying each record
  against its own stored claim — which survives the removal of the record it names — cannot see this, and
  was the previous behaviour of the in-memory store.
- **An intact multi-tenant trail verifies clean.** Both SQL stores write one chain per tenant and
  application, and verification now enumerates each partition separately. Previously it compared records
  drawn from different chains and reported `ViolationsDetected` on an untampered estate — a false positive
  that makes a verifier useless as evidence, because a check that cries wolf on healthy data gets switched
  off and takes the real detections with it.
- **An untenanted record verifies against its own tag.** The tag is computed over the record as supplied,
  where an untenanted event carries no tenant; the column cannot store that absence and holds a reserved
  term instead. Verification folds the stored term back to the value that was signed. Without that fold
  every untenanted record failed its own verification — the store reported tampering on a trail nobody had
  touched. Records written before this fold remain verifiable; the tag never changed, only the reading of
  it.

## Evidence

Per the standing rule, a guarantee with no test that RED-detects its violation is documented **UNVERIFIED**
and is not asserted.

| Guarantee | Test | Status |
|---|---|---|
| D1 — `InMemoryAuditStore` | `InMemoryAuditStoreChainIntegrityShould.Detect_tampered_event_hash`, and `InMemoryAuditStoreChainDeletionShould.Detect_a_record_whose_content_changed_while_both_hash_columns_were_left_intact` | verified |
| D2 — `InMemoryAuditStore` | `InMemoryAuditStoreChainDeletionShould.Detect_a_record_deleted_from_the_middle_of_an_intact_trail` — RED against the store before the shared verifier was adopted | verified |
| Left-boundary anchoring — `InMemoryAuditStore` | `InMemoryAuditStoreChainDeletionShould.Detect_records_deleted_from_the_front_of_the_verified_range` — RED before anchoring existed | verified |
| Scope correctness under multi-tenancy — `InMemoryAuditStore` | `AuditStoreConformanceTestKit.VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified` | verified |
| D1 — SqlServer | `SqlServerAuditStoreIntegrationShould.Verify_chain_integrity_detects_tampering` (real container) | verified |
| D2 — SqlServer | `SqlServerAuditUntenantedRangeVerificationShould.StillReportViolations_WhenARecordIsRemovedFromAnUntenantedWindow` — a record genuinely deleted from inside the window, executed against a real container | verified |
| D2 right-edge pin — SqlServer | `SqlServerAuditStoreIntegrationShould.Verify_chain_integrity_reports_a_record_deleted_from_the_end_of_the_range` and `…_reports_several_records_deleted_from_the_end_of_the_range` (real container) — RED with the successor withheld | verified |
| D2 right-edge pin does not accuse an untouched range — SqlServer | `SqlServerAuditStoreIntegrationShould.Verify_chain_integrity_verifies_an_untouched_range_that_has_a_successor` and `…_verifies_an_untouched_two_tenant_range_that_has_successors` (real container) | verified |
| D3 — SqlServer | `SqlServerAuditStoreIntegrationShould.Verify_chain_integrity_detects_chain_link_break` — RED against the store before the stored-claim comparison existed — and `…_reports_a_rewritten_stored_prior_tag_and_passes_an_untouched_one`, which pairs it with its liveness arm (real container) | verified |
| D1 — `Excalibur.AuditLogging.Postgres` | `AuditStoreConformanceTestKit.VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations`, surfaced by `PostgresAuditStoreConformanceTests`, executed against a real container. Rewrites a record's `Action` field out-of-band while leaving its hash columns intact, and asserts the intact trail verifies before the rewrite | verified |
| D2 — `Excalibur.AuditLogging.Postgres` | `PostgresAuditUntenantedRangeVerificationShould.StillReportViolations_WhenARecordIsRemovedFromAnUntenantedWindow` — a record genuinely deleted from inside the window, executed against a real container | verified |
| D2 right-edge pin — `Excalibur.AuditLogging.Postgres` | `PostgresAuditUntenantedRangeVerificationShould.ReportViolations_WhenARecordIsRemovedFromTheEndOfTheWindow` (real container) — RED with the successor withheld, paired with `ReportAnUntouchedTrailVerified_WhenTheWindowDoesNotEndAtTheLastRecord` | verified |
| D3 — `Excalibur.AuditLogging.Postgres` | `PostgresAuditUntenantedRangeVerificationShould.ReportViolations_WhenOnlyTheStoredPriorTagIsRewritten`, which asserts the untouched trail verifies before it rewrites anything (real container) | verified |
| D1-only, chaining disabled — SqlServer | `SqlServerAuditStoreIntegrationShould.Verify_chain_integrity_with_hash_chaining_disabled_verifies_an_untouched_trail` (liveness) and `…_still_detects_a_rewritten_record` (safety), real container. The liveness arm is RED against a version of the store that carries every record's tag forward regardless of the chaining setting — the false-accusation shape this design avoids | verified |
| D1-only, chaining disabled — `Excalibur.AuditLogging.Postgres` | `PostgresAuditUntenantedRangeVerificationShould.VerifiesAnUntouchedTrail_WhenHashChainingIsDisabled` (liveness) and `StillDetectsARewrittenRecord_WhenHashChainingIsDisabled` (safety), real container | verified |
| Left-boundary anchoring — SQL providers | `SqlServerAuditUntenantedRangeVerificationShould` and `PostgresAuditUntenantedRangeVerificationShould` verify windows that begin after the trail's first record, against real containers | verified |
| Untenanted records verify against their own tag | the `…UntenantedRangeVerificationShould` classes on both SQL providers, including the arm mixing both spellings of "no tenant" in one trail, executed against real containers | verified |
| `NoEventsInScope` reported for an empty window | `AuditStoreConformanceTestKit.VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope`, surfaced by every provider's conformance class, real containers for SQL Server and Postgres | verified |
| Intact chain reports `Verified` | `AuditStoreConformanceTestKit.VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified`, surfaced by every provider's conformance class, real containers for SQL Server and Postgres | verified |
| An unservable filter is refused, never answered emptily — encryption decorator | `EncryptingAuditStoreConformanceTests.QueryAsync_ByActorId_ShouldFilter` (overriding the kit arm), `…QueryAsync_ByEncryptedIpAddress_ShouldRefuseRatherThanReturnEmpty` and `…CountAsync_ByEncryptedActorId_ShouldRefuseRatherThanCountZero`, over a real Postgres container with real AES-256-GCM. Each is RED against the forwarding decorator, which returned an empty list and a zero. Paired with the liveness arms in `EncryptingAuditEventStoreFilterRefusalShould`, which prove a field left in the clear is still filtered and a query naming no encrypted field is still forwarded — without them a guard that refused everything would pass | verified |

**The shared conformance kit now tests tamper detection.** It previously did not: its two integrity arms
asserted that an intact chain verifies and that an empty window reports `NoEventsInScope`, and both are
liveness arms — each is satisfied by a store that detects nothing at all. A provider could therefore pass
conformance in full while implementing neither detection.

Three arms close that hole — a record deleted from the middle, a record rewritten with both hash columns
left intact, and an intact trail interleaving two tenants. The first two require the fixture to tamper with
the provider's storage directly, so the kit declares those hooks as **required** rather than optional: a
provider cannot be certified without supplying them, which means the tamper arms cannot be quietly skipped
by the providers most likely to fail them.

**Two cautions about reading this section.** An arm that is defined in the kit is not thereby run: each
provider surfaces the arms it executes, and an arm a provider does not surface is invisible rather than
failing. And an arm that has never been executed against real infrastructure proves nothing about that
provider — which is why the rows above distinguish *wired* from *verified*.

## Consumer obligations

To obtain the guarantee, a caller must:

1. **Configure a keyed integrity strategy and protect the signing key.** Tamper-evidence rests entirely on
   the verifier holding a key the writer does not. A trail whose signing key is stored alongside the audit
   data is not evidence against anyone who can reach that data.
2. **Hold the Compliance Officer or Administrator role.** Verification through `RbacAuditStore` throws
   `UnauthorizedAccessException` below that, and records a meta-audit entry for each attempt.
3. **Pass a valid range.** `startDate` after `endDate` throws `ArgumentException`
   (`src/Excalibur/Excalibur.AuditLogging/DefaultAuditLogger.cs:90`). Both bounds are inclusive.
4. **Branch on all three outcomes.** Treat `NoEventsInScope` as *no conclusion*, never as a pass. Where the
   result is transcribed into compliance evidence, report the unexamined window as unexamined; an empty
   window presented as a passing check is a misstatement to whoever reads the report.
5. **Not infer coverage from `EventsVerified`.** It counts records examined in the range under the
   provider's scope, which is not necessarily the caller's tenant, and is not a count of records that
   *should* have been present.
6. **Verify the range they mean to attest.** Verification examines only the supplied window; records
   outside it are not examined and their alteration is not reported.
7. **Choose, per field, between encryption at rest and the ability to query by it.** Where field encryption
   is in use, a field it covers cannot be filtered on: the query is refused, naming the field. Decide which
   questions the trail must be able to answer — "what did this actor do" is usually one of them — and leave
   those fields unencrypted, accepting that they are then readable by anyone holding the database. A caller
   that treats the refusal as a transient failure and retries will retry forever; it is a statement about
   the configuration, not about the store's health.

8. **Dispatch an audited operation inside a request scope when using the scoped audit context.** The
   middleware registered by `AddAuditContext()` fills the scoped `IAuditContext` — correlation id, tenant,
   actor — from the scope the message is processed in. A message dispatched from a provider that is not a
   scope has no such context to fill, so entries its handler records carry no correlation id, no tenant and
   an actor of "unknown". The framework logs that gap rather than binding one context for the life of the
   container, which would attribute every later message to the first caller. Entries so recorded are still
   chained and still tamper-evident; what is absent is the attribution, not the integrity.

## Known gaps

Each of these is present in the code as written today.

- **An encrypted field cannot be queried at all, and the trail says so rather than answering nothing.** The
  encryption decorator uses randomized authenticated encryption, so two records holding the same actor id
  hold different stored bytes and no server-side equality can find them. There is no reinterpretation of
  that cipher which makes the column searchable. The two mechanisms that would restore the capability are
  **not implemented here**: encrypting those columns with a deterministic mode, which makes equal values
  visibly equal to anyone holding the ciphertext and so exposes them to frequency analysis; or a blind
  index — a keyed one-way digest of the plaintext kept in its own column and queried in the ciphertext's
  place, which leaks the same equality but is not reversible, and which needs a column the audit event
  shape and every provider's schema do not currently have. Until one of them exists, the honest statement
  is: *a field is either readable at rest or filterable, not both.* The choice is per field and belongs to
  the consumer; what the framework guarantees is that the unavailable half fails loudly instead of
  returning an empty result that reads like an answer.

- **Verification establishes nothing about records written after the last one present in the chain.** This is
  the one edge that cannot be pinned from inside the stored trail, and it is stated here rather than left to
  be inferred from the absence of a claim. Where a verified range has a successor, records removed from the
  end of that range are detected. Where the range runs to the end of the partition's chain there is no
  successor to check against: delete the most recently written records and the survivors chain perfectly to
  one another and to the anchor, the walk holds, and nothing in the data presented mentions the removed
  suffix. There is nothing left to detect. This is the classic and unavoidable gap in a bare hash chain, and
  it has a known remedy that is **not implemented here** — a separately attested head, a signed tuple of
  partition, record count, head tag and watermark, written where the audit writer cannot rewrite it. Until
  that exists, the honest statement of the guarantee is: *verification establishes that the records present
  in scope are exactly the records written, in order, from the anchor forward — and, where a successor
  exists, that none were removed from the end of the range. It establishes nothing about records written
  after the last one present.* Read the gap against the threat before discounting it: an attacker who can
  delete rows deletes the most recent ones, because those are the ones describing what they just did.
- **The stores read the whole verified range into memory before verifying it.** The verification contract
  does not require this — it takes a stream and folds it carrying one accumulator, so it runs in constant
  space — but all three stores currently materialize their query results and hand over a list. Space
  therefore still grows with the number of records in the range, not with the chain's shape. The contract is
  the part that could not be changed later without a breaking change; the stores can be moved onto lazy reads
  at any time without one.
- **The anchor lookup scans the history before the range, not the range.** Resolving each partition's anchor
  runs a window function over every record written before the verified window, so verifying one day at year
  five reads five years to recover a handful of tags. The cost is in that query rather than in the
  verification walk, which is where anyone measuring this should look first.

- **A chain partition is a tenant and an application together, and a record carrying no application name
  belongs to its own partition.** A deployment that writes some records with an application name and some
  without, under one tenant, previously threaded them into a single interleaved chain that no verification
  could separate. Records written that way before this change will not verify as one chain.
- **Verification of a time range examines the contiguous run of the chain spanning that range**, including
  records written inside it whose timestamps fall outside — otherwise a clock skew would leave a gap
  indistinguishable from a deletion. `EventsVerified` therefore counts what was examined, which can exceed
  the number of records whose timestamps lie in the window.
- **Chain writes serialize on a per-instance lock, not a database-level one.** The head-read and the
  dependent insert are guarded within one process; two processes writing concurrently to the same chain
  partition can interleave and fork the chain, producing linkage violations that no tampering caused.
- **Chaining is optional on the SQL stores, and verification honors the setting.** With hash chaining
  disabled, `StoreAsync` signs every record independently against a null prior tag, and
  `VerifyChainIntegrityAsync` verifies each record the same way it was written — as its own single-record
  partition, asserting D1 (content integrity) against that null prior tag. D2 (linkage) is not established:
  there is no chain, by the store's own configuration, so deletion, insertion, and reordering are
  undetectable while chaining is disabled. That is the configuration's tradeoff, made explicitly by setting
  `EnableHashChain = false`, and it is why a `Verified` result under that setting means D1 held — not that
  the full D1+D2 guarantee this document leads with was established. A result never reports the mixed
  guarantee as if it were the whole one.
- **Only the enumerated canonical fields are covered.** A field added to the audit record but not to the
  canonicalizer is invisible to verification, and nothing fails when the two drift apart.
- **`CompromisedChainCount` is not comparable across providers.** Some stores count every failing check
  and continue; at least one returns on the first violation, so its count is always one and its
  `EventsVerified` is a partial count of the range.
- **`CompromisedChainCount` is in a different unit depending on whether the store chains.** With chaining
  on it counts chains, and two altered records in one chain count once. With chaining off the store
  supplies one partition per record, so the same field counts records. `IsHashChained` on the result says
  which, and a store that does not chain must be reported in record vocabulary rather than through the
  chained one — "no compromised chains" would otherwise read as evidence against deletion, insertion and
  reordering, none of which an unchained trail can test.
- **On a reported violation, `EventsVerified` counts what the walk consumed, not the size of the range.**
  The walk stops at a partition's first break, so a compromised partition contributes only the records
  examined up to it. This is deliberate — records after a break are unverifiable rather than independently
  sound, and counting them as verified would put an unearned number in front of a reader — but it means the
  count on a violating result is not the number of records in the window.
