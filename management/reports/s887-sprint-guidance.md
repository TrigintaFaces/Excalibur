# S887 Sprint Guidance (COMPASS) — Execution Plan + SoftwareArchitect Seam Rulings

**Author:** SoftwareArchitect (GUIDE/COMPASS) · **Sprint:** sprint-887 · **Baseline:** `4b284ceb0`
**Source of truth for scope:** `management/reports/s887-spec-decomposition.md` (BLUEPRINT). This guidance
does NOT restate the lane map — it pins the **architectural seams** and the **execution order**. Where this
doc and the decomposition disagree, the decomposition's lane/owner assignment wins; the *seam rulings below
are authoritative* (architecture is my call).

---

## 0. Execution order (priority)

1. **P0 seams first — `tj6qvl` (crypto-shred) + `88xrgq` (GDPR erasure).** These are correctness/PII P0s;
   land them before the P1 provider work.
2. **Lane A provider correctness** (BackendDeveloper) — all require **NON-SKIPPED real-infra locks**
   (`verify-against-real-infra-not-mock`). `l9c3cv` before `ljbwh8` (parity depends on canonical TenantScope).
3. **Lane B review fast-follows** — `b0hghp` (seam ruling below), `vttjcz` (leader-gate lock).
4. **Lanes C/D/E/F** governance/docs/tooling — parallelizable, lower blast radius.
5. **Close-as-satisfied phantoms** (~24) — PM/owners confirm premise-no-longer-reproduces (cite SHA) and close.

Every implement→test handoff is coupled (`forge-integration-conventions`): impl + its non-vacuous
real-DI/real-infra lock land in **one** settled commit before REVIEW.

---

## 1. SoftwareArchitect SEAM RULINGS (authoritative — grounded run→read→cite)

### 1.1 `tj6qvl` (P0) — Key-preserving `DecorateEventStore` — RULING: mirror `DecorateProjectionStore`

**Grounded:** `EventSourcingUtilitiesServiceCollectionExtensions.cs:390-412` — `DecorateEventStore`
re-registers **non-keyed** unconditionally (`services.Add(new ServiceDescriptor(typeof(IEventStore), sp =>
…))`). The correct sibling `DecorateProjectionStore:419-445` branches on `descriptor.IsKeyedService` and
re-registers via **`ServiceDescriptor.DescribeKeyed(type, descriptor.ServiceKey, …)`**, building the inner
(undecorated) factory through the **keyed-safe accessors** (`GetImplementationInstance` /
`GetImplementationFactory` / `GetImplementationType` — `BuildProjectionOriginalFactory:451-474`) because raw
descriptor reads throw on keyed descriptors on .NET 8+.

**Failure it causes:** when the event store is registered keyed (repository resolves
`GetRequiredKeyedService<IEventStore>("default")`), `DecorateEventStore` removes the keyed descriptor and
re-adds a **non-keyed** one → the keyed-`"default"` resolve either boot-fails or resolves an
**un-decorated (plaintext-PII) store** → the crypto-shred decorator is unreachable. P0.

**Ruling (What Would Microsoft Do):** `ServiceDescriptor.DescribeKeyed` **is** the BCL keyed-registration
API; the projection sibling already uses it correctly. Make `DecorateEventStore` key-preserving by mirroring
it **exactly**:
1. Branch on `descriptor.IsKeyedService`; when keyed → `ServiceDescriptor.DescribeKeyed(typeof(IEventStore),
   descriptor.ServiceKey, (sp,_) => decoratorFactory(originalFactory(sp), sp), descriptor.Lifetime)`.
2. Build `originalFactory` via a `BuildEventStoreOriginalFactory` that mirrors `BuildProjectionOriginalFactory`
   (keyed-safe accessors) so the inner resolve never throws on a keyed descriptor and never re-enters the
   decorated registration.
3. Preserve the existing `bool` return contract (true = decorated).

**Structural follow-up (enforce-invariants-structurally — file a bead, do NOT silently fix out of scope):**
`DecorateSnapshotStore:360-380` carries the **same** non-keyed latent bug. The durable fix is a single
generic key-preserving `Decorate<TService>(services, factory)` helper that event-store, projection-store, and
snapshot-store all call — so "non-keyed re-registration" becomes **inexpressible**. Minimal fix this sprint =
mirror the projection pattern for the event store; the unify-into-one-helper refactor is a tracked follow-up
(file it, @ProjectManager route as a P2). Reason recorded per Microsoft-first: the BCL already provides
`DescribeKeyed`; three hand-rolled copies is the divergence risk, one helper removes it.

**Lock (TestsDeveloper, author≠impl, real DI):** register a **keyed `"default"`** event store +
`AddEventStoreEncryption` (crypto-shred) → `BuildServiceProvider()` → resolve the keyed `IEventStore` →
- **safety:** persisted payload is ciphertext-at-rest (the decorator is in the chain);
- **liveness:** it boots and a write→read round-trips the plaintext back (the store still works).
MUST be RED on current non-keyed HEAD (keyed resolve fails / returns plaintext), GREEN on fix. No hand-injected
substitutes — real container/`ServiceProvider` (`verify-against-real-infra-not-mock` S873 real-DI-resolve bar).

### 1.2 `b0hghp` — `MonotonicFencedResourceGuard` invariant — RULING: `<=` → `<` (non-decreasing / Chubby)

**Grounded:** `MonotonicFencedResourceGuard.cs:30` — `if (fencingToken <= _highWater) throw` = **strict**
monotonic. Two real defects: (a) a same-tenure leader stamping N operations with the **same** token has ops
2..N **rejected**; (b) a Kubernetes lease whose fencing token legitimately **starts at 0** is rejected on the
first op (`0 <= 0`). The XML doc (:8-9) documents the strict rule.

