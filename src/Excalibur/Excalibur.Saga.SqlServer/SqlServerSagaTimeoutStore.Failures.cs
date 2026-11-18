// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore : ISagaTimeoutFailureStore
{
    private readonly SqlSagaTimeoutFailureStore _failureStore;

    bool ISagaTimeoutFailureStore.HoldsSurviveProcessRestart => true;
    ValueTask ISagaTimeoutFailureStore.ValidateFailureStorageAsync(CancellationToken cancellationToken)
        => _failureStore.ValidateAsync(cancellationToken);
    ValueTask<SagaTimeoutHoldAttempt> ISagaTimeoutFailureStore.TryHoldFailureAsync(
        SagaTimeoutFailureObservation observation, ClaimedSagaTimeout claim, Guid operationId, CancellationToken cancellationToken)
        => _failureStore.HoldAsync(observation, claim, operationId, cancellationToken);
    ValueTask<SagaTimeoutHoldPage> ISagaTimeoutFailureStore.GetHeldFailuresAsync(
        string tenantId, string? sagaId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => _failureStore.GetHeldAsync(tenantId, sagaId, pageSize, cursor, cancellationToken);
    ValueTask<SagaTimeoutHoldReceipt?> ISagaTimeoutFailureStore.TryResumeHeldFailureAsync(
        SagaTimeoutHoldToken token, Guid commandId, CancellationToken cancellationToken)
        => _failureStore.ResumeAsync(token, commandId, cancellationToken);
}
