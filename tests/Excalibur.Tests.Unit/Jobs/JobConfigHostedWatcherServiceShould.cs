using Excalibur.Jobs;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class JobConfigHostedWatcherServiceShould
{
	[Fact]
	public void ConstructWithValidParameters()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup" };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		// Act
		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public async Task PauseJobWhenDisabled()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = true };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert - PauseJob should be called for disabled jobs
		_ = A.CallTo(() => scheduler.PauseJob(
				A<JobKey>.That.Matches(j => j.Name == "TestJob" && j.Group == "TestGroup"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ResumeJobWhenEnabled()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = false };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert - ResumeJob should be called for enabled jobs
		_ = A.CallTo(() => scheduler.ResumeJob(
				A<JobKey>.That.Matches(j => j.Name == "TestJob" && j.Group == "TestGroup"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
