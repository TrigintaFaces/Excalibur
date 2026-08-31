# S889 Sprint Guidance — COMPASS (GUIDE)

**Mission:** 237 · sprint-889 · band ~100 · **GUIDE/COMPASS** (SoftwareArchitect)
**Baseline (real committed HEAD):** `42b8261ea` — ⚠ plan cites `6cc8e6bf4`; S888-retro commits landed after. **Premise-gate every remaining bead vs `42b8261ea`, not the plan's stale hash.**
**Type:** correctness-AUDIT continuation (Dijkstra / Liskov / Metz) + S888 correctness carry. `skipStrategy` — no new product bet.
**Inputs:** `management/specs/mini/sprint-889/decomposition-index.md` (SPEC/BLUEPRINT), `bd ready --json`, `<team-discoveries>`.

SA carries **no impl lane** — owns GUIDE seam rulings; IMPLEMENT integration PM-owned; per-bead impl+lock pattern per SPEC.

---

## 1. Execution order (priority)

1. **P0 LEAD — 15ph5g / D1** (tenant scoping capability marker structurally inseparable from wiring). Seam pinned below. **SINGLE OWNER** across the marker/wiring surface (== Liskov L11/L12 tenant half).
2. **P1 correctness carry — xal5q6** (delete dead `EnforceOutboxFence`), **sd36sc** (fencing default), **y0robr** (GDPR erasure), **vlky2n / L11** (AWS crypto-shred false-cert), **xeo795 / D3** (ClaimDueTimeouts atomic lock).
3. **P1 Metz spine — owxhc8 / M1** (outbox mapping single home), **mgwuiu / xbg37o** family.
4. **Liskov 8cnpj4 family** — one load-bearing postcondition per family.
5. **P2 audit fill** — after PM trim ruling (§4).

---

## 2. Seam rulings (WWMD-pinned — `pin-interface-seam-before-tests`)

