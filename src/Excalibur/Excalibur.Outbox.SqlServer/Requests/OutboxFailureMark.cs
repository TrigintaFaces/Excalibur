// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// The single source of the outbox failure transition's SQL: the columns it writes, the guards that make it
/// safe, and the visibility floor that governs the next claim.
/// </summary>
/// <remarks>
/// <para>
/// Every path that moves a message to <c>Failed</c> composes its statement from these members rather than
/// writing its own. The guards and the floor are not defence in depth that a path may reasonably omit — each
/// one closes a specific defect, and a path that re-derives the statement drops them silently:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Ownership.</b> Without it a dispatcher marks failed a message another dispatcher currently holds.
/// </description></item>
/// <item><description>
/// <b>Not-already-sent.</b> Without it a delivered message is reverted to <c>Failed</c> and re-delivered —
/// duplication produced by our own bookkeeping rather than by any transport. The ownership guard alone does
/// not close this, because this same statement releases the lease on every failed transition, so after any
/// failure the row satisfies "unleased" for every dispatcher thereafter.
/// </description></item>
/// <item><description>
/// <b>Floor.</b> Without it the lease is freed with no lower bound on the next claim, which is the retry
/// hot-loop the floor exists to prevent.
/// </description></item>
/// </list>
/// <para>
/// The batch path shipped without all three while its single-message sibling enforced them, so the guarantee
/// held or not depending on which overload the caller happened to reach. Sharing the fragments is what makes
/// that divergence unrepresentable rather than merely fixed once.
/// </para>
/// </remarks>
internal static class OutboxFailureMark
{
	/// <summary>
	/// The failure-anchored visibility floor, computed entirely on the SERVER clock.
	/// </summary>
	/// <remarks>
	/// Anchoring on <c>SYSUTCDATETIME()</c> rather than on a dispatcher-supplied instant keeps the floor a
	/// single-clock decision: the claim predicate compares <c>NextAttemptAt</c> against the same server clock,
	/// so a dispatcher whose clock is skewed cannot shorten the floor by the skew.
	/// </remarks>
	public const string ServerFloorExpression =
		"TODATETIMEOFFSET(DATEADD(SECOND, @FloorSeconds, SYSUTCDATETIME()), 0)";

	/// <summary>
	/// The caller's computed schedule, re-anchored on the SERVER clock as a DELAY.
	/// </summary>
	/// <remarks>
	/// The caller hands this statement an absolute instant it computed from ITS OWN clock, while the claim
	/// predicate reads the stored column back on the SERVER's. Persisting the caller's instant therefore puts
	/// two machines that have no reason to agree on opposite sides of one comparison. A duration carries no
	/// clock, so converting the caller's instant to "how long from now" before it leaves the dispatcher and
	/// re-anchoring it to <c>SYSUTCDATETIME()</c> here preserves the caller's intent exactly while leaving a
	/// single clock in the comparison.
	/// </remarks>
	public const string ServerAnchoredScheduleExpression =
		"TODATETIMEOFFSET(DATEADD(MILLISECOND, @NextAttemptDelayMs, SYSUTCDATETIME()), 0)";

	/// <summary>
	/// The later of the caller's delay and the configured floor, both measured from the SERVER clock.
	/// </summary>
	/// <remarks>
	/// The maximum is taken over the two DELAYS, which is the same choice as taking it over the two instants
	/// once both are anchored to the same clock. Relaxing the result below the floor still takes inverting
	/// this one comparison, so it is not something ordinary use can express.
	/// </remarks>
	public const string ServerAnchoredComposedExpression =
		"TODATETIMEOFFSET(DATEADD(MILLISECOND, CASE WHEN @NextAttemptDelayMs > @FloorSeconds * 1000 " +
		"THEN @NextAttemptDelayMs ELSE @FloorSeconds * 1000 END, SYSUTCDATETIME()), 0)";

	/// <summary>
	/// The columns written by every failure transition, excluding the <c>NextAttemptAt</c> schedule.
	/// </summary>
	/// <remarks>
	/// The lease is released (parity with the sent and dead-letter transitions) so the schedule below — not a
	/// lingering lease — governs the next claim. The retry count is non-decreasing: a stale late writer must
	/// not lower it, because the dead-letter ceiling is driven by that count and a count that can fall is a
	/// message that never terminates.
	/// </remarks>
	public const string SetClause =
		"""
		SET Status = 3, LastError = @ErrorMessage,
		    RetryCount = CASE WHEN RetryCount > @RetryCount THEN RetryCount ELSE @RetryCount END,
		    LastAttemptAt = @LastAttemptAt,
		    LeasedAt = NULL, LeasedBy = NULL
		""";

	/// <summary>
	/// The guards that make the transition safe, applied by every failure path.
	/// </summary>
	public const string Guards =
		"""
		  AND Status <> 2
		  AND (LeasedBy IS NULL OR LeasedBy = @LeasedBy)
		""";

	/// <summary>
	/// Builds the <c>NextAttemptAt</c> assignment, composing the caller's computed schedule with the
	/// configured floor so the result can only ever be the LATER of the two.
	/// </summary>
	/// <param name="hasNextAttempt">Whether a caller-computed next-attempt instant was supplied.</param>
	/// <param name="hasFloor">Whether a configured floor F was supplied.</param>
	/// <returns>The assignment fragment, or an empty string when the column is to be left unchanged.</returns>
	/// <remarks>
	/// <para>
	/// The two inputs are composed, NOT alternatives. Treating the caller's value as an override was the
	/// defect: the fine-grained backoff calculator yields on the order of a second at the first attempt, so a
	/// consumer who configured a floor of five minutes — exactly as the guarantee contract instructs — was
	/// retried a second later, and the capability that was supposed to refine the schedule instead weakened
	/// the guarantee below what the same failure gets without it.
	/// </para>
	/// <para>
	/// Composing with the maximum means the caller's schedule can only ever push the next attempt OUT, never
	/// pull it in. Relaxing the floor below F is therefore not something ordinary use can express: it takes
	/// inverting the comparison in this one expression, which is a single-token mutation a test can bind.
	/// </para>
	/// </remarks>
	public static string NextAttemptClause(bool hasNextAttempt, bool hasFloor) =>
		(hasNextAttempt, hasFloor) switch
		{
			// The composed case: the later of the caller's delay and the floor, both on the server clock.
			(true, true) => $", NextAttemptAt = {ServerAnchoredComposedExpression}",

			// No floor configured: the caller's delay is the only bound available, still server-anchored.
			(true, false) => $", NextAttemptAt = {ServerAnchoredScheduleExpression}",

			// The plain failure path: the floor alone, on the server clock.
			(false, true) => $", NextAttemptAt = {ServerFloorExpression}",

			// Neither supplied: leave the column untouched.
			(false, false) => string.Empty,
		};
}
