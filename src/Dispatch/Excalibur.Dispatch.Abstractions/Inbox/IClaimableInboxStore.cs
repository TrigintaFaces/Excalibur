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
/// <item><description><see cref="TryClaimAsync"/> atomically before the handler runs. <see langword="false"/> means another caller already holds the claim (duplicate) — skip.</description></item>
/// <item><description>On handler success, finalize the claim via <see cref="IInboxStore.MarkProcessedAsync"/> (the entry becomes terminal <see cref="InboxStatus.Processed"/>).</description></item>
/// <item><description>On handler failure, <see cref="ReleaseAsync"/> the claim so a redelivery can re-admit the message. Leaving a terminal entry on failure would silently drop the message.</description></item>
/// </list>
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
	/// Releases a previously acquired claim for a specific handler by removing the entry, so that a
	/// redelivery of the same message can be re-admitted.
	/// </summary>
	/// <remarks>
	/// Call this when the handler fails after a successful <see cref="TryClaimAsync"/>. Releasing an
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
