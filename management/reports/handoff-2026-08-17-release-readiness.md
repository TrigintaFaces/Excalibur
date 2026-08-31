# Release-readiness handoff — 2026-08-17

**Goal in force:** no release P0s, all release P1s resolved.
**State at handoff:** `open release P0: 1 · P1: 72` (from 4 and 91). 25 commits.
**Not met.** Every lane hit its session limit; this records what a successor needs.

---

## Start here, in this order

### 1. `yopa8q` — silently ungated cloud-native outbox. Nobody is on it.

Cosmos, DynamoDB and Firestore register `ICloudNativeOutboxStore`, which does **not**
derive from `IOutboxStore`. The multi-tenancy gate keys on `IOutboxStore`, so it never
fires for them: those hosts start cleanly with **no tenant enforcement and no error**.

Higher severity than anything else open, because a rejected host is loud and an ungated
one is silent. The intended fix cannot reach them — the seam constrains
`where TStore : class, TContract` — so it needs the marker on the cloud-native contract
and the open-world sweep to pick it up. **Verify the sweep actually reaches a
self-registered contract before assuming it does.**

### 2. Verify ~20 uncommitted files before touching the tree

Two lanes stopped mid-flight without completion reports: audit-chain integrity
(`Excalibur.AuditLogging*`, integration tests) and the conformance kits
(`SnapshotStoreConformanceTestKit`, `AuditStoreConformanceTestKit`,
`ConformanceAmbientTenantContext`). Deliberately **not** committed — landing unverified
work is worse than leaving it. Uncommitted work is the only unrecoverable artifact here.

Also uncommitted and known-broken: `MongoDbOutboxTenantPartitionAdmissionShould.cs` — a
real-infra arm its author was still writing; the staged-snapshot gate caught that it does
not compile (assertion overload mismatch). Finish or delete it deliberately.

### 3. `6s97ed` — last open child of the last open P0

Hold released; the ruling favours it (`vs3hv8` closes instead). **Hard gate:** the
analyzer ships with *two opposing diagnostics or neither* — missing tenant term AND
spurious term on a key-addressed statement. The single-diagnostic version would have
mandated the defect that broke outbox delivery.

---

## Rulings owed — do not guess these

- **In-memory legal-hold reader divergence.** With an explicit tenant id, in-memory is
  *more* permissive than SQL (extra null arm). Narrowing hides legacy rows; widening
  diverges further. Opposite direction from the fix that just landed.
- **First-run provisioning.** Six provider stores default `AutoCreateSchema` false, the
  shared store defaults true. Microsoft precedent argues the six are right. Product call.
- **`w6be00` substrate** (routed to architect): two parallel options types per transport;
  the hand-copy is a symptom. Spans six packages and their public builders.

---

## Deliberate decisions a successor should not silently reverse

- **Consumer-visible regression accepted.** Three contracts (`IAuditStore`,
  `IDeadLetterQueue`, `IDataInventoryStore`) have no provider that can satisfy the new
  gate, so affected consumers now get a **named startup failure** instead of a silent
  cross-tenant leak. Correct direction, pre-freeze, documented as a current limitation
  with workarounds. Closing the window is `m9hmp6` / `o98puy`.
- **Two capability markers stay separate.** Collapsing them re-creates a proven defect
  where one marker invites a decorator that reads the tenant as absent at drain time and
  stalls delivery for *every* tenant while passing any safety-only test.
- **Consistency must not become uniformity.** Three beads this session prescribed changes
  that would have *broken* documented guarantees. A tenant term on a statement already
  addressed by its key can only turn the correct row into zero rows.

---

## Traps that cost real time, each measured

- **Working tree is not HEAD.** `git grep -- src/**` reads the working tree. With ~60
  files uncommitted this produced a false claim in the ADR *and* a false green reported
  to the operator, within one hour. Verify in a clean worktree at HEAD; state which tree.
- **A build that failed silently invalidates the test run after it.** A non-compiling
  mutant produced a 10/10 GREEN against the *previous* binary. Confirm the build was
  clean before citing any result.
- **Never `git rm --cached`.** It writes the shared index; a lane's deletion was swept
  into an unrelated commit. Reservations coordinate the working tree, nothing coordinates
  the index. Use plain `rm` and let the integrator stage.
- **Never pipe an existence question through `head`.** Directory order means you read your
  own truncation as an absence. Bit two people here, both while on guard against it.
- **Bead state, not heartbeat, decides whether a lock is stale.** A lapsed TTL means a
  timer expired, not that work finished. Check whether the work is delivered.
- **Safety and liveness arms catch different failures.** Liveness catches an inert
  component; **safety catches a lying fixture.** One lock was vacuous twice and both times
  a *safety* arm exposed it.
