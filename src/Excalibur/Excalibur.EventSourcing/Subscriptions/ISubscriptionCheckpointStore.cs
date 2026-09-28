// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Persists subscription checkpoint positions for durable subscriptions.
/// </summary>
/// <remarks>
/// <para>
/// Checkpoint stores enable event subscriptions to resume from their last known
/// position after a restart. Each subscription is identified by a unique name.
/// </para>
/// <para>
/// Follows the pattern from <c>Azure.Messaging.ServiceBus.ServiceBusProcessor</c>
/// which uses checkpoint-based position tracking for message processing.
/// </para>
/// </remarks>
public interface ISubscriptionCheckpointStore
{
	/// <summary>
	/// Gets the last checkpointed position for a named subscription.
	/// </summary>
	/// <param name="subscriptionName">The unique subscription identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The last stored position, or <see langword="null"/> if no checkpoint exists
	/// (indicating the subscription should start from the beginning).
	/// </returns>
	Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken);

	/// <summary>
	/// Advances the checkpoint for a named subscription, but only while it still holds the position the
	/// caller last observed.
	/// </summary>
	/// <param name="subscriptionName">The unique subscription identifier.</param>
	/// <param name="expectedPosition">
	/// The position the caller believes is stored now, or <see langword="null"/> when the caller believes
	/// no checkpoint exists yet. "No checkpoint" and "a checkpoint of 0" are different states and must
	/// stay distinguishable, which is why this is nullable rather than a sentinel value.
	/// </param>
	/// <param name="newPosition">The position to advance to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="CheckpointAdvanceOutcome.Advanced"/> when the checkpoint moved, or
	/// <see cref="CheckpointAdvanceOutcome.Superseded"/> when another writer had already moved it, in
	/// which case NOTHING was written.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is a compare-and-set rather than a blind write, and the difference is not defensive tidiness.
	/// A blind write lets a reader that paused and resumed -- or a second reader of the same subscription
	/// -- move the checkpoint BACKWARDS to a value it read minutes ago, after which every event between
	/// the two positions is delivered a second time.
	/// </para>
	/// <para>
	/// Implementations MUST perform the comparison and the write as ONE atomic operation against the
	/// durable store: a conditional UPDATE, never a read followed by a write. A read-then-write
	/// implementation reintroduces precisely the interleaving this signature exists to exclude, and it
	/// does so invisibly -- the signature would still look correct.
	/// </para>
	/// <para>
	/// A refusal is REPORTED, never thrown. Losing the race is an expected outcome for a subscription
	/// that has been superseded, and the caller's correct response is to stop rather than to retry. An
	/// operation that can decline has to be able to say so: a method returning nothing here would make
	/// "it advanced" and "it declined" the same observation.
	/// </para>
	/// </remarks>
	Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
		string subscriptionName,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken);

	/// <summary>
	/// Enumerates every stored subscription checkpoint.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// All persisted checkpoints, one per named subscription. Returns an empty list when no
	/// checkpoints have been stored.
	/// </returns>
	/// <remarks>
	/// Enables projection-lag reporting: pairing each subscription's checkpoint with the global
	/// stream head position yields the per-stream lag.
	/// </remarks>
	Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A single stored subscription checkpoint: the named subscription and its last checkpointed position.
/// </summary>
/// <param name="SubscriptionName">The unique subscription identifier.</param>
/// <param name="Position">The last checkpointed global-stream position.</param>
public readonly record struct SubscriptionCheckpoint(string SubscriptionName, long Position);

/// <summary>
/// The result of attempting to advance a subscription checkpoint.
/// </summary>
public enum CheckpointAdvanceOutcome
{
	/// <summary>
	/// The checkpoint moved to the requested position.
	/// </summary>
	Advanced,

	/// <summary>
	/// Another writer had already moved the checkpoint, so this advance was refused and nothing was
	/// written.
	/// </summary>
	/// <remarks>
	/// This is the expected outcome for a reader that has been superseded -- a resumed instance, or a
	/// second reader of the same subscription. The correct response is to stop processing this
	/// subscription, not to re-read and retry: another reader owns it and is making progress.
	/// </remarks>
	Superseded,
}
