// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Diagnostics;

internal sealed partial class TelemetrySagaStoreDecorator
{
    object? IServiceProvider.GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ISagaStore) || serviceType == typeof(IServiceProvider)) { return this; }
        if (serviceType != typeof(ISagaAtomicTransitionStore)) { return null; }
        return _inner.GetService(serviceType) is ISagaAtomicTransitionStore ? new AtomicFacade(this) : null;
    }

    // Instrument listeners are application callbacks. Observability must never replace a durable result
    // or the provider's exact primary error/cancellation. Attempt the two instruments independently.
    private void RecordAtomicOperation(string operation,string sagaType,double durationMs)
    {
        try
        {
            var tags=new TagList { { "operation",operation }, { "saga_type",sagaType } };
            _operationsCounter.Add(1,tags);
        }
        catch(Exception error) when(error is not (AccessViolationException or StackOverflowException))
        {
            // Best-effort metric observer failure; never turn a committed/unknown outcome into a new result.
        }
        try
        {
            var tags=new TagList { { "operation",operation }, { "saga_type",sagaType } };
            _operationDuration.Record(durationMs,tags);
        }
        catch(Exception error) when(error is not (AccessViolationException or StackOverflowException))
        {
            // No recursive logging/metric reporting here: those observers can themselves throw.
        }
    }

    private ISagaAtomicTransitionStore AtomicCapability() =>
        _inner.GetService(typeof(ISagaAtomicTransitionStore)) as ISagaAtomicTransitionStore
        ?? throw new InvalidOperationException("The selected inner saga store does not support atomic transitions.");

    private sealed class AtomicFacade(TelemetrySagaStoreDecorator owner) : ISagaAtomicTransitionStore
    {
        public async ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity,
            ISagaAtomicTimeoutParticipant participant, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(participant);
            var start = Stopwatch.GetTimestamp();
            try { await owner.AtomicCapability().ValidateTimerParticipantAsync(identity, participant, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("atomic_timer_admission", identity.StateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
        public async ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(delivery);
            var start = Stopwatch.GetTimestamp();
            try { return await owner.AtomicCapability().IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("atomic_timeout_current", delivery.Scope.StateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
        public async ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrEmpty(deliveryId);
            var start = Stopwatch.GetTimestamp();
            try { return await owner.AtomicCapability().HasOwnedTimeoutIdentityAsync(identity, deliveryId, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("atomic_timeout_owned", identity.StateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
        public async ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            var start = Stopwatch.GetTimestamp();
            try { return await owner.AtomicCapability().LoadSnapshotAsync(identity, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("load_durable", identity.StateType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
        public async ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            var start = Stopwatch.GetTimestamp();
            try { return await owner.AtomicCapability().FindCommittedDeliveryAsync(identity, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("lookup_delivery", identity.SagaType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
        public async ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transition);
            var start = Stopwatch.GetTimestamp();
            try { return await owner.AtomicCapability().CommitAsync(transition, cancellationToken).ConfigureAwait(false); }
            finally { owner.RecordAtomicOperation("commit_durable", transition.Identity.SagaType, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
        }
    }
}
