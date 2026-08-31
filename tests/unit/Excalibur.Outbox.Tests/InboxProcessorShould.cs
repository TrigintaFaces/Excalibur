using Microsoft.Extensions.Logging.Abstractions;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization.MemoryPack;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Reflection;
using System.Text.Json;

// Use alias to avoid namespace collision with Excalibur.Outbox.InboxOptions
using DeliveryMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;
using DispatchMessageResult = Excalibur.Dispatch.MessageResult;
using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="InboxProcessor"/>.
/// Tests the high-performance inbox processor implementation including batch processing,
/// producer-consumer pattern, circuit breaker integration, and dead letter queue routing.
/// </summary>
/// <remarks>
/// The drain carries InboxEntry.Payload through to IInboxMessage.MessageBody as raw bytes and reads it
/// back with the serializer's UTF-8 overload, so an entry holding UTF-8 JSON round-trips and dispatches.
/// These arms use the real (sealed) DispatchJsonSerializer, so an entry seeded with payload bytes that are
/// not UTF-8 JSON still exercises the genuine deserialization-failure path (retry or dead-letter routing).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
#pragma warning disable CA1506 // Excessive class coupling -- test requires many fakes for InboxProcessor constructor
public sealed class InboxProcessorShould : UnitTestBase
#pragma warning restore CA1506
{
	/// <summary>
	/// Shared JSON options matching the DispatchJsonSerializer's camelCase configuration
	/// for creating test payloads that the real serializer can deserialize.
	/// </summary>
	// The handler type every fixture entry is stored under. Distinct from the message type by
	// construction — the store's composite key is (MessageId, HandlerType), and a mark addressed with
	// the message type instead matches no row.
	private const string FixtureHandlerType = "TestHandler";

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
		var inboxStore = A.Fake<IInboxStore>();
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<InboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<InboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<InboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
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
			Capacity =
			{
				QueueCapacity = 10,
				ProducerBatchSize = 100, // Larger than queue capacity - invalid
				ConsumerBatchSize = 100,
				PerRunTotal = 100,
				ParallelProcessingDegree = 1,
			},
			MaxAttempts = 3,
		});

		var inboxStore = A.Fake<IInboxStore>();
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<InboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<InboxProcessor>.Instance;

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
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<InboxProcessor>.Instance;

		// Act - Create processor without optional dependencies
		await using var processor = new InboxProcessor(
			options,
			inboxStore,
			serviceProvider,
			serializer,
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
		// The entry's payload is real UTF-8 JSON, which is what the default serializer writes. The drain must
		// carry those bytes through unchanged and deserialize them, so the message reaches the dispatcher and
		// the entry is finalized as processed.
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "inbox-success",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Success());

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert - the drain must actually reach the handler. Without this call assertion the arm would pass
		// on any path that finalizes the entry without dispatching it, which is how a body that never
		// deserialized went unnoticed.
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		processed.ShouldBe(1);
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync(
				"inbox-success",
				FixtureHandlerType,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
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
				FixtureHandlerType,
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
				FixtureHandlerType,
				A<string>.That.Contains("Moved to DLQ: Max retries exceeded"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.InboxStore.MarkProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_FailsDiagnosably_WhenTheStoredPayloadIsBinaryRatherThanJson()
	{
		// Arrange
		// A payload written by a configured IPayloadSerializer is magic-byte-prefixed binary. The JSON drain
		// structurally cannot read it, and it must say so: a bare parse error names neither the payload shape
		// nor the cause, leaving the entry to burn its retry budget with nothing to diagnose from.
		await using var scenario = await CreateBinaryPayloadDispatchScenarioAsync(
			messageId: "inbox-binary-payload",
			payload: [0x02, 0xFF, 0x00, 0x10]);

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(0);
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>.That.Matches(m => m.ExternalMessageId == "inbox-binary-payload"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>.That.Matches(ex =>
					ex != null
					&& ex.ToString().Contains("not UTF-8 JSON", StringComparison.Ordinal)
					&& ex.ToString().Contains("0x02", StringComparison.Ordinal)),
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_UsesEnvelopePayload_WhenInternalSerializerIsConfigured()
	{
		// Arrange
		await using var scenario = await CreateEnvelopeDispatchScenarioAsync("inbox-envelope");

		// Act
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert - Verify the envelope path was taken (internal serializer was invoked)
		var internalSerializer = scenario.InternalSerializer ?? throw new InvalidOperationException("Internal serializer was not configured.");
		internalSerializer.DeserializeCalls.ShouldBe(1);

		// The envelope in this scenario carries payload bytes [7, 8, 9], which are not UTF-8 JSON, so the real
		// DispatchJsonSerializer cannot read them as the message type. With maxAttempts=3 and retryCount=0,
		// the deserialization failure causes a retry. The assertion above is what pins the envelope path.
		processed.ShouldBe(0);
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				// The mark is keyed by the ENTRY, so the envelope's own message id is not the key here.
				"inbox-envelope",
				FixtureHandlerType,
				ErrorConstants.ProcessingFailedRetryAttempt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}



	[Fact]
	public async Task DispatchPendingMessagesAsync_LeavesEntryForRetry_WhenCircuitBreakerIsOpenBeforeExecution()
	{
		// bd-v9jq1a AC-1 (Inbox CB-open pre-check, non-vacuity): a transient circuit-breaker-OPEN must leave
		// the entry FOR RETRY via the no-increment IInboxStoreAdmin.MarkFailedAsync(retryCount) overload
		// (re-admitted by GetAllTenantsFailedEntriesAsync once the breaker recovers), NEVER dead-letter it. RED on
		// pre-fix (pre-fix routed CB-open straight to the DLQ → bulk loss). Flipped broken-behavior cert (NFR-6).
		// Arrange
		await using var scenario = await CreateOpenCircuitDispatchScenarioAsync("inbox-open-circuit");

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — NOT dead-lettered on CB-open
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._,
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		// Assert — NOT the dead-letter mark (the 4-arg auto-increment MarkFailedAsync overload)
		A.CallTo(() => scenario.InboxStore.MarkFailedAsync(
				A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Assert — left for retry via the no-increment IInboxStoreAdmin overload (retryCount preserved)
		A.CallTo(() => ((IInboxStoreAdmin)scenario.InboxStore).MarkFailedAsync(
				"inbox-open-circuit",
				FixtureHandlerType,
				A<string>._,
				A<int>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_DoesNotTouchDedupStore_WhenCircuitBreakerOpen()
	{
		// bd-v9jq1a AC-6 / FR-5 (CEO condition 2, CRITICAL): on CB-open the re-route MUST touch ONLY the
		// failed/retry state — NEVER mark the dedup store. If CB-open marked the message deduplicated, the
		// eventual retry would be seen as a FALSE DUPLICATE → silently dropped (a NEW data-loss path, the
		// exact class this sprint fixes). RED on pre-fix (pre-fix dead-letters; this lock guards the fix's
		// invariant — the CB-open path never writes dedup).
		// Arrange — forced CB-open with a WIRED dedup store.
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntryWithSerializedPayload("inbox-dedup-open", new TestInboxDispatchMessage("inbox-dedup-open"));
		var inboxStore = CreateInboxStore(entry);
		var serializer = new DispatchJsonSerializer();
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreaker = A.Fake<ICircuitBreakerPolicy>();
		A.CallTo(() => circuitBreaker.State).Returns(CircuitState.Open);
		var registry = A.Fake<ITransportCircuitBreakerRegistry>();
		A.CallTo(() => registry.GetOrCreate(A<string>._)).Returns(circuitBreaker);
		var dedupStore = A.Fake<IDeduplicationStore>();
		var serviceProvider = CreateServiceProvider(dispatcher);

		await using var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: registry,
			deduplicationStore: dedupStore);
		processor.Init("dispatcher-dedup-open");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — CB-open must NOT write the dedup store (else the retry is a false duplicate → silent drop)
		A.CallTo(() => dedupStore.AddAsync(A<string>._, A<TimeSpan?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		// And it is left for retry via the no-increment overload, never dead-lettered.
		A.CallTo(() => ((IInboxStoreAdmin)inboxStore).MarkFailedAsync(
				"inbox-dedup-open", A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._, DeadLetterReason.CircuitBreakerOpen, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_LeavesEntryForRetry_WhenCircuitOpensDuringExecution()
	{
		// bd-v9jq1a AC-2 (mid-execute catch direction, InboxProcessor:686, non-vacuity): a
		// CircuitBreakerOpenException thrown DURING dispatch (breaker passed the pre-check, then opened
		// mid-flight) is a transient short-circuit → leave the entry FOR RETRY via the no-increment overload,
		// never dead-letter. RED on pre-fix. Mirrors the Outbox mid-execute lock.
		// Arrange — State==Closed passes the pre-check; ExecuteAsync throws to hit the catch.
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntryWithSerializedPayload("inbox-open-during", new TestInboxDispatchMessage("inbox-open-during"));
		var inboxStore = CreateInboxStore(entry);
		var serializer = new DispatchJsonSerializer();
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreaker = A.Fake<ICircuitBreakerPolicy>();
		A.CallTo(() => circuitBreaker.State).Returns(CircuitState.Closed);
		A.CallTo(() => circuitBreaker.ExecuteAsync<bool>(A<Func<CancellationToken, Task<bool>>>._, A<CancellationToken>._))
			.ThrowsAsync(new CircuitBreakerOpenException("transport circuit opened mid-dispatch"));
		var registry = A.Fake<ITransportCircuitBreakerRegistry>();
		A.CallTo(() => registry.GetOrCreate(A<string>._)).Returns(circuitBreaker);
		var serviceProvider = CreateServiceProvider(dispatcher);

		await using var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: registry);
		processor.Init("dispatcher-open-during");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — NOT dead-lettered; left for retry via the no-increment overload.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._, DeadLetterReason.CircuitBreakerOpen, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IInboxStoreAdmin)inboxStore).MarkFailedAsync(
				"inbox-open-during", A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksEntryFailedForRetry_WhenMessageTypeCannotBeResolved()
	{
		// Arrange
		var entry = CreateInboxEntry("inbox-missing-type");
		entry.MessageType = "MissingDispatchMessageType";
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();

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
				// Keyed by the entry, so the unresolvable MESSAGE type is not what the mark is addressed with.
				FixtureHandlerType,
				ErrorConstants.ProcessingFailedRetryAttempt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesEntryToDeadLetterQueue_WhenMetadataDeserializationFails()
	{
		// Arrange
		// The entry's payload bytes are [1, 2, 3], which are not UTF-8 JSON, so the body deserialization fails
		// before metadata deserialization is even attempted. With maxAttempts=1, the deserialization error
		// routes the message to the dead letter queue via MaxRetriesExceeded.
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
				FixtureHandlerType,
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
			Capacity =
			{
				QueueCapacity = 2000,
				ProducerBatchSize = 500,
				ConsumerBatchSize = 200,
				PerRunTotal = 5000,
				ParallelProcessingDegree = 8,
			},
			MaxAttempts = 3,
			BatchTuning =
			{
				EnableDynamicBatchSizing = true,
				MinBatchSize = 10,
				MaxBatchSize = 1000,
			},
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
			Capacity =
			{
				QueueCapacity = 500,
				ProducerBatchSize = 100,
				ConsumerBatchSize = 50,
				PerRunTotal = 1000,
				ParallelProcessingDegree = 4,
			},
			MaxAttempts = 5,
		});
	}

	// ── hlt4g4: a drained/re-admitted failed entry must be reprocessed under ITS OWN tenant scope, not
	//    tenant-blind. Author≠implementer RED-first lock for the (b-public) drain fix: InboxProcessor now
	//    populates IInboxMessage.TenantId from the entry and wraps the per-entry dispatch+mark unit in
	//    TenantContextHolder.BeginScope(message.TenantId) (mirrors OutboxProcessor). Observable: a capturing
	//    IDispatcher reads the ambient ITenantContext during DispatchAsync — the scope BeginScope establishes.
	//    RED against committed HEAD (no BeginScope in the drain → ambient tenant is null/tenant-blind);
	//    GREEN on the fix. Both arms per testing-patterns §3: SAFETY (tenant-B not reprocessed tenant-blind)
	//    + LIVENESS (reprocessing still happens, and under the entry's OWN tenant — not a hardcoded value).

	[Fact]
	public async Task ReprocessADrainedEntryUnderItsOwnTenantScope_NotTenantBlind()
	{
		// SAFETY. A tenant-B failed entry, when drained, must be processed under tenant B's ambient scope.
		var (capturedTenant, observedUnderScope) = await DrainCapturingTenantAsync("tenant-B").ConfigureAwait(false);

		observedUnderScope.ShouldBe(
			1, "the drained entry must actually reach the per-entry process unit — else the scope assertion is vacuous");
		capturedTenant.ShouldBe(
			"tenant-B",
			"EXPECTED RED until the drain wraps reprocessing in BeginScope(entry.TenantId) (tracked: hlt4g4). A "
			+ "tenant-B failed entry must be processed (dedup-checked, dispatched, marked) under tenant B's ambient "
			+ "scope, not tenant-blind (null) — a tenant-blind re-admission acts in the wrong (or no) tenant's context");
	}

	[Fact]
	public async Task StillReprocessAnEntryUnderTheEntrysOwnTenant_IntraTenantDrainWorks()
	{
		// LIVENESS. Reprocessing still happens, and the scope is the entry's OWN tenant (tenant-A here), not a
		// hardcoded value — so the fix cannot pass the safety arm by always scoping to one tenant.
		var (capturedTenant, observedUnderScope) = await DrainCapturingTenantAsync("tenant-A").ConfigureAwait(false);

		observedUnderScope.ShouldBe(1, "tenant A's own failed entry is still drained and reaches the process unit");
		capturedTenant.ShouldBe(
			"tenant-A",
			"tenant A's entry is processed under tenant A — the ambient scope is the entry's own tenant, "
			+ "not a hardcoded value");
	}

	// Drains a single failed entry stamped with <paramref name="entryTenantId"/> through the real
	// InboxProcessor, capturing the ambient ITenantContext observed inside DispatchAsync. Returns the
	// captured tenant and the number of dispatches (reprocessing occurrences).
	private static async Task<(string? CapturedTenant, int ObservedUnderScope)> DrainCapturingTenantAsync(string? entryTenantId)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = CreateInboxEntryWithSerializedPayload("inbox-tenant", new TestInboxDispatchMessage("inbox-tenant"));
		entry.TenantId = entryTenantId;
		var inboxStore = CreateInboxStore(entry);

		var services = new ServiceCollection();
		_ = services.AddTenantContext();
		_ = services.AddScoped(_ => A.Fake<IDispatcher>());
		var serviceProvider = services.BuildServiceProvider();
		await using var providerLifetime = serviceProvider.ConfigureAwait(false);

		// AsyncLocal-backed: reads the ambient tenant that BeginScope establishes in the drain's process unit.
		var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
		var captured = new List<string?>();

		// The deduplication check (IsDuplicateAsync → ContainsAsync) runs INSIDE the per-entry
		// BeginScope(message.TenantId) at the very start of the process-and-mark unit — before dispatch — so
		// it observes the tenant scope the fix establishes, independently of whether the later dispatch
		// succeeds (the test harness's base64 payload path fails deserialization, so dispatch never runs).
		var dedupStore = A.Fake<IDeduplicationStore>();
		_ = A.CallTo(() => dedupStore.ContainsAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				captured.Add(tenantContext.TenantId);
				return Task.FromResult(false); // not a duplicate → the entry is processed under this scope
			});

		await using var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			inboxStore: inboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: serviceProvider,
			deadLetterQueue: CreateDeadLetterQueue(),
			deduplicationStore: dedupStore);
		processor.Init("dispatcher-hlt4g4");

		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		return (captured.Count > 0 ? captured[^1] : null, captured.Count);
	}

	private static InboxProcessor CreateProcessor(
		IOptions<DeliveryInboxOptions>? options = null,
		IInboxStore? inboxStore = null,
		DispatchJsonSerializer? serializer = null,
		IServiceProvider? serviceProvider = null,
		ILogger<InboxProcessor>? logger = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null,
		IBackoffCalculator? backoffCalculator = null,
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null,
		IDeduplicationStore? deduplicationStore = null)
	{
		return new InboxProcessor(
			options ?? CreateValidOptions(),
			inboxStore ?? A.Fake<IInboxStore>(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			serializer ?? new DispatchJsonSerializer(),
			logger ?? NullLogger<InboxProcessor>.Instance,

			envelopeDeserializer: envelopeDeserializer,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry,
			backoffCalculator: backoffCalculator,
			deduplicationStore: deduplicationStore);
	}

	private static IOptions<DeliveryInboxOptions> CreateSingleMessageOptions(int maxAttempts)
	{
		return Options.Create(new DeliveryInboxOptions
		{
			Capacity =
			{
				QueueCapacity = 1,
				ProducerBatchSize = 1,
				ConsumerBatchSize = 1,
				PerRunTotal = 1,
				ParallelProcessingDegree = 2,
			},
			MaxAttempts = maxAttempts,
			BatchTuning =
			{
				EnableBatchDatabaseOperations = false,
			},
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
		var entry = CreateInboxEntryWithSerializedPayload(messageId, new TestInboxDispatchMessage(messageId));
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();
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
		var entry = CreateInboxEntryWithSerializedPayload(messageId, new TestInboxDispatchMessage(messageId));
		entry.Payload = [1, 42, 99];
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var deadLetterQueue = CreateDeadLetterQueue();
		var internalSerializer = new StubInternalSerializer
		{
			InboxEnvelopeFactory = _ => new EnvelopeData
			{
				MessageId = envelopeMessageId,
				MessageType = typeof(TestInboxDispatchMessage).Name,
				Payload = [7, 8, 9],
				Timestamp = DateTimeOffset.UtcNow,
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
			envelopeDeserializer: internalSerializer);
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
		var entry = CreateInboxEntryWithSerializedPayload(messageId, new TestInboxDispatchMessage(messageId));
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();
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

	private static Task<DispatchScenario> CreateBinaryPayloadDispatchScenarioAsync(string messageId, byte[] payload)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = FixtureHandlerType,
			MessageType = typeof(TestInboxDispatchMessage).Name,
			Payload = payload,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["CorrelationId"] = "corr-binary"
			},
			RetryCount = 0,
			ReceivedAt = DateTimeOffset.UtcNow
		};
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();
		var deadLetterQueue = CreateDeadLetterQueue();
		var dispatcher = CreateDispatcher(DispatchMessageResult.Success());
		var serviceProvider = CreateServiceProvider(dispatcher);

		// maxAttempts 1 so the single drain reaches the dead-letter branch, where the exception is observable.
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

	private static Task<DispatchScenario> CreateBadMetadataDispatchScenarioAsync(string messageId)
	{
		MessageTypeRegistry.RegisterType<TestInboxDispatchMessage>();
		// Create an entry with arbitrary payload bytes. They are carried through to MessageBody unchanged and
		// are not UTF-8 JSON, so with maxAttempts=1 the deserialization failure routes the message to the
		// dead letter queue.
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = FixtureHandlerType,
			MessageType = typeof(TestInboxDispatchMessage).Name,
			Payload = [1, 2, 3],
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				// String values that the CoreMessageJsonContext can serialize, while the payload [1, 2, 3] is
				// not UTF-8 JSON, causing deserialization failure downstream.
				["CorrelationId"] = "bad-correlation"
			},
			RetryCount = 0,
			ReceivedAt = DateTimeOffset.UtcNow
		};
		var inboxStore = CreateInboxStore(entry);
		// Use real DispatchJsonSerializer -- it is sealed and cannot be faked
		var serializer = new DispatchJsonSerializer();
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

	/// <summary>
	/// Creates an InboxEntry with a serialized JSON payload that the real DispatchJsonSerializer
	/// can deserialize. Used for dispatch scenarios that need the serializer to work correctly.
	/// </summary>
	private static InboxEntry CreateInboxEntryWithSerializedPayload<TMessage>(string messageId, TMessage dispatchMessage)
	{
		var messageJson = JsonSerializer.Serialize(dispatchMessage, s_testJsonOptions);
		var metadata = new DeliveryMessageMetadata(
			MessageId: messageId,
			CorrelationId: "corr-1",
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0");
		var metadataJson = JsonSerializer.Serialize(metadata, s_testJsonOptions);

		return new InboxEntry
		{
			MessageId = messageId,
			HandlerType = FixtureHandlerType,
			MessageType = typeof(TMessage).Name,
			Payload = System.Text.Encoding.UTF8.GetBytes(messageJson),
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["CorrelationId"] = "corr-1",
				["ContentType"] = "application/json",
				["SerializerVersion"] = "1.0.0",
				["MessageVersion"] = "1.0.0",
				["_serialized"] = metadataJson
			},
			RetryCount = 0,
			ReceivedAt = DateTimeOffset.UtcNow
		};
	}

	private static InboxEntry CreateInboxEntry(string messageId)
	{
		return new InboxEntry
		{
			MessageId = messageId,
			HandlerType = FixtureHandlerType,
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
		var inboxStore = A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>());
		_ = A.CallTo(() => ((IInboxStoreAdmin)inboxStore).GetAllTenantsFailedEntriesAsync(
				A<int>._,
				A<DateTimeOffset?>._,
				A<int>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>([entry]));
		return inboxStore;
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

	private sealed class StubInternalSerializer : IBinaryEnvelopeDeserializer
	{
		public int DeserializeCalls { get; private set; }

		public Func<ReadOnlySpan<byte>, EnvelopeData>? InboxEnvelopeFactory { get; init; }

		public EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data)
		{
			DeserializeCalls++;
			return InboxEnvelopeFactory?.Invoke(data);
		}

		public EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data)
		{
			DeserializeCalls++;
			return null;
		}
	}

	#endregion
}
