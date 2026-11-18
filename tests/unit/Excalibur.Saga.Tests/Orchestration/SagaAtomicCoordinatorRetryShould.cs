// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>Exercises the actual coordinator's retry scope, not SQL transaction acceptance.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Saga")]
public sealed partial class SagaAtomicCoordinatorRetryShould
{
    [Fact]
    public async Task ReconcileUnknownUsingSamePreparedBatchWithoutRunningHandlerOrCodecAgain()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        var batches = new List<SagaPreparedTransition>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                batches.Add(call.GetArgument<SagaPreparedTransition>(0)!);
                return ValueTask.FromResult(batches.Count == 1
                    ? new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Unknown, null) { Failure = new IOException("lost reply") }
                    : new SagaTransitionCommitResult(SagaTransitionCommitOutcome.AlreadyCommitted, 1));
            });
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);

        await coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)
            .ConfigureAwait(false);

        batches.Count.ShouldBe(2);
        batches[1].ShouldBeSameAs(batches[0]);
        batches[1].Identity.TransitionId.ShouldBe(batches[0].Identity.TransitionId);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.State.ShouldNotBeNull().Version.ShouldBe(1);
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task RetainKnownDeliveryAuthorityWhileRetryingItsFailedReload()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        var reads = 0;
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(_ => ++reads switch
            {
                1 => new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null),
                2 => ValueTask.FromException<SagaDurableSnapshot?>(new IOException("reload connection fault")),
                _ => ValueTask.FromResult<SagaDurableSnapshot?>(new SagaDurableSnapshot(Guid.NewGuid(), 1, 0, 1, false, "{}"u8.ToArray(), null)),
            });
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, 1)));
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);

        await coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)
            .ConfigureAwait(false);

        reads.ShouldBe(3);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.State.ShouldNotBeNull().Version.ShouldBe(0); // speculative loser is never written back
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task ReleaseConcurrencyPermitWhenPolicyFactoryThrowsBeforeFirstAttempt()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1)));
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter, failFirstPolicyResolution: true);
        using var coordinator = CreateCoordinator(provider);
        var evt = new ProbeEvent(Guid.NewGuid().ToString());
        var error = await Should.ThrowAsync<IOException>(() =>
            coordinator.ProcessEventAsync(CreateContext(), evt, CancellationToken.None)).ConfigureAwait(false);
        error.Message.ShouldBe("first policy factory failure");
        counter.Handlers.ShouldBe(0);
        using var retryBudget = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await coordinator.ProcessEventAsync(CreateContext(), evt, retryBudget.Token).ConfigureAwait(false);

        counter.PolicyResolutions.ShouldBe(2);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RefuseDeliveryIdentityChangeAfterAuthoritativeCasConflict()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.VersionConflict, null)));
        var context = CreateContext();
        var counter = new Counter { OnHandle = () => context.MessageId = "changed-by-handler" };
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);

        var error = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ProcessEventAsync(
            context, new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)).ConfigureAwait(false);

        error.Message.ShouldContain("original authority or delivery");
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.State.ShouldNotBeNull().Version.ShouldBe(0);
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task RefuseExplicitlyEmptyDeliveryBeforeAuthorityLookupHandlerOrPreparation()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1)));
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);

        var error = await Should.ThrowAsync<ArgumentException>(() => coordinator.ProcessEventAsync(
            CreateContext(string.Empty), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)).ConfigureAwait(false);

        error.Message.ShouldContain("cannot be empty");
        counter.Handlers.ShouldBe(0);
        counter.Preparations.ShouldBe(0);
        counter.State.ShouldBeNull();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNoLegacyWrites(provider);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task PreserveNullAbsenceAndOpaqueWhitespaceDeliveryIdentity(string? deliveryId)
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        var batches = new List<SagaPreparedTransition>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                batches.Add(call.GetArgument<SagaPreparedTransition>(0)!);
                return ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1));
            });
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);

        await coordinator.ProcessEventAsync(CreateContext(deliveryId), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)
            .ConfigureAwait(false);

        var batch = batches.ShouldHaveSingleItem();
        batch.Identity.DeliveryId.ShouldBe(deliveryId);
        batch.Identity.TransitionId.ShouldNotBe(Guid.Empty);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.State.ShouldNotBeNull().Version.ShouldBe(1);
        AssertNoLegacyWrites(provider);
    }

    [Theory]
    [InlineData("__default__")]
    [InlineData("__untenanted__")]
    [InlineData("tenantA ")]
    public async Task CaptureMissingEmissionTenantFromExactStoreAuthorityWithoutMutatingInbound(string tenant)
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        SagaPreparedTransition? committed = null;
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                committed = call.GetArgument<SagaPreparedTransition>(0);
                return ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1));
            });
        var counter = new Counter { CapturePreparedTenant = true };
        using var provider = CreateProvider(atomic, counter, tenant: tenant);
        using var coordinator = CreateCoordinator(provider);
        var inbound = CreateContext();
        inbound.GetTenantId().ShouldBeNull();

        await coordinator.ProcessEventAsync(inbound, new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)
            .ConfigureAwait(false);

        var batch = committed.ShouldNotBeNull();
        batch.Identity.TenantId.ShouldBe(tenant);
        batch.Writes.ShouldHaveSingleItem().TenantId.ShouldBe(tenant);
        counter.PreparedTenant.ShouldBe(tenant);
        counter.Handlers.ShouldBe(1);
        inbound.GetTenantId().ShouldBeNull();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task RefuseExplicitDifferentEmissionTenantWithoutRelabelingItToStoreAuthority()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .Returns(new ValueTask<SagaDurableSnapshot?>((SagaDurableSnapshot?)null));
        var counter = new Counter { CapturePreparedTenant = true };
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);
        var inbound = CreateContext();
        inbound.GetOrCreateIdentityFeature().TenantId = "other-tenant";

        var error = await Should.ThrowAsync<ArgumentException>(() => coordinator.ProcessEventAsync(
            inbound, new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)).ConfigureAwait(false);

        error.Message.ShouldContain("tenant provenance differs");
        counter.PreparedTenant.ShouldBe("other-tenant");
        inbound.GetTenantId().ShouldBe("other-tenant");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNoLegacyWrites(provider);
    }

    [Fact]
    public async Task RefuseHandlerIntroducedOrdinaryJournalBeforeAtomicEncodingOrCommit()
    {
        var atomic = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => atomic.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(null));
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1)));
        Counter? counter = null;
        counter = new Counter
        {
            OnHandle = () =>
            {
                var state = counter.ShouldNotBeNull().State.ShouldNotBeNull();
                state.TimerRecoverySequence = 1;
                state.PendingTimerRecovery = new SagaTimerRecoveryJournal(
                    new SagaScopeIdentity("tenant", nameof(ProbeState), state.SagaId), state.TimerRecoveryIncarnationId,
                    1, Guid.NewGuid(), Guid.NewGuid(), 0, 1, [1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 0]);
            },
        };
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ProcessEventAsync(
            CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None)).ConfigureAwait(false);
        error.Message.ShouldContain("atomic handler cannot introduce an ordinary timer recovery journal");
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
        counter.StateEncodings.ShouldBe(0);
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNoLegacyWrites(provider);
    }

    private static ServiceProvider CreateProvider(ISagaAtomicTransitionStore atomic, Counter counter, bool failFirstPolicyResolution = false, string tenant = "tenant", IMessageFailureClassifier? classifier = null)
    {
        SagaRegistry.Register<ProbeSaga, ProbeState>(info => info.StartsWith<ProbeEvent>());
        var legacy = A.Fake<ISagaStore>();
        A.CallTo(() => ((IServiceProvider)legacy).GetService(typeof(ISagaAtomicTransitionStore))).Returns(atomic);
        var outer = A.Fake<IOutboxStore>();
        A.CallTo(() => ((IServiceProvider)outer).GetService(typeof(ITransactionalOutboxPreparer)))
            .Returns(A.Fake<ITransactionalOutboxPreparer>());
        var write = A.Fake<IPreparedTransactionalOutboxWrite>();
        A.CallTo(() => write.MessageId).Returns(Guid.NewGuid().ToString());
        A.CallTo(() => write.TenantId).ReturnsLazily(() => counter.CapturePreparedTenant ? counter.PreparedTenant : "tenant");
        A.CallTo(() => write.GetCanonicalWireBytes()).ReturnsLazily(() => "wire"u8.ToArray());
        var policy = new SagaAtomicPreparationPolicy((_, _) => { counter.StateEncodings++; return "{}"u8.ToArray(); },
            (_, _) => throw new InvalidOperationException("unknown reconciliation must not decode/re-run a handler"),
            (_, _, child, _) =>
            {
                counter.PreparedTenant = child.GetTenantId();
                counter.Preparations++;
                return ValueTask.FromResult(write);
            });
        var services = new ServiceCollection();
        services.AddSingleton(legacy);
        services.AddSingleton(outer);
        services.AddSingleton<SagaAtomicPreparationPolicy>(_ =>
        {
            counter.PolicyResolutions++;
            if (failFirstPolicyResolution && counter.PolicyResolutions == 1)
            {
                throw new IOException("first policy factory failure");
            }

            return policy;
        });
        services.AddSingleton(counter);
        if (classifier is not null) { services.AddSingleton(classifier); }
        services.AddSingleton<ITenantContext>(new TestTenantContext(tenant));
        services.AddSingleton(A.Fake<IDispatcher>());
        return services.BuildServiceProvider();
    }

    private static SagaCoordinator CreateCoordinator(ServiceProvider provider)
        => new(provider, provider.GetRequiredService<ISagaStore>(),
            Options.Create(new SagaOptions { MaxAttempts = 2, RetryDelay = TimeSpan.Zero, MaxConcurrency = 1 }), NullLogger<SagaCoordinator>.Instance);

    private static IMessageContext CreateContext(string? deliveryId = "same-delivery")
    {
        return new MessageContext { MessageId = deliveryId };
    }

    private static void AssertNoLegacyWrites(ServiceProvider provider)
    {
        var legacy = provider.GetRequiredService<ISagaStore>();
        A.CallTo(() => legacy.SaveAsync(A<ProbeState>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private sealed class Counter
    {
        internal bool CapturePreparedTenant { get; init; }

        internal string? PreparedTenant { get; set; }

        internal Action? OnHandle { get; init; }

        internal Func<CancellationToken, Task>? OnHandleAsync { get; init; }

        internal int PolicyResolutions { get; set; }

        internal int Handlers { get; set; }

        internal int Preparations { get; set; }

        internal int StateEncodings { get; set; }

        internal ProbeState? State { get; set; }
    }

    private sealed class ProbeState : SagaState
    {
    }

    private sealed record ProbeEvent(string SagaId) : ISagaEvent
    {
        public string? StepId => null;
    }

    private sealed class ProbeMessage : IDispatchMessage
    {
    }

    private sealed class ProbeSaga(ProbeState state, IDispatcher dispatcher, Counter counter)
        : SagaBase<ProbeState>(state, dispatcher, NullLogger.Instance)
    {
        public override bool HandlesEvent(object eventMessage) => true;

        public override async Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
        {
            counter.Handlers++;
            counter.State = State;
            await SendCommandAsync(new ProbeMessage(), cancellationToken).ConfigureAwait(false);
            counter.OnHandle?.Invoke();
            if (counter.OnHandleAsync is { } next) { await next(cancellationToken).ConfigureAwait(false); }
            return SagaEventOutcome.Handled;
        }
    }
}
