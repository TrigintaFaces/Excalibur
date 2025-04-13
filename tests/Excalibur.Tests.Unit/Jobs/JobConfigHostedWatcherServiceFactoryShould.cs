using Excalibur.Jobs;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class JobConfigHostedWatcherServiceFactoryShould
{
	[Fact]
	public void ImplementIJobConfigHostedWatcherServiceFactoryInterface()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act
		var factory = new JobConfigHostedWatcherServiceFactory(serviceProvider);

		// Assert
		_ = factory.ShouldBeAssignableTo<IJobConfigHostedWatcherServiceFactory>();
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenServiceProviderIsNull()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobConfigHostedWatcherServiceFactory(null!))
			.ParamName.ShouldBe("serviceProvider");
	}

	[Fact]
	public async Task CreateJobConfigHostedWatcherService()
	{
		// Arrange
		var services = new ServiceCollection();

		var schedulerFactory = A.Fake<ISchedulerFactory>();
		var scheduler = A.Fake<IScheduler>();
		var configMonitor = A.Fake<IOptionsMonitor<TestJobConfig>>();
		var logger = A.Fake<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>();

		// Mock scheduler creation
		_ = A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._)).Returns(scheduler);

		// Register all dependencies directly
		_ = services.AddSingleton(schedulerFactory);
		_ = services.AddSingleton(configMonitor);
		_ = services.AddSingleton<ILogger<JobConfigHostedWatcherService<TestJob, TestJobConfig>>>(logger);

		// NOTE: This factory isn't used to create the logger because it's resolved from DI directly
		var loggerFactory = A.Fake<ILoggerFactory>();
		_ = services.AddSingleton(loggerFactory);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act
		var result = await factory.CreateAsync<TestJob, TestJobConfig>().ConfigureAwait(true);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeAssignableTo<IJobConfigHostedWatcherService>();
		_ = result.ShouldBeOfType<JobConfigHostedWatcherService<TestJob, TestJobConfig>>();
	}

	[Fact]
	public async Task ThrowInvalidOperationExceptionWhenDependencyResolutionFails()
	{
		// Arrange
		var services = new ServiceCollection(); // no ISchedulerFactory registered
		var logger = A.Fake<ILogger<JobConfigHostedWatcherServiceFactory>>();
		_ = services.AddSingleton(logger);

		var provider = services.BuildServiceProvider();
		var factory = new JobConfigHostedWatcherServiceFactory(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<TestJob, TestJobConfig>()).ConfigureAwait(true);

		exception.Message.ShouldBe("Failed to create JobConfigHostedWatcherService for TestJob.");
		_ = exception.InnerException.ShouldNotBeNull();
		exception.InnerException.Message.ShouldContain("No service for type");
	}
}
