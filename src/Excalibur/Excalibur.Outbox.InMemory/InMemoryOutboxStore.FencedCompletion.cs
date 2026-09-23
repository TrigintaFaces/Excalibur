// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.InMemory;

/// <summary>
/// The completion-path fencing this store previously left to the unfenced members.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> Fencing the mark-sent transition alone leaves the two transitions a
/// FAILED delivery takes unfenced, so a superseded tenure could still report a failure, apply a backoff,
/// or bury a message a live tenure then delivers successfully. The composition guard refuses to start a
/// host whose store cannot fence those transitions — and this store, the one the guard's own remedy text
/// recommends to a consumer whose store cannot fence, could not. A development or test host that composed
/// the in-memory outbox with a leader election therefore failed to start, pointed at itself.
/// </para>
/// <para>
/// <b>Atomicity is real here, not approximated.</b> Every member below evaluates the fence, the claim and
/// the mutation inside the single <c>_claimLock</c> region that already serialises the claim and the
/// mark-sent. That is the same indivisibility a SQL store buys with one statement: a fresher tenure cannot
/// advance the high-water in a gap between the check and the write, because there is no gap. Splitting
/// them would let a check pass truthfully against a value that no longer held by the time it wrote.
/// The unfenced completion overloads on the other part of this type take the same lock, so the statement
/// holds of the store and not only of the members below — which is the form it has to take to be worth
/// anything. A critical section excludes what shares its lock and nothing else, so a locked member beside
/// an unlocked one that writes the same message is not serialised with it at all.
/// </para>
/// <para>
/// <b>The refusals are returned, never thrown.</b> The drain reaches these members from inside its own
/// exception handling, where a sibling handler on the same block cannot run, so an exception raised here
/// escapes the whole cycle and abandons every message the caller still legitimately holds. A refusal on
/// one message must cost that message and nothing else.
/// </para>
/// </remarks>
public sealed partial class InMemoryOutboxStore
{
	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset? nextAttemptAt,
		OutboxWriteAuthority authority,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var now = DateTimeOffset.UtcNow;

		lock (_claimLock)
		{
			// THE FENCE IS EVALUATED FIRST AND IN ISOLATION. A superseded tenure must learn nothing about
			// the row's state: answering "not found" or "claim lost" to a caller that has already lost the
			// tenure tells it to carry on with its batch, which is precisely what it must not do.
			if (authority.FencingToken < _fencingHighWaterMark)
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused);
			}

			_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, authority.FencingToken);

			if (!_messages.TryGetValue(messageId, out var message))
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.MessageNotFound);
			}

			// TERMINAL-STATUS exclusion, reported rather than silent, and reported AS ITSELF. Sent and
			// DeadLettered are final: nobody owns a terminal message, so this is neither a lost claim nor an
			// absence. Returning success here would let a failure reported after a successful send move the
			// message back to Failed, which IS in the claim predicate, so a delivered message would be
			// delivered again. This store keeps the row, so it can see the difference and must say it.
			if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.AlreadyTerminal);
			}

			// The claim term. Orthogonal to the fence: one process can claim, fail, re-claim and fail again
			// under an identical fencing token, so only the claim identity separates a live report from a
			// stale cycle's. The unreserved-input path (staged then failed without ever being claimed) has
			// no lease and is not refused -- there is no claim for the caller to have lost.
			if (_leases.TryGetValue(messageId, out var lease)
				&& !string.Equals(lease.LeasedBy, authority.ClaimIdentity, StringComparison.Ordinal))
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.ClaimLost);
			}

			// Attempts are non-decreasing across re-claims: a stale lower report must never move the count
			// DOWN, which would weaken the processor's dead-letter ceiling. Captured BEFORE MarkFailed,
			// which increments it.
			var priorRetryCount = message.RetryCount;
			message.MarkFailed(errorMessage);
			message.RetryCount = Math.Max(priorRetryCount, retryCount);
			message.LastAttemptAt = now;

			// The caller owns the backoff policy, exactly as on the unfenced route. A null schedule means
			// this failure takes no computed backoff, and the message falls back to the failure-anchored
			// visibility floor so it can never re-enter the claimable set in the same drain cycle.
			_nextAttempt[messageId] = nextAttemptAt
				?? (now + TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds));
			_ = _leases.TryRemove(messageId, out _);

			LogMessageFailed(messageId, errorMessage, retryCount);

			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}
	}

	/// <inheritdoc />
	public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
		string messageId,
		string reason,
		long fencingToken,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_claimLock)
		{
			if (fencingToken < _fencingHighWaterMark)
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused);
			}

			_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fencingToken);

			if (!_messages.TryGetValue(messageId, out var message))
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.MessageNotFound);
			}

			// No claim term on this member, by the same ruling the fenced interface records: the harm a
			// dead-letter does is cross-tenure, and a fencing token refuses exactly that. ClaimLost is not
			// reachable from here -- an implementation returning it would report a decision it never made.
			//
			// AN ALREADY-TERMINAL ROW NOW HAS AN OUTCOME OF ITS OWN. This branch previously reported
			// MessageNotFound, which was honest-by-parity with a store whose terminal idiom DELETES the row
			// but a plain lie here, where the row is present and readable. Answering Applied would be the
			// dangerous choice rather than merely the inaccurate one: it tells the drain its dead-letter
			// mark took effect on a message that was SENT, so the drain does not withdraw the external
			// dead-letter entry it wrote moments earlier, and the message ends up simultaneously delivered
			// and sitting in the dead-letter queue for an operator to replay.
			if (message.Status is OutboxStatus.Sent or OutboxStatus.DeadLettered)
			{
				return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.AlreadyTerminal);
			}

			message.Status = OutboxStatus.DeadLettered;
			message.LastError = reason;

			_ = _leases.TryRemove(messageId, out _);
			_ = _nextAttempt.TryRemove(messageId, out _);

			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}
	}
}
