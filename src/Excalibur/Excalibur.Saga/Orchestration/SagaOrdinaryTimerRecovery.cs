// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Excalibur.Saga.Diagnostics;

namespace Excalibur.Saga.Orchestration;

internal sealed partial class SagaOrdinaryTimerRecovery(IServiceProvider serviceProvider, ISagaStore stateStore, ILogger logger)
{
    internal async ValueTask<OrdinaryTimerReceiver?> PairAsync<TState>(TState state,
        ISagaTimeoutStore? selected, CancellationToken cancellationToken) where TState : SagaState
    {
        if (selected?.GetService(typeof(ISagaTimerRecoveryStore)) is not ISagaTimerRecoveryStore receiver) { return null; }
        var tenant = KeyedTenantPartition.FromContext(serviceProvider.GetRequiredService<ITenantContext>()).TenantId;
        // Observe AFTER initial load/activation, then verify the same state still owns this attempt.
        // A concurrent first Apply+ack before load cannot leave an old token paired with new state.
        var fence = await receiver.ObserveFenceAsync(new SagaTimerRecoveryDomain(tenant, state.SagaId), cancellationToken).ConfigureAwait(false);
        var current = await stateStore.LoadAsync<TState>(state.SagaId, cancellationToken).ConfigureAwait(false);
        if (current is null ? state.Version != 0
            : current.Version != state.Version || current.TimerRecoveryIncarnationId != state.TimerRecoveryIncarnationId)
        { throw new ConcurrencyException(nameof(SagaState), state.SagaId.ToString(), state.Version, current?.Version ?? -1); }
        return new(selected, receiver, tenant, fence);
    }

    internal async ValueTask<bool> ValidateDeliveryAsync<TState>(IMessageContext context, TState state,
        OrdinaryTimerReceiver? selected, CancellationToken cancellationToken) where TState : SagaState
    {
        if (selected is null) { return true; }
        var origin = SagaTimeoutDeliveryMetadata.Read(context, selected.TenantId);
        if (origin is null) { return true; }
        if (origin.Scope.SagaId != state.SagaId || origin.Scope.StateType != typeof(TState).Name)
        { throw new InvalidOperationException("Original timeout authority differs from the resolved saga route."); }
        // First establish that this receiver knows this original ordinary authority. A profile mismatch
        // or missing authority is an error, not an obsolete-delivery success claim.
        if (!await selected.Receiver.IsTimeoutDeliveryCurrentAsync(origin, cancellationToken).ConfigureAwait(false)) { return false; }
        return origin.IncarnationId == state.TimerRecoveryIncarnationId;
    }

    [RequiresUnreferencedCode("Ordinary state transitions use the selected runtime state-store serializer.")]
    [RequiresDynamicCode("Ordinary state transitions use runtime state-store serialization.")]
    internal Task<bool> SaveAndApplyAsync<TState>(TState state, SagaBase<TState> saga,
        OrdinaryTimerReceiver? observed, CancellationToken cancellationToken) where TState : SagaState
        => SaveAndApplyAsync(state, saga, observed, false, cancellationToken);

    [RequiresUnreferencedCode("Ordinary state transitions use the selected runtime state-store serializer.")]
    [RequiresDynamicCode("Ordinary state transitions use runtime state-store serialization.")]
    internal async Task<bool> SaveAndApplyAsync<TState>(TState state, SagaBase<TState> saga,
        OrdinaryTimerReceiver? observed, bool dedupEntryEvicted, CancellationToken cancellationToken) where TState : SagaState
    {
        if (state.TimerRecoveryIncarnationId == Guid.Empty)
        {
            if (state.Version < 1) { throw new InvalidOperationException("A new saga must have a nonempty timer recovery incarnation."); }
            state.TimerRecoveryIncarnationId = Guid.NewGuid();
        }
        var intents = saga.CaptureOrdinaryTimerIntents();
        if (intents.Length != 0)
        {
            observed = Require(saga, observed);
            var encoded = SagaTimerRecoveryCodec.Encode(intents);
            state.TimerRecoverySequence = checked(state.TimerRecoverySequence + 1);
            state.PendingTimerRecovery = new SagaTimerRecoveryJournal(
                new SagaScopeIdentity(observed.TenantId, typeof(TState).Name, state.SagaId),
                state.TimerRecoveryIncarnationId, state.TimerRecoverySequence, Guid.NewGuid(),
                observed.Fence.Token, observed.Fence.MutationRevision, 1, encoded);
        }
        await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        if (dedupEntryEvicted) { SagaDeduplicationDiagnostics.Report(logger, "ordinary"); }
        if (intents.Length != 0)
        {
            if (!await RecoverAsync(state, observed, cancellationToken).ConfigureAwait(false)) { return false; }
            saga.ClearOrdinaryTimerIntents();
        }
        return true;
    }

