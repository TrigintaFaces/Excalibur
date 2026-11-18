// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Applies persisted ordinary saga timer intents under retained receiver authority.</summary>
/// <remarks>
/// Optional facet discovered through selected ISagaTimeoutStore.GetService. It does not advertise
/// atomic saga/outbox transitions. Receiver domain is exact tenant+sagaId, NEVER state type. State-type
/// conflicts are refused within that domain, not split into cancellation islands. Existing three timer
/// providers remain supported; no default provider refusal or mandatory lifetime quota is implied.
/// All timer mutations and receipt/fence updates share the existing provider atomic boundary.
/// Own-batch ordinal order is independent of external cancellation cutoffs: Schedule(T),Cancel(T)
/// leaves no T; CancelAll,Schedule(U) retains U. State ack CAS binds stored incarnation AND version
/// AND pending batch, and purge atomically excludes pending journals. A prior read-check is insufficient.
/// Fence token stays stable within an incarnation; only first application of a new state incarnation
/// rotates it. No automatic journal rebinding is part of ordinary recovery.
/// </remarks>
public interface ISagaTimerRecoveryStore
{
    /// <summary>Observes or establishes retained receiver-local authority for the exact domain.</summary>
    /// <param name="domain">Exact tenant and coordinator saga ID, aligned with all public cancel paths.</param>
    /// <param name="cancellationToken">Cancellation for this attempt.</param>
    /// <returns>Known opaque incarnation/batch fence and mutation revision.</returns>
    /// <remarks>
    /// Establish an unpredictable retained token even before the first timer mutation; another receiver
    /// must not accept a foreign initial journal. Never represent lookup/storage failure as absence.
    /// Observe the actual constructor-selected receiver AFTER state load, then revalidate the same
    /// state incarnation/version/absence before handler/new journal capture. Pair again after recovery
    /// acknowledgement; never refresh a persisted journal token or its original cancellation revision. Retain authority after payload/state deletion and
    /// purge; no TTL, nonce reuse or hidden global quota. Normal provider storage pressure stays explicit.
    /// </remarks>
    ValueTask<SagaTimerRecoveryFence> ObserveFenceAsync(SagaTimerRecoveryDomain domain, CancellationToken cancellationToken);

    /// <summary>Atomically applies the original batch with known per-intent cancellation/protection results.</summary>
    /// <param name="journal">Original journal persisted with state/dedup, including captured token/revision.</param>
    /// <param name="cancellationToken">Cancellation for this attempt.</param>
    /// <returns>Complete known receipt, positively superseded authority, revalidation, or explicit unknown.</returns>
    /// <remarks>
    /// Verify full frame, configured limits and exact immutable receipt digest before mutation. Known
    /// duplicate means exact current incarnation/sequence/batch+digest, not an arbitrary older request.
    /// Public CancelOne records retained authority for ONLY its matching ID; other scheduled IDs remain.
    /// Public CancelAll records a scope-wide cutoff. Later cancellation versus captured Schedule produces
    /// Cancelled for that intent while other intents apply. Captured Cancel/CancelAll affect only their
    /// captured authority; newer timers are preserved and reported ProtectedNewerTimer. No blind rebase
    /// over cancellation. Cancellation/claim retirement retain authority after physical row deletion.
    /// Raw public Schedule cannot overwrite a recovery-tracked ID, even after cancellation/retirement;
    /// refuse that overwrite or require the exact current conditional authority. New untracked IDs keep
    /// normal public scheduling semantics. Public mutations must share scope lock/transaction and revision.
    /// No scalar fence change may be interpreted as successful cancel or application. RevalidationRequired
    /// cannot acknowledge the journal. Superseded must carry a known later fence and still requires current
    /// saga-state revalidation before abandoning any old local attempt. A current unresolved journal is
    /// never erased merely because a different receiver incarnation exists. Submission cancellation may
    /// be ambiguous; Unknown/throw retains original IDs/payload/journal. SQL atomic profile is unaffected.
    /// </remarks>
    ValueTask<SagaTimerRecoveryApplyResult> ApplyAsync(SagaTimerRecoveryJournal journal, CancellationToken cancellationToken);

    /// <summary>Validates original queued timer authority, independent of payload-row presence.</summary>
    /// <param name="delivery">The original scope/incarnation/sequence/ID carried from the claim.</param>
    /// <param name="cancellationToken">Cancellation for this read.</param>
    /// <returns>False only for positively cancelled/superseded authority; lookup/corruption must throw.</returns>
    /// <remarks>Normal queue-acceptance retirement alone is not obsolete. New state incarnation is.</remarks>
    ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken);
}
