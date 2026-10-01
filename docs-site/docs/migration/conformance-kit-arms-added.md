---
title: Conformance kits gained arms, and your suite must wire them
sidebar_label: Conformance kits gained arms
description: Two shipped conformance kits gained new arms. If your suite wires the completeness guard it now fails; if it does not, the new arms silently never run. Both cases need the same one-line fix.
---

# Conformance kits gained arms, and your suite must wire them

Two of the shipped conformance kits gained arms in this release:

| Kit | Arms added |
| --- | --- |
| `SagaStoreConformanceTestKit` | 1 |
| `PositionedProjectionStoreConformanceTestKit` | 3 |
| `KeyManagementProviderConformanceTestKit` | 5 |
| `ErasureStoreConformanceTestKit` | 3 |

**This does not look like an API change and it is one.** No signature you call has changed, nothing you
wrote stopped compiling, and the packages you reference are the same ones. What changed is the set of
checks a kit expects your suite to expose — so the effect lands in *your* test project, on the next run
after the upgrade.

If you do not derive from either kit, nothing here applies to you.

## Which of the two populations you are in

Every kit inherits `ConformanceTestKit.ConformanceSuite_ShouldWireEveryArm()`. It reflects over the arms
the kit declares and over the members *your* suite declares, and fails when an arm has no member exposing
it. Whether you wired that guard decides what you see now.

### If you wired the completeness guard, your suite fails — and that is the good case

The guard throws, naming the count and every missing member:

```text
MySagaStoreTests does not expose 1 of the 19 arms in this kit to its test runner, so they never
execute and cannot fail. Declare a member per arm — a wrapper that calls it, or an override — and
attribute it for your runner; mark it skipped there if you have a known gap, so the gap stays
visible. Unwired: ProcessedEventIds_SurviveTheRoundTrip_SoAReplayIsStillRecognised
```

The arm total in that message is the kit's, so the number you see depends on which kit you derive from
and is not quoted here as a fixed value. Add the members named under *Unwired* and the guard passes.

### If you did not wire it, nothing fails — and that is the case to worry about

An arm your suite does not expose is not run by your test runner. It does not fail, it does not error,
and it does not appear as a skip. **Your suite reports green over a check that never executed.** That is
the same shape as the defect these arms were added to catch, one level up: the arm cannot fail because it
was never reached.

So if you derive from a conformance kit and your suite passed this upgrade without changing, you have not
avoided the work — you have not been told about it. Wire the guard:

```csharp
[Fact]
public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
```

Then act on what it reports. We recommend this on every derived suite, permanently: it is what makes a
future kit addition visible to you at upgrade time rather than silently absent.

## What to add

The guard matches a member whose name is **exactly** the arm's name, or the arm's name followed by an
underscore and a suffix. A name that merely *contains* the arm's name does not satisfy it — one arm's
wrapper must not be able to satisfy a shorter arm whose name is a prefix of it, because the shorter arm
would then never run while the guard reported green.

So `Refuse_a_row_that_carries_no_position` is satisfied by a member called
`Refuse_a_row_that_carries_no_position` or `Refuse_a_row_that_carries_no_position_Test`, and not by
`Verify_Refuse_a_row_that_carries_no_position`.

### `SagaStoreConformanceTestKit`

```csharp
[Fact]
public Task ProcessedEventIds_SurviveTheRoundTrip_SoAReplayIsStillRecognised_Test() =>
    ProcessedEventIds_SurviveTheRoundTrip_SoAReplayIsStillRecognised();
```

This arm saves a saga and loads it back through your real store, and requires that the set of processed
event ids survives the round trip. It cannot be established by reading your store's code or by
exercising an object that was never serialized: a serializer that does not populate a get-only
collection on deserialize returns an **empty set without failing**, and a store in that state
recognises no replay at all — every redelivery runs the step again.

### `PositionedProjectionStoreConformanceTestKit`

