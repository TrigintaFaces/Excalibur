// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

/// <summary>
/// A schedule row that keeps failing must stop being due on every poll.
/// </summary>
/// <remarks>
/// <para>
/// The per-row try/catch that stopped one bad row starving the others wrapped the next-execution update
/// as well as the processing, so a row that always throws never advanced <c>NextExecutionUtc</c>. It was
/// still due on the next poll, and was re-processed and re-logged on every poll for the life of the
/// process. Durable schedule rows outliving the code that created them is the expected steady state, so
/// this is reachable in normal operation rather than exceptional.
/// </para>
/// <para>
/// <b>The arms are paired.</b> The safety arm alone — "a failing row is disabled" — is satisfied by a
/// scheduler that disables everything on the first hiccup, which would silently stop a consumer's
/// schedules. The liveness arm holds that a row whose schedule CAN be advanced keeps running on its
/// normal cadence and stays enabled.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class FailedScheduleRowStopsRetryingShould
{
	[Fact]
	public async Task DisableARowWhoseNextExecutionCannotBeComputed()
	{
		// The cron cannot be parsed, so there is no next occurrence to advance to. Left due, this row
		// re-runs and re-logs on every poll forever.
		var cron = A.Fake<ICronScheduler>();
		_ = A.CallTo(() => cron.Parse(A<string>._, A<TimeZoneInfo>._))
			.Throws(new FormatException("unparseable cron"));

		var store = A.Fake<IScheduleStore>();
		var row = A.Fake<IScheduledMessage>();
		row.Id = Guid.NewGuid();
		row.CronExpression = "not a cron";
		row.Enabled = true;

		await AdvanceOrDisableAsync(CreateService(store, cron), row);

		row.Enabled.ShouldBeFalse(
			"a row whose next execution cannot be computed must stop being due, or it is retried on every "
			+ "poll for the life of the process");
		A.CallTo(() => store.StoreAsync(row, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task KeepARowRunningWhenItsScheduleCanStillBeAdvanced()
	{
		// LIVENESS: a failure must move the row to its next slot, not switch it off. A scheduler that
		// disabled every failing row would satisfy the arm above and silently stop a consumer's schedules.
		var store = A.Fake<IScheduleStore>();
		var row = A.Fake<IScheduledMessage>();
		row.Id = Guid.NewGuid();
		row.CronExpression = string.Empty;
		row.Interval = TimeSpan.FromMinutes(5);
		row.Enabled = true;

		var before = DateTimeOffset.UtcNow;

		await AdvanceOrDisableAsync(CreateService(store, A.Fake<ICronScheduler>()), row);

		row.Enabled.ShouldBeTrue("an interval schedule can still be advanced, so the row keeps running");
		row.NextExecutionUtc.ShouldNotBeNull();
		row.NextExecutionUtc!.Value.ShouldBeGreaterThan(
			before,
			"the row must move to a future slot, or it is still due on the very next poll");
		A.CallTo(() => store.StoreAsync(row, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private static ScheduledMessageService CreateService(IScheduleStore store, ICronScheduler cron) =>
		new(
			store,
			A.Fake<IDispatcher>(),
			new DispatchJsonSerializer(),
			cron,
			Microsoft.Extensions.Options.Options.Create(new SchedulerOptions()),
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()),
			NullLogger<ScheduledMessageService>.Instance,
			null,
			null);

	/// <summary>Invokes the private recovery step, which is the artifact under test.</summary>
	private static Task AdvanceOrDisableAsync(ScheduledMessageService service, IScheduledMessage row) =>
		(Task)typeof(ScheduledMessageService)
			.GetMethod("AdvanceOrDisableAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(service, [row, CancellationToken.None])!;
}
