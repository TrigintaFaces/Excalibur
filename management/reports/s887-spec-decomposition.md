# S887 SPEC Decomposition (BLUEPRINT) — Premise-Gated Lane Map

**Author:** ProductManager · **Baseline (source):** `4b284ceb0` · **Phase:** SPEC → GUIDE
**Method:** every in-scope bead premise-gated vs baseline (run→read→cite). Scope shrank from the nominal
~100 (1 P0 + 77 P1 + P2 top-up) to the surviving **real** work — shrink is correct per operator directive.

## Headline
- **77 P1 + 1 P0 gated.** ~24 ALREADY-SATISFIED phantoms → close. ~20 REPRODUCES real work. ~7 design/feature or infra-gated → scope-owner ruling. 9 P1 epics → defer/slice.
- **2 real P0s:** `tj6qvl` (crypto-shred decorator unreachable) + `88xrgq` (GDPR erasure vacuous coverage — was mis-flagged "blocked", now UNBLOCKED).
- Dedup `prd3xg~xx6wd8` = both already closed (doc-only, canonical `prd3xg`). `uysw7y/vfssk3` = NOT a dup (mistaken pairing).

---

## IN-SCOPE LANES (real work — disjoint, single-owner)

### Lane A — Provider correctness (owner: BackendDeveloper). All require NON-SKIPPED real-infra locks.
| Bead | Work | Lock |
|---|---|---|
| tj6qvl (P0) | *(see Lane B — crypto-shred; keep with B)* | — |
| 1m19p6 | ORA-08177 transient-retry in SERIALIZABLE append (timestamp+INSERT-ALL fix already landed S886) | real-Oracle, deterministic no-lost-update |
| tfszov | Oracle outbox unquoted-alias casing → empty read-back (GetScheduledOutboxMessages.cs:39-40) | real-Oracle, empty-must-FAIL |
| l9c3cv | Postgres+SqlServer Inbox → canonical TenantScope.FromContext (context-less=None; fail-closed only MT-active-unresolved) | real Postgres+SqlServer |
| ljbwh8 | Oracle Inbox tenant-parity (AFTER l9c3cv) + composite-PK DDL | real-Oracle tenant-isolation |
| y5tn3e | **Narrowed to Postgres** outbox corr/caus/priority carry (SqlServer already-satisfied; Oracle add `priority` col) | real-Postgres round-trip |
| y6a3l8 | Oracle 3rd Guid raw bind (QuerySagaSummariesRequest.cs:116 `.ToByteArray()`) + Oracle admin-read deriver | real-Oracle admin seam |
| 3q1jtm | Postgres outbox fencing-token CAS on completion (DeleteOutboxMessage.cs:33-36; SqlServer is ref) | real-Postgres two-leader |
| 0yy2sp | InMemoryInboxStore.EvictOldestEntry status-blind (:591-602) → evicts Processed | pure unit (safety∧liveness) |
| ws03wt | Fixtures execute shipped DDL scripts, not embedded copies (create canonical eventstore DDL first) | real-container script round-trip |

### Lane B — REVIEW fast-follows + LeaderElection (owner: FrontendDeveloper + TestsDeveloper locks)
| Bead | Work | Notes |
|---|---|---|
| tj6qvl (P0) | Key-preserving DecorateEventStore (mirror DecorateProjectionStore DescribeKeyed) so crypto-shred decorator reachable via keyed-"default" | **SA seam**; real-DI lock asserts ciphertext-at-rest (safety) + boot/round-trip (liveness) |
| vttjcz | Author safety∧liveness lock for OutboxProcessorJob leader gate (wired, untested) | TestsDeveloper; ShouldProcess false⇒not dispatched / true⇒dispatched / no-gate⇒runs |
| b0hghp | MonotonicFencedResourceGuard `<=`→non-decreasing (rejects K8s token-0 + per-tenure repeat) | **SA SEAM RULING — inverts documented invariant + its lock**; F-5 flip Reject_AnEqualToken; docs fix; unit lock |

### Lane C — bd/tracker tooling (owner: PlatformDeveloper) — MOSTLY PHANTOM (S882 ledger removal)
| Bead | Work |
|---|---|
| uebl66 | Wire DB-ahead-of-committed-HEAD staging check into pre-commit (bd-flush-guard --verify-staged unwired). Safety: stale/absent jsonl while DB ahead REJECTED; liveness: code-only commit (DB not-ahead) ALLOWED |
| lnzjzz | (low) `bd update -d` structural refuse/append wrapper |
| k9rhut / xvqvke | (low/latent) doc-note or guard — recommend downgrade |
| anrdho / 0wjdjl / 0he3g1 | KEEP — runtime-repro (anrdho, 0wjdjl) / Integration.Tests ProjectReference audit (0he3g1) |

