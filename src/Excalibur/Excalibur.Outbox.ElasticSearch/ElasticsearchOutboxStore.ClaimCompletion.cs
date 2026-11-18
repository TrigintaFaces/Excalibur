// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.ElasticSearch;

public sealed partial class ElasticsearchOutboxStore
{
    private const string ClaimCompletionScript = """
        long now = System.currentTimeMillis();
        if (ctx._source.status == params.sentStatus || ctx._source.status == params.deadStatus) {
            ctx.op = 'noop'; return;
        }
        def expires = ctx._source.leaseExpiresAt;
        if (ctx._source.leasedBy == null || ctx._source.leasedBy != params.claim || expires == null
            || ZonedDateTime.parse(expires).toInstant().toEpochMilli() <= now) {
            ctx.op = 'noop'; return;
        }
        if (params.operation == 0) {
            ctx._source.status = params.sentStatus;
            ctx._source.sentAt = params.sentAt;
        } else {
            ctx._source.status = params.operation == 3 ? params.deadStatus : params.failedStatus;
            ctx._source.lastError = params.reason;
            def prior = ctx._source.retryCount;
            if (prior == null || params.count > prior) { ctx._source.retryCount = params.count; }
            ctx._source.lastAttemptAt = Instant.ofEpochMilli(now).toString();
            if (params.operation != 3) { ctx._source.nextAttemptAt = Instant.ofEpochMilli(now + params.delayMs).toString(); }
        }
        ctx._source.remove('leasedBy');
        ctx._source.remove('leaseExpiresAt');
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
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var floorMs = (long)_options.FailureBackoffFloorSeconds * 1000;
        var delayMs = nextAttemptAt is { } requested ? Math.Max(floorMs, (long)(requested - now).TotalMilliseconds) : floorMs;
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["claim"] = claimIdentity, ["operation"] = operation, ["reason"] = reason ?? string.Empty,
            ["count"] = count, ["delayMs"] = delayMs, ["sentAt"] = now.ToString("O", CultureInfo.InvariantCulture),
            ["sentStatus"] = (int)OutboxStatus.Sent, ["deadStatus"] = (int)OutboxStatus.DeadLettered,
            ["failedStatus"] = (int)OutboxStatus.Failed,
        };
        for (var attempt = 0; attempt < TerminalTransitionMaxRetries; attempt++)
        {
            var get = await _client.GetAsync<ElasticsearchOutboxDocument>(_options.IndexName, messageId, cancellationToken).ConfigureAwait(false);
            if (!get.IsValidResponse)
            {
                if (get.ApiCallDetails?.HttpStatusCode == 404) { return OutboxCompletionOutcome.MessageNotFound; }
                throw new InvalidOperationException($"Failed to read outbox message '{messageId}' for claim completion: {get.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
            }
            if (!get.Found) { return OutboxCompletionOutcome.MessageNotFound; }
            if (get.Source is null) { throw new InvalidOperationException($"Outbox message '{messageId}' has no source document."); }
            // Read-only refusals linearize at this snapshot. A mutation must still present its version
            // and exact acquisition to the node; a fresh GET is never used to invent caller authority.
            if (get.Source.Status is (int)OutboxStatus.Sent or (int)OutboxStatus.DeadLettered) { return OutboxCompletionOutcome.AlreadyTerminal; }
            if (!string.Equals(get.Source.LeasedBy, claimIdentity, StringComparison.Ordinal)) { return OutboxCompletionOutcome.ClaimLost; }
            if (get.SeqNo is null || get.PrimaryTerm is null) { return OutboxCompletionOutcome.Unknown; }
            var result = await _client.UpdateAsync<ElasticsearchOutboxDocument, ElasticsearchOutboxDocument>(
                _options.IndexName, messageId,
                u => u.Script(sc => sc.Source(ClaimCompletionScript).Params(parameters))
                    .IfSeqNo(get.SeqNo).IfPrimaryTerm(get.PrimaryTerm).Refresh(GetRefresh()), cancellationToken).ConfigureAwait(false);
            if (result.IsValidResponse)
            {
                // The CAS proves the preimage version did not change. A guarded noop therefore means
                // its lease expired at the node, not an outcome guessed from another actor's later row.
                return result.Result switch
                {
                    Result.Updated => OutboxCompletionOutcome.Applied,
                    Result.NoOp => OutboxCompletionOutcome.ClaimLost,
                    _ => OutboxCompletionOutcome.Unknown,
                };
            }
            if (result.ApiCallDetails?.HttpStatusCode == 409) { continue; }
            if (result.ApiCallDetails?.HttpStatusCode == 404) { return OutboxCompletionOutcome.MessageNotFound; }
            throw new InvalidOperationException($"Failed to complete outbox claim for '{messageId}': {result.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }
        return OutboxCompletionOutcome.Unknown;
    }
}