### D1 / 15ph5g (P0 LEAD) — capability marker MUST be emitted by the wiring, never registered beside it
**RULING (structural, `enforce-invariants-structurally` / S886 rw2ull):** `ITenantScopingCapability<T>` is produced **by the SAME factory/ctor that injects `ITenantContext` into the store** — *no marker unless the dependency was actually supplied.* A store constructed without `ITenantContext` MUST be structurally incapable of carrying a truthful marker. The requirement check (`RequireTenantScopingCapability<T>`) resolves the **real consumer** and inspects **emitted behaviour** (store carries `ITenantContext` → produces a tenant predicate), NOT a standalone registered flag.
**Current defect:** per-provider `TenantScopingCapabilityMarker.cs` (Outbox/Inbox/Saga/EventSourcing × PG/Oracle/SqlServer) are **separately-registerable flags** — the exact S886 "lying marker" surface. Fold emission into the store factory so the marker and the wiring are **one structural act**.
**WWMD:** Microsoft never registers a capability attestation independent of the thing it attests (keyed-service resolution reflects the *real* registration; health capabilities come from the registered checks). Match that — marker is a *consequence* of wiring, not a sibling.
**SINGLE OWNER:** D1 == tenant half of **Liskov L11/L12**. ONE impl owner touches the marker/wiring seam across all providers (`coordinate-before-parallel-work` — do NOT let D1 and L11/L12 edit marker files in parallel). TestsDeveloper authors the author≠impl lock: **assert the property** ("store built without `ITenantContext` cannot advertise the capability / a scoped read cannot cross tenants"), non-vacuous (RED on the separately-registered flag), safety **and** liveness (tenant A *does* see A's row; tenant B does *not*).

### sd36sc (P1) — fencing opt-in → DEFAULT is a shipped-LE **behavior change** → needs PdM + SA
**RULING (SA structural, pending PdM scope concurrence — flagged §3):** Default-ON fencing for framework-protected resources is the correct **safe default** (WWMD: secure-by-default; Microsoft ships the safe posture and makes the unsafe one explicit opt-out). Structural fail-safe: a provider that **cannot** fence a framework-protected resource **fails fast at startup** (ADR-336), never silently runs unfenced. `t3hwan` decides which NoSQL outbox providers get leader-gated fencing vs an explicit opt-out capability.
**Blast radius (non-optional):** this changes observable behavior — update log/telemetry strings + docs that narrate the old opt-in behavior (DOCS lane). **PdM owns** the product decision "default flips"; SA owns the structural fail-safe shape.

### y0robr (P1 GDPR, blocked → unblocked at GUIDE) — build the fix or prove-uncoverable, never fake success
**PdM ruling stands (re-affirmed):** **NO no-op / exemption to force erasure success.** Either migrate Outbox/Inbox/Projection surfaces to `SubjectFieldCryptor`, OR declare a surface uncoverable with an **independently-verified retention argument** filed as a tracked bead. An erasure that cannot complete **surfaces**, never certifies success (fail-closed).
**SA crypto seam:** key material via `System.Security.Cryptography` (`RandomNumberGenerator`) / DataProtection key-ring — **never** `Guid.NewGuid`/`Random`. *Which surfaces are in scope is a PdM call (§3).*

### vlky2n / L11 (P1) — crypto-shred must not issue a false erasure certificate
**RULING:** `AuthorizationEffect.Permit != default(0)` — a defaulted/zero effect must not read as Permit (the Liskov weakened-postcondition trap). AWS crypto-shred returns a certificate **only** when the key was actually destroyed; a no-key / failed-destroy path returns failure, not a success cert. Lock: RED when a defaulted effect or a no-op shred yields a "success" certificate.

### xal5q6 (P1) — delete dead `EnforceOutboxFence`, RE-CONFIRMED DEAD
**SA independent re-confirm (run→read→cite vs `42b8261ea`):** `grep 'new EnforceOutboxFence('` → **zero** `src/**` hits (only `.beads/*.jsonl` bead-text; positive control passed — engine found those hits). Type survives only as `EnforceOutboxFence.cs` (PG + Oracle declarations), 4 `PublicAPI.Unshipped.txt` lines, and 2 stale fixture comments. CAS folded into `FencedReserveOutboxMessages`/`FencedDeleteOutboxMessage` (internal).
**RULING:** DELETE both files + remove the 4 `PublicAPI.Unshipped.txt` lines + fix the 2 stale fixture comments (`OracleOutboxStoreContainerFixture.cs:124`, `PostgresOutboxStoreContainerFixture.cs:138`). Shipping it = advertised dead **public** API + internal-first violation (live replacements are `internal`). **BLOCKING-before-CLOSE.** Owner: PlatformDeveloper (lane C) or PM-fold at CLOSE. Zero-risk (unshipped, no consumer ever had it).

### 8cnpj4 family (Liskov) — name the ONE load-bearing postcondition
Each conformance family names the single postcondition the test must **fail without**: no strengthened precondition, no weakened postcondition across impls, preserved invariants, honored history constraint. Lock non-vacuous + safety+liveness; ≥1 fixture implements the interface **directly** (no first-party base supplying the member — `testing-patterns §3`).

### mgwuiu / xbg37o / owxhc8-M1 (Metz) — earned vs accidental duplication
Maintain the **DO-NOT-TOUCH register** so extractions don't inline the WRONG abstraction (Metz: duplication is cheaper than the wrong abstraction). `owxhc8/M1` (outbox `FromOutboundMessage` mapping → one injected home) is a legitimate **earned-single-home** extraction — the mapping is one concept drifting across providers. Guard: the extracted home is *injected* (a seam), not a static god-helper.

---

## 3. Needs a ruling from another owner (raise now, don't block build on assumption)

| Seam | Owner | Question |
|------|-------|----------|
| **sd36sc** fencing default-flip | **ProductManager** (behavior change) + **PdM/`t3hwan`** | Confirm default-ON for framework-protected resources; which NoSQL outbox providers get leader-gated fencing vs opt-out. |
| **y0robr** GDPR scope | **ProductManager** | Which of Outbox/Inbox/Projection surfaces are in-scope for `SubjectFieldCryptor` migration vs declared-uncoverable-with-proof. |
| **Trim −11** | **ProjectManager** (sprint-scope owner) | Confirm the 11 lowest-value P2 cuts (§4). |

## 4. Trim to land ~100 (SPEC handoff)
- **−1 mup0ui / D4** — SA concurs SPEC premise-gate: PHANTOM, close-as-satisfied. Contiguity guard already on mainline (`AggregateRoot.cs:243-272`, S884 K2/K3 envelope-authoritative). **Do NOT build.**
- **−11 lowest-value P2 cuts** — **ProjectManager confirms** (recommend from SPEC candidate pool: Liskov-P2 exploratory `u1v1yf, 5n26r8, lnabsr, zw9uv3, s2wmw1` + Metz-P2 cosmetic tails + redundant harness self-tests). **Keep full Dijkstra spine, all P0/P1, all provider-fidelity + GDPR.** Cut fill, never coverage.

## 5. WWMD design gate — per lane, MANDATORY before build
- **No advertised-but-unwired seams** (ADR-336): every declared interface/marker gets a **wired AND tested** AC end-to-end (D1 marker emitted by *real DI resolution*, not a separate flag).
- Any capability an AC introduces (retry/cache/pool/hosted/options/time/telemetry/crypto/DI) names the **BCL/first-party primitive it builds ON** — hand-rolled equivalent = decomposition defect.
- Crypto ACs name `RandomNumberGenerator` / DataProtection key-ring — never `Guid.NewGuid`/`Random` for key material.

## 6. Great-minds pre-mortem
Deliberately **skipped** persona dispatch at GUIDE — the sprint body **is** the Dijkstra/Liskov/Metz audit; the lenses run at IMPLEMENT/TEST on each bead. D1 keystone already grounded in `enforce-invariants-structurally` (S886 rw2ull) with the exact defect named; no marginal pre-mortem value.

---

## 7. Decomposition validation (spec-decompose pass — VALIDATE, not re-author)

SPEC/BLUEPRINT already produced the lane spine + per-bead impl+lock pattern (each audit bead = its own unit-spec on the bead — anti-over-decompose is deliberate; NOT 100 MS files). SA validation against the 3-pass method:

- **Pass 1 (Surface) — OK.** Audit spans Domain / Data(providers) / Infra(LE, DI) / Testing(conformance locks) / Docs. Each bead 1–2 layers.
- **Pass 2 (Journey) — OK.** 3 spine epics (`haqhcm` Dijkstra, `8cnpj4` Liskov, `mgwuiu` Metz) + S888 carry + DX/migration + shipped-doc defects. No orphan/duplicate — each bead maps to exactly one lane owner.
- **Pass 3 (Risk / contention) — ONE real contention flagged + resolved:**
  - **`TenantScopingCapabilityMarker.cs` × {Outbox,Inbox,Saga,EventSourcing} × {PG,Oracle,SqlServer}** + the store factories are touched by **both D1 (15ph5g)** and **Liskov L11/L12**. **Resolution = Option A (single edit surface, SINGLE OWNER):** one impl owner edits the marker/wiring seam across all providers; TestsDeveloper authors author≠impl locks only. PM assigns the single owner at IMPLEMENT dispatch (`forge-integration-conventions` clause 7 — pin disjoint file ownership up front). Do NOT run D1 and L11/L12 as parallel edits.
  - All other lanes disjoint by file ownership (A Tests / B Backend / C Platform / G Frontend / DOCS) — no further contention.
- **WWMD gate — OK.** Every declared marker/interface carries a wired-AND-tested AC; crypto beads name `RandomNumberGenerator`/DataProtection; no advertised-but-unwired admitted (ADR-336).

No mini-spec files re-authored (would be a second source of truth vs the bead unit-specs). Decomposition endorsed as a sound audit spine.

---
*COMPASS · SoftwareArchitect · seam rulings input to owners, not final where §3 flags another owner.*
