// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
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
        var timeouts = A.Fake<ISagaTimeoutStore>();
        var states = A.Fake<ISagaStore>();
        SagaTimeout? scheduled = null;
        TimeoutState? saved = null;
        A.CallTo(() => timeouts.ScheduleTimeoutAsync(A<SagaTimeout>._, A<CancellationToken>._))
            .Invokes(call => scheduled = call.GetArgument<SagaTimeout>(0));
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._))
            .Invokes(call => saved = call.GetArgument<TimeoutState>(0));
        using var provider = CreateProvider(timeouts);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");
        await coordinator.HandleEventInternalAsync<TimedSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<TimedSaga>(), CancellationToken.None);
        scheduled.ShouldNotBeNull().SagaId.ShouldBe(message.SagaId);
        saved.ShouldNotBeNull().TimeoutId.ShouldBe(scheduled.TimeoutId);
        A.CallTo(() => timeouts.ScheduleTimeoutAsync(A<SagaTimeout>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => states.SaveAsync(A<TimeoutState>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreserveSubclassSuppliedStore()
    {
        var registered = A.Fake<ISagaTimeoutStore>();
        var supplied = A.Fake<ISagaTimeoutStore>();
        var states = A.Fake<ISagaStore>();
        using var provider = CreateProvider(registered, supplied);
        using var coordinator = CreateCoordinator(provider, states);
        var message = new StartEvent(Guid.NewGuid().ToString(), "schedule");
        await coordinator.HandleEventInternalAsync<PreconfiguredSaga, TimeoutState>(
            new MessageContext(message, provider), message, Info<PreconfiguredSaga>(), CancellationToken.None);
        A.CallTo(() => supplied.ScheduleTimeoutAsync(A<SagaTimeout>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => registered.ScheduleTimeoutAsync(A<SagaTimeout>._, A<CancellationToken>._)).MustNotHaveHappened();
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

    private static ServiceProvider CreateProvider(ISagaTimeoutStore? registered, ISagaTimeoutStore? supplied = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
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
            if (((StartEvent)eventMessage).StepId == "schedule")
                State.TimeoutId = await RequestTimeoutAsync<TimeoutMessage>(TimeSpan.FromMinutes(1), cancellationToken);
            return SagaEventOutcome.Handled;
        }
    }
    private sealed class PreconfiguredSaga : TimedSaga
    {
        public PreconfiguredSaga(TimeoutState state, IDispatcher dispatcher, ILogger<TimedSaga> logger, CustomStore custom)
            : base(state, dispatcher, logger) => TimeoutStore = custom.Store;
    }
}
