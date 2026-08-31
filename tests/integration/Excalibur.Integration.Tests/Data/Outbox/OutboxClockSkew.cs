// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// A dispatcher host whose clock disagrees with everybody else's.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole fault model, and it is not exotic: two machines running the same dispatcher, with no
/// guarantee their clocks agree. Nothing crashes, nothing pauses, no time passes. One of them simply
/// believes it is later than the other does.
/// </para>
/// <para>
/// Injecting it as the store's <see cref="TimeProvider"/> is what makes the defect FALSIFIABLE. A store
/// that decides claim eligibility on its own clock will read a lease its peer is actively delivering under
/// as expired and take it — and the atomic claim underneath does not stop that, because the two dispatchers
/// are not racing. Skewed, they are not even simultaneous: the second is the only writer at that instant,
/// so its compare-and-swap succeeds legitimately on a predicate that was already false. A store that
/// decides on the SERVER's clock cannot be moved by this at all, which is what these locks assert.
/// </para>
/// </remarks>
/// <param name="offset">How far ahead of real time this host believes it is.</param>
internal sealed class SkewedClock(TimeSpan offset) : TimeProvider
{
	/// <inheritdoc/>
	public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;
}

/// <summary>
/// Shared shape of the outbox claim clock-skew locks.
/// </summary>
/// <remarks>
/// <para>
/// Each provider's lock carries the same three arms, and all three are load-bearing:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>SAFETY</b> — a dispatcher whose clock runs more than a full lease ahead must NOT be handed a message
/// another dispatcher currently holds. This is the arm that goes RED on an unfixed store.
/// </item>
/// <item>
/// <b>LIVENESS</b> — once a lease genuinely elapses, the next dispatcher MUST be able to claim. Without
/// this arm the safety assertion is satisfied by a store that returns nothing to anybody, forever: a total
/// stall passes a safety-only test perfectly, and a stalled outbox delivers nothing at all. The liveness
/// arm is what separates "correct" from "inert".
/// </item>
/// <item>
/// <b>BASE</b> — one un-skewed dispatcher still claims and settles a message, so neither of the arms above
/// is passing because the store is broken in some ordinary way.
/// </item>
/// </list>
/// </remarks>
internal static class OutboxClockSkewArms
{
	/// <summary>
	/// How far ahead the skewed dispatcher's clock runs, beyond the lease, in the safety arm.
	/// </summary>
	/// <remarks>
	/// The skew must exceed the lease for the defect to be reachable at all: the unfixed predicate is
	/// <c>leaseStamp &lt; skewedNow - lease</c>, which only admits a live lease once the skew is larger than
	/// the lease itself. A comfortable margin on top keeps the arm from depending on how long the test takes
	/// to run.
	/// </remarks>
	public static readonly TimeSpan SafetyMargin = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Polls <paramref name="attempt"/> until it yields a message id, or the budget runs out.
	/// </summary>
	/// <remarks>
	/// A lease now expires on the STORE's clock rather than on the test host's, so the liveness arm cannot
	/// fast-forward a <see cref="TimeProvider"/> to make it elapse — the wait has to be real. Polling
	/// against the condition (rather than sleeping for a fixed interval and asserting once) keeps the arm
	/// deterministic under CI load: a slow container makes it take longer, never makes it flake.
	/// </remarks>
	/// <param name="attempt">Claims a batch and returns the ids won.</param>
	/// <param name="messageId">The id being waited for.</param>
	/// <param name="budget">How long to keep trying.</param>
	/// <returns><see langword="true"/> when the message was claimed inside the budget.</returns>
	public static async Task<bool> PollUntilClaimableAsync(
		Func<Task<IReadOnlyCollection<string>>> attempt,
		string messageId,
		TimeSpan budget)
	{
		ArgumentNullException.ThrowIfNull(attempt);

		var deadline = DateTimeOffset.UtcNow + budget;
		while (DateTimeOffset.UtcNow < deadline)
		{
			var won = await attempt().ConfigureAwait(false);
			if (won.Contains(messageId, StringComparer.Ordinal))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
		}

		return false;
	}
}
