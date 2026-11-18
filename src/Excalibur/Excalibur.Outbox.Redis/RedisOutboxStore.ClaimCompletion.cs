// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Redis;

public sealed partial class RedisOutboxStore
{
    // One Redis action decides existence, terminal state, exact live acquisition and the mutation.
    // No extra outcome field is persisted and no follow-up read attempts to reconstruct the result.
    private const string ClaimCompletionLuaScript = """
        local key = KEYS[1]
        local stagedIdx = KEYS[2]
        local failedIdx = KEYS[3]
        local scheduledIdx = KEYS[4]
        local leasedIdx = KEYS[5]
        local sentIdx = KEYS[6]
        local id = ARGV[1]
        local claim = ARGV[2]
        local operation = tonumber(ARGV[3])
        local reason = ARGV[4]
        local count = tonumber(ARGV[5])
        local delayMs = tonumber(ARGV[6])
        local ttl = tonumber(ARGV[7])
        local sentStatus = ARGV[8]
        local deadStatus = ARGV[9]
        local targetStatus = ARGV[10]
        local sentAt = ARGV[11]
        if redis.call('EXISTS', key) == 0 then return 'NOT_FOUND' end
        local status = redis.call('HGET', key, 'Status')
        if status == sentStatus or status == deadStatus then return 'TERMINAL' end
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        local owner = redis.call('HGET', key, 'LeasedBy')
        local expires = redis.call('ZSCORE', leasedIdx, id)
        if not owner or owner == '' or owner ~= claim or not expires or tonumber(expires) <= now then
            return 'CLAIM_LOST'
        end
        if operation == 0 then
            redis.call('HSET', key, 'Status', sentStatus, 'SentAt', sentAt)
            if ttl and ttl > 0 then redis.call('EXPIRE', key, ttl) end
            redis.call('ZADD', sentIdx, tonumber(sentAt), id)
        elseif operation == 1 or operation == 2 then
            local prior = tonumber(redis.call('HGET', key, 'RetryCount')) or 0
            count = math.max(count, prior)
            local nextAt = now + delayMs
            redis.call('HSET', key, 'Status', targetStatus, 'LastError', reason,
                'RetryCount', count, 'LastAttemptAt', now, 'NextAttemptAt', nextAt)
            redis.call('ZADD', scheduledIdx, nextAt, id)
            if operation == 1 then redis.call('ZADD', failedIdx, count * 1000000000000 + now, id) end
        elseif operation == 3 then
            local prior = tonumber(redis.call('HGET', key, 'RetryCount')) or 0
            redis.call('HSET', key, 'Status', deadStatus, 'LastError', reason, 'RetryCount', math.max(count, prior))
        else
            return 'UNKNOWN'
        end
        redis.call('HDEL', key, 'LeasedAt', 'LeasedBy')
        redis.call('ZREM', stagedIdx, id)
        redis.call('ZREM', leasedIdx, id)
        if operation ~= 1 then redis.call('ZREM', failedIdx, id) end
        if operation == 0 or operation == 3 then redis.call('ZREM', scheduledIdx, id) end
        return 'SUCCESS'
        """;

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

    /// <inheritdoc />
    public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount,
        string claimIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return CompleteClaimAsync(messageId, claimIdentity, 3, reason, attemptCount, null, cancellationToken);
    }

    private async ValueTask<OutboxCompletionOutcome> CompleteClaimAsync(string messageId, string claimIdentity, int operation,
        string? reason, int count, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureConnectedAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var floorMs = (long)TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds).TotalMilliseconds;
        var delayMs = nextAttemptAt is { } requested ? Math.Max(floorMs, (long)(requested - now).TotalMilliseconds) : floorMs;
        var targetStatus = operation == 2 ? OutboxStatus.Staged : OutboxStatus.Failed;
        var result = await _database!.ScriptEvaluateAsync(ClaimCompletionLuaScript,
            [GetMessageKey(messageId), GetStagedIndexKey(), GetFailedIndexKey(), GetScheduledIndexKey(), GetLeasedIndexKey(), GetSentIndexKey()],
            [messageId, claimIdentity, operation, reason ?? string.Empty, count, delayMs, _options.SentMessageTtlSeconds,
                (int)OutboxStatus.Sent, (int)OutboxStatus.DeadLettered, (int)targetStatus, now.ToUnixTimeMilliseconds()]).ConfigureAwait(false);
        var outcome = result.ToString() switch
        {
            "SUCCESS" => OutboxCompletionOutcome.Applied,
            "NOT_FOUND" => OutboxCompletionOutcome.MessageNotFound,
            "TERMINAL" => OutboxCompletionOutcome.AlreadyTerminal,
            "CLAIM_LOST" => OutboxCompletionOutcome.ClaimLost,
            _ => OutboxCompletionOutcome.Unknown,
        };
        if (outcome is OutboxCompletionOutcome.Applied)
        {
            if (operation == 0) { LogMessageSent(messageId); }
            else if (operation is 1 or 2) { LogMessageFailed(messageId, reason!, count); }
        }
        return outcome;
    }
}
