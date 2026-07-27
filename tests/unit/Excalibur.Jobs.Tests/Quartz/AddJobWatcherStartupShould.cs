// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling

using System.Collections.Generic;

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Regression lock for the <c>AddJobWatcher</c> keystone (Wave-0, delete-and-delegate to
/// <see cref="IHostedService.StartAsync"/>). Proves the watcher is registered as a direct
/// <see cref="IHostedService"/> and starts a host WITHOUT the removed
/// <c>AsyncFactoryHostedService</c>/<c>IJobOptionsHostedWatcherServiceFactory</c> triad.
/// </summary>
/// <remarks>
/// Non-vacuous: on the pre-fix implementation <c>AddJobWatcher</c> registered
/// <c>AsyncFactoryHostedService&lt;TJob,TOptions&gt;</c> whose factory was never registered, so
/// <see cref="IHostedService.StartAsync"/> threw <see cref="InvalidOperationException"/>
/// ("No service for type 'IJobOptionsHostedWatcherServiceFactory' has been registered") at host
/// startup. These tests RED on that impl and GREEN on the direct-<see cref="IHostedService"/> seam.
/// Only the runtime test constructs a Quartz scheduler (Quartz keeps a process-global scheduler
/// repository, so a single scheduler per test run keeps the lock deterministic).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class AddJobWatcherStartupShould
{
	private const string JobName = "watch-job";
	private const string JobGroup = "watch-group";

	[Fact]
	public async Task StartTheHostWithoutAFactoryAndPauseADisabledJobThroughTheResolvedScheduler()
	{
		// Arrange — register a real Quartz job + trigger under the watched key, and configure the
		// watched options as Disabled so the watcher must resolve the scheduler and PAUSE the job.
		var jobKey = new JobKey(JobName, JobGroup);
		var triggerKey = new TriggerKey("watch-trigger", JobGroup);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddQuartz(q =>
		{
			_ = q.AddJob<NoopJob>(jobKey);
			_ = q.AddTrigger(t => t
				.ForJob(jobKey)
				.WithIdentity(triggerKey)
				.WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
		});

		var config = BuildJobConfig(disabled: true);
		services.AddJobWatcher<TestConfigurableJob, TestJobOptions>(config.GetSection("Jobs:Test"));

		using var provider = services.BuildServiceProvider();

		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
		await scheduler.Start();

		// The watcher IS the IHostedService (direct registration, the BCL async-init seam); the deleted
		// AsyncFactoryHostedService/IJobOptionsHostedWatcherServiceFactory triad is registered nowhere.
		var watcher = provider.GetServices<IHostedService>()
			.OfType<JobOptionsHostedWatcherService<TestConfigurableJob, TestJobOptions>>()
			.ShouldHaveSingleItem();

		try
		{
			// Act — StartAsync must SUCCEED (RED on the pre-fix impl: throws InvalidOperationException
			// resolving the unregistered factory) and resolve the scheduler via ISchedulerFactory (the
			// async-init seam), applying the Disabled=true state by pausing the job.
			await watcher.StartAsync(CancellationToken.None);

			// Assert — the trigger is Paused, proving the scheduler was resolved AND used.
			var state = await scheduler.GetTriggerState(triggerKey);
			state.ShouldBe(TriggerState.Paused);

			await watcher.StopAsync(CancellationToken.None);
		}
		finally
		{
			await scheduler.Shutdown();
		}
	}

	private static IConfiguration BuildJobConfig(bool disabled) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jobs:Test:JobName"] = JobName,
				["Jobs:Test:JobGroup"] = JobGroup,
				["Jobs:Test:Disabled"] = disabled ? "true" : "false",
			})
			.Build();

	// --- Test helpers ---

	private sealed class TestJobOptions : JobOptions;

	private sealed class TestConfigurableJob : IConfigurableJob<TestJobOptions>;

	[DisallowConcurrentExecution]
	private sealed class NoopJob : IJob
	{
		public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
	}
}
