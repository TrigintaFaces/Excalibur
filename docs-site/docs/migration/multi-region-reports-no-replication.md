---
title: Multi-region key replication now reports that it never happened
sidebar_label: Multi-region replication honesty
description: The multi-region key provider recorded successful synchronisations it never performed. It now reports no sync and a non-empty backlog, and forwards the capabilities of the provider it wraps.
---

# Multi-region key replication now reports that it never happened

**If you use `MultiRegionKeyProvider` and monitor its replication status, expect it to change from healthy
to never-synchronised after this upgrade. Nothing got worse. The previous value was fabricated.**

## What it used to report, and why that was wrong

`ReplicateKeysAsync` recorded a successful-synchronisation instant, zeroed the pending-key count and logged
replication complete — **every time it was called, whether or not any key material was copied.**

No key material was ever copied. The routine it delegates to has three branches and none of them transfers
anything: a cloud provider is expected to replicate through its own geo-replication, configured outside this
framework and unobservable to it; the in-memory provider can neither export nor import key material; an
unrecognised provider has no known sync path. That routine deliberately recorded nothing, for exactly this
reason — and its caller recorded a success for it three statements later.

The consequence was not a wrong number in isolation. The recovery-point check computes its lag from that
instant, so it reported the recovery-point target **met**. A consumer configuring disaster recovery saw a met
objective and an empty backlog over a passive region holding **no key material**, and would have discovered
the truth at a failover — when the keys are needed and not there.

## What it reports now

- **`ReplicationStatus.LastSuccessfulSync` stays `null`** until a replication actually transfers key
  material. No branch does today, so on current provider combinations it stays null.
- **`ReplicationStatus.PendingKeys` keeps its backlog** rather than being zeroed by a replication that moved
  nothing.
- **The recovery-point check returns early** on a null instant instead of computing lag from a fabricated one
  and reporting a target met.

**If an alert fires after this upgrade, it is telling you something that was already true.** Key material is
not being copied between your regions by this framework. Depending on your providers, that may be entirely
expected — Azure Key Vault and AWS KMS replicate through their own geo-replication (AWS multi-region keys,
Key Vault's own regional model), configured on the provider side, and this framework cannot observe whether
it is enabled for a given key. **It now claims nothing about it rather than claiming success.** Verify your
geo-replication with your provider's own tooling; do not read this framework's replication status as evidence
of it.

## Capabilities of the wrapped provider are now reachable

`MultiRegionKeyProvider` never forwarded `GetService`, so every optional capability it does not implement
itself resolved to `null` through it. The one that mattered is **`IDurableKeyProvider`**: the decorator does
not implement it and the cloud providers do, so a multi-region deployment backed by a real key vault reported
as having **no durable key store** — which the durability gate reads as a volatile one, for keys that are in
fact durable.

It now answers for the capabilities it implements itself, and defers everything else to the region currently
serving reads and writes. Its own implementations win where it has one, because the decorator's behaviour
spans both regions and the wrapped provider's describes only one of them.

**What this may change for you:** a host that was silently running without a capability — most likely the
durability gate treating a durable store as volatile — will now see that capability present. If you added a
configuration workaround for that, it is no longer needed.

## Related

- [Version Upgrades](version-upgrades.md) — the versioning policy and what each release stage promises
