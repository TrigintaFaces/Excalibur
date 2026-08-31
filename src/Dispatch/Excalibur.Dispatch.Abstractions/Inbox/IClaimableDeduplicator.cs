// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInMemoryDeduplicator"/> implementations that support an atomic
/// claim-before-execute idempotency protocol, the in-memory analogue of <see cref="IClaimableInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of
/// <see cref="IInMemoryDeduplicator"/>) so that <see cref="IInMemoryDeduplicator"/> stays within the
/// Interface Segregation threshold. Deduplicators that support atomic claiming implement this interface
/// in addition to <see cref="IInMemoryDeduplicator"/>.
/// </para>
/// <para>
/// The protocol replaces the racy check-then-act of a separate
/// <see cref="IInMemoryDeduplicator.IsDuplicateAsync"/> followed by a later mark: claim atomically before
/// the handler runs, and release the claim if the handler fails so a redelivery can re-admit the message.
/// </para>
/// </remarks>
public interface IClaimableDeduplicator
{
	/// <summary>
	/// Atomically claims a message id if and only if it is not already present, marking it for the
	/// specified retention window.
	/// </summary>
	/// <remarks>
	/// This is a single atomic "first writer wins" operation. A successful claim doubles as the
	/// dedup marker: on handler success no further call is required; on handler failure call
	/// <see cref="ReleaseAsync"/> to remove it.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="expiry">How long to remember the message id once claimed.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A <see cref="LeaseToken"/> identifying this holder if the caller created the claim (first writer,
	/// proceed to handle); <see langword="null"/> if the message id is already present (duplicate, skip).
	/// </returns>
	Task<LeaseToken?> TryClaimAsync(string messageId, TimeSpan expiry, CancellationToken cancellationToken);

	/// <summary>
	/// Releases a previously acquired claim by removing the message id, so that a redelivery of the same
	/// message can be re-admitted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Call this when the handler fails after a successful <see cref="TryClaimAsync"/>. Releasing an
	/// already-removed or never-claimed message id is a no-op.
	/// </para>
	/// <para>
	/// <b>Release names what it releases.</b> The claim carries an expiry, so a caller that outran it may
	/// find the id already re-claimed by a second caller. Removing by id alone would delete that live
	/// claim and admit the same message twice; presenting the term makes releasing someone else's claim
	/// inexpressible.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="claim">The term returned by <see cref="TryClaimAsync"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller still held the claim and it was removed; <see langword="false"/>
	/// if the claim had already lapsed and been taken by another caller, or was never present.
	/// </returns>
	Task<bool> ReleaseAsync(string messageId, LeaseToken claim, CancellationToken cancellationToken);
}
