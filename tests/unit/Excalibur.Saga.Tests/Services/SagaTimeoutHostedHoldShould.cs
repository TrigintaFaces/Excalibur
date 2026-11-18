// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Services;

// Component handoff/observation tests, not substitute provider persistence or current-claim proof.
public sealed class SagaTimeoutHostedHoldShould
{
    [Fact]
    public async Task RefuseHostedStartWithoutSelectedCapabilityBeforeAnyClaim()
    {
        var store = A.Fake<ISagaTimeoutStore>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = NewWorker(store, provider);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => worker.StartAsync(CancellationToken.None));
        error.Message.ShouldContain(nameof(ISagaTimeoutFailureStore));
        A.CallTo(() => store.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PropagateUnusableSchemaBeforeAnyClaimOrDispatcher()
    {
        var store = A.Fake<ISagaTimeoutStore>();
        var facet = A.Fake<ISagaTimeoutFailureStore>();
        var primary = new InvalidOperationException("Controlled missing hold columns on actual selected target.");
        A.CallTo(() => store.GetService(typeof(ISagaTimeoutFailureStore))).Returns(facet);
        A.CallTo(() => facet.ValidateFailureStorageAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromException(primary));
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = NewWorker(store, provider);
        (await Should.ThrowAsync<InvalidOperationException>(() => worker.StartAsync(CancellationToken.None))).ShouldBeSameAs(primary);
        A.CallTo(() => store.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Unknown)]
    public async Task HoldOriginalObservationWithoutRetirement(MessageFailureKind kind)
    {
        var rig = new Rig(kind);
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("original diagnostic"), CancellationToken.None);
        rig.Calls.Count.ShouldBe(1);
        rig.Calls[0].Observation.Kind.ShouldBe(kind);
        rig.Calls[0].Observation.OriginalClaim.ClaimToken.ShouldBe("claim-1");
        rig.Calls[0].Claim.SourceEpoch.ShouldBe("source");
        rig.Calls[0].Claim.DeliveryEpoch.ShouldBe("cycle");
        A.CallTo(() => rig.Store.MarkDeliveredAsync(A<ClaimedSagaTimeout>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(MessageFailureKind.Transient)]
    [InlineData(MessageFailureKind.Uncertain)]
    public async Task PreserveRetryablePathAndCallerCancellation(MessageFailureKind kind)
    {
        var rig = new Rig(kind);
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new TimeoutException("recoverable"), CancellationToken.None);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await rig.Delivery.RecordFaultAsync(NewClaim(), new ArgumentException("not a cancellation disposition"), canceled.Token);
        rig.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task TreatUntypedFailedResultAsUnknownWithoutCallingClassifier()
    {
        var rig = new Rig(MessageFailureKind.Transient);
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordUntypedResultAsync(NewClaim(), "timeout transient poison status429", CancellationToken.None);
        rig.Calls.ShouldHaveSingleItem().Observation.Kind.ShouldBe(MessageFailureKind.Unknown);
        A.CallTo(() => rig.Classifier.Classify(A<Exception>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReconcileOriginalOperationBeforeFreshClaimRebindAndNeverDispatchSameCycle()
    {
        var rig = new Rig(MessageFailureKind.Unknown);
        var ackLost = new IOException("controlled unknown commit acknowledgment");
        rig.Reply = _ => throw ackLost;
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("failure"), CancellationToken.None);
        var originalOperation = rig.Calls[0].Operation;
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        rig.Reply = _ => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.RetryWithFreshClaim); // Positive same-cycle stale-claim result; no unknown commit was inferred.
        var fresh = NewClaim() with { ClaimToken = "claim-2" };
        // EXACT production order: reconcile first, then claim and suppress. The refusal updates
        // backoff before the fresh claim arrives; retaining it is necessary to make rebind reachable.
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        (await rig.Delivery.SuppressPendingAsync(fresh, CancellationToken.None)).ShouldBeTrue();
        rig.Calls.Count.ShouldBe(2);
        rig.Clock.Advance(TimeSpan.FromSeconds(2));
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Calls.Count.ShouldBe(4);
        rig.Calls[1].Claim.ClaimToken.ShouldBe("claim-1");
        rig.Calls[1].Operation.ShouldBe(originalOperation);
        rig.Calls[2].Claim.ClaimToken.ShouldBe("claim-1");
        rig.Calls[2].Operation.ShouldBe(originalOperation);
        rig.Calls[3].Claim.ClaimToken.ShouldBe("claim-2");
        rig.Calls[3].Operation.ShouldNotBe(originalOperation);
        rig.Calls[3].Observation.ShouldBeSameAs(rig.Calls[0].Observation);
        rig.Calls[3].Observation.OriginalClaim.ClaimToken.ShouldBe("claim-1");
        // New authorized cycle is not blacklisted by the old local observation.
        (await rig.Delivery.SuppressPendingAsync(fresh with { DeliveryEpoch = "resumed-cycle" }, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task BackOffUnresolvedHoldWithoutBlockingAnotherSource()
    {
        var rig = new Rig(MessageFailureKind.Unknown);
        rig.Reply = _ => throw new IOException("controlled unavailable storage");
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("failure"), CancellationToken.None);
        for (var index = 0; index < 10; index++)
        { await rig.Delivery.RetryPendingAsync(CancellationToken.None); }
        rig.Calls.Count.ShouldBe(1);
        (await rig.Delivery.SuppressPendingAsync(NewClaim() with { Timeout = NewClaim().Timeout with { TimeoutId = "healthy" } }, CancellationToken.None)).ShouldBeFalse();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Calls.Count.ShouldBe(2);
        rig.Calls[1].Operation.ShouldBe(rig.Calls[0].Operation);
    }

    [Fact]
    public async Task ReleaseEndedOriginalObservationWithoutWaitingForAnyFreshClaim()
    {
        var rig = new Rig(MessageFailureKind.Unknown);
        rig.Reply = _ => throw new IOException("controlled unknown hold outcome before operator cancellation");
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("original"), CancellationToken.None);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        rig.Reply = _ => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded);
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Calls.Count.ShouldBe(2);
        rig.Clock.Advance(TimeSpan.FromMinutes(10));
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Calls.Count.ShouldBe(2, "resolved source no longer retains/retries an unresolved observation");
        (await rig.Delivery.SuppressPendingAsync(NewClaim(), CancellationToken.None)).ShouldBeFalse();
        A.CallTo(() => rig.Store.MarkDeliveredAsync(A<ClaimedSagaTimeout>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task NeverTreatRejectedBindingAsEndedOrAuthorizeFreshClaimRebind()
    {
        var rig = new Rig(MessageFailureKind.Unknown);
        rig.Reply = _ => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected);
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("original"), CancellationToken.None);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        (await rig.Delivery.SuppressPendingAsync(NewClaim() with { ClaimToken = "claim-2" }, CancellationToken.None)).ShouldBeTrue();
        rig.Calls.Count.ShouldBe(2);
        rig.Calls[1].Operation.ShouldBe(rig.Calls[0].Operation);
        rig.Calls[1].Claim.ClaimToken.ShouldBe("claim-1");
    }

    [Fact]
    public async Task BoundNewDispatchAtCapacityWhileExistingRebindAndRecoveryRemainLive()
    {
        var rig = new Rig(MessageFailureKind.Unknown, maxPending: 1);
        rig.Reply = _ => throw new IOException("controlled disposition outage");
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("original"), CancellationToken.None);
        rig.Delivery.HasCapacity.ShouldBeFalse();
        var healthy = NewClaim() with { Timeout = NewClaim().Timeout with { TimeoutId = "healthy" } };
        (await rig.Delivery.SuppressPendingAsync(healthy, CancellationToken.None)).ShouldBeFalse();
        rig.Delivery.HasCapacity.ShouldBeFalse("the facade must defer NEW dispatch here without acknowledgment");
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        rig.Reply = _ => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
        (await rig.Delivery.SuppressPendingAsync(NewClaim() with { ClaimToken = "claim-2" }, CancellationToken.None)).ShouldBeTrue();
        rig.Calls.Count.ShouldBe(3, "full capacity still allows original reconciliation and fresh claim rebinding");
        rig.Calls[2].Claim.ClaimToken.ShouldBe("claim-2");
        rig.Clock.Advance(TimeSpan.FromSeconds(2));
        rig.Reply = call => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held,
            new SagaTimeoutHoldReceipt(new SagaTimeoutHoldToken("committed"), SagaTimeoutHoldState.Held,
                call.Claim.Timeout, call.Observation.Kind, call.Observation.Reason));
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Delivery.HasCapacity.ShouldBeTrue();
        (await rig.Delivery.SuppressPendingAsync(healthy, CancellationToken.None)).ShouldBeFalse();
        A.CallTo(() => rig.Store.MarkDeliveredAsync(A<ClaimedSagaTimeout>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task NeverDiscardOriginalPendingFromForeignBackendSameTextAddress()
    {
        var rig = new Rig(MessageFailureKind.Unknown, maxPending: 1);
        rig.Reply = _ => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected);
        await rig.Delivery.ValidateAsync(CancellationToken.None);
        await rig.Delivery.RecordFaultAsync(NewClaim(), new UnclassifiedFailureException("original"), CancellationToken.None);
        (await rig.Delivery.SuppressPendingAsync(NewClaim() with { SourceEpoch = "other-bound-backend" }, CancellationToken.None)).ShouldBeFalse();
        rig.Delivery.HasCapacity.ShouldBeFalse("a foreign same-ID claim cannot certify original source ended");
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        await rig.Delivery.RetryPendingAsync(CancellationToken.None);
        rig.Calls.Count.ShouldBe(2);
        rig.Calls[1].Observation.OriginalClaim.SourceEpoch.ShouldBe("source");
        rig.Calls[1].Operation.ShouldBe(rig.Calls[0].Operation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RetainBadConfigurationOrUntypedFailedResultWithoutBlindAcknowledgment(int mode)
    {
        var rig = new Rig(MessageFailureKind.Transient);
        var registry = new SagaTypeRegistry();
        var type = mode == 3 ? typeof(ProbeTimeout) : mode == 1 ? typeof(object) : typeof(RequiresArgument);
        if (mode != 0) { registry.RegisterType(type); }
        var claim = NewClaim() with
        {
            Timeout = NewClaim().Timeout with
            {
                TimeoutType = mode == 0 ? "not-registered" : type.AssemblyQualifiedName!,
                TimeoutData = mode == 1 ? new byte[] { 123, 125 } : null,
            },
        };
        var polls = 0;
        A.CallTo(() => rig.Store.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<IReadOnlyList<ClaimedSagaTimeout>>(
                Interlocked.Increment(ref polls) == 1 ? [claim] : []));
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<IMessageResult>(MessageResult.Failed("timeout status429 is diagnostic only")));
        using var provider = new ServiceCollection().AddSingleton(dispatcher).BuildServiceProvider();
        using var worker = new SagaTimeoutDeliveryService(rig.Store, provider, NullLogger<SagaTimeoutDeliveryService>.Instance,
            global::Microsoft.Extensions.Options.Options.Create(new SagaTimeoutOptions { PollInterval = TimeSpan.FromMilliseconds(20) }), registry);
        using var deadline = new CancellationTokenSource(global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
        try
        {
            await worker.StartAsync(deadline.Token);
            var held = await rig.Entered.Task.WaitAsync(deadline.Token);
            held.Observation.Kind.ShouldBe(MessageFailureKind.Unknown);
            held.Observation.OriginalClaim.Timeout.TimeoutId.ShouldBe(claim.Timeout.TimeoutId);
            held.Observation.Reason.ShouldContain(mode == 3 ? "diagnostic only" : "repair configuration");
            if (mode == 3)
            { A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly(); }
            else
            { A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened(); }
        }
        finally
        {
            await worker.StopAsync(deadline.Token);
            if (worker.ExecuteTask is { } execution) { await execution.WaitAsync(deadline.Token); }
        }
        A.CallTo(() => rig.Store.MarkDeliveredAsync(A<ClaimedSagaTimeout>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private sealed class UnclassifiedFailureException(string message) : Exception(message);

    private sealed class ProbeTimeout : IDispatchMessage
    {
        public ProbeTimeout() { }
    }

    private sealed class RequiresArgument(int value)
    {
        public int Value { get; } = value;
    }

    private static SagaTimeoutDeliveryService NewWorker(ISagaTimeoutStore store, IServiceProvider provider) => new(
        store, provider, NullLogger<SagaTimeoutDeliveryService>.Instance,
        global::Microsoft.Extensions.Options.Options.Create(new SagaTimeoutOptions()), new SagaTypeRegistry());

    private static ClaimedSagaTimeout NewClaim() => new(new SagaTimeout("timeout", "saga", "Saga", "Message", [1, 2, 3],
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch) { TenantId = "tenant" }, "claim-1")
    { SourceEpoch = "source", DeliveryEpoch = "cycle" };

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class Rig
    {
        internal Rig(MessageFailureKind kind, int maxPending = 2)
        {
            A.CallTo(() => Store.GetService(typeof(ISagaTimeoutFailureStore))).Returns(Facet);
            A.CallTo(() => Facet.ValidateFailureStorageAsync(A<CancellationToken>._)).ReturnsLazily(() => ValueTask.CompletedTask);
            A.CallTo(() => Classifier.Classify(A<Exception>._)).Returns(kind);
            A.CallTo(() => Facet.TryHoldFailureAsync(A<SagaTimeoutFailureObservation>._, A<ClaimedSagaTimeout>._,
                A<Guid>._, A<CancellationToken>._)).ReturnsLazily((SagaTimeoutFailureObservation observation,
                    ClaimedSagaTimeout claim, Guid operation, CancellationToken _) =>
                {
                    var call = new HoldCall(observation, claim, operation);
                    Calls.Add(call);
                    Entered.TrySetResult(call);
                    return ValueTask.FromResult(Reply(call));
                });
            Delivery = new SagaTimeoutFailureDelivery(Store, Classifier, NullLogger<SagaTimeoutDeliveryService>.Instance, maxPending, Clock);
        }
        internal ISagaTimeoutStore Store { get; } = A.Fake<ISagaTimeoutStore>();
        internal ISagaTimeoutFailureStore Facet { get; } = A.Fake<ISagaTimeoutFailureStore>();
        internal IMessageFailureClassifier Classifier { get; } = A.Fake<IMessageFailureClassifier>();
        internal Clock Clock { get; } = new();
        internal List<HoldCall> Calls { get; } = [];
        internal TaskCompletionSource<HoldCall> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SagaTimeoutFailureDelivery Delivery { get; }
        internal Func<HoldCall, SagaTimeoutHoldAttempt> Reply { get; set; } = call => new(SagaTimeoutHoldOutcome.Held,
            new SagaTimeoutHoldReceipt(new SagaTimeoutHoldToken("held"), SagaTimeoutHoldState.Held, call.Claim.Timeout, call.Observation.Kind, call.Observation.Reason));
    }

    private sealed record HoldCall(SagaTimeoutFailureObservation Observation, ClaimedSagaTimeout Claim, Guid Operation);
}
