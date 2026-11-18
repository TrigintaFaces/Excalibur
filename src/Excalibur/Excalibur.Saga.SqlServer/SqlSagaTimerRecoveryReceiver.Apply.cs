// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryState;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlSagaTimerRecoveryReceiver
{
    private sealed record ValidatedRecoveryFrame(SagaTimerIntent[] Intents, IReadOnlyCollection<string> Allocated, byte[] Digest);

    private static ValidatedRecoveryFrame ValidateRecoveryFrame(SagaTimerRecoveryJournal journal)
    {
        // Decode the SAME Core implementation through the existing provider friend, before any mutation.
        var intents = SagaTimerRecoveryCodec.Decode(journal);
        var allocated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intent in intents)
        {
            if (intent.Action == SagaTimerAction.Schedule && !allocated.Add(intent.TimeoutId))
            { throw new InvalidDataException("A batch cannot allocate one timer ID twice."); }
        }
        var digest = journal.GetIntentDigest();
        return new ValidatedRecoveryFrame(intents, allocated, digest);
    }

    private static async Task<SagaTimerRecoveryApplyResult> ApplyValidatedFrameAsync(SqlConnection connection, SqlTransaction transaction,
        string table, SagaTimerRecoveryJournal journal, ValidatedRecoveryFrame frame, CancellationToken cancellationToken)
    {
        var scope = await LockRecoveryScopeForTargetAsync(connection, transaction, journal.GetDomain(), table, create: false, cancellationToken).ConfigureAwait(false);
        var previous = await ReadExistingRecoveryOutcomeAsync(connection, transaction, journal, scope, frame.Intents, frame.Digest, cancellationToken).ConfigureAwait(false);
        if (previous is not null) { return previous; }
        var cutoffs = await ReadExternalRecoveryCutoffsAsync(connection, transaction, scope, frame.Allocated, cancellationToken).ConfigureAwait(false);
        var revision = checked(scope.Revision + 1);
        var results = await ApplyOriginalRecoveryIntentsAsync(connection, transaction, table, scope, journal, frame.Intents,
            revision, cutoffs, cancellationToken).ConfigureAwait(false);
        return await PersistRecoveryReceiptAsync(connection, transaction, scope, journal, frame.Digest, revision, results, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SagaTimerRecoveryApplyResult?> ReadExistingRecoveryOutcomeAsync(SqlConnection connection, SqlTransaction transaction,
        SagaTimerRecoveryJournal journal, RecoveryScopeRow scope, SagaTimerIntent[] intents, byte[] digest, CancellationToken cancellationToken)
    {
        if (scope.Incarnation == journal.IncarnationId && scope.Sequence == journal.Sequence)
        {
            if (scope.BatchId != journal.BatchId || scope.ExpectedFence != journal.ExpectedFence
                || scope.ObservedRevision != journal.ObservedRevision || !string.Equals(scope.StateType, journal.Scope.StateType, StringComparison.Ordinal)
                || !scope.Digest.AsSpan().SequenceEqual(digest))
            { throw new InvalidOperationException("Timer journal identity conflicts with the retained exact receipt."); }
            var previous = (await connection.ResolveAsync(new ReadRecoveryResultsRequest(scope.Id, transaction, cancellationToken)).ConfigureAwait(false)).ToArray();
            if (previous.Length != intents.Length) { throw new InvalidDataException("The retained timer receipt is incomplete."); }
            for (var ordinal = 0; ordinal < intents.Length; ordinal++)
            {
                var expected = intents[ordinal].Action switch
                {
                    SagaTimerAction.Schedule => SagaPreparedTimerAction.Schedule,
                    SagaTimerAction.Cancel => SagaPreparedTimerAction.Cancel,
                    _ => SagaPreparedTimerAction.CancelAll,
                };
                if (previous[ordinal].Action != expected
                    || !string.Equals(previous[ordinal].TimeoutId, intents[ordinal].TimeoutId, StringComparison.Ordinal))
                { throw new InvalidDataException("The retained timer receipt differs from its original intents."); }
            }
            var replay = new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.AlreadyApplied, scope.Fence(), previous);
            return replay;
        }
        if (scope.Token != journal.ExpectedFence)
        {
            var retired = await connection.ResolveAsync(new ReadRetiredRecoveryTokenRequest(scope.Id, journal.ExpectedFence, transaction, cancellationToken)).ConfigureAwait(false);
            if (!retired.Any()) { throw new InvalidOperationException("The original timer journal fence is unknown to this receiver domain."); }
            return new(SagaTimerRecoveryApplyOutcome.Superseded, scope.Fence(), []);
        }
        if (scope.Incarnation == journal.IncarnationId && journal.Sequence < scope.Sequence)
        {
            return new(SagaTimerRecoveryApplyOutcome.Superseded, scope.Fence(), []);
        }
        if (scope.Incarnation == journal.IncarnationId)
        {
            if (!string.Equals(scope.StateType, journal.Scope.StateType, StringComparison.Ordinal) || journal.Sequence != checked(scope.Sequence + 1))
            { throw new InvalidOperationException("Timer journal state type or sequence is not the next original batch."); }
        }
        else if (journal.Sequence != 1)
        { throw new InvalidOperationException("A fresh state incarnation starts with its first original timer batch."); }
        if (journal.ObservedRevision > scope.Revision) { throw new InvalidDataException("A journal cannot claim a future receiver observation."); }

        return null;
    }

    private static async Task<Dictionary<string, RecoveryCutoff>> ReadExternalRecoveryCutoffsAsync(SqlConnection connection, SqlTransaction transaction,
        RecoveryScopeRow scope, IReadOnlyCollection<string> allocated, CancellationToken cancellationToken)
    {
        // Freeze external cutoffs before the first in-batch mutation. Otherwise CancelAll,Schedule(U)
        // would mistake this batch's own cancellation for a later external cancellation.
        var cutoffs = new Dictionary<string, RecoveryCutoff>(StringComparer.Ordinal);
        foreach (var id in allocated)
        {
            var original = (await connection.ResolveAsync(new ReadRecoveryTimerRequest(scope.Id, id, transaction, cancellationToken)).ConfigureAwait(false)).SingleOrDefault();
            var cutoff = new RecoveryCutoff(scope.CancelAllThrough, scope.CancelAllRevision);
            if (original is not null && original.CancelThrough > cutoff.Through)
            { cutoff = new(original.CancelThrough, original.CancelRevision); }
            cutoffs.Add(id, cutoff);
        }
        return cutoffs;
    }

    private static async Task<IReadOnlyList<SagaTimerRecoveryIntentResult>> ApplyOriginalRecoveryIntentsAsync(SqlConnection connection, SqlTransaction transaction,
        string table, RecoveryScopeRow scope, SagaTimerRecoveryJournal journal, SagaTimerIntent[] intents, long revision,
        IReadOnlyDictionary<string, RecoveryCutoff> cutoffs, CancellationToken cancellationToken)
    {
        var results = new List<SagaTimerRecoveryIntentResult>(intents.Length);
        for (var ordinal = 0; ordinal < intents.Length; ordinal++)
        {
            var intent = intents[ordinal];
            var cutoff = intent.Action == SagaTimerAction.Schedule ? cutoffs[intent.TimeoutId] : new RecoveryCutoff(-1, 0);
            var rows = await connection.ResolveAsync(new ApplyRecoveryIntentRequest(table, scope,
                journal, intent, revision, cutoff, transaction, cancellationToken)).ConfigureAwait(false);
            var actual = rows.Single();
            var action = intent.Action switch
            {
                SagaTimerAction.Schedule => SagaPreparedTimerAction.Schedule,
                SagaTimerAction.Cancel => SagaPreparedTimerAction.Cancel,
                _ => SagaPreparedTimerAction.CancelAll,
            };
            results.Add(new(ordinal, action, intent.TimeoutId, actual.Outcome, actual.Revision));
        }
        return results;
    }

    private static async Task<SagaTimerRecoveryApplyResult> PersistRecoveryReceiptAsync(SqlConnection connection, SqlTransaction transaction,
        RecoveryScopeRow scope, SagaTimerRecoveryJournal journal, byte[] digest, long revision,
        IReadOnlyList<SagaTimerRecoveryIntentResult> results, CancellationToken cancellationToken)
    {
        var nextToken = scope.Token;
        if (scope.Incarnation != journal.IncarnationId)
        {
            do
            {
                nextToken = Guid.NewGuid();
            }
            while (nextToken == Guid.Empty || nextToken == scope.Token
                || (await connection.ResolveAsync(new ReadRetiredRecoveryTokenRequest(scope.Id, nextToken, transaction, cancellationToken)).ConfigureAwait(false)).Any());
            _ = await connection.ResolveAsync(new RetireRecoveryTokenRequest(scope.Id, scope.Token, transaction, cancellationToken)).ConfigureAwait(false);
        }
        var nextFence = new SagaTimerRecoveryFence(nextToken, journal.IncarnationId, journal.Sequence, revision);
        var applied = new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.Applied, nextFence, results);
        _ = await connection.ResolveAsync(new WriteRecoveryReceiptRequest(scope.Id, nextToken, journal, digest, revision, transaction, cancellationToken)).ConfigureAwait(false);
        foreach (var result in results)
        { _ = await connection.ResolveAsync(new WriteRecoveryResultRequest(scope.Id, result, transaction, cancellationToken)).ConfigureAwait(false); }
        // Any failed/uncertain acknowledgement throws and leaves the original caller journal intact.
        // No provider exception is turned into an Applied/AlreadyApplied certificate.
        return applied;
    }
}
