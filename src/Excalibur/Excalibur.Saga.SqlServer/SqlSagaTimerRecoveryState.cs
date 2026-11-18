// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.SqlServer;

internal static class SqlSagaTimerRecoveryState
{
    internal readonly record struct RecoveryCutoff(long Through, long Revision);
    internal sealed record RecoveryIntentDisposition(SagaTimerRecoveryIntentOutcome Outcome, long Revision);
    internal sealed record RecoveryScopeRow(Guid Id, int Target, string Tenant, Guid SagaId, Guid Token,
        Guid Incarnation, long Sequence, long Revision, string? StateType, Guid? BatchId, Guid? ExpectedFence,
        long? ObservedRevision, byte[] Digest, long CancelAllThrough, long CancelAllRevision)
    {
        internal SagaTimerRecoveryFence Fence() => new(Token, Incarnation, Sequence, Revision);
    }
    internal sealed record RecoveryTimerRow(bool Tracked, Guid? Incarnation, long? Sequence, string? StateType,
        long CreatedRevision, bool Cancelled, long CancelThrough, long CancelRevision);
}