```csharp
[Fact]
public Task Refuse_a_row_that_carries_no_position_Test() =>
    Refuse_a_row_that_carries_no_position();

[Fact]
public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
    Rebuild_repairs_a_row_that_carries_no_position();

[Fact]
public Task Report_requires_rebuild_for_a_refold_against_an_unnumbered_row_Test() =>
    Report_requires_rebuild_for_a_refold_against_an_unnumbered_row();
```

- **`Refuse_a_row_that_carries_no_position`** — a row carrying no position number must be **refused**,
  never adopted. A caller that read no position knows nothing about which prefix the stored state
  already covers, so folding onto it and stamping this batch's position produces a row that reads as
  authoritative and is not.
- **`Rebuild_repairs_a_row_that_carries_no_position`** — the refusal must have an exit.
  `RebuildAtPositionAsync` writes state and position together with no expectation to satisfy, for a
  caller that folded the whole stream from an empty seed and can therefore number the row. **This arm is
  what makes the refusal above legitimate:** without it, a store that refuses *every* write satisfies the
  safety arm, which is the cheapest way never to double-apply and the most expensive way to be wrong.
- **`Report_requires_rebuild_for_a_refold_against_an_unnumbered_row`** — a re-fold against a row holding a
  complete fold with no number must report `ProjectionRefoldOutcome.RequiresRebuild`, which is terminal,
  rather than a superseded result. Superseded tells the caller to re-read and retry, and nothing about a
  numberless row changes on its own, so the caller loops forever.

### `KeyManagementProviderConformanceTestKit`

```csharp
[Fact]
public Task CreateKeyIfAbsentAsync_WhenAbsent_ShouldCreateOneActiveVersion_Test() =>
    CreateKeyIfAbsentAsync_WhenAbsent_ShouldCreateOneActiveVersion();

[Fact]
public Task CreateKeyIfAbsentAsync_WhenPresent_ShouldNotRotateOrDemote_Test() =>
    CreateKeyIfAbsentAsync_WhenPresent_ShouldNotRotateOrDemote();

[Fact]
public Task CreateKeyIfAbsentAsync_ConcurrentFirstWrites_ShouldNotDemoteEachOther_Test() =>
    CreateKeyIfAbsentAsync_ConcurrentFirstWrites_ShouldNotDemoteEachOther();

[Fact]
public Task IsKeyDestroyedAsync_AfterZeroRetentionDelete_ShouldAgreeWithTheReportedOutcome_Test() =>
    IsKeyDestroyedAsync_AfterZeroRetentionDelete_ShouldAgreeWithTheReportedOutcome();

[Fact]
public Task IsKeyDestroyedAsync_ForALiveKey_ShouldReportNotDestroyed_Test() =>
    IsKeyDestroyedAsync_ForALiveKey_ShouldReportNotDestroyed();
```

The first three hold your provider to `CreateKeyIfAbsentAsync`, which is new on
`IKeyManagementProvider` — see
[Provisioning a key is no longer a rotation](create-key-if-absent.md) for the member itself.

- **`CreateKeyIfAbsentAsync_WhenAbsent_ShouldCreateOneActiveVersion`** — provisioning an absent key
  produces one version, usable for encryption, and the returned metadata describes a key your provider
  really holds. This is the liveness arm: without it, a provider that creates nothing satisfies the two
  safety arms below.
- **`CreateKeyIfAbsentAsync_WhenPresent_ShouldNotRotateOrDemote`** — provisioning a key that already
  exists must leave the version that was there in place and still `Active`. It asserts no version *count*,
  because a backend whose only create operation also adds a version cannot promise one; it asserts that
  nothing already there was retired, which every backend can.
- **`CreateKeyIfAbsentAsync_ConcurrentFirstWrites_ShouldNotDemoteEachOther`** — four callers provisioning
  the same absent key concurrently must all receive the same handle, a lost race must be a no-op rather
  than an error, and no version may be left fenced out of encryption.

The last two hold your provider to `IKeyDestructionStatusProvider.IsKeyDestroyedAsync` — **the capability
that gates whether an erasure can be certified at all** — at the retention the erasure actually uses:

