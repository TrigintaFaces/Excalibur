// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Distinguishes known complete receipt, supersession, required state revalidation and uncertainty.</summary>
public enum SagaTimerRecoveryApplyOutcome
{
    /// <summary>All original intents have an atomic, explicit per-intent receipt.</summary>
    Applied = 0,
    /// <summary>The exact current batch/digest already has that receipt; no effect repeated.</summary>
    AlreadyApplied = 1,
    /// <summary>A different batch advanced receiver authority; no implicit rebinding or success.</summary>
    RevalidationRequired = 2,
    /// <summary>Commit acknowledgement remains unknown; retain the original journal.</summary>
    Unknown = 3,
    /// <summary>A positively retained later incarnation or sequence superseded this attempt.</summary>
    Superseded = 4,
}

/// <summary>Returns complete per-intent disposition or a non-success authority observation.</summary>
public sealed class SagaTimerRecoveryApplyResult
{
    private readonly IReadOnlyList<SagaTimerRecoveryIntentResult> _intentResults;
    /// <summary>Creates an atomic receiver observation.</summary>
    /// <param name="outcome">The actual receiver outcome.</param>
    /// <param name="fence">Known current fence; absent only for unknown acknowledgement.</param>
    /// <param name="intentResults">Complete ordered original-intent receipt only for Applied/AlreadyApplied.</param>
    public SagaTimerRecoveryApplyResult(SagaTimerRecoveryApplyOutcome outcome, SagaTimerRecoveryFence? fence,
        IReadOnlyList<SagaTimerRecoveryIntentResult> intentResults)
    {
        ArgumentNullException.ThrowIfNull(intentResults);
        if (!Enum.IsDefined(outcome)) { throw new ArgumentOutOfRangeException(nameof(outcome)); }
        if (outcome == SagaTimerRecoveryApplyOutcome.Unknown != (fence is null))
        {
            throw new ArgumentException("Known outcomes require a fence; unknown must not fabricate one.", nameof(fence));
        }
        var completed = outcome is SagaTimerRecoveryApplyOutcome.Applied or SagaTimerRecoveryApplyOutcome.AlreadyApplied;
        if (completed != (intentResults.Count != 0))
        {
            throw new ArgumentException("Only known complete batches carry a nonempty complete receipt.", nameof(intentResults));
        }
        var copy = intentResults.ToArray();
        for (var ordinal = 0; ordinal < copy.Length; ordinal++)
        {
            ArgumentNullException.ThrowIfNull(copy[ordinal]);
            if (copy[ordinal].Ordinal != ordinal) { throw new ArgumentException("Receipt ordinals must be complete and ordered.", nameof(intentResults)); }
        }
        Outcome = outcome; Fence = fence; _intentResults = Array.AsReadOnly(copy);
    }
    /// <summary>Gets the actual receiver outcome.</summary>
    public SagaTimerRecoveryApplyOutcome Outcome { get; }
    /// <summary>Gets known current authority, or null for unknown acknowledgement.</summary>
    public SagaTimerRecoveryFence? Fence { get; }
    /// <summary>Gets immutable ordered results; cancelled work is never mislabeled as applied.</summary>
    public IReadOnlyList<SagaTimerRecoveryIntentResult> IntentResults => _intentResults;
}
