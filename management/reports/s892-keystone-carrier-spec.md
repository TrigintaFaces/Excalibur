# S892 Keystone Carrier Build-Spec — `owxhc8` + `su6232` (one commit)

> **Author:** SoftwareArchitect · **Purpose:** a self-contained, unambiguous build-spec for the S892
> keystone, so a **fresh backend-developer carrier** (clause-4) or a revived BackendDeveloper can land it
> directly without reconstructing the rulings from the OPCOM thread. Consolidates SA rulings 33426 / 33448 /
> guidance §1 + PdM's finalized AC-K1.1. **PM integrates + commits (branch state is PM's).**
> Baseline: current committed HEAD. **Land `owxhc8` + `su6232` as ONE commit.**

## What this keystone is
Route the **SqlServer** outbox stage/insert path through the canonical `OutboxMessage.FromOutboundMessage`
mapping seam (owxhc8), and add per-partition `SequenceNumber` ordering fidelity to Postgres+Oracle (su6232).
They share the canonical shape + the sequence column → **one owner, one commit.**

## Ruling recap (do NOT re-open these — settled)
- **Decision-1 = (A):** `FromOutboundMessage` stays **creation-only**. `Status`/`RetryCount` are **NOT** added
  to the canonical `OutboxMessage`; the SqlServer INSERT supplies them as **fresh-stage constants**
  (`Status = OutboxStatus.Staged`, `RetryCount = 0`). Rationale: `FromOutboundMessage` takes an
  `OutboundMessage` (no lifecycle fields) → it is *structurally* creation-only; the invalid
  "staged-but-already-Delivered" state stays inexpressible.
- **Decision-2 = fresh-stage K1.1:** the byte-identical parity gate asserts a **fresh-stage** `OutboundMessage`
  round-trips identically; an "arbitrary Status/RetryCount" arm is vacuous (that path can't occur).

## Build steps (in order)

### 1. Reconcile the canonical seam to the correct superset — do this FIRST
`OutboxMessage.FromOutboundMessage` (`src/Excalibur/Excalibur.Outbox/Outbox/OutboxMessage.cs:105`) currently
**re-stamps `CreatedAt = DateTimeOffset.UtcNow`** — a bug (PG/Oracle already route through it, so they
already overwrite the caller's timestamp = a live Liskov divergence vs SqlServer).
- **Change it to preserve `message.CreatedAt`** (stop the `UtcNow` re-stamp).
- **⚠ GUARD (mandatory):** this is correct ONLY if `OutboundMessage.CreatedAt` is guaranteed populated at
  construction. **Verify `OutboundMessage` stamps `CreatedAt` at construction** (via `TimeProvider`, per the
  `3e82d2` discipline — stamp-once-at-construction). If any stage path can leave it `default(DateTimeOffset)`,
  **fix THAT (stamp at construction)** — do NOT keep re-stamping at map time as a workaround. A defaulted
  `CreatedAt` persisted verbatim = `0001-01-01` garbage, strictly worse than `NOW()`.
- Confirm `OutboxMessage` ctor defaults `Status = Staged`, `RetryCount = 0` so the seam's omission of them ==
  the SqlServer fresh-stage constants. Confirm the header serializer is `EventSerializationDefaults.Canonical`
  on both paths.

### 2. Route SqlServer through the reconciled seam (`owxhc8`)
`SqlServerOutboxStore.InsertMessageAsync` (`~:1787`) → `InsertOutboxMessageRequest`
(`src/Excalibur/Excalibur.Outbox.SqlServer/Requests/InsertOutboxMessageRequest.cs:43-76`) currently inline-maps
18 fields off `message`. Route the stage path through `OutboxMessage.FromOutboundMessage(outboundMessage)` →
insert from the resulting `OutboxMessage`.
- SqlServer INSERT sets `@Status = (int)OutboxStatus.Staged`, `@RetryCount = 0` — reference the **named
  staging constant**, not a bare literal (matches PG/Oracle's hardcoded `attempts = 0`).
- `InsertOutboxMessageRequest` is a **public** type — if the routing narrows its field-set, that's a
  `PublicAPI.Unshipped.txt` delta to review (don't widen; consider whether it should be `internal`, but that's
  a separate call — don't do it here).

### 3. `su6232` — PG/Oracle persist `sequence_number` + drain ordering
- Postgres (`Excalibur.Outbox.Postgres/Requests/InsertOutboxMessage.cs:62-64`) and Oracle (same path) currently
  **omit `sequence_number`** and hardcode `NOW()`/`SYSTIMESTAMP` for created-at. Add: persist `sequence_number`
  (new column + ctor param + `@SequenceNumber`) **and** preserve caller `CreatedAt` (`@CreatedAt` instead of
  `NOW()`/`SYSTIMESTAMP` — the align-UP from step 1's blast radius).
- Drain: `ORDER BY (PartitionKey, SequenceNumber)` on PG/Oracle (mirror SqlServer `SqlServerOutboxStore.cs`
  drain ordering).
- **Scope:** per-partition strictly-increasing + at-least-once. **NOT** global total order. Down-aligning any
  provider (removing the guarantee) is **prohibited**.

### 4. Conformance kit ASSERTs ordering
The cross-provider conformance kit currently **excludes** ordering (`~:278-280`, "store-managed"). Change it to
**ASSERT** per-partition ordering across **all** providers. Safety arm: no cross-partition reorder claimed as
total order. Liveness arm: every provider returns the staged rows in per-partition order.

### 5. F-5 shipped-DDL sweep (su6232 adds a column)
`sequence_number` is a new column → sweep `tests/**` fixture DDL **and** `docs-site/**` + `samples/**` shipped
DDL for the outbox `CREATE TABLE` / INSERT / SELECT; update every one that must carry the column. (The
`34k958`/`gec369` shipped-DDL-drift class.)

## The proving gate — AC-K1.1 (Tests owns the lock; it must be RED-first)
Byte-identical parity for a **fresh-stage** `OutboundMessage`: mapped through SqlServer's OLD inline map vs the
NEW seam-routed map → **all persisted column values byte-identical** (Id, MessageType, Payload, Headers,
Destination, **CreatedAt (preserved)**, ScheduledAt, Correlation/Causation, TenantId, Priority,
TargetTransports, IsMultiTransport, PartitionKey, GroupKey, SequenceNumber) **and** `Status = Staged`,
`RetryCount = 0`.
- **RED-first:** fails today on the `CreatedAt = UtcNow` re-stamp → GREEN after step 1.
- **Non-vacuity arm:** a fresh `OutboundMessage` carries a sane `CreatedAt` that survives the round-trip
  identically across all three providers (guards both the re-stamp bug AND a default-zero regression).

## Files in scope (reserve before edit)
`Excalibur.Outbox/Outbox/OutboxMessage.cs`, `Excalibur.Outbox.SqlServer/**` (store + InsertOutboxMessageRequest),
`Excalibur.Outbox.Postgres/**` + `Excalibur.Outbox.Oracle/**` (InsertOutboxMessage + drain), the conformance
kit, and any `tests/**`/`docs-site/**`/`samples/**` outbox DDL touched by the new column.

## Do NOT (out of scope for the keystone)
- Do NOT add `Status`/`RetryCount` to canonical `OutboxMessage` (Decision-1 = A).
- Do NOT force MongoDb through the relational canonical seam (different document shape, out of scope).
- Do NOT touch `j1wfzu` (MarkFailed lease-clear) / `vmy75v` (fencing) / `nu13kj` (ctor consolidation) here —
  they share the SqlServer outbox surface and are **sequenced after** the keystone (single-owner, reserve-first).
