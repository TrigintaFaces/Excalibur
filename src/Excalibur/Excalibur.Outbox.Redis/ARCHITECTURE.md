# Architecture — Excalibur.Outbox.Redis

> **Guarantee contract for the Redis outbox provider.** This document states what this provider adds to
> and diverges from the shared outbox contract in `Excalibur.Outbox`'s own `ARCHITECTURE.md` — read that
> document first for the delivery guarantee every provider shares (at-least-once, the claim/fail/mark-sent
> protocol, consumer obligations). This file covers what is specific to Redis: the atomic-claim mechanism,
> key layout and retention, tenancy, and this provider's own known gaps.

## Delivery guarantee

**At-least-once**, same as every provider (see the shared contract). The claim is **single-writer**: this
provider does not implement `IFencedOutboxStore`, so it carries no leadership fence. A deployment running
more than one active dispatcher against the same Redis instance without an external leader election can
observe split delivery. Run exactly one active dispatcher, or put leader election in front of it.

## How it is achieved

- **Stage.** `StageMessageAsync` writes the message as a Redis hash and indexes it into a staged sorted
  set.
- **Claim.** A single Lua script performs the atomic read-decide-write: it reclaims any lease past its
  expiry back to the staged index, then atomically moves up to `batchSize` staged entries into a leased
  index with a lease-expiry score, so two concurrent claimers can never receive overlapping batches. One
  script, one round trip — there is no read-then-write window a second claimer could observe.
- **Fail / retry.** `MarkFailedAsync` frees the lease and sets the next-attempt floor in one atomic write,
  matching the shared contract's requirement that this never split into two writes.
- **Mark-sent + retention.** `MarkSentAsync` applies the retention TTL (`RedisOutboxOptions
  .SentMessageTtlSeconds`, default 604800 seconds / 7 days) inside the **same** atomic Lua script that
  transitions the message to sent — there is no separate `EXPIRE` call, so a crash between the status
  write and the TTL write cannot leave a sent message as an immortal key.

## Tenant scoping

This provider is **tenant-partitioned**, not tenant-scoped: it persists the tenant on the hash it writes
at stage time and hands that value back to the caller on drain, rather than reading an ambient tenant on
any operation. That is the correct shape for an outbox — the drain claims across every tenant by
construction (one dispatcher, every tenant's messages), and a store that instead tried to filter its claim
by an ambient tenant would return nothing for every tenanted message and stall delivery entirely. Both
registration branches (a supplied `ConnectionMultiplexer`, and the DI-constructed path) register through
the same tenant-partitioned registration, so a host wiring either shape gets the same attestation —
attesting only one branch would leave the other silently rejected by row-discriminator multi-tenancy
(`OutboxBuilderRedisExtensions.cs:123` and `:136`).

**Untenanted representation — converged with the relational providers.** An untenanted message stores the
reserved, non-null sentinel here exactly as it does in a relational provider's `NOT NULL DEFAULT` column,
so the same message compares equal on the tenant term whichever store is underneath. The write emits the
field unconditionally, folded through the single total conversion (`RedisOutboxStore.cs:988`), and the read
folds a missing field the same way (`:1087`) — so a key written under the older, field-omitted shape reads
back identically rather than as a null. No historical-data migration is required: keys written under the
old shape age out within one `SentMessageTtlSeconds` retention window, and the read-tolerance covers them
until they do.

## Evidence (conformance)

Derives the shared `OutboxStoreConformanceTestKit`
(`tests/integration/Excalibur.Integration.Tests/Redis/Outbox/RedisOutboxStoreConformanceShould.cs`) against
a real Redis container, never skip-gated. The suite carries no skipped arms; per the shared contract's
provider-maturity table, this provider's full at-least-once suite is verified green.

## Consumer obligations

Same as the shared contract (idempotent handlers; `F` greater than the maximum expected delivery
duration). Additionally: **do not run more than one active dispatcher** against a single Redis instance
without external leader election — this provider has no fencing of its own to fall back on.

## Limitations

- **Single-writer**, no leadership fence (see above).
- Retention is TTL-driven; a consumer that needs sent messages retained longer than
  `SentMessageTtlSeconds` should not rely on this provider as an audit trail.
