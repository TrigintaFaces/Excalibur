// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Testing.Conformance;

/// <summary>Required capability arms for retaining authoritative scheduled occurrence failures.</summary>
/// <remarks>Every arm runs against the same configured authority. No missing capability is reported as a pass.
/// Providers own teardown; fresh identifiers and exact tenant partitions isolate each arm. These store-only
/// arms do not prove hosted delivery, physical persistence, Native AOT or exactly-once effects.</remarks>
public abstract class ScheduleFailureStoreConformanceTestKit : ConformanceTestKit
{
    /// <summary>Creates the actual failure-capable authority.</summary>
    /// <returns>The selected authority.</returns>
    protected abstract IScheduleExecutionFailureStore CreateStore();

    /// <summary>Verifies first policy capture survives release and a different host's request.</summary>
    /// <returns>The arm.</returns>
    public virtual async Task CapturedBudgetCannotBeWidenedOrErased()
    {
        var store = CreateStore();
        var (row, generation) = await SeedAsync(store).ConfigureAwait(false);
        var first = await AcquireAsync(store, row, generation, 2).ConfigureAwait(false);
        Require(first.PolicySelection == ScheduleAdmissionPolicySelection.CapturedNew && first.CapturedMaxAdmissionAttempts == 2,
            "First admission did not capture the requested policy.");
        await store.ReleaseExecutionAsync(first.Lease, CancellationToken.None).ConfigureAwait(false);
        var second = await AcquireAsync(store, row, generation, 200).ConfigureAwait(false);
        Require(second.CapturedMaxAdmissionAttempts == 2 && second.PolicySelection == ScheduleAdmissionPolicySelection.UsedExisting,
            "A later host widened or silently replaced captured policy.");
        Require(!await store.TryUpdateProgressAsync(second.Lease, new(null, row.NextExecutionUtc!.Value.AddMinutes(1)), CancellationToken.None).ConfigureAwait(false),
            "Inherited progress erased captured unsettled work.");
        var observedCountMutation = second.Lease with { AdmissionAttempt = 999 };
        Require(await store.TryRecordFailureAsync(observedCountMutation,
            new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None).ConfigureAwait(false),
            "Mutable reported admission count was incorrectly included in bearer authority.");
        var failure = await FailureAsync(store, row, generation).ConfigureAwait(false);
        Require(failure.AdmissionAttempts == 2 && failure.MaxAdmissionAttempts == 2, "Stored history trusted mutable caller observations.");
        await store.ReleaseExecutionAsync(observedCountMutation, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Verifies exact expected-state recovery deduplicates through later progress.</summary>
    /// <returns>The arm.</returns>
    public virtual async Task QuarantineRecoveryPreservesHistoryAndDeduplicates()
    {
        var store = CreateStore();
        var (row, generation) = await SeedAsync(store).ConfigureAwait(false);
        var first = await AcquireAsync(store, row, generation, 1).ConfigureAwait(false);
        Require(!await store.TryRecordFailureAsync(first.Lease,
            new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), CancellationToken.None).ConfigureAwait(false),
            "Ordinary failure API quarantined before the admission bound.");
        Require(await store.TryRecordFailureAsync(first.Lease,
            new(ScheduleFailureReason.MalformedPayload, ScheduleFailureDisposition.Retrying), CancellationToken.None).ConfigureAwait(false), "Failure was not retained.");
        await store.ReleaseExecutionAsync(first.Lease, CancellationToken.None).ConfigureAwait(false);
        var bound = await AcquireAsync(store, row, generation, 100).ConfigureAwait(false);
        Require(bound.Lease.AdmissionAttempt == 2 && bound.CapturedMaxAdmissionAttempts == 1, "The persisted bound was lost.");
        Require(await store.TryRecordFailureAsync(bound.Lease,
            new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), CancellationToken.None).ConfigureAwait(false), "Quarantine did not commit.");
        await store.ReleaseExecutionAsync(bound.Lease, CancellationToken.None).ConfigureAwait(false);
        var original = await FailureAsync(store, row, generation).ConfigureAwait(false);
        Require(original.Disposition == ScheduleFailureDisposition.Quarantined && original.Message.MessageBody == row.MessageBody,
            "Quarantine claimed success without retaining the original envelope.");
        var command = Guid.NewGuid();
        var receipts = await Task.WhenAll(store.TryResumeQuarantinedOccurrenceAsync(original.Token, command, CancellationToken.None),
            store.TryResumeQuarantinedOccurrenceAsync(original.Token, command, CancellationToken.None)).ConfigureAwait(false);
        var receipt = receipts[0] ?? throw new TestFixtureAssertionException("Recovery was not applied.");
        Require(receipts[1] == receipt && receipt.FreshGeneration != generation && receipt.MaxAdmissionAttempts == 1,
            "Concurrent replay minted another generation or widened the recovery budget.");
        Require(await store.TryResumeQuarantinedOccurrenceAsync(original.Token, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false) is null,
            "Another command reset the same quarantine again.");
        var recovered = await AcquireAsync(store, row, receipt.FreshGeneration, 200).ConfigureAwait(false);
        Require(!recovered.PolicyDispositionAllowed && recovered.CapturedMaxAdmissionAttempts == 1 && recovered.Lease.AdmissionAttempt == 1,
            "Recovered occurrence lost its original-work execution requirement.");
        Require(!await store.TryCompleteExecutionAsync(recovered.Lease, new(null, row.NextExecutionUtc!.Value.AddMinutes(1)),
            ScheduleExecutionCompletionDisposition.PolicySkipped, CancellationToken.None).ConfigureAwait(false), "Recovered work was skipped as downtime backlog.");
        Require(await store.TryCompleteExecutionAsync(recovered.Lease,
            new(row.NextExecutionUtc, row.NextExecutionUtc!.Value.AddMinutes(1)), ScheduleExecutionCompletionDisposition.Fulfilled,
            CancellationToken.None).ConfigureAwait(false), "Recovered successful work could not settle.");
        await store.ReleaseExecutionAsync(recovered.Lease, CancellationToken.None).ConfigureAwait(false);
        Require(await store.TryResumeQuarantinedOccurrenceAsync(original.Token, command, CancellationToken.None).ConfigureAwait(false) == receipt,
            "Recovery command replay lost its original receipt after progress.");
        var retained = await FailureAsync(store, row, generation).ConfigureAwait(false);
        Require(retained.RecoveryReceipt == receipt && retained.Token == original.Token && retained.Message.MessageBody == row.MessageBody,
            "Successful recovery erased original quarantine provenance.");
    }

