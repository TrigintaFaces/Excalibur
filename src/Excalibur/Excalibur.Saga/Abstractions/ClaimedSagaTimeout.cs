// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
public sealed record ClaimedSagaTimeout(SagaTimeout Timeout, string ClaimToken);
