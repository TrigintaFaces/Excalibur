// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Postgres;

public sealed partial class PostgresOutboxStore
{
    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken) =>
        CompleteAcquisitionAsync(messageId,claimIdentity,null,PostgresClaimTransition.Sent,null,0,null,cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authority.FencingToken);
        return CompleteAcquisitionAsync(messageId,authority.ClaimIdentity,authority.FencingToken,PostgresClaimTransition.Sent,null,0,null,cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
        return CompleteAcquisitionAsync(messageId,claimIdentity,null,PostgresClaimTransition.DeadLettered,reason,attemptCount,null,cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        OutboxWriteAuthority authority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authority.FencingToken);
        return CompleteAcquisitionAsync(messageId,authority.ClaimIdentity,authority.FencingToken,PostgresClaimTransition.DeadLettered,reason,attemptCount,null,cancellationToken);
    }

    private async ValueTask<OutboxCompletionOutcome> CompleteAcquisitionAsync(string messageId, string claimIdentity,
        long? fencingToken, PostgresClaimTransition transition, string? error, int retryCount,
        DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        if (fencingToken.HasValue) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fencingToken.Value); }
        var delay = nextAttemptAt.HasValue ? (double?)(nextAttemptAt.Value-DateTimeOffset.UtcNow).TotalSeconds : null;
        var request = new ClaimScopedOutboxCompletion(messageId,claimIdentity,fencingToken,transition,error,retryCount,delay,
            _options.FailureBackoffFloorSeconds,_options.QualifiedOutboxTableName,_options.QualifiedDeadLetterTableName,
            _options.QualifiedFenceTableName,DbTimeouts.RegularTimeoutSeconds,cancellationToken);
        // One statement participates in the borrowed connection's transaction. Applied is provisional
        // when its caller owns an explicit transaction; this store never commits or disposes that owner.
        return await _db.Connection.ResolveAsync(request).ConfigureAwait(false);
    }
}
