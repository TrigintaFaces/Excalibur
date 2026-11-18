// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Oracle.ManagedDataAccess.Client;

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Data.Validation;
using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleSagaTimerRecovery : ISagaTimerRecoveryStore
{
    private readonly Func<OracleConnection> _connectionFactory;
    private readonly OracleSagaTimeoutStoreOptions _options;
    private readonly ITenantContext _tenantContext;

    internal OracleSagaTimerRecovery(Func<OracleConnection> connectionFactory,
        OracleSagaTimeoutStoreOptions options, ITenantContext tenantContext)
    {
        _connectionFactory = connectionFactory;
        _options = options;
        _tenantContext = tenantContext;
    }

    private KeyedTenantPartition CurrentPartition() => KeyedTenantPartition.FromContext(_tenantContext);

    internal RecoveryTargetBinding CaptureRecoveryTarget()
    {
        var owner = _options.SchemaName;
        var table = _options.TableName;
        SqlIdentifierValidator.ThrowIfInvalid(owner, nameof(_options.SchemaName));
        SqlIdentifierValidator.ThrowIfInvalid(table, nameof(_options.TableName));
        return new(owner.ToUpperInvariant(), table.ToUpperInvariant());
    }
    /// <inheritdoc />
    async ValueTask<SagaTimerRecoveryFence> ISagaTimerRecoveryStore.ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(domain.TenantId);
        var target = CaptureRecoveryTarget();
        return await RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var scope = await LockRecoveryScopeAsync(connection, transaction, domain, target, create: true, cancellationToken).ConfigureAwait(false);
            return scope.Fence();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    async ValueTask<SagaTimerRecoveryApplyResult> ISagaTimerRecoveryStore.ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(journal.Scope.TenantId);
        var target = CaptureRecoveryTarget();
        // Decode the SAME Core implementation through the existing provider friend, before any mutation.
        var intents = DecodeRecoveryIntents(journal);
        var digest = journal.GetIntentDigest();
        return await RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var scope = await LockRecoveryScopeAsync(connection, transaction, journal.GetDomain(), target, create: false, cancellationToken).ConfigureAwait(false);
            if (scope.Incarnation == journal.IncarnationId && scope.Sequence == journal.Sequence)
            {
                return await ReadExactReplayAsync(connection, transaction, target, scope, journal, intents, digest, cancellationToken).ConfigureAwait(false);
            }
            if (scope.Token != journal.ExpectedFence)
            {
                var retired = await ReadRetiredRecoveryTokenAsync(connection, transaction, target, scope.Id, journal.ExpectedFence, cancellationToken).ConfigureAwait(false);
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

            // Freeze external cutoffs before the first in-batch mutation. Otherwise CancelAll,Schedule(U)
            // would mistake this batch's own cancellation for a later external cancellation.
            var cutoffs = new Dictionary<string, RecoveryCutoff>(StringComparer.Ordinal);
            foreach (var allocatedIntent in intents)
            {
                if (allocatedIntent.Action != SagaTimerAction.Schedule) { continue; }
                var id = allocatedIntent.TimeoutId;
                var original = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, id, cancellationToken).ConfigureAwait(false);
                var cutoff = new RecoveryCutoff(scope.CancelAllThrough, scope.CancelAllRevision);
                if (original is not null && original.CancelThrough > cutoff.Through)
                { cutoff = new(original.CancelThrough, original.CancelRevision); }
                cutoffs.Add(id, cutoff);
            }
            var revision = checked(scope.Revision + 1);
            var results = new List<SagaTimerRecoveryIntentResult>(intents.Length);
            for (var ordinal = 0; ordinal < intents.Length; ordinal++)
            {
                var intent = intents[ordinal];
                var cutoff = intent.Action == SagaTimerAction.Schedule ? cutoffs[intent.TimeoutId] : new RecoveryCutoff(-1, 0);
                var actual = await ApplyRecoveryIntentAsync(connection, transaction, target, scope,
                    journal, intent, revision, cutoff, cancellationToken).ConfigureAwait(false);
                var action = intent.Action switch
                {
                    SagaTimerAction.Schedule => SagaPreparedTimerAction.Schedule,
                    SagaTimerAction.Cancel => SagaPreparedTimerAction.Cancel,
                    _ => SagaPreparedTimerAction.CancelAll,
                };
                results.Add(new(ordinal, action, intent.TimeoutId, actual.Outcome, actual.Revision));
            }
            var nextToken = scope.Token;
            if (scope.Incarnation != journal.IncarnationId)
            {
                do
                {
                    nextToken = Guid.NewGuid();
                }
                while (nextToken == Guid.Empty || nextToken == scope.Token
                    || (await ReadRetiredRecoveryTokenAsync(connection, transaction, target, scope.Id, nextToken, cancellationToken).ConfigureAwait(false)).Any());
                await RetireRecoveryTokenAsync(connection, transaction, target, scope.Id, scope.Token, cancellationToken).ConfigureAwait(false);
            }
            var nextFence = new SagaTimerRecoveryFence(nextToken, journal.IncarnationId, journal.Sequence, revision);
            var applied = new SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome.Applied, nextFence, results);
            await WriteRecoveryReceiptAsync(connection, transaction, target, scope.Id, nextToken, journal, digest, revision, cancellationToken).ConfigureAwait(false);
            foreach (var result in results)
            { await WriteRecoveryResultAsync(connection, transaction, target, scope.Id, result, cancellationToken).ConfigureAwait(false); }
            // Any failed/uncertain acknowledgement throws and leaves the original caller journal intact.
            // No provider exception is turned into an Applied/AlreadyApplied certificate.
            return applied;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    async ValueTask<bool> ISagaTimerRecoveryStore.IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(delivery.Scope.TenantId);
        var target = CaptureRecoveryTarget();
        return await RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var scope = await LockRecoveryScopeAsync(connection, transaction,
                new(delivery.Scope.TenantId, delivery.Scope.SagaId), target, create: false, cancellationToken).ConfigureAwait(false);
            var timer = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, delivery.TimeoutId, cancellationToken).ConfigureAwait(false);
            if (timer is null || !timer.Tracked) { throw new InvalidOperationException("Original tracked timer authority is missing."); }
            var through = Math.Max(scope.CancelAllThrough, timer.CancelThrough);
            var current = scope.Incarnation == delivery.IncarnationId && timer.Incarnation == delivery.IncarnationId
                && timer.Sequence == delivery.Generation && string.Equals(timer.StateType, delivery.Scope.StateType, StringComparison.Ordinal)
                && !timer.Cancelled && timer.CreatedRevision > through;
            return current;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static SagaTimerIntent[] DecodeRecoveryIntents(SagaTimerRecoveryJournal journal)
    {
        var intents = SagaTimerRecoveryCodec.Decode(journal);
        var allocated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intent in intents)
        {
            if (intent.Action == SagaTimerAction.Schedule && !allocated.Add(intent.TimeoutId))
            { throw new InvalidDataException("A batch cannot allocate one timer ID twice."); }
        }
        return intents;
    }

    private static async Task<SagaTimerRecoveryApplyResult> ReadExactReplayAsync(OracleConnection connection,
        OracleTransaction transaction, RecoveryTargetBinding target, RecoveryScopeRow scope,
        SagaTimerRecoveryJournal journal, SagaTimerIntent[] intents, byte[] digest, CancellationToken cancellationToken)
    {
        if (scope.BatchId != journal.BatchId || scope.ExpectedFence != journal.ExpectedFence
            || scope.ObservedRevision != journal.ObservedRevision || !string.Equals(scope.StateType, journal.Scope.StateType, StringComparison.Ordinal)
            || !scope.Digest.AsSpan().SequenceEqual(digest))
        { throw new InvalidOperationException("Timer journal identity conflicts with the retained exact receipt."); }
        var previous = (await ReadRecoveryResultsAsync(connection, transaction, target, scope.Id, cancellationToken).ConfigureAwait(false)).ToArray();
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

    private async Task<T> RunRecoveryTransactionAsync<T>(Func<OracleConnection, OracleTransaction, Task<T>> operation, CancellationToken cancellationToken)
    {
        OracleConnection? connection = null;
        OracleTransaction? transaction = null;
        Exception? primary = null;
        var committed = false;
        T result = default!;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = _connectionFactory();
            if (candidate.State != System.Data.ConnectionState.Closed)
            { throw new InvalidOperationException("The Oracle timer connection factory must supply a new closed connection; a live handout is not owned."); }
            connection = candidate;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = (OracleTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            result = await operation(connection, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            committed = true;
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (transaction is not null)
            {
                if (!committed)
                {
                    try
                    {
                        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await transaction.RollbackAsync(budget.Token).ConfigureAwait(false);
                    }
                    catch (Exception cleanup) { PreserveRecoveryCleanup(ref primary, cleanup); }
                }
                try { await transaction.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { PreserveRecoveryCleanup(ref primary, cleanup); }
            }
            if (connection is not null)
            {
                try { await connection.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { PreserveRecoveryCleanup(ref primary, cleanup); }
            }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        return result;
    }

    private static void PreserveRecoveryCleanup(ref Exception? primary, Exception cleanup)
    {
        if (primary is null) { primary = cleanup; return; }
        try { primary.Data["OracleTimerRecoveryCleanup:" + Guid.NewGuid().ToString("N")] = cleanup; }
        catch (Exception) { /* A cleanup observer cannot replace the original operation failure. */ }
    }

    private void RequireRecoveryTenant(string tenant)
    {
        if (!string.Equals(tenant, CurrentPartition().TenantId, StringComparison.Ordinal))
        { throw new InvalidOperationException("Timer recovery tenant differs from the current receiver authority."); }
    }

    private async Task<RecoveryScopeRow> LockRecoveryScopeAsync(OracleConnection connection, OracleTransaction transaction,
        SagaTimerRecoveryDomain domain, RecoveryTargetBinding target, bool create, CancellationToken cancellationToken)
    {
        var selected = await RequireRecoveryTargetAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
        var scope = await ReadRecoveryScopeAsync(connection, transaction, target, selected, domain, cancellationToken).ConfigureAwait(false);
        if (scope is not null) { return scope; }
        if (!create) { throw new InvalidOperationException("A persisted timer journal has no retained receiver authority."); }
        var candidate = new RecoveryScopeRow(Guid.NewGuid(), selected, domain.TenantId, domain.SagaId,
            Guid.NewGuid(), Guid.Empty, 0, 0, null, null, null, null, [], -1, 0);
        try { await CreateRecoveryScopeAsync(connection, transaction, target, candidate, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is OracleException { Number: 1 }
            or OperationFailedException { InnerException: OracleException { Number: 1 } })
        {
            // Oracle has no absent-key range lock. The unique domain insert serializes first observers;
            // only duplicate-key is recoverable, and the winner must be re-read under FOR UPDATE.
            return await ReadRecoveryScopeAsync(connection, transaction, target, selected, domain, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A competing recovery scope insert did not establish this exact domain.");
        }
        return candidate;
    }

    private readonly record struct RecoveryCutoff(long Through, long Revision);
    private sealed record RecoveryIntentDisposition(SagaTimerRecoveryIntentOutcome Outcome, long Revision);
    internal sealed record RecoveryScopeRow(Guid Id, Guid Target, string Tenant, Guid SagaId, Guid Token,
        Guid Incarnation, long Sequence, long Revision, string? StateType, Guid? BatchId, Guid? ExpectedFence,
        long? ObservedRevision, byte[] Digest, long CancelAllThrough, long CancelAllRevision)
    {
        internal SagaTimerRecoveryFence Fence() => new(Token, Incarnation, Sequence, Revision);
    }
    internal sealed record RecoveryTimerRow(bool Tracked, Guid? Incarnation, long? Sequence, string? StateType,
        long CreatedRevision, bool Cancelled, long CancelThrough, long CancelRevision);
    // Failure disposition reuses the existing owned connection/transaction/primary-cleanup boundary.
    internal Task<T> RunFailureTransactionAsync<T>(Func<OracleConnection, OracleTransaction, Task<T>> operation, CancellationToken cancellationToken) =>
        RunRecoveryTransactionAsync(operation, cancellationToken);

    internal async Task ValidateFailureTargetAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, CancellationToken cancellationToken)
    {
        if (await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false))
        { _ = await RequireRecoveryTargetAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false); }
    }

    internal async Task<RecoveryScopeRow?> LockFailureDomainAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, string tenant, string sagaId, CancellationToken cancellationToken)
    {
        RequireRecoveryTenant(tenant);
        if (!await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false)) { return null; }
        if (!Guid.TryParse(sagaId, out var id) || id == Guid.Empty) { return null; }
        return await LockRecoveryScopeAsync(connection, transaction, new(tenant, id), target, create: true, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool?> IsFailureOwnedCurrentAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, RecoveryScopeRow? scope, SagaTimeoutDeliveryIdentity? delivery, CancellationToken cancellationToken)
    {
        if (delivery is null) { return true; }
        if (scope is null || scope.Tenant != delivery.Scope.TenantId || scope.SagaId != delivery.Scope.SagaId) { return null; }
        if (scope.Incarnation != Guid.Empty && scope.Incarnation != delivery.IncarnationId) { return false; }
        var timer = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, delivery.TimeoutId, cancellationToken).ConfigureAwait(false);
        if (timer is null || !timer.Tracked) { return null; }
        if (timer.Cancelled || timer.CreatedRevision <= Math.Max(scope.CancelAllThrough, timer.CancelThrough)) { return false; }
        if (timer.Incarnation is { } incarnation && incarnation != delivery.IncarnationId) { return false; }
        if (timer.Sequence > delivery.Generation) { return false; }
        if (scope.Incarnation != delivery.IncarnationId || timer.Incarnation != delivery.IncarnationId
            || timer.Sequence != delivery.Generation || timer.StateType != delivery.Scope.StateType) { return null; }
        return true;
    }

}
