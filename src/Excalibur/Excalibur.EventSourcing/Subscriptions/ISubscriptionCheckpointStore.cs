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
	/// <param name="newPosition">
	/// A nonnegative position at least as large as <paramref name="expectedPosition"/> when supplied.
	/// Equal positions are accepted only if the atomic comparison still matches.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="CheckpointAdvanceOutcome.Advanced"/> when the comparison matched and the position was accepted (including an equal position), or
	/// <see cref="CheckpointAdvanceOutcome.Superseded"/> when the expected checkpoint state did not match, in
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
	/// A comparison mismatch is reported as Superseded. Invalid negative or descending arguments throw
	/// before storage access, even when the expectation would not match. These checks and the atomic
	/// comparison together prevent rewind within the same checkpoint lifetime. Deletion, restoration,
	/// external writes, and legacy writers are outside that guarantee.
	/// </para>
	/// <para>
	/// Acceptance is not exclusive ownership or fencing of projection effects. Multiple equal-position
	/// calls may succeed. A caller receiving Superseded must reconcile its processing state before
	/// continuing; the result alone does not prove that another reader remains active.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Either position is negative, or the new position is smaller than the supplied expected position.
	/// </exception>
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
	/// The expected state matched and the requested position was accepted, including an equal position.
	/// </summary>
	Advanced,

	/// <summary>
	/// The expected checkpoint state did not match, so this advance was refused and nothing was written.
	/// </summary>
	/// <remarks>
	/// The caller must reconcile its processing state before continuing. This outcome does not establish
	/// ownership, fence effects, or prove that another reader is still making progress.
	/// </remarks>
	Superseded,
}
