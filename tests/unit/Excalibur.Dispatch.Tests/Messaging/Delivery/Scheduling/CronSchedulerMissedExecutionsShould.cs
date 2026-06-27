// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Author≠impl regression lock for S853 · <c>sklt7i</c> (MS-D1, DATA LOSS) — the cron scheduler must
/// actually return missed executions over a downtime window so <see cref="ScheduledMessageService"/>
/// can replay them.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (PlatformDeveloper) against the committed seam.
/// </para>
/// <para>
/// <b>The bug:</b> <see cref="TimeZoneAwareCronExpression"/> is the only <see cref="ICronExpression"/>
/// implementation, and it declared <c>: ICronExpression</c> only — NOT
/// <see cref="ICronExpressionQuery"/> — even though it implements every query member. The extension
/// <c>CronExpressionExtensions.GetOccurrencesBetween</c> gates on <c>cron is ICronExpressionQuery</c>
/// (<c>CronExpressionExtensions.cs:28</c>) and returns <c>[]</c> when that is false, so
/// <see cref="CronScheduler.GetMissedExecutions"/> (<c>CronScheduler.cs:156</c>) silently dropped EVERY
/// missed execution — scheduled messages over any downtime window were never replayed.
/// The fix declares <c>: ICronExpression, ICronExpressionQuery</c> on the class (one line, members
/// already present).
/// </para>
/// <para>
/// <b>RED on pre-fix HEAD:</b> with <see cref="TimeZoneAwareCronExpression"/> NOT implementing
/// <see cref="ICronExpressionQuery"/>, <see cref="ReturnMissedOccurrences_OverADowntimeWindow"/> sees an
/// EMPTY result and fails. <see cref="ReturnEmpty_ForANonQueryCronExpression"/> is the self-contained
/// non-vacuity proof: it drives a plain <see cref="ICronExpression"/> (no
/// <see cref="ICronExpressionQuery"/>) — structurally IDENTICAL to the pre-fix
/// <see cref="TimeZoneAwareCronExpression"/> — through the SAME <see cref="CronScheduler.GetMissedExecutions"/>
/// path and confirms it yields <c>[]</c>, proving the gate is load-bearing and the primary assertion is
/// non-vacuous.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CronSchedulerMissedExecutionsShould
{
	private static CronScheduler CreateScheduler() =>
		new(Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()), NullLogger<CronScheduler>.Instance);

	[Fact]
	public void ReturnMissedOccurrences_OverADowntimeWindow()
	{
		// Arrange — a "every 10 minutes" schedule, and a 1-hour downtime window [00:00, 01:00).
		var scheduler = CreateScheduler();
		var cron = scheduler.Parse("*/10 * * * *", TimeZoneInfo.Utc);
		var lastRun = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var now = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero);

		// Act — the system MUST surface the executions missed while it was down.
		var missed = scheduler.GetMissedExecutions(cron, lastRun, now).ToList();

		// Assert — the five UNAMBIGUOUSLY-interior marks (00:10..00:50, all strictly inside the open
		// window, boundary-independent) MUST be returned. Pre-fix this list is EMPTY ⇒ RED (the data loss).
		missed.ShouldNotBeEmpty("missed scheduled executions must be replayed, not silently dropped (data loss)");
		foreach (var minute in new[] { 10, 20, 30, 40, 50 })
		{
			var expected = new DateTimeOffset(2026, 1, 1, 0, minute, 0, TimeSpan.Zero);
			missed.ShouldContain(expected, $"the 00:{minute:00} occurrence missed during downtime must be replayed");
		}
	}

	[Fact]
	public void ReturnEmpty_ForANonQueryCronExpression()
	{
		// Non-vacuity proof: a plain ICronExpression that does NOT implement ICronExpressionQuery —
		// structurally identical to the PRE-FIX TimeZoneAwareCronExpression — drives the SAME
		// GetMissedExecutions path and yields []. This is exactly the silent-drop the primary lock guards
		// against, so the primary assertion is provably non-vacuous: pre-fix, the real type WAS such a type.
		var scheduler = CreateScheduler();
		var nonQueryCron = new NonQueryCronExpression();
		var lastRun = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var now = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero);

		var missed = scheduler.GetMissedExecutions(nonQueryCron, lastRun, now).ToList();

		missed.ShouldBeEmpty("a non-ICronExpressionQuery expression has no query path — this is the pre-fix bug surface");
	}

	/// <summary>
	/// A minimal <see cref="ICronExpression"/> that deliberately does NOT implement
	/// <see cref="ICronExpressionQuery"/> — mirrors the pre-fix <see cref="TimeZoneAwareCronExpression"/>
	/// shape so the non-vacuity guard reproduces the exact silent-drop seam.
	/// </summary>
	private sealed class NonQueryCronExpression : ICronExpression
	{
		public string Expression => "*/10 * * * *";

		public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

		public bool IncludesSeconds => false;

		public DateTimeOffset? GetNextOccurrence(DateTimeOffset from) => from.AddMinutes(10);

		public bool IsValid() => true;
	}
}
