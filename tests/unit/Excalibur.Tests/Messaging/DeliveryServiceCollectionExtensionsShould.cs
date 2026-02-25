// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Messaging;

/// <summary>
///     Unit tests for DeliveryServiceCollectionExtensions to verify service registration functionality.
/// </summary>
[Trait("Category", "Unit")]
public class DeliveryServiceCollectionExtensionsShould
{
	private readonly IServiceCollection _services = new ServiceCollection();

	private interface ITestService
	{
	}

	[Fact]
	public void AddOutboxShouldRegisterOutboxStoreService()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var outboxStore = serviceProvider.GetService<IOutboxStore>();
		_ = outboxStore.ShouldNotBeNull();
		_ = outboxStore.ShouldBeOfType<TestOutboxStore>();
	}

	[Fact]
	public void AddOutboxShouldConfigureDefaultOptions()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var options = serviceProvider.GetService<IOptions<OutboxOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.PerRunTotal.ShouldBe(10000);
		options.Value.QueueCapacity.ShouldBe(5000);
		options.Value.ProducerBatchSize.ShouldBe(100);
		options.Value.ConsumerBatchSize.ShouldBe(10);
		options.Value.MaxAttempts.ShouldBe(5);
		options.Value.DefaultMessageTimeToLive.ShouldBeNull();
	}

	[Fact]
	public void AddOutboxShouldAcceptCustomConfiguration()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>(static options =>
		{
			options.PerRunTotal = 5000;
			options.QueueCapacity = 2500;
			options.ProducerBatchSize = 50;
			options.ConsumerBatchSize = 5;
			options.MaxAttempts = 3;
			options.DefaultMessageTimeToLive = TimeSpan.FromHours(24);
		});

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var options = serviceProvider.GetService<IOptions<OutboxOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.PerRunTotal.ShouldBe(5000);
		options.Value.QueueCapacity.ShouldBe(2500);
		options.Value.ProducerBatchSize.ShouldBe(50);
		options.Value.ConsumerBatchSize.ShouldBe(5);
		options.Value.MaxAttempts.ShouldBe(3);
		options.Value.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void AddOutboxShouldReturnServiceCollection()
	{
		// Act
		var result = _services.AddOutbox<TestOutboxStore>();

		// Assert
		result.ShouldBeSameAs(_services);
	}

	[Fact]
	public void AddOutboxShouldRegisterServiceAsSingleton()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var store1 = serviceProvider.GetService<IOutboxStore>();
		var store2 = serviceProvider.GetService<IOutboxStore>();

		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddOutboxShouldNotReplaceExistingRegistration()
	{
		// Arrange
		_ = _services.AddSingleton<IOutboxStore, AnotherTestOutboxStore>();

		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var outboxStore = serviceProvider.GetService<IOutboxStore>();
		_ = outboxStore.ShouldBeOfType<AnotherTestOutboxStore>();
	}

	[Fact]
	public void AddOutboxShouldHandleNullConfigurationAction()
	{
		// Act & Assert
		_ = Should.NotThrow(() => _services.AddOutbox<TestOutboxStore>(configure: null));

		var serviceProvider = _services.BuildServiceProvider();
		var options = serviceProvider.GetService<IOptions<OutboxOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.PerRunTotal.ShouldBe(10000); // Default value
	}

	[Fact]
	public void AddOutboxShouldAllowMultipleCallsWithDifferentStores()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>();
		_ = _services.AddOutbox<AnotherTestOutboxStore>(); // This should not replace the first

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var outboxStore = serviceProvider.GetService<IOutboxStore>();
		_ = outboxStore.ShouldBeOfType<TestOutboxStore>(); // First registration should remain
	}

	[Fact]
	public void AddOutboxShouldWorkWithDifferentStoreTypes()
	{
		// Arrange
		var services1 = new ServiceCollection();
		var services2 = new ServiceCollection();

		// Act
		_ = services1.AddOutbox<TestOutboxStore>();
		_ = services2.AddOutbox<AnotherTestOutboxStore>();

		// Assert
		var provider1 = services1.BuildServiceProvider();
		var provider2 = services2.BuildServiceProvider();

		var store1 = provider1.GetService<IOutboxStore>();
		var store2 = provider2.GetService<IOutboxStore>();

		_ = store1.ShouldBeOfType<TestOutboxStore>();
		_ = store2.ShouldBeOfType<AnotherTestOutboxStore>();
	}

	[Fact]
	public void AddOutboxShouldValidateOptionsConfiguration()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>(options => options.PerRunTotal = -1);

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		_ = Should.Throw<OptionsValidationException>(() =>
		{
			var options = serviceProvider.GetRequiredService<IOptions<OutboxOptions>>();
			_ = options.Value; // This should trigger validation
		});
	}

	[Fact]
	public void AddOutboxShouldPreserveOtherServiceRegistrations()
	{
		// Arrange
		_ = _services.AddSingleton<ITestService, TestService>();

		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var testService = serviceProvider.GetService<ITestService>();
		var outboxStore = serviceProvider.GetService<IOutboxStore>();

		_ = testService.ShouldNotBeNull();
		_ = testService.ShouldBeOfType<TestService>();
		_ = outboxStore.ShouldNotBeNull();
		_ = outboxStore.ShouldBeOfType<TestOutboxStore>();
	}

	[Fact]
	public void AddOutboxShouldSupportCustomOptionsPattern()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>(static options =>
		{
			options.PerRunTotal = 1000;
			options.QueueCapacity = 500;
		});

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var optionsSnapshot = serviceProvider.GetService<IOptionsSnapshot<OutboxOptions>>();
		_ = optionsSnapshot.ShouldNotBeNull();
		optionsSnapshot.Value.PerRunTotal.ShouldBe(1000);
		optionsSnapshot.Value.QueueCapacity.ShouldBe(500);
	}

	[Fact]
	public void AddOutboxShouldRegisterWithCorrectServiceLifetime()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>();

		// Assert
		var outboxDescriptor = _services.FirstOrDefault(static s => s.ServiceType == typeof(IOutboxStore));
		_ = outboxDescriptor.ShouldNotBeNull();
		outboxDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		outboxDescriptor.ImplementationType.ShouldBe(typeof(TestOutboxStore));
	}

	[Fact]
	public void AddOutboxShouldHandleComplexConfigurationScenarios()
	{
		// Act
		_ = _services.AddOutbox<TestOutboxStore>(static options =>
		{
			options.PerRunTotal = 20000;
			options.QueueCapacity = 10000;
			options.ProducerBatchSize = 200;
			options.ConsumerBatchSize = 20;
			options.MaxAttempts = 10;
			options.DefaultMessageTimeToLive = TimeSpan.FromDays(7);
		});

		// Assert
		var serviceProvider = _services.BuildServiceProvider();
		var options = serviceProvider.GetService<IOptions<OutboxOptions>>();
		_ = options.ShouldNotBeNull();

		var value = options.Value;
		value.PerRunTotal.ShouldBe(20000);
		value.QueueCapacity.ShouldBe(10000);
		value.ProducerBatchSize.ShouldBe(200);
		value.ConsumerBatchSize.ShouldBe(20);
		value.MaxAttempts.ShouldBe(10);
		value.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void AddOutboxShouldWorkWithGenericConstraints() =>
		// Act & Assert
		Should.NotThrow(() =>
		{
			_ = _services.AddOutbox<TestOutboxStore>();
			_ = _services.AddOutbox<AnotherTestOutboxStore>();
		});

	[Fact]
	public void AddOutboxShouldSupportFluentConfiguration()
	{
		// Act
		var result = _services
			.AddOutbox<TestOutboxStore>(static options => options.PerRunTotal = 1500)
			.AddSingleton<ITestService, TestService>();

		// Assert
		result.ShouldBeSameAs(_services);
		var serviceProvider = _services.BuildServiceProvider();

		_ = serviceProvider.GetService<IOutboxStore>().ShouldNotBeNull();
		_ = serviceProvider.GetService<ITestService>().ShouldNotBeNull();
	}

	// Test helper classes
	private sealed class TestOutboxStore : IOutboxStore
	{
		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries = 3, DateTimeOffset? olderThan = null, int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize = 1000, CancellationToken cancellationToken = default)
			=> new(0);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
			=> new(new OutboxStatistics());
	}

	private sealed class AnotherTestOutboxStore : IOutboxStore
	{
		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken = default)
			=> default;

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries = 3, DateTimeOffset? olderThan = null, int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize = 100, CancellationToken cancellationToken = default)
			=> new(Enumerable.Empty<OutboundMessage>());

		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize = 1000, CancellationToken cancellationToken = default)
			=> new(0);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
			=> new(new OutboxStatistics());
	}

	private sealed class TestService : ITestService
	{
	}
}
