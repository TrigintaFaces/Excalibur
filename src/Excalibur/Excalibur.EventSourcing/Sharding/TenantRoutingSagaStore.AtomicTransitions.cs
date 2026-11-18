// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.EventSourcing.Sharding;

internal sealed partial class TenantRoutingSagaStore
{
    object? IServiceProvider.GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ISagaStore) || serviceType == typeof(IServiceProvider)) { return this; }
        if (serviceType != typeof(ISagaAtomicTransitionStore)) { return null; }
        var selected = _resolver.Resolve(ResolveTenant());
        return selected.GetService(typeof(ISagaAtomicTransitionStore)) is ISagaAtomicTransitionStore
            ? new AtomicFacade(this) : null;
    }

    private ISagaAtomicTransitionStore AtomicFor(string requestedTenant)
    {
        var current = ResolveTenant();
        if (!string.Equals(current, requestedTenant, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Atomic saga request tenant differs from the authoritative routing tenant.");
        }
        var selected = _resolver.Resolve(current);
        return selected.GetService(typeof(ISagaAtomicTransitionStore)) as ISagaAtomicTransitionStore
            ?? throw new InvalidOperationException("The currently selected tenant shard does not support atomic saga transitions.");
    }

    // Capture only the outer owner. Every call revalidates ambient authority and re-resolves the selected facet.
    private sealed class AtomicFacade(TenantRoutingSagaStore owner) : ISagaAtomicTransitionStore
    {
        public ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity,
            ISagaAtomicTimeoutParticipant participant, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(participant);
            return owner.AtomicFor(identity.TenantId).ValidateTimerParticipantAsync(identity, participant, cancellationToken);
        }
        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(delivery);
            return owner.AtomicFor(delivery.Scope.TenantId).IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);
        }
        public ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrEmpty(deliveryId);
            return owner.AtomicFor(identity.TenantId).HasOwnedTimeoutIdentityAsync(identity, deliveryId, cancellationToken);
        }
        public ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            return owner.AtomicFor(identity.TenantId).LoadSnapshotAsync(identity, cancellationToken);
        }
        public ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);
            return owner.AtomicFor(identity.TenantId).FindCommittedDeliveryAsync(identity, cancellationToken);
        }
        public ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transition);
            return owner.AtomicFor(transition.Identity.TenantId).CommitAsync(transition, cancellationToken);
        }
    }
}