- **`IsKeyDestroyedAsync_AfterZeroRetentionDelete_ShouldAgreeWithTheReportedOutcome`** — the erasure calls
  `DeleteKeyAsync(keyId, 0, ct)`, and providers *branch* on a retention of zero. The arm requires the two
  answers to agree: a destruction reported `Completed` must report the material as destroyed, and one
  reported `ScheduledIrreversible` must not — while a key is scheduled it is still recoverable, and
  certifying that would tell a data subject their data is unreadable when it can still be recovered. It
  does **not** require a fixed `true`, because a backend with a mandatory minimum deletion window clamps a
  zero retention up to that minimum and is correct to answer `false`.
- **`IsKeyDestroyedAsync_ForALiveKey_ShouldReportNotDestroyed`** — a live, never-deleted key must report as
  not destroyed. This is what stops a provider answering `true` to everything, which would certify every
  erasure it was ever asked about, including ones that destroyed nothing.

### `ErasureStoreConformanceTestKit`

```csharp
[Fact]
public Task RecordKeyDestroyedAsync_ShouldAppendIdempotentlyAndSurvive_Test() =>
    RecordKeyDestroyedAsync_ShouldAppendIdempotentlyAndSurvive();

[Fact]
public Task RecordKeyDestroyedAsync_NonExistentRequest_ShouldThrowKeyNotFoundException_Test() =>
    RecordKeyDestroyedAsync_NonExistentRequest_ShouldThrowKeyNotFoundException();

[Fact]
public Task RecordKeyDestroyedAsync_ShouldTreatHandlesDifferingOnlyInCaseAsDistinct_Test() =>
    RecordKeyDestroyedAsync_ShouldTreatHandlesDifferingOnlyInCaseAsDistinct();
```

These hold your store to `IErasureStore.RecordKeyDestroyedAsync`, which is new — see
[Erasure destroyed-key record](erasure-destroyed-key-record.md) for the member and the table it needs.

- **`..._ShouldAppendIdempotentlyAndSurvive`** — recorded handles are readable back, a later record adds to
  what earlier ones wrote rather than replacing it, and recording a handle twice leaves one entry. A store
  that truncated the set would erase the evidence of every earlier pass, which is the defect this record
  exists to prevent arriving by another route.
- **`..._NonExistentRequest_ShouldThrowKeyNotFoundException`** — recording against a request that does not
  exist throws. This is also what stops the arm above being satisfied by a store that accepts every write
  and stores nothing.
- **`..._ShouldTreatHandlesDifferingOnlyInCaseAsDistinct`** — two handles differing only in case are two
  handles. On a SQL store this is decided by the column's **collation**, not by framework code, so a store
  that provisioned the column with a case-insensitive collation folds two subjects' handles into one and
  attests coverage for a key that was never destroyed.

:::warning These two arms fail rather than skip when the capability is absent
If your provider does not supply `IKeyDestructionStatusProvider`, the arms fail with an explanation
instead of passing quietly. That is deliberate: a provider without the capability never has its erasures
certified, and it is better to see that at upgrade time than to discover it when an erasure cannot be
completed. If it is intended for your provider, mark these two arms skipped with that reason.
:::

:::note These three arms are annotated for trimming and AOT
Each carries `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, because projection serialization is
reflective and the kit does not choose your store's serializer. If your test project is analyzed for trim
or AOT warnings, propagate the annotation onto your wrapper — or suppress it there — rather than removing
it from the call.
:::

## If you have a known gap, mark it skipped rather than leaving it unwired

The two states are not equivalent. An unwired arm is invisible; a skipped one is reported. Declare the
member and skip it in your runner, so the gap stays in your test output where you will see it again:

```csharp
[Fact(Skip = "Our store cannot yet number a rebuilt row; tracked internally.")]
public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
    Rebuild_repairs_a_row_that_carries_no_position();
```

## Related

- [Consumer conformance toolkit](../testing/conformance-toolkit.md) — how a kit is derived from and what a green run covers
- [Projections](../event-sourcing/projections.md) — the positioned-projection contract these three arms hold a store to
- [Sagas](../sagas/index.md) — replay identity and what the saga store must persist
- [Resolved issues](../resolved-issues.md) — the defects these arms were added to detect
