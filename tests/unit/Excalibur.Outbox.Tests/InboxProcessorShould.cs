// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization.MemoryPack;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Reflection;

// Use alias to avoid namespace collision with Excalibur.Outbox.InboxOptions
using DeliveryMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;
using DispatchMessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
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
	private static readonly MethodInfo PerformBatchDatabaseOperationsAsyncMethod = typeof(InboxProcessor)
		.GetMethod("PerformBatchDatabaseOperationsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private PerformBatchDatabaseOperationsAsync method.");

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

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksEntryProcessed_WhenDispatchSucceeds()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "inbox-success",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Success());

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync("inbox-success", typeof(TestInboxDispatchMessage).Name, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksEntryFailedForRetry_WhenDispatchFailsBeforeMaxAttempts()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "inbox-retry",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Failed("handler failed"));

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(0);
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				"inbox-retry",
				typeof(TestInboxDispatchMessage).Name,
				ErrorConstants.ProcessingFailedRetryAttempt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesEntryToDeadLetterQueue_WhenDispatchFailsAtMaxAttempts()
	{
		// Arrange
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "inbox-dlq",
			maxAttempts: 1,
			dispatchResult: DispatchMessageResult.Failed("terminal failure"));

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(0);
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>.That.Matches(m => m.ExternalMessageId == "inbox-dlq"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				"inbox-dlq",
				typeof(TestInboxDispatchMessage).Name,
				A<string>.That.Contains("Moved to DLQ: Max retries exceeded"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_UsesEnvelopePayload_WhenInternalSerializerIsConfigured()
	{
		// Arrange
		await using var scenario = await CreateEnvelopeDispatchScenarioAsync("inbox-envelope");

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		var internalSerializer = scenario.InternalSerializer ?? throw new InvalidOperationException("Internal serializer was not configured.");
		internalSerializer.SpanDeserializeCalls.ShouldBe(1);
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync(
				scenario.EnvelopeMessageId.ToString(),
				typeof(TestInboxDispatchMessage).Name,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_MarksProcessedRetryAndDeadLetterEntries()
	{
		// Arrange
		var inboxStore = A.Fake<IInboxStore>();
		await using var processor = CreateProcessor(inboxStore: inboxStore);

		var successful = new List<(string MessageId, string HandlerType)> { ("inbox-success", "HandlerA") };
		var failedToRetry = new List<(string MessageId, string HandlerType)> { ("inbox-retry", "HandlerB") };
		var failedToDeadLetter = new List<(string MessageId, string HandlerType)> { ("inbox-dlq", "HandlerC") };

		// Act
		var task = (Task?)PerformBatchDatabaseOperationsAsyncMethod.Invoke(
			processor,
			[successful, failedToRetry, failedToDeadLetter, CancellationToken.None]);
		if (task is null)
		{
			throw new InvalidOperationException("Expected task result from PerformBatchDatabaseOperationsAsync.");
		}

		await task;

		// Assert
		A.CallTo(() => inboxStore.MarkProcessedAsync("inbox-success", "HandlerA", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => inboxStore.MarkFailedAsync(
				"inbox-retry",
				"HandlerB",
				ErrorConstants.ProcessingFailedRetryAttempt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => inboxStore.MarkFailedAsync(
				"inbox-dlq",
				"HandlerC",
				ErrorConstants.MessageMovedToDeadLetterQueueMaxRetriesExceeded,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_DoesNotWrite_WhenAllListsAreEmpty()
	{
		// Arrange
		var inboxStore = A.Fake<IInboxStore>();
		await using var processor = CreateProcessor(inboxStore: inboxStore);

		// Act
		var task = (Task?)PerformBatchDatabaseOperationsAsyncMethod.Invoke(
			processor,
			[new List<(string MessageId, string HandlerType)>(),
				new List<(string MessageId, string HandlerType)>(),
				new List<(string MessageId, string HandlerType)>(),
				CancellationToken.None]);
		if (task is null)
		{
			throw new InvalidOperationException("Expected task result from PerformBatchDatabaseOperationsAsync.");
		}

		await task;

		// Assert
		A.CallTo(() => inboxStore.MarkProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => inboxStore.MarkFailedAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesEntryToDeadLetterQueue_WhenCircuitBreakerIsOpenBeforeExecution()
	{
		// Arrange
		await using var scenario = await CreateOpenCircuitDispatchScenarioAsync("inbox-open-circuit");

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>.That.Matches(message => message.ExternalMessageId == "inbox-open-circuit"),
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				"inbox-open-circuit",
				typeof(TestInboxDispatchMessage).Name,
				A<string>.That.Contains("Moved to DLQ: Circuit breaker open"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksEntryFailedForRetry_WhenMessageTypeCannotBeResolved()
	{
		// Arrange
		var entry = CreateInboxEntry("inbox-missing-type");
		entry.MessageType = "MissingDispatchMessageType";
		var inboxStore = CreateInboxStore(entry);
		var serializer = A.Fake<IJsonSerializer>();

		await using var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: serializer);
		processor.Init("dispatcher-1");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(0);
		A.CallTo(() => inboxStore.MarkFailedAsync(
				"inbox-missing-type",
				"MissingDispatchMessageType",
				ErrorConstants.ProcessingFailedRetryAttempt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesEntryToDeadLetterQueue_WhenMetadataDeserializationFails()
	{
		// Arrange
		await using var scenario = await CreateBadMetadataDispatchScenarioAsync("inbox-bad-metadata");

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(0);
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>.That.Matches(message => message.ExternalMessageId == "inbox-bad-metadata"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				"inbox-bad-metadata",
				typeof(TestInboxDispatchMessage).Name,
				A<string>.That.Contains("Moved to DLQ: Max retries exceeded"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
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
		IBackoffCalculator? backoffCalculator = null,
		IInternalSerializer? internalSerializer = null)
	{
		return new InboxProcessor(
			options ?? CreateValidOptions(),
			inboxStore ?? A.Fake<IInboxStore>(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			serializer ?? A.Fake<IJsonSerializer>(),
			logger ?? A.Fake<ILogger<InboxProcessor>>(),
			telemetryClient: null,
			internalSerializer: internalSerializer,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator);
	}

	private static IOptions<DeliveryInboxOptions> CreateSingleMessageOptions(int maxAttempts)
	{
		return Options.Create(new DeliveryInboxOptions
		{
			QueueCapacity = 1,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = maxAttempts,
			ParallelProcessingDegree = 2,
			EnableBatchDatabaseOperations = false
		});
	}

	private static ServiceProvider CreateServiceProvider(IDispatcher dispatcher)
	{
		var services = new ServiceCollection();
		_ = services.AddScoped(_ => dispatcher);
		return services.BuildServiceProvider();
	}

	private static Task<DispatchScenario> CreateDispatchScenarioAsync(
		string messageId,
		int maxAttempts,
		IMessageResult dispatchResult)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntry(messageId);
		var inboxStore = CreateInboxStore(entry);
		var serializer = CreateSerializerForDispatch(new TestInboxDispatchMessage(messageId));
		var dispatcher = CreateDispatcher(dispatchResult);
		var deadLetterQueue = CreateDeadLetterQueue();

		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-1");

		return Task.FromResult(new DispatchScenario(
			processor,
			inboxStore,
			deadLetterQueue,
			serviceProvider,
			dispatcher));
	}

	private static Task<DispatchScenario> CreateEnvelopeDispatchScenarioAsync(string messageId)
	{
		var envelopeMessageId = Guid.NewGuid();
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntry(messageId);
		entry.Payload = [1, 42, 99];
		var inboxStore = CreateInboxStore(entry);
		var serializer = CreateSerializerForDispatch(new TestInboxDispatchMessage(messageId));
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var deadLetterQueue = CreateDeadLetterQueue();
		var internalSerializer = new StubInternalSerializer
		{
			InboxEnvelopeFactory = _ => new InboxEnvelope
			{
				MessageId = envelopeMessageId,
				MessageType = typeof(TestInboxDispatchMessage).Name,
				Payload = [7, 8, 9],
				ReceivedAt = DateTimeOffset.UtcNow,
				Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["CorrelationId"] = "corr-envelope"
				}
			}
		};

		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			internalSerializer: internalSerializer);
		processor.Init("dispatcher-1");

		return Task.FromResult(new DispatchScenario(
			processor,
			inboxStore,
			deadLetterQueue,
			serviceProvider,
			dispatcher,
			internalSerializer,
			envelopeMessageId));
	}

	private static Task<DispatchScenario> CreateOpenCircuitDispatchScenarioAsync(string messageId)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntry(messageId);
		var inboxStore = CreateInboxStore(entry);
		var serializer = CreateSerializerForDispatch(new TestInboxDispatchMessage(messageId));
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreaker = A.Fake<ICircuitBreakerPolicy>();
		A.CallTo(() => circuitBreaker.State).Returns(CircuitState.Open);
		var registry = A.Fake<ITransportCircuitBreakerRegistry>();
		A.CallTo(() => registry.GetOrCreate(A<string>._)).Returns(circuitBreaker);

		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: registry);
		processor.Init("dispatcher-1");

		return Task.FromResult(new DispatchScenario(
			processor,
			inboxStore,
			deadLetterQueue,
			serviceProvider,
			dispatcher));
	}

	private static Task<DispatchScenario> CreateBadMetadataDispatchScenarioAsync(string messageId)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntry(messageId);
		var inboxStore = CreateInboxStore(entry);
		var serializer = A.Fake<IJsonSerializer>();
		_ = A.CallTo(() => serializer.DeserializeAsync(
				A<string>._,
				A<Type>.That.Matches(type => type == typeof(TestInboxDispatchMessage))))
			.Returns(Task.FromResult<object?>(new TestInboxDispatchMessage(messageId)));
		_ = A.CallTo(() => serializer.DeserializeAsync(
				A<string>._,
				A<Type>.That.Matches(type => type == typeof(DeliveryMessageMetadata))))
			.Returns(Task.FromResult<object?>(new object()));
		var deadLetterQueue = CreateDeadLetterQueue();
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var serviceProvider = CreateServiceProvider(dispatcher);

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-1");

		return Task.FromResult(new DispatchScenario(
			processor,
			inboxStore,
			deadLetterQueue,
			serviceProvider,
			dispatcher));
	}

	private static InboxEntry CreateInboxEntry(string messageId)
	{
		return new InboxEntry
		{
			MessageId = messageId,
			HandlerType = "TestHandler",
			MessageType = typeof(TestInboxDispatchMessage).Name,
			Payload = [1, 2, 3],
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["CorrelationId"] = "corr-1"
			},
			RetryCount = 0,
			ReceivedAt = DateTimeOffset.UtcNow
		};
	}

	private static IInboxStore CreateInboxStore(InboxEntry entry)
	{
		var inboxStore = A.Fake<IInboxStore>();
		_ = A.CallTo(() => inboxStore.GetFailedEntriesAsync(
				A<int>._,
				A<DateTimeOffset?>._,
				A<int>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>([entry]));
		return inboxStore;
	}

	private static IJsonSerializer CreateSerializerForDispatch(TestInboxDispatchMessage dispatchMessage)
	{
		var serializer = A.Fake<IJsonSerializer>();
		ConfigureSerializerForInboxDispatch(serializer, dispatchMessage);
		return serializer;
	}

	private static IDispatcher CreateDispatcher(IMessageResult dispatchResult)
	{
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(dispatchResult));
		return dispatcher;
	}

	private static IDeadLetterQueue CreateDeadLetterQueue()
	{
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));
		return deadLetterQueue;
	}

	private static void ConfigureSerializerForInboxDispatch(IJsonSerializer serializer, TestInboxDispatchMessage dispatchMessage)
	{
		var metadata = new DeliveryMessageMetadata(
			MessageId: dispatchMessage.Id,
			CorrelationId: "corr-1",
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0");

		_ = A.CallTo(() => serializer.DeserializeAsync(
				A<string>._,
				A<Type>.That.Matches(t => t == typeof(TestInboxDispatchMessage))))
			.Returns(Task.FromResult<object?>(dispatchMessage));

		_ = A.CallTo(() => serializer.DeserializeAsync(
				A<string>._,
				A<Type>.That.Matches(t => t == typeof(DeliveryMessageMetadata))))
			.Returns(Task.FromResult<object?>(metadata));
	}

	private sealed class DispatchScenario : IAsyncDisposable
	{
		public DispatchScenario(
			InboxProcessor processor,
			IInboxStore inboxStore,
			IDeadLetterQueue deadLetterQueue,
			ServiceProvider serviceProvider,
			IDispatcher dispatcher,
			StubInternalSerializer? internalSerializer = null,
			Guid envelopeMessageId = default)
		{
			Processor = processor;
			InboxStore = inboxStore;
			DeadLetterQueue = deadLetterQueue;
			Dispatcher = dispatcher;
			InternalSerializer = internalSerializer;
			EnvelopeMessageId = envelopeMessageId;
			_serviceProvider = serviceProvider;
		}

		public InboxProcessor Processor { get; }

		public IInboxStore InboxStore { get; }

		public IDeadLetterQueue DeadLetterQueue { get; }

		public IDispatcher Dispatcher { get; }

		public StubInternalSerializer? InternalSerializer { get; }

		public Guid EnvelopeMessageId { get; }

		private readonly ServiceProvider _serviceProvider;

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync();
			await _serviceProvider.DisposeAsync();
		}
	}

	private sealed record TestInboxDispatchMessage(string Id) : IDispatchEvent;

	private sealed class StubInternalSerializer : IInternalSerializer
	{
		public int SpanDeserializeCalls { get; private set; }

		public Func<ReadOnlySpan<byte>, InboxEnvelope>? InboxEnvelopeFactory { get; init; }

		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
		{
			ArgumentNullException.ThrowIfNull(bufferWriter);
		}

		public byte[] Serialize<T>(T value) => [];

		public T Deserialize<T>(ReadOnlySequence<byte> buffer)
		{
			var bytes = buffer.ToArray();
			return Deserialize<T>(bytes);
		}

		public T Deserialize<T>(ReadOnlySpan<byte> buffer)
		{
			SpanDeserializeCalls++;
			if (typeof(T) == typeof(InboxEnvelope) && InboxEnvelopeFactory is not null)
			{
				var envelope = InboxEnvelopeFactory(buffer);
				return (T)(object)envelope;
			}

			return default!;
		}
	}

	#endregion
}
