// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Applies <see cref="FirestoreRetryPolicy"/> to a Firestore operation.
/// </summary>
/// <remarks>
/// <para>
/// The policy says which failures are transient and how patiently to wait; it has no way to apply itself,
/// so before this existed each caller wrote its own loop. Those loops drifted — a caller retrying only
/// <c>Aborted</c>, over a different attempt count and a different delay, disagreed with the policy the
/// provider advertises while looking entirely reasonable in isolation. Callers share this so that the
/// answer to "what does Firestore retry, and how often" has exactly one definition.
/// </para>
/// <para>
/// The bound is in the loop header rather than in a catch filter. A filter-terminated loop is one edit
/// away from never terminating at all, and the edit does not look dangerous.
/// </para>
/// </remarks>
internal static class FirestoreRetryExecutor
{
	/// <summary>
	/// Upper bound on a single backoff wait, so that raising the policy's attempt count later cannot
	/// silently turn a contention pause into a multi-minute stall.
	/// </summary>
	private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Runs an operation, retrying the failures <see cref="FirestoreRetryPolicy"/> classifies as transient.
	/// </summary>
	/// <typeparam name="T">The operation's result type.</typeparam>
	/// <param name="operation">
	/// The operation to run. It may be invoked more than once, so it must be safe to repeat: an operation
	/// whose retry would double an effect belongs behind a conditional write, not behind this.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The operation's result.</returns>
	/// <remarks>
	/// The operation is attempted at most <see cref="FirestoreRetryPolicy.MaxRetryAttempts"/> + 1 times:
	/// one initial attempt plus that many retries. The final attempt is made outside the loop so that its
	/// failure reaches the caller as itself — a caller that has genuinely run out of retries should see the
	/// provider's own exception, not a wrapper that hides which status ended it.
	/// </remarks>
	internal static async Task<T> ExecuteAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		var policy = FirestoreRetryPolicy.Instance;

		for (var attempt = 0; attempt < policy.MaxRetryAttempts; attempt++)
		{
			try
			{
				return await operation(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (policy.ShouldRetry(ex))
			{
				// Cancellation is not retryable per the policy, so a cancelled token surfaces from here
				// rather than being slept on.
				await Task.Delay(NextDelay(policy.BaseRetryDelay, attempt), cancellationToken).ConfigureAwait(false);
			}
		}

		return await operation(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Computes the wait before the retry following <paramref name="attempt"/>: exponential growth from
	/// <paramref name="baseDelay"/>, capped, with full jitter.
	/// </summary>
	/// <param name="baseDelay">The policy's base delay, used as the first attempt's ceiling.</param>
	/// <param name="attempt">The zero-based index of the attempt that just failed.</param>
	/// <returns>A randomised delay in the interval [0, ceiling].</returns>
	/// <remarks>
	/// <para>
	/// The randomisation is the load-bearing part, not the growth. The callers that reach this executor
	/// contend for a SINGLE document by design — arbitrating concurrent redelivery of one message is the
	/// job — so when that document is contended, every racing caller is rejected at nearly the same
	/// instant. A delay computed only from the attempt number is identical for all of them, so they wake
	/// together and reproduce the collision they were waiting out; the backoff paces each caller without
	/// ever separating them from each other. Drawing the wait from a range spreads the herd across that
	/// range, which is what lets the contention actually drain.
	/// </para>
	/// <para>
	/// Full jitter — a draw over the whole interval rather than a fixed delay plus a small perturbation —
	/// is used because the width of the spread is what disperses the herd. A draw close to zero is not a
	/// defect: the caller that draws it is the one that goes first.
	/// </para>
	/// <para>
	/// Nothing here is a secret and no security property depends on the draw being unpredictable — this is
	/// backoff timing, not key or token material. The cryptographic source is used because it is the one
	/// the platform offers that needs no exemption, and its cost is not measurable beside the delay it is
	/// about to sleep.
	/// </para>
	/// </remarks>
	// internal, not private, so the dispersion contract can be tested against the DRAW itself.
	// Asserting it through the executor means measuring elapsed wall time across a herd of tasks,
	// which makes a property of this function depend on the scheduler that runs it.
	internal static TimeSpan NextDelay(TimeSpan baseDelay, int attempt)
	{
		// Cap the exponent so the ceiling cannot overflow if the policy's attempt count is raised: the
		// delay cap below bounds the value anyway, this bounds the arithmetic that reaches it.
		var shift = Math.Min(attempt, 16);
		var ceilingMs = (long)Math.Min(
			baseDelay.TotalMilliseconds * (1L << shift),
			MaxRetryDelay.TotalMilliseconds);

		// A non-positive ceiling means the policy asked for no wait at all; draw nothing rather than
		// handing the generator an empty range.
		return ceilingMs <= 0
			? TimeSpan.Zero
			: TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32((int)ceilingMs + 1));
	}
}
