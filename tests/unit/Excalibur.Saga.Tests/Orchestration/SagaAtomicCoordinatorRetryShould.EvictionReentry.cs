// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

public sealed partial class SagaAtomicCoordinatorRetryShould
{
    [Fact]
    public async Task NeverReportTheEvictionOfAnAtomicVersionConflictLoser()
    {
        var store = A.Fake<ISagaAtomicTransitionStore>();
        var loads = 0;
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(++loads == 1 ? null
                : new SagaDurableSnapshot(Guid.NewGuid(), 1, 0, 0, false, "{}"u8.ToArray(), null)));
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.VersionConflict, null)));
        Counter counter = null!;
        counter = new Counter { OnHandle = () => SeedEvictionWindow(counter.State.ShouldNotBeNull()) };
        using var provider = CreateProvider(store, counter);
        var logger = new CapturingLogger<SagaCoordinator>();
        var executor = new SagaAtomicAttemptExecutor(provider, provider.GetRequiredService<ISagaStore>(),
            static (_, message) => Guid.Parse(message.SagaId), "#msg:", logger);
        var attempt = new SagaAtomicAttemptExecutor.AtomicAttemptScope();
        var message = new ProbeEvent(Guid.NewGuid().ToString());
        var info = new SagaInfo(typeof(ProbeSaga), typeof(ProbeState));
        info.StartsWith<ProbeEvent>();

        var failure = await Should.ThrowAsync<ConcurrencyException>(() =>
            executor.HandleAtomicAttemptAsync<ProbeSaga, ProbeState>(attempt,
                provider.GetRequiredService<SagaAtomicPreparationPolicy>(), CreateContext(), message, info,
                state => new ProbeSaga(state, provider.GetRequiredService<IDispatcher>(), counter),
                (saga, token) => saga.HandleAsync(message, token), static _ => false, CancellationToken.None));

        failure.ShouldNotBeNull();
        attempt.Pending.ShouldBeNull("authoritative CAS loss discards this speculative batch");
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.State.ShouldNotBeNull().Version.ShouldBe(0);
        loads.ShouldBe(2);
        logger.Entries.ShouldNotContain(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted");
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task NeverRepeatAnEvictionReportWhenTheSameAttemptReentersWithKnownCommittedResult()
    {
        var store = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(null));
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1)));
        Counter counter = null!;
        counter = new Counter { OnHandle = () => SeedEvictionWindow(counter.State.ShouldNotBeNull()) };
        using var provider = CreateProvider(store, counter);
        var logger = new CapturingLogger<SagaCoordinator>();
        var executor = new SagaAtomicAttemptExecutor(provider, provider.GetRequiredService<ISagaStore>(),
            static (_, message) => Guid.Parse(message.SagaId), "#msg:", logger);
        var attempt = new SagaAtomicAttemptExecutor.AtomicAttemptScope();
        var message = new ProbeEvent(Guid.NewGuid().ToString());
        var context = CreateContext();
        var info = new SagaInfo(typeof(ProbeSaga), typeof(ProbeState));
        info.StartsWith<ProbeEvent>();
        var policy = provider.GetRequiredService<SagaAtomicPreparationPolicy>();

        // Exercise the existing retry entry, not two independent public processing invocations.
        Task EnterAsync() => executor.HandleAtomicAttemptAsync<ProbeSaga, ProbeState>(attempt, policy,
            context, message, info, state => new ProbeSaga(state, provider.GetRequiredService<IDispatcher>(), counter),
            (saga, token) => saga.HandleAsync(message, token), static _ => false, CancellationToken.None);
        await EnterAsync();
        var originalBatch = attempt.Pending.ShouldNotBeNull().Batch;
        attempt.Pending.KnownResult.ShouldNotBeNull().Outcome.ShouldBe(SagaTransitionCommitOutcome.Committed);
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(1);
        await EnterAsync();

        attempt.Pending.ShouldNotBeNull().Batch.ShouldBeSameAs(originalBatch);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.StateEncodings.ShouldBe(1);
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(1);
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }
}
