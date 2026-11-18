// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.InMemory;

public sealed partial class InMemoryOutboxStore
{
    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken) =>
        ApplyClaimCompletion(messageId, claimIdentity, null, ClaimCompletion.Sent, null, 0, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority, CancellationToken cancellationToken) =>
        ApplyClaimCompletion(messageId, authority.ClaimIdentity, authority.FencingToken, ClaimCompletion.Sent, null, 0, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return ApplyClaimCompletion(messageId, claimIdentity, null, ClaimCompletion.Failed, errorMessage, retryCount, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount,
        DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        return ApplyClaimCompletion(messageId, claimIdentity, null, ClaimCompletion.Failed, errorMessage, retryCount, nextAttemptAt, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ApplyClaimCompletion(messageId, claimIdentity, null, ClaimCompletion.DeadLettered, reason, attemptCount, null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        OutboxWriteAuthority authority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ApplyClaimCompletion(messageId, authority.ClaimIdentity, authority.FencingToken, ClaimCompletion.DeadLettered,
            reason, attemptCount, null, cancellationToken);
    }

    // All strong routes share the existing claim lock. Outcome, guard, mutation and lease cleanup
    // are one transition, including expired leases which have not yet been claimed by a successor.
    private ValueTask<OutboxCompletionOutcome> ApplyClaimCompletion(string messageId, string claimIdentity, long? fencingToken,
        ClaimCompletion completion, string? reason, int count, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (fencingToken is { } token) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(token); }
        ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_claimLock)
        {
            if (fencingToken is { } fence)
            {
                if (fence < _fencingHighWaterMark) { return ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused); }
                _fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fence);
            }
            if (!_messages.TryGetValue(messageId, out var message)) { return ValueTask.FromResult(OutboxCompletionOutcome.MessageNotFound); }
            if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
            {
                return ValueTask.FromResult(OutboxCompletionOutcome.AlreadyTerminal);
            }
            if (!_leases.TryGetValue(messageId, out var lease)
                || !string.Equals(lease.LeasedBy, claimIdentity, StringComparison.Ordinal)
                || lease.LeasedAt <= _timeProvider.GetUtcNow() - TimeSpan.FromSeconds(_options.LeaseTimeoutSeconds))
            {
                return ValueTask.FromResult(OutboxCompletionOutcome.ClaimLost);
            }
            switch (completion)
            {
                case ClaimCompletion.Sent:
                    message.MarkSent();
                    message.SentAt = _timeProvider.GetUtcNow();
                    _ = _leases.TryRemove(messageId, out _);
                    _ = _nextAttempt.TryRemove(messageId, out _);
                    break;
                case ClaimCompletion.Failed:
                    ApplyFailureUnderClaimLock(messageId, message, reason!, count, nextAttemptAt);
                    break;
                case ClaimCompletion.DeadLettered:
                    message.Status = OutboxStatus.DeadLettered;
                    message.RetryCount = Math.Max(message.RetryCount, count);
                    message.LastError = reason;
                    _ = _leases.TryRemove(messageId, out _);
                    _ = _nextAttempt.TryRemove(messageId, out _);
                    break;
            }
        }
        if (completion is ClaimCompletion.Sent) { LogMessageSent(messageId); }
        return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
    }

    private enum ClaimCompletion { Sent, Failed, DeadLettered }
}
