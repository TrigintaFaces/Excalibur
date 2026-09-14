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
/// moves it to a status that every store's claim predicate excludes. Excluding it from the claim is not on its
/// own sufficient: the message must also not be taken back OUT of that status by a later completion, and that
/// obligation — including that dead-lettered counts as terminal — is stated on
/// <see cref="IOutboxStore.MarkFailedAsync"/> and binds this transition too.
/// </para>
/// </remarks>
public interface IDeadLetterableOutboxStore
{
	/// <summary>
	/// Durably transitions the specified message to the terminal <see cref="OutboxStatus.DeadLettered"/>
	/// status after its retry policy is exhausted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// After this transition the message MUST NOT be returned by any claim predicate
	/// (<see cref="IOutboxStore.GetUnsentMessagesAsync"/> or <c>IOutboxStoreAdmin.GetAllTenantsFailedMessagesAsync</c>),
	/// preventing re-delivery.
	/// </para>
	/// <para>
	/// <b>Whether the row is also REMOVED depends on the store's idiom, and the difference is visible to
	/// an operator.</b> A store that treats the terminal transition as a move copies the message to a
	/// dedicated dead-letter table and deletes the outbox row, so the outbox does not grow. A store that
	/// records the terminal state in a status column leaves the row in place; re-delivery is still
	/// prevented, but the row remains and this interface offers nothing that enumerates or removes it.
	/// Plan retention for that case rather than assuming the transition bounds the table.
	/// </para>
	/// <para>
	/// <b>This member marks the row. It does NOT put the message anywhere a consumer can read it back.</b>
	/// The readable dead-letter surface is a separate contract with its own enumerate, count and replay
	/// members, and the framework's own drain writes to BOTH: it enqueues the message there and then calls
	/// this member. A caller invoking this member directly gets only the second half, so the message is
	/// buried and unreadable even on a store whose idiom moves the row. The same gap opens when the
	/// composition resolves a no-op dead-letter queue, which discards the message and logs, while this
	/// member still records the terminal state.
	/// </para>
	/// <para>
	/// So: if the message must remain recoverable, write it to the dead-letter queue before calling this,
	/// exactly as the drain does. Nothing here does it for you, and nothing reports that it was not done.
	/// </para>
	/// <para>
	/// <b>A TERMINAL message is never reopened by this member either, whoever reports it.</b> The same
	/// obligation the base completion carries applies here and has to be restated, because this interface
	/// is composition rather than inheritance: nothing an implementor reads on <see cref="IOutboxStore"/>
	/// reaches this member. A store MUST NOT dead-letter a message that is already sent or already
	/// dead-lettered, and that binds a report from the CURRENT claim holder exactly as it binds a stale
	/// one. Dead-lettering a delivered message records a delivery failure that did not happen; repeating
	/// it writes a second dead-letter row for one message, and either redrive then produces a duplicate.
	/// </para>
	/// <para>
	/// <b>Clearing the delivery lease is not hygiene — it is load-bearing and it cuts both ways.</b> It
	/// stops a stale sweep resurrecting the message, and it also removes the only evidence a LATER
	/// completion could be judged against, so an ownership guard alone cannot protect this row afterwards.
	/// The terminal-status obligation above is what remains, which is why it is stated and not implied.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">A human-readable reason describing why the message was dead-lettered.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous dead-letter transition.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken);
}
