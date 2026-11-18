// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

namespace Excalibur.Saga.Tests.Orchestration;

public sealed partial class SagaAtomicCoordinatorRetryShould
{
    [Theory]
    [InlineData(MessageFailureKind.Unknown)]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    public async Task RefuseAutomaticHandlerReplayForUnknownPermanentAndPoisonFaults(MessageFailureKind kind)
    {
        Exception primary = kind switch
        {
            MessageFailureKind.Unknown => new UnclassifiedSagaFailure("Controlled unknown saga fault."),
            MessageFailureKind.Permanent => new ArgumentException("Controlled permanent saga fault."),
            MessageFailureKind.Poison => new JsonException("Controlled poison saga fault."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var atomic = NewRetryClassificationStore();
        var counter = new Counter { OnHandle = () => throw primary };
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);
        var observed = await Should.ThrowAsync<Exception>(() => coordinator.ProcessEventAsync(CreateContext(),
            new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None));
        observed.ShouldBeSameAs(primary);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1, "the handler already prepared its immutable intent before throwing; no commit is permitted");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task HonorExplicitClassifierVetoForKnownHandlerTimeout()
    {
        var atomic = NewRetryClassificationStore();
        var primary = new TimeoutException("Controlled handler timeout veto.");
        var counter = new Counter { OnHandle = () => throw primary };
        using var provider = CreateProvider(atomic, counter, classifier: new ConstantSagaClassifier(MessageFailureKind.Permanent));
        using var coordinator = CreateCoordinator(provider);
        var observed = await Should.ThrowAsync<TimeoutException>(() => coordinator.ProcessEventAsync(CreateContext(),
            new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None));
        observed.ShouldBeSameAs(primary);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1, "the handler already prepared its immutable intent before throwing; no commit is permitted");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(MessageFailureKind.Transient)]
    [InlineData(MessageFailureKind.Uncertain)]
    public async Task AllowExplicitBusinessClassificationWithoutChangingTheDefaultUnknownRule(MessageFailureKind kind)
    {
        var atomic = NewRetryClassificationStore();
        var primary = new UnclassifiedSagaFailure("Explicitly classified recoverable business failure.");
        var failures = 0;
        var counter = new Counter { OnHandle = () => { if (++failures == 1) { throw primary; } } };
        using var provider = CreateProvider(atomic, counter, classifier: new ConstantSagaClassifier(kind));
        using var coordinator = CreateCoordinator(provider);
        await coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None);
        counter.Handlers.ShouldBe(2);
        counter.Preparations.ShouldBe(2, "each handler attempt prepares once; the failed attempt never committed");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HonorCustomVetoAfterThisCoordinatorsActualInnerTimeoutFires()
    {
        var atomic = NewRetryClassificationStore();
        var counter = new Counter { OnHandleAsync = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        using var provider = CreateProvider(atomic, counter, classifier: new ConstantSagaClassifier(MessageFailureKind.Permanent));
        using var coordinator = new SagaCoordinator(provider, provider.GetRequiredService<ISagaStore>(),
            global::Microsoft.Extensions.Options.Options.Create(new SagaOptions
            {
                MaxAttempts = 3, RetryDelay = TimeSpan.Zero, DefaultTimeout = TimeSpan.FromMilliseconds(100), MaxConcurrency = 1,
            }), NullLogger<SagaCoordinator>.Instance);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<TimeoutRejectedException>(() => coordinator.ProcessEventAsync(CreateContext(),
            new ProbeEvent(Guid.NewGuid().ToString()), budget.Token));
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1, "the handler already prepared its immutable intent before throwing; no commit is permitted");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RefuseToInventTimeoutEvidenceForAHandlerThrownTimeoutRejectedException()
    {
        var atomic = NewRetryClassificationStore();
        var primary = new TimeoutRejectedException("Handler-thrown exception without this coordinator's timeout event.");
        var counter = new Counter { OnHandle = () => throw primary };
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);
        var observed = await Should.ThrowAsync<TimeoutRejectedException>(() => coordinator.ProcessEventAsync(CreateContext(),
            new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None));
        observed.ShouldBeSameAs(primary);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1, "the handler already prepared its immutable intent before throwing; no commit is permitted");
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task KeepTheSamePreparedCommitWhenAnOuterRetryOwnerIsActive()
    {
        var atomic = NewRetryClassificationStore();
        var batches = new List<SagaPreparedTransition>();
        A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).ReturnsLazily(call =>
        {
            batches.Add(call.GetArgument<SagaPreparedTransition>(0)!);
            return ValueTask.FromResult(batches.Count == 1
                ? new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Unknown, null) { Failure = new IOException("Controlled lost commit reply.") }
                : new SagaTransitionCommitResult(SagaTransitionCommitOutcome.AlreadyCommitted, 1));
        });
        var counter = new Counter();
        using var provider = CreateProvider(atomic, counter);
        using var coordinator = CreateCoordinator(provider);
        var outer = new RetryMiddleware(global::Microsoft.Extensions.Options.Options.Create(new RetryOptions
        {
            MaxRetryAttempts = 2, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero,
        }), A.Fake<ITelemetrySanitizer>(), NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext();
        var evt = new ProbeEvent(Guid.NewGuid().ToString());
        var coordinatorEntries = 0;
        var result = await outer.InvokeAsync(evt, context, async (_, _, token) =>
        {
            coordinatorEntries++;
            await coordinator.ProcessEventAsync(context, evt, token);
            return MessageResult.Success();
        }, CancellationToken.None);
        result.Succeeded.ShouldBeTrue();
        coordinatorEntries.ShouldBe(1, "local pending-commit reconciliation must not be deferred out of its attempt scope");
        batches.Count.ShouldBe(2);
        batches[1].ShouldBeSameAs(batches[0]);
        batches[1].Identity.TransitionId.ShouldBe(batches[0].Identity.TransitionId);
        counter.Handlers.ShouldBe(1);
        counter.Preparations.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveCanonicalVersionConflictRecoveryAndExplicitCustomVeto(bool veto)
    {
        var atomic = NewRetryClassificationStore();
        var primary = new ConcurrencyException("ProbeState", Guid.NewGuid().ToString(), 0L, 1L);
        var failures = 0;
        var counter = new Counter { OnHandle = () => { if (++failures == 1) { throw primary; } } };
        using var provider = CreateProvider(atomic, counter,
            classifier: veto ? new ConstantSagaClassifier(MessageFailureKind.Permanent) : null);
        using var coordinator = CreateCoordinator(provider);
        var action = () => coordinator.ProcessEventAsync(CreateContext(), new ProbeEvent(Guid.NewGuid().ToString()), CancellationToken.None);
        if (veto)
        {
            var observed = await Should.ThrowAsync<ConcurrencyException>(action);
            observed.ShouldBeSameAs(primary);
            counter.Handlers.ShouldBe(1);
            counter.Preparations.ShouldBe(1);
            A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
        else
        {
            await action();
            counter.Handlers.ShouldBe(2);
            counter.Preparations.ShouldBe(2);
            A.CallTo(() => atomic.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }

    private static ISagaAtomicTransitionStore NewRetryClassificationStore()
    {
        var store = A.Fake<ISagaAtomicTransitionStore>();
        A.CallTo(() => store.LoadSnapshotAsync(A<SagaScopeIdentity>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<SagaDurableSnapshot?>(null));
        A.CallTo(() => store.CommitAsync(A<SagaPreparedTransition>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(new SagaTransitionCommitResult(SagaTransitionCommitOutcome.Committed, 1)));
        return store;
    }

    private sealed class UnclassifiedSagaFailure(string message) : Exception(message);
    private sealed class ConstantSagaClassifier(MessageFailureKind kind) : IMessageFailureClassifier
    {
        public MessageFailureKind Classify(Exception exception) => kind;
    }
}
