// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>Checks process-local owning preparation before the transaction begins.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Saga")]
public sealed class SagaAtomicEmissionCaptureShould
{
    [Fact]
    public async Task BeginPreparationBeforeSendReturnsAndKeepEmissionOrder()
    {
        var saga = new ProbeSaga();
        var first = new TaskCompletionSource<IPreparedTransactionalOutboxWrite>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstWrite = A.Fake<IPreparedTransactionalOutboxWrite>();
        var secondWrite = A.Fake<IPreparedTransactionalOutboxWrite>();
        var calls = 0;
        saga.AtomicEmissionPreparer = (_, _, _) => ++calls == 1
            ? new ValueTask<IPreparedTransactionalOutboxWrite>(first.Task)
            : ValueTask.FromResult(secondWrite);

        var pendingFirst = saga.SendAsync(new ProbeMessage());
        calls.ShouldBe(1);
        pendingFirst.IsCompleted.ShouldBeFalse();
        await saga.SendAsync(new ProbeMessage()).ConfigureAwait(false);
        calls.ShouldBe(2);
        first.SetResult(firstWrite);
        await pendingFirst.ConfigureAwait(false);

        var writes = await saga.JoinPreparedEmissionsAsync().ConfigureAwait(false);
        writes.Count.ShouldBe(2);
        writes[0].ShouldBeSameAs(firstWrite);
        writes[1].ShouldBeSameAs(secondWrite);
    }

    [Fact]
    public async Task PreserveFirstFaultWhileJoiningEveryStartedPreparation()
    {
        var saga = new ProbeSaga();
        var primary = new IOException("first owning preparation");
        var second = new TaskCompletionSource<IPreparedTransactionalOutboxWrite>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        saga.AtomicEmissionPreparer = (_, _, _) => ++calls == 1
            ? ValueTask.FromException<IPreparedTransactionalOutboxWrite>(primary)
            : new ValueTask<IPreparedTransactionalOutboxWrite>(second.Task);

        var failed = saga.SendAsync(new ProbeMessage());
        var later = saga.SendAsync(new ProbeMessage());
        var joined = saga.JoinPreparedEmissionsAsync();
        joined.IsCompleted.ShouldBeFalse();
        second.SetException(new IOException("later owning preparation"));
        (await Should.ThrowAsync<IOException>(() => joined).ConfigureAwait(false)).ShouldBeSameAs(primary);
        (await Should.ThrowAsync<IOException>(() => failed).ConfigureAwait(false)).ShouldBeSameAs(primary);
        await Should.ThrowAsync<IOException>(() => later).ConfigureAwait(false);
    }

    [Fact]
    public async Task RejectNullPreparedWriteInsteadOfAcceptingPlaintextFallback()
    {
        var saga = new ProbeSaga
        {
            AtomicEmissionPreparer = (_, _, _) => ValueTask.FromResult<IPreparedTransactionalOutboxWrite>(null!),
        };

        await Should.ThrowAsync<InvalidOperationException>(() => saga.SendAsync(new ProbeMessage())).ConfigureAwait(false);
        await Should.ThrowAsync<InvalidOperationException>(() => saga.JoinPreparedEmissionsAsync()).ConfigureAwait(false);
    }

    [Fact]
    public async Task PreserveInvocationOrderAcrossSynchronousPreparerReentry()
    {
        var saga = new ProbeSaga();
        var firstWrite = A.Fake<IPreparedTransactionalOutboxWrite>();
        var nestedWrite = A.Fake<IPreparedTransactionalOutboxWrite>();
        var calls = 0;
        Task? nested = null;
        saga.AtomicEmissionPreparer = (_, _, _) =>
        {
            if (++calls == 1)
            {
                nested = saga.SendAsync(new ProbeMessage());
                return ValueTask.FromResult(firstWrite);
            }

            return ValueTask.FromResult(nestedWrite);
        };

        await saga.SendAsync(new ProbeMessage()).ConfigureAwait(false);
        await nested.ShouldNotBeNull().ConfigureAwait(false);
        var writes = await saga.JoinPreparedEmissionsAsync().ConfigureAwait(false);
        calls.ShouldBe(2);
        writes[0].ShouldBeSameAs(firstWrite);
        writes[1].ShouldBeSameAs(nestedWrite);
        await Should.ThrowAsync<InvalidOperationException>(() => saga.SendAsync(new ProbeMessage())).ConfigureAwait(false);
        calls.ShouldBe(2);
    }

    private sealed class ProbeState : SagaState
    {
    }

    private sealed class ProbeMessage : IDispatchMessage
    {
    }

    private sealed class ProbeSaga()
        : SagaBase<ProbeState>(new ProbeState { SagaId = Guid.NewGuid() }, A.Fake<IDispatcher>(), NullLogger.Instance)
    {
        public override bool HandlesEvent(object eventMessage) => true;

        public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
            => Task.FromResult(SagaEventOutcome.Handled);

        internal Task SendAsync(IDispatchMessage message) => SendCommandAsync(message, CancellationToken.None);
    }
}
