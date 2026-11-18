// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Regression")]
public sealed class SagaCommittedDedupEvictionShould
{
    private const string EvictionEvent = "SagaDeduplicationEntryEvicted";

    [Theory]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    public async Task ReportOnlyAnAcknowledgedInsertionThatEvicts(int retainedCount, int expectedWarnings)
    {
        using var fixture = new Fixture(retainedCount);
        await fixture.DeliverAsync("new-delivery");

        fixture.Calls.Handlers.ShouldBe(1);
        fixture.Calls.Saves.ShouldBe(1);
        fixture.Committed.ProcessedEventIds.Count.ShouldBe(1000);
        fixture.Committed.HasProcessedEvent("#msg:new-delivery").ShouldBeTrue();
        fixture.Committed.HasProcessedEvent("#msg:old-0").ShouldBe(retainedCount == 999);
        var warnings = fixture.Logger.Entries.Where(entry => entry.EventId.Name == EvictionEvent).ToArray();
        warnings.Length.ShouldBe(expectedWarnings);
        warnings.ShouldAllBe(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SuppressARetainedDuplicateWithoutSaveOrEvictionWarning()
    {
        using var fixture = new Fixture(1000);
        await fixture.DeliverAsync("old-0");

        fixture.Calls.Handlers.ShouldBe(0);
        fixture.Calls.Saves.ShouldBe(0);
        fixture.Committed.ProcessedEventIds.Count.ShouldBe(1000);
        fixture.Logger.Entries.ShouldNotContain(entry => entry.EventId.Name == EvictionEvent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreservePrimarySaveFailureAndNeverReportSpeculativeEviction(bool concurrencyFailure)
    {
        using var fixture = new Fixture(1000);
        Exception primary = concurrencyFailure
            ? new ConcurrencyException("competing state version")
            : new IOException("state write refused");
        fixture.SaveFailure = primary;

        Exception? caught = null;
        try { await fixture.DeliverAsync("new-delivery"); }
        catch (Exception error) { caught = error; }

        caught.ShouldBeSameAs(primary);
        fixture.Calls.Handlers.ShouldBe(1);
        fixture.Calls.Saves.ShouldBe(1);
        fixture.Committed.HasProcessedEvent("#msg:old-0").ShouldBeTrue();
        fixture.Committed.HasProcessedEvent("#msg:new-delivery").ShouldBeFalse();
        fixture.Logger.Entries.ShouldNotContain(entry => entry.EventId.Name == EvictionEvent);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly SagaCoordinator _coordinator;
        private readonly SagaInfo _info;

        public Fixture(int count)
        {
            Committed = new ProbeState();
            for (var index = 0; index < count; index++)
            {
                Committed.TryMarkEventProcessed("#msg:old-" + index);
            }
            var store = A.Fake<ISagaStore>();
            A.CallTo(() => store.GetService(A<Type>._)).Returns(null);
            A.CallTo(() => store.LoadAsync<ProbeState>(A<Guid>._, A<CancellationToken>._))
                .ReturnsLazily(() => Task.FromResult<ProbeState?>(Copy(Committed)));
            A.CallTo(() => store.SaveAsync(A<ProbeState>._, A<CancellationToken>._))
                .ReturnsLazily((ProbeState state, CancellationToken _) =>
                {
                    Calls.Saves++;
                    if (SaveFailure is not null) { return Task.FromException(SaveFailure); }
                    Committed = Copy(state);
                    return Task.CompletedTask;
                });
            var services = new ServiceCollection();
            services.AddSingleton(store);
            services.AddSingleton(A.Fake<IDispatcher>());
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(Calls);
            _provider = services.BuildServiceProvider();
            _info = new SagaInfo(typeof(ProbeSaga), typeof(ProbeState));
            _info.Handles<ProbeEvent>();
            _coordinator = new SagaCoordinator(_provider, store,
                global::Microsoft.Extensions.Options.Options.Create(new SagaOptions()), Logger);
        }

        public ProbeState Committed { get; private set; }
        public InvocationCounts Calls { get; } = new();
        public CapturingLogger<SagaCoordinator> Logger { get; } = new();
        public Exception? SaveFailure { get; set; }

        public Task DeliverAsync(string id) => _coordinator.HandleEventInternalAsync<ProbeSaga, ProbeState>(
            new MessageContext { MessageId = id },
            new ProbeEvent { SagaId = Committed.SagaId.ToString() }, _info, CancellationToken.None);

        public void Dispose()
        {
            _coordinator.Dispose();
            _provider.Dispose();
        }

        private static ProbeState Copy(ProbeState state)
        {
            var copy = new ProbeState
            {
                SagaId = state.SagaId,
                Version = state.Version,
                TimerRecoveryIncarnationId = state.TimerRecoveryIncarnationId,
            };
            foreach (var id in state.ProcessedEventIds)
            {
                copy.TryMarkEventProcessed(id);
            }
            return copy;
        }
    }

    private sealed class InvocationCounts
    {
        public int Handlers { get; set; }
        public int Saves { get; set; }
    }
    private sealed class ProbeState : SagaState;
    private sealed class ProbeEvent : ISagaEvent
    {
        public required string SagaId { get; init; }
        public string? StepId { get; init; }
    }
    private sealed class ProbeSaga(ProbeState state, IDispatcher dispatcher,
        ILogger<ProbeSaga> logger, InvocationCounts calls) : SagaBase<ProbeState>(state, dispatcher, logger)
    {
        public override bool HandlesEvent(object eventMessage) => eventMessage is ProbeEvent;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        {
            calls.Handlers++;
            return Task.FromResult(SagaEventOutcome.Handled);
        }
    }
}
