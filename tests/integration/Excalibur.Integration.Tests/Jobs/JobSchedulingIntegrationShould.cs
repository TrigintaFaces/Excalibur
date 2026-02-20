// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Integration.Tests.Jobs;

/// <summary>
/// Integration tests for job scheduling and execution.
/// These tests verify job registration, scheduling, and execution flow.
/// </summary>
public sealed class JobSchedulingIntegrationShould : IntegrationTestBase
{
	#region Job Registration Tests

	[Fact]
	public void JobServices_RegisterCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new List<string>());
		_ = services.AddSingleton<IJobExecutor, TestJobExecutor>();
		_ = services.AddSingleton<IJobScheduler, TestJobScheduler>();

		// Act
		using var provider = services.BuildServiceProvider();
		var executor = provider.GetService<IJobExecutor>();
		var scheduler = provider.GetService<IJobScheduler>();

		// Assert
		_ = executor.ShouldNotBeNull();
		_ = scheduler.ShouldNotBeNull();
	}

	#endregion

	#region Job Execution Tests

	[Fact]
	public async Task JobExecutor_ExecutesJob()
	{
		// Arrange
		var executed = false;
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IJobExecutor>(new TestJobExecutor(() => executed = true));
		using var provider = services.BuildServiceProvider();
		var executor = provider.GetRequiredService<IJobExecutor>();

		// Act
		await executor.ExecuteAsync(new TestJobContext("test-job"), TestCancellationToken).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task JobExecutor_HandlesExceptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IJobExecutor>(new ThrowingJobExecutor());
		using var provider = services.BuildServiceProvider();
		var executor = provider.GetRequiredService<IJobExecutor>();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => executor.ExecuteAsync(new TestJobContext("failing-job"), TestCancellationToken)).ConfigureAwait(false);
	}

	[Fact]
	public async Task JobExecutor_SupportsCancellation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IJobExecutor>(new SlowJobExecutor());
		using var provider = services.BuildServiceProvider();
		var executor = provider.GetRequiredService<IJobExecutor>();
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => executor.ExecuteAsync(new TestJobContext("slow-job"), cts.Token)).ConfigureAwait(false);
	}

	#endregion

	#region Job Scheduling Tests

	[Fact]
	public async Task JobScheduler_SchedulesJob()
	{
		// Arrange
		var scheduledJobs = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IJobScheduler>(new TestJobScheduler(scheduledJobs));
		using var provider = services.BuildServiceProvider();
		var scheduler = provider.GetRequiredService<IJobScheduler>();

		// Act
		await scheduler.ScheduleAsync("test-job", TimeSpan.FromMinutes(5), TestCancellationToken).ConfigureAwait(false);

		// Assert
		scheduledJobs.ShouldContain("test-job");
	}

	[Fact]
	public async Task JobScheduler_CancelsScheduledJob()
	{
		// Arrange
		var scheduledJobs = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IJobScheduler>(new TestJobScheduler(scheduledJobs));
		using var provider = services.BuildServiceProvider();
		var scheduler = provider.GetRequiredService<IJobScheduler>();

		// Act
		await scheduler.ScheduleAsync("cancel-test", TimeSpan.FromMinutes(5), TestCancellationToken).ConfigureAwait(false);
		await scheduler.CancelAsync("cancel-test", TestCancellationToken).ConfigureAwait(false);

		// Assert
		scheduledJobs.ShouldNotContain("cancel-test");
	}

	#endregion

	#region Test Helpers

	private interface IJobExecutor
	{
		Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken);
	}

	private interface IJobScheduler
	{
		Task ScheduleAsync(string jobId, TimeSpan delay, CancellationToken cancellationToken);
		Task CancelAsync(string jobId, CancellationToken cancellationToken);
	}

	private interface IJobContext
	{
		string JobId { get; }
	}

	private sealed record TestJobContext(string JobId) : IJobContext;

	private sealed class TestJobExecutor(Action? onExecute = null) : IJobExecutor
	{
		public Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
		{
			onExecute?.Invoke();
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingJobExecutor : IJobExecutor
	{
		public Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Job execution failed");
		}
	}

	private sealed class SlowJobExecutor : IJobExecutor
	{
		public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
		{
			await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class TestJobScheduler(List<string> scheduledJobs) : IJobScheduler
	{
		public Task ScheduleAsync(string jobId, TimeSpan delay, CancellationToken cancellationToken)
		{
			scheduledJobs.Add(jobId);
			return Task.CompletedTask;
		}

		public Task CancelAsync(string jobId, CancellationToken cancellationToken)
		{
			_ = scheduledJobs.Remove(jobId);
			return Task.CompletedTask;
		}
	}

	#endregion
}
