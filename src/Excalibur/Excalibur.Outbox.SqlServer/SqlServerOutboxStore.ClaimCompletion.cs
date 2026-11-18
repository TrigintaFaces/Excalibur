// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer.Requests;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.SqlServer;

public sealed partial class SqlServerOutboxStore
{
    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken)
    {
        ValidateClaim(messageId, claimIdentity);
        return CompleteClaimAsync(messageId, claimIdentity, null, SqlServerClaimTransition.Sent, null, 0, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority, CancellationToken cancellationToken)
    {
        ValidateAuthority(messageId, authority);
        return CompleteClaimAsync(messageId, authority.ClaimIdentity, authority.FencingToken, SqlServerClaimTransition.Sent, null, 0, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, string claimIdentity, CancellationToken cancellationToken) =>
        FailClaimAsync(messageId, errorMessage, retryCount, null, claimIdentity, cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount,
        DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken) =>
        FailClaimAsync(messageId, errorMessage, retryCount, nextAttemptAt, claimIdentity, cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ValidateClaim(messageId, claimIdentity);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
        return CompleteClaimAsync(messageId, claimIdentity, null, SqlServerClaimTransition.DeadLettered, reason, attemptCount, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        OutboxWriteAuthority authority, CancellationToken cancellationToken)
    {
        ValidateAuthority(messageId, authority);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptCount);
        return CompleteClaimAsync(messageId, authority.ClaimIdentity, authority.FencingToken, SqlServerClaimTransition.DeadLettered, reason, attemptCount, null, cancellationToken);
    }

    private ValueTask<OutboxCompletionOutcome> FailClaimAsync(string messageId, string errorMessage, int retryCount,
        DateTimeOffset? nextAttemptAt, string claimIdentity, CancellationToken cancellationToken)
    {
        ValidateClaim(messageId, claimIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        return CompleteClaimAsync(messageId, claimIdentity, null, SqlServerClaimTransition.Failed, errorMessage, retryCount, nextAttemptAt, cancellationToken);
    }

    private static void ValidateClaim(string messageId, string claimIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);
    }

    private static void ValidateAuthority(string messageId, OutboxWriteAuthority authority)
    {
        ValidateClaim(messageId, authority.ClaimIdentity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authority.FencingToken);
    }

    private async ValueTask<OutboxCompletionOutcome> CompleteClaimAsync(string messageId, string? claimIdentity, long? fencingToken,
        SqlServerClaimTransition transition, string? error, int attempts, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (fencingToken.HasValue) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fencingToken.Value); }
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        try
        {
            var accepted = await AcceptScopeAsync(connection, transaction, fencingToken, cancellationToken).ConfigureAwait(false);
            var outcome = accepted
                ? await connection.ResolveAsync(new SqlServerClaimCompletionRequest(_options.Tables.QualifiedOutboxTableName,
                    messageId, claimIdentity, transition, error, attempts, nextAttemptAt,
                    _options.Processing.FailureBackoffFloorSeconds, _options.Processing.LeaseTimeoutSeconds,
                    _options.Processing.CommandTimeoutSeconds, fencingToken, transaction, cancellationToken)).ConfigureAwait(false)
                : OutboxCompletionOutcome.FenceRefused;
            // A valid newer authority advances its durable fence even when the row-level decision refuses.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return outcome;
        }
        catch
        {
            await RollbackClaimOperationAsync(transaction, messageId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<bool> AcceptScopeAsync(SqlConnection connection, SqlTransaction transaction, long? fencingToken, CancellationToken cancellationToken)
    {
        if (!fencingToken.HasValue) { return true; }
        return await ReadScopeHighWaterAsync(connection, transaction, fencingToken.Value, cancellationToken).ConfigureAwait(false) == fencingToken.Value;
    }

    private Task<long> ReadScopeHighWaterAsync(SqlConnection connection, SqlTransaction transaction, long fencingToken, CancellationToken cancellationToken) =>
        connection.ResolveAsync(new SqlServerTransactionalFenceRequest(_options.Tables.QualifiedFenceTableName,
            _options.Tables.QualifiedOutboxTableName, fencingToken, _options.Processing.CommandTimeoutSeconds, transaction, cancellationToken));

    private async Task<OutboxMessageRow[]> ClaimRowsAsync(SqlConnection connection, int batchSize, long? fencingToken, CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        try
        {
            OutboxMessageRow[] rows = [];
            var highWater = fencingToken.HasValue
                ? await ReadScopeHighWaterAsync(connection, transaction, fencingToken.Value, cancellationToken).ConfigureAwait(false)
                : (long?)null;
            if (!fencingToken.HasValue || highWater == fencingToken.Value)
            {
                var claimed = await connection.ResolveAsync(new GetUnsentMessagesRequest(_options.Tables.QualifiedOutboxTableName,
                    batchSize, _options.Processing.CommandTimeoutSeconds, _options.Processing.LeaseTimeoutSeconds,
                    _options.ProcessorId, fencingToken, _options.Tables.QualifiedFenceTableName,
                    _options.Tables.QualifiedOutboxTableName, transaction, cancellationToken)).ConfigureAwait(false);
                rows = claimed.ToArray();
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (fencingToken.HasValue && highWater is { } refusedHighWater && refusedHighWater > fencingToken.Value)
            {
                ReportClaimFenceRefusal(fencingToken.Value, refusedHighWater);
            }
            return rows;
        }
        catch
        {
            await RollbackClaimOperationAsync(transaction, "claim-batch").ConfigureAwait(false);
            throw;
        }
    }

    private async Task RollbackClaimOperationAsync(SqlTransaction transaction, string operationId)
    {
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(_options.Processing.CommandTimeoutSeconds, 1, 30)));
            await transaction.RollbackAsync(cleanup.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Outbox rollback cleanup failed for {OperationId}; preserving the original operation failure.", operationId);
        }
    }
}
