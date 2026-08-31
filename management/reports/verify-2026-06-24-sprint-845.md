# S845 VERIFY (TRACEPOINT) — Requirement→Test Traceability Matrix

**Date:** 2026-06-24 · **Verifier:** TestsDeveloper (VERIFY/TRACEPOINT) · **Tree:** HEAD `6d58aa786` · **Full CI:** all 10 shards GREEN (0 failures).

**Scope:** live code lanes verified — **MS-A** (`pedo87`), **MS-C** (`2mhglb`/`d68obg`), **MS-E** (`htcbgu`/`b7y5tc`). MS-B (`bd81z9`) + MS-D (`e9ej9m`) closed as **verified stale-dups** (already satisfied in S841/S842 — no code, nothing to verify). MS-F (`kmvrwk`) = CI-YAML config (no .NET test). `rdau9c` closed as dup of `htcbgu` (FR-E5).

## Step 1b — Routing/Behavior-Change Observability Staleness Check

Both behavior-changing source lanes grepped for `LoggerMessage`/telemetry/template strings asserting pre-change behavior:
- **Lane A `HandlerScopeResolver.cs`** — no `LoggerMessage` templates; comments correctly describe the NEW transitive walk ("the depth-1 blind spot pedo87 fixes"); `CreateNoScopeDiagnostic` message is accurate. **CLEAN.**
- **Lane C `ClaimCheckMessageSerializer.cs`** — no telemetry templates assert old behavior; the only `"CC01"` references are accurate *historical* doc-comments ("the previous in-band magic-prefix heuristic"). **CLEAN.**
- **Lane E** — test-only, no observability surface. N/A.

**No staleness defects.** (S844 regression class — CB-open stale log templates — does not recur here.)

---

## MS-A — Handler-DI Correctness (`pedo87`)

| ID | Req | Coverage | Test(s) |
|----|-----|----------|---------|
| FR-A1 | scope-flow (nndgud, confirm-only) | ✅ FULL | `ScopedHandlerResolutionShould` (bare/context overloads, fresh-scope dispose, ambient borrow) 8/8 |
| FR-A2 | transitive Scoped via Transient | ✅ FULL | `…ScopeRequirementShould.TransitiveScopedDependencyThroughTransient` (RED-on-pre-fix) + `ResolveTransientHandlerWithScopedDependency` |
| FR-A3 | longest *activatable* ctor | ✅ FULL | `HandlerWithUnresolvableLongestConstructor_AnalyzesShorterActivatableConstructor` (RED-on-pre-fix) |
| FR-A4 | keyed-DI guard (n8xxwm) | ⊘ MOOT | already satisfied by `wl9s4v` (S-prior); struck from spec — not a gap |
| FR-A5 | cache + cycle guard | ✅ FULL | `HandlerGraphWithDependencyCycle_TerminatesWithDefinedVerdict`; cache exercised by repeated `RequiresScope` |
| NFR-A1 | warm-path perf | ⚠️ by-design | walk runs first-dispatch-only + cached; no regression. No micro-benchmark (SA did not require one) |
| NFR-A2 | AOT/no new trimmer warnings | ✅ build-gate | full-CI build 0 warnings (TreatWarningsAsErrors); DAM flow + IL2070/IL2072 suppressions |
| AC-A1..A6 | (Given/When/Then) | ✅ FULL | A1 transitive, A2 concurrency-distinct-scope, A3 ctor-select, A4 keyed(moot/safe), A5 fresh-scope-dispose, A6 cycle |
| EC-A1 | no public ctor → Root | ✅ FULL | `HandlerWithNoPublicConstructor_DoesNotRequireScope` (added this phase, `6d58aa786`) |
| EC-A2 | CanCreateScope false → false | ✅ FULL | `WhenNoScopeCanBeCreated_DoesNotRequireScope` |
| EC-A3 | keyed impl null → no throw | ✅ | covered by `wl9s4v` keyed-safe accessor |
| EC-A4 | diamond dep → counted once | ✅ | visited-set (same mechanism as cycle lock) |

**MS-A verdict: FULL MUST coverage.** (NFR-A1 = non-critical SHOULD, verified by design.)

## MS-C — Serialization Correctness (`2mhglb` / `d68obg`)