### Lane D — governance / gate / CI wiring (owner: PlatformDeveloper or TestsDeveloper)
| Bead | Work |
|---|---|
| 527ciw | validate-sprint-plan.sh add file→owner collision check |
| 1b54fe | BannedApiAnalyzers in tests/Directory.Build.props (Microsoft-first; 210 raw Task.Delay) |
| ug47j4 | CI trigger on chore/dev-team-six branch |
| 79ssi6 | Machine-readable freeze flag read by pre-commit + poll hook (S880 mechanism) |
| sthdvg | spa-gate compare wwwroot/index.html (CSP), CRLF-normalized |
| ssv79t | Deterministic .slnf add EventSourcing.Handlers.Tests OR drop superset claim |
| 4oqjp0 | Namespace-oracle manifest-completeness guard (comment-half already fixed) |
| mg8ln9 / 0ew5y7 | RULES-DOC clause-adds (testing-patterns §3 fixture-vacuity; verify-before-claiming negative-result) — low-risk parallel fill |
| s25n17 | SOC2 CNF-003/SEC-005 fail-closed on null dep (P2 top-up; SEC-005 degraded-PASS = SA decision) |
| ltfo0a | Wire bd-single-daemon-guard + bd-verified-write as ACTIVE pre-commit checks (P2 top-up) |
| vd3cwu | Triage/classify ~40 gate-shaped scripts (P2 top-up; wiring = follow-up beads) |

### Lane E — OPCOM / poll-hook / rules infra (owner: PlatformDeveloper)
| Bead | Work |
|---|---|
| 0fhf1f | Rewrite ~12 .claude/agents/ + .claude/workers/ files: drop `--status ready_for_*` (silent no-op) → in_progress + comment/OPCOM handoff. Grep gate |
| p0l8rk / rhfehg / 7h7srz | **OUT-OF-REPO (OPCOM server D:\claude_projects\opcom)** → route/defer; 7h7srz optional partial client mitigation |
| rn71ob | KEEP watch (= forge-integration clause 9, sub-threshold by design) |

### Lane F — docs / public-surface (owner: DocumentationWriter + FrontendDeveloper)
| Bead | Work |
|---|---|
| ftwim0 | Fix `void ApplyEventInternal` → `bool` in src XML-docs (AggregateRoot.cs:24,74,166; KeyedAggregateRoot.cs:40) + ~24 docs-site + 2 templates. grep void ApplyEventInternal = 0 |
| mxy5rv | Sweep ~54 files invalid bd status tokens (in-progress→in_progress; distinguish bd-cmd vs OPCOM task-status) |
| doc-phantoms | encryption-migration.md:222/223/370 AddEncryptionProvider→AddEncryption(builder); :299-302 Store-Decorators table (EncryptingEventStore→*Decorator, EncryptingOutboxStore→*Decorator, EncryptingSagaStore=NO type); message-encryption.md:140 AddStoreEncryption→AddEncryption+AddInboxEncryption/AddOutboxEncryption |

---

## CLOSE AS ALREADY-SATISFIED (phantoms — premise no longer reproduces)
Lane A: 4jxsgg, umv1ub, 8d6i7k, 3o2u83, s8m5u1, sav3kz *(cite SHA a4c0b70a0)*, xlqpju
Lane B: 5fswhd *(b2e3aa286)*
Lane C: kqh6yf, f2dcru, ch5x5p, 4brswt, b9idqv, 8e5lmp, lxi1yq, 4z94uc
Lane D: 4ffcw1, bjxlc2, qvfbvu, 48ay30
Lane E: a3pwqc, 4c0sfv
Lane F: 9gv8zu *(Version/AggregateId already removed S884)*
Carryover: 4uubd1 *(phantom P0)*, 3ttcqv, u4x8sb *(corr/caus satisfied for Oracle — PM confirm Oracle outbox RED count first; residual→y5tn3e/tfszov)*

## NEW BEADS TO FILE
- nxjn2k residual: scoped real-infra exhaustion regression locks per provider (6 providers translate; all untested). *(rescope nxjn2k or new bead)*

## SCOPE-OWNER RULINGS NEEDED (route at GUIDE)
- **SoftwareArchitect seams:** b0hghp (invert documented fencing invariant + lock), tj6qvl (key-preserving decorate seam), 4oqjp0 (manifest-completeness guard oracle), uw1nv4 (cloud-native ETag fencing seam).
- **SA/PM feature-scope (MS-first "build the fix, not document the limitation"):** guejd9 (durable IWorkflowSignalInbox), y0robr (per-subject field encryption — clarify per-tenant vs per-DataSubjectId), lh1i1q (CDC single-active-consumer lease).
- **PM decisions:** SEC-005 degraded-PASS vs fail-closed (s25n17); EncryptingSagaStore docs-fix vs missing decorator; u4x8sb close vs carry; whether to top-up scope with more P2 clusters to hit ~100 (honest surviving scope is smaller).

## DEFER / SLICE — P1 epics (do NOT pull raw into lanes)
haqhcm (Dijkstra audit), 8cnpj4 (Liskov audit), 66wjpx (provider expansion), nv3nou + waxs7f (dashboard), infiyf (10.x release-readiness), 02sj2h (exactly-once), tteeng (AI-agent-native), 3yvqf2 (durable-execution), w2zq7d (migration tooling). Also: mxanei (TestAggregate totality — slice from Dijkstra), bq7w1f (onboarding), s4kwiv (MediatR codemod pre-launch).

## GATED-INFRA (operator/env)
63xsiv (Cosmos Linux emulator non-functional — blocks all Cosmos real-infra), ajt1iy (needs Cosmos SDK≥3.60 bump, gated on 63xsiv).
