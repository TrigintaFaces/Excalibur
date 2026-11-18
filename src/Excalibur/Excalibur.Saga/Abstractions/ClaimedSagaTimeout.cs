// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Abstractions;

/// <summary>
/// A saga timeout together with the claim the caller holds on it.
/// </summary>
/// <param name="Timeout">The timeout that was claimed.</param>
/// <param name="ClaimToken">
/// The token identifying this particular claim. Opaque to the caller: only the store that issued it
/// interprets it, and callers must not construct, parse or reuse one.
/// </param>
/// <remarks>
/// <para>
/// <strong>This type exists so that retiring a timeout you do not hold is not expressible.</strong>
/// <see cref="ISagaTimeoutStore.MarkDeliveredAsync"/> takes a claim rather than an identifier, and only
/// <see cref="ISagaTimeoutStore.ClaimDueTimeoutsAsync"/> produces one. The diagnostic read,
/// <see cref="ISagaTimeoutStore.GetDueTimeoutsAsync"/>, returns bare timeouts and claims nothing, so a
/// caller cannot hand a monitoring result to the retirement call even by mistake — it does not compile.
/// </para>
/// <para>
/// The token is per claim, not per processor. Two claims of the same timeout by the same process, one
/// after a lease expiry, carry different tokens; that is the distinction the retirement predicate turns
/// on, and a token that merely identified the process could not express it.
/// </para>
/// <para>
/// This follows the shape a message broker uses for a leased message — the received message carries the
/// lock that settles it, and a peeked message cannot be settled at all.
/// </para>
/// </remarks>
public sealed record ClaimedSagaTimeout(SagaTimeout Timeout, string ClaimToken)
{
    /// <summary>Initializes a claim carrying original immutable owned-timeout authority captured by the claim statement.</summary>
    /// <param name="timeout">The actual claimed payload row.</param>
    /// <param name="claimToken">The actual opaque acquisition token.</param>
    /// <param name="ownedDelivery">The original authority projected from that same row.</param>
    public ClaimedSagaTimeout(SagaTimeout timeout, string claimToken, SagaTimeoutDeliveryIdentity ownedDelivery)
        : this(timeout, claimToken)
    {
        ArgumentNullException.ThrowIfNull(timeout);
        ArgumentNullException.ThrowIfNull(ownedDelivery);
        if (!string.Equals(timeout.TimeoutId, ownedDelivery.TimeoutId, StringComparison.Ordinal)
            || !Guid.TryParse(timeout.SagaId, out var sagaId) || sagaId != ownedDelivery.Scope.SagaId
            || !string.Equals(KeyedTenantPartition.FromStoredValue(timeout.TenantId).TenantId,
                ownedDelivery.Scope.TenantId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Claimed timeout row differs from its original owned authority.", nameof(ownedDelivery));
        }
        OwnedDelivery = ownedDelivery;
    }

    /// <summary>Gets original owned authority, or null for an explicitly nonowned ordinary-store row.</summary>
    /// <remarks>Null never grants manual admission to a durable receiver.</remarks>
    public SagaTimeoutDeliveryIdentity? OwnedDelivery { get; }
    /// <summary>Gets the opaque source-row lifetime epoch projected by the same atomic claim statement.</summary>
    /// <remarks>
    /// Null means the claim lacks preserving hold capability; hosted validation must reject that path.
    /// The selected store assigns a fresh epoch to every newly scheduled/replaced source lifetime, including
    /// unowned ordinary timers and equal-ID/equal-payload replacements. Reclaims of that SAME source retain it.
    /// Never derive it solely from ID, payload hash, due date or process identity. This is expected-state
    /// metadata, not a credential; store validation remains mandatory. Existing owned incarnation is also checked.
    /// </remarks>
    public string? SourceEpoch { get; init; }

    /// <summary>Gets the opaque persisted delivery/disposition cycle projected by the SAME atomic claim statement.</summary>
    /// <remarks>
    /// Assign on source creation and atomically advance on EVERY successful resume; reclaims without resume
    /// preserve it. SourceEpoch remains the independent source-row lifetime. The failed observation binds BOTH.
    /// Same-command resume replay must not advance this cycle twice. Old observations and fresh operation IDs
    /// cannot hold a later delivery cycle, even after earlier receipts have been evicted. Null is unsupported
    /// for hosted preserving delivery. This is expected-state metadata, not an authentication credential.
    /// </remarks>
    public string? DeliveryEpoch { get; init; }
}
