// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Jobs;
using Excalibur.Jobs.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "Regression")]
public sealed class QuartzMisfireContractShould
{
	[Theory]
	[InlineData("plain")]
	[InlineData("legacy-context")]
	[InlineData("metadata-context")]
	[InlineData("instances")]
	[InlineData("recurring-extension")]
	public async Task RegisterAnExplicitOneFireMisfirePolicyOnEveryConvenienceCronRoute(string route)
	{
		var expression = route == "recurring-extension" ? "0 0 */1 * * ?" : "0 0 * * * ?";
		var key = "misfire-" + route;
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ContextRecorder>();
		var configurator = new JobConfigurator(services);
		switch (route)
		{
			case "plain":
				configurator.AddJob<MisfireBackgroundJob>(expression, key);
				break;
			case "legacy-context":
				configurator.AddJob<ContextRecordingJob, ContextPayload>(expression, new("legacy"), key);
				break;
			case "metadata-context":
				configurator.AddJob<ContextRecordingJob, ContextPayload>(expression, new("metadata"), ContextJson.Default.ContextPayload, key);
				break;
			case "instances":
				configurator.AddJobInstances<MisfireBackgroundJob>(new QuartzJobOptions(key, expression));
				break;
			case "recurring-extension":
				configurator.AddRecurringJob<MisfireBackgroundJob>(TimeSpan.FromHours(1), key);
				break;
		}
		services.AddQuartz(q => q.ConfigureScheduler(options => options.InstanceName = Guid.NewGuid().ToString("N")));
		await using var provider = services.BuildServiceProvider();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(deadline.Token);
		try
		{
			var trigger = (await scheduler.GetTrigger(new TriggerKey(key + "-trigger"), deadline.Token)).ShouldBeAssignableTo<ICronTrigger>();
			trigger.JobKey.ShouldBe(new JobKey(key));
			trigger.CronExpressionString.ShouldBe(expression);
			trigger.MisfireInstruction.ShouldBe(CronTriggerMisfireInstruction.FireAndProceed,
				"the framework must declare its ONE default rather than inherit Quartz SmartPolicy");
			(await scheduler.GetJobDetail(new JobKey(key), deadline.Token)).ShouldNotBeNull();
		}
		finally
		{
			await scheduler.Shutdown();
		}
	}

	[Theory]
	[InlineData(CronTriggerMisfireInstruction.DoNothing)]
	[InlineData(CronTriggerMisfireInstruction.FireAndProceed)]
	[InlineData(CronTriggerMisfireInstruction.IgnoreMisfires)]
	public async Task PreserveTheActualRegisteredPolicySelectedThroughThePublicQuartzCallback(CronTriggerMisfireInstruction instruction)
	{
		const string expression = "0 * * * * ?";
		var key = new JobKey("callback-" + instruction);
		var triggerKey = new TriggerKey(key.Name + "-trigger");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddQuartzWithJobs(q =>
		{
			q.ConfigureScheduler(options => options.InstanceName = Guid.NewGuid().ToString("N"));
			q.AddJob<QuartzJobAdapter>(options => options.WithIdentity(key)
				.UsingJobData("JobType", typeof(MisfireBackgroundJob).AssemblyQualifiedName!));
			q.AddTrigger(options => options.ForJob(key).WithIdentity(triggerKey)
				.WithCronSchedule(expression, cron => cron.InTimeZone(TimeZoneInfo.Utc).WithMisfireInstruction(instruction)));
		});
		await using var provider = services.BuildServiceProvider();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(deadline.Token);
		try
		{
			var trigger = (await scheduler.GetTrigger(triggerKey, deadline.Token)).ShouldBeAssignableTo<ICronTrigger>();
			trigger.JobKey.ShouldBe(key);
			trigger.CronExpressionString.ShouldBe(expression);
			trigger.TimeZone.ShouldBe(TimeZoneInfo.Utc);
			trigger.MisfireInstruction.ShouldBe(instruction);
		}
		finally
		{
			await scheduler.Shutdown();
		}
	}
}

public sealed class MisfireBackgroundJob : IBackgroundJob
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
