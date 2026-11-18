// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Marten;

public sealed partial class MartenOutboxStore
{
    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken) =>
        CompleteClaimAsync(messageId, claimIdentity, 0, null, 0, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return CompleteClaimAsync(messageId, claimIdentity, 1, errorMessage, retryCount, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount,
        DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return CompleteClaimAsync(messageId, claimIdentity, 2, errorMessage, retryCount, nextAttemptAt, cancellationToken);
    }

    private async ValueTask<OutboxCompletionOutcome> CompleteClaimAsync(string id, string acquisition, int operation,
        string? reason, int count, DateTimeOffset? requested, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisition);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        token.ThrowIfCancellationRequested();
        await using var connection = await OpenClaimConnectionAsync(token).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        // Claim before document is the same lock order used by acquisition, legacy settlement and cleanup.
        await MartenOutboxClaims.LockAcquisitionAsync(connection, transaction,
            _options.ClaimsSchemaName, _options.ClaimsTableName, id, token).ConfigureAwait(false);
        await using var session = _store.LightweightSession(global::Marten.Services.SessionOptions.ForTransaction(transaction));
        var document = await session.LoadAsync<MartenOutboxDocument>(id, token).ConfigureAwait(false);
        if (document is null) { return OutboxCompletionOutcome.MessageNotFound; }
        if (document.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered) { return OutboxCompletionOutcome.AlreadyTerminal; }
        // Recheck the live exact generation in the mutation itself, after the fresh document read.
        var changed = await MartenOutboxClaims.TransitionAcquisitionAsync(connection, transaction,
            _options.ClaimsSchemaName, _options.ClaimsTableName, id, acquisition, _options.ClaimTimeout,
            operation == 0, _options.FailureBackoffFloorSeconds, requested, token).ConfigureAwait(false);
        if (!changed) { return OutboxCompletionOutcome.ClaimLost; }
        if (operation == 0)
        {
            document.Status = OutboxStatus.Sent;
            document.SentAt = _timeProvider.GetUtcNow();
            document.LastError = null;
        }
        else
        {
            document.Status = operation == 2 ? OutboxStatus.Staged : OutboxStatus.Failed;
            document.LastError = reason;
            document.RetryCount = Math.Max(document.RetryCount, count);
            document.LastAttemptAt = _timeProvider.GetUtcNow();
        }
        session.Update(document);
        await session.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        if (operation == 0) { LogMessageSent(id); }
        else { LogMessageFailed(id, reason!, count); }
        return OutboxCompletionOutcome.Applied;
    }
}