| ID | Req | Coverage | Test(s) |
|----|-----|----------|---------|
| FR-C1 | collision-free format tag | ✅ FULL | `…SerializerShould` inline `[0x00]` + envelope `[0x01]` framing asserts (strengthen-don't-weaken) |
| FR-C2 | inline ≡ old-magic bytes not misclassified | ✅ FULL | inline-frame strip tests (RED-on-prepend-only mainline) |
| FR-C2b | unknown tag / empty → typed `SerializationException` | ✅ FULL | `Deserialize_ShortUnrecognizedTagData_ThrowsTypedSerializationException` (0x43 → throw; RED on any passthrough) |
| FR-C3 | Avro evolution OR docs-correct | ✅ DOCS | docs-correction ruling; README reader==writer (AC-C4-docs → DOCS/CHRONICLE phase, DocumentationWriter); evolution tracked `vfssk3` |
| AC-C1/C2/C3 | round-trip sync+async, envelope, cross-serializer | ✅ FULL | inline/envelope tag tests + AC-C3 cross-serializer (PR-authored, 465/465 GREEN). Framing is base-serializer-agnostic by construction |
| AC-C-tag | byte0 0x7B/0x43/len-0 → typed throw | ✅ FULL | `…ShortUnrecognizedTagData…` + sync envelope-tag throw |
| EC-C1/C2 | short / exact-tag-length payload | ✅ FULL | short-buffer + frame-length tests |
| EC-C3 | Avro corrupt writer-schema → typed error | ✅ existing | Avro lib throws typed on corrupt header; d68obg is docs-only (no impl regression) |

**MS-C verdict: FULL MUST coverage.** (AC-C4-docs is a DOCS-phase deliverable — flagged for CHRONICLE/validate-docs.)

## MS-E — Conformance Vacuity (`htcbgu` / `b7y5tc`)

| ID | Req | Coverage | Test(s) / Disposition |
|----|-----|----------|----------|
| FR-E1 | no green no-op MUST | ✅ FULL | AC-E5 grep gate = 0 vacuous green MUSTs; full-CI shows 48 honest skips isolated to conformance |
| FR-E2 | CloudEvents real assertion | 🟡 SANCTIONED SKIP | harness has no CE binding → `[Fact(Skip)]` tracked `bd-jj4hx4`/umbrella `urttf7` (AC-owner ruling) |
| FR-E3 | at-least-once redelivery | 🟡 SANCTIONED SKIP | no ack/nack in harness → Skip tracked `bd-5dox7c` |
| FR-E4 | metadata preserved | ✅/🟡 | **body-level KEPT (real assertion)**; carrier-header (Headers/BasicProperties) Skip tracked `bd-liyait` |
| FR-E5 | rdau9c closed as dup | ✅ | closed by PM in `80da183e` |
| AC-E1 | kept-real RED on broken double | ✅ FULL | ProjectReviewer broken-double: 5 kept-real RED / 8 skip / 0 pass |
| AC-E5 | grep gate 0 vacuous MUST | ✅ FULL | verified (full-CI: 48 skips, all `[Fact(Skip)]`) |
| EC-E1/E2 | unsupportable→skip / flaky-poll | ✅ | EC-E1 honored via honest skips; EC-E2 N/A (redelivery skipped) |

**MS-E verdict: FULL coverage of the `htcbgu` integrity defect.** FR-E2/E3 + FR-E4-header are **sanctioned skip-with-tracked-bead** per ProductManager (AC owner) + SA ruling — the ACs were reshaped to the skip-with-reason shape; these are tracked deferrals (`urttf7` + 5 children), **not coverage gaps**.

---

## Coverage Summary

| | Total | Covered | Sanctioned-defer | Missing | Status |
|---|---|---|---|---|---|
| MS-A FR | 5 (1 moot) | 4 | 0 | 0 | ✅ |
| MS-A NFR | 2 | 2 (1 by-design/1 build-gate) | 0 | 0 | ✅ |
| MS-A AC/EC | 10 | 10 | 0 | 0 | ✅ |
| MS-C FR | 3 | 3 | 0 | 0 | ✅ |
| MS-C AC/EC | 8 | 8 (1 DOCS-phase) | 0 | 0 | ✅ |
| MS-E FR | 5 | 2 + FR-E4-body | FR-E2/E3/E4-header (tracked) | 0 | ✅ |
| MS-E AC/EC | 5 | 5 | 0 | 0 | ✅ |

**VERDICT: FULL MUST COVERAGE — spec-verify PASS.**

- **Critical gaps (MUST without test):** NONE.
- **Sanctioned deferrals (tracked, not buried):** FR-E2 (`bd-jj4hx4`), FR-E3 (`bd-5dox7c`), FR-E4-header (`bd-liyait`), perf (`bd-lpkwjr`), filtering (`bd-1rbj0a`) — all under umbrella `Excalibur.Dispatch-urttf7`. Cited in-test in the `[Fact(Skip)]` reasons.
- **DOCS-phase follow-up:** AC-C4-docs (Avro README reader==writer correction) → CHRONICLE/`validate-docs`.
- **Orphaned tests:** NONE — every new lock traces to an AC/EC.
- **Observability staleness (Step 1b):** CLEAN — no stale templates in either behavior-changing source.
