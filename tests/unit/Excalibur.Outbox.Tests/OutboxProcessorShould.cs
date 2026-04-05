using Microsoft.Extensions.Logging.Abstractions;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Test class has high coupling by design

using System.Buffers;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Use alias to avoid namespace collision with Excalibur.Outbox.OutboxOptions
using DispatchMessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using DeliveryMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;
using DeliveryGuaranteeOptions = Excalibur.Dispatch.Options.Delivery.DeliveryGuaranteeOptions;
using DeliveryOutboxDeliveryGuarantee = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryGuarantee;
using DeliveryOutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

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
	/// <summary>
	/// Shared JSON options matching the DispatchJsonSerializer's camelCase configuration
	/// for creating test payloads that the real serializer can deserialize.
	/// </summary>
	private static readonly JsonSerializerOptions s_testJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<OutboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
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
			BatchProcessing = { ParallelProcessingDegree = 1 }
		});

		var outboxStore = A.Fake<IOutboxStore>();
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

		// Act - Create processor without optional dependencies
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger,

			envelopeDeserializer: null,
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

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksMessageSent_WhenDispatchSucceeds()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "message-success",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Success());

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync("message-success", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_DispatchesCommand_WhenOutboxContainsNonEventMessage()
	{
		// Arrange -- T.2 (tqg8h): OutboxProcessor now accepts IDispatchMessage, not just IIntegrationEvent.
		// This test proves commands (IDispatchAction) flow through the outbox correctly.
		var messageType = typeof(TestOutboxCommand).Name;
		MessageTypeRegistry.RegisterType<TestOutboxCommand>();

		var outboundMessage = CreateOutboundMessageWithEnvelope(
			"command-message",
			messageType,
			new TestOutboxCommand("do-something"));

		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>([outboundMessage]));

		var serializer = new DispatchJsonSerializer();
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-command");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(m => m is TestOutboxCommand),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => outboxStore.MarkSentAsync("command-message", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksMessageFailedForRetry_WhenDispatchFailsBeforeMaxAttempts()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "message-retry",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Failed("dispatch failed"));

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync("message-retry", "dispatch failed", 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesMessageToDeadLetterQueue_WhenDispatchFailsAtMaxAttempts()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "message-dlq",
			maxAttempts: 1,
			dispatchResult: DispatchMessageResult.Failed("terminal failure"));

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "message-dlq"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(
				"message-dlq",
				A<string>.That.Contains("Moved to DLQ: Max retries exceeded"),
				1,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_HandlesMixedSuccessAndTerminalFailure()
	{
		// Arrange
		await using var scenario = await CreateParallelMixedResultScenarioAsync();

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync("message-success", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "message-failure"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(
				"message-failure",
				A<string>.That.Contains("Moved to DLQ: Max retries exceeded"),
				1,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_RoutesCircuitOpenBatchToDeadLetterQueue()
	{
		// Arrange
		await using var scenario = await CreateParallelCircuitOpenScenarioAsync();

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedTwiceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(
				A<string>.That.Matches(id => id == "message-open-1" || id == "message-open-2"),
				A<string>.That.Contains("Moved to DLQ: Circuit breaker open"),
				3,
				A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_TransactionalGuarantee_UsesTransactionalMarkSent_WhenStoreSupportsTransactions()
	{
		// Arrange
		var transactionalStore = A.Fake<ITransactionalOutboxStore>();
		await using var scenario = await CreateTransactionalDispatchScenarioAsync(transactionalStore, supportsTransactions: true);

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => transactionalStore.MarkSentTransactionalAsync(
				A<IReadOnlyList<string>>.That.Matches(ids =>
					ids.Count == 1 && ids[0] == "message-transactional"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => transactionalStore.MarkSentAsync("message-transactional", A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_TransactionalGuarantee_FallsBackToMarkSent_WhenStoreDoesNotSupportTransactions()
	{
		// Arrange
		var transactionalStore = A.Fake<ITransactionalOutboxStore>();
		await using var scenario = await CreateTransactionalDispatchScenarioAsync(transactionalStore, supportsTransactions: false);

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => transactionalStore.MarkSentAsync("message-transactional", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => transactionalStore.MarkSentTransactionalAsync(A<IReadOnlyList<string>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_DoesNotCalculateBackoff_WhenAutomaticRetryIsDisabled()
	{
		// Arrange
		var backoffCalculator = A.Fake<IBackoffCalculator>();
		var deliveryGuaranteeOptions = Options.Create(new DeliveryGuaranteeOptions
		{
			EnableAutomaticRetry = false
		});

		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "message-no-auto-retry",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Failed("dispatch failed"),
			backoffCalculator: backoffCalculator,
			deliveryGuaranteeOptions: deliveryGuaranteeOptions);

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync("message-no-auto-retry", "dispatch failed", 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => backoffCalculator.CalculateDelay(A<int>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_UsesEnvelopePayload_WhenInternalSerializerIsConfigured()
	{
		// Arrange
		var setup = await CreateEnvelopeDispatchScenarioAsync();
		await using var scenario = setup.Scenario;

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		setup.EnvelopeDeserializer.DeserializeCalls.ShouldBeGreaterThan(0);
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(setup.EnvelopeMessageId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_WithBatchDatabaseOperations_HandlesSuccessAndRetry()
	{
		// Arrange
		await using var scenario = await CreateParallelBatchRetryScenarioAsync();

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync("message-success", A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync("message-retryable", ErrorConstants.RetryAttempt, 1, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_MinimizedWindow_MarksSentImmediatelyPerMessage()
	{
		// Arrange
		await using var scenario = await CreateParallelMinimizedWindowScenarioAsync();

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync("message-minimized", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
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

	private static IOptions<DeliveryOutboxOptions> CreateSingleMessageOptions(int maxAttempts)
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 1,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = maxAttempts,
			BatchProcessing = { ParallelProcessingDegree = 1 }
		});
	}

	private static IOptions<DeliveryOutboxOptions> CreateParallelOptions(int maxAttempts)
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 2,
			ConsumerBatchSize = 2,
			PerRunTotal = 2,
			MaxAttempts = maxAttempts,
			BatchProcessing = { ParallelProcessingDegree = 2 },
			EnableBatchDatabaseOperations = false
		});
	}

	private static IOptions<DeliveryOutboxOptions> CreateTransactionalOptions()
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 3,
			BatchProcessing = { ParallelProcessingDegree = 2 },
			EnableBatchDatabaseOperations = false,
			DeliveryGuarantee = DeliveryOutboxDeliveryGuarantee.TransactionalWhenApplicable
		});
	}

	private static IOptions<DeliveryOutboxOptions> CreateParallelBatchDatabaseOptions(
		int maxAttempts,
		DeliveryOutboxDeliveryGuarantee deliveryGuarantee = DeliveryOutboxDeliveryGuarantee.AtLeastOnce)
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 2,
			ConsumerBatchSize = 2,
			PerRunTotal = 2,
			MaxAttempts = maxAttempts,
			BatchProcessing = { ParallelProcessingDegree = 2 },
			EnableBatchDatabaseOperations = true,
			DeliveryGuarantee = deliveryGuarantee
		});
	}

	private static IOptions<DeliveryOutboxOptions> CreateParallelMinimizedWindowOptions()
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 4,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 3,
			BatchProcessing = { ParallelProcessingDegree = 2 },
			EnableBatchDatabaseOperations = true,
			DeliveryGuarantee = DeliveryOutboxDeliveryGuarantee.MinimizedWindow
		});
	}

	private static OutboxProcessor CreateProcessor(
		IOptions<DeliveryOutboxOptions>? options = null,
		IOutboxStore? outboxStore = null,
		DispatchJsonSerializer? serializer = null,
		IServiceProvider? serviceProvider = null,
		ILogger<OutboxProcessor>? logger = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null,
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null)
	{
		return new OutboxProcessor(
			options ?? CreateValidOptions(),
			outboxStore ?? A.Fake<IOutboxStore>(),
			serializer ?? new DispatchJsonSerializer(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			logger ?? NullLogger<OutboxProcessor>.Instance,

			envelopeDeserializer: envelopeDeserializer,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator,
			deliveryGuaranteeOptions: deliveryGuaranteeOptions);
	}

	private static async Task<DispatchScenario> CreateDispatchScenarioAsync(
		string messageId,
		int maxAttempts,
		IMessageResult dispatchResult,
		IBackoffCalculator? backoffCalculator = null,
		IOptions<DeliveryGuaranteeOptions>? deliveryGuaranteeOptions = null)
	{
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();

		var outboundMessage = CreateOutboundMessageWithEnvelope(
			messageId,
			messageType,
			new TestOutboxIntegrationEvent(messageId));

		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>([outboundMessage]));

		// Use real DispatchJsonSerializer -- DispatchJsonSerializer is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(dispatchResult));

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));

		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			backoffCalculator: backoffCalculator,
			deliveryGuaranteeOptions: deliveryGuaranteeOptions);
		processor.Init("dispatcher-1");

		await Task.CompletedTask;
		return new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);
	}

	private static async Task<DispatchScenario> CreateTransactionalDispatchScenarioAsync(
		ITransactionalOutboxStore transactionalStore,
		bool supportsTransactions)
	{
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();

		var outboundMessage = CreateOutboundMessageWithEnvelope(
			"message-transactional",
			messageType,
			new TestOutboxIntegrationEvent("transactional"));

		var fetchCount = 0;
		_ = A.CallTo(() => transactionalStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? [outboundMessage]
					: [];
				return new ValueTask<IEnumerable<OutboundMessage>>(batch);
			});
		_ = A.CallTo(() => transactionalStore.SupportsTransactions).Returns(supportsTransactions);
		_ = A.CallTo(() => transactionalStore.MarkSentTransactionalAsync(A<IReadOnlyList<string>>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Use real DispatchJsonSerializer -- DispatchJsonSerializer is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateTransactionalOptions(),
			outboxStore: transactionalStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-transactional");

		await Task.CompletedTask;
		return new DispatchScenario(processor, transactionalStore, deadLetterQueue, serviceProvider, dispatcher);
	}

	private static async Task<EnvelopeDispatchScenario> CreateEnvelopeDispatchScenarioAsync()
	{
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		var envelopeMessageId = "11111111-1111-1111-1111-111111111111";
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();

		var outboxStore = CreateSingleMessageOutboxStore(
			CreateBinaryEnvelopeOutboundMessage("message-envelope", messageType));

		var envelopePayload = CreateNestedOutboxMessagePayload(
			envelopeMessageId,
			messageType,
			new TestOutboxIntegrationEvent("from-envelope"));

		var envelopeDeserializer = CreateEnvelopeDeserializer(
			Guid.Parse(envelopeMessageId),
			messageType,
			envelopePayload);
		var serializer = CreateEnvelopeAwareJsonSerializer(envelopeMessageId, messageType);
		var dispatcher = CreateSuccessDispatcher();
		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			envelopeDeserializer: envelopeDeserializer);
		processor.Init("dispatcher-envelope");

		await Task.CompletedTask;
		return new EnvelopeDispatchScenario(
			new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher),
			envelopeDeserializer,
			envelopeMessageId);
	}

	private static async Task<DispatchScenario> CreateParallelBatchRetryScenarioAsync()
	{
		var successType = typeof(TestParallelSuccessIntegrationEvent).Name;
		var retryType = typeof(TestParallelFailureIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();
		MessageTypeRegistry.RegisterType<TestParallelFailureIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope("message-success", successType, new TestParallelSuccessIntegrationEvent("ok")),
			CreateOutboundMessageWithEnvelope("message-retryable", retryType, new TestParallelFailureIntegrationEvent("retry")));

		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelSuccessIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelFailureIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("retryable failure")));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateParallelBatchDatabaseOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-parallel-batch");

		await Task.CompletedTask;
		return new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);
	}

	private static async Task<DispatchScenario> CreateParallelMinimizedWindowScenarioAsync()
	{
		var messageType = typeof(TestParallelSuccessIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();

		var outboxStore = CreateSingleMessageOutboxStore(
			CreateOutboundMessageWithEnvelope("message-minimized", messageType, new TestParallelSuccessIntegrationEvent("minimized")));
		var serializer = new DispatchJsonSerializer();
		var dispatcher = CreateSuccessDispatcher();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelSuccessIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateParallelMinimizedWindowOptions(),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-minimized-window");

		await Task.CompletedTask;
		return new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);
	}

	private static async Task<DispatchScenario> CreateParallelMixedResultScenarioAsync()
	{
		var successType = typeof(TestParallelSuccessIntegrationEvent).Name;
		var failureType = typeof(TestParallelFailureIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();
		MessageTypeRegistry.RegisterType<TestParallelFailureIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope("message-success", successType, new TestParallelSuccessIntegrationEvent("ok")),
			CreateOutboundMessageWithEnvelope("message-failure", failureType, new TestParallelFailureIntegrationEvent("failed")));

		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelSuccessIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelFailureIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("parallel failure")));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateParallelOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-parallel");

		await Task.CompletedTask;
		return new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);
	}

	private static async Task<DispatchScenario> CreateParallelCircuitOpenScenarioAsync()
	{
		var messageType = typeof(TestParallelSuccessIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope("message-open-1", messageType, new TestParallelSuccessIntegrationEvent("open")),
			CreateOutboundMessageWithEnvelope("message-open-2", messageType, new TestParallelSuccessIntegrationEvent("open")));

		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreakerRegistry = CreateCircuitOpenRegistry();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateParallelOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry);
		processor.Init("dispatcher-open");

		await Task.CompletedTask;
		return new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);
	}


	private static ServiceProvider CreateServiceProvider(IDispatcher dispatcher)
	{
		var services = new ServiceCollection();
		_ = services.AddScoped(_ => dispatcher);
		return services.BuildServiceProvider();
	}

	private static IOutboxStore CreateSingleMessageOutboxStore(OutboundMessage message)
	{
		var outboxStore = A.Fake<IOutboxStore>();
		var fetchCount = 0;
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? [message]
					: [];
				return new ValueTask<IEnumerable<OutboundMessage>>(batch);
			});
		return outboxStore;
	}

	private static OutboundMessage CreateBinaryEnvelopeOutboundMessage(string messageId, string messageType)
	{
		var outboundMessage = CreateOutboundMessage(messageId, messageType, payloadHint: "{}");
		outboundMessage.Payload = [0x01, 0xAA, 0xBB, 0xCC];
		return outboundMessage;
	}

	/// <summary>
	/// Creates UTF-8 bytes of a serialized <see cref="DeliveryOutboxMessage"/> containing
	/// nested event JSON and metadata JSON. This payload is what the <see cref="OutboxProcessor"/>
	/// expects to find in <c>OutboxEnvelope.Payload</c> after the internal serializer
	/// deserializes the binary envelope format.
	/// </summary>
	/// <remarks>
	/// The flow is:
	/// 1. <c>ConvertToOutboxMessageWithEnvelopeSupport</c> sets <c>messageBody = UTF8.GetString(envelope.Payload)</c>
	/// 2. <c>DispatchAsync</c> deserializes <c>messageBody</c> as <c>OutboxMessage</c> (the delivery record)
	/// 3. Then deserializes nested <c>message.MessageBody</c> as the event type
	/// 4. And <c>message.MessageMetadata</c> as <c>MessageMetadata</c>
	/// So the payload must be a serialized <c>DeliveryOutboxMessage</c> with nested JSON strings.
	/// </remarks>
	private static byte[] CreateNestedOutboxMessagePayload<TEvent>(
		string messageId,
		string messageType,
		TEvent integrationEvent)
		where TEvent : IDispatchMessage
	{
		var eventJson = JsonSerializer.Serialize(integrationEvent, s_testJsonOptions);
		var metadata = new DeliveryMessageMetadata(
			MessageId: messageId,
			CorrelationId: "correlation-envelope",
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0");
		var metadataJson = JsonSerializer.Serialize(metadata, s_testJsonOptions);

		var deliveryRecord = new DeliveryOutboxMessage(
			messageId,
			messageType,
			messageMetadata: metadataJson,
			messageBody: eventJson,
			createdAt: DateTimeOffset.UtcNow);

		var deliveryRecordJson = JsonSerializer.Serialize(deliveryRecord, s_testJsonOptions);
		return System.Text.Encoding.UTF8.GetBytes(deliveryRecordJson);
	}

	private static StubEnvelopeDeserializer CreateEnvelopeDeserializer(
		Guid envelopeMessageId,
		string messageType,
		byte[] payload)
	{
		return new StubEnvelopeDeserializer
		{
			OutboxEnvelopeFactory = _ => new EnvelopeData
			{
				MessageId = envelopeMessageId,
				MessageType = messageType,
				Payload = payload,
				Timestamp = DateTimeOffset.UtcNow,
				Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["CorrelationId"] = "correlation-envelope"
				}
			}
		};
	}

	private static DispatchJsonSerializer CreateEnvelopeAwareJsonSerializer(string envelopeMessageId, string messageType)
	{
		return new DispatchJsonSerializer();
	}

	private static IDispatcher CreateSuccessDispatcher()
	{
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));
		return dispatcher;
	}

	private static IDeadLetterQueue CreateDeadLetterQueue()
	{
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));
		return deadLetterQueue;
	}

	private static ITransportCircuitBreakerRegistry CreateCircuitOpenRegistry()
	{
		var circuitBreaker = A.Fake<ICircuitBreakerPolicy>();
		_ = A.CallTo(() => circuitBreaker.State).Returns(CircuitState.Open);

		var circuitRegistry = A.Fake<ITransportCircuitBreakerRegistry>();
		_ = A.CallTo(() => circuitRegistry.GetOrCreate(A<string>._)).Returns(circuitBreaker);
		return circuitRegistry;
	}

	private static IOutboxStore CreateParallelOutboxStore(OutboundMessage first, OutboundMessage second)
	{
		var outboxStore = A.Fake<IOutboxStore>();
		var fetchCount = 0;
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? [first, second]
					: [];
				return new ValueTask<IEnumerable<OutboundMessage>>(batch);
			});
		return outboxStore;
	}

	/// <summary>
	/// Creates an OutboundMessage whose Payload is a serialized DeliveryOutboxMessage envelope
	/// containing the event body and metadata as nested JSON strings. This allows the real
	/// (non-faked) DispatchJsonSerializer to deserialize the payload correctly.
	/// </summary>
	private static OutboundMessage CreateOutboundMessageWithEnvelope<TEvent>(
		string messageId,
		string messageType,
		TEvent integrationEvent)
		where TEvent : IDispatchMessage
	{
		var eventJson = JsonSerializer.Serialize(integrationEvent, s_testJsonOptions);
		var metadata = new DeliveryMessageMetadata(
			MessageId: messageId,
			CorrelationId: "correlation-1",
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0");
		var metadataJson = JsonSerializer.Serialize(metadata, s_testJsonOptions);

		var envelope = new DeliveryOutboxMessage(
			messageId,
			messageType,
			messageMetadata: metadataJson,
			messageBody: eventJson,
			createdAt: DateTimeOffset.UtcNow);

		var envelopeJson = JsonSerializer.Serialize(envelope, s_testJsonOptions);

		return new OutboundMessage
		{
			Id = messageId,
			MessageType = messageType,
			Payload = System.Text.Encoding.UTF8.GetBytes(envelopeJson),
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = 0
		};
	}

	private static OutboundMessage CreateOutboundMessage(
		string id,
		string messageType,
		int retryCount = 0,
		string payloadHint = "{}")
	{
		return new OutboundMessage
		{
			Id = id,
			MessageType = messageType,
			Payload = System.Text.Encoding.UTF8.GetBytes(payloadHint),
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = retryCount
		};
	}

	private sealed class DispatchScenario : IAsyncDisposable
	{
		public DispatchScenario(
			OutboxProcessor processor,
			IOutboxStore outboxStore,
			IDeadLetterQueue deadLetterQueue,
			ServiceProvider serviceProvider,
			IDispatcher dispatcher)
		{
			Processor = processor;
			OutboxStore = outboxStore;
			DeadLetterQueue = deadLetterQueue;
			Dispatcher = dispatcher;
			_serviceProvider = serviceProvider;
		}

		public OutboxProcessor Processor { get; }

		public IOutboxStore OutboxStore { get; }

		public IDeadLetterQueue DeadLetterQueue { get; }

		public IDispatcher Dispatcher { get; }

		private readonly ServiceProvider _serviceProvider;

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync();
			await _serviceProvider.DisposeAsync();
		}
	}

	private sealed record EnvelopeDispatchScenario(
		DispatchScenario Scenario,
		StubEnvelopeDeserializer EnvelopeDeserializer,
		string EnvelopeMessageId);

	private sealed record TestOutboxIntegrationEvent(string Value) : IIntegrationEvent;

	private sealed record TestOutboxCommand(string Value) : IDispatchAction;

	private sealed record TestParallelSuccessIntegrationEvent(string Value) : IIntegrationEvent;

	private sealed record TestParallelFailureIntegrationEvent(string Value) : IIntegrationEvent;

	private sealed class StubEnvelopeDeserializer : IBinaryEnvelopeDeserializer
	{
		public int DeserializeCalls { get; private set; }

		public Func<ReadOnlySpan<byte>, EnvelopeData>? OutboxEnvelopeFactory { get; init; }

		public EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data)
		{
			DeserializeCalls++;
			return null;
		}

		public EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data)
		{
			DeserializeCalls++;
			return OutboxEnvelopeFactory?.Invoke(data);
		}
	}

	#endregion
}
