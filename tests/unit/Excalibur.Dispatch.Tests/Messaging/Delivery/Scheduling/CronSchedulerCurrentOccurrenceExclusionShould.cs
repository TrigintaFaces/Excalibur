// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Author≠impl regression lock for FR-D7 · <c>axdtve</c> (double-fire on restart) — the
/// <see cref="CronScheduler.GetMissedExecutions"/> upper bound MUST be EXCLUSIVE of the live-fire
/// boundary (the occurrence exactly at <c>now</c>).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (FrontendDeveloper) against the committed seam
/// (<c>CronScheduler.cs:149-184</c>, <c>ICronExpressionQuery.GetOccurrencesBetween</c> at
/// <c>ICronExpressionQuery.cs:16</c>, gated through <c>CronExpressionExtensions.cs:25-34</c>).
/// </para>
/// <para>
/// <b>The bug:</b> the upper guard was <c>if (occurrence &gt; now) break;</c> (strictly-future), so the
/// <c>== now</c> occurrence fell through to <c>yield return</c>. On restart the live scheduler ALSO fires
/// the <c>== now</c> occurrence ⇒ double-fire. The fix changed the guard to
/// <c>if (occurrence &gt;= now) break;</c> (exclusive of the live-fire boundary).
/// </para>
/// <para>
/// <b>Designed RED on pre-fix HEAD / GREEN on the fix:</b> the real Cronos-backed
/// <see cref="TimeZoneAwareCronExpression.GetOccurrencesBetween"/> uses <c>toInclusive: false</c> and so
/// would NOT surface the <c>== now</c> occurrence, masking the loop guard. This lock therefore drives a
/// FAKE <see cref="ICronExpressionQuery"/> whose <c>GetOccurrencesBetween</c> DELIBERATELY INCLUDES the
/// <c>== now</c> occurrence (00:30), making the <see cref="CronScheduler"/> loop guard the sole
/// load-bearing assertion. With the pre-fix <c>&gt; now</c> guard, 00:30 is yielded (double-fire) ⇒ the
/// exclusion assertion FAILS (RED). With the <c>&gt;= now</c> fix, 00:30 is excluded ⇒ GREEN. The
/// interior-miss assertion guards against over-correction (no data loss). The RED-proof against the
/// production guard is deferred to post-commit (the source file is reserved by the implementer and is not
/// mutated here).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CronSchedulerCurrentOccurrenceExclusionShould
{
	private static CronScheduler CreateScheduler() =>
		new(Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()), NullLogger<CronScheduler>.Instance);

	// "*/10" semantics over [00:00, 00:30] — occurrences {00:10, 00:20, 00:30}, where 00:30 == now.
	private static readonly DateTimeOffset LastRun = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 30, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Mark10 = new(2026, 1, 1, 0, 10, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Mark20 = new(2026, 1, 1, 0, 20, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Mark30 = Now; // the live-fire boundary == now

	[Fact]
	public void ExcludeTheCurrentOccurrence_AtTheLiveFireBoundary()
	{
		// Arrange — a query whose occurrence set INCLUDES the == now mark (00:30), so the loop guard is
		// the only thing that can exclude it.
		var scheduler = CreateScheduler();
		var cron = new BoundaryIncludingCronQuery(new[] { Mark10, Mark20, Mark30 });

		// Act — replay missed executions over the downtime window [00:00, 00:30].
		var missed = scheduler.GetMissedExecutions(cron, LastRun, Now).ToList();

		// Assert (primary) — the == now occurrence is the live scheduler's job; replaying it here would
		// double-fire it on restart. Pre-fix (> now) it IS yielded ⇒ RED; with the fix (>= now) it is
		// excluded ⇒ GREEN.
		missed.ShouldNotContain(Mark30, "the == now occurrence is fired by the live scheduler; replaying it as 'missed' double-fires on restart");
	}

	[Fact]
	public void StillReturnInteriorMisses_NoDataLoss()
	{
		// Arrange — same boundary-including occurrence set.
		var scheduler = CreateScheduler();
		var cron = new BoundaryIncludingCronQuery(new[] { Mark10, Mark20, Mark30 });

		// Act
		var missed = scheduler.GetMissedExecutions(cron, LastRun, Now).ToList();

		// Assert (no over-correction) — the strictly-interior occurrences (00:10, 00:20) that were genuinely
		// missed during downtime MUST still be replayed; the exclusive-upper-bound fix must not drop them.
		missed.ShouldContain(Mark10, "the 00:10 occurrence missed during downtime must still be replayed");
		missed.ShouldContain(Mark20, "the 00:20 occurrence missed during downtime must still be replayed");
	}

	/// <summary>
	/// A fake <see cref="ICronExpression"/> + <see cref="ICronExpressionQuery"/> whose
	/// <see cref="GetOccurrencesBetween"/> returns a FIXED set that DELIBERATELY INCLUDES the
	/// <c>== now</c> boundary occurrence — unlike the real Cronos-backed query (<c>toInclusive: false</c>).
	/// This forces the <see cref="CronScheduler"/> loop guard to be the sole exclusion mechanism under test.
	/// </summary>
	private sealed class BoundaryIncludingCronQuery(IReadOnlyList<DateTimeOffset> occurrences)
		: ICronExpression, ICronExpressionQuery
	{
		public string Expression => "*/10 * * * *";

		public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

		public bool IncludesSeconds => false;

		public DateTimeOffset? GetNextOccurrence(DateTimeOffset from) => from.AddMinutes(10);

		public bool IsValid() => true;

		public DateTimeOffset? GetNextOccurrenceUtc(DateTimeOffset fromUtc) => fromUtc.AddMinutes(10);

		// Intentionally returns the boundary (== now) occurrence so the CronScheduler guard is load-bearing.
		public IEnumerable<DateTimeOffset> GetOccurrencesBetween(DateTimeOffset from, DateTimeOffset endTime) => occurrences;

		public string GetDescription() => Expression;
	}
}
