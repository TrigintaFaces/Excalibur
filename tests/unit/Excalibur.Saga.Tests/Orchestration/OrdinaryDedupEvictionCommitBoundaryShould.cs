// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Regression")]
public sealed class OrdinaryDedupEvictionCommitBoundaryShould
{
    [Fact]
    public async Task ReportPrimarySaveBeforeFailedTimerApplyAndNeverRepeatOnRecoveryAck()
    {
        var state = new ProbeState();
        var states = A.Fake<ISagaStore>();
        var writes = 0;
        SagaTimerRecoveryJournal? committedJournal = null;
        A.CallTo(() => states.LoadAsync<ProbeState>(state.SagaId, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<ProbeState?>(state));
        A.CallTo(() => states.SaveAsync(A<ProbeState>._, A<CancellationToken>._))
            .Invokes((ProbeState saved, CancellationToken _) =>
            {
                writes++;
                if (writes == 1) { committedJournal = saved.PendingTimerRecovery.ShouldNotBeNull(); }
            }).Returns(Task.CompletedTask);
        var real = new InMemorySagaTimeoutStore(new TestTenantContext());
        var selected = A.Fake<ISagaTimeoutStore>();
        A.CallTo(() => selected.GetService(A<Type>._)).Returns(null);
        var facet = A.Fake<ISagaTimerRecoveryStore>();
        A.CallTo(() => selected.GetService(typeof(ISagaTimerRecoveryStore))).Returns(facet);
        A.CallTo(() => facet.ObserveFenceAsync(A<SagaTimerRecoveryDomain>._, A<CancellationToken>._))
            .ReturnsLazily((SagaTimerRecoveryDomain domain, CancellationToken token) => real.ObserveFenceAsync(domain, token));
        var controlled = new IOException("controlled apply refusal after acknowledged primary save");
        A.CallTo(() => facet.ApplyAsync(A<SagaTimerRecoveryJournal>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromException<SagaTimerRecoveryApplyResult>(controlled));
        var services = new ServiceCollection();
        services.AddDefaultTenantContext();
        using var provider = services.BuildServiceProvider();
        var logger = new CapturingLogger<SagaCoordinator>();
        var helper = new SagaOrdinaryTimerRecovery(provider, states, logger);
        var paired = (await helper.PairAsync(state, selected, CancellationToken.None)).ShouldNotBeNull();
        var saga = new CaptureSaga(state, selected);
        var originalId = await saga.CaptureAsync();

        var failure = await Should.ThrowAsync<IOException>(() =>
            helper.SaveAndApplyAsync(state, saga, paired, dedupEntryEvicted: true, cancellationToken: CancellationToken.None));
        failure.ShouldBeSameAs(controlled);
        writes.ShouldBe(1);
        committedJournal.ShouldBeSameAs(state.PendingTimerRecovery);
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(1);
        (await real.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldBeEmpty();

        // Real receiver applies exactly the retained original journal; no fabricated Applied receipt.
        A.CallTo(() => facet.ApplyAsync(A<SagaTimerRecoveryJournal>._, A<CancellationToken>._))
            .ReturnsLazily((SagaTimerRecoveryJournal journal, CancellationToken token) => real.ApplyAsync(journal, token));
        (await helper.RecoverAsync(state, paired, CancellationToken.None)).ShouldBeTrue();
        writes.ShouldBe(2);
        state.PendingTimerRecovery.ShouldBeNull();
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(1);
        (await real.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(originalId);
    }

    private sealed class ProbeState : SagaState;
    private sealed class CaptureSaga(ProbeState state, ISagaTimeoutStore selected)
        : SagaBase<ProbeState>(state, A.Fake<IDispatcher>(), NullLogger<CaptureSaga>.Instance)
    {
        public Task<string> CaptureAsync()
        {
            TimeoutStore = selected;
            return RequestTimeoutAsync<ProbeTimeout>(TimeSpan.FromMinutes(1), CancellationToken.None);
        }
        public override bool HandlesEvent(object eventMessage) => false;
        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
            => Task.FromResult(SagaEventOutcome.Declined);
    }
    private sealed class ProbeTimeout
    {
        public ProbeTimeout() { }
    }
}
