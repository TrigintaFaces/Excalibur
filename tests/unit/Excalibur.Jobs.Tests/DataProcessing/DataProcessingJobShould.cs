// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.DataProcessing;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Quartz;

namespace Excalibur.Jobs.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessingJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class DataProcessingJobShould
{
	private readonly IDataOrchestrationManager _orchestrationManager = A.Fake<IDataOrchestrationManager>();
	private readonly JobHeartbeatTracker _heartbeatTracker = new();
	private readonly DataProcessingJob _job;

	public DataProcessingJobShould()
	{
		_job = new DataProcessingJob(
			_orchestrationManager,
			_heartbeatTracker,
			NullLogger<DataProcessingJob>.Instance);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOrchestrationManagerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingJob(null!, _heartbeatTracker, NullLogger<DataProcessingJob>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenHeartbeatTrackerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingJob(_orchestrationManager, null!, NullLogger<DataProcessingJob>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingJob(_orchestrationManager, _heartbeatTracker, null!));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() => _job.Execute(null!));
	}

	[Fact]
	public async Task ExecuteJob_CallsProcessDataTasks()
	{
		// Arrange
		var context = CreateJobContext("DataJob", "DataGroup");

		// Act
		await _job.Execute(context);

		// Assert
		A.CallTo(() => _orchestrationManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteJob_RecordsHeartbeat_OnSuccess()
	{
		// Arrange
		var jobName = "DataJob-" + Guid.NewGuid();
		var context = CreateJobContext(jobName, "DataGroup");

		// Act
		await _job.Execute(context);

		// Assert
		_heartbeatTracker.GetLastHeartbeat(jobName).ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteJob_SwallowsException_LogsError()
	{
		// Arrange
		A.CallTo(() => _orchestrationManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Processing failed"));

		var context = CreateJobContext("DataJob", "DataGroup");

		// Act — should not throw (Quartz best practice)
		await _job.Execute(context);
	}

	[Fact]
	public async Task ExecuteJob_RethrowsOperationCanceled_WhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		A.CallTo(() => _orchestrationManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.Throws(new OperationCanceledException(cts.Token));

		var context = CreateJobContext("DataJob", "DataGroup", cts.Token);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() => _job.Execute(context));
	}

	[Fact]
	public void HaveCorrectJobConfigSectionName()
	{
		DataProcessingJob.JobConfigSectionName.ShouldBe("Jobs:DataProcessingJob");
	}

	// --- ConfigureJob honors the Disabled flag (Excalibur.Dispatch-ku1i3e) ---
	// IServiceCollectionQuartzConfigurator cannot be faked, so these drive the real Quartz
	// configurator and inspect the resulting QuartzOptions for the registered job detail.
	// Inspecting options (rather than building a scheduler) keeps the test deterministic — it avoids
	// Quartz's process-global SchedulerRepository, which is shared across parallel test classes.

	[Fact]
	public void ConfigureJobDoesNotRegisterJobWhenDisabled()
	{
		// Arrange — Disabled:true must mean the job is never registered with the scheduler.
		var config = BuildJobConfig(disabled: true);

		// Act
		var registered = IsJobRegistered(config);

		// Assert
		registered.ShouldBeFalse();
	}

	[Fact]
	public void ConfigureJobRegistersJobWhenEnabled()
	{
		// Arrange — control case: proves the assertion above tests the Disabled gate, not a wiring slip.
		var config = BuildJobConfig(disabled: false);

		// Act
		var registered = IsJobRegistered(config);

		// Assert
		registered.ShouldBeTrue();
	}

	private static bool IsJobRegistered(IConfiguration config)
	{
		var services = new ServiceCollection();
		_ = services.AddQuartz(q => DataProcessingJob.ConfigureJob(q, config));
		using var provider = services.BuildServiceProvider();

		var quartzOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<QuartzOptions>>().Value;
		return quartzOptions.JobDetails.Any(j => j.Key.Equals(new JobKey("DataProcessingJob", "TestGroup")));
	}

	private static IConfiguration BuildJobConfig(bool disabled) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["Jobs:DataProcessingJob:JobName"] = "DataProcessingJob",
				["Jobs:DataProcessingJob:JobGroup"] = "TestGroup",
				["Jobs:DataProcessingJob:CronSchedule"] = "0/30 * * * * ?",
				["Jobs:DataProcessingJob:Disabled"] = disabled ? "true" : "false",
			})
			.Build();

	private static IJobExecutionContext CreateJobContext(string jobName, string jobGroup, CancellationToken ct = default)
	{
		var jobKey = new JobKey(jobName, jobGroup);
		var jobDetail = A.Fake<IJobDetail>();
		A.CallTo(() => jobDetail.Key).Returns(jobKey);

		var context = A.Fake<IJobExecutionContext>();
		A.CallTo(() => context.JobDetail).Returns(jobDetail);
		A.CallTo(() => context.CancellationToken).Returns(ct);

		return context;
	}
}
