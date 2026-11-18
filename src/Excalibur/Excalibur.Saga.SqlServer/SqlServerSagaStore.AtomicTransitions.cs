// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaStore
{
    private readonly ISagaAtomicTransitionStore? _atomicTransitionStore;

    object? IServiceProvider.GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ISagaAtomicTransitionStore))
        {
            return SupportsAtomicTables && _atomicTransitionStore is not null
                ? new AtomicTransitionFacade(this)
                : null;
        }

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    // The first SQL slice deliberately owns these actual fixed-schema writers. Discovery must
    // not claim that changing only the legacy table option changes those writers as well.
    private bool SupportsAtomicTables =>
        string.Equals(_options.SchemaName, "dispatch", StringComparison.Ordinal)
        && string.Equals(_options.TableName, "sagas", StringComparison.Ordinal);

    private ISagaAtomicTransitionStore ResolveAtomicStore()
    {
        if (!SupportsAtomicTables || _atomicTransitionStore is null)
        {
            throw new InvalidOperationException(
                "Atomic saga transitions require an owned SQL connection string and the supported durable schema.");
        }

        return _atomicTransitionStore;
    }

    // Never return the raw kernel: previously discovered facades must revalidate selected
    // configuration on every call. The kernel itself validates the current authoritative tenant.
    private sealed class AtomicTransitionFacade(SqlServerSagaStore owner) : ISagaAtomicTransitionStore
    {
        public ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity,
            ISagaAtomicTimeoutParticipant participant, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().ValidateTimerParticipantAsync(identity, participant, cancellationToken);

        public ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);

        public ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().HasOwnedTimeoutIdentityAsync(identity, deliveryId, cancellationToken);

        public ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(
            SagaScopeIdentity identity, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().LoadSnapshotAsync(identity, cancellationToken);

        public ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(
            SagaDeliveryIdentity identity, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().FindCommittedDeliveryAsync(identity, cancellationToken);

        public ValueTask<SagaTransitionCommitResult> CommitAsync(
            SagaPreparedTransition transition, CancellationToken cancellationToken)
            => owner.ResolveAtomicStore().CommitAsync(transition, cancellationToken);
    }
}
