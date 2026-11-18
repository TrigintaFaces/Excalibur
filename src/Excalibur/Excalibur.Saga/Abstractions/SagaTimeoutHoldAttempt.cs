// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Saga.Abstractions;

/// <summary>Conditional hold outcome with an optional truthful receipt; no payload is fabricated.</summary>
public sealed class SagaTimeoutHoldAttempt
{
    /// <summary>Initializes a validated conditional hold result.</summary>
    /// <param name="outcome">The provider-certified result.</param>
    /// <param name="receipt">Required current Held receipt for Held; optional non-Held receipt for ended observation.</param>
    public SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome outcome, SagaTimeoutHoldReceipt? receipt = null)
    {
        if (!Enum.IsDefined(outcome)) { throw new ArgumentOutOfRangeException(nameof(outcome)); }
        if (outcome == SagaTimeoutHoldOutcome.Held && receipt?.State != SagaTimeoutHoldState.Held)
        { throw new ArgumentException("Held requires an actual current held receipt.", nameof(receipt)); }
        if ((outcome is SagaTimeoutHoldOutcome.RetryWithFreshClaim or SagaTimeoutHoldOutcome.Rejected) && receipt is not null)
        { throw new ArgumentException("A refused claim/input does not certify a hold receipt.", nameof(receipt)); }
        if (outcome == SagaTimeoutHoldOutcome.ObservationEnded && receipt?.State == SagaTimeoutHoldState.Held)
        { throw new ArgumentException("An ended observation cannot certify a current hold.", nameof(receipt)); }
        Outcome = outcome;
        Receipt = receipt;
    }
    /// <summary>Gets the provider-certified result; only Held or ObservationEnded resolves local pending observation.</summary>
    public SagaTimeoutHoldOutcome Outcome { get; }
    /// <summary>Gets the real current/historical receipt when supplied, otherwise null.</summary>
    public SagaTimeoutHoldReceipt? Receipt { get; }
}
