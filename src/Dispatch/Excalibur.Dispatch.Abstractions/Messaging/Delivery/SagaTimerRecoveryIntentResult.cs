// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>States the known disposition of one original timer intent.</summary>
public enum SagaTimerRecoveryIntentOutcome
{
    /// <summary>The requested mutation applied under its authority.</summary>
    Applied = 0,
    /// <summary>A retained later cancellation positively cancelled this original schedule.</summary>
    Cancelled = 1,
    /// <summary>A captured cancellation preserved a timer established under newer authority.</summary>
    ProtectedNewerTimer = 2,
}

/// <summary>Retains a provider-established per-intent result instead of claiming cancelled work applied.</summary>
public sealed class SagaTimerRecoveryIntentResult
{
    /// <summary>Creates the exact original intent disposition.</summary>
    /// <param name="ordinal">Original zero-based intent position.</param>
    /// <param name="action">Original action, not a replacement mutation.</param>
    /// <param name="timeoutId">Exact original ID; empty only for CancelAll.</param>
    /// <param name="outcome">The positively established disposition.</param>
    /// <param name="authorityRevision">Provider revision establishing the result.</param>
    public SagaTimerRecoveryIntentResult(int ordinal, SagaPreparedTimerAction action, string timeoutId,
        SagaTimerRecoveryIntentOutcome outcome, long authorityRevision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegative(authorityRevision);
        ArgumentNullException.ThrowIfNull(timeoutId);
        if (!Enum.IsDefined(action)) { throw new ArgumentOutOfRangeException(nameof(action)); }
        if (!Enum.IsDefined(outcome)) { throw new ArgumentOutOfRangeException(nameof(outcome)); }
        if (action == SagaPreparedTimerAction.CancelAll)
        {
            if (timeoutId.Length != 0) { throw new ArgumentException("CancelAll has no individual ID.", nameof(timeoutId)); }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutId.Length, 450, nameof(timeoutId));
        }
        if (outcome == SagaTimerRecoveryIntentOutcome.Cancelled && action != SagaPreparedTimerAction.Schedule)
        {
            throw new ArgumentException("Only a scheduled intent can have a cancellation disposition.", nameof(outcome));
        }
        if (outcome == SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer && action == SagaPreparedTimerAction.Schedule)
        {
            throw new ArgumentException("ProtectedNewerTimer describes a captured cancellation.", nameof(outcome));
        }
        Ordinal = ordinal; Action = action; TimeoutId = timeoutId; Outcome = outcome; AuthorityRevision = authorityRevision;
    }
    /// <summary>Gets the original intent position.</summary>
    public int Ordinal { get; }
    /// <summary>Gets the original action.</summary>
    public SagaPreparedTimerAction Action { get; }
    /// <summary>Gets the exact original timer ID.</summary>
    public string TimeoutId { get; }
    /// <summary>Gets its known disposition.</summary>
    public SagaTimerRecoveryIntentOutcome Outcome { get; }
    /// <summary>Gets the provider revision establishing that disposition.</summary>
    public long AuthorityRevision { get; }
}
