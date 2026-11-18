// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>Detached immutable observation of ONE failed dispatch, retained across hold reconciliation/rebinding.</summary>
public sealed class SagaTimeoutFailureObservation
{
    private readonly ClaimedSagaTimeout _originalClaim;

    /// <summary>Captures original claim and bytes once; both epochs come from the same claim statement.</summary>
    /// <param name="originalClaim">The original failed dispatch claim.</param>
    /// <param name="kind">Permanent, Poison or Unknown.</param>
    /// <param name="reason">Diagnostic text of at most 1024 UTF-16 code units.</param>
    /// <remarks>Only Permanent, Poison and Unknown are hold reasons. Diagnostic reason is bounded to 1024 UTF-16 units.</remarks>
    public SagaTimeoutFailureObservation(ClaimedSagaTimeout originalClaim, MessageFailureKind kind, string reason)
    {
        ArgumentNullException.ThrowIfNull(originalClaim);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(originalClaim.Timeout);
        ArgumentException.ThrowIfNullOrEmpty(originalClaim.ClaimToken);
        if (string.IsNullOrEmpty(originalClaim.SourceEpoch) || string.IsNullOrEmpty(originalClaim.DeliveryEpoch))
        { throw new ArgumentException("The selected claim did not project source lifetime and delivery-cycle epochs.", nameof(originalClaim)); }
        if (kind is not (MessageFailureKind.Permanent or MessageFailureKind.Poison or MessageFailureKind.Unknown))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }
        if (reason.Length > 1024) { throw new ArgumentOutOfRangeException(nameof(reason)); }
        _originalClaim = CopyClaim(originalClaim);
        Kind = kind;
        Reason = reason;
    }

    /// <summary>Gets a detached copy of the original failed dispatch claim.</summary>
    public ClaimedSagaTimeout OriginalClaim => CopyClaim(_originalClaim);
    /// <summary>Gets the observed nonretryable classification.</summary>
    public MessageFailureKind Kind { get; }
    /// <summary>Gets bounded diagnostic text, independent of the original payload.</summary>
    public string Reason { get; }

    internal static ClaimedSagaTimeout CopyClaim(ClaimedSagaTimeout claim) =>
        claim with { Timeout = claim.Timeout with { TimeoutData = claim.Timeout.TimeoutData?.ToArray() } };
}

