// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

// ywodwj — RecurringDispatchScheduler's past-schedule decision must read the clock through TimeProvider, not
// DateTimeOffset.UtcNow. These are deterministic author!=impl regression locks: the FakeTimeProvider is set
// FAR IN THE FUTURE (year 3000) so the injected clock provably differs from the wall clock. Each arm is RED
// on the pre-fix DateTimeOffset.UtcNow read (which compares against ~2026, not the fake 3000).
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RecurringDispatchSchedulerClockShould
{
	private static readonly DateTimeOffset FakeNow = new(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private sealed record TestMessage(string Value);

	private static (RecurringDispatchScheduler Scheduler, IScheduleStore Store) NewScheduler(
		FakeTimeProvider clock, PastScheduleBehavior pastBehavior)
	{
		var store = A.Fake<IScheduleStore>();
		var options = Microsoft.Extensions.Options.Options.Create(
			new SchedulerOptions { PastScheduleBehavior = pastBehavior });
		var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions());
		var scheduler = new RecurringDispatchScheduler(
			store,
			new DispatchJsonSerializer(),
			options,
			A.Fake<ICronScheduler>(),
			cronOptions,
			NullLogger<RecurringDispatchScheduler>.Instance,
			clock);
		return (scheduler, store);
	}

	// SAFETY: a time PAST relative to the injected clock is rejected — even though it is FUTURE on the wall
	// clock. RED on the pre-fix UtcNow read (the wall clock ~2026 sees year 2999 as future -> no throw).
	[Fact]
	public async Task RejectAPastSchedule_MeasuredAgainstTheInjectedClock_NotTheWallClock()
	{
		var clock = new FakeTimeProvider(FakeNow);
		var (scheduler, store) = NewScheduler(clock, PastScheduleBehavior.Reject);

		var pastVsInjectedButFutureVsWall = FakeNow.AddYears(-1); // year 2999: past vs 3000, future vs 2026

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await scheduler.ScheduleOnceAsync(
				pastVsInjectedButFutureVsWall, new TestMessage("m"), CancellationToken.None));

		A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	// LIVENESS: with ExecuteImmediately, a past-vs-injected-clock schedule is CLAMPED to the injected now and
	// still stored (scheduling is not inert). The stored NextExecutionUtc is the FAKE now, proving the clamp
	// read the injected clock. RED on the pre-fix UtcNow read (which would clamp to the wall clock ~2026).
	[Fact]
	public async Task ClampAPastSchedule_ToTheInjectedClock_AndStillStoreIt()
	{
		var clock = new FakeTimeProvider(FakeNow);
		var (scheduler, store) = NewScheduler(clock, PastScheduleBehavior.ExecuteImmediately);

		IScheduledMessage? stored = null;
		A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
			.Invokes((IScheduledMessage m, CancellationToken _) => stored = m)
			.Returns(Task.CompletedTask);

		var pastVsInjected = FakeNow.AddYears(-1); // year 2999: past vs the fake 3000

		await scheduler.ScheduleOnceAsync(pastVsInjected, new TestMessage("m"), CancellationToken.None);

		stored.ShouldNotBeNull("a past schedule under ExecuteImmediately is still stored — scheduling is not inert");
		stored.NextExecutionUtc.ShouldBe(
			FakeNow, "the past schedule is clamped to the INJECTED clock's now, not the wall clock");
	}
}