    /// <summary>Verifies the write-gap guard, administrative retention and tenant-bounded detached reads.</summary>
    /// <returns>The arm.</returns>
    public virtual async Task UnsettledWorkAndTenantInventoryRemainProtected()
    {
        var store = CreateStore();
        var (row, generation) = await SeedAsync(store).ConfigureAwait(false);
        var admission = await AcquireAsync(store, row, generation, 2).ConfigureAwait(false);
        var refused = false;
        try { await store.StoreAsync(row, CancellationToken.None).ConfigureAwait(false); }
        catch (InvalidOperationException) { refused = true; }
        Require(refused, "Administrative replacement erased captured work before failure persistence.");
        await store.CompleteAsync(row.Id, CancellationToken.None).ConfigureAwait(false);
        var quarantine = await FailureAsync(store, row, generation).ConfigureAwait(false);
        Require(quarantine.Disposition == ScheduleFailureDisposition.Quarantined
            && quarantine.Reason == ScheduleFailureReason.AdministrativeCancellation && quarantine.AdmissionAttempts == 1,
            "Administrative completion lost interrupted work or fabricated handler failure.");
        Require(!await store.TryCompleteExecutionAsync(admission.Lease, new(row.NextExecutionUtc, row.NextExecutionUtc!.Value.AddMinutes(1)),
            ScheduleExecutionCompletionDisposition.Fulfilled, CancellationToken.None).ConfigureAwait(false), "Old active owner settled after administrative quarantine.");
        quarantine.Message.MessageBody = "caller mutation";
        Require((await FailureAsync(store, row, generation).ConfigureAwait(false)).Message.MessageBody == row.MessageBody,
            "Failure observation leaked mutable stored envelope.");
        Require((await store.GetQuarantinesAsync(row.TenantId + "other", null, 1, CancellationToken.None).ConfigureAwait(false)).Count == 0,
            "Quarantine inventory crossed its requested tenant partition.");
        var page = await store.GetQuarantinesAsync(row.TenantId, null, 1, CancellationToken.None).ConfigureAwait(false);
        Require(page.Count == 1 && page[0].Token == quarantine.Token, "Bounded quarantine inventory hid retained work.");
        Require((await store.GetQuarantinesAsync(row.TenantId, page[0].Token.QuarantineId, 1, CancellationToken.None).ConfigureAwait(false)).Count == 0,
            "Exclusive quarantine cursor repeated its boundary.");
    }

