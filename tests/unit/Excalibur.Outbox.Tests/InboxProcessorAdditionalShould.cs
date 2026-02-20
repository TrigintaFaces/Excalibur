// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Use alias to avoid namespace collision with Excalibur.Outbox.InboxOptions
using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Additional unit tests for <see cref="InboxProcessor"/>.
/// Covers additional paths and edge cases for improved coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxProcessorAdditionalShould : UnitTestBase
{
	#region Constructor Edge Cases

	[Fact]
	public async Task Constructor_AcceptsAllOptionalDependencies()
	{
		// Arrange
		var options = CreateValidOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var circuitBreakerRegistry = A.Fake<ITransportCircuitBreakerRegistry>();
		var backoffCalculator = A.Fake<IBackoffCalculator>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger,
			telemetryClient: null,
			internalSerializer: null,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_AcceptsExactQueueCapacityEqualToProducerBatchSize()
	{
		// Arrange - QueueCapacity == ProducerBatchSize is valid (edge case)
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 100,
			ProducerBatchSize = 100, // Equal to queue capacity - should be valid
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_DisablesDynamicBatchSizing_WhenNotEnabled()
	{
		// Arrange
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4,
			EnableDynamicBatchSizing = false
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region Init Edge Cases

	[Fact]
	public async Task Init_CanBeCalledMultipleTimes()
	{
		// Arrange
		await using var processor = CreateProcessor();

		// Act - Multiple initialization should not throw
		processor.Init("dispatcher-1");
		processor.Init("dispatcher-2");
		processor.Init("dispatcher-3");

		// Assert - No exception means success
	}

	#endregion

	#region DispatchPendingMessagesAsync with Empty Results

	[Fact]
	public async Task DispatchPendingMessagesAsync_ReturnsZero_WhenStoreReturnsEmptyEntries()
	{
		// Arrange
		var inboxStore = A.Fake<IInboxStore>();
		_ = A.CallTo(() => inboxStore.GetFailedEntriesAsync(
				A<int>._,
				A<DateTimeOffset>._,
				A<int>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<InboxEntry>>(Enumerable.Empty<InboxEntry>()));

		await using var processor = CreateProcessor(inboxStore: inboxStore);
		processor.Init("dispatcher-1");

		// Act
		var result = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	#endregion

	#region Parallel Processing Configuration

	[Fact]
	public async Task Constructor_InitializesWithParallelProcessingDegreeOf1()
	{
		// Arrange
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 1 // Sequential processing
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_InitializesWithHighParallelProcessingDegree()
	{
		// Arrange
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 2000,
			ProducerBatchSize = 500,
			ConsumerBatchSize = 200,
			PerRunTotal = 5000,
			MaxAttempts = 3,
			ParallelProcessingDegree = 16 // High parallelism
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region High Throughput Configuration

	[Fact]
	public async Task Constructor_InitializesWithHighThroughputConfiguration()
	{
		// Arrange
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 2000,
			ProducerBatchSize = 500,
			ConsumerBatchSize = 200,
			PerRunTotal = 5000,
			MaxAttempts = 3,
			ParallelProcessingDegree = 8,
			EnableDynamicBatchSizing = true,
			MinBatchSize = 10,
			MaxBatchSize = 1000,
			BatchProcessingTimeout = TimeSpan.FromMinutes(2),
			EnableBatchDatabaseOperations = true
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region High Reliability Configuration

	[Fact]
	public async Task Constructor_InitializesWithHighReliabilityConfiguration()
	{
		// Arrange
		var options = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 100,
			ProducerBatchSize = 20,
			ConsumerBatchSize = 10,
			PerRunTotal = 200,
			MaxAttempts = 10,
			ParallelProcessingDegree = 1, // Sequential for reliability
			EnableDynamicBatchSizing = false,
			BatchProcessingTimeout = TimeSpan.FromMinutes(10)
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region Disposal Edge Cases

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var processor = CreateProcessor();

		// Act - Multiple disposals should be safe
		for (var i = 0; i < 10; i++)
		{
			await processor.DisposeAsync();
		}

		// Assert - No exception means success
	}

	[Fact]
	public async Task DisposeAsync_HandlesDisposalBeforeInit()
	{
		// Arrange
		var processor = CreateProcessor();
		// Note: Init() not called

		// Act
		await processor.DisposeAsync();

		// Assert - No exception means success
	}

	#endregion

	#region Helper Methods

	private static IOptions<DeliveryInboxOptions> CreateValidOptions()
	{
		return Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4
		});
	}

	private static InboxProcessor CreateProcessor(
		IOptions<DeliveryInboxOptions>? options = null,
		IInboxStore? inboxStore = null,
		IJsonSerializer? serializer = null,
		IServiceProvider? serviceProvider = null,
		ILogger<InboxProcessor>? logger = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null)
	{
		return new InboxProcessor(
			options ?? CreateValidOptions(),
			inboxStore ?? A.Fake<IInboxStore>(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			serializer ?? A.Fake<IJsonSerializer>(),
			logger ?? A.Fake<ILogger<InboxProcessor>>(),
			telemetryClient: null,
			internalSerializer: null,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator);
	}

	#endregion
}
