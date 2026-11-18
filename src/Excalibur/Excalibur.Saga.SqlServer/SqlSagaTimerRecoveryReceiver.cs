// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryState;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlSagaTimerRecoveryReceiver(
    Func<SqlConnection> connectionFactory, Func<string> getTenant, Func<string> getTarget)
{
    private readonly Func<SqlConnection> _connectionFactory = connectionFactory;
    private readonly Func<string> _getTenant = getTenant;
    private readonly Func<string> _getTarget = getTarget;

    /// <inheritdoc />
    internal async ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(domain.TenantId);
        var table = _getTarget();
        return await RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var scope = await LockRecoveryScopeForTargetAsync(connection, transaction, domain, table, create: true, cancellationToken).ConfigureAwait(false);
            return scope.Fence();
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(journal.Scope.TenantId);
        var table = _getTarget();
        var frame = ValidateRecoveryFrame(journal);
        return await RunRecoveryTransactionAsync((connection, transaction) =>
            ApplyValidatedFrameAsync(connection, transaction, table, journal, frame, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    internal async ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        cancellationToken.ThrowIfCancellationRequested();
        RequireRecoveryTenant(delivery.Scope.TenantId);
        var table = _getTarget();
        return await RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var scope = await LockRecoveryScopeForTargetAsync(connection, transaction,
                new(delivery.Scope.TenantId, delivery.Scope.SagaId), table, create: false, cancellationToken).ConfigureAwait(false);
            var timer = (await connection.ResolveAsync(new ReadRecoveryTimerRequest(scope.Id, delivery.TimeoutId, transaction, cancellationToken)).ConfigureAwait(false)).SingleOrDefault();
            if (timer is null || !timer.Tracked) { throw new InvalidOperationException("Original tracked timer authority is missing."); }
            var through = Math.Max(scope.CancelAllThrough, timer.CancelThrough);
            var current = scope.Incarnation == delivery.IncarnationId && timer.Incarnation == delivery.IncarnationId
                && timer.Sequence == delivery.Generation && string.Equals(timer.StateType, delivery.Scope.StateType, StringComparison.Ordinal)
                && !timer.Cancelled && timer.CreatedRevision > through;
            return current;
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<T> RunRecoveryTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> operation, CancellationToken token, SqlConnection? borrowedConnection = null)
    {
        SqlConnection? connection = null;
        SqlTransaction? transaction = null;
        Exception? primary = null;
        var committed = false;
        T result = default!;
        try
        {
            connection = borrowedConnection ?? _connectionFactory();
            if (borrowedConnection is null) { await connection.OpenAsync(token).ConfigureAwait(false); }
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
            result = await operation(connection, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
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
                    catch (Exception cleanup) { SqlServerSagaTimeoutStore.PreserveTimerRecoveryFailure(ref primary, cleanup, "RecoveryRollback"); }
                }
                try { await transaction.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { SqlServerSagaTimeoutStore.PreserveTimerRecoveryFailure(ref primary, cleanup, "RecoveryTransactionDispose"); }
            }
            if (connection is not null && borrowedConnection is null)
            {
                try { await connection.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { SqlServerSagaTimeoutStore.PreserveTimerRecoveryFailure(ref primary, cleanup, "RecoveryConnectionDispose"); }
            }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        return result;
    }

    private void RequireRecoveryTenant(string tenant)
    {
        if (!string.Equals(tenant, _getTenant(), StringComparison.Ordinal))
        { throw new InvalidOperationException("Timer recovery tenant differs from the current receiver authority."); }
    }

    private static async Task<RecoveryScopeRow> LockRecoveryScopeForTargetAsync(SqlConnection connection, SqlTransaction transaction,
        SagaTimerRecoveryDomain domain, string table, bool create, CancellationToken token)
    {
        var target = (await connection.ResolveAsync(new RecoveryTargetRequest(table, transaction, token)).ConfigureAwait(false)).Single();
        _ = await connection.ResolveAsync(new LockAtomicRecoveryDomainRequest(target, domain, transaction, token)).ConfigureAwait(false);
        var scope = (await connection.ResolveAsync(new ReadRecoveryScopeRequest(target, domain, transaction, token)).ConfigureAwait(false)).SingleOrDefault();
        if (scope is not null)
        {
            if (!string.Equals(scope.Tenant, domain.TenantId, StringComparison.Ordinal))
            { throw new InvalidOperationException("Exact receiver tenant differs from its retained domain key."); }
            return scope;
        }
        if (!create) { throw new InvalidOperationException("A persisted timer journal has no retained receiver authority."); }
        scope = new(Guid.NewGuid(), target, domain.TenantId, domain.SagaId, Guid.NewGuid(), Guid.Empty, 0, 0,
            null, null, null, null, [], -1, 0);
        _ = await connection.ResolveAsync(new CreateRecoveryScopeRequest(scope, transaction, token)).ConfigureAwait(false);
        return scope;
    }


}
