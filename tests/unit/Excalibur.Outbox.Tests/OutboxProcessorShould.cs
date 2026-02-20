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

// Use alias to avoid namespace collision with Excalibur.Outbox.OutboxOptions
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxProcessor"/>.
/// Tests the high-performance outbox processor implementation including batch processing,
/// producer-consumer pattern, circuit breaker integration, and dead letter queue routing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxProcessorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			null!,
			outboxStore,
			serializer,
			serviceProvider,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOutboxStoreIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			options,
			null!,
			serializer,
			serviceProvider,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			null!,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			null!));
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenQueueCapacityLessThanBatchSize()
	{
		// Arrange - Custom options with invalid config (QueueCapacity < ProducerBatchSize)
		var customOptions = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 10,
			ProducerBatchSize = 100, // Larger than queue capacity - invalid
			ConsumerBatchSize = 100,
			PerRunTotal = 100,
			MaxAttempts = 3,
			ParallelProcessingDegree = 1
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => new OutboxProcessor(
			customOptions,
			outboxStore,
			serializer,
			serviceProvider,
			logger));
	}

	[Fact]
	public async Task Constructor_CreatesProcessor_WithValidParameters()
	{
		// Arrange
		var options = CreateValidOptions();
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
	public async Task Constructor_UsesNullObjectPatternDefaults_WhenOptionalDependenciesNotProvided()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = A.Fake<IJsonSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = A.Fake<ILogger<OutboxProcessor>>();

		// Act - Create processor without optional dependencies
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
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
	public async Task DispatchPendingMessagesAsync_ReturnsZero_WhenNoMessagesAvailable()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		await using var processor = CreateProcessor(outboxStore: outboxStore);
		processor.Init("dispatcher-1");

		// Act
		var result = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		var processor = CreateProcessor(outboxStore: outboxStore);
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
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		using var cts = new CancellationTokenSource();
		await using var processor = CreateProcessor(outboxStore: outboxStore);
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
		// Arrange - Use HighThroughput preset which has dynamic batch sizing enabled
		var options = Options.Create(DeliveryOutboxOptions.HighThroughput());

		// Act
		await using var processor = CreateProcessor(options: options);

		// Assert - No exception means dynamic batch size calculator was initialized
		_ = processor.ShouldNotBeNull();
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

	private static OutboundMessage CreateOutboundMessage(string id, string messageType, int retryCount = 0)
	{
		return new OutboundMessage
		{
			Id = id,
			MessageType = messageType,
			Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = retryCount
		};
	}

	#endregion
}
