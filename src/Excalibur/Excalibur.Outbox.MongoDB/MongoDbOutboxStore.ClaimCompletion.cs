// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Excalibur.Outbox.MongoDB;

public sealed partial class MongoDbOutboxStore
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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var terminal = new BsonDocument("$in", new BsonArray
        {
            "$status", new BsonArray { (int)OutboxStatus.Sent, (int)OutboxStatus.DeadLettered },
        });
        var leaseAt = new BsonDocument("$ifNull", new BsonArray { StoredInstant("leasedAt"), BsonNull.Value });
        var eligible = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$not", new BsonArray { terminal }),
            new BsonDocument("$eq", new BsonArray { "$leasedBy", new BsonDocument("$literal", claimIdentity) }),
            new BsonDocument("$ne", new BsonArray { leaseAt, BsonNull.Value }),
            new BsonDocument("$gt", new BsonArray { leaseAt,
                new BsonDocument("$subtract", new BsonArray { "$$NOW", (long)_options.LeaseTimeoutSeconds * 1000 }) }),
        });
        var changes = new BsonDocument { { "leasedAt", BsonNull.Value }, { "leasedBy", BsonNull.Value } };
        if (operation == 0)
        {
            changes.Add("status", (int)OutboxStatus.Sent);
            changes.Add("sentAt", new BsonDateTime(_timeProvider.GetUtcNow().UtcDateTime));
            changes.Add("lastError", BsonNull.Value);
        }
        else
        {
            changes.Add("status", (int)(operation == 3 ? OutboxStatus.DeadLettered : operation == 2 ? OutboxStatus.Staged : OutboxStatus.Failed));
            changes.Add("lastError", new BsonDocument("$literal", reason!));
            changes.Add("retryCount", new BsonDocument("$max", new BsonArray { "$retryCount", count }));
            if (operation != 3)
            {
                var floorMs = (long)_options.FailureBackoffFloorSeconds * 1000;
                var delayMs = nextAttemptAt is { } requested
                    ? Math.Max(floorMs, (long)(requested - _timeProvider.GetUtcNow()).TotalMilliseconds) : floorMs;
                changes.Add("lastAttemptAt", "$$NOW");
                changes.Add("nextAttemptAt", new BsonDocument("$add", new BsonArray { "$$NOW", delayMs }));
            }
        }
        // A single findAndModify uses one runtime-constant expression context for its conditional
        // update and BEFORE-image computed projection. The returned outcome is not persisted.
        var update = new PipelineUpdateDefinition<MongoDbOutboxDocument>(
            PipelineDefinition<MongoDbOutboxDocument, MongoDbOutboxDocument>.Create(
                new BsonDocument("$replaceWith", new BsonDocument("$cond", new BsonArray
                {
                    eligible, new BsonDocument("$mergeObjects", new BsonArray { "$$ROOT", changes }), "$$ROOT",
                }))));
        var outcomeExpression = new BsonDocument("$cond", new BsonArray
        {
            terminal, (int)OutboxCompletionOutcome.AlreadyTerminal,
            new BsonDocument("$cond", new BsonArray { eligible, (int)OutboxCompletionOutcome.Applied, (int)OutboxCompletionOutcome.ClaimLost }),
        });
        var projection = new BsonDocument { { "_id", 0 }, { "outcome", outcomeExpression } };
        var result = await _collection!.FindOneAndUpdateAsync(
            Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.Id, messageId), update,
            new FindOneAndUpdateOptions<MongoDbOutboxDocument, BsonDocument>
            {
                ReturnDocument = ReturnDocument.Before,
                Projection = new BsonDocumentProjectionDefinition<MongoDbOutboxDocument, BsonDocument>(projection, BsonDocumentSerializer.Instance),
            }, cancellationToken).ConfigureAwait(false);
        if (result is null) { return OutboxCompletionOutcome.MessageNotFound; }
        var outcome = result.GetValue("outcome", -1).AsInt32 switch
        {
            (int)OutboxCompletionOutcome.Applied => OutboxCompletionOutcome.Applied,
            (int)OutboxCompletionOutcome.AlreadyTerminal => OutboxCompletionOutcome.AlreadyTerminal,
            (int)OutboxCompletionOutcome.ClaimLost => OutboxCompletionOutcome.ClaimLost,
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
