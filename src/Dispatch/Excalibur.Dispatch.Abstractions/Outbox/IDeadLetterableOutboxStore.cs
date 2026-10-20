// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// <para>The count describes logical attempts already observed by the caller, not physical transport requests.
	/// Only an applied transition may change it: the same atomic operation records max(stored, supplied).
	/// Missing and already-terminal messages retain their count; zero is valid for a pre-dispatch poison message.</para>
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
	/// <para>
	/// <b>THE REFUSAL IS REPORTED, NEVER THROWN, AND THE CALLER MUST TEST FOR
	/// <see cref="OutboxCompletionOutcome.Applied"/> BY NAME.</b> Every guard an implementation runs here
	/// — the row is gone, the row is already terminal, the lease belongs to someone else — ends in a write
	/// that did not happen, and the caller has already written the dead-letter entry by the time it asks.
	/// A refusal it cannot see leaves the message simultaneously delivered by the live claimant and sitting
	/// unreplayed in the dead-letter queue, where a redrive re-executes a message that already succeeded.
	/// Deciding success by excluding the refusals instead makes every member added later a success, which
	/// is the direction this contract must never fail.
	/// </para>
	/// <para>
	/// <b>Reachable outcomes.</b> <see cref="OutboxCompletionOutcome.Applied"/> when the row was mutated;
	/// <see cref="OutboxCompletionOutcome.MessageNotFound"/> when no row matched;
	/// <see cref="OutboxCompletionOutcome.AlreadyTerminal"/> when the row was already sent or already
	/// dead-lettered; <see cref="OutboxCompletionOutcome.ClaimLost"/> when the store holds a delivery lease
	/// for a different owner. <see cref="OutboxCompletionOutcome.FenceRefused"/> is NOT reachable — this
	/// member carries no fencing token, so an implementation returning it would report a judgement it was
	/// given no means to make. <see cref="OutboxCompletionOutcome.Unknown"/> must never be returned by a
	/// store; it exists so that a default or unset value cannot read as success.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">A human-readable reason describing why the message was dead-lettered.</param>
	/// <param name="attemptCount">Observed logical delivery attempts. Nonnegative; an applied transition atomically records the maximum of this value and the stored count.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The outcome of the transition. Only <see cref="OutboxCompletionOutcome.Applied"/> means the row was mutated.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown before mutation when <paramref name="attemptCount"/> is negative.</exception>
	ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, CancellationToken cancellationToken);
}
