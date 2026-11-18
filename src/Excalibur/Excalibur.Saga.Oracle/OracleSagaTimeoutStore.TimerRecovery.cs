// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle;

public sealed partial class OracleSagaTimeoutStore : ISagaTimerRecoveryStore
{
    ValueTask<SagaTimerRecoveryFence> ISagaTimerRecoveryStore.ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken) =>
        ((ISagaTimerRecoveryStore)_timerRecovery).ObserveFenceAsync(domain, cancellationToken);
    ValueTask<SagaTimerRecoveryApplyResult> ISagaTimerRecoveryStore.ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken) =>
        ((ISagaTimerRecoveryStore)_timerRecovery).ApplyAsync(journal, cancellationToken);
    ValueTask<bool> ISagaTimerRecoveryStore.IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken) =>
        ((ISagaTimerRecoveryStore)_timerRecovery).IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);

    private static ClaimedSagaTimeout CreateRecoveryClaim(TimeoutRecord row, string claimToken)
    {
        var timeout = new SagaTimeout(row.TimeoutId, row.SagaId, row.SagaType, row.TimeoutType, row.TimeoutData, row.DueAt, row.ScheduledAt)
        { TenantId = KeyedTenantPartition.FromStoredValue(row.TenantId).TenantId };
        if (row.OwnedIncarnationId is null && row.OwnedGeneration is null && row.OwnedStateType is null)
        { return new ClaimedSagaTimeout(timeout, claimToken); }
        if (row.OwnedIncarnationId is not { Length: 16 } || row.OwnedGeneration is null || row.OwnedStateType is null
            || !Guid.TryParse(row.SagaId, out var sagaId))
        { throw new InvalidDataException("Oracle timeout has incomplete original recovery authority."); }
        return new ClaimedSagaTimeout(timeout, claimToken, new SagaTimeoutDeliveryIdentity(
            new SagaScopeIdentity(timeout.TenantId!, row.OwnedStateType, sagaId), row.TimeoutId,
            new Guid(row.OwnedIncarnationId), checked((long)row.OwnedGeneration.Value)));
    }
}
