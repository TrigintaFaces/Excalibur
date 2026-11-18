// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Oracle;

public sealed partial class OracleSagaTimeoutStore : ISagaTimeoutFailureStore
{
    private readonly OracleTimeoutFailureStore _timeoutFailures;
    bool ISagaTimeoutFailureStore.HoldsSurviveProcessRestart => true;
    ValueTask ISagaTimeoutFailureStore.ValidateFailureStorageAsync(CancellationToken cancellationToken) =>
        new(_timeoutFailures.ValidateAsync(cancellationToken));
    ValueTask<SagaTimeoutHoldAttempt> ISagaTimeoutFailureStore.TryHoldFailureAsync(SagaTimeoutFailureObservation observation,
        ClaimedSagaTimeout claim, Guid operationId, CancellationToken cancellationToken) =>
        new(_timeoutFailures.TryHoldAsync(observation, claim, operationId, cancellationToken));
    ValueTask<SagaTimeoutHoldPage> ISagaTimeoutFailureStore.GetHeldFailuresAsync(string tenantId, string? sagaId,
        int pageSize, string? cursor, CancellationToken cancellationToken) =>
        new(_timeoutFailures.QueryAsync(tenantId, sagaId, pageSize, cursor, cancellationToken));
    ValueTask<SagaTimeoutHoldReceipt?> ISagaTimeoutFailureStore.TryResumeHeldFailureAsync(SagaTimeoutHoldToken token,
        Guid commandId, CancellationToken cancellationToken) =>
        new(_timeoutFailures.TryResumeAsync(token, commandId, cancellationToken));
}
