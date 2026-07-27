// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations that support an atomic
/// claim-before-execute idempotency protocol: reserve a message for processing, then either finalize
/// it on success or release it on failure.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IInboxStore"/>)
/// so that <see cref="IInboxStore"/> stays within the Interface Segregation threshold. Inbox stores that
/// support atomic claiming implement this interface in addition to <see cref="IInboxStore"/>.
/// </para>
/// <para>
/// The protocol makes idempotent handling correct under concurrent duplicate delivery <b>without</b>
/// dropping a message whose handler fails:
/// </para>
/// <list type="number">
/// <item><description><see cref="TryClaimAsync(string, string, CancellationToken)"/> atomically before the handler runs. <see langword="false"/> means another caller already holds the claim (duplicate) — skip.</description></item>
/// <item><description>On handler success, finalize the claim via <see cref="IInboxStore.MarkProcessedAsync"/> (the entry becomes terminal <see cref="InboxStatus.Processed"/>).</description></item>
/// <item><description>On handler failure, <see cref="ReleaseAsync"/> the claim so a redelivery can re-admit the message. Leaving a terminal entry on failure would silently drop the message.</description></item>
/// </list>
/// <para>
/// The steps above describe the two-argument <see cref="TryClaimAsync(string, string, CancellationToken)"/>
/// protocol (caller-governed TTL, explicit release-on-failure). The self-expiring lease overload
/// <see cref="TryClaimAsync(string, string, TimeSpan, CancellationToken)"/> uses a different failure model:
/// a failed handler leaves the entry <see cref="InboxStatus.Failed"/> and a redelivery re-admits it for
/// retry — no explicit release call. In both models only <see cref="InboxStatus.Processed"/> is terminal,
/// so a handler failure never silently drops the message.
/// </para>
/// </remarks>
public interface IClaimableInboxStore
{
	/// <summary>
	/// Atomically claims a message for a specific handler by inserting a non-terminal
	/// (<see cref="InboxStatus.Processing"/>) entry if and only if none already exists.
	/// </summary>
	/// <remarks>
	/// This is a single atomic "first writer wins" operation (e.g. <c>INSERT … ON CONFLICT DO NOTHING</c>,
	/// <c>INSERT … WHERE NOT EXISTS</c>, or an atomic add). It replaces the racy check-then-act of a
	/// separate <see cref="IInboxStore.IsProcessedAsync"/> followed by a later mark.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller created the claim (first writer, proceed to handle);
	/// <see langword="false"/> if the message is already claimed or processed (duplicate, skip).
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically claims a message for a specific handler under a self-expiring <b>lease</b>: the claim
	/// succeeds if no entry exists, the entry is <see cref="InboxStatus.Received"/>, or the entry is
	/// <see cref="InboxStatus.Processing"/> but its lease has expired (reclaiming a dead processor's stuck
	/// claim). A terminal <see cref="InboxStatus.Processed"/> entry is never reclaimed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the full-mode, self-governing counterpart to the two-argument
	/// <see cref="TryClaimAsync(string, string, CancellationToken)"/> (which never auto-expires — the caller
	/// governs TTL there). Collapsing admission and expired-lease reclaim into a <b>single</b> atomic
	/// compare-and-set makes both the double-admission race and the permanent-stuck-claim inexpressible.
	/// </para>
	/// <para>
	/// The <c>leaseExpiry &lt; now</c> comparison MUST be evaluated against the <b>store's own server clock</b>
	/// inside the atomic operation (SQL <c>SYSUTCDATETIME()</c>/<c>now()</c>, Redis <c>TIME</c>, Mongo
	/// <c>$$NOW</c>) — never an app-side clock — because the invariant is distributed across competing
	/// processors and only the store's single clock is skew-free. <paramref name="leaseDuration"/> governs the
	/// app-side lease length only.
	/// </para>
	/// <para>
	/// A <see cref="InboxStatus.Failed"/> entry is <b>re-admittable</b>: a handler failure leaves the entry
	/// Failed, and a redelivery re-claims it so processing is retried. This keeps the atomic lease-claim
	/// path consistent with the non-lease admission path, so a handler failure never silently drops the
	/// message. Only <see cref="InboxStatus.Processed"/> is terminal for claiming.
	/// </para>
	/// <para>
	/// Lease-based claiming is an optional capability; a store that does not implement it throws
	/// <see cref="NotSupportedException"/>.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="leaseDuration">How long the claim is held before it becomes reclaimable by another processor.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller acquired the claim, reclaimed an expired lease, or re-admitted a
	/// previously <see cref="InboxStatus.Failed"/> entry for retry (it is now the sole processor);
	/// <see langword="false"/> if a live lease is held by another caller, or the entry is terminal
	/// <see cref="InboxStatus.Processed"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	/// <exception cref="NotSupportedException">Thrown when the store does not support lease-based claiming.</exception>
	ValueTask<bool> TryClaimAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			"This inbox store does not support lease-based claiming (the TryClaimAsync lease overload). " +
			"Use a store that implements it (SqlServer, Postgres, Redis, or MongoDB) or the two-argument claim.");

	/// <summary>
	/// Releases a previously acquired claim for a specific handler by removing the entry, so that a
	/// redelivery of the same message can be re-admitted.
	/// </summary>
	/// <remarks>
	/// Call this when the handler fails after a successful <see cref="TryClaimAsync(string, string, CancellationToken)"/>. Releasing an
	/// already-removed or never-claimed entry is a no-op. Do not call after the claim has been finalized
	/// via <see cref="IInboxStore.MarkProcessedAsync"/>.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous release operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken);
}
