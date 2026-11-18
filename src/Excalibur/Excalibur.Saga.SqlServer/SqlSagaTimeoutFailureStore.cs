// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimeoutFailureRequests;
using static Excalibur.Saga.SqlServer.SqlSagaTimeoutFailureState;

namespace Excalibur.Saga.SqlServer;

internal sealed class SqlSagaTimeoutFailureStore(
    Func<string> getTenant, Func<string> getTarget,
    SqlSagaTimerRecoveryReceiver receiver)
{
    internal async Task<IReadOnlyList<ClaimedSagaTimeout>?> TryClaimAsync(SqlConnection connection,
        string table, DateTimeOffset asOf, int count, int leaseSeconds, string claim, CancellationToken token)
    {
        var metadata = await ReadMetadataAsync(connection, table, null, token).ConfigureAwait(false);
        if (!metadata.Available) { return null; }
        var rows = await connection.ResolveAsync(new ClaimFailureTimeoutRowsRequest(table, metadata.OwnedColumns,
            asOf, count, leaseSeconds, claim, token)).ConfigureAwait(false);
        var backend = Backend(connection, metadata.Target, metadata.TargetEpoch);
        return rows.Select(row => (row.Owned is null ? new ClaimedSagaTimeout(row.Timeout, claim)
            : new ClaimedSagaTimeout(row.Timeout, claim, row.Owned)) with
            { SourceEpoch = SourceEpoch(backend, row.Source, row.Timeout), DeliveryEpoch = row.Delivery.ToString("N") }).ToArray();
    }

    internal async Task<SagaTimeoutRetirementOutcome?> TryRetireAsync(SqlConnection connection,
        string table, string tenant, ClaimedSagaTimeout claim, CancellationToken token)
    {
        var metadata = await ReadMetadataAsync(connection, table, null, token).ConfigureAwait(false);
        if (!metadata.Available) { return null; }
        if (!TrySourceEpoch(claim.SourceEpoch, Backend(connection, metadata.Target, metadata.TargetEpoch), claim.Timeout, out var source)
            || !Guid.TryParseExact(claim.DeliveryEpoch, "N", out var cycle) || cycle == Guid.Empty)
        { return SagaTimeoutRetirementOutcome.Superseded; }
        var count = await connection.ResolveAsync(new RetireFailureTimeoutRequest(table, tenant, claim, source, token)).ConfigureAwait(false);
        return count == 0 ? SagaTimeoutRetirementOutcome.Superseded : SagaTimeoutRetirementOutcome.Retired;
    }

    internal async ValueTask ValidateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var table = getTarget();
        _ = await receiver.RunRecoveryTransactionAsync<bool>(async (connection, transaction) =>
        {
            var metadata = await ReadMetadataAsync(connection, table, transaction, token).ConfigureAwait(false);
            RequireAvailable(metadata);
            return true;
        }, token).ConfigureAwait(false);
    }

    internal async ValueTask<SagaTimeoutHoldAttempt> HoldAsync(SagaTimeoutFailureObservation observation,
        ClaimedSagaTimeout claim, Guid operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(claim);
        RequireCommand(operation, nameof(operation));
        token.ThrowIfCancellationRequested();
        var original = observation.OriginalClaim;
        var supplied = SagaTimeoutFailureObservation.CopyClaim(claim);
        var tenant = getTenant();
        if (original.Timeout.TenantId != tenant || supplied.Timeout.TenantId != tenant || string.IsNullOrEmpty(supplied.ClaimToken))
        { return new(SagaTimeoutHoldOutcome.Rejected); }
        var table = getTarget();
        return await receiver.RunRecoveryTransactionAsync<SagaTimeoutHoldAttempt>(async (connection, transaction) =>
        {
            var metadata = await ReadMetadataAsync(connection, table, transaction, token).ConfigureAwait(false);
            RequireAvailable(metadata);
            var backend = Backend(connection, metadata.Target, metadata.TargetEpoch);
            if (!TrySourceEpoch(original.SourceEpoch, backend, original.Timeout, out var originalSource)
                || SourceEpoch(backend, originalSource, original.Timeout) != original.SourceEpoch
                || !Guid.TryParseExact(original.DeliveryEpoch, "N", out var originalDelivery) || originalDelivery == Guid.Empty
                || originalDelivery.ToString("N") != original.DeliveryEpoch)
            { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            await receiver.LockFailureDomainAsync(connection, transaction, table, tenant, original.Timeout.SagaId, token).ConfigureAwait(false);
            var row = (await connection.ResolveAsync(new ReadFailureRowRequest(table, metadata.OwnedColumns,
                tenant, original.Timeout.SagaId, original.Timeout.TimeoutId, transaction, token)).ConfigureAwait(false)).SingleOrDefault();
            if (row is null || row.Source != originalSource) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            if (!MatchesObservation(row, observation, backend)) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            if (row.Operation == operation)
            {
                if (!SameHoldOperation(row, observation, supplied, backend)) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
                if (!row.Held) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded, Receipt(backend, row)); }
            }
            if (row.Delivery.ToString("N") != original.DeliveryEpoch) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            if (!MatchesSuppliedClaim(row, supplied, backend)) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Rejected); }
            if (!await receiver.LockFailureAuthorityAsync(connection, transaction, table,
                original.Timeout, original.OwnedDelivery, token).ConfigureAwait(false))
            { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded); }
            // Another host's matching committed hold also certifies exclusion; it does not claim THIS operation committed.
            if (row.Held) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held, Receipt(backend, row)); }
            if (row.Claim != supplied.ClaimToken) { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.RetryWithFreshClaim); }
            var held = row with
            {
                Held = true, Version = checked(row.Version + 1), Token = Guid.NewGuid(), Operation = operation,
                OriginalClaim = original.ClaimToken, HoldClaim = supplied.ClaimToken, ObservedDelivery = row.Delivery,
                Kind = observation.Kind, Reason = observation.Reason, ResumeCommand = null, Claim = null,
            };
            _ = await connection.ResolveAsync(new WriteFailureHoldRequest(table, held, transaction, token)).ConfigureAwait(false);
            return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held, Receipt(backend, held));
        }, token).ConfigureAwait(false);
    }

    internal async ValueTask<SagaTimeoutHoldReceipt?> ResumeAsync(SagaTimeoutHoldToken token,
        Guid command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        RequireCommand(command, nameof(command));
        cancellationToken.ThrowIfCancellationRequested();
        var handle = Parse(token);
        var tenant = getTenant();
        if (handle is null || handle.Tenant != tenant) { return null; }
        var table = getTarget();
        return await receiver.RunRecoveryTransactionAsync<SagaTimeoutHoldReceipt?>(async (connection, transaction) =>
        {
            var metadata = await ReadMetadataAsync(connection, table, transaction, cancellationToken).ConfigureAwait(false);
            RequireAvailable(metadata);
            var backend = Backend(connection, metadata.Target, metadata.TargetEpoch);
            if (backend != handle.Backend) { return null; }
            // Acquire domain authority BEFORE the payload row, as public cancellation and atomic writes do.
            await receiver.LockFailureDomainAsync(connection, transaction, table, tenant, handle.Saga, cancellationToken).ConfigureAwait(false);
            var row = (await connection.ResolveAsync(new ReadFailureRowRequest(table, metadata.OwnedColumns,
                tenant, handle.Saga, handle.TimeoutId, transaction, cancellationToken)).ConfigureAwait(false)).SingleOrDefault();
            if (row is null || row.Source != handle.Source || row.ObservedDelivery != handle.Delivery
                || row.Version != handle.Version || row.Token != handle.Token) { return null; }
            if (row.ResumeCommand == command) { return Receipt(backend, row); }
            if (!row.Held || !await receiver.LockFailureAuthorityAsync(connection, transaction, table,
                row.Timeout, row.Owned, cancellationToken).ConfigureAwait(false)) { return null; }
            var resumed = row with { Held = false, Delivery = Guid.NewGuid(), ResumeCommand = command, Claim = null };
            _ = await connection.ResolveAsync(new ResumeFailureRowRequest(table, resumed, transaction, cancellationToken)).ConfigureAwait(false);
            return Receipt(backend, resumed);
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<SagaTimeoutHoldPage> GetHeldAsync(string tenant, string? saga, int pageSize,
        string? cursor, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        token.ThrowIfCancellationRequested();
        if (tenant != getTenant()) { throw new InvalidOperationException("Held timer query tenant differs from the current receiver authority."); }
        var table = getTarget();
        return await receiver.RunRecoveryTransactionAsync<SagaTimeoutHoldPage>(async (connection, transaction) =>
        {
            var metadata = await ReadMetadataAsync(connection, table, transaction, token).ConfigureAwait(false);
            RequireAvailable(metadata);
            var backend = Backend(connection, metadata.Target, metadata.TargetEpoch);
            var prefix = string.Join('|', "sql-timeout-page-v1", backend, Part(tenant), saga is null ? "-" : Part(saga)) + "|";
            string? after = null;
            if (cursor is not null)
            {
                if (!cursor.StartsWith(prefix, StringComparison.Ordinal)) { throw new ArgumentException("Held timer cursor belongs to another target or exact partition/filter.", nameof(cursor)); }
                try { after = Unpart(cursor[prefix.Length..]); }
                catch (FormatException error) { throw new ArgumentException("Invalid held timer cursor.", nameof(cursor), error); }
            }
            var rows = (await connection.ResolveAsync(new QueryHeldFailureRowsRequest(table, metadata.OwnedColumns,
                tenant, saga, after, pageSize + 1, transaction, token)).ConfigureAwait(false)).ToArray();
            var page = rows.Take(pageSize).Select(row => Receipt(backend, row)).ToArray();
            var next = rows.Length > pageSize ? prefix + Part(page[^1].Timeout.TimeoutId) : null;
            return new SagaTimeoutHoldPage(page, next);
        }, token).ConfigureAwait(false);
    }

    private static bool SameHoldOperation(Row row, SagaTimeoutFailureObservation observation, ClaimedSagaTimeout claim, string backend)
    {
        var original = observation.OriginalClaim;
        return row.OriginalClaim == original.ClaimToken && row.HoldClaim == claim.ClaimToken
            && row.ObservedDelivery!.Value.ToString("N") == original.DeliveryEpoch
            && SourceEpoch(backend, row.Source, row.Timeout) == claim.SourceEpoch && row.ObservedDelivery.Value.ToString("N") == claim.DeliveryEpoch
            && row.Kind == observation.Kind && row.Reason == observation.Reason
            && SameTimeout(row.Timeout, claim.Timeout) && SameAuthority(row.Owned, claim.OwnedDelivery);
    }

    private static bool MatchesSuppliedClaim(Row row, ClaimedSagaTimeout claim, string backend) =>
        SourceEpoch(backend, row.Source, row.Timeout) == claim.SourceEpoch && row.Delivery.ToString("N") == claim.DeliveryEpoch
        && SameTimeout(row.Timeout, claim.Timeout)
        && SameAuthority(row.Owned, claim.OwnedDelivery);

    private static void RequireCommand(Guid command, string name)
    { if (command == Guid.Empty) { throw new ArgumentException("Operation identity must be nonempty.", name); } }
    private static void RequireAvailable(Metadata metadata)
    { if (!metadata.Available) { throw new InvalidOperationException("Apply 07-SagaTimeoutFailureHolds.sql to the selected direct timeout table before hosted preserving delivery."); } }
    private static async Task<Metadata> ReadMetadataAsync(SqlConnection connection, string table,
        SqlTransaction? transaction, CancellationToken token) =>
        (await connection.ResolveAsync(new FailureMetadataRequest(table, transaction, token)).ConfigureAwait(false)).Single();
}
