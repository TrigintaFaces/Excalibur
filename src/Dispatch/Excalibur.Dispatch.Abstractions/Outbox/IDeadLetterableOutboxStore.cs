// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for polling <see cref="IOutboxStore"/> implementations that durably transition a
/// retry-exhausted message to the terminal <see cref="OutboxStatus.DeadLettered"/> status.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IOutboxStore"/>)
/// so that <see cref="IOutboxStore"/> stays within the Interface Segregation threshold, mirroring how
/// <c>IOutboxStoreAdmin</c> and <c>IOutboxStoreBatch</c> segregate optional outbox capabilities.
/// </para>
/// <para>
/// Without a terminal transition, a message that exhausts its retry policy stays
/// <see cref="OutboxStatus.Failed"/>, is re-claimed by the delivery poller after its lease expires, and is
/// re-delivered and re-dead-lettered forever. Marking the message <see cref="OutboxStatus.DeadLettered"/>
/// moves it to a status that every store's claim predicate structurally excludes, so it can never be
/// re-claimed.
/// </para>
/// </remarks>
public interface IDeadLetterableOutboxStore
{
	/// <summary>
	/// Durably transitions the specified message to the terminal <see cref="OutboxStatus.DeadLettered"/>
	/// status after its retry policy is exhausted.
	/// </summary>
	/// <remarks>
	/// After this transition the message MUST NOT be returned by any claim predicate
	/// (<see cref="IOutboxStore.GetUnsentMessagesAsync"/> or <c>IOutboxStoreAdmin.GetFailedMessagesAsync</c>),
	/// preventing re-delivery and unbounded dead-letter-queue growth. Implementations should also clear any
	/// delivery lease for hygiene.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">A human-readable reason describing why the message was dead-lettered.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous dead-letter transition.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken);
}
