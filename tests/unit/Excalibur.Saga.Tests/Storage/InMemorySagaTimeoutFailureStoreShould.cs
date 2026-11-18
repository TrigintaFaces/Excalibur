// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Storage;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Regression")]
public sealed class InMemorySagaTimeoutFailureStoreShould
{
    private static readonly string[] ExpectedFirstPageIds = ["a", "b"];
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1);

    [Fact]
    public async Task ExposeActualSelectedFacetWithoutClaimingProcessDurability()
    {
        var store = NewStore();
        var facet = ((IServiceProvider)(ISagaTimeoutStore)store).GetService(typeof(ISagaTimeoutFailureStore));
        facet.ShouldBeSameAs(store);
        Failure(store).HoldsSurviveProcessRestart.ShouldBeFalse();
        await Failure(store).ValidateFailureStorageAsync(None);
    }

    [Theory]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Unknown)]
    public async Task RetainOriginalPayloadButExcludeHeldWorkFromNormalClaims(MessageFailureKind kind)
    {
        var store = NewStore();
        var bytes = new byte[] { 1, 2, 3 };
        await store.ScheduleTimeoutAsync(Timeout("held", bytes), None);
        bytes[0] = 99;
        var claim = await Claim(store, Now);
        var observation = new SagaTimeoutFailureObservation(claim, kind, "failure");
        claim.Timeout.TimeoutData![0] = 88;
        // The caller-mutated claim is not the actual captured source anymore.
        (await Failure(store).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        claim = observation.OriginalClaim;
        var heldAttempt = await Failure(store).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None);
        heldAttempt.Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
        var held = heldAttempt.Receipt.ShouldNotBeNull();
        held.State.ShouldBe(SagaTimeoutHoldState.Held);
        held.Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        (await store.ClaimDueTimeoutsAsync(Now.AddHours(1), 10, None)).ShouldBeEmpty();
        var diagnostic = (await store.GetDueTimeoutsAsync(Now, None)).Single();
        diagnostic.TimeoutId.ShouldBe("held");
        diagnostic.TimeoutData![1] = 99;
        var page = await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary", 10, null, None);
        page.Items.Single().Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        (await store.MarkDeliveredAsync(claim, None)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        (await store.GetDueTimeoutsAsync(Now, None)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReconcileSameHoldOperationAndRefuseItsConflictingInputs()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("original"), None);
        var claim = await Claim(store, Now);
        var observation = Observe(claim);
        var operation = Guid.NewGuid();
        var first = (await Failure(store).TryHoldFailureAsync(observation, claim, operation, None)).Receipt.ShouldNotBeNull();
        var replay = (await Failure(store).TryHoldFailureAsync(observation, claim, operation, None)).Receipt.ShouldNotBeNull();
        replay.Token.Value.ShouldBe(first.Token.Value);
        replay.State.ShouldBe(SagaTimeoutHoldState.Held);
        (await Failure(store).TryHoldFailureAsync(new(claim, MessageFailureKind.Unknown, "different"), claim, operation, None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var resumed = (await Failure(store).TryResumeHeldFailureAsync(first.Token, Guid.NewGuid(), None))!;
        resumed.State.ShouldBe(SagaTimeoutHoldState.Resumed);
        (await Failure(store).TryHoldFailureAsync(observation, claim, operation, None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
    }

    [Fact]
    public async Task RequireCurrentClaimForRebindAndPreserveBothEpochsUntilResume()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("reclaim"), None);
        var first = await Claim(store, Now);
        var observation = Observe(first);
        var current = await Claim(store, Now.AddMinutes(3));
        current.ClaimToken.ShouldNotBe(first.ClaimToken);
        current.SourceEpoch.ShouldBe(first.SourceEpoch);
        current.DeliveryEpoch.ShouldBe(first.DeliveryEpoch);
        (await Failure(store).TryHoldFailureAsync(observation, first, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
        var held = (await Failure(store).TryHoldFailureAsync(observation, current, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        held.State.ShouldBe(SagaTimeoutHoldState.Held);
    }

    [Fact]
    public async Task FenceOldFailureAfterResumeAndLatestReceiptEviction()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("cycle"), None);
        var first = await Claim(store, Now);
        var old = Observe(first);
        var oldOperation = Guid.NewGuid();
        var held = (await Failure(store).TryHoldFailureAsync(old, first, oldOperation, None)).Receipt.ShouldNotBeNull();
        var resume = Guid.NewGuid();
        await Failure(store).TryResumeHeldFailureAsync(held.Token, resume, None);
        var second = await Claim(store, Now);
        second.SourceEpoch.ShouldBe(first.SourceEpoch);
        second.DeliveryEpoch.ShouldNotBe(first.DeliveryEpoch);
        second.Timeout.TimeoutData.ShouldBe(first.Timeout.TimeoutData);
        await Failure(store).TryResumeHeldFailureAsync(held.Token, resume, None);
        // The replay must not rotate the cycle again or steal the current claim.
        var secondHeld = (await Failure(store).TryHoldFailureAsync(Observe(second), second, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        await Failure(store).TryResumeHeldFailureAsync(secondHeld.Token, Guid.NewGuid(), None);
        var third = await Claim(store, Now);
        third.DeliveryEpoch.ShouldNotBe(second.DeliveryEpoch);
        (await Failure(store).TryHoldFailureAsync(old, first, oldOperation, None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await Failure(store).TryHoldFailureAsync(old, third, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, resume, None)).ShouldBeNull();
        (await Failure(store).TryHoldFailureAsync(Observe(third), third, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
    }

    [Fact]
    public async Task RefuseOldSourceAfterIdenticalUnownedCancelAndRecreation()
    {
        var store = NewStore();
        var timeout = Timeout("same-id");
        await store.ScheduleTimeoutAsync(timeout, None);
        var first = await Claim(store, Now);
        var held = (await Failure(store).TryHoldFailureAsync(Observe(first), first, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        await store.CancelTimeoutAsync("ordinary", "same-id", None);
        await store.ScheduleTimeoutAsync(timeout, None);
        var replacement = await Claim(store, Now);
        replacement.SourceEpoch.ShouldNotBe(first.SourceEpoch);
        replacement.Timeout.TimeoutData.ShouldBe(first.Timeout.TimeoutData);
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None)).ShouldBeNull();
        (await Failure(store).TryHoldFailureAsync(Observe(first), replacement, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await store.MarkDeliveredAsync(first, None)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        (await store.MarkDeliveredAsync(replacement, None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Fact]
    public async Task RefuseForeignPartitionAndForgedSagaWithoutMutatingHeldWork()
    {
        var tenant = new MutableTenant { TenantId = "Tenant" };
        var store = new InMemorySagaTimeoutStore(tenant);
        await store.ScheduleTimeoutAsync(Timeout("opaque-id "), None);
        var claim = await Claim(store, Now);
        var observation = Observe(claim);
        var forged = claim with { Timeout = claim.Timeout with { SagaId = "another-saga" } };
        (await Failure(store).TryHoldFailureAsync(observation, forged, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        tenant.TenantId = "Tenant ";
        (await Failure(store).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        tenant.TenantId = "Tenant";
        var held = (await Failure(store).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        tenant.TenantId = "tenant";
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None)).ShouldBeNull();
        tenant.TenantId = "Tenant";
        (await Failure(store).GetHeldFailuresAsync("Tenant", null, 10, null, None)).Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task KeepHealthyWorkLiveAndCancellationEffectiveInEitherResumeOrder()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("a-held"), None);
        var claim = await Claim(store, Now);
        var held = (await Failure(store).TryHoldFailureAsync(Observe(claim), claim, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        await store.ScheduleTimeoutAsync(Timeout("b-healthy"), None);
        var healthy = await Claim(store, Now);
        healthy.Timeout.TimeoutId.ShouldBe("b-healthy");
        (await store.MarkDeliveredAsync(healthy, None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None);
        await store.CancelAllTimeoutsAsync("ordinary", None);
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None)).ShouldBeNull();
        (await store.ClaimDueTimeoutsAsync(Now.AddHours(1), 10, None)).ShouldBeEmpty();
        (await store.GetDueTimeoutsAsync(Now, None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PageActualHeldSnapshotsWithPartitionAndTargetBoundCursor()
    {
        var store = NewStore();
        foreach (var id in new[] { "a", "b", "c" })
        {
            await store.ScheduleTimeoutAsync(Timeout(id), None);
            var claim = await Claim(store, Now);
            await Failure(store).TryHoldFailureAsync(Observe(claim), claim, Guid.NewGuid(), None);
        }
        var first = await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary", 2, null, None);
        first.Items.Select(x => x.Timeout.TimeoutId).ShouldBe(ExpectedFirstPageIds);
        first.NextCursor.ShouldNotBeNull();
        var last = await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary", 2, first.NextCursor, None);
        last.Items.Single().Timeout.TimeoutId.ShouldBe("c");
        last.NextCursor.ShouldBeNull();
        await Should.ThrowAsync<ArgumentException>(async () => await Failure(NewStore()).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary", 2, first.NextCursor, None));
        await Should.ThrowAsync<ArgumentException>(async () => await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 2, first.NextCursor, None));
    }

    [Fact]
    public async Task LeaveOriginalClaimAndPayloadUntouchedOnPreCancelledHold()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("cancel"), None);
        var claim = await Claim(store, Now);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await Failure(store).TryHoldFailureAsync(Observe(claim), claim, Guid.NewGuid(), cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        (await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 10, null, None)).Items.ShouldBeEmpty();
        (await store.MarkDeliveredAsync(claim, None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Fact]
    public async Task PreserveOwnedJournalIdentityAndRefuseResumeAfterReceiverCancellation()
    {
        var store = NewStore();
        var domain = new SagaTimerRecoveryDomain(TenantDefaults.DefaultTenantId, Guid.NewGuid());
        var fence = await store.ObserveFenceAsync(domain, None);
        var timeout = Timeout("owned") with { SagaId = domain.SagaId.ToString() };
        SagaTimerIntent[] intents = [new(SagaTimerAction.Schedule, timeout.TimeoutId, timeout)];
        var journal = new SagaTimerRecoveryJournal(new(domain.TenantId, "OriginalState", domain.SagaId),
            Guid.NewGuid(), 1, Guid.NewGuid(), fence.Token, fence.MutationRevision, 1, SagaTimerRecoveryCodec.Encode(intents));
        await store.ApplyAsync(journal, None);
        var claim = await Claim(store, Now);
        claim.OwnedDelivery.ShouldNotBeNull();
        var stripped = new ClaimedSagaTimeout(claim.Timeout, claim.ClaimToken)
        { SourceEpoch = claim.SourceEpoch, DeliveryEpoch = claim.DeliveryEpoch };
        (await Failure(store).TryHoldFailureAsync(Observe(claim), stripped, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var held = (await Failure(store).TryHoldFailureAsync(Observe(claim), claim, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        (await store.ApplyAsync(journal, None)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        (await store.ClaimDueTimeoutsAsync(Now.AddHours(1), 10, None)).ShouldBeEmpty();
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), timeout.TimeoutId, None);
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None)).ShouldBeNull();
        (await store.IsTimeoutDeliveryCurrentAsync(claim.OwnedDelivery!, None)).ShouldBeFalse();
        (await store.GetDueTimeoutsAsync(Now, None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeduplicateConcurrentResumeAndPreserveTheFreshClaimCycle()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("concurrent"), None);
        var original = await Claim(store, Now);
        var held = (await Failure(store).TryHoldFailureAsync(Observe(original), original, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        var command = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<SagaTimeoutHoldReceipt?> ResumeAsync()
        {
            await start.Task;
            return await Failure(store).TryResumeHeldFailureAsync(held.Token, command, None);
        }
        var one = ResumeAsync(); var two = ResumeAsync();
        start.SetResult();
        var results = await Task.WhenAll(one, two);
        results.ShouldAllBe(receipt => receipt != null && receipt.State == SagaTimeoutHoldState.Resumed);
        var claim = await Claim(store, Now);
        claim.SourceEpoch.ShouldBe(original.SourceEpoch);
        claim.DeliveryEpoch.ShouldNotBe(original.DeliveryEpoch);
        await Failure(store).TryResumeHeldFailureAsync(held.Token, command, None);
        (await Failure(store).TryHoldFailureAsync(Observe(claim), claim, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
    }

    [Fact]
    public async Task ReleaseFailureMetadataWithPayloadWhileRetainingOnlyActiveHeldSources()
    {
        var store = NewStore();
        for (var index = 0; index < 64; index++)
        {
            await store.ScheduleTimeoutAsync(Timeout($"healthy-{index}"), None);
            store.GetFailureSourceCount().ShouldBe(1);
            var claim = await Claim(store, Now);
            (await store.MarkDeliveredAsync(claim, None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
            store.GetFailureSourceCount().ShouldBe(0);
        }
        await store.ScheduleTimeoutAsync(Timeout("held-lifecycle"), None);
        var original = await Claim(store, Now);
        var held = (await Failure(store).TryHoldFailureAsync(Observe(original), original, Guid.NewGuid(), None)).Receipt.ShouldNotBeNull();
        store.GetFailureSourceCount().ShouldBe(1);
        await store.CancelAllTimeoutsAsync("ordinary", None);
        store.GetFailureSourceCount().ShouldBe(0);
        (await Failure(store).TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), None)).ShouldBeNull();
        await store.ScheduleTimeoutAsync(Timeout("held-lifecycle"), None);
        var replacement = await Claim(store, Now);
        replacement.SourceEpoch.ShouldNotBe(original.SourceEpoch);
        (await Failure(store).TryHoldFailureAsync(Observe(original), replacement, Guid.NewGuid(), None)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        store.Clear();
        store.GetFailureSourceCount().ShouldBe(0);
    }

    [Fact]
    public async Task CertifyCancelledObservationEndedWithoutAnyFutureClaimOrHistoricalEntry()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("cancel-before-hold"), None);
        var claim = await Claim(store, Now);
        var observation = Observe(claim);
        // No hold commits. After operator cancellation the original operation must reconcile without
        // waiting for a nonexistent fresh claim, and without inventing a delivered receipt.
        await store.CancelTimeoutAsync("ordinary", "cancel-before-hold", None);
        store.GetFailureSourceCount().ShouldBe(0);
        var ended = await Failure(store).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None);
        ended.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        ended.Receipt.ShouldBeNull();
        (await store.ClaimDueTimeoutsAsync(Now.AddHours(1), 10, None)).ShouldBeEmpty();
        (await store.MarkDeliveredAsync(claim, None)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        await store.ScheduleTimeoutAsync(Timeout("healthy-after-cancel"), None);
        var healthy = await Claim(store, Now);
        (await store.MarkDeliveredAsync(healthy, None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Fact]
    public async Task RejectForeignTargetOrChangedRawIdentityBeforeCertifyingAbsence()
    {
        var actual = NewStore();
        var foreign = NewStore();
        await actual.ScheduleTimeoutAsync(Timeout("same-address"), None);
        var claim = await Claim(actual, Now);
        var observation = Observe(claim);
        var malformed = claim with { DeliveryEpoch = "malformed" };
        (await Failure(actual).TryHoldFailureAsync(Observe(malformed), malformed, Guid.NewGuid(), None))
            .Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        actual.GetFailureSourceCount().ShouldBe(1);
        (await actual.GetDueTimeoutsAsync(Now, None)).ShouldHaveSingleItem().TimeoutId.ShouldBe("same-address");
        (await Failure(foreign).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None))
            .Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        await actual.CancelTimeoutAsync("ordinary", "same-address", None);
        var altered = claim with { Timeout = claim.Timeout with { TimeoutId = "different-address" } };
        (await Failure(actual).TryHoldFailureAsync(Observe(altered), altered, Guid.NewGuid(), None))
            .Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await Failure(actual).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), None))
            .Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
    }

    [Fact]
    public async Task ObserveAnotherOperationsCurrentHoldWithoutClaimingTheOriginalOperationCommitted()
    {
        var store = NewStore();
        await store.ScheduleTimeoutAsync(Timeout("other-host"), None);
        var original = await Claim(store, Now);
        var oldObservation = Observe(original);
        var current = await Claim(store, Now.AddMinutes(3));
        var otherOperation = Guid.NewGuid();
        var otherObservation = new SagaTimeoutFailureObservation(current, MessageFailureKind.Poison, "actual other holder");
        var other = await Failure(store).TryHoldFailureAsync(otherObservation, current, otherOperation, None);
        other.Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
        var actualReceipt = other.Receipt.ShouldNotBeNull();
        var observed = await Failure(store).TryHoldFailureAsync(oldObservation, original, Guid.NewGuid(), None);
        observed.Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
        var observedReceipt = observed.Receipt.ShouldNotBeNull();
        observedReceipt.Token.Value.ShouldBe(actualReceipt.Token.Value);
        observedReceipt.Kind.ShouldBe(MessageFailureKind.Poison);
        observedReceipt.Reason.ShouldBe("actual other holder");
        // SAME operation identity with conflicting inputs must reject before the current-hold shortcut.
        (await Failure(store).TryHoldFailureAsync(oldObservation, original, otherOperation, None))
            .Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await store.ClaimDueTimeoutsAsync(Now.AddHours(1), 10, None)).ShouldBeEmpty();
        (await Failure(store).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary", 10, null, None)).Items.Count.ShouldBe(1);
    }

    private static InMemorySagaTimeoutStore NewStore() => new(new TestTenantContext());
    private static ISagaTimeoutFailureStore Failure(InMemorySagaTimeoutStore store) => store;
    private static SagaTimeoutFailureObservation Observe(ClaimedSagaTimeout claim) => new(claim, MessageFailureKind.Unknown, "original");
    private static SagaTimeout Timeout(string id, byte[]? bytes = null) => new(id, "ordinary", "Saga", "Timeout", bytes ?? [1, 2, 3], Now.AddSeconds(-1), Now.AddMinutes(-1));
    private static async Task<ClaimedSagaTimeout> Claim(InMemorySagaTimeoutStore store, DateTimeOffset asOf) =>
        (await store.ClaimDueTimeoutsAsync(asOf, 1, None)).Single();
    private sealed class MutableTenant : ITenantContext
    {
        public string? TenantId { get; set; }
        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }
}
