// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Saga")]
public sealed class SagaCoordinatorTimeoutStoreShould
{
    [Fact]
    public async Task SupplyRegisteredStoreBeforeSchedulingAndSaving()
    {
        var timeouts = new RecordingTimeoutStore();
        var states = A.Fake<ISagaStore>();
        TimeoutState? saved = null;
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._))
            .Invokes(call => saved = call.GetArgument<TimeoutState>(0));
        using var provider = CreateProvider(timeouts);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");
        await coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None);
        var scheduled = (await timeouts.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).Single();
        scheduled.SagaId.ShouldBe(message.SagaId);
        saved.ShouldNotBeNull().TimeoutId.ShouldBe(scheduled.TimeoutId);
        timeouts.ApplyCalls.ShouldBe(1);
        // Primary state+journal commit, then acknowledgement clearing that same journal.
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task PreserveSubclassSuppliedStore()
    {
        var registered = new RecordingTimeoutStore();
        var supplied = new RecordingTimeoutStore();
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(registered, supplied);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");
        await coordinator.HandleEventInternalAsync<PreconfiguredSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<PreconfiguredSaga>(), CancellationToken.None);
        supplied.ApplyCalls.ShouldBe(1);
        registered.ApplyCalls.ShouldBe(0);
        registered.ObserveCalls.ShouldBe(0);
        (await supplied.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().SagaId.ShouldBe(message.SagaId);
        (await registered.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PreserveOptionalCompositionWithoutTimeoutUse()
    {
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(null);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "skip");
        await coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None);
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RefuseUnconfiguredTimeoutBeforeSaving()
    {
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(null);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None));
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    /// <summary>
    /// The orphan-timer shape. A concurrency conflict on save is the DESIGNED operating mode of a
    /// multi-instance deployment, not a fault: nothing is marked and nothing is saved, so the event stays
    /// deliverable. If the timeout row were already durable at that point, no committed state would name
    /// it, nothing could ever cancel it, and it would still be claimed and delivered at its due time --
    /// once per conflict.
    /// </summary>
    [Fact]
    public async Task WriteNoTimeoutRowWhenTheStateCommitIsRefused()
    {
        var timeouts = new RecordingTimeoutStore();
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(timeouts);
        using var coordinator = CreateCoordinator(provider, states);
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._))
            .Throws(new ConcurrencyException("another instance advanced this saga since it was loaded"));

        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");

        _ = await Should.ThrowAsync<ConcurrencyException>(() => coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None));

        timeouts.ApplyCalls.ShouldBe(0);
        (await timeouts.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldBeEmpty();
    }

    /// <summary>
    /// The parked-forever shape, and the reason this one is the more severe of the pair: a cancellation is
    /// DESTRUCTIVE. Applied before a refused commit, the row the reverted state still expects is already
    /// gone, so there is nothing left to deliver and nothing anywhere to notice -- the saga simply waits
    /// for a timeout that no longer exists.
    /// </summary>
    [Fact]
    public async Task RetireNoTimeoutRowWhenTheStateCommitIsRefused()
    {
        var timeouts = new RecordingTimeoutStore();
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(timeouts);
        using var coordinator = CreateCoordinator(provider, states);
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._))
            .Throws(new ConcurrencyException("another instance advanced this saga since it was loaded"));

        var message = new StartEvent(Guid.NewGuid().ToString(), "cancel");
        var existing = new SagaTimeout("pending-timeout", message.SagaId, "TimedSaga", "TimeoutMessage", [1, 2],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        await timeouts.ScheduleTimeoutAsync(existing, CancellationToken.None);

        _ = await Should.ThrowAsync<ConcurrencyException>(() => coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None));

        timeouts.ApplyCalls.ShouldBe(0);
        timeouts.LegacyCancelCalls.ShouldBe(0);
        var retained = (await timeouts.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).Single();
        retained.TimeoutId.ShouldBe(existing.TimeoutId);
        retained.TimeoutData.ShouldBe(existing.TimeoutData);
    }

    /// <summary>
    /// The liveness half of the two arms above, which a coordinator that applied NOTHING would also pass.
    /// Timers are applied BEFORE messages are dispatched: a dispatched message can come back as a new
    /// delivery, and a reply-driven cancellation running against a row that does not exist yet would find
    /// nothing to retire and leave the schedule behind as an orphan.
    /// </summary>
    [Fact]
    public async Task ApplyTheTimerBeforeDispatchingWhenTheStateCommitSucceeds()
    {
        var order = new List<string>();
        var timeouts = new RecordingTimeoutStore(() => order.Add("timer"));
        var states = A.Fake<ISagaStore>();
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._))
            .Invokes(call => order.Add(call.GetArgument<TimeoutState>(0)!.PendingTimerRecovery is null ? "ack" : "commit"));
        using var provider = CreateProvider(timeouts);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");

        await coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None);

        order.ShouldBe(["commit", "timer", "ack"],
            "the real receiver applies only after the state+journal commit; acknowledgement clears that same journal afterwards");
        timeouts.ApplyCalls.ShouldBe(1);
        timeouts.PayloadCountAtApply.ShouldBe(1);
        (await timeouts.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().SagaId.ShouldBe(message.SagaId);
    }

    // A transparent recording facet over REAL timer authority, not a fabricated success capability.
    // Observation occurs after actual Apply and diagnostic read; no legacy Schedule/Cancel call is replayed.
    private sealed class RecordingTimeoutStore(Action? applied = null) : ISagaTimeoutStore, ISagaTimerRecoveryStore
    {
        private readonly InMemorySagaTimeoutStore _inner = new(new TestTenantContext());
        public int ObserveCalls { get; private set; }
        public int ApplyCalls { get; private set; }
        public int LegacyCancelCalls { get; private set; }
        public int PayloadCountAtApply { get; private set; }
        private ISagaTimerRecoveryStore Receiver => _inner;
        public ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
        {
            ObserveCalls++;
            return Receiver.ObserveFenceAsync(domain, cancellationToken);
        }
        public async ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
        {
            var result = await Receiver.ApplyAsync(journal, cancellationToken);
            PayloadCountAtApply = (await _inner.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, cancellationToken)).Count;
            ApplyCalls++;
            applied?.Invoke();
            return result;
        }
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken) =>
            Receiver.IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);
        public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken) => _inner.ScheduleTimeoutAsync(timeout, cancellationToken);
        public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
        {
            LegacyCancelCalls++;
            return _inner.CancelTimeoutAsync(sagaId, timeoutId, cancellationToken);
        }
        public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken)
        {
            LegacyCancelCalls++;
            return _inner.CancelAllTimeoutsAsync(sagaId, cancellationToken);
        }
        public Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken) =>
            _inner.ClaimDueTimeoutsAsync(asOf, batchSize, cancellationToken);
        public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken) =>
            _inner.GetDueTimeoutsAsync(asOf, cancellationToken);
        public Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken) =>
            _inner.MarkDeliveredAsync(claim, cancellationToken);
    }

    private static ServiceProvider CreateProvider(ISagaTimeoutStore? registered, ISagaTimeoutStore? supplied = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDefaultTenantContext();
        services.AddSingleton(A.Fake<IDispatcher>());
        if (registered is not null) services.AddSingleton(registered);
        if (supplied is not null) services.AddSingleton(new CustomStore(supplied));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static SagaCoordinator CreateCoordinator(IServiceProvider provider, ISagaStore states)
    {
        // A fake otherwise manufactures a state with an unrelated random SagaId. Model the real
        // new-saga path explicitly so timer ownership is checked against the incoming start.
        A.CallTo(() => states.LoadAsync<TimeoutState>(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult<TimeoutState?>(null));
        return new(provider, states, Options.Create(new SagaOptions()), NullLogger<SagaCoordinator>.Instance);
    }
    private static SagaInfo Info<TSaga>() => new SagaInfo(typeof(TSaga), typeof(TimeoutState)).StartsWith<StartEvent>();
    private sealed record StartEvent(string SagaId, string? StepId) : ISagaEvent;
    private sealed class TimeoutMessage;
    private sealed class TimeoutState : SagaState { public string? TimeoutId { get; set; } }
    private sealed record CustomStore(ISagaTimeoutStore Store);
    private class TimedSaga(TimeoutState state, IDispatcher dispatcher, ILogger<TimedSaga> logger)
        : SagaBase<TimeoutState>(state, dispatcher, logger)
    {
        public override bool HandlesEvent(object eventMessage) => eventMessage is StartEvent;
        public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        {
            switch (((StartEvent)eventMessage).StepId)
            {
                case "schedule":
                    State.TimeoutId = await RequestTimeoutAsync<TimeoutMessage>(TimeSpan.FromMinutes(1), cancellationToken);
                    break;

                case "cancel":
                    // A saga cancelling a timeout it believes is pending -- the ordinary shape, and the one
                    // whose store call must not outlive a refused commit.
                    State.TimeoutId = null;
                    await CancelTimeoutAsync("pending-timeout", cancellationToken);
                    break;

                default:
                    break;
            }

            return SagaEventOutcome.Handled;
        }
    }
    private sealed class PreconfiguredSaga : TimedSaga
    {
        public PreconfiguredSaga(TimeoutState state, IDispatcher dispatcher, ILogger<TimedSaga> logger, CustomStore custom)
            : base(state, dispatcher, logger) => TimeoutStore = custom.Store;
    }
}
