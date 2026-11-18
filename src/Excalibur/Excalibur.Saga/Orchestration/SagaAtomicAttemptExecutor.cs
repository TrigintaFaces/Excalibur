// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Diagnostics;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Orchestration;

/// <summary>Executes one admitted atomic attempt without owning the coordinator concurrency policy.</summary>
internal sealed class SagaAtomicAttemptExecutor(IServiceProvider serviceProvider, ISagaStore sagaStore,
    Func<Type, ISagaEvent, Guid> resolveSagaId, string replayKeyPrefix, ILogger logger)
{
    internal async Task HandleAtomicAttemptAsync<TSaga, TState>(
        AtomicAttemptScope attempt, SagaAtomicPreparationPolicy policy, IMessageContext context, ISagaEvent evt,
        SagaInfo sagaInfo, Func<TState, TSaga> createSaga,
        Func<TSaga, CancellationToken, Task<SagaEventOutcome>> invokeHandler, Func<TSaga, bool> isTimeoutHandler, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
    {
        await attempt.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delivery = context.MessageId;
            if (delivery is { Length: 0 })
            {
                throw new ArgumentException("An explicitly present delivery ID cannot be empty; use null for absent identity.", nameof(context));
            }

            var tenant = KeyedTenantPartition.FromContext(serviceProvider.GetRequiredService<ITenantContext>()).TenantId;
            var sagaId = resolveSagaId(typeof(TSaga), evt);
            var origin = SagaTimeoutDeliveryMetadata.Read(context, tenant);
            if (origin is not null && (origin.Scope.SagaId != sagaId
                || !string.Equals(origin.Scope.StateType, typeof(TState).Name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Timeout origin differs from the registered saga authority.");
            }
            if (attempt.ManualTimeout && (origin is not null || delivery is null))
            {
                throw new InvalidOperationException("Explicit manual timeout requires a fresh nonowned envelope identity and no owned origin.");
            }
            var invocation = new AtomicInvocationIdentity(typeof(TSaga), tenant, sagaId, typeof(TState).Name,
                origin?.TimeoutId ?? delivery, delivery, origin?.IncarnationId, origin?.Generation, attempt.ManualTimeout);
            if (attempt.OriginalInvocation is null)
            {
                attempt.OriginalInvocation = invocation;
                attempt.OriginalTimeout = origin;
            }
            else if (attempt.OriginalInvocation != invocation)
            {
                throw new InvalidOperationException("An atomic retry attempted to replace its original authority or delivery.");
            }

            if (attempt.Pending is { } pending)
            {
                if (pending.SagaImplementation != typeof(TSaga)
                    || pending.Scope.SagaId != invocation.SagaId
                    || !string.Equals(pending.Scope.TenantId, tenant, StringComparison.Ordinal)
                    || !string.Equals(pending.Batch.Identity.DeliveryId, invocation.DeliveryId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("An atomic retry attempted to replace its original authority or delivery.");
                }

                await CommitPendingAttemptAsync(attempt, pending, cancellationToken).ConfigureAwait(false);
                return;
            }

            await ExecuteAtomicHandlerAsync<TSaga, TState>(
                attempt, policy, context, evt, sagaInfo, createSaga, invokeHandler, isTimeoutHandler, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = attempt.Gate.Release();
        }
    }

    private async Task ExecuteAtomicHandlerAsync<TSaga, TState>(
        AtomicAttemptScope attempt, SagaAtomicPreparationPolicy policy, IMessageContext context, ISagaEvent evt,
        SagaInfo sagaInfo, Func<TState, TSaga> createSaga,
        Func<TSaga, CancellationToken, Task<SagaEventOutcome>> invokeHandler, Func<TSaga, bool> isTimeoutHandler, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
    {
        var atomic = sagaStore.GetService(typeof(ISagaAtomicTransitionStore)) as ISagaAtomicTransitionStore
            ?? throw new InvalidOperationException("The selected outer saga store does not support atomic transitions.");
        var original = attempt.OriginalInvocation
            ?? throw new InvalidOperationException("An atomic handler has no pinned invocation authority.");
        var tenant = original.TenantId;
        var sagaId = original.SagaId;
        var scope = new SagaScopeIdentity(tenant, original.StateType, sagaId);
        var origin = attempt.OriginalTimeout;
        if (origin is not null && !await atomic.IsTimeoutDeliveryCurrentAsync(origin, cancellationToken).ConfigureAwait(false))
        {
            return; // Positively known obsolete authority; no handler or preparation.
        }
        if (origin is null && original.EnvelopeId is not null
            && await atomic.HasOwnedTimeoutIdentityAsync(scope, original.EnvelopeId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A retained owned timeout cannot be admitted without its original metadata.");
        }
        var snapshot = await atomic.LoadSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        if (origin is not null && (snapshot is null || snapshot.IncarnationId != origin.IncarnationId
            || snapshot.Generation != origin.Generation))
        {
            return; // A synchronized generation/incarnation change superseded the earlier preflight.
        }
        if (snapshot?.Closed == true)
        {
            return;
        }

        var delivery = original.DeliveryId;
        if (snapshot is not null && delivery is not null
            && await DeliveryWasCommittedAsync(atomic, scope, snapshot, delivery, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (snapshot is null && !sagaInfo.IsStartEvent(evt.GetType()))
        {
            var missing = serviceProvider.GetService<ISagaNotFoundHandler<TSaga>>();
            if (missing is not null)
            {
                await missing.HandleAsync(evt, evt.SagaId, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var state = RestoreAtomicState<TState>(policy, snapshot, sagaId);
        var saga = createSaga(state);
        var timeoutHandler = isTimeoutHandler(saga);
        if (original.ManualTimeout && !timeoutHandler)
        {
            throw new InvalidOperationException("Explicit manual timeout requires the registered timeout handler.");
        }
        if (origin is null && timeoutHandler && !original.ManualTimeout)
        {
            throw new InvalidOperationException("Owned timeout metadata is required; missing metadata never selects manual permission.");
        }
        var timerParticipant = await ConfigurePreparationAsync(
            saga, atomic, scope, policy, context, cancellationToken).ConfigureAwait(false);

        SagaEventOutcome outcome;
        ExceptionDispatchInfo? handlerFailure = null;
        try
        {
            outcome = await invokeHandler(saga, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            handlerFailure = ExceptionDispatchInfo.Capture(error);
            outcome = SagaEventOutcome.Declined;
        }

        var writes = await JoinAtomicEmissionsAsync(saga, handlerFailure).ConfigureAwait(false);
        if (outcome != SagaEventOutcome.Handled)
        {
            return;
        }

        await FinalizeAtomicHandlerAsync(attempt, policy, atomic, scope, state, saga, snapshot,
            original, origin, timerParticipant, writes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ISagaAtomicTimeoutParticipant?> ConfigurePreparationAsync<TState>(
        SagaBase<TState> saga, ISagaAtomicTransitionStore atomic, SagaScopeIdentity scope,
        SagaAtomicPreparationPolicy policy, IMessageContext context, CancellationToken cancellationToken)
        where TState : SagaState
    {
        saga.TimeoutStore ??= serviceProvider.GetService<ISagaTimeoutStore>();
        var timerParticipant = RequireTimerParticipant(saga.TimeoutStore);
        if (timerParticipant is not null)
        {
            await atomic.ValidateTimerParticipantAsync(scope, timerParticipant, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var outerOutbox = serviceProvider.GetRequiredService<IOutboxStore>();
        // Discovery is through the selected outer decorator, never its raw concrete provider.
        _ = outerOutbox.GetService(typeof(ITransactionalOutboxPreparer)) as ITransactionalOutboxPreparer
            ?? throw new InvalidOperationException("The selected outer outbox does not support mediated preparation.");
        saga.HandlingContext = context;
        saga.AtomicEmissionPreparer = (message, child, token) => policy.PrepareEmissionAsync(outerOutbox, message, child, scope.TenantId, token);

        return timerParticipant;
    }

    private async Task FinalizeAtomicHandlerAsync<TSaga, TState>(AtomicAttemptScope attempt,
        SagaAtomicPreparationPolicy policy, ISagaAtomicTransitionStore atomic, SagaScopeIdentity scope,
        TState state, TSaga saga, SagaDurableSnapshot? snapshot, AtomicInvocationIdentity original,
        SagaTimeoutDeliveryIdentity? origin, ISagaAtomicTimeoutParticipant? timerParticipant,
        IReadOnlyList<IPreparedTransactionalOutboxWrite> writes, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
    {
        var sagaId = original.SagaId;
        var tenant = original.TenantId;
        var delivery = original.DeliveryId;
        if (state.SagaId != sagaId || state.Version != (snapshot?.Version ?? 0))
        {
            throw new InvalidOperationException("The handler changed authoritative saga identity or version.");
        }

        var dedupEntryEvicted = delivery is not null
            && SagaDeduplicationDiagnostics.MarkProcessed(state, replayKeyPrefix + delivery);

        if (state.Completed && state.CompletedAt is null)
        {
            state.CompletedAt = (serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
        }

        var identity = new SagaTransitionIdentity(tenant, typeof(TState).Name, sagaId,
            snapshot?.IncarnationId ?? Guid.NewGuid(), Guid.NewGuid(), delivery);
        if (state.PendingTimerRecovery is not null)
        { throw new InvalidOperationException("An atomic handler cannot introduce an ordinary timer recovery journal."); }
        state.TimerRecoveryIncarnationId = identity.IncarnationId;
        var transition = new SagaPreparedTransition(identity, snapshot?.Version ?? 0, snapshot?.Generation ?? 0,
            snapshot?.NextEffectSequence ?? 0, state.Completed, policy.EncodeState(state, typeof(TState)),
            saga.CapturePreparedTimers(snapshot?.Generation ?? 0), writes, state.CompletedAt, timerParticipant, original.ManualTimeout, origin);
        var pending = new PendingAtomicAttempt(atomic, scope, state, transition, typeof(TSaga), dedupEntryEvicted);
        attempt.Pending = pending;
        await CommitPendingAttemptAsync(attempt, pending, cancellationToken).ConfigureAwait(false);
        // No legacy Save, timer flush or dispatcher flush is reachable from this path.
    }

    private static TState RestoreAtomicState<TState>(SagaAtomicPreparationPolicy policy, SagaDurableSnapshot? snapshot, Guid sagaId)
        where TState : SagaState, new()
    {
        if (snapshot is null)
        {
            return new TState { SagaId = sagaId };
        }

        var bytes = snapshot.GetEncodedState() ?? throw new InvalidOperationException("An active saga lacks its state payload.");
        var state = policy.DecodeState(bytes, typeof(TState)) as TState
            ?? throw new InvalidOperationException("The selected state codec returned no matching saga state.");
        if (state.SagaId != sagaId || state.Completed)
        {
            throw new InvalidOperationException("Stored saga payload conflicts with its authoritative durable scope.");
        }

        state.Version = snapshot.Version;
        state.CompletedAt = snapshot.CompletedAt;
        return state;
    }

    private static ISagaAtomicTimeoutParticipant? RequireTimerParticipant(ISagaTimeoutStore? selected)
    {
        if (selected is null)
        {
            return null;
        }

        return selected.GetService(typeof(ISagaAtomicTimeoutParticipant)) as ISagaAtomicTimeoutParticipant
            ?? throw new InvalidOperationException("The selected timeout backend cannot participate in atomic saga transitions.");
    }

    private static async Task<bool> DeliveryWasCommittedAsync(ISagaAtomicTransitionStore atomic, SagaScopeIdentity scope,
        SagaDurableSnapshot snapshot, string delivery, CancellationToken cancellationToken)
    {
        var lookup = await atomic.FindCommittedDeliveryAsync(new SagaDeliveryIdentity(scope.TenantId, scope.StateType,
            scope.SagaId, snapshot.IncarnationId, delivery), cancellationToken).ConfigureAwait(false);
        return lookup.Outcome switch
        {
            SagaTransitionCommitOutcome.DeliveryAlreadyCommitted => true,
            SagaTransitionCommitOutcome.NotFound => false,
            _ => throw new InvalidOperationException("Delivery admission did not establish committed or authoritative absence."),
        };
    }

    private static async Task<IReadOnlyList<IPreparedTransactionalOutboxWrite>> JoinAtomicEmissionsAsync<TState>(
        SagaBase<TState> saga, ExceptionDispatchInfo? handlerFailure) where TState : SagaState
    {
        IReadOnlyList<IPreparedTransactionalOutboxWrite>? writes = null;
        try
        {
            writes = await saga.JoinPreparedEmissionsAsync().ConfigureAwait(false);
        }
        catch (Exception) when (handlerFailure is not null)
        {
            // Every preparation was joined. Preserve the handler's original authority/fault;
            // diagnostic observers must not replace it with a later preparation failure.
        }

        handlerFailure?.Throw();
        return writes!;
    }

    private static async Task ApplyAtomicOutcomeAsync(ISagaAtomicTransitionStore atomic, SagaScopeIdentity scope,
        SagaState state, SagaPreparedTransition transition, SagaTransitionCommitResult result, CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case SagaTransitionCommitOutcome.Committed:
            case SagaTransitionCommitOutcome.AlreadyCommitted:
                state.Version = result.CommittedVersion
                    ?? throw new InvalidOperationException("A committed transition has no authoritative version.");
                return;
            case SagaTransitionCommitOutcome.DeliveryAlreadyCommitted:
                // Discard this speculative object; load authoritative state, never write it back.
                _ = await atomic.LoadSnapshotAsync(scope, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("A winning delivery receipt has no durable scope.");
                return;
            case SagaTransitionCommitOutcome.VersionConflict:
                var current = await atomic.LoadSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
                throw new ConcurrencyException(nameof(SagaState), scope.SagaId.ToString(), transition.ExpectedVersion, current?.Version ?? -1L);
            case SagaTransitionCommitOutcome.Unknown:
                if (result.Failure is not null)
                {
                    ExceptionDispatchInfo.Capture(result.Failure).Throw();
                }

                throw new InvalidOperationException("The transition outcome remains unknown; do not infer rollback or regenerate the batch.");
            default:
                throw new InvalidOperationException("The transition was refused without advancing local saga state.");
        }
    }
    private async Task CommitPendingAttemptAsync(AtomicAttemptScope owner, PendingAtomicAttempt pending,
        CancellationToken cancellationToken)
    {
        var result = pending.KnownResult
            ?? await pending.Store.CommitAsync(pending.Batch, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == SagaTransitionCommitOutcome.VersionConflict)
        {
            // Authoritative CAS refusal allows a later handler attempt from newly loaded state.
            owner.Pending = null;
        }
        else if (result.Outcome != SagaTransitionCommitOutcome.Unknown)
        {
            // Includes refusals and successful authority: observer/reload failure must not rerun
            // the handler or regenerate a conflicting GUID on the next policy attempt.
            pending.KnownResult = result;
        }

        await ApplyAtomicOutcomeAsync(pending.Store, pending.Scope, pending.State, pending.Batch,
            result, cancellationToken).ConfigureAwait(false);
        if (pending.DedupEntryEvicted && !pending.EvictionReported
            && result.Outcome is SagaTransitionCommitOutcome.Committed or SagaTransitionCommitOutcome.AlreadyCommitted)
        {
            // The original immutable batch is positively acknowledged. Mark before any observer runs.
            pending.EvictionReported = true;
            SagaDeduplicationDiagnostics.Report(logger, "atomic");
        }
    }

    internal sealed class AtomicAttemptScope
    {
        // No AvailableWaitHandle is created. Do not dispose while a timed-out cooperative
        // operation may still hold the gate; scope/task references own its managed lifetime.
        internal SemaphoreSlim Gate { get; } = new(1, 1);

        internal bool ManualTimeout { get; init; }

        internal SagaTimeoutDeliveryIdentity? OriginalTimeout { get; set; }

        internal PendingAtomicAttempt? Pending { get; set; }

        internal AtomicInvocationIdentity? OriginalInvocation { get; set; }
    }

    internal sealed record AtomicInvocationIdentity(Type SagaImplementation, string TenantId,
        Guid SagaId, string StateType, string? DeliveryId, string? EnvelopeId,
        Guid? TimeoutIncarnation, long? TimeoutGeneration, bool ManualTimeout);

    internal sealed class PendingAtomicAttempt(ISagaAtomicTransitionStore store, SagaScopeIdentity scope,
        SagaState state, SagaPreparedTransition batch, Type sagaImplementation, bool dedupEntryEvicted)
    {
        internal ISagaAtomicTransitionStore Store { get; } = store;

        internal SagaScopeIdentity Scope { get; } = scope;

        internal SagaState State { get; } = state;

        internal SagaPreparedTransition Batch { get; } = batch;

        internal Type SagaImplementation { get; } = sagaImplementation;

        internal SagaTransitionCommitResult? KnownResult { get; set; }

        internal bool DedupEntryEvicted { get; } = dedupEntryEvicted;

        internal bool EvictionReported { get; set; }
    }

}
