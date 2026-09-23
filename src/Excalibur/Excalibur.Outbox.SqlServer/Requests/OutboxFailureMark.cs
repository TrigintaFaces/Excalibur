// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

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
	/// The guards that make the transition safe. Composed by the single-message and batch failure
	/// paths. Any other statement that writes this table's <c>Status</c> column needs them too, and
	/// composing this constant is how it gets them.
	/// </summary>
	/// <remarks>
	/// Status 2 is Sent and 5 is DeadLettered -- both TERMINAL, and neither may be reversed by a failure
	/// report. Excluding Sent alone is not enough: a late report against a dead-lettered row would return
	/// it to the failed set, and the failed set is claimable, so a message we decided to stop delivering
	/// becomes deliverable again. The exclusion is unconditional and needs no ownership evidence -- it is
	/// true of a report from the CURRENT claim holder as much as a superseded one.
	/// </remarks>
	public const string Guards =
		"""
		  AND Status NOT IN (2, 5)
		  AND (LeasedBy IS NULL
		       OR LeasedBy = @LeasedBy
		       OR LEFT(LeasedBy, @LeasedByPrefixLength) = @LeasedByPrefix)
		""";

	/// <summary>
	/// Binds the ownership parameters the <see cref="Guards"/> lease arm reads.
	/// </summary>
	/// <param name="parameters">The parameter set the composed statement will execute with.</param>
	/// <param name="leasedBy">The bare processor identity of the caller recording the failure.</param>
	/// <remarks>
	/// <para>
	/// <b>The claim does not store the bare processor identity, so an equality test against it can never
	/// match a claimed row.</b> The claim stamps a per-call CLAIM identity of the form
	/// <c>{processorId}:{claimId}</c>, because the processor half answers which PROCESS holds the row and
	/// the claim half answers which CLAIM -- only the second distinguishes two cycles of the same process,
	/// one of whose leases lapsed while a dispatch hung. Comparing the stored value to a bare processor id
	/// left the second arm dead: only UNCLAIMED rows could be marked, so a claimed message could not be
	/// marked failed by its own owner, the row stayed Staged, and the retry count and dead-letter ceiling
	/// never advanced. The sibling sent and dead-letter marks carry no lease arm at all, which is why they
	/// were unaffected and why the asymmetry pointed here.
	/// </para>
	/// <para>
	/// <b>Matched by PREFIX, and anchored rather than split.</b> The claim's own contract states that
	/// consumers match this by prefix and never by splitting on the separator, because the processor half
	/// may itself contain colons. The length is passed as a parameter rather than computed with
	/// <c>LEN()</c>, which ignores trailing spaces and would silently widen the match for a processor id
	/// that ends in one. <c>LIKE</c> is avoided deliberately: a processor id containing <c>%</c>,
	/// <c>_</c> or <c>[</c> would otherwise need escaping, and an unescaped wildcard here would match
	/// another processor's lease.
	/// </para>
	/// <para>
	/// The equality arm is kept beside the prefix arm so a row leased by a path that wrote the bare
	/// identity is still markable; the prefix arm only ever ADDS the composed form.
	/// </para>
	/// </remarks>
	public static void AddLeaseOwnership(DynamicParameters parameters, string leasedBy)
	{
		ArgumentNullException.ThrowIfNull(parameters);

		parameters.Add("@LeasedBy", leasedBy);

		// REFUSED rather than defended. A zero-length prefix is catastrophic here -- LEFT(LeasedBy, 0) is
		// the empty string, so the arm would be TRUE for EVERY leased row and the ownership guard would
		// become no guard at all. Guarding that with a null-check leaves the hole one token away (null ->
		// string.Empty) in a branch no call site reaches and no test covers, which is dead code that is
		// dangerous rather than harmless. Demanding a non-empty identity makes the zero-length prefix
		// unconstructible instead, so the bad state cannot be written down rather than merely being avoided.
		ArgumentException.ThrowIfNullOrEmpty(leasedBy);

		var prefix = leasedBy + ":";
		parameters.Add("@LeasedByPrefix", prefix);
		parameters.Add("@LeasedByPrefixLength", prefix.Length);
	}

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

	/// <summary>
	/// Converts the caller's absolute next-attempt instant into the DELAY it represents, so the statement can
	/// re-anchor it on the server clock.
	/// </summary>
	/// <param name="nextAttemptAt">The next-attempt instant the caller computed from its own clock.</param>
	/// <returns>The delay in milliseconds, which is negative when the schedule has already elapsed.</returns>
	/// <remarks>
	/// <para>
	/// The caller computes this instant as "now, plus a backoff" against its OWN clock; the claim predicate
	/// reads the stored column back against the SERVER's. Binding the instant verbatim therefore straddles two
	/// clocks, and where the dispatcher runs ahead of the database the message stays invisible for the whole
	/// skew AFTER its backoff has genuinely elapsed — a due message withheld, bounded by nothing but the skew.
	/// Recovering the duration here and re-adding it to <c>SYSUTCDATETIME()</c> in the statement keeps the
	/// dispatcher's intent and puts one clock on both sides of the comparison.
	/// </para>
	/// <para>
	/// An already-elapsed schedule yields a NEGATIVE delay, which is deliberate: composed with the floor it
	/// simply loses to it, and with no floor configured it makes the message due immediately, which is what an
	/// elapsed schedule means. The value is clamped to the range <c>DATEADD</c> accepts.
	/// </para>
	/// </remarks>
	internal static int ToServerDelayMilliseconds(DateTimeOffset nextAttemptAt)
	{
		var delayMs = (nextAttemptAt - DateTimeOffset.UtcNow).TotalMilliseconds;

		return delayMs <= int.MinValue
			? int.MinValue
			: delayMs >= int.MaxValue ? int.MaxValue : (int)delayMs;
	}

}