    /// <summary>Verifies cancellation and stale authority cannot create a failure record.</summary>
    /// <returns>The arm.</returns>
    public virtual async Task FailureMutationRequiresCurrentUncancelledAuthority()
    {
        var store = CreateStore();
        var (row, generation) = await SeedAsync(store).ConfigureAwait(false);
        var admission = await AcquireAsync(store, row, generation, 2).ConfigureAwait(false);
        var failure = new ScheduleOccurrenceFailure(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);
        var observedCancellation = false;
        try { await store.TryRecordFailureAsync(admission.Lease, failure, cancelled.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { observedCancellation = true; }
        Require(observedCancellation, "Cancellation was ignored before failure mutation.");
        Require(await store.GetOccurrenceFailureAsync(row.Id, generation, row.NextExecutionUtc!.Value, row.TenantId,
            CancellationToken.None).ConfigureAwait(false) is null, "Pre-cancelled failure write mutated state.");
        var stale = admission.Lease with { };
        await store.ReleaseExecutionAsync(stale, CancellationToken.None).ConfigureAwait(false);
        Require(!await store.TryRecordFailureAsync(stale, failure, CancellationToken.None).ConfigureAwait(false), "Released authority created failure history.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new TestFixtureAssertionException(message); }
    }

    private static async Task<(IScheduledMessage Row, Guid Generation)> SeedAsync(IScheduleExecutionFailureStore store)
    {
        var row = new ScheduledMessage
        {
            Id = Guid.NewGuid(), Enabled = true, NextExecutionUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Interval = TimeSpan.FromMinutes(1), MessageName = "conformance.retained.type", MessageBody = "{raw malformed envelope",
            CorrelationId = "producer-correlation", TenantId = Guid.NewGuid().ToString("N"), TraceParent = "trace", UserId = "user", TimeZoneId = "UTC",
        };
        await store.StoreAsync(row, CancellationToken.None).ConfigureAwait(false);
        return (row, (await store.GetExecutionSnapshotsAsync(CancellationToken.None).ConfigureAwait(false)).Single(s => s.Message.Id == row.Id).Generation);
    }

    private static async Task<ScheduleFailureExecutionAdmission> AcquireAsync(IScheduleExecutionFailureStore store,
        IScheduledMessage row, Guid generation, int maximum) => await store.TryAcquireExecutionAsync(row.Id, generation,
            row.NextExecutionUtc!.Value, maximum, CancellationToken.None).ConfigureAwait(false)
        ?? throw new TestFixtureAssertionException("Current due work was not admitted.");

    private static async Task<ScheduleOccurrenceFailureSnapshot> FailureAsync(IScheduleExecutionFailureStore store,
        IScheduledMessage row, Guid generation) => await store.GetOccurrenceFailureAsync(row.Id, generation,
            row.NextExecutionUtc!.Value, row.TenantId, CancellationToken.None).ConfigureAwait(false)
        ?? throw new TestFixtureAssertionException("Retained occurrence failure was not queryable.");
}
