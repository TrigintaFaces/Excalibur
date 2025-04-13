using Excalibur.Data;
using Excalibur.Data.Outbox;
using Excalibur.Domain;
using Excalibur.Tests.Fakes.Application;
using Excalibur.Tests.Shared;

using FakeItEasy;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Tests.Unit.Data;

public class ServiceCollectionExtensionsShould
{
	private readonly IServiceCollection _serviceCollection;
	private readonly IConfiguration _configuration;

	public ServiceCollectionExtensionsShould()
	{
		_serviceCollection = new ServiceCollection();

		// Create fake configuration with outbox section
		var configurationBuilder = new ConfigurationBuilder();
		_ = configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["OutboxConfiguration:TableName"] = "Outbox",
			["OutboxConfiguration:DeadLetterTableName"] = "OutboxDeadLetter",
			["OutboxConfiguration:MaxAttempts"] = "3",
			["OutboxConfiguration:DispatcherTimeoutMilliseconds"] = "60000"
		});
		_configuration = configurationBuilder.Build();
	}

	[Fact]
	public void RegisterOutboxServicesWhenAddExcaliburDataOutboxServicesCalled()
	{
		// Act
		_ = _serviceCollection.AddSingleton(ActivityContextMother.WithCorrelationAndTenant());
		_ = _serviceCollection.AddSingleton(A.Fake<IDomainDb>());
		_ = _serviceCollection.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		_ = _serviceCollection.AddSingleton<IHostApplicationLifetime, TestAppLifetime>();

		var result = _serviceCollection.AddExcaliburDataOutboxServices(_configuration);
		var serviceProvider = _serviceCollection.BuildServiceProvider();

		// Assert
		result.ShouldBe(_serviceCollection);

		var outbox = serviceProvider.GetService<IOutbox>();
		_ = outbox.ShouldNotBeNull();
		_ = outbox.ShouldBeOfType<Excalibur.Data.Outbox.Outbox>();

		var outboxManager = serviceProvider.GetService<IOutboxManager>();
		_ = outboxManager.ShouldNotBeNull();
		_ = outboxManager.ShouldBeOfType<OutboxManager>();

		var outboxConfig = serviceProvider.GetService<IOptions<OutboxConfiguration>>();
		_ = outboxConfig.ShouldNotBeNull();
		outboxConfig.Value.TableName.ShouldBe("Outbox");
		outboxConfig.Value.DeadLetterTableName.ShouldBe("OutboxDeadLetter");
		outboxConfig.Value.MaxAttempts.ShouldBe(3);
		outboxConfig.Value.DispatcherTimeoutMilliseconds.ShouldBe(60000);
	}

	[Fact]
	public void RegisterMediatorOutboxDispatcherWhenAddExcaliburMediatorOutboxMessageDispatcherCalled()
	{
		// Act
		_ = _serviceCollection.AddSingleton(A.Fake<IPublisher>());

		var result = _serviceCollection.AddExcaliburMediatorOutboxMessageDispatcher();
		var serviceProvider = _serviceCollection.BuildServiceProvider();

		// Assert
		result.ShouldBe(_serviceCollection);

		var dispatcher = serviceProvider.GetService<IOutboxMessageDispatcher>();
		_ = dispatcher.ShouldNotBeNull();
		_ = dispatcher.ShouldBeOfType<MediatorOutboxMessageDispatcher>();
	}

	[Fact]
	public void RegisterRepositoriesWhenAddExcaliburDataRepositoriesCalled()
	{
		// Arrange
		var assembly = typeof(TestAggregateRepository).Assembly;

		// Act
		var result = _serviceCollection.AddExcaliburDataRepositories(assembly);

		// Assert
		result.ShouldBe(_serviceCollection);

		// Verify that repository registration was attempted
		// Note: This does not test the actual registration which requires a full implementation and proper service registration scanning
	}

	[Fact]
	public void RegisterAllServicesWhenAddExcaliburDataServicesCalled()
	{
		// Arrange
		var assembly = typeof(TestAggregateRepository).Assembly;
		_ = _serviceCollection.AddSingleton(ActivityContextMother.WithCorrelationAndTenant());
		_ = _serviceCollection.AddSingleton(A.Fake<IDomainDb>());
		_ = _serviceCollection.AddSingleton(A.Fake<IPublisher>());
		_ = _serviceCollection.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		_ = _serviceCollection.AddSingleton<IHostApplicationLifetime, TestAppLifetime>();

		// Act
		var result = _serviceCollection.AddExcaliburDataServices(_configuration, assembly);
		var serviceProvider = _serviceCollection.BuildServiceProvider();

		// Assert
		result.ShouldBe(_serviceCollection);

		var outbox = serviceProvider.GetService<IOutbox>();
		_ = outbox.ShouldNotBeNull();

		var outboxManager = serviceProvider.GetService<IOutboxManager>();
		_ = outboxManager.ShouldNotBeNull();
	}
}