    private OrdinaryTimerReceiver Require<TState>(SagaBase<TState> saga,
        OrdinaryTimerReceiver? observed) where TState : SagaState
    {
        if (saga.TimeoutStore is null
            || saga.TimeoutStore.GetService(typeof(ISagaTimerRecoveryStore)) is not ISagaTimerRecoveryStore receiver)
        {
            throw new InvalidOperationException("The selected timeout store must mediate persisted ordinary timer recovery before timer-owing state can commit.");
        }
        if (observed is not null && ReferenceEquals(observed.Store, saga.TimeoutStore)) { return observed; }
        throw new InvalidOperationException("The saga timeout store changed after its state/receiver authority was paired.");
    }

    [RequiresUnreferencedCode("Ordinary recovery acknowledges through the selected state store's runtime serializer.")]
    [RequiresDynamicCode("Ordinary recovery uses the selected state store's runtime serialization contract.")]
    internal async Task<bool> RecoverAsync<TState>(TState state,
        OrdinaryTimerReceiver? selected, CancellationToken cancellationToken) where TState : SagaState
    {
        if (state.PendingTimerRecovery is not { } journal) { return true; }
        if (selected is null)
        { throw new InvalidOperationException("A persisted timer journal requires its configured recovery-capable timeout store."); }
        if (journal.IncarnationId != state.TimerRecoveryIncarnationId || journal.Sequence != state.TimerRecoverySequence
            || journal.Scope.SagaId != state.SagaId || journal.Scope.StateType != typeof(TState).Name
            || !string.Equals(journal.Scope.TenantId, selected.TenantId, StringComparison.Ordinal))
        { throw new InvalidOperationException("Persisted timer journal differs from its current authoritative state."); }
        var result = await selected.Receiver.ApplyAsync(journal, cancellationToken).ConfigureAwait(false);
        if (result.Outcome is SagaTimerRecoveryApplyOutcome.Applied or SagaTimerRecoveryApplyOutcome.AlreadyApplied)
        {
            var intents = SagaTimerRecoveryCodec.Decode(journal);
            if (result.Fence is not { } committedFence || committedFence.IncarnationId != journal.IncarnationId
                || committedFence.Sequence != journal.Sequence)
            { throw new InvalidOperationException("Timer receipt belongs to different incarnation or batch sequence."); }
            if (result.IntentResults.Count != intents.Length)
            { throw new InvalidOperationException("Timer receipt does not cover every original intent."); }
            for (var ordinal = 0; ordinal < intents.Length; ordinal++)
            {
                var receipt = result.IntentResults[ordinal];
                var action = intents[ordinal].Action switch
                {
                    SagaTimerAction.Schedule => SagaPreparedTimerAction.Schedule,
                    SagaTimerAction.Cancel => SagaPreparedTimerAction.Cancel,
                    _ => SagaPreparedTimerAction.CancelAll,
                };
                if (receipt.Ordinal != ordinal || receipt.TimeoutId != intents[ordinal].TimeoutId
                    || receipt.Action != action || receipt.AuthorityRevision > committedFence.MutationRevision)
                { throw new InvalidOperationException("Timer receipt differs from original intent identity/order."); }
                if (receipt.Outcome != SagaTimerRecoveryIntentOutcome.Applied)
                {
                    try { LogDisposition(logger, journal.BatchId, ordinal, receipt.TimeoutId, receipt.Outcome, receipt.AuthorityRevision); }
                    catch (Exception error) when (error is not (AccessViolationException or StackOverflowException))
                    { /* An observer cannot replace a known durable timer disposition. */ }
                }
            }
            state.LastTimerRecoveryBatchId = journal.BatchId;
            state.PendingTimerRecovery = null;
            // REQUIRED provider join: actual Save CAS includes incarnation+version+pending batch.
            // If acknowledgement is lost, the persisted journal remains and exact receiver receipt replays.
            await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (result.Outcome == SagaTimerRecoveryApplyOutcome.Superseded)
        {
            var current = await stateStore.LoadAsync<TState>(state.SagaId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.TimerRecoveryIncarnationId != journal.IncarnationId
                || current.PendingTimerRecovery?.BatchId != journal.BatchId) { return false; }
        }
        // No automatic rebase, replacement timer IDs, state progress or handler re-entry.
        throw new IOException("The original ordinary timer journal remains unresolved under receiver authority.");
    }

    [LoggerMessage(SagaEventId.SagaRecoveryCompleted, LogLevel.Warning,
        "Timer recovery batch {BatchId} intent {Ordinal} timer {TimeoutId} has authoritative disposition {Outcome} at receiver revision {Revision}")]
    private static partial void LogDisposition(ILogger logger, Guid batchId, int ordinal, string timeoutId,
        SagaTimerRecoveryIntentOutcome outcome, long revision);

    internal sealed record OrdinaryTimerReceiver(ISagaTimeoutStore Store, ISagaTimerRecoveryStore Receiver,
        string TenantId, SagaTimerRecoveryFence Fence);
}
