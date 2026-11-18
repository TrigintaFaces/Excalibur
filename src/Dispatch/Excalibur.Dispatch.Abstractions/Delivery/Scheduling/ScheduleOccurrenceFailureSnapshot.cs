// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Detached failure provenance and an optional immutable recovery receipt.</summary>
public sealed class ScheduleOccurrenceFailureSnapshot
{
    private readonly IScheduledMessage _message;

    /// <summary>Initializes a detached failure observation.</summary>
    /// <param name="token">Stable failure identity; quarantine identifiers are provider-owned.</param>
    /// <param name="reason">Bounded failure reason.</param>
    /// <param name="disposition">Current failure disposition.</param>
    /// <param name="admissionAttempts">Consumed grants, not proven handler calls; zero is valid for explicitly cancelled recovery before its first grant.</param>
    /// <param name="maxAdmissionAttempts">Immutable automatic budget.</param>
    /// <param name="recordedUtc">Authority-recorded timestamp.</param>
    /// <param name="message">Original complete envelope.</param>
    /// <param name="recoveryReceipt">Persisted recovery result, if resolved.</param>
    public ScheduleOccurrenceFailureSnapshot(ScheduleQuarantineToken token, ScheduleFailureReason reason,
        ScheduleFailureDisposition disposition, int admissionAttempts, int maxAdmissionAttempts,
        DateTimeOffset recordedUtc, IScheduledMessage message, ScheduleQuarantineRecoveryReceipt? recoveryReceipt = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(admissionAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAdmissionAttempts);
        Token = token;
        Reason = reason;
        Disposition = disposition;
        AdmissionAttempts = admissionAttempts;
        MaxAdmissionAttempts = maxAdmissionAttempts;
        RecordedUtc = recordedUtc;
        _message = new ScheduleExecutionSnapshot(token.Generation, message).Message;
        RecoveryReceipt = recoveryReceipt;
    }

    /// <summary>Gets the stable provider-owned identity.</summary>
    public ScheduleQuarantineToken Token { get; }
    /// <summary>Gets the bounded reason.</summary>
    public ScheduleFailureReason Reason { get; }
    /// <summary>Gets the confirmed disposition.</summary>
    public ScheduleFailureDisposition Disposition { get; }
    /// <summary>Gets the count of consumed admissions.</summary>
    public int AdmissionAttempts { get; }
    /// <summary>Gets the captured automatic budget.</summary>
    public int MaxAdmissionAttempts { get; }
    /// <summary>Gets the provider-clock observation.</summary>
    public DateTimeOffset RecordedUtc { get; }
    /// <summary>Gets an independently detached envelope copy.</summary>
    public IScheduledMessage Message => new ScheduleExecutionSnapshot(Token.Generation, _message).Message;
    /// <summary>Gets the immutable recovery receipt if this quarantine was resumed.</summary>
    public ScheduleQuarantineRecoveryReceipt? RecoveryReceipt { get; }
}
