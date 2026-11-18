// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Same-authority admission policy, bounded failure retention and explicit quarantine recovery.
/// Budget capture/grant/count, failure/disable, and recovery/receipt/history each share one atomic boundary.
/// Query tenant IDs are exact partitions supplied by an authorized caller, not authorization credentials.
/// </summary>
/// <remarks>
/// The first budgeted admission atomically captures the positive requested limit with grant/count before
/// dispatch. Later requests, even with a different positive limit, return UsedExisting and the stored
/// limit; neither inherited admission nor progress may erase captured policy/history for an unsettled
/// occurrence. Consumed unbudgeted legacy history refuses upgrade with an actionable exception.
/// Failure/disable and recovery/history/receipt mutations share the existing authority transaction.
/// Recovery compares the exact token, creates a fresh generation with count zero and the SAME captured
/// budget, and retains original provenance. The next ordinary acquisition creates execution ownership.
/// Same command/token returns its original receipt after later progress and restart; conflicting command
/// or token cannot grant another reset. Ordinary administration cannot erase unresolved provenance:
/// replacement refuses any captured unsettled/recovery epoch, even before a failure write succeeds.
/// Complete atomically retains such an occurrence as administrative quarantine, clearing execution
/// ownership and disabling without losing the generation, original payload, count or captured budget.
/// Already quarantined completion is idempotent. Normal uncaptured administration keeps its prior contract.
/// Cancellation observed before mutation leaves state unchanged. In-flight cancellation/unknown reply
/// does not establish rollback; reconcile exact durable state before claiming failure or success.
/// Query cursors use ordinal Guid.ToString("N") order, exclusive and weakly consistent during inserts;
/// they are bounded observations, not a transaction-snapshot pagination guarantee.
/// </remarks>
public interface IScheduleExecutionFailureStore : IScheduleExecutionStore
{
    /// <summary>Captures policy on the first authoritative grant, or returns existing policy without widening it.</summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="generation">Observed administrative generation.</param>
    /// <param name="occurrenceUtc">Exact due occurrence.</param>
    /// <param name="requestedMaxAdmissionAttempts">Positive proposed automatic budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Admission and explicit captured-policy observation; null for ordinary authority contention.</returns>
    /// <exception cref="InvalidOperationException">Consumed legacy admission history has no captured budget; explicit repair is required.</exception>
    Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
        DateTimeOffset occurrenceUtc, int requestedMaxAdmissionAttempts, CancellationToken cancellationToken);

    /// <summary>Conditionally retires a fulfilled occurrence or explicitly policy-skipped first-grant work.</summary>
    /// <remarks>
    /// Fulfilled is the caller assertion of actual successful dispatch, not provider proof. PolicySkipped
    /// and PolicyDisabled require provider-stored count one, no failure or recovered-work lineage and
    /// unchanged LastExecutionUtc. PolicyDisabled requires Disable; enabled completion requires a next
    /// occurrence strictly after the admitted occurrence. Inherited progress refuses captured-policy work.
    /// The provider rechecks these facts; caller-supplied AdmissionAttempt/policy observations are not authority.
    /// </remarks>
    /// <param name="lease">Exact current unexpired authority.</param>
    /// <param name="result">Bounded progress.</param>
    /// <param name="disposition">Actual successful dispatch or an intentional catchup policy disposition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True only on first confirmed atomic completion; false grants no reset or progress.</returns>
    Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
        ScheduleExecutionCompletionDisposition disposition, CancellationToken cancellationToken);

    /// <summary>Conditionally records failure without resetting budget/count or advancing the occurrence; quarantine also disables it.</summary>
    /// <param name="lease">Exact current unexpired authority.</param>
    /// <param name="failure">Bounded reason and requested disposition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True only after confirmed atomic mutation. False never authorizes ordinary progress.</returns>
    Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure,
        CancellationToken cancellationToken);

    /// <summary>Resumes one unresolved quarantine under exact expected state and an idempotent command.</summary>
    /// <param name="expectedQuarantineToken">Exact provider-owned quarantine identity/version.</param>
    /// <param name="recoveryCommandId">Nonempty durable command identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The same durable receipt on identical command replay; null on conflicting expected state/command.</returns>
    Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(
        ScheduleQuarantineToken expectedQuarantineToken, Guid recoveryCommandId, CancellationToken cancellationToken);

    /// <summary>Reads exact retained provenance in one tenant partition, including resolved quarantine history.</summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="generation">Original failure generation.</param>
    /// <param name="occurrenceUtc">Original occurrence.</param>
    /// <param name="tenantId">Exact tenant partition, including null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detached matching provenance or null.</returns>
    Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid scheduleId, Guid generation,
        DateTimeOffset occurrenceUtc, string? tenantId, CancellationToken cancellationToken);

    /// <summary>Reads a bounded stable quarantine page for one exact tenant partition; resolved history is retained.</summary>
    /// <param name="tenantId">Exact tenant partition, including null.</param>
    /// <param name="afterQuarantineId">Exclusive stable GUID cursor, or null for the first page.</param>
    /// <param name="maximumCount">Page size from1 through256.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detached provenance ordered by the ordinal Guid.ToString("N") ordering.</returns>
    Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenantId,
        Guid? afterQuarantineId, int maximumCount, CancellationToken cancellationToken);
}
