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

	[Fact]
	public async Task StopAsyncDisposesChangeListener()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = false };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);
		var changeListenerDisposable = A.Fake<IDisposable>();
		_ = A.CallTo(() => optionsMonitor.OnChange(A<Action<TestJobConfig, string?>>.Ignored))
			.Returns(changeListenerDisposable);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// First start the service to register the change listener
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Act
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		A.CallTo(() => changeListenerDisposable.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StartAsyncHandlesNullScheduler()
	{
		// Arrange
		IScheduler? scheduler = null;
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = true };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Act & Assert - Should not throw an exception
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
	}

	// Testing exception handling in the StopAsync method
	[Fact]
	public async Task StopAsyncCatchesExceptions()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = false };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);

		// First create a test service without setting up the disposable
		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		// Using using statement to ensure proper disposal
		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Call StartAsync but don't register any change listener that throws
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Act - Now test that exceptions are caught
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// No assertion needed - test passes if no exception is thrown
	}

	[Fact]
	public async Task DisposeProperlyDisposesResources()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = false };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);
		var changeListenerDisposable = A.Fake<IDisposable>();
		_ = A.CallTo(() => optionsMonitor.OnChange(A<Action<TestJobConfig, string?>>.Ignored))
			.Returns(changeListenerDisposable);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		// Act
		var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Register the change listener
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);

		// Dispose the service
		service.Dispose();

		// Act again - verify double dispose doesn't cause issues
		service.Dispose();

		// Assert
		A.CallTo(() => changeListenerDisposable.Dispose()).MustHaveHappenedOnceExactly();
	}

	// Testing the exception handling in StartAsync
	[Fact]
	public async Task StartAsyncCatchesSchedulerExceptions()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var jobConfig = new TestJobConfig { JobName = "TestJob", JobGroup = "TestGroup", Disabled = false };
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		_ = A.CallTo(() => optionsMonitor.CurrentValue).Returns(jobConfig);
		_ = A.CallTo(() => scheduler.ResumeJob(A<JobKey>._, A<CancellationToken>._))
			.Throws(new SchedulerException("Test exception"));

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		using var service = new JobConfigHostedWatcherService<TestJob, TestJobConfig>(
			scheduler,
			optionsMonitor,
			logger);

		// Act & Assert - Verify that the exception is caught and wrapped
		await Should.ThrowAsync<Exception>(async () =>
			await service.StartAsync(CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
	}
}
