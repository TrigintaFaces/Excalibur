// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

/// <summary>Owns the primary SQL connection and transaction for one durable saga transition.</summary>
/// <remarks>
/// This implementation targets the matching reviewed default-table schema. Registration must fail closed
/// for an unsupported binding; it must never be substituted for ordinary state-only Save semantics.
/// </remarks>
internal sealed partial class SqlServerSagaAtomicTransitionStore(string connectionString, ITenantContext tenantContext)
    : ISagaAtomicTransitionStore
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateTenant(identity.TenantId);
        return await ReadOwnedAsync<SagaTransitionCommitResult>(async (connection,transaction,token)=>
        {
            var scope = await LockScopeAsync(connection, transaction, identity.TenantId, identity.SagaId, token).ConfigureAwait(false);
            if (scope is null) { return new(SagaTransitionCommitOutcome.NotFound, null); }
            if (!string.Equals(scope.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(scope.SagaType, identity.SagaType, StringComparison.Ordinal))
            {
                return new(SagaTransitionCommitOutcome.IdentityConflict, null);
            }
            var receipt = await FindDeliveryAsync(connection, transaction, scope.ScopeId, identity.IncarnationId,
                identity.DeliveryId, token).ConfigureAwait(false);
            return receipt is null ? new(SagaTransitionCommitOutcome.NotFound, null)
                : new(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, receipt.CommittedVersion);
        },cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ValidateTenant(transition.Identity.TenantId);
        cancellationToken.ThrowIfCancellationRequested();
        SqlTransaction? transaction = null;
        Exception? primary = null;
        SagaTransitionCommitResult? result = null;
        var commitAttempted = false;
        var commitAcknowledged = false;
        try
        {
            // Dispose the owned connection only after transaction cleanup has preserved the primary failure.
            await using var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ValidateDurabilityAsync(connection, cancellationToken).ConfigureAwait(false);
                transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var identity = transition.Identity;
                var scope = await LockScopeAsync(connection, transaction, identity.TenantId, identity.SagaId, cancellationToken).ConfigureAwait(false);
                if (scope is not null && !ScopeMatches(scope, identity))
                {
                    result = new(SagaTransitionCommitOutcome.IdentityConflict, null);
                }
                else
                {
                    if (scope is null)
                    {
                        if (transition.ExpectedVersion != 0 || transition.FirstSequence != 0 || transition.Generation != 0)
                        {
                            result = new(SagaTransitionCommitOutcome.VersionConflict, null);
                        }
                        else
                        {
                            scope = await CreateScopeAsync(connection, transaction, identity, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    if (result is null && scope is not null)
                    {
                        result = await ResolveExistingAsync(connection, transaction, scope, transition, cancellationToken).ConfigureAwait(false);
                        if (result is null)
                        {
                            transition.ValidateTimerParticipant(transaction, SqlSagaTimerBinding.ForWriter(connection, "dbo", "SagaTimeouts"));
                            // ResolveExisting above establishes historical exact receipt first. Only a NEW
                            // write must revalidate manual non-ownership under this same scope lock.
                            if (transition.OwnedTimeout is { } originalTimeout
                                && !await IsOriginalTimeoutCurrentAsync(connection, transaction, scope, originalTimeout, cancellationToken).ConfigureAwait(false))
                            {
                                throw new InvalidOperationException("Original owned timeout authority was revoked before the new transition committed.");
                            }
                            await ValidateManualTimeoutAdmissionAsync(connection, transaction, scope.ScopeId, transition, cancellationToken).ConfigureAwait(false);
                            result = await WriteTransitionAsync(connection, transaction, scope, transition, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                if (result?.Outcome == SagaTransitionCommitOutcome.Committed)
                {
                    commitAttempted = true;
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    commitAcknowledged = true;
                }
            }
            catch (Exception error) { primary = error; }
            finally
            {
                primary = await CleanupOwnedAsync(null, transaction, commitAcknowledged, primary).ConfigureAwait(false);
            }
        }
        catch (Exception error) { Attach(ref primary, error, "ConnectionDispose"); }

        if (primary is not null && commitAttempted && !commitAcknowledged)
        {
            // Lost acknowledgement is not rollback. Serialize a fresh authoritative lookup with the same
            // scope lock after closing the uncertain connection. Never re-prepare or mint another batch.
            using var resolution = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var known = await ResolveUncertainAsync(transition, resolution.Token).ConfigureAwait(false);
                if (known.Outcome is SagaTransitionCommitOutcome.AlreadyCommitted or SagaTransitionCommitOutcome.IdentityConflict)
                {
                    return known;
                }
                if (known.Outcome != SagaTransitionCommitOutcome.NotFound)
                {
                    return new(SagaTransitionCommitOutcome.Unknown, null) { Failure = primary };
                }
            }
            catch (Exception error)
            {
                primary.Data["CommitResolutionFailure"] = error;
                return new(SagaTransitionCommitOutcome.Unknown, null) { Failure = primary };
            }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        return result ?? throw new InvalidOperationException("The durable transition produced no outcome.");
    }

    private async Task<SagaTransitionCommitResult> ResolveUncertainAsync(SagaPreparedTransition transition, CancellationToken token)
    {
        return await ReadOwnedAsync<SagaTransitionCommitResult>(async(connection,transaction,operationToken)=>
        {
            var scope = await LockScopeAsync(connection, transaction, transition.Identity.TenantId, transition.Identity.SagaId, operationToken).ConfigureAwait(false);
            if (scope is null) { return new(SagaTransitionCommitOutcome.NotFound, null); }
            if (!ScopeMatches(scope, transition.Identity)) { return new(SagaTransitionCommitOutcome.IdentityConflict, null); }
            var receipt = await FindReceiptAsync(connection, transaction, scope.ScopeId, transition.Identity.IncarnationId,
                transition.Identity.TransitionId, operationToken).ConfigureAwait(false);
            return receipt is null ? new(SagaTransitionCommitOutcome.NotFound, null)
                : receipt.Digest.AsSpan().SequenceEqual(transition.GetDigest())
                    ? new(SagaTransitionCommitOutcome.AlreadyCommitted, receipt.CommittedVersion)
                    : new(SagaTransitionCommitOutcome.IdentityConflict, null);
        },token).ConfigureAwait(false);
    }

    private static async Task<SagaTransitionCommitResult?> ResolveExistingAsync(SqlConnection connection, SqlTransaction transaction,
        ScopeRow scope, SagaPreparedTransition transition, CancellationToken token)
    {
        var identity = transition.Identity;
        var exact = await FindReceiptAsync(connection, transaction, scope.ScopeId, identity.IncarnationId, identity.TransitionId, token).ConfigureAwait(false);
        if (exact is not null)
        {
            return exact.Digest.AsSpan().SequenceEqual(transition.GetDigest())
                ? new(SagaTransitionCommitOutcome.AlreadyCommitted, exact.CommittedVersion)
                : new(SagaTransitionCommitOutcome.IdentityConflict, null);
        }
        if (identity.DeliveryId is not null)
        {
            var delivery = await FindDeliveryAsync(connection, transaction, scope.ScopeId, identity.IncarnationId, identity.DeliveryId, token).ConfigureAwait(false);
            if (delivery is not null) { return new(SagaTransitionCommitOutcome.DeliveryAlreadyCommitted, delivery.CommittedVersion); }
        }
        if (scope.HasTimerParticipant && !transition.HasTimerParticipant)
        {
            throw new InvalidOperationException("This durable incarnation requires its selected timer participant before another state commit.");
        }
        if (scope.IncarnationId != identity.IncarnationId || scope.IsClosed) { return new(SagaTransitionCommitOutcome.IncarnationClosed, null); }
        if (scope.CommittedVersion != transition.ExpectedVersion || scope.Generation != transition.Generation
            || scope.NextEffectSequence != transition.FirstSequence) { return new(SagaTransitionCommitOutcome.VersionConflict, null); }
        return null;
    }

    private static async Task<SagaTransitionCommitResult> WriteTransitionAsync(SqlConnection connection, SqlTransaction transaction,
        ScopeRow scope, SagaPreparedTransition transition, CancellationToken token)
    {
        var version = checked(transition.ExpectedVersion + 1);
        var changed = await WriteStateAsync(connection, transaction, transition, version, token).ConfigureAwait(false);
        if (changed != 1) { return new(SagaTransitionCommitOutcome.VersionConflict, null); }
        await InsertReceiptAsync(connection, transaction, scope.ScopeId, transition, version, token).ConfigureAwait(false);
        var generation = await ApplyTimersAsync(connection, transaction, scope.ScopeId, transition, token).ConfigureAwait(false);
        for (var ordinal = 0; ordinal < transition.Writes.Count; ordinal++)
        {
            var write = transition.Writes[ordinal];
            // The selected prepared handle mediates codec/encryption and validates its actual SQL domain.
            // It consumes the same private frozen DTO represented by this transition's digest.
            await write.StageAsync(transaction, token).ConfigureAwait(false);
            await InsertMembershipAsync(connection, transaction, scope.ScopeId, transition, write.MessageId,
                ordinal, checked(transition.FirstSequence + ordinal), token).ConfigureAwait(false);
        }
        await UpdateScopeAsync(connection, transaction, scope.ScopeId, transition, version, generation, token).ConfigureAwait(false);
        return new(SagaTransitionCommitOutcome.Committed, version);
    }

    private void ValidateTenant(string tenant)
    {
        var current = KeyedTenantPartition.FromContext(tenantContext).TenantId;
        if (!string.Equals(current, tenant, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Durable transition tenant differs from the selected store's authoritative tenant scope.");
        }
    }

    private static async Task ValidateDurabilityAsync(SqlConnection connection,CancellationToken token)
    {

        var value=await connection.ResolveAsync(new ValidateDurabilityAsyncRequest(static _=>{ }, null, token)).ConfigureAwait(false);
        if(value is not string setting || setting is not ("DISABLED" or "ALLOWED"))
        {
            throw new InvalidOperationException("Durable saga effects require fully durable commits; the selected database must not force delayed durability.");
        }
    }

    private static bool ScopeMatches(ScopeRow scope, SagaTransitionIdentity identity) =>
        string.Equals(scope.TenantId, identity.TenantId, StringComparison.Ordinal)
        && string.Equals(scope.SagaType, identity.SagaType, StringComparison.Ordinal);

    private static void Attach(ref Exception? primary, Exception error, string phase)
    {
        if (primary is null) { primary = error; }
        else { primary.Data["Secondary" + phase] = error; }
    }

    private static byte[] ExactBytes(string value)
    {
        var bytes = new byte[checked(value.Length * sizeof(char))];
        for (var index = 0; index < value.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(char)), value[index]);
        }
        return bytes;
    }

    private static byte[] TenantKey(string tenant) => SHA256.HashData(ExactBytes(tenant));

    private static void Add(DynamicParameters command, string name, object? value, DbType type) =>
        command.Add(name, value, dbType: type);

    private static void BindIdentity(DynamicParameters command, SagaTransitionIdentity identity)
    {
        Add(command, "TenantId", identity.TenantId, DbType.String);
        Add(command, "TenantKey", TenantKey(identity.TenantId), DbType.Binary);
        Add(command, "TenantBytes", ExactBytes(identity.TenantId), DbType.Binary);
        Add(command, "TenantLength", checked(identity.TenantId.Length * sizeof(char)), DbType.Int32);
        Add(command, "SagaId", identity.SagaId, DbType.Guid);
    }

    private sealed record ScopeRow(Guid ScopeId, string TenantId, string SagaType, Guid IncarnationId,
        long Generation, bool IsClosed, long CommittedVersion, long NextEffectSequence, bool HasTimerParticipant);
    private sealed record ReceiptRow(byte[] Digest, long CommittedVersion, string? DeliveryId);
}
