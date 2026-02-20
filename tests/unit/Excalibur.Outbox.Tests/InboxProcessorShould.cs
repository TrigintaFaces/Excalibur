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
/// Unit tests for <see cref="InboxProcessor"/>.
/// Tests the high-performance inbox processor implementation including batch processing,
/// producer-consumer pattern, circuit breaker integration, and dead letter queue routing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxProcessorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new InboxProcessor(
			null!,
			inboxStore,
			serviceProvider,
			serializer,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenInboxStoreIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new InboxProcessor(
			options,
			null!,
			serviceProvider,
			serializer,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new InboxProcessor(
			options,
			inboxStore,
			null!,
			serializer,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			null!));
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenQueueCapacityLessThanBatchSize()
	{
		// Arrange - Custom options with invalid config (QueueCapacity < ProducerBatchSize)
		var customOptions = Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 10,
			ProducerBatchSize = 100, // Larger than queue capacity - invalid
			ConsumerBatchSize = 100,
			PerRunTotal = 100,
			MaxAttempts = 3,
			ParallelProcessingDegree = 1
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => new InboxProcessor(
			customOptions,
			inboxStore,
			serviceProvider,
			serializer,
			logger));
	}

	[Fact]
	public async Task Constructor_CreatesProcessor_WithValidParameters()
	{
		// Arrange
		var options = CreateValidOptions();
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
	public async Task Constructor_UsesNullObjectPatternDefaults_WhenOptionalDependenciesNotProvided()
	{
		// Arrange
		var options = CreateValidOptions();
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<InboxProcessor>>();

		// Act - Create processor without optional dependencies
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
			logger,
			telemetryClient: null,
			internalSerializer: null,
			deadLetterQueue: null,
			circuitBreakerRegistry: null,
			backoffCalculator: null,
			deliveryGuaranteeOptions: null);

		// Assert - Should not throw and should use null object defaults internally
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region Init Tests

	[Fact]
	public async Task Init_ThrowsArgumentException_WhenDispatcherIdIsNull()
	{
		// Arrange
		await using var processor = CreateProcessor();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => processor.Init(null!));
	}

	[Fact]
	public async Task Init_ThrowsArgumentException_WhenDispatcherIdIsEmpty()
	{
		// Arrange
		await using var processor = CreateProcessor();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => processor.Init(string.Empty));
	}

	[Fact]
	public async Task Init_ThrowsArgumentException_WhenDispatcherIdIsWhitespace()
	{
		// Arrange
		await using var processor = CreateProcessor();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => processor.Init("   "));
	}

	[Fact]
	public async Task Init_Succeeds_WithValidDispatcherId()
	{
		// Arrange
		await using var processor = CreateProcessor();

		// Act & Assert - Should not throw
		processor.Init("dispatcher-1");
	}

	#endregion

	#region DispatchPendingMessagesAsync Tests

	[Fact]
	public async Task DispatchPendingMessagesAsync_ThrowsInvalidOperationException_WhenNotInitialized()
	{
		// Arrange
		await using var processor = CreateProcessor();
		// Note: Init() not called

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange - setup store mock but test doesn't reach it (throws ObjectDisposedException first)
		var inboxStore = A.Fake<IInboxStore>();

		var processor = CreateProcessor(inboxStore: inboxStore);
		processor.Init("dispatcher-1");
		await processor.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_HandlesCancellation_ThrowsTaskCanceledException()
	{
		// Arrange
		var inboxStore = A.Fake<IInboxStore>();

		using var cts = new CancellationTokenSource();
		await using var processor = CreateProcessor(inboxStore: inboxStore);
		processor.Init("dispatcher-1");

		// Cancel immediately
		await cts.CancelAsync();

		// Act & Assert - Cancellation throws TaskCanceledException (expected behavior)
		_ = await Should.ThrowAsync<TaskCanceledException>(
			() => processor.DispatchPendingMessagesAsync(cts.Token));
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes_Safely()
	{
		// Arrange
		var processor = CreateProcessor();
		processor.Init("dispatcher-1");

		// Act - Multiple disposal should not throw
		await processor.DisposeAsync();
		await processor.DisposeAsync();
		await processor.DisposeAsync();

		// Assert - No exception means success
	}

	[Fact]
	public async Task DisposeAsync_CompletesChannel_AndReleasesResources()
	{
		// Arrange
		var processor = CreateProcessor();
		processor.Init("dispatcher-1");

		// Act
		await processor.DisposeAsync();

		// Assert - After disposal, further operations should fail
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
	}

	#endregion

	#region Dynamic Batch Sizing Tests

	[Fact]
	public async Task Constructor_InitializesDynamicBatchSizeCalculator_WhenEnabled()
	{
		// Arrange - Use HighThroughput preset which may have dynamic batch sizing enabled
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
			MaxBatchSize = 1000
		});

		// Act
		await using var processor = CreateProcessor(options: options);

		// Assert - No exception means dynamic batch size calculator was initialized
		_ = processor.ShouldNotBeNull();
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
