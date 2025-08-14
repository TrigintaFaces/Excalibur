using Excalibur.Jobs;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class JobConfigHostedWatcherServiceFactoryAdditionalTests
{
	[Fact]
	public async Task ThrowExceptionWhenSchedulerFactoryNotAvailable()
	{
		// Arrange
		var services = new ServiceCollection();

		// Add everything except ISchedulerFactory
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();
		var loggerFactory = A.Fake<ILoggerFactory>();
		_ = A.CallTo(() => loggerFactory.CreateLogger(A<string>._))
			.Returns(logger);

		_ = services.AddSingleton(optionsMonitor);
		_ = services.AddSingleton(loggerFactory);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<TestJob, TestJobConfig>()).ConfigureAwait(true);

		exception.Message.ShouldContain("Failed to create JobConfigHostedWatcherService");
		exception.InnerException.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowExceptionWhenOptionsMonitorNotAvailable()
	{
		// Arrange
		var services = new ServiceCollection();

		// Add ISchedulerFactory but not IOptionsMonitor
		var schedulerFactory = A.Fake<ISchedulerFactory>();
		var scheduler = A.Fake<IScheduler>();
		_ = A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._)).Returns(scheduler);

		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();
		var loggerFactory = A.Fake<ILoggerFactory>();
		_ = A.CallTo(() => loggerFactory.CreateLogger(A<string>._))
			.Returns(logger);

		_ = services.AddSingleton(schedulerFactory);
		_ = services.AddSingleton(loggerFactory);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<TestJob, TestJobConfig>()).ConfigureAwait(true);

		exception.Message.ShouldContain("Failed to create JobConfigHostedWatcherService");
		exception.InnerException.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowExceptionWhenLoggerFactoryNotAvailable()
	{
		// Arrange
		var services = new ServiceCollection();

		// Add everything except ILoggerFactory
		var schedulerFactory = A.Fake<ISchedulerFactory>();
		var scheduler = A.Fake<IScheduler>();
		_ = A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._)).Returns(scheduler);

		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();

		_ = services.AddSingleton(schedulerFactory);
		_ = services.AddSingleton(optionsMonitor);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<TestJob, TestJobConfig>()).ConfigureAwait(true);

		exception.Message.ShouldContain("Failed to create JobConfigHostedWatcherService");
		exception.InnerException.ShouldNotBeNull();
	}

	// Simplified test that doesn't try to verify LogError directly, which is a static extension method
	[Fact]
	public async Task HandleExceptionWhenSchedulerFactoryThrows()
	{
		// Arrange
		var services = new ServiceCollection();

		// Setup the minimum required services
		var schedulerFactory = A.Fake<ISchedulerFactory>();
		_ = A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._))
			.ThrowsAsync(new SchedulerException("Cannot create scheduler"));

		var factoryLogger = A.Fake<ILogger<JobConfigHostedWatcherServiceFactory>>();
		var loggerFactory = A.Fake<ILoggerFactory>();
		var optionsMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();

		_ = services.AddSingleton(schedulerFactory);
		_ = services.AddSingleton(optionsMonitor);
		_ = services.AddSingleton(loggerFactory);
		_ = services.AddSingleton(factoryLogger);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act & Assert - We expect an exception about inability to create the service
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await factory.CreateAsync<TestJob, TestJobConfig>().ConfigureAwait(true)).ConfigureAwait(true);

		exception.Message.ShouldContain("Failed to create JobConfigHostedWatcherService");
		exception.InnerException.ShouldBeOfType<SchedulerException>();
	}
}
