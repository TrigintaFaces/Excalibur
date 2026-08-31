# S888 SPEC Decomposition (BLUEPRINT) — Provider Parity, Exactly-Once Durability & Design-Audit Hardening

**Author:** ProductManager · **Phase:** SPEC (task #2603) · **Baseline premise-gated against committed HEAD `8997102bf`** (mainline advanced to `21156a57f` = baseline + plan doc; code regions unchanged).
**Method:** every named bead premise-gated against current `src/` (not bin/); epics decomposed into child tasks with testable Given/When/Then ACs. All provider-lane ACs require **non-skipped real-infra locks that assert emitted behaviour** (per `verify-against-real-infra-not-mock`).

---

## 0. Executive summary

- **Phantoms / close-as-satisfied (scope shrinks — correct, not a miss):** `xlqpju`, `s8m5u1`. Epics `02sj2h` (18/18 children closed) and `w2zq7d` (45/47, CEO-ruled CLOSEOUT) are near-closed — no new children.
- **Premise CORRECTED (deferred → IN-SCOPE):** `63xsiv` — the Cosmos emulator is no longer externally-gated; Microsoft shipped the **vNext emulator (GA)**, so the fix is a mechanical fixture repin, front-loadable this sprint.
- **Dedups:** `3q1jtm` ⊂ `uw1nv4` (close 3q1jtm as dup); `y0robr` narrowed to inbox+projection to de-overlap `aknqta` (outbox).
- **PdM rulings made here:** `bq7w1f` HelloDispatch surface = **N/A**; `y0robr`/`aknqta` dedup; `63xsiv` reclassification.
- **SA-seam rulings needed at GUIDE:** listed in §5 (y1moc0 capability, fence-storage control table, xlqpju close-confirm, per-subject cryptor placement, 5fswhd default, leader split-brain D2≡L5, jrbf4r narrowing, event-id range, Liskov postconditions).
- **Overlap single-owner (per `coordinate-before-parallel-work`):** D2≡L5 (leader split-brain) → one owner; Dijkstra-row08 casing ≡ L9 (serializer) → one owner; D1 ≡ tenant half of L11/L12.

---

## 1. Lane A — Outbox durability, fencing-token & conformance (BackendDeveloper)

| Bead | Verdict | AC (Given/When/Then) — real-infra, RED-on-pre-fix | Files |
|---|---|---|---|
| `h7ng89` | **REAL** | Given a stored OutboundMessage with non-null CorrelationId/CausationId/Priority>0/ScheduledAt/PartitionKey/GroupKey/TargetTransports/IsMultiTransport, When staged→reloaded from real Postgres, Then every field round-trips byte-for-byte. | `Excalibur.Outbox.Postgres/Requests/InsertOutboxMessage.cs` + reserve/scheduled SELECTs + DDL + `PostgresOutboxStore.cs` conversion + test-fixture DDL |
| `y1moc0` | **REAL** (Postgres+Oracle only — **NOT Marten**, which tracks sent) | Given a delete-on-sent store, When the base sent-tracking + cleanup facts run, Then they are capability-gated (skipped/inverted for `SupportsSentTracking==false`) **and still asserted (liveness)** for tracking stores. | `OutboxStoreConformanceTestBase.cs` (make 6 `[Fact]`s capability-gated) + capability marker in abstractions. **SA-seam.** |
| `uw1nv4` (canonical; `3q1jtm` dup) | **REAL** | Given two leadership tenures (token N, then stale N-1), When stale-token GetUnsentMessages/MarkSent, Then stale claim yields **0 rows** (set-based, MUST NOT throw) and stale mark rejected; high-water advances monotonically to max. Real-infra per provider. | `PostgresOutboxStore.cs`/`OracleOutboxStore.cs`/`MartenOutboxStore.cs` + fence control table + DDL. **SA-seam (fence storage).** |
| `3q1jtm` | **DEDUP → close as dup of `uw1nv4`** (Mongo prong already delivered; verify uw1nv4 delivered scope covers PG+Oracle per forge-integration cl.8 before closing). | — | — |
| `s8m5u1` | **PHANTOM** — deriver + csproj ref + kwq3zu field-parity all committed at HEAD (`6cd758d57`/`efbe6007d`). | Close-as-satisfied. Caveat: run `MartenOutboxStoreConformanceShould` once on real Postgres/Marten to confirm 42/42 still holds. | — |
| epic `02sj2h` | **NEAR-CLOSE** — 18/18 children closed, ACs 1-6 delivered. | Verify no open dependents; close epic. Exactly-once *hardening* traces to sd36sc/y5tn3e, not new 02sj2h children. | — |
| epic `sd36sc` | uw1nv4 IS its PG/Oracle/Marten decomposition. | **NEW triage child:** Cosmos/DynamoDb/Elasticsearch/Firestore/Redis have zero `IFencedOutboxStore` — SA triage: leader-gated (build) or single-writer (document unfenced). | triage bead |

## 2. Lane B — Inbox transactional parity + tenancy (BackendDeveloper, 2nd wave)

| Bead | Verdict | AC | Files |
|---|---|---|---|
| `rjolfk` | **REAL** (PdM-ruled IN-SCOPE; premise confirmed — Cosmos/Mongo implement `IScopedTransactionalInboxStore`, SqlServer/Postgres do not; `InboxMiddleware.cs:467-477` already consumes it) | Given SqlServer/Postgres inbox, When a message is processed, Then handler + processed-mark commit in one `IDbTransaction` (consumer enlists via `context.GetInboxTransactionScope()?.AsSqlTransaction()`); throw→rollback (neither persists). Safety: crash between → reprocessable, no partial commit. Liveness: success commits both atomically, no re-execute. Real-infra non-skipped. | `SqlServerInboxStore.cs`, `PostgresInboxStore.cs`, `IInboxTransactionScope.cs` (`AsSqlTransaction()`). **Oracle+Marten DEFERRED** (parity follow-up). |
| `ljbwh8` | **REAL** (Oracle inbox has no ambient tenant scoping — pre-l9c3cv fail-open) | AC1 safety: tenant A wrote → tenant B scoped read/claim/mark returns/affects nothing; AC1 liveness: A's own scoped read returns it. AC2: non-MT → no tenant predicate. AC3: MT-active-unresolved → fail closed via `TenantScope.FromContext`. AC4: ctor accepts `ITenantContext?`, DI resolves it. AC5: README composite-PK `(MessageId,HandlerType,TenantId)`. Real-Oracle non-skipped. | `Excalibur.Inbox.Oracle/OracleInboxStore.cs` (mirror l9c3cv-corrected PG/SqlServer) + Oracle DI + README |

## 3. Lane C — Provider infra unblock + LeaderElection + Oracle parity (PlatformDeveloper)

| Bead | Verdict | AC | Files |
|---|---|---|---|
| `63xsiv` | **REAL — PREMISE CORRECTED: no longer externally-gated.** Cosmos vNext emulator is **GA** (`mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest`, health endpoint :8080). Fix = mechanical repin of 5 fixtures (all pin legacy `:latest`). **Front-load.** | Given a Docker host, When any Cosmos fixture starts, Then the emulator reaches ready + `CreateDatabaseIfNotExistsAsync` succeeds and ≥1 previously-blocked Cosmos real-infra lock runs green (non-skipped). | 5 `*ContainerFixture.cs` + `Directory.Packages.props` (Testcontainers.CosmosDb bump + vNext wait-strategy on :8080) |
| `5fswhd` | **REAL** (defect-1 `$set` sibling FIXED; defect-2 LIVE: `ReleaseLockAsync` DeleteOne + `ttl_expiresAt` TTL destroy the doc → fencing resets to 1 on restart) | Given real Mongo + default registration (no `IFencingTokenProvider`), When incumbent acquires token T, gracefully releases/restarts, challenger takes over, Then challenger's token is strictly > T (never resets to 1) — `TakeOver_AndAdvanceFencingToken...` RED→GREEN. Liveness: active renew → no takeover. **Do NOT close on defect-1 alone.** | `MongoDbLeaderElection.cs`. **SA-seam (deliver sd36sc default; reopen CLOSED sd36sc = PM scope call).** |
| `uysw7y` | **REAL** (P3 throughput — N round-trips; `InsertEventsBatchRequest.cs:134-165`) | Given a batch of N≤100 events, When appended, Then same per-row RETURNING-POSITION semantics via ODP.NET `ArrayBindCount` in a single round-trip under SERIALIZABLE; per-version→position mapping byte-identical to the loop. Real-Oracle. | `Excalibur.EventStore.Oracle/Requests/InsertEventsBatchRequest.cs`. **SA light-confirm (RETURNING-array output).** |

## 4. Lane D — EventSourcing ISP + GDPR crypto-shred (BackendDeveloper 3rd wave / SA seam)

| Bead | Verdict | AC | Files |
|---|---|---|---|
| `xlqpju` | **PHANTOM** — ISP goal already met by `IGlobalStreamQuery` (`Queries/IGlobalStreamQuery.cs:25`); `IEventStore` already 3 methods; `IGloballyOrderedEventStore` doesn't exist. | Close-as-satisfied. **SA confirms `IGlobalStreamQuery` is the accepted capability seam** (rename/consolidate is cosmetic, not a defect). | — |
| `y0robr` | **REAL** (PdM-ruled per-DataSubjectId; premise confirmed — Inbox/Outbox/Projection decorators use one `_defaultContext`, no crypto-shred) | **NARROWED to inbox+projection** (outbox = `aknqta`). Two-subject real-infra: encrypt A+B via decorator → destroy-key(A) → A PII unrecoverable, B intact, A non-PII still loads. Route through `SubjectFieldCryptor`/`IFieldEncryptor` (reuse ktepi9), keyed by `[DataSubjectId]`. RED vs `_defaultContext`. | `EncryptingInboxStoreDecorator.cs`, `EncryptingProjectionStoreDecorator.cs`. **SA-seam (cryptor injection placement).** |
| epic `uahb0i` | Structure exists; W1/W2/W3 closed. Remaining per-surface slices under the umbrella: | snapshot=`wc85fx` (field-level from-scratch; current `EncryptingSnapshotStore` is blob-level single-key), outbox=`aknqta`, inbox+projection=`y0robr` (narrowed). Each: two-subject real-infra shred lock. | per-surface decorators |

## 5. Lane E — Design-audit decomposition (SPEC fans out; single-owner by seam)

### `haqhcm` — Dijkstra: 7 declaration-without-demonstration seams
- **D1 (P0)** Tenant scoping capability marker enforces nothing structural (`ITenantScopingCapability<T>` empty marker; S886 `rw2ull` class). AC: marker emitted ONLY by the factory that injects `ITenantContext` (structurally inseparable); RED lock proves an independently-registered marker fails the requirement. *(≡ tenant half of L11/L12 — single owner.)*
- **D2 (P0)** Leader at-most-one is a 3-precondition hope (`MongoDbLeaderElection` wall-clock, no CAS/grace, ambient `UtcNow`). AC: takeover requires monotonic-`TimeProvider` grace + CAS; RED = skewed-clock interleaving yielding two `IsLeader`. *(≡ L5 — single owner; couples 5fswhd.)*
- **D3 (P1) — PREMISE PARTLY STALE, RE-SCOPE.** The atomic `ClaimDueTimeoutsAsync` already shipped (`SagaTimeoutDeliveryService.cs:115`). Drop "add the claim"; keep only "add RED-on-non-atomic conformance lock across providers (incl. InMemory)".
- **D4 (P1) — VERIFY vs S884 FIRST.** `AssemblyInfo.cs:13` mentions a "LoadFromHistory contiguity check"; K2/K3 made version envelope-authoritative. Confirm the gap still reproduces before spec'ing (may be close-as-satisfied). If real: read authoritative `HistoricEvent.Version`, throw on contiguity gap.
- **D5 (P2)** In-memory inbox eviction fails OPEN (`InMemoryInboxStore.EvictOldestEntry`). AC: full inbox with live records → fail closed (throw), not silent evict.
- **D6 (P2)** Async projection swallows apply failure + advances checkpoint. AC: apply throw → checkpoint does NOT advance (halt), no read-model drift.
- **D7 (P2)** Key-escrow M-of-N inspects no shares / counts no threshold (`KeyEscrowBackupService.RecoverKeyAsync`). AC: `<M` shares → fail closed (throw, no reconstruct); `≥M` → reproduces secret. (Couples `security/dotnet.md` discarded-crypto.)

### `8cnpj4` — Liskov: 14 interface families (owner **TestsDeveloper**) — write postcondition into the contract + derive a conformance test that FAILS without it
L1 IEventStore (append→return failure not throw; RED Cosmos/Dynamo/Redis/Sqlite/InMemory) · L2 IOutboxStore atomic-claim disjoint (RED Mongo/InMemory) · L3 IInboxStore fault-model named (RED InMemory durability claim) · L4 ISagaStore purge terminal-only (RED Cosmos/Dynamo/Firestore NotSupported vs doc) · L5 ILeaderElection at-most-one + FencingToken non-null (RED Mongo split-brain — **≡D2, single owner**) · L6 IMaterializedViewBuilder+ICdcStateStore exactly-once fold + DeletePosition true-iff-existed (RED cloud unconditional true) · L7 IMessageBus handler fault-independence (RED LocalMessageBus fail-fast) · L8 ITransportSubscriber MessageAction settles (RED Grpc log-only) · L9 serializers ResolveType(GetTypeName(t))==t + wire parity (RED 3 incompatible names; AOT casing — **≡Dijkstra row08, single owner**) · L10 IDb typed ConcurrencyException (RED no relational provider throws it) · L11 authz/erasure: AWS crypto-shred false-certificate (**highest severity**) + `AuthorizationEffect.Permit=0`→Deny · L12 ITenantContext history-constraint (model family; RabbitMQ options ValidateOnStart) · L13 IWorkflowSignalInbox deterministic-replay analyzer error-not-warn + signal durability · L14 job-scheduler family shares one `IJobSchedulerProvider`.

### `mgwuiu` — Metz changeability (verify byte-identical before every extract):
Shared-knowledge: M1 outbox `FromOutboundMessage` drift (P1) · M2 serializer-registration ValidateOnStart (P1, RED MessagePack/MemoryPack) · M3 event-envelope wire format ×13 (P2) · M4 audit envelope ×9 (P2) · M5 leader state-transition ×7 (P2) · M6 A3 fail-posture Cedar/Opa (P3). Missing-object: M7 workflow-journal double-switch (P1, sharpest) · M8 PersistenceProviderFactory dead dispatch (P2) · M9 fail-open null-object (P2). Dependency-direction: M10 wall-clock→TimeProvider drift (P2, couples D2) · M11 IEnvironment seam (P3) · M12 AwsS3ClaimCheckStore inject IAmazonS3 (P2) · M13 TenantScopedEventStore extend DelegatingEventStore (P2) · M-sched IJobSchedulerProvider + conformance kit (P2, ≡L14). **DO-NOT-TOUCH (record in epic):** per-provider SQL bodies, per-transport native maps, Dispatcher cross-overload dup, ES/OpenSearch sinks, TraceSampler/CedarMode enum switches.

## 6. Lane F — Governance, harness & docs

| Bead | Verdict | AC | Owner |
|---|---|---|---|
| `0fhf1f` | **REAL** — `ready_for_integration` in **8** corpus files (not 10; 2 reverted S882). Contradicts `forge-integration-conventions.md:31`. | grep `.claude/{rules,agents,workers,skills,hooks}` for `ready_for_integration` → 0 hits; each "signal ready" reads "explicit OPCOM/tracker-comment handoff". Corpus edit = DEBRIEF/self-improving scope, not mid-sprint. | Docs/DEBRIEF |
| `jslh06` | **REAL** (P3 cosmetic) — gate `count_live_p0` counts `!=closed`, reports "1 live P0" while `--status open`=0. | Report count with status breakdown or stay silent; never unqualified mismatch. RED on current miscount. | Platform |
| `exuzoe` | **REAL** (P2) — no `task-delay-syncwait-gate.test.sh`. | Author the self-test: staged raw `Task.Delay`/`GetAwaiter().GetResult()` in tests/** → nonzero (safety); compliant → 0 (liveness). | TestsDeveloper |
| `5pedth` | **REAL** (P2) — `_dupcheck.py:44` still `>=0.6`; no self-test. | 3 distinct sibling discoveries, same owner, generic title → NOT collapsed (safety); genuine near-dup still flagged (liveness). Author≠impl. | TestsDeveloper |
| `rud0sj` | **REAL** (P3) — daemon-write-in-window self-test never authored. | Daemon writes tracked jsonl during stage→commit window; anchored-blob staging → committed blob = pre-window content. RED vs `git commit --only`. | TestsDeveloper |
| `505y99` | **REAL** (P3) — superset invariant unimplemented in `validate-shard-results.ps1`. | Deterministic shard asserted a **superset** of union of sharded assemblies; RED on planted missing assembly. | TestsDeveloper |
| `15q2hm` | **REAL (tracking only)** — fix lives in the separate OPCOM server repo. | **Owner clarification:** this repo files a cross-repo follow-up; code premise not verifiable here. | PM/operator |
| `jrbf4r` | **PARTIALLY REAL — NARROW.** Real phantoms: `EventStoreDispatcherService`, generic `IEventStore.LoadAsync<TKey>`. **PRESERVE (live in src):** `EventStoreMessage<TKey>` (`Delivery/EventStore/EventStoreMessage.cs:16`), `Delivery.EventStore` namespace. | Remove only genuine phantoms, verify each token vs src before deleting. Event-id-range reassignment = **SA decision**, separable. | DocumentationWriter/SA |
| `0nwj5y` | **REAL** (P3) — `EventStoreConformanceTestBase` has no null/whitespace aggregateId rejection fact. | Cloud store + null/whitespace aggregateId → `ArgumentException` (safety); valid → round-trips (liveness). Add conformance fact so all derivers inherit. | TestsDeveloper |

## 7. Lane G — Migration tooling & DX (FrontendDeveloper)

| Bead | Verdict | AC | Notes |
|---|---|---|---|
| `s4kwiv` | **REAL** (P1, validation-gap execution task) | Real OSS MediatR app (pre/post processors + exception actions) → codemod → record %-auto-migrated + exact EXMIG0002 manual-step list + zero crash/silent-skip. Deliverable = report artifact. | Compat stack + EXMIG0001-04 all closed; only the real-app run remains. |
| `bq7w1f` | **PARTIALLY REAL — NARROW.** dispatch-minimal-api template ALREADY uses `DispatchPostAction` (done). Buried: `dispatch-api/Program.cs` (MapControllers), `dispatch-only.md:51`, `index.md:165` (hand-rolled MapPost+DispatchAsync). | Those 3 surfaces use `DispatchPostAction`/`ToHttpResult`. **PdM ruling: HelloDispatch = N/A** (console sample, no HTTP surface; the HTTP-bridge showcase is dispatch-minimal-api, already done). | — |
| epic `w2zq7d` | **CEO-ruled CLOSEOUT-not-SPEC; 45/47 closed.** No new children. Open tail: `2tf65w` (benchmark compat Send vs DispatchAsync, P2), `s4kwiv`. | — | — |

---

## 8. SA-seam rulings needed at GUIDE (SoftwareArchitect)
1. **y1moc0** — `SupportsSentTracking` capability opt-out contract shape (new capability interface vs virtual/skip), parallel to the fencing capability.
2. **uw1nv4 / sd36sc** — fence high-water **storage** for delete-on-sent Postgres/Oracle (dedicated control table/row à la Mongo control-doc; SqlServer's live-row `MAX(FencingToken)` inapplicable). Marten can use a document. **+ triage** the 5 unfenced cloud stores (build vs document-single-writer).
3. **5fswhd** — deliver `sd36sc` durable Mongo fencing provider as the DEFAULT (makes the reset-to-1 path unreachable). Reopening CLOSED `sd36sc` is a **PM scope call**.
4. **xlqpju** — confirm `IGlobalStreamQuery` is the accepted global-ordering capability seam → close-as-satisfied.
5. **wc85fx/aknqta/y0robr** — per-subject `IFieldEncryptor`/`SubjectFieldCryptor` injection placement across the 4 decorators (currently inert `_defaultContext`).
6. **D2 ≡ L5** — leader split-brain: rule the CAS+grace+TimeProvider seam **once**, single owner.
7. **D1 / L11-L12** — tenant capability-marker structural inseparability (S886 class).
8. **jrbf4r** — narrow the phantom set (preserve live `Delivery.EventStore`); event-id-range reassignment.
9. **8cnpj4** — sign off the 14 load-bearing postconditions before TestsDeveloper derives the conformance locks.

## 9. Phantom-close / dedup action list (for PM integration — tracker writes PM-only)
- **CLOSE-as-satisfied:** `xlqpju` (IGlobalStreamQuery), `s8m5u1` (deriver committed; run once to confirm 42/42).
- **CLOSE-as-dup:** `3q1jtm` → `uw1nv4` (after confirming uw1nv4 delivered scope covers PG+Oracle, forge-integration cl.8).
- **NARROW:** `y0robr` → inbox+projection (outbox = `aknqta`).
- **EPIC near-close:** `02sj2h` (verify dependents), `w2zq7d` (open tail 2tf65w+s4kwiv only).
- **RE-SCOPE:** D3 (conformance-lock only, claim already shipped). **VERIFY-FIRST:** D4 (vs S884 — may be close-as-satisfied).
- **NEW child beads:** sd36sc 5-store triage; uahb0i inbox+projection slices (if not covered by narrowed y0robr); the Dijkstra D1-D7, Liskov L1-L14, Metz M1-M13 audit children (create per §5).

## 10. Non-negotiables carried into IMPLEMENT
- Non-skipped real-infra locks binding the DEFAULT serializer/client; assert emitted behaviour, not registration presence.
- Author≠impl regression locks, RED-on-pre-fix; every safety arm paired with a liveness arm.
- F-5 cross-project sweep on every type-contract change (capability markers, new columns, new interface members).
- Independent REVIEW_CODE reads impl on committed mainline (the S887 net that caught 4 CI-invisible own-keystone defects).
- PM is the only committer; coupled impl+lock per lane; CLOSE gates run SERIALLY.

---

## 11. Created child beads (SPEC output — tracker writes done in DB; PM flushes jsonl)

**Dijkstra (parent `haqhcm`):** `15ph5g` D1(P0) · `0qyitl` D2(P0, carries L5) · `xeo795` D3(P1 re-scoped) · `mup0ui` D4(P1 verify-first) · `5uajzo` D5(P2) · `h9nlsf` D6(P2) · `rzr5zs` D7(P2).
**Liskov (parent `8cnpj4`):** `eh0if2` L2 · `3gaywc` L3 · `vomqoe` L4 · `2nmc1e` L6 · `8z65sn` L7 · `0j5oub` L8 · `uo90tv` L9 · `c4u6mj` L10 · `vlky2n` L11(P1) · `cvvfo6` L12 · `lkdfb1` L14. (L5 folded into D2 `0qyitl`; L1→existing `nq761s`; L13→existing `s2wmw1`.)
**Metz (parent `mgwuiu`):** `owxhc8` M1(P1) · `5ir4bv` M3 · `0reyda` M5 · `iu0ooe` M6(P3) · `8mnnvj` M8 · `h5ed5e` M9 · `2j3slx` M10 · `bfak2b` M11(P3) · `bg157e` M12 · `yz7zz4` M13 · `rna328` M-sched · `xbg37o` DO-NOT-TOUCH register(P3). (M2→existing `3cvkow`; M7→existing `8ux18h`.)
**Provider:** `t3hwan` sd36sc 5-store triage(P2).
**Closed:** `xlqpju` (IGlobalStreamQuery), `s8m5u1` (deriver committed). **Left open for PM to close at integration:** `3q1jtm`→`uw1nv4` (after uw1nv4 covers PG+Oracle, forge-integration cl.8).

### Loose ends for PM/GUIDE
- **M4 (audit-envelope ×9)** — NOT created: `bd-file.sh` false-positive-matched my just-created M3 `5ir4bv` (≥0.6 title heuristic). M4 is a distinct §5 slice → PM `--force` create under `mgwuiu`.
- **L1 (IEventStore append→return-not-throw)** — existing home `nq761s` is **CLOSED**; PM may reopen for the conformance-test deliverable, or file a fresh child.
- **aknqta / wc85fx** already exist (uahb0i outbox/snapshot); all 4 GDPR surfaces have homes (inbox+projection = narrowed `y0robr`).
- **SA-seam rulings** (§8) awaited at GUIDE before TestsDeveloper derives the L-family conformance locks.
