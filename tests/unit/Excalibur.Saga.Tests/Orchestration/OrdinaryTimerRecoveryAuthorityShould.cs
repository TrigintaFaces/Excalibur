// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Regression")]
public sealed class OrdinaryTimerRecoveryAuthorityShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyOwnOrdinalsSeparatelyFromExternalCancellationCutoff(bool cancelAllFirst)
    {
        var store = new InMemorySagaTimeoutStore(new TestTenantContext());
        var domain = Domain();
        var fence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var timeout = Timeout(domain, "own-original");
        SagaTimerIntent[] intents = cancelAllFirst
            ? [new(SagaTimerAction.CancelAll, string.Empty, null), new(SagaTimerAction.Schedule, timeout.TimeoutId, timeout)]
            : [new(SagaTimerAction.Schedule, timeout.TimeoutId, timeout), new(SagaTimerAction.Cancel, timeout.TimeoutId, null)];
        var result = await store.ApplyAsync(Journal(domain, fence, intents), CancellationToken.None);
        result.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        result.IntentResults.Count.ShouldBe(2);
        var rows = await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None);
        rows.Count.ShouldBe(cancelAllFirst ? 1 : 0);
        if (cancelAllFirst) { rows[0].TimeoutId.ShouldBe(timeout.TimeoutId); }
    }

    [Fact]
    public async Task PreserveOtherIdWhenPublicCancellationWinsBeforeDelayedSchedule()
    {
        var store = new InMemorySagaTimeoutStore(new TestTenantContext());
        var domain = Domain();
        var fence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var first = Timeout(domain, "cancel-only-this"); var other = Timeout(domain, "keep-this");
        var journal = Journal(domain, fence, [new(SagaTimerAction.Schedule, first.TimeoutId, first), new(SagaTimerAction.Schedule, other.TimeoutId, other)]);
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), first.TimeoutId, CancellationToken.None);
        var receipt = await store.ApplyAsync(journal, CancellationToken.None);
        receipt.IntentResults[0].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Cancelled);
        receipt.IntentResults[1].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Applied);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(other.TimeoutId);
    }

    [Fact]
    public async Task PreserveNewerTimerFromAnOlderCapturedCancelAll()
    {
        var store = new InMemorySagaTimeoutStore(new TestTenantContext()); var domain = Domain();
        var fence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var journal = Journal(domain, fence, [new(SagaTimerAction.CancelAll, string.Empty, null)]);
        var newer = Timeout(domain, "newer-public-id");
        await store.ScheduleTimeoutAsync(newer, CancellationToken.None);
        var receipt = await store.ApplyAsync(journal, CancellationToken.None);
        receipt.IntentResults.ShouldHaveSingleItem().Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(newer.TimeoutId);
    }

    [Fact]
    public async Task RetainClaimAuthorityAfterPayloadRetirementAndRefuseTrackedIdResurrection()
    {
        var store = new InMemorySagaTimeoutStore(new TestTenantContext()); var domain = Domain();
        var fence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var timeout = Timeout(domain, "retained-original");
        var journal = Journal(domain, fence, [new(SagaTimerAction.Schedule, timeout.TimeoutId, timeout)]);
        await store.ApplyAsync(journal, CancellationToken.None);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, CancellationToken.None)).ShouldHaveSingleItem();
        var delivery = claim.OwnedDelivery.ShouldNotBeNull();
        (await store.MarkDeliveredAsync(claim, CancellationToken.None)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await store.IsTimeoutDeliveryCurrentAsync(delivery, CancellationToken.None)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => store.ScheduleTimeoutAsync(timeout, CancellationToken.None));
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), timeout.TimeoutId, CancellationToken.None);
        (await store.IsTimeoutDeliveryCurrentAsync(delivery, CancellationToken.None)).ShouldBeFalse();
        (await store.ApplyAsync(journal, CancellationToken.None)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RejectOldAcknowledgementAfterPurgeAndSameVersionRecreation()
    {
        var states = new InMemorySagaStore(new TestTenantContext()); var domain = Domain();
        var receiver = new InMemorySagaTimeoutStore(new TestTenantContext());
        var fence = await receiver.ObserveFenceAsync(domain, CancellationToken.None);
        var journal = Journal(domain, fence, [new(SagaTimerAction.CancelAll, string.Empty, null)]);
        var initial = new ProbeState
        {
            SagaId = domain.SagaId, TimerRecoveryIncarnationId = journal.IncarnationId,
            TimerRecoverySequence = 1, PendingTimerRecovery = journal,
            Completed = true, CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        await states.SaveAsync(initial, CancellationToken.None);
        var staleAck = (await states.LoadAsync<ProbeState>(initial.SagaId, CancellationToken.None)).ShouldNotBeNull();
        (await states.PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None)).ShouldBe(0);
        var healthyAck = (await states.LoadAsync<ProbeState>(initial.SagaId, CancellationToken.None)).ShouldNotBeNull();
        await receiver.ApplyAsync(journal, CancellationToken.None);
        healthyAck.PendingTimerRecovery = null; healthyAck.LastTimerRecoveryBatchId = journal.BatchId;
        await states.SaveAsync(healthyAck, CancellationToken.None);
        (await states.PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None)).ShouldBe(1);
        var recreated = new ProbeState { SagaId = initial.SagaId };
        await states.SaveAsync(recreated, CancellationToken.None);
        recreated.Version.ShouldBe(staleAck.Version); // Both1: Version-only CAS would wrongly accept.
        staleAck.PendingTimerRecovery = null; staleAck.LastTimerRecoveryBatchId = journal.BatchId;
        await Should.ThrowAsync<ConcurrencyException>(() => states.SaveAsync(staleAck, CancellationToken.None));
        var current = (await states.LoadAsync<ProbeState>(initial.SagaId, CancellationToken.None)).ShouldNotBeNull();
        current.TimerRecoveryIncarnationId.ShouldBe(recreated.TimerRecoveryIncarnationId);
        current.LastTimerRecoveryBatchId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task FenceOldReceiverAttemptAndQueuedDeliveryAfterNewIncarnationApplies()
    {
        var store = new InMemorySagaTimeoutStore(new TestTenantContext()); var domain = Domain();
        var firstFence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var first = Timeout(domain, "old-incarnation-id");
        var oldJournal = Journal(domain, firstFence, [new(SagaTimerAction.Schedule, first.TimeoutId, first)]);
        await store.ApplyAsync(oldJournal, CancellationToken.None);
        var oldClaim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, CancellationToken.None)).ShouldHaveSingleItem();
        await store.MarkDeliveredAsync(oldClaim, CancellationToken.None);
        var nextFence = await store.ObserveFenceAsync(domain, CancellationToken.None);
        var next = Timeout(domain, "new-incarnation-id");
        var newJournal = Journal(domain, nextFence, [new(SagaTimerAction.Schedule, next.TimeoutId, next)]);
        var applied = await store.ApplyAsync(newJournal, CancellationToken.None);
        applied.Fence.ShouldNotBeNull().Token.ShouldNotBe(nextFence.Token);
        (await store.ApplyAsync(oldJournal, CancellationToken.None)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Superseded);
        (await store.IsTimeoutDeliveryCurrentAsync(oldClaim.OwnedDelivery.ShouldNotBeNull(), CancellationToken.None)).ShouldBeFalse();
        (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(next.TimeoutId);
    }

    [Fact]
    public async Task PairCurrentStateAfterFirstBatchRotatesAnEarlierObservedFence()
    {
        var tenant = new TestTenantContext();
        var states = new InMemorySagaStore(tenant);
        var timers = new InMemorySagaTimeoutStore(tenant);
        var domain = Domain();
        var earlierA = await timers.ObserveFenceAsync(domain, CancellationToken.None);
        // B commits and acknowledges its first batch before A loads the now-current state.
        var first = Journal(domain, earlierA, [new(SagaTimerAction.CancelAll, string.Empty, null)]);
        var stateB = new ProbeState { SagaId = domain.SagaId, TimerRecoveryIncarnationId = first.IncarnationId,
            TimerRecoverySequence = 1, PendingTimerRecovery = first };
        await states.SaveAsync(stateB, CancellationToken.None);
        var receiptB = await timers.ApplyAsync(first, CancellationToken.None);
        stateB.PendingTimerRecovery = null; stateB.LastTimerRecoveryBatchId = first.BatchId;
        await states.SaveAsync(stateB, CancellationToken.None);
        var loadedA = (await states.LoadAsync<ProbeState>(domain.SagaId, CancellationToken.None)).ShouldNotBeNull();
        loadedA.Version.ShouldBe(2);
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenant);
        using var provider = services.BuildServiceProvider();
        var recovery = new SagaOrdinaryTimerRecovery(provider, states, NullLogger.Instance);
        var pairedA = (await recovery.PairAsync(loadedA, timers, CancellationToken.None)).ShouldNotBeNull();
        pairedA.Fence.Token.ShouldBe(receiptB.Fence.ShouldNotBeNull().Token);
        pairedA.Fence.Token.ShouldNotBe(earlierA.Token);
        var next = new SagaTimerRecoveryJournal(first.Scope, loadedA.TimerRecoveryIncarnationId, 2,
            Guid.NewGuid(), pairedA.Fence.Token, pairedA.Fence.MutationRevision, 1,
            SagaTimerRecoveryCodec.Encode([new(SagaTimerAction.CancelAll, string.Empty, null)]));
        loadedA.TimerRecoverySequence = 2; loadedA.PendingTimerRecovery = next;
        await states.SaveAsync(loadedA, CancellationToken.None);
        (await recovery.RecoverAsync(loadedA, pairedA, CancellationToken.None)).ShouldBeTrue();
        (await states.LoadAsync<ProbeState>(domain.SagaId, CancellationToken.None)).ShouldNotBeNull().PendingTimerRecovery.ShouldBeNull();
        // Same tenant/domain on a different actual receiver cannot accept the original receiver's journal.
        var foreign = new InMemorySagaTimeoutStore(tenant);
        await foreign.ObserveFenceAsync(domain, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(async () => await foreign.ApplyAsync(next, CancellationToken.None));
    }

    [Fact]
    public async Task PairRepeatedStableLegacyEmptyObservationsBeforeFirstJournalCapture()
    {
        var id = Guid.NewGuid();
        var states = A.Fake<ISagaStore>();
        A.CallTo(() => states.LoadAsync<ProbeState>(id, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<ProbeState?>(new ProbeState { SagaId = id, Version = 1, TimerRecoveryIncarnationId = Guid.Empty }));
        var first = (await states.LoadAsync<ProbeState>(id, CancellationToken.None)).ShouldNotBeNull();
        var second = (await states.LoadAsync<ProbeState>(id, CancellationToken.None)).ShouldNotBeNull();
        first.ShouldNotBeSameAs(second);
        first.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        second.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty);
        var tenant = new TestTenantContext();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenant);
        using var provider = services.BuildServiceProvider();
        var receiver = new InMemorySagaTimeoutStore(tenant);
        var helper = new SagaOrdinaryTimerRecovery(provider, states, NullLogger.Instance);
        var paired = (await helper.PairAsync(first, receiver, CancellationToken.None)).ShouldNotBeNull();
        paired.Fence.Sequence.ShouldBe(0);
        first.TimerRecoveryIncarnationId.ShouldBe(Guid.Empty); // Pair observes; it does not invent write authority.
        A.CallTo(() => states.SaveAsync(A<ProbeState>._, A<CancellationToken>._)).MustNotHaveHappened();
        var controlled = new IOException("test-owned failure before persistence; not a durable-save proof");
        SagaTimerRecoveryJournal? captured = null;
        A.CallTo(() => states.SaveAsync(A<ProbeState>._, A<CancellationToken>._))
            .Invokes((ProbeState snapshot, CancellationToken _) =>
            {
                snapshot.TimerRecoveryIncarnationId.ShouldNotBe(Guid.Empty);
                captured = snapshot.PendingTimerRecovery.ShouldNotBeNull();
                captured.IncarnationId.ShouldBe(snapshot.TimerRecoveryIncarnationId);
            }).ThrowsAsync(controlled);
        var saga = new LegacyCaptureSaga(first, receiver);
        var originalId = await saga.CaptureAsync(CancellationToken.None);
        var failure = await Should.ThrowAsync<IOException>(() => helper.SaveAndApplyAsync(first, saga, paired, CancellationToken.None));
        failure.ShouldBeSameAs(controlled);
        captured.ShouldNotBeNull().IncarnationId.ShouldBe(first.TimerRecoveryIncarnationId);
        captured.ExpectedFence.ShouldBe(paired.Fence.Token);
        captured.ObservedRevision.ShouldBe(paired.Fence.MutationRevision);
        first.TimerRecoverySequence.ShouldBe(1);
        SagaTimerRecoveryCodec.Decode(captured).ShouldHaveSingleItem().TimeoutId.ShouldBe(originalId);
        (await receiver.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldBeEmpty();
        A.CallTo(() => states.SaveAsync(A<ProbeState>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private sealed class LegacyCaptureSaga(ProbeState state, ISagaTimeoutStore receiver)
        : SagaBase<ProbeState>(state, A.Fake<IDispatcher>(), NullLogger<LegacyCaptureSaga>.Instance)
    {
        public Task<string> CaptureAsync(CancellationToken cancellationToken)
        {
            TimeoutStore = receiver;
            return RequestTimeoutAsync<LegacyTimeout>(TimeSpan.FromMinutes(1), cancellationToken);
        }
        public override bool HandlesEvent(object eventMessage) => false;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
            Task.FromResult(SagaEventOutcome.Declined);
    }

    private sealed class LegacyTimeout
    {
        public LegacyTimeout() { }
    }

    private static SagaTimerRecoveryDomain Domain() => new(TenantDefaults.DefaultTenantId, Guid.NewGuid());
    private static SagaTimeout Timeout(SagaTimerRecoveryDomain domain, string id) =>
        new(id, domain.SagaId.ToString(), "original-saga", "original-timeout", [1, 2], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    private static SagaTimerRecoveryJournal Journal(SagaTimerRecoveryDomain domain, SagaTimerRecoveryFence fence, SagaTimerIntent[] intents) =>
        new(new SagaScopeIdentity(domain.TenantId, nameof(ProbeState), domain.SagaId), Guid.NewGuid(), 1,
            Guid.NewGuid(), fence.Token, fence.MutationRevision, 1, SagaTimerRecoveryCodec.Encode(intents));
    private sealed class ProbeState : SagaState;
}
