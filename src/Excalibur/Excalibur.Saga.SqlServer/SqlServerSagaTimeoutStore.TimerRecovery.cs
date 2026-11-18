// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore : ISagaTimerRecoveryStore
{
    private readonly SqlSagaTimerRecoveryReceiver _timerRecovery;

    ValueTask<SagaTimerRecoveryFence> ISagaTimerRecoveryStore.ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
        => _timerRecovery.ObserveFenceAsync(domain, cancellationToken);

    ValueTask<SagaTimerRecoveryApplyResult> ISagaTimerRecoveryStore.ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
        => _timerRecovery.ApplyAsync(journal, cancellationToken);

    ValueTask<bool> ISagaTimerRecoveryStore.IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
        => _timerRecovery.IsTimeoutDeliveryCurrentAsync(delivery, cancellationToken);
}