**Ruling (What Would Microsoft / Lamport Do — I applied the Lamport fencing lens myself):** the Chubby
sequencer / Lamport fencing rule is **non-decreasing**: a fencing token identifies a **tenure**, not an
operation, so equal tokens come from the *same* leader and are safe to accept; only a **strictly smaller**
token means an older (stale) leader is acting after a newer one and must be rejected. Change:

```csharp
if (fencingToken < _highWater) throw …   // reject ONLY strictly-older
_highWater = fencingToken;               // (equal is a no-op advance; fine)
```

This **inverts the documented invariant** (strict-greater → non-decreasing) — update the XML doc (:6-17)
accordingly. Safety is preserved: two *distinct* leaders can never share a token (the lock service issues
unique monotonic tokens per tenure), so accepting equal never admits a stale leader. Same-tenure replay
*idempotency* is a **different** concern (idempotency keys), not this guard's job — do not conflate.

**F-5 + lock (TestsDeveloper):** the existing `Reject_AnEqualToken` test asserts the OLD strict rule — F-5
**flip** it to `Accept_ASameTenureRepeatToken`. Add both arms (`testing-patterns §3`):
- **safety:** a strictly-older token (`present < highWater`) is **rejected** (`StaleFencingTokenException`);
- **liveness:** first-token-`0` is **accepted**; an equal same-tenure token is **accepted**; a strictly-greater
  token advances the high-water.

**Sentinel note (implementer):** `_highWater` inits to `0`, so first-token-0 is accepted by construction
(`0 < 0` false) — that is the desired K8s behavior, keep the `0` init. Do not "fix" it to `long.MinValue`;
that would change first-token semantics.

### 1.3 `uw1nv4` — cloud-native ETag fencing seam — RULING: server-side conditional-write, same rule

Cloud-native stores (Cosmos ETag / `IfMatch`, DynamoDB conditional `ConditionExpression`, Firestore
precondition) enforce fencing **server-side** on a stored high-water column — exactly as
`MonotonicFencedResourceGuard`'s own `<remarks>` (:11-17) states external-store resources do. Ruling: the
cloud seam must enforce the **same non-decreasing rule** as 1.2 via a conditional update keyed on the stored
token (`stored_token <= presented_token` accepts; strictly-greater stored rejects), NOT a client-side
read-then-write (TOCTOU). This keeps the fencing contract identical across in-memory and external substrates.
Deeper wiring detail → route to BackendDeveloper; I'll confirm at REVIEW_ARCH.

### 1.4 `4oqjp0` — namespace-oracle manifest-completeness guard — RULING: enumerate, fail-closed

The manifest-completeness guard must **enumerate** the real project set from the solution (source of truth)
and **fail-closed** on any project missing from the manifest — never sample. Non-vacuous self-test: a planted
missing entry must fail the guard (`testing-patterns §3` liveness+safety; `gate-full-guard-suite` enumerate-
don't-sample). Comment-half already fixed per decomposition; wire the oracle. Owner PlatformDeveloper.

---

## 2. SA/PM feature-scope items (Microsoft-first "build the fix, not document the limitation")

These need a **PM scope ruling** (are they in S887 or sliced?). My architectural lean, for PM's decision:

- **`guejd9` durable `IWorkflowSignalInbox`** — real gap; if in-scope, build on the existing inbox/outbox
  persistence seam (do NOT hand-roll a new store). Lean: slice to a focused bead, keep this sprint's P0/P1
  correctness the priority.
- **`y0robr` per-subject field encryption** — **needs PM/PdM clarification first**: per-tenant vs
  per-DataSubjectId key derivation is a requirements question, not architecture. Hold until clarified.
- **`lh1i1q` CDC single-active-consumer lease** — build on `ILeaderElection` (already exists); do not invent a
  second lease mechanism. Feature-scope → PM.

@ProjectManager / @ProductManager: rule scope on these three. I've pinned the *architecture*; the
*in-or-out-this-sprint* call is yours.

---

## 3. Blockers / gated (do NOT dispatch into these)

- **Cosmos real-infra (`63xsiv`, `ajt1iy`)** — Linux emulator non-functional; all Cosmos real-infra locks are
  blocked on operator/env. Any Cosmos-dependent AC must be marked infra-gated, NOT closed green.
- **Out-of-repo OPCOM server beads (`p0l8rk`/`rhfehg`/`7h7srz`)** — live in `D:\claude_projects\opcom`; route/defer.
- **P1 epics** (Dijkstra/Liskov audits, provider expansion, dashboards, durable-execution, exactly-once) —
  defer/slice per decomposition §DEFER; do NOT pull raw into lanes.

---

## 4. Verification bar (every lane)

- **Real-infra, non-skipped locks** for any external-system/DI-resolution fix (`verify-against-real-infra-not-mock`).
- **Safety ∧ liveness** on every guard/gate/isolation seam (`testing-patterns §3`) — a safety-only assertion
  passes on inert code.
- **F-5 cross-project sweep** on every type-contract/CUT/signature change before locks-done.
- **Clean impl-project rebuild** before citing any lock result (`clean-rebuild-before-trusting-locks`).
- **No internal refs** (bd-/S887/ADR-) in any public XML doc / docs-site / samples touched.

---

*Seam rulings 1.1–1.4 are architecture calls and final. Scope calls (§2) and lane assignment defer to
ProductManager / ProjectManager. Raise a grounded challenge if any ruling looks wrong at IMPLEMENT — I'd
rather fix it before code than at REVIEW.*
