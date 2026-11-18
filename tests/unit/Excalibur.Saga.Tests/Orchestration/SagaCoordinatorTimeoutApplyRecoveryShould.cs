// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

/// <summary>Successful state commit must not permanently discard a failed timeout-store apply.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Saga")]
public sealed class SagaCoordinatorTimeoutApplyRecoveryShould
{
    [Fact]
    public async Task RecoverTheCommittedTimerOnSameDeliveryWithoutRehandlingOrMintingAnotherId()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = budget.Token;
        var states = new InMemorySagaStore(new TestTenantContext());
        var timers = new InMemorySagaTimeoutStore(new TestTenantContext());
        var fault = new IOException("test-owned schedule failure before timer insertion");
        var observing = new PostCommitScheduleFault(states, timers, fault);
        var counter = new RunCounter();
        var dispatcher = CreateDispatcher();
        using var provider = CreateProvider(observing, counter, dispatcher);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule", EmitCommand: false);
        var messageId = Guid.NewGuid().ToString("N");

        using (var first = CreateCoordinator(provider, states))
        {
            var actual = await Should.ThrowAsync<IOException>(() => Handle(first, provider, message, messageId, token));
            actual.ShouldBeSameAs(fault);
        }

        // These observations are from the REAL store's serialization/deep-copy boundary, not a fake Save callback.
        var committed = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        committed.Version.ShouldBe(1);
        committed.RequestCount.ShouldBe(1);
        committed.ProcessedEventIds.ShouldContain("#msg:" + messageId);
        committed.TimeoutId.ShouldNotBeNullOrWhiteSpace();
        observing.ObservedCommittedId.ShouldBe(committed.TimeoutId);
        observing.ScheduleCalls.ShouldBe(1);
        (await timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
        DispatchCalls(dispatcher).ShouldBe(0);

        // Same delivery, new coordinator/saga activation, healthy apply endpoint after the one-shot fault.
        using (var redelivery = CreateCoordinator(provider, states))
        {
            await Handle(redelivery, provider, message, messageId, token);
        }

        var reloaded = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        reloaded.ShouldNotBeSameAs(committed);
        reloaded.TimeoutId.ShouldBe(committed.TimeoutId);
        reloaded.RequestCount.ShouldBe(1);
        counter.HandlerCalls.ShouldBe(1);
        DispatchCalls(dispatcher).ShouldBe(0); // This workload emits no command; positive arm below exercises dispatch.
        var rows = await timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token);
        rows.Count.ShouldBe(1, "redelivery must recover the committed timer rather than discard it as an already-processed event");
        rows[0].TimeoutId.ShouldBe(committed.TimeoutId);
        rows[0].SagaId.ShouldBe(message.SagaId);
        observing.ScheduleCalls.ShouldBe(2);
    }

    [Fact]
    public async Task RecoverThroughConstructorSelectedStoreWithoutAnyDiTimeoutRegistration()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = budget.Token;
        var states = new InMemorySagaStore(new TestTenantContext());
        var timers = new InMemorySagaTimeoutStore(new TestTenantContext());
        var fault = new IOException("test-owned schedule failure before timer insertion");
        var observing = new PostCommitScheduleFault(states, timers, fault);
        var counter = new RunCounter();
        var dispatcher = CreateDispatcher();
        using var provider = CreateConstructorProvider(observing, counter, dispatcher);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule", EmitCommand: false);
        var messageId = Guid.NewGuid().ToString("N");

        using (var first = CreateCoordinator(provider, states))
        {
            var actual = await Should.ThrowAsync<IOException>(() => Handle(first, provider, message, messageId, token));
            actual.ShouldBeSameAs(fault);
        }

        // These observations are from the REAL store's serialization/deep-copy boundary, not a fake Save callback.
        var committed = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        committed.Version.ShouldBe(1);
        committed.RequestCount.ShouldBe(1);
        committed.ProcessedEventIds.ShouldContain("#msg:" + messageId);
        committed.TimeoutId.ShouldNotBeNullOrWhiteSpace();
        observing.ObservedCommittedId.ShouldBe(committed.TimeoutId);
        observing.ScheduleCalls.ShouldBe(1);
        (await timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
        DispatchCalls(dispatcher).ShouldBe(0);

        // Same delivery, new coordinator/saga activation, healthy apply endpoint after the one-shot fault.
        using (var redelivery = CreateCoordinator(provider, states))
        {
            await Handle(redelivery, provider, message, messageId, token);
        }

        var reloaded = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        reloaded.ShouldNotBeSameAs(committed);
        reloaded.TimeoutId.ShouldBe(committed.TimeoutId);
        reloaded.RequestCount.ShouldBe(1);
        counter.HandlerCalls.ShouldBe(1);
        DispatchCalls(dispatcher).ShouldBe(0); // This workload emits no command; positive arm below exercises dispatch.
        var rows = await timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token);
        rows.Count.ShouldBe(1, "redelivery must recover the committed timer rather than discard it as an already-processed event");
        rows[0].TimeoutId.ShouldBe(committed.TimeoutId);
        rows[0].SagaId.ShouldBe(message.SagaId);
        observing.ScheduleCalls.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepHealthyTimerAndOptionalDispatchExactlyOnceOnSameDelivery(bool emitCommand)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = budget.Token;
        var states = new InMemorySagaStore(new TestTenantContext());
        var timers = new InMemorySagaTimeoutStore(new TestTenantContext());
        var observing = new PostCommitScheduleFault(states, timers, fault: null);
        var counter = new RunCounter();
        var dispatcher = CreateDispatcher();
        using var provider = CreateProvider(observing, counter, dispatcher);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule", emitCommand);
        var messageId = Guid.NewGuid().ToString("N");
        using (var first = CreateCoordinator(provider, states))
        {
            await Handle(first, provider, message, messageId, token);
        }

        var before = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        using (var second = CreateCoordinator(provider, states))
        {
            await Handle(second, provider, message, messageId, token);
        }

        var after = (await states.LoadAsync<RecoveryState>(Guid.Parse(message.SagaId), token)).ShouldNotBeNull();
        after.ShouldNotBeSameAs(before);
        after.TimeoutId.ShouldBe(before.TimeoutId);
        after.Version.ShouldBe(before.Version);
        after.RequestCount.ShouldBe(1);
        after.ProcessedEventIds.ShouldContain("#msg:" + messageId);
        counter.HandlerCalls.ShouldBe(1);
        observing.ScheduleCalls.ShouldBe(1);
        var rows = await timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token);
        rows.Count.ShouldBe(1);
        rows[0].TimeoutId.ShouldBe(after.TimeoutId);
        rows[0].SagaId.ShouldBe(message.SagaId);
        DispatchCalls(dispatcher).ShouldBe(emitCommand ? 1 : 0);
    }

    private static IDispatcher CreateDispatcher()
    {
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(MessageResult.Success()));
        return dispatcher;
    }

    private static int DispatchCalls(IDispatcher dispatcher) => Fake.GetCalls(dispatcher)
        .Count(call => call.Method.Name == nameof(IDispatcher.DispatchAsync));

    private static ServiceProvider CreateProvider(ISagaTimeoutStore timers, RunCounter counter, IDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new TestTenantContext());
        services.AddSingleton(timers);
        services.AddSingleton(counter);
        services.AddSingleton(dispatcher);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ServiceProvider CreateConstructorProvider(ISagaTimeoutStore timers, RunCounter counter, IDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new TestTenantContext());
        services.AddSingleton(new ConstructorTimers(timers));
        services.AddSingleton(counter);
        services.AddSingleton(dispatcher);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record ConstructorTimers(ISagaTimeoutStore Store);

    private static SagaCoordinator CreateCoordinator(IServiceProvider provider, ISagaStore states) =>
        new(provider, states, Options.Create(new SagaOptions()), NullLogger<SagaCoordinator>.Instance);

    private static Task Handle(SagaCoordinator coordinator, IServiceProvider provider, StartEvent message, string messageId, CancellationToken token) =>
        coordinator.HandleEventInternalAsync<RecoveringSaga, RecoveryState>(
            new MessageContext(message, provider) { MessageId = messageId },
            message,
            new SagaInfo(typeof(RecoveringSaga), typeof(RecoveryState)).StartsWith<StartEvent>(),
            token);

    private sealed record StartEvent(string SagaId, string? StepId, bool EmitCommand) : ISagaEvent;

    private sealed class TimeoutMessage;

    private sealed class EmittedCommand : IDispatchMessage;

    private sealed class RunCounter
    {
        public int HandlerCalls { get; set; }
    }

    internal sealed class RecoveryState : SagaState
    {
        public RecoveryState()
        {
        }

        public string? TimeoutId { get; set; }

        public int RequestCount { get; set; }
    }

    private sealed class RecoveringSaga : SagaBase<RecoveryState>
    {
        private readonly RunCounter counter;

        public RecoveringSaga(RecoveryState state, IDispatcher dispatcher, ILogger<RecoveringSaga> logger,
            RunCounter counter, ConstructorTimers? selected = null) : base(state, dispatcher, logger)
        {
            this.counter = counter;
            TimeoutStore = selected?.Store;
        }

        public override bool HandlesEvent(object eventMessage) => eventMessage is StartEvent;

        public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        {
            counter.HandlerCalls++;
            State.RequestCount++;
            State.TimeoutId = await RequestTimeoutAsync<TimeoutMessage>(TimeSpan.FromMinutes(1), cancellationToken);
            if (((StartEvent)eventMessage).EmitCommand)
            {
                await SendCommandAsync(new EmittedCommand(), cancellationToken);
            }

            return SagaEventOutcome.Handled;
        }
    }

    private sealed class PostCommitScheduleFault(InMemorySagaStore states, ISagaTimeoutStore inner, IOException? fault) : ISagaTimeoutStore, ISagaTimerRecoveryStore
    {
        private bool failNext = fault is not null;

        public int ScheduleCalls { get; private set; }

        public string? ObservedCommittedId { get; private set; }

        public async Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
        {
            await ObserveSchedule(timeout, cancellationToken);
            await inner.ScheduleTimeoutAsync(timeout, cancellationToken);
        }

        private async Task ObserveSchedule(SagaTimeout timeout, CancellationToken cancellationToken)
        {
            ScheduleCalls++;
            var committed = (await states.LoadAsync<RecoveryState>(Guid.Parse(timeout.SagaId), cancellationToken)).ShouldNotBeNull();
            committed.Version.ShouldBe(1);
            committed.RequestCount.ShouldBe(1);
            committed.ProcessedEventIds.Count.ShouldBe(1);
            committed.TimeoutId.ShouldBe(timeout.TimeoutId);
            ObservedCommittedId = committed.TimeoutId;
            if (failNext)
            {
                failNext = false;
                throw fault ?? new IOException("Missing injected primary fault");
            }

        }

        private ISagaTimerRecoveryStore Receiver => inner.GetService(typeof(ISagaTimerRecoveryStore))
            .ShouldBeAssignableTo<ISagaTimerRecoveryStore>().ShouldNotBeNull();

        public ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
            => Receiver.ObserveFenceAsync(domain, cancellationToken);

        public async ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
        {
            foreach (var intent in SagaTimerRecoveryCodec.Decode(journal))
            {
                if (intent.Action == SagaTimerAction.Schedule)
                {
                    await ObserveSchedule(intent.Timeout.ShouldNotBeNull(), cancellationToken);
                }
            }
            return await Receiver.ApplyAsync(journal, cancellationToken);
        }

        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
            => Receiver.IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);

        public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken token) => inner.CancelTimeoutAsync(sagaId, timeoutId, token);

        public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken token) => inner.CancelAllTimeoutsAsync(sagaId, token);

        public Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken token) => inner.ClaimDueTimeoutsAsync(asOf, batchSize, token);

        public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken token) => inner.GetDueTimeoutsAsync(asOf, token);

        public Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken token) => inner.MarkDeliveredAsync(claim, token);
    }
}
