// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

public sealed partial class SagaAtomicCoordinatorRetryShould
{
    [Theory]
    [InlineData(SagaTransitionCommitOutcome.Committed, 1)]
    [InlineData(SagaTransitionCommitOutcome.AlreadyCommitted, 1)]
    [InlineData(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, 0)]
    [InlineData(SagaTransitionCommitOutcome.IdentityConflict, 0)]
    [InlineData(SagaTransitionCommitOutcome.IncarnationClosed, 0)]
    public async Task ReportOnlyEvictionFromTheExactAcknowledgedAtomicBatch(SagaTransitionCommitOutcome outcome, int reports)
    {
        var store = A.Fake<ISagaAtomicTransitionStore>();
        var loads = 0;
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(++loads == 1 ? null
                : new SagaDurableSnapshot(Guid.NewGuid(), 1, 0, 0, false, "{}"u8.ToArray(), null)));
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(outcome,
                outcome is SagaTransitionCommitOutcome.Committed or SagaTransitionCommitOutcome.AlreadyCommitted ? 1 : null)));
        Counter counter = null!;
        counter = new Counter { OnHandle = () => SeedEvictionWindow(counter.State.ShouldNotBeNull()) };
        var logger = new CapturingLogger<SagaCoordinator>();
        using var provider = CreateProvider(store, counter);
        using var coordinator = EvictionCoordinator(provider, logger);
        Exception? caught = null;
        try { await coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None); }
        catch (Exception error) { caught = error; }

        if (outcome is SagaTransitionCommitOutcome.IdentityConflict or SagaTransitionCommitOutcome.IncarnationClosed)
        { caught.ShouldBeOfType<InvalidOperationException>(); }
        else { caught.ShouldBeNull(); }
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(reports);
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task ReportReconciledEvictionOnceWithoutRepeatingHandlerOrPreparedBatch()
    {
        var store = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(null));
        var batches = new List<SagaPreparedTransition>();
        var logger = new CapturingLogger<SagaCoordinator>();
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                logger.Entries.ShouldNotContain(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted");
                batches.Add(call.GetArgument<SagaPreparedTransition>(0)!);
                return ValueTask.FromResult(batches.Count == 1
                    ? new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Unknown, null) { Failure = new IOException("lost commit reply") }
                    : new SagaTransitionCommitResult(SagaTransitionCommitOutcome.AlreadyCommitted, 1));
            });
        Counter counter = null!;
        counter = new Counter { OnHandle = () => SeedEvictionWindow(counter.State.ShouldNotBeNull()) };
        using var provider = CreateProvider(store, counter);
        using var coordinator = EvictionCoordinator(provider, logger);
        await coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None);

        batches.Count.ShouldBe(2);
        batches[1].ShouldBeSameAs(batches[0]);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.StateEncodings.ShouldBe(1);
        logger.Entries.Count(entry => entry.EventId.Name == "SagaDeduplicationEntryEvicted").ShouldBe(1);
        AssertNoLegacyWrites(provider);
    }

    private static void SeedEvictionWindow(SagaState state)
    {
        for (var index = 0; index < 1000; index++) { state.TryMarkEventProcessed("#msg:seed-" + index); }
    }

    private static SagaCoordinator EvictionCoordinator(ServiceProvider provider, CapturingLogger<SagaCoordinator> logger)
        => new(provider, provider.GetRequiredService<ISagaStore>(),
            Options.Create(new SagaOptions { MaxAttempts = 2, RetryDelay = TimeSpan.Zero, MaxConcurrency = 1 }), logger);
}
