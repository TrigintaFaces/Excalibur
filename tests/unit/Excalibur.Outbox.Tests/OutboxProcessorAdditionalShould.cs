// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;

using Excalibur.Dispatch.Options.Delivery;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Use alias to avoid namespace collision with Excalibur.Outbox.OutboxOptions
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Additional unit tests for <see cref="OutboxProcessor"/>.
/// Covers additional paths and edge cases for improved coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxProcessorAdditionalShould : UnitTestBase
{
	#region Constructor Edge Cases

	[Fact]
	public async Task Constructor_AcceptsAllOptionalDependencies()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var circuitBreakerRegistry = A.Fake<ITransportCircuitBreakerRegistry>();
		var backoffCalculator = A.Fake<IBackoffCalculator>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 100,
			ProducerBatchSize = 100, // Equal to queue capacity - should be valid
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_DisablesDynamicBatchSizing_WhenNotEnabled()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4,
			EnableDynamicBatchSizing = false
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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

	#region Parallel Processing Configuration

	[Fact]
	public async Task Constructor_InitializesWithParallelProcessingDegreeOf1()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 1 // Sequential processing
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_InitializesWithHighParallelProcessingDegree()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 2000,
			ProducerBatchSize = 500,
			ConsumerBatchSize = 200,
			PerRunTotal = 5000,
			MaxAttempts = 3,
			ParallelProcessingDegree = 16 // High parallelism
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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
		var options = Options.Create(new DeliveryOutboxOptions
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

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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
		var options = Options.Create(new DeliveryOutboxOptions
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

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region Delivery Guarantee Configurations

	[Fact]
	public async Task Constructor_InitializesWithAtLeastOnceDeliveryGuarantee()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4,
			DeliveryGuarantee = OutboxDeliveryGuarantee.AtLeastOnce
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_InitializesWithMinimizedWindowDeliveryGuarantee()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4,
			DeliveryGuarantee = OutboxDeliveryGuarantee.MinimizedWindow
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task Constructor_InitializesWithTransactionalWhenApplicableDeliveryGuarantee()
	{
		// Arrange
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 500,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			PerRunTotal = 1000,
			MaxAttempts = 5,
			ParallelProcessingDegree = 4,
			DeliveryGuarantee = OutboxDeliveryGuarantee.TransactionalWhenApplicable
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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

	private static IOptions<DeliveryOutboxOptions> CreateValidOptions()
	{
		return Options.Create(DeliveryOutboxOptions.Balanced());
	}

	private static OutboxProcessor CreateProcessor(
		IOptions<DeliveryOutboxOptions>? options = null,
		IOutboxStore? outboxStore = null,
		IJsonSerializer? serializer = null,
		IServiceProvider? serviceProvider = null,
		ILogger<OutboxProcessor>? logger = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null)
	{
		return new OutboxProcessor(
			options ?? CreateValidOptions(),
			outboxStore ?? A.Fake<IOutboxStore>(),
			serializer ?? A.Fake<IJsonSerializer>(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			logger ?? A.Fake<ILogger<OutboxProcessor>>(),
			telemetryClient: null,
			internalSerializer: null,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator);
	}

	#endregion
}
