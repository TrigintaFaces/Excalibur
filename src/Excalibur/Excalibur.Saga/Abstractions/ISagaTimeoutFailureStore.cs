// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>Selected timeout-store hold authority. Discover through the selected ISagaTimeoutStore.GetService.</summary>
/// <remarks>
/// Implementations atomically validate current claim, exact source lifetime AND delivery cycle, payload and receiver cancellation
/// authority with hold/resume mutation. Tokens identify expected state; they are not authorization credentials.
/// All operation IDs are scoped to retained physical target + exact timer identity + source epoch.
/// Conflicting inputs under the same retained operation ID refuse. No global command ledger is promised.
/// </remarks>
public interface ISagaTimeoutFailureStore
{
    /// <summary>Gets whether this selected implementation persists holds across process restart.</summary>
    bool HoldsSurviveProcessRestart { get; }

    /// <summary>Verifies usable selected-target schema, claim epoch projection and hold/query/resume support.</summary>
    /// <remarks>Hosted delivery must await this successfully BEFORE pumping. No migration or dispatch occurs.</remarks>
    ValueTask ValidateFailureStorageAsync(CancellationToken cancellationToken);

    /// <summary>Conditionally holds the original observed failure under the supplied current claim.</summary>
    /// <remarks>
    /// observation is detached once from the failed dispatch and never rewritten. For the initial operation,
    /// claim is its original claim. To rebind after expiry, a NEW operationId and a newly acquired claim are
    /// required; the observation retains ORIGINAL claim inputs, source lifetime and delivery cycle. The store compares exact
    /// source lifetime/delivery cycle/payload/identity and current receiver cancellation authority before allowing rebind.
    /// Repeating an operation uses its identical claim token and inputs, even after acknowledgment loss.
    /// Held requires an actual current held receipt. RetryWithFreshClaim positively proves the original
    /// source/cycle remains current but the supplied claim is not. ObservationEnded proves that exact
    /// original source/cycle ended under the SAME bound selected target (absence, replacement, resume
    /// or irreversible receiver revocation); it is not a delivery acknowledgment. Rejected covers
    /// wrong target/tenant or conflicting inputs and is not authoritative source absence.
    /// Bind SourceEpoch to the selected logical backend/table instance BEFORE checking absence.
    /// Unknown commit throws; caller reconciles the original operation unchanged.
    /// A stale operation never re-holds resumed/replaced work. Successful hold retains original payload/due.
    /// </remarks>
    ValueTask<SagaTimeoutHoldAttempt> TryHoldFailureAsync(
        SagaTimeoutFailureObservation observation, ClaimedSagaTimeout claim, Guid operationId,
        CancellationToken cancellationToken);

    /// <summary>Queries actual held rows in one exact raw tenant partition, optionally one exact saga ID.</summary>
    /// <remarks>
    /// pageSize is 1..256. Cursor is opaque, exclusive, target/partition-bound and weakly consistent.
    /// Returned snapshots own payload bytes. Null cursor starts a page. No implicit cross-tenant access grant.
    /// </remarks>
    ValueTask<SagaTimeoutHoldPage> GetHeldFailuresAsync(
        string tenantId, string? sagaId, int pageSize, string? cursor, CancellationToken cancellationToken);

    /// <summary>Conditionally resumes an existing held row, preserving original ID, payload and due timestamp.</summary>
    /// <remarks>
    /// token binds target, exact timer source lifetime, delivery cycle and held version. Same commandId replay returns the
    /// recorded result within retained epoch metadata, without another transition or dispatch lease.
    /// Wrong version, cancellation/replacement or missing row refuses (null); never inserts a payload.
    /// Successful resume atomically advances the persisted DeliveryEpoch before normal claim acquisition; the original SourceEpoch remains the row lifetime. Old observations cannot bind to this new cycle, even after old receipt eviction. Normal claim acquisition later grants a fresh claim. Same-command receipt remains replayable after
    /// that acquisition while its receipt is retained. Later independent cycles may make old requests refuse;
    /// they never regain mutation authority. Explicit cancel/replacement always remains effective.
    /// </remarks>
    ValueTask<SagaTimeoutHoldReceipt?> TryResumeHeldFailureAsync(
        SagaTimeoutHoldToken token, Guid commandId, CancellationToken cancellationToken);
}

