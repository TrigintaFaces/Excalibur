// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Quartz.Impl.Triggers;

namespace Excalibur.Jobs.Tests.Quartz;

[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "Regression")]
public sealed class QuartzMisfireBehaviorShould
{
	[Theory]
	[InlineData(CronTriggerMisfireInstruction.DoNothing, 0)]
	[InlineData(CronTriggerMisfireInstruction.FireAndProceed, 1)]
	[InlineData(CronTriggerMisfireInstruction.IgnoreMisfires, 3)]
	public async Task ApplyTheActualRegisteredQuartzPolicyAndRetainTheNextOrdinaryOpportunity(
		CronTriggerMisfireInstruction instruction, int expectedMissedOpportunities)
	{
		var now = new DateTimeOffset(2035, 1, 2, 12, 3, 30, TimeSpan.Zero);
		var clock = new FakeTimeProvider(now);
		var first = new DateTimeOffset(2035, 1, 2, 12, 1, 0, TimeSpan.Zero);
		var ordinary = new DateTimeOffset(2035, 1, 2, 12, 4, 0, TimeSpan.Zero);
		var key = new JobKey("transition-" + instruction);
		var triggerKey = new TriggerKey(key.Name + "-trigger");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddQuartzWithJobs(q =>
		{
			q.ConfigureScheduler(options => options.InstanceName = Guid.NewGuid().ToString("N"));
			q.UseTimeProvider(clock);
			q.AddJob<QuartzHealthyRecordingJob>(job => job.WithIdentity(key));
			q.AddTrigger(trigger => trigger.WithIdentity(triggerKey).ForJob(key)
				.WithCronSchedule("0 * * * * ?", schedule => schedule.InTimeZone(TimeZoneInfo.Utc).WithMisfireInstruction(instruction)));
		});
		await using var provider = services.BuildServiceProvider();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(deadline.Token);
		try
		{
			var registered = (await scheduler.GetTrigger(triggerKey, deadline.Token)).ShouldBeOfType<CronTriggerImpl>();
			registered.MisfireInstruction.ShouldBe(instruction);
			// Clone the ACTUAL registered trigger. Mutate only its public vendor test seam;
			// this measures vendor transition opportunities, not hosted job acquisitions.
			var snapshot = registered.Clone().ShouldBeOfType<CronTriggerImpl>();
			snapshot.ShouldNotBeSameAs(registered);
			snapshot.StartTimeUtc = first;
			snapshot.NextFireTimeUtc = first;
			snapshot.UpdateAfterMisfire(null);
			var opportunities = new List<DateTimeOffset>();
			while (snapshot.NextFireTimeUtc is { } next && next < ordinary)
			{
				next.ShouldBeGreaterThanOrEqualTo(first);
				opportunities.Count.ShouldBeLessThan(4, "a stuck vendor transition must fail rather than loop forever");
				opportunities.Add(next);
				snapshot.Triggered(null);
			}
			opportunities.Count.ShouldBe(expectedMissedOpportunities);
			if (instruction == CronTriggerMisfireInstruction.FireAndProceed)
			{
				opportunities.ShouldBe([now]);
			}
			else if (instruction == CronTriggerMisfireInstruction.IgnoreMisfires)
			{
				opportunities.ShouldBe([first, first.AddMinutes(1), first.AddMinutes(2)]);
			}
			snapshot.NextFireTimeUtc.ShouldBe(ordinary);
			clock.Advance(TimeSpan.FromSeconds(30));
			clock.GetUtcNow().ShouldBe(ordinary);
			snapshot.Triggered(null);
			snapshot.PreviousFireTimeUtc.ShouldBe(ordinary);
			snapshot.NextFireTimeUtc.ShouldBe(ordinary.AddMinutes(1));
			// A detached snapshot transition cannot modify registered scheduler state.
			(await scheduler.GetTrigger(triggerKey, deadline.Token))!.NextFireTimeUtc.ShouldBe(registered.NextFireTimeUtc);
		}
		finally { await scheduler.Shutdown(); }
	}

	[Fact]
	public async Task ExecuteAnActualHealthyCronJobOnTheInjectedSchedulerClock()
	{
		var initial = new DateTimeOffset(2035, 1, 2, 12, 3, 30, TimeSpan.Zero);
		var expected = initial.AddSeconds(30);
		var clock = new FakeTimeProvider(initial);
		var marker = new QuartzHealthyMarker();
		var key = new JobKey("healthy-clock");
		var triggerKey = new TriggerKey("healthy-clock-trigger");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(marker);
		services.AddQuartzWithJobs(q =>
		{
			q.ConfigureScheduler(options => options.InstanceName = Guid.NewGuid().ToString("N"));
			q.UseTimeProvider(clock);
			q.AddJob<QuartzHealthyRecordingJob>(job => job.WithIdentity(key));
			q.AddTrigger(trigger => trigger.WithIdentity(triggerKey).ForJob(key)
				.WithCronSchedule("0 * * * * ?", schedule => schedule.InTimeZone(TimeZoneInfo.Utc)
					.WithMisfireInstruction(CronTriggerMisfireInstruction.FireAndProceed)));
		});
		await using var provider = services.BuildServiceProvider();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(deadline.Token);
		try
		{
			(await scheduler.GetTrigger(triggerKey, deadline.Token))!.NextFireTimeUtc.ShouldBe(expected);
			await scheduler.Start(deadline.Token);
			clock.Advance(TimeSpan.FromSeconds(30));
			var firing = await marker.Entered.Task.WaitAsync(deadline.Token);
			firing.Scheduled.ShouldBe(expected);
			firing.Actual.ShouldBe(expected);
			marker.Calls.ShouldBe(1);
			(await scheduler.GetTrigger(triggerKey, deadline.Token))!.NextFireTimeUtc.ShouldBe(expected.AddMinutes(1));
		}
		finally { await scheduler.Shutdown(waitForJobsToComplete: true); }
	}
}

public sealed class QuartzHealthyMarker
{
	private int _calls;

	public int Calls => Volatile.Read(ref _calls);

	public void RecordCall() => Interlocked.Increment(ref _calls);
	public TaskCompletionSource<(DateTimeOffset? Scheduled, DateTimeOffset Actual)> Entered { get; } =
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class QuartzHealthyRecordingJob(QuartzHealthyMarker marker) : IJob
{
	public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
	{
		marker.RecordCall();
		marker.Entered.TrySetResult((context.ScheduledFireTimeUtc, context.FireTimeUtc));
		return ValueTask.CompletedTask;
	}
}
