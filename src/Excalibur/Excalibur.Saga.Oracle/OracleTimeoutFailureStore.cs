// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Oracle.ManagedDataAccess.Client;
using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleTimeoutFailureStore(OracleSagaTimerRecovery recovery, Func<string> getTenant)
{
    internal Task ValidateAsync(CancellationToken cancellationToken) => recovery.RunFailureTransactionAsync(async (connection, transaction) =>
    {
        var target = recovery.CaptureRecoveryTarget();
        _ = await RequireProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
        await recovery.ValidateFailureTargetAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
        return true;
    }, cancellationToken);

    private static readonly SagaTimeoutHoldAttempt Rejected = new(SagaTimeoutHoldOutcome.Rejected);
    private static readonly SagaTimeoutHoldAttempt RetryClaim = new(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
    private static readonly SagaTimeoutHoldAttempt Ended = new(SagaTimeoutHoldOutcome.ObservationEnded);

    internal Task<SagaTimeoutHoldAttempt> TryHoldAsync(SagaTimeoutFailureObservation observation,
        ClaimedSagaTimeout claim, Guid operationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation); ArgumentNullException.ThrowIfNull(claim);
        if (operationId == Guid.Empty) { throw new ArgumentException("A nonempty scoped hold operation is required.", nameof(operationId)); }
        var original = observation.OriginalClaim;
        var target = recovery.CaptureRecoveryTarget(); var tenant = getTenant();
        if (tenant != original.Timeout.TenantId || tenant != claim.Timeout.TenantId
            || !IsRawEpoch(original.DeliveryEpoch) || !IsRawEpoch(claim.DeliveryEpoch)
            || original.Timeout.TimeoutId != claim.Timeout.TimeoutId || original.Timeout.SagaId != claim.Timeout.SagaId)
        { return Task.FromResult(Rejected); }
        return recovery.RunFailureTransactionAsync(async (connection, transaction) =>
        {
            var profile = await RequireProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            // A foreign selected backend/address can never certify original-source absence.
            if (!HasSourceBinding(profile, original.SourceEpoch ?? string.Empty, tenant, original.Timeout.SagaId, original.Timeout.TimeoutId)
                || !HasSourceBinding(profile, claim.SourceEpoch ?? string.Empty, tenant, claim.Timeout.SagaId, claim.Timeout.TimeoutId)) { return Rejected; }
            var scope = await recovery.LockFailureDomainAsync(connection, transaction, target, tenant, original.Timeout.SagaId, cancellationToken).ConfigureAwait(false);
            var owned = await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            var row = await ReadRowAsync(connection, transaction, profile, tenant, original.Timeout.SagaId, original.Timeout.TimeoutId, owned, cancellationToken).ConfigureAwait(false);
            // API schedule/replacement always mints a new row lifetime; resume always rotates cycle.
            // Under this SAME bound authority, absence/mismatch irreversibly ends this observation.
            if (row is null || row.Source != original.SourceEpoch) { return Ended; }
            if (!SameSnapshot(row, original) || !SameSnapshot(row, claim)) { return Rejected; }
            var previous = row.Receipt;
            // Reconcile SAME operation before lease validation. Conflicting same-op inputs are rejected
            // even when another interpretation could certify that some hold currently exists.
            if (previous?.Operation == operationId && previous.Cycle == original.DeliveryEpoch)
            {
                if (previous.OriginalClaim != original.ClaimToken || previous.OperationClaim != claim.ClaimToken
                    || claim.DeliveryEpoch != previous.Cycle || previous.Kind != observation.Kind || previous.Reason != observation.Reason) { return Rejected; }
                if (row.Cycle != original.DeliveryEpoch && previous.ResumeCommand is not null)
                { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded, Receipt(profile, row, previous, SagaTimeoutHoldState.Resumed)); }
                return row.Held && row.Cycle == original.DeliveryEpoch ? new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held,
                    Receipt(profile, row, previous, SagaTimeoutHoldState.Held)) : Rejected;
            }
            if (row.Cycle != original.DeliveryEpoch) { return Ended; }
            if (claim.DeliveryEpoch != original.DeliveryEpoch) { return Rejected; }
            var current = await OracleSagaTimerRecovery.IsFailureOwnedCurrentAsync(connection, transaction, target, scope, row.Owned, cancellationToken).ConfigureAwait(false);
            if (current is null) { return Rejected; }
            if (!current.Value) { return Ended; }
            // Another host may have held this SAME source/cycle under a DIFFERENT operation. A real
            // current Held receipt resolves local observation without claiming our operation committed.
            if (row.Held)
            { return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held, Receipt(profile, row,
                previous ?? throw new InvalidDataException("Held source has no receipt."), SagaTimeoutHoldState.Held)); }
            if (row.ClaimToken != claim.ClaimToken || string.IsNullOrEmpty(claim.ClaimToken)) { return RetryClaim; }
            var frame = new OracleTimeoutFailureFrames.Receipt(operationId, original.ClaimToken, claim.ClaimToken,
                row.Cycle, Guid.NewGuid(), observation.Kind, observation.Reason, null, null);
            var version = checked(row.Version + 1);
            var count = await ExecuteRecoveryAsync(connection, transaction,
                $"UPDATE {target.Payload} SET FailureHeld=1,FailureVersion=:Version,FailureReceipt=:Receipt,ClaimedAt=NULL,ClaimedBy=NULL WHERE ROWID=CHARTOROWID(:PayloadRowLocator) AND FailureSourceEpoch=:Source AND FailureDeliveryEpoch=:Cycle AND FailureVersion=:Previous",
                [Number("Version", version), new("Receipt", OracleDbType.Blob, OracleTimeoutFailureFrames.EncodeReceipt(frame)), Text("PayloadRowLocator", row.RowId),
                    Binary("Source", RawSource(row.Source)), Binary("Cycle", Convert.FromHexString(row.Cycle)), Number("Previous", row.Version)], cancellationToken).ConfigureAwait(false);
            if (count != 1) { throw new InvalidOperationException("Oracle timeout hold lost its locked source authority."); }
            return new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held,
                Receipt(profile, row with { Held = true, Version = version, Receipt = frame }, frame, SagaTimeoutHoldState.Held));
        }, cancellationToken);
    }

    internal Task<SagaTimeoutHoldReceipt?> TryResumeAsync(SagaTimeoutHoldToken token, Guid commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (commandId == Guid.Empty) { throw new ArgumentException("A nonempty scoped resume command is required.", nameof(commandId)); }
        OracleTimeoutFailureFrames.Binding binding;
        try { binding = OracleTimeoutFailureFrames.DecodeBinding(token.Value); }
        catch (Exception error) when (error is FormatException or InvalidDataException or EndOfStreamException)
        { return Task.FromResult<SagaTimeoutHoldReceipt?>(null); }
        if (!IsRawEpoch(binding.Cycle) || binding.Version < 1 || binding.HoldToken == Guid.Empty)
        { return Task.FromResult<SagaTimeoutHoldReceipt?>(null); }
        var target = recovery.CaptureRecoveryTarget(); var tenant = getTenant();
        if (tenant != binding.Tenant || target.Owner != binding.Owner || target.Table != binding.Table)
        { return Task.FromResult<SagaTimeoutHoldReceipt?>(null); }
        return recovery.RunFailureTransactionAsync<SagaTimeoutHoldReceipt?>(async (connection, transaction) =>
        {
            var profile = await RequireProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            if (!SameTarget(profile, binding)) { return null; }
            var scope = await recovery.LockFailureDomainAsync(connection, transaction, target, tenant, binding.SagaId, cancellationToken).ConfigureAwait(false);
            var owned = await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            var row = await ReadRowAsync(connection, transaction, profile, tenant, binding.SagaId, binding.TimeoutId, owned, cancellationToken).ConfigureAwait(false);
            if (row is null || row.Timeout.TenantId != tenant || row.Timeout.SagaId != binding.SagaId || row.Source != binding.Source) { return null; }
            var frame = row.Receipt;
            if (frame is null || frame.HoldToken != binding.HoldToken || frame.Cycle != binding.Cycle || row.Version != binding.Version) { return null; }
            if (frame.ResumeCommand == commandId && frame.ResumeExpected == binding.HoldToken)
            { return Receipt(profile, row, frame, SagaTimeoutHoldState.Resumed); }
            if (!row.Held || row.Cycle != binding.Cycle || frame.ResumeCommand is not null) { return null; }
            if (await OracleSagaTimerRecovery.IsFailureOwnedCurrentAsync(connection, transaction, target, scope, row.Owned, cancellationToken).ConfigureAwait(false) is not true) { return null; }
            var resumed = frame with { ResumeCommand = commandId, ResumeExpected = binding.HoldToken };
            var count = await ExecuteRecoveryAsync(connection, transaction,
                $"UPDATE {target.Payload} SET FailureHeld=0,FailureDeliveryEpoch=SYS_GUID(),FailureReceipt=:Receipt,ClaimedAt=NULL,ClaimedBy=NULL WHERE ROWID=CHARTOROWID(:PayloadRowLocator) AND FailureSourceEpoch=:Source AND FailureDeliveryEpoch=:Cycle AND FailureHeld=1 AND FailureVersion=:Version",
                [new("Receipt", OracleDbType.Blob, OracleTimeoutFailureFrames.EncodeReceipt(resumed)), Text("PayloadRowLocator", row.RowId), Binary("Source", RawSource(row.Source)),
                    Binary("Cycle", Convert.FromHexString(row.Cycle)), Number("Version", row.Version)], cancellationToken).ConfigureAwait(false);
            if (count != 1) { throw new InvalidOperationException("Oracle timeout resume lost its locked held authority."); }
            return Receipt(profile, row, resumed, SagaTimeoutHoldState.Resumed);
        }, cancellationToken);
    }

    private static SagaTimeoutHoldReceipt Receipt(Profile profile, FailureRow row, OracleTimeoutFailureFrames.Receipt frame, SagaTimeoutHoldState state)
    {
        var binding = new OracleTimeoutFailureFrames.Binding(profile.Target.Owner, profile.Target.Table, profile.ObjectId,
            row.Timeout.TenantId, row.Timeout.SagaId, row.Timeout.TimeoutId, row.Source, frame.Cycle, row.Version, frame.HoldToken);
        return new(new SagaTimeoutHoldToken(OracleTimeoutFailureFrames.EncodeBinding(binding)), state, row.Timeout, frame.Kind, frame.Reason);
    }
    private static async Task<Profile> RequireProfileAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, CancellationToken cancellationToken) =>
        await ReadProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Oracle timeout hold capability requires script04 on configured physical target {target.Payload}; manual APIs remain available without it.");
}
