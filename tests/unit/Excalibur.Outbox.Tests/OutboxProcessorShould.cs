using Microsoft.Extensions.Logging.Abstractions;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA1506 // Test class has high coupling by design

using System.Buffers;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;
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

// Use alias to avoid namespace collision with Excalibur.Outbox.OutboxOptions
using DispatchMessageResult = Excalibur.Dispatch.MessageResult;
using DeliveryMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;
using DeliveryGuaranteeOptions = Excalibur.Dispatch.Options.Delivery.DeliveryGuaranteeOptions;
using DeliveryOutboxDeliveryGuarantee = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryGuarantee;
using DeliveryOutboxMessage = Excalibur.Outbox.OutboxMessage;
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

	[Fact]
	public async Task DrainAgainAfterEmptyRun()
	{
		await using var scenario = await CreateDispatchScenarioAsync("second-run", 3, DispatchMessageResult.Success());
		var calls = 0;
		var original = await scenario.OutboxStore.GetUnsentMessagesAsync(1, CancellationToken.None);
		A.CallTo(() => scenario.OutboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(scenario.OutboxStore, ++calls == 2 ? original : [], null)));
		(await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None)).ShouldBe(0);
		(await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None)).ShouldBe(1);
		AssertCapturedWrites(scenario.OutboxStore);
	}

	[Fact]
	public async Task RejectConcurrentDrainAndJoinBothDisposers()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		CancellationToken producerToken = default;
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(call => new ValueTask<IEnumerable<OutboundMessage>>(ReadAsync(call.GetArgument<CancellationToken>(1))));
		async Task<IEnumerable<OutboundMessage>> ReadAsync(CancellationToken token)
			{
				producerToken = token;
				entered.SetResult();
				await release.Task;
				token.ThrowIfCancellationRequested();
				return Enumerable.Empty<OutboundMessage>();
			}
		var processor = CreateProcessor(outboxStore: store);
		processor.Init("first");
		var run = processor.DispatchPendingMessagesAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		try
		{
			await Should.ThrowAsync<InvalidOperationException>(() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
			Should.Throw<InvalidOperationException>(() => processor.Init("second"));
			var firstDisposal = processor.DisposeAsync().AsTask();
			var secondDisposal = processor.DisposeAsync().AsTask();
			firstDisposal.IsCompleted.ShouldBeFalse();
			secondDisposal.IsCompleted.ShouldBeFalse();
			// DisposeAsync cancels the lifetime source with CancelAsync, which reaches this run's LINKED
			// token through a callback the runtime queues on the thread pool -- measured, the child is
			// still uncancelled 1968 of 2000 times when CancelAsync returns. Releasing the producer before
			// that lands lets it read an uncancelled token and complete normally, so wait for it.
			var cancelObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			using (producerToken.Register(cancelObserved.SetResult))
			{
				await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
			}
			release.SetResult();
			await Should.ThrowAsync<OperationCanceledException>(() => run);
			await Task.WhenAll(firstDisposal, secondDisposal).WaitAsync(TimeSpan.FromSeconds(10));
		}
		finally
		{
			release.TrySetResult();
			await processor.DisposeAsync();
		}
		AssertCapturedWrites(store);
	}

	[Fact]
	public async Task ReuseAfterProducerFailure()
	{
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var failure = new InvalidOperationException("claim failed");
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Throws(failure);
		await using var processor = CreateProcessor(outboxStore: store);
		processor.Init("retry");
		(await Should.ThrowAsync<InvalidOperationException>(() => processor.DispatchPendingMessagesAsync(CancellationToken.None)))
			.ShouldBeSameAs(failure);
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(store, [], null)));
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None)).ShouldBe(0);
		AssertCapturedWrites(store);
	}

	[Fact]
	public async Task PropagateDependencyCancellationWithoutTreatingItAsSuccessfulCompletion()
	{
		using var timeout = new CancellationTokenSource();
		await timeout.CancelAsync();
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Throws(new OperationCanceledException("dependency timeout", timeout.Token));
		await using var processor = CreateProcessor(outboxStore: store);
		processor.Init("timeout");
		await Should.ThrowAsync<OperationCanceledException>(() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
		AssertCapturedWrites(store);
	}

	[Fact]
	public async Task JoinRunEvenWhenDisposalCancellationCallbackThrows()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(call => new ValueTask<IEnumerable<OutboundMessage>>(ReadAsync(call.GetArgument<CancellationToken>(1))));
		async Task<IEnumerable<OutboundMessage>> ReadAsync(CancellationToken token)
		{
			using var registration = token.Register(() => { callbackEntered.SetResult(); throw new InvalidOperationException("callback failed"); });
			entered.SetResult();
			await release.Task;
			token.ThrowIfCancellationRequested();
			return [];
		}
		var processor = CreateProcessor(outboxStore: store);
		processor.Init("callback");
		var run = processor.DispatchPendingMessagesAsync(CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var disposal = processor.DisposeAsync().AsTask();
		try
		{
			await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			disposal.IsCompleted.ShouldBeFalse();
		}
		finally
		{
			release.TrySetResult();
		}
		await Should.ThrowAsync<OperationCanceledException>(() => run);
		await Should.ThrowAsync<AggregateException>(() => disposal);
		await Should.ThrowAsync<AggregateException>(() => processor.DisposeAsync().AsTask());
		AssertCapturedWrites(store);
	}

	[Fact]
	public async Task ConsumerFailureStopsProducerUnderBackpressure()
	{
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(store, [new OutboundMessage
			{
				Id = Guid.NewGuid().ToString(), MessageType = "test", Payload = [],
			}], null)));
		var registry = A.Fake<ITransportCircuitBreakerRegistry>();
		A.CallTo(() => registry.GetOrCreate(A<string>._)).Throws(new InvalidOperationException("consumer failed"));
		var options = CreateValidOptions();
		options.Value.QueueCapacity = 1;
		options.Value.ProducerBatchSize = 1;
		options.Value.ConsumerBatchSize = 1;
		options.Value.BatchProcessing.ParallelProcessingDegree = 1;
		await using var processor = CreateProcessor(options: options, outboxStore: store, circuitBreakerRegistry: registry);
		processor.Init("backpressure");
		await Should.ThrowAsync<InvalidOperationException>(() =>
			processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
		AssertCapturedWrites(store);
	}

	[Fact]
	public async Task StopReclaimingABatchContainingOnlyAlreadySeenIdentities()
	{
		var store = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(store, [new OutboundMessage
			{
				Id = "repeated", MessageType = "unregistered", Payload = [],
			}], null)));
		var options = CreateValidOptions();
		options.Value.PerRunTotal = 3;
		options.Value.ProducerBatchSize = 1;
		await using var processor = CreateProcessor(options: options, outboxStore: store);
		processor.Init("duplicate");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await processor.DispatchPendingMessagesAsync(deadline.Token);
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
		AssertCapturedWrites(store);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			null!,
			outboxStore,
			serializer,
			serviceProvider,
			logger,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance));
		AssertCapturedWrites(outboxStore);
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
			logger,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var serializer = new DispatchJsonSerializer();
		var logger = NullLogger<OutboxProcessor>.Instance;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			null!,
			logger,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance));
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			null!,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance));
		AssertCapturedWrites(outboxStore);
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

		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => new OutboxProcessor(
			customOptions,
			outboxStore,
			serializer,
			serviceProvider,
			logger,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance));
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task Constructor_CreatesProcessor_WithValidParameters()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var serializer = new DispatchJsonSerializer();
		var serviceProvider = A.Fake<IServiceProvider>();
		var logger = NullLogger<OutboxProcessor>.Instance;

		// Act
		await using var processor = new OutboxProcessor(
			options,
			outboxStore,
			serializer,
			serviceProvider,
			logger,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);

		// Assert
		_ = processor.ShouldNotBeNull();
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task Constructor_AcceptsExplicitDiscard_WhenOtherOptionalDependenciesNotProvided()
	{
		// Arrange
		var options = CreateValidOptions();
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
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
			deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance,
			backoffCalculator: null,
			deliveryGuaranteeOptions: null);

		// Assert - Should not throw and should use null object defaults internally
		_ = processor.ShouldNotBeNull();
		AssertCapturedWrites(outboxStore);
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
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, Enumerable.Empty<OutboundMessage>(), null)));

		await using var processor = CreateProcessor(outboxStore: outboxStore);
		processor.Init("dispatcher-1");

		// Act
		var result = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(0);
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, Enumerable.Empty<OutboundMessage>(), null)));

		var processor = CreateProcessor(outboxStore: outboxStore);
		processor.Init("dispatcher-1");
		await processor.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.DispatchPendingMessagesAsync(CancellationToken.None));
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_HandlesCancellation_ThrowsTaskCanceledException()
	{
		// Arrange
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, Enumerable.Empty<OutboundMessage>(), null)));

		using var cts = new CancellationTokenSource();
		await using var processor = CreateProcessor(outboxStore: outboxStore);
		processor.Init("dispatcher-1");

		// Cancel immediately
		await cts.CancelAsync();

		// Act & Assert - Cancellation throws TaskCanceledException (expected behavior)
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => processor.DispatchPendingMessagesAsync(cts.Token));
		AssertCapturedWrites(outboxStore);
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
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync("message-success", CapturedClaim(scenario.OutboxStore, "message-success"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
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

		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], null)));

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
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkSentAsync("command-message", CapturedClaim(outboxStore, "command-message"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_MarksMessageFailedForRetry_WhenDispatchFailsBeforeMaxAttempts()
	{
		// Arrange
		var backoff = A.Fake<IBackoffCalculator>();
		A.CallTo(() => backoff.CalculateDelay(1)).Returns(TimeSpan.FromSeconds(7));
		await using var scenario = await CreateDispatchScenarioAsync(
			messageId: "message-retry",
			maxAttempts: 3,
			dispatchResult: DispatchMessageResult.Failed("dispatch failed"), backoffCalculator: backoff);

		// Act
		var before = DateTimeOffset.UtcNow;
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		var after = DateTimeOffset.UtcNow;

		// Assert
		processed.ShouldBe(0, "a refused delivery is not a successful dispatch");
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedWithBackoffAsync("message-retry", "dispatch failed", 1,
			A<DateTimeOffset>.That.Matches(at => at >= before.AddSeconds(7) && at <= after.AddSeconds(7)), CapturedClaim(scenario.OutboxStore, "message-retry"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => backoff.CalculateDelay(1)).MustHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		AssertCapturedWrites(scenario.OutboxStore);
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
		processed.ShouldBe(0, "a refused delivery is not a successful dispatch");
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "message-dlq"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		// bd-stlcgg (S841): the terminal transition is MarkDeadLetteredAsync (terminal DeadLettered status), not
		// MarkFailedAsync — so a retry-exhausted message is never re-claimed.
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync("message-dlq", A<string>._, A<int>._, CapturedClaim(scenario.OutboxStore, "message-dlq"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
	}

	/// <summary>
	/// SAFETY (F1 fencing): a superseded leader's fenced mark-sent is refused with
	/// <see cref="StaleOutboxFencingTokenException"/>. That refusal must abort the message's drain cycle with
	/// NO further store write -- never the unfenced dead-letter or retry paths, which a superseded leader has
	/// no business performing. RED before the fix, which let the exception fall into the generic
	/// <c>catch (Exception ex)</c> and dead-letter the message on the stale leader's behalf.
	/// </summary>
	/// <summary>
	/// SAFETY (F1 fencing): losing leadership outright — an active gate that yields NO token — must refuse
	/// with no further store write, exactly as a superseded token does.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Requirement:</b> a tenure that cannot prove it is the leader does not write. <b>Predicate this arm
	/// tests:</b> after the guard refuses, the message is neither dead-lettered nor marked failed. The
	/// assertion is on the message's DISPOSITION, deliberately not on the exception type — the type is the
	/// mechanism that gets it there, and an arm pinned to the type would go green on a fix that renamed the
	/// exception while still routing the message into the failure path.
	/// </para>
	/// <para>
	/// <b>Why this was reachable at all.</b> The guard was already correct in refusing; it threw a general
	/// failure type, which the fence-refusal catch could not match and the generic handler therefore took. A
	/// leader that had lost leadership dead-lettered a message that may well have been delivered — the fence
	/// defeated through its own signal rather than through a missing check. Making the refusal a member of
	/// the fence-refusal family gives every call site the property at once, which is why no catch clause was
	/// added for the old type.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_DoesNotDeadLetter_WhenTheGateIsActiveButHasNoToken()
	{
		// Arrange
		const string MessageId = "message-gate-active-no-token";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);
		// Leadership is held through transport admission and lost when that admitted dispatch returns. That ordering is the whole
		// scenario: the same guard protects both steps, so a token that is absent from the start refuses at
		// the claim and the message is never dispatched at all -- which would make this arm pass without
		// ever reaching the path it exists to test.
		// The FENCED overload is the one a tenure holding a token actually calls; stubbing the unfenced one
		// leaves the claim returning nothing, and the arm then passes by never processing a message at all.
		var claimed = false;
		var dispatched = false;
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, 5L, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				claimed = true;
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], 5L));
			});

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(() => { dispatched = true; return Task.FromResult<IMessageResult>(DispatchMessageResult.Success()); });

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).ReturnsLazily(() => dispatched ? (long?)null : 5L);

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-gate-no-token");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS FIRST. Without this the arm is satisfied by a processor that claims nothing and dispatches
		// nothing -- every "must not have happened" below is trivially true of a drain that did no work, and
		// the arm would pass against the defect it exists to catch.
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		claimed.ShouldBeTrue("the fenced claim must have run, or the refusal path was never reached");

		// SAFETY. maxAttempts is 1, so the generic failure path would dead-letter on its FIRST pass: under
		// the old signal a routine leadership loss sent an already-dispatched message to the dead-letter
		// queue.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// A tenure that loses its token and then FAILS a dispatch must not write the failure unfenced.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The sibling arm above covers token-loss followed by a SUCCESSFUL delivery</b>, where the refusal is
	/// learned at the mark-sent. This one covers token-loss followed by a FAILED one, which reaches a
	/// different member by a different route and was covered nowhere.
	/// </para>
	/// <para>
	/// <b>Why the fence was unreachable exactly when it mattered.</b> The fenced failure route is guarded by
	/// a condition that CONJOINS the token, so the guard is false in two states: fencing is off, or fencing
	/// is on and the token has gone. The gate yields a token only while this instance is the leader, so the
	/// second state means THIS TENURE HAS BEEN SUPERSEDED -- and the code fell through it into an unfenced
	/// write. The fence disappeared in precisely the state it exists to refuse.
	/// </para>
	/// <para>
	/// <b>The claim term does not cover this, which is why the token is the only remedy.</b> The dispatcher
	/// identity is assigned once per process and survives losing and regaining leadership, so a claim-scoped
	/// write refuses a DIFFERENT dispatcher and never a STALE TENURE OF THE SAME ONE. The write therefore
	/// lands whenever the successor has not yet re-claimed the row -- the ordinary case straight after a
	/// handover, since the successor must wait for the reservation to age out. It consumes an attempt and
	/// pushes the visibility floor out by a full backoff interval against a row this drain no longer owns.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_DoesNotWriteAFailureUnfenced_WhenTheGateIsActiveButHasNoToken()
	{
		// Arrange
		const string MessageId = "message-failure-no-token";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));
		outboundMessage.DispatcherId = "dispatcher-failure-no-token";

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);

		// Leadership is held through transport admission and lost when that admitted dispatch returns. A token absent from the start
		// refuses at the claim, so the message is never dispatched and the arm would pass without ever
		// reaching the path it exists to test.
		var claimed = false;
		var dispatched = false;
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, 5L, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				claimed = true;
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], 5L));
			});

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(() => { dispatched = true; return Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("dispatch failed")); });

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).ReturnsLazily(() => dispatched ? (long?)null : 5L);

		// maxAttempts 3, so this failure takes the ordinary retry route and never reaches dead-lettering --
		// the row-destroying member has its own arm below.
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			leaderGate: leaderGate);
		processor.Init("dispatcher-failure-no-token");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS FIRST. Every "must not have happened" below is trivially true of a drain that claimed
		// nothing and dispatched nothing, and such a drain would pass this arm against the live defect.
		claimed.ShouldBeTrue("the fenced claim must have run, or the refusal path was never reached");
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// SAFETY. No failure write of ANY shape reaches the store without a token: not the unscoped member,
		// not the backoff member, and not the claim-scoped members -- the claim does not refuse a stale
		// tenure of the same process, so routing through it is not containment.
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(
				A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedWithBackoffAsync(
				A<string>._, A<string>._, A<int>._, A<DateTimeOffset>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// A retry-exhausted message whose tenure has been superseded must not have its outbox row destroyed.
	/// </summary>
	/// <remarks>
	/// <b>This is the severe branch.</b> The unfenced dead-letter transition copies the message to the
	/// dead-letter table and DELETES the outbox row on a message-id match alone -- no claim term, no tenure
	/// term. So a superseded tenure reaching its attempt ceiling destroys a row a live successor has already
	/// claimed and has not yet delivered. Every other completion leaves the row recoverable; this one does
	/// not, and the message is lost from the outbox entirely.
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_DoesNotDestroyTheOutboxRow_WhenARetryExhaustedTenureHasNoToken()
	{
		// Arrange
		const string MessageId = "message-deadletter-no-token";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));
		outboundMessage.DispatcherId = "dispatcher-deadletter-no-token";

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);

		var claimed = false;
		var dispatched = false;
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, 5L, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				claimed = true;
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], 5L));
			});

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(() => { dispatched = true; return Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("dispatch failed")); });

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).ReturnsLazily(() => dispatched ? (long?)null : 5L);

		// maxAttempts 1, so a failed dispatch exhausts retries on its FIRST pass and takes the dead-letter
		// route -- the branch that destroys the row.
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			leaderGate: leaderGate);
		processor.Init("dispatcher-deadletter-no-token");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS FIRST.
		claimed.ShouldBeTrue("the fenced claim must have run, or the refusal path was never reached");
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// SAFETY. The row survives for the tenure that now owns it.
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(
				A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// A REFUSED dead-letter mark withdraws the dead-letter entry this tenure already enqueued.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the invariant the whole dead-letter path turns on, and it had NO coverage.</b> The
	/// external enqueue happens BEFORE the fenced mark that would make it true, deliberately: a crash
	/// between the two must leave the message in the dead-letter queue rather than marked terminal and
	/// present nowhere. The cost of that order is that a REFUSED mark leaves an entry describing a message
	/// this tenure no longer owns.
	/// </para>
	/// <para>
	/// <b>Why an un-withdrawn entry is worse than it sounds.</b> A refusal means a newer tenure owns the
	/// row — and that tenure may go on to DELIVER the message successfully. The message is then
	/// simultaneously delivered and sitting unreplayed in the dead-letter queue, where the shipped redrive
	/// path selects pending entries by filter. An operator draining the queue re-executes a message that
	/// already succeeded, from our own bookkeeping rather than any fault of theirs. A dead-letter dedup key
	/// cannot help: there is exactly one entry and it is the wrong entry.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task WithdrawTheDeadLetterEntry_WhenTheFencedMarkIsRefused()
	{
		// Arrange
		const string MessageId = "message-dl-refused-compensate";
		var entryId = Guid.NewGuid();

		var (outboxStore, dispatcher, leaderGate, deadLetterQueue, admin) =
			CompensationHarness(MessageId, entryId, tokenSurvives: true);

		// The store REFUSES the fenced mark: a newer tenure has advanced the high-water.
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, null, null, a)), A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused));

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-dl-refused");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS FIRST -- the entry must actually have been written, or "withdrawn" is vacuous.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();

		// THE REFUSAL ACTUALLY HAPPENED. The store is a recording fake, so this is a spy's record, not the
		// configured return value: a processor that never asked the store and purged for some other reason
		// would otherwise pass this arm, and "never called" would read as "refused".
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(MessageId, A<string>._, A<int>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, MessageId, null, a)), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly()
			// SAFETY -- and the entry was withdrawn, by its own id, AFTER the refusal it compensates.
			.Then(A.CallTo(() => admin.PurgeAsync(entryId, A<CancellationToken>._)).MustHaveHappenedOnceExactly());

		Fake.GetCalls(outboxStore).Count(call => call.Method.Name.StartsWith("MarkFailed", StringComparison.Ordinal))
			.ShouldBe(0, "a refused recovery transfer must not trigger alternate failure bookkeeping under stale authority");
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// The same withdrawal happens when the tenure has NO TOKEN AT ALL, not only when the store refuses.
	/// </summary>
	/// <remarks>
	/// The refusal branch above is reached by asking the store and being told no. This one never reaches
	/// the store: under an active gate a null token IS the superseded state, so the fenced route is skipped
	/// entirely. Before this was closed the drain fell through to the UNFENCED transition, which destroys
	/// the outbox row on a message-id match alone — and left the dead-letter entry standing, because the
	/// compensation lived only on the branch that asked.
	/// </remarks>
	[Fact]
	public async Task WithdrawTheDeadLetterEntry_WhenTheTenureHasNoTokenAtAll()
	{
		// Arrange
		const string MessageId = "message-dl-no-token-compensate";
		var entryId = Guid.NewGuid();

		var (outboxStore, dispatcher, leaderGate, deadLetterQueue, admin) =
			CompensationHarness(MessageId, entryId, tokenSurvives: false);

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-dl-no-token");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS -- the drain actually ran; without this every "must not" below is trivially true.
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// SAFETY -- the entry is withdrawn ...
		A.CallTo(() => admin.PurgeAsync(entryId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// ... AND the row is not destroyed by the unfenced transition.
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(
				A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// LIVENESS, and it is the arm that matters most here: an APPLIED mark must NOT withdraw the entry.
	/// </summary>
	/// <remarks>
	/// <b>Without this, a processor that purged unconditionally would pass both safety arms above while
	/// silently deleting every legitimate dead letter it ever wrote.</b> That is a far worse defect than
	/// the one those arms exist to catch — the messages would be gone from the outbox AND gone from the
	/// queue an operator redrives from — and "the bad thing did not happen" cannot distinguish it, because
	/// purging everything satisfies "the stale entry was purged" perfectly.
	/// </remarks>
	[Fact]
	public async Task NotWithdrawTheDeadLetterEntry_WhenTheMarkIsApplied()
	{
		// Arrange
		const string MessageId = "message-dl-applied-keep";
		var entryId = Guid.NewGuid();

		var (outboxStore, dispatcher, leaderGate, deadLetterQueue, admin) =
			CompensationHarness(MessageId, entryId, tokenSurvives: true);

		// The store ACCEPTS the fenced mark: this tenure genuinely owns the row.
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, null, null, a)), A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied));

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-dl-applied");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS -- the entry was written ...
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();

		// SAFETY -- and it SURVIVES, because the mark that makes it true was applied.
		A.CallTo(() => admin.PurgeAsync(A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// Builds the shared harness for the three dead-letter compensation arms.
	/// </summary>
	/// <param name="messageId">The message the drain will claim and fail.</param>
	/// <param name="entryId">The id the dead-letter queue reports for the entry it wrote.</param>
	/// <param name="tokenSurvives">
	/// <see langword="false"/> drops the fencing token after the claim, which is the superseded state.
	/// </param>
	private static (IOutboxStore Store, IDispatcher Dispatcher, Excalibur.Dispatch.ILeaderProcessingGate Gate,
		IDeadLetterQueue Queue, IDeadLetterQueueAdmin Admin) CompensationHarness(
		string messageId, Guid entryId, bool tokenSurvives)
	{
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(messageId, messageType, new TestOutboxIntegrationEvent(messageId));
		outboundMessage.DispatcherId = "dispatcher-compensation";

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);

		var deadLetterEnqueued = false;
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, 5L, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], 5L));
			});

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("dispatch failed")));

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken)
			.ReturnsLazily(() => tokenSurvives || !deadLetterEnqueued ? 5L : (long?)null);

		// The queue must ALSO be the admin surface: withdrawal lives on IDeadLetterQueueAdmin, and the
		// processor resolves it by casting the queue it was given. A queue that is not an admin cannot be
		// compensated through, which is itself the documented degradation -- so the fake implements both.
		var deadLetterQueue = A.Fake<IDeadLetterQueue>(f => f.Implements<IDeadLetterQueueAdmin>());
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.ReturnsLazily(() => { deadLetterEnqueued = true; return Task.FromResult(entryId); });

		var admin = (IDeadLetterQueueAdmin)deadLetterQueue;
		A.CallTo(() => admin.PurgeAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(true));

		return (outboxStore, dispatcher, leaderGate, deadLetterQueue, admin);
	}

	/// <summary>
	/// LIVENESS for the two arms above: a tenure that STILL HOLDS its token completes a failure normally.
	/// </summary>
	/// <remarks>
	/// Without this, a processor that refused every completion write unconditionally -- or one that never
	/// reached the completion path at all -- would satisfy both safety arms perfectly. Refusing everything is
	/// the cheapest way to pass a "must not have happened" assertion and the most expensive way to be wrong:
	/// the outbox would retry every message forever and mark nothing.
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_StillWritesTheFailureFenced_WhenTheTenureKeepsItsToken()
	{
		// Arrange
		const string MessageId = "message-failure-token-held";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));
		outboundMessage.DispatcherId = "dispatcher-token-held";

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);

		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, 5L, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], 5L)));

		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkFailedAsync(
				A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied));

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("dispatch failed")));

		// The token is held throughout -- the tenure is never superseded.
		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).Returns(5L);

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			leaderGate: leaderGate);
		processor.Init("dispatcher-token-held");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS. The failure IS recorded, through the fenced member, carrying the tenure's token.
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkFailedAsync(
				A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_AbortsWithNoFurtherWrite_WhenFencedMarkSentIsRefused()
	{
		// Arrange
		const string MessageId = "message-fenced-stale";
		const long Tenure = 5L;

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		// FIXTURE HONESTY: consumers discover the fencing capability via GetService(typeof(T)), not an
		// `is`-cast. A bare FakeItEasy fake answers GetService with a non-null dummy that is not the
		// requested interface, so a capability the fake genuinely Implements<T>() reports absent. Teach it
		// the real contract: return itself for a capability it implements, null otherwise.
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], Tenure)));
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkSentAsync(MessageId, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, MessageId, Tenure, a)), A<CancellationToken>._))
			.ThrowsAsync(new StaleOutboxFencingTokenException("stale") { PresentedToken = Tenure, HighWaterToken = Tenure + 1 });

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).Returns(Tenure);

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-fenced");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- the record was dequeued (processed count reflects that), but NOTHING downstream of the
		// refused mark-sent may write to the store on this superseded tenure's behalf.
		processed.ShouldBe(1);
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// SAFETY (F1 fencing), the REPORTED refusal: a store that returns
	/// <see cref="OutboxCompletionOutcome.FenceRefused"/> instead of throwing must abort the message's drain
	/// cycle with NO further store write, exactly as the thrown form does.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm exists because the sibling above cannot reach the path it tests.</b> The sibling stubs the
	/// store to THROW, so its evidence is entirely about the catch clause. The fenced mark-sent now REPORTS
	/// its refusal in the return value, which is the path every first-party store takes and the only one a
	/// conforming consumer store should take -- and no catch clause is involved in it at all. Both arms are
	/// kept: the thrown form is still handled, because the missing-token composition guard raises it and a
	/// consumer store written against the older contract may too.
	/// </para>
	/// <para>
	/// <b>Requirement:</b> a superseded tenure performs no unfenced write after learning it is superseded.
	/// <b>Predicate this arm tests:</b> after a reported FenceRefused, neither the unfenced mark-failed nor
	/// either dead-letter route runs. Those are stated separately because a fix that routed the reported
	/// refusal into the generic handler would still return normally -- which is why the assertion is on the
	/// message's DISPOSITION and not on the drain returning without throwing.
	/// </para>
	/// <para>
	/// The real producer claims both rows, but the channel can split them into separate processing batches.
	/// At least one dispatch must reach the reported refusal. Exactly one fenced completion and no failure
	/// or dead-letter writes may follow. This integration control does not require both rows to dispatch:
	/// withdrawal may legitimately stop the queued row before its transport admission.
	/// </para>
	/// <para>
	/// This scheduling-independent control alone does not guarantee that deleting the local completion-loop
	/// break is mutation-red: the shared tenure admission guard may still stop a later write. The separate
	/// deterministic OutboxProcessorTenureShould controls prove already-admitted work is joined and later
	/// admissions are withdrawn. No two-dispatch barrier is used here because a one-row first batch would
	/// block the sole consumer from ever admitting the second row.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_AbortsWithNoFurtherWrite_WhenFencedMarkSentReportsRefusal()
	{
		// Arrange
		const string FirstMessageId = "message-fenced-reported-stale-1";
		const string SecondMessageId = "message-fenced-reported-stale-2";
		const long Tenure = 5L;

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var first = CreateOutboundMessageWithEnvelope(FirstMessageId, messageType, new TestOutboxIntegrationEvent(FirstMessageId));
		var second = CreateOutboundMessageWithEnvelope(SecondMessageId, messageType, new TestOutboxIntegrationEvent(SecondMessageId));

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);

		// Claimed once, then nothing: a second claim returning the same rows would let the drain mark them
		// on a later cycle and the count assertion below would stop discriminating.
		var claims = 0;
		var claimedIds = new List<string>();
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (Interlocked.Increment(ref claims) != 1) { return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [], Tenure)); }
				claimedIds.Add(first.Id);
				claimedIds.Add(second.Id);
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [first, second], Tenure));
			});

		// REPORTED, not thrown. This is the whole difference from the sibling arm.
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkSentAsync(A<string>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, null, Tenure, a)), A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused));

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).Returns(Tenure);

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		await using var processor = CreateProcessor(
			options: CreateParallelOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-fenced-reported");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		claimedIds.ShouldBe([FirstMessageId, SecondMessageId]);

		// LIVENESS FIRST. Without it every "must not have happened" below is trivially true of a drain that
		// claimed nothing, and the arm would pass against the defect it exists to catch.
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceOrMore();

		// SAFETY, and this is the assertion that discriminates. The first refusal is TENURE-scoped, so the
		// captured tenure admits no further completion. Depending on channel scheduling, the second row
		// may be in this batch or still queued; either way it must not receive another completion write.
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkSentAsync(A<string>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, null, Tenure, a)), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// SAFETY. Nothing downstream of the refused mark-sent may write on a superseded tenure's behalf.
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// The ROW-SCOPED half, and the arm that stops the fix over-correcting: a mark-sent refused for a reason
	/// about THIS MESSAGE must not stand the tenure down.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the liveness twin of the arm above, and it is the one that would be forgotten.</b>
	/// <see cref="OutboxCompletionOutcome.AlreadyTerminal"/> says the row was already sent or buried -- it is
	/// owned by nobody and the tenure is intact. A processor that treated it like a fence refusal would
	/// abandon every other message it still legitimately holds, which strands work a live tenure owns. Both
	/// outcomes mean "the write did not apply"; only one means "stop".
	/// </para>
	/// <para>
	/// <b>Predicate this arm tests:</b> the drain still completes normally and still performs no unfenced
	/// write for this message. It does NOT assert a log line, because the observable consequence a consumer
	/// experiences is the disposition, and an arm pinned to a log message goes green on a fix that keeps the
	/// message and changes the control flow.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_KeepsDraining_WhenFencedMarkSentReportsARowScopedRefusal()
	{
		// Arrange
		const string MessageId = "message-fenced-row-scoped";
		const long Tenure = 5L;

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(MessageId, messageType, new TestOutboxIntegrationEvent(MessageId));

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], Tenure)));
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkSentAsync(MessageId, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, MessageId, Tenure, a)), A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.AlreadyTerminal));

		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).Returns(Tenure);

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 3),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-row-scoped");

		// Act -- must not throw: a row-scoped refusal is an ordinary operational event.
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// LIVENESS: the message was dispatched and the drain reported it, so the cycle was not stood down.
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		processed.ShouldBe(1, "a row-scoped refusal must not reduce the drain's reported progress");

		// SAFETY: a refusal is still not a delivery failure, so no failure or dead-letter write follows.
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// SAFETY (F1 fencing), the non-batch fallback: a stale-fence refusal on the per-id completion loop must
	/// end the drain cycle QUIETLY -- it must not escape the processor.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Requirement:</b> a superseded tenure that learns it has lost leadership stops without surfacing an
	/// error to its caller. <b>Predicate this arm actually tests:</b> the public drain entry point returns
	/// normally instead of throwing, and no unfenced store write follows the refusal. Those are stated
	/// separately because they are not the same claim, and only the first one distinguishes the fix: before
	/// it, the refusal propagated out of the batch loop, and the failure-path writes were skipped only as a
	/// side effect of that propagation.
	/// </para>
	/// <para>
	/// <b>Why the exception escaping is the harm.</b> On a host with a background service the escape is
	/// swallowed one frame up, so this looks cosmetic there. It is not cosmetic on the manual-trigger path:
	/// <see cref="OutboxProcessor"/> is the public entry point a serverless consumer calls directly, with
	/// nothing above it to catch, so a routine leadership handover was surfacing to consumer function code
	/// and recording as an invocation failure. It also reported one handover as two error-level events where
	/// every other refusal site emits a single warning.
	/// </para>
	/// <para>
	/// <b>Reaching this branch takes three conditions</b>, none of which a default processor satisfies, so
	/// they are built explicitly rather than inherited from a preset: at-least-once delivery, batch database
	/// operations OFF, and a parallel degree above one. The shipped preset that turns batch operations off
	/// also selects the minimized-window guarantee, which this branch excludes -- so a reader who assumes
	/// "some preset covers it" is wrong, and the assumption is worth denying in the test itself.
	/// </para>
	/// <para>
	/// <b>Two messages, refused on the first.</b> That is what makes the assertion about the SECOND one
	/// meaningful: it was published but never marked, which is the deliberate, contract-legal outcome (it
	/// redelivers under at-least-once once the claim ages out) rather than an oversight.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_ReturnsQuietly_WhenTheNonBatchCompletionLoopIsFenced()
	{
		// Arrange
		const string FirstId = "message-fenced-nonbatch-1";
		const string SecondId = "message-fenced-nonbatch-2";
		const long Tenure = 11L;
		const string ClaimIdentity = "dispatcher-fenced-nonbatch";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var first = CreateOutboundMessageWithEnvelope(FirstId, messageType, new TestOutboxIntegrationEvent(FirstId));
		var second = CreateOutboundMessageWithEnvelope(SecondId, messageType, new TestOutboxIntegrationEvent(SecondId));

		// The claim identity is REQUIRED for the fenced completion route, and leaving it unset is what made
		// this arm nondeterministic. Without it the processor cannot take the claim-scoped path and falls
		// through to the UNFENCED MarkFailedAsync -- whose suppression then depended on whether a sibling
		// message in the same batch had already taught the drain its tenure was superseded. That is a
		// property of how producer and consumer happened to interleave, not of the fencing contract, so the
		// arm passed or failed on a scheduling accident and reddened the release-blocking shard under load.
		first.DispatcherId = ClaimIdentity;
		second.DispatcherId = ClaimIdentity;

		var outboxStore = CreateWorkerStore(fake => fake
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IDeadLetterableOutboxStore>());
		// The fencing capability is discovered through GetService, not an `is`-cast: a bare fake answers with
		// a non-null dummy of the wrong type, which reads as "capability absent". Teach it the real contract.
		A.CallTo(() => outboxStore.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(outboxStore) ? outboxStore : null);
		A.CallTo(() => ((IFencedOutboxStore)outboxStore).GetUnsentMessagesAsync(A<int>._, Tenure, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [first, second], Tenure)));
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkSentAsync(A<string>._, A<OutboxWriteAuthority>.That.Matches(a => IsExpectedAuthority(outboxStore, null, Tenure, a)), A<CancellationToken>._))
			.ThrowsAsync(new StaleOutboxFencingTokenException("superseded")
			{
				PresentedToken = Tenure,
				HighWaterToken = Tenure + 1,
			});

		// The store REFUSES the superseded tenure AT THE MEMBER PERFORMING THE WRITE. That is the contract
		// under test: the refusal is carried by the write itself, not inferred from something a sibling
		// message learned earlier in the same batch.
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)outboxStore).MarkFailedAsync(
				A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused));

		// The SECOND message fails to dispatch, which is what puts an entry in the retry set. Without that
		// entry the "no unfenced write" assertions below cannot fail under ANY implementation -- there
		// would be nothing for the failure loop to write -- and they would be decorative rather than
		// load-bearing. This is the arm's own guard against asserting something it cannot observe.
		// EXACTLY ONE of the two dispatches fails. Which one does not matter and is deliberately not
		// pinned -- the parallel degree makes the order genuinely nondeterministic, and an arm that
		// depended on identity here would be asserting a scheduling accident. What matters is only that
		// the retry set ends up non-empty.
		var dispatchCount = 0;
		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<IMessageResult>(
				Interlocked.Increment(ref dispatchCount) == 1
					? DispatchMessageResult.Success()
					: DispatchMessageResult.Failed("one message fails so the retry set is non-empty")));

		var leaderGate = A.Fake<Excalibur.Dispatch.ILeaderProcessingGate>();
		A.CallTo(() => leaderGate.ShouldProcess).Returns(true);
		A.CallTo(() => leaderGate.FencingToken).Returns(Tenure);

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		var processor = CreateProcessor(
			options: CreateNonBatchParallelAtLeastOnceOptions(),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue,
			leaderGate: leaderGate);
		processor.Init("dispatcher-fenced-nonbatch");

		// Act -- the assertion IS that this does not throw. Calling it outside Should.NotThrow keeps the
		// failure readable: an escape surfaces as the real exception and stack, not as a wrapped assertion.
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		processed.ShouldBe(
			1,
			"one message published and one failed to dispatch, so the count reflects the single delivery; "
			+ "the refusal governs what may be WRITTEN afterwards, not what was already dispatched.");

		// LIVENESS is carried by the processed count asserted above, NOT by demanding that a failure write
		// occurred. Whether the failure loop runs at all depends on the batch partition: when both messages
		// land in one batch the successful one teaches the drain its tenure is superseded and the early
		// return suppresses the loop entirely, so NO write is the correct outcome; when they land in two
		// batches the failing batch carries no success, the loop runs, and the write is attempted. An
		// earlier draft of this arm asserted the fenced member MUST have been called, and that was wrong in
		// exactly the same way the original arm was wrong -- it pinned a scheduling accident.
		//
		// SAFETY, and this is now partition-INDEPENDENT, which is the whole point of the repair. The claim
		// identity and the fenced capability above mean that IF the loop runs it routes to the fenced member,
		// where the store itself refuses the stale tenure. The unfenced member carries no tenure at all, so a
		// superseded caller reaching it would write with no authority. It is unreachable on either partition.
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)outboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// At-least-once, batch database operations OFF, parallel degree above one -- the only combination that
	/// reaches the per-id completion fallback. Stated explicitly rather than taken from a preset: no shipped
	/// preset produces it, and relying on a default would let a later default change silently retarget the
	/// arm at a different branch.
	/// </summary>
	private static IOptions<DeliveryOutboxOptions> CreateNonBatchParallelAtLeastOnceOptions() =>
		Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 2,
			ConsumerBatchSize = 2,
			PerRunTotal = 2,
			MaxAttempts = 3,
			DeliveryGuarantee = DeliveryOutboxDeliveryGuarantee.AtLeastOnce,
			EnableBatchDatabaseOperations = false,
			BatchProcessing = { ParallelProcessingDegree = 2 },
		});

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesToDeadLetterQueue_PreservesTenantId_LegacyPath()
	{
		// Independent regression lock (author != fixer) for the OutboxProcessor cross-provider TenantId
		// drop: the LEGACY conversion path (no envelope deserializer, JSON payload) must carry the
		// staged tenant onto the IOutboxMessage handed to the DLQ. NON-VACUOUS: RED on pre-fix
		// OutboxProcessor.cs (ConvertToOutboxMessageLegacy initializer omitted TenantId -> null), GREEN after.
		// Arrange — a multi-tenant outbound message whose dispatch fails terminally (maxAttempts == 1).
		const string tenantId = "tenant-legacy-7f3a";
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();

		var outboundMessage = CreateOutboundMessageWithEnvelope(
			"message-dlq-tenant-legacy", messageType, new TestOutboxIntegrationEvent("legacy"));
		outboundMessage.TenantId = tenantId;

		var outboxStore = CreateSingleMessageOutboxStore(outboundMessage);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure")));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-tenant-legacy");
		await using var scenario = new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — the DLQ-enqueued message preserves the tenant scope (RED pre-fix: TenantId was null).
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "message-dlq-tenant-legacy" && m.TenantId == tenantId),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(outboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesToDeadLetterQueue_PreservesTenantId_EnvelopePath()
	{
		// Independent regression lock (author != fixer) for the OutboxProcessor TenantId drop: the
		// ENVELOPE conversion path (binary-marker payload + envelope deserializer) must carry the staged
		// tenant onto the IOutboxMessage handed to the DLQ. NON-VACUOUS: RED on pre-fix OutboxProcessor.cs
		// (envelope-conversion initializer omitted TenantId -> null), GREEN after. The envelope path keys
		// the converted MessageId off envelope.MessageId, so the assertion matches that id.
		// Arrange — a multi-tenant binary-envelope outbound message whose dispatch fails terminally.
		const string tenantId = "tenant-envelope-9c21";
		const string envelopeMessageId = "11111111-1111-1111-1111-111111111111";
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();

		var outboundMessage = CreateBinaryEnvelopeOutboundMessage("message-dlq-tenant-envelope", messageType);
		outboundMessage.TenantId = tenantId;

		var outboxStore = CreateSingleMessageOutboxStore(outboundMessage);
		var envelopePayload = CreateNestedOutboxMessagePayload(
			envelopeMessageId, messageType, new TestOutboxIntegrationEvent("envelope"));
		var envelopeDeserializer = CreateEnvelopeDeserializer(
			Guid.Parse(envelopeMessageId), messageType, envelopePayload);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure")));

		var deadLetterQueue = CreateDeadLetterQueue();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			envelopeDeserializer: envelopeDeserializer);
		processor.Init("dispatcher-tenant-envelope");
		await using var scenario = new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — the DLQ-enqueued message preserves the tenant scope (RED pre-fix: TenantId was null).
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == envelopeMessageId && m.TenantId == tenantId),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(outboxStore);
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
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync("message-success", CapturedClaim(scenario.OutboxStore, "message-success"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "message-failure"),
				DeadLetterReason.MaxRetriesExceeded,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync("message-failure", A<string>._, A<int>._, CapturedClaim(scenario.OutboxStore, "message-failure"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(scenario.OutboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_LeavesCircuitOpenBatchForRetry_NotDeadLetter()
	{
		// bd-2tvy5s AC-2 (BATCH path, mandatory — CEO condition 1): a transient circuit-breaker-OPEN on the
		// bulk batch path must leave records FOR RETRY (MarkFailedAsync, attempt-preserved), NEVER dead-letter
		// them. RED on pre-fix (pre-fix dead-letters the whole batch on CB-open → bulk irreversible DLQ loss).
		// Flipped from the prior broken-behavior cert (...RoutesCircuitOpenBatchToDeadLetterQueue), NFR-6.
		// Arrange
		await using var scenario = await CreateParallelCircuitOpenScenarioAsync();

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — NOT dead-lettered on CB-open (neither via the DLQ nor the store)
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync(
				A<string>._,
				A<string>._, A<int>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Assert — both open records left for retry (marked failed/re-claimable, attempt preserved)
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync(A<string>.That.Matches(id => id == "message-open-1" || id == "message-open-2"), A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_SingleRecord_LeavesCircuitOpenForRetry_NotDeadLetter()
	{
		// bd-2tvy5s AC-1 (single-record DispatchReservedRecordAsync, non-vacuity): a forced circuit-breaker-OPEN
		// on the single-record path leaves the record FOR RETRY (MarkFailedAsync, attempt-preserved), NEVER
		// dead-letter. RED on pre-fix (pre-fix routes the record to the DLQ on CB-open).
		// Arrange — ParallelProcessingDegree == 1 (CreateValidOptions) selects the single-record path.
		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var message = CreateOutboundMessageWithEnvelope(
			"message-single-open", messageType, new TestOutboxIntegrationEvent("open"));
		var outboxStore = CreateSingleMessageOutboxStore(message);
		var serializer = new DispatchJsonSerializer();
		var dispatcher = A.Fake<IDispatcher>();
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreakerRegistry = CreateCircuitOpenRegistry();
		var serviceProvider = CreateServiceProvider(dispatcher);
		var processor = CreateProcessor(
			options: CreateValidOptions(),
			outboxStore: outboxStore,
			serializer: serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry ?? PassThroughCircuitBreakerRegistry.Instance);
		processor.Init("dispatcher-single-open");
		await using var scenario = new DispatchScenario(processor, outboxStore, deadLetterQueue, serviceProvider, dispatcher);

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert — NOT dead-lettered on CB-open
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync(
				"message-single-open",
				A<string>._, A<int>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)scenario.OutboxStore).MarkDeadLetteredAsync("message-single-open", A<string>._, A<int>._, CapturedClaim(scenario.OutboxStore, "message-single-open"), A<CancellationToken>._))
			.MustNotHaveHappened();

		// Assert — left for retry (marked failed/re-claimable, attempt preserved), and never dispatched (CB open)
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync("message-single-open", A<string>._, A<int>._, CapturedClaim(scenario.OutboxStore, "message-single-open"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
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
		processed.ShouldBe(0, "a refused delivery is not a successful dispatch");
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync("message-no-auto-retry", "dispatch failed", 1, CapturedClaim(scenario.OutboxStore, "message-no-auto-retry"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => backoffCalculator.CalculateDelay(A<int>._)).MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
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
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync("message-envelope", CapturedClaim(scenario.OutboxStore, "message-envelope"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync(setup.EnvelopeMessageId, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => scenario.OutboxStore.MarkSentAsync(setup.EnvelopeMessageId, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_ParallelProcessing_WithBatchDatabaseOperations_HandlesSuccessAndRetry()
	{
		// Arrange
		var backoff = A.Fake<IBackoffCalculator>();
		A.CallTo(() => backoff.CalculateDelay(1)).Returns(TimeSpan.FromSeconds(7));
		await using var scenario = await CreateParallelBatchRetryScenarioAsync(backoff);

		// Act
		var before = DateTimeOffset.UtcNow;
		var processed = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		var after = DateTimeOffset.UtcNow;

		// Assert
		processed.ShouldBe(1);
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync("message-success", CapturedClaim(scenario.OutboxStore, "message-success"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedWithBackoffAsync("message-retryable", ErrorConstants.RetryAttempt, 1,
			A<DateTimeOffset>.That.Matches(at => at >= before.AddSeconds(7) && at <= after.AddSeconds(7)), CapturedClaim(scenario.OutboxStore, "message-retryable"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		A.CallTo(() => backoff.CalculateDelay(1)).MustHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IDispatchMessage>.That.Matches(message => message is TestParallelFailureIntegrationEvent), A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		AssertCapturedWrites(scenario.OutboxStore);
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
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkSentAsync("message-minimized", CapturedClaim(scenario.OutboxStore, "message-minimized"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => ((IClaimScopedOutboxStore)scenario.OutboxStore).MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(scenario.OutboxStore);
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

	/// <summary>
	/// Drives the processor through a real decorator, which is the only shape in which the capability probe
	/// and a type cast disagree.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Requirement:</b> a retry-exhausted message reaches the terminal dead-lettered status when the store
	/// is decorated. <b>Predicate this arm tests:</b> the same, asserted at the inner store. The sibling arm
	/// in <c>TelemetryOutboxDecoratorDeadLetterTransparencyShould</c> proves the DECORATOR forwards the
	/// capability; it never drives the PROCESSOR through one, and that gap is where the defect lived.
	/// </para>
	/// <para>
	/// <b>Why a decorator and not a fake.</b> A bare fake is the exact inverse of a decorated store — the cast
	/// succeeds and the probe fails — so every arm built on one is green whichever discovery mechanism the
	/// processor uses. Measured: with the cast restored and the fakes correct, the whole suite stays green.
	/// Only a real decorator makes the two mechanisms disagree, and the premise is asserted below rather than
	/// assumed, so the arm fails loudly if a future decorator starts declaring the capability outright.
	/// </para>
	/// <para>
	/// <b>RED-on-mutant:</b> restore the cast (<c>_outboxStore is not IDeadLetterableOutboxStore</c>) and the
	/// processor throws instead of dead-lettering. The mutation breaks the requirement, not a proxy for it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_DeadLettersThroughADecoratedStore_WhenTheCapabilityIsOnlyReachableByProbing()
	{
		// Arrange
		const string MessageId = "message-decorated-dlq";

		var messageType = typeof(TestOutboxIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(
			MessageId,
			messageType,
			new TestOutboxIntegrationEvent(MessageId));

		var inner = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => inner.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(inner, [outboundMessage], null)));

		using var decorated = new TelemetryOutboxStoreDecorator(inner);

		((object)decorated is IClaimScopedDeadLetterableOutboxStore).ShouldBeFalse(
			"the claim-scoped terminal capability must require discovery through the decorator");
		_ = decorated.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>(
			"the processor's actual claim-scoped terminal capability must reach the inner store");

		// THE PREMISE, asserted rather than assumed. If these two ever agree, the arm below can no longer
		// discriminate the defect and would pass for the wrong reason -- so it fails here instead, saying why.
		((object)decorated is IDeadLetterableOutboxStore).ShouldBeFalse(
			"the decorator must NOT declare the capability, or a cast would find it and this arm would be green "
			+ "under both discovery mechanisms");
		_ = decorated.GetService(typeof(IDeadLetterableOutboxStore)).ShouldBeAssignableTo<IDeadLetterableOutboxStore>(
			"the decorator must forward the capability to its inner, or the message could not be dead-lettered "
			+ "by any mechanism and the arm would be asserting an impossibility");

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure")));

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));

		var processor = CreateProcessor(
			options: CreateSingleMessageOptions(maxAttempts: 1),
			outboxStore: decorated,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-decorated");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- the terminal transition landed on the INNER store, through the decorator.
		processed.ShouldBe(0, "a refused delivery is not a successful dispatch");
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)inner).MarkDeadLetteredAsync(MessageId, A<string>._, A<int>._, CapturedClaim(inner, MessageId), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		AssertCapturedWrites(inner);
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

	/// <summary>
	/// The dead-letter loop CONTINUES after one message's write throws, so the remaining failed messages
	/// still reach the queue.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the half its sibling cannot see.</b>
	/// <see cref="DispatchPendingMessagesAsync_StillMarksTheDeliveredMessage_WhenDeadLetteringAnotherMessageThrows"/>
	/// covers the expensive property — a throw must not skip batch completion for a message that already
	/// delivered — and it does so by making the sink throw for EVERY message. That is the right shape for
	/// that property and it makes continuation unobservable: when every write throws, a loop that stopped
	/// at the first one and a loop that ran to the end produce identical call sets. So the guard's OTHER
	/// stated property, that one bad message no longer takes the rest of the batch's dead letters with it,
	/// had no arm.
	/// </para>
	/// <para>
	/// <b>Two failing messages, and the sink throws for exactly one of them, keyed on the message ID.</b>
	/// Both share a dispatch type because the type decides the dispatch outcome and both must fail; the id
	/// is what separates their dead-letter outcomes. Keying on the id rather than on call order is
	/// load-bearing: the failed set is drained from a concurrent collection, so an arm keyed on "the first
	/// call" would pass or fail on which message the drain happened to pick up.
	/// </para>
	/// <para>
	/// <b>Mutant:</b> remove the per-iteration try/catch around the dead-letter call in the
	/// <c>failedToDeadLetter</c> loop. The throw escapes, the loop never reaches the second message, and
	/// the assertion below goes RED — while the sibling arm stays GREEN if the escape happens to be caught
	/// upstream, which is why this one is needed separately.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_StillDeadLettersTheRestOfTheBatch_WhenOneDeadLetterWriteThrows()
	{
		// Arrange
		const string ThrowingId = "message-dead-letter-sink-throws";
		const string SurvivorId = "message-dead-letter-must-still-land";

		var failingType = typeof(TestParallelFailureIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelFailureIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope(ThrowingId, failingType, new TestParallelFailureIntegrationEvent("throws")),
			CreateOutboundMessageWithEnvelope(SurvivorId, failingType, new TestParallelFailureIntegrationEvent("survives")));

		// Both fail their dispatch, so with MaxAttempts 1 both reach the dead-letter loop on this pass.
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(e => e is TestParallelFailureIntegrationEvent),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure -> dead letter")));

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));

		// THE INJECTION, keyed on identity so it cannot land on whichever message was drained first.
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => string.Equals(m.MessageId, ThrowingId, StringComparison.Ordinal)),
				A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.ThrowsAsync(new InvalidOperationException("dead-letter sink is unavailable for this message"));

		var processor = CreateProcessor(
			options: CreateNonBatchTerminalFailureOptions(),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-dlq-partial-fault");

		// Act -- called outside Should.NotThrowAsync deliberately: if the guard is absent the escape
		// surfaces as the real exception and stack rather than a wrapped assertion message.
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// NON-VACUITY: the faulting write was genuinely attempted. Without this the assertion below is
		// satisfied by a drain that dead-letters everything because it never hit the fault at all.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => string.Equals(m.MessageId, ThrowingId, StringComparison.Ordinal)),
				A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappened();

		// THE PROPERTY: the loop did not strand its remainder.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => string.Equals(m.MessageId, SurvivorId, StringComparison.Ordinal)),
				A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappened();
		AssertCapturedWrites(outboxStore);
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


	/// <summary>
	/// Independent regression lock (author != fixer, TestsDeveloper) for the dead-letter routing guard.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The property.</b> A fault while dead-lettering ONE message must not skip batch completion for a
	/// DIFFERENT message that already delivered. The two messages are unrelated; only the control flow
	/// joined them. Before the guard, a throw from <c>IDeadLetterQueue.EnqueueAsync</c> escaped the
	/// <c>failedToDeadLetter</c> loop, the completion block below it never ran, and a message that HAD been
	/// published was never marked sent -- so the next cycle published it again. A dead-letter fault turned
	/// into a duplicate delivery of someone else's message.
	/// </para>
	/// <para>
	/// <b>Identity, not call order.</b> The two dispatch outcomes are keyed on the message TYPE, following
	/// the existing parallel-scenario idiom in this file, so the arm cannot pass or fail on which message
	/// the drain happened to pick up first. Parallel degree is pinned to one for the same reason.
	/// </para>
	/// <para>
	/// <b>Options are stated explicitly rather than taken from a preset.</b> The completion path under test
	/// is the per-id fallback, which requires <c>EnableBatchDatabaseOperations = false</c>; a preset that
	/// later flipped that flag would silently retarget this arm at the batch branch, where the assertion
	/// below cannot fail.
	/// </para>
	/// <para>
	/// <b>Mutant — CORRECTED, and the original was aimed at a proxy.</b> Make
	/// <c>TryRouteToDeadLetterQueueAsync</c> rethrow from its general <c>catch</c> instead of returning
	/// <c>DeadLetterRoutingOutcome.Faulted</c>. The throw then escapes, completion is skipped, and this arm
	/// goes RED along with its continuation sibling.
	/// </para>
	/// <para>
	/// <b>The mutant this comment used to name does NOT redden it</b>, and that is worth keeping rather
	/// than quietly replacing. It said: <i>"delete the try/catch around RouteToDeadLetterQueueAsync in
	/// OutboxProcessor. The throw escapes, completion is skipped, and the safety assertion goes RED."</i>
	/// Measured: deleting the per-iteration guard in the <c>failedToDeadLetter</c> loop leaves this arm and
	/// its sibling GREEN. No exception ever reaches that guard — <c>TryRouteToDeadLetterQueueAsync</c> has
	/// already converted every non-cancellation fault into a returned outcome one level down, which is where
	/// the property actually lives. The loop's general <c>catch</c> is therefore unreachable; its
	/// <c>catch (OperationCanceledException)</c> is not, because that one IS rethrown to it and is what
	/// stops a shutdown skipping the completion block.
	/// </para>
	/// <para>
	/// <b>So the arm was sound and its stated mutant was not.</b> A mutation that deletes the thing a check
	/// reads proves only that the check reads it; this one deleted a guard the behaviour no longer flows
	/// through, reported no change, and would have been recorded as "non-vacuous, proven" by anyone who
	/// planted it and saw the suite stay green without asking why.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_StillMarksTheDeliveredMessage_WhenDeadLetteringAnotherMessageThrows()
	{
		// Arrange
		const string DeliveredId = "message-delivered-despite-dlq-fault";
		const string PoisonId = "message-terminal-failure";

		var deliveredType = typeof(TestParallelSuccessIntegrationEvent).Name;
		var failingType = typeof(TestParallelFailureIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();
		MessageTypeRegistry.RegisterType<TestParallelFailureIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope(DeliveredId, deliveredType, new TestParallelSuccessIntegrationEvent("ok")),
			CreateOutboundMessageWithEnvelope(PoisonId, failingType, new TestParallelFailureIntegrationEvent("terminal")));

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
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure -> dead letter")));

		// THE INJECTION: the dead-letter sink itself faults. This is the only thing that differs from the
		// liveness partner below.
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.ThrowsAsync(new InvalidOperationException("dead-letter sink is unavailable"));

		var processor = CreateProcessor(
			options: CreateNonBatchTerminalFailureOptions(),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-dlq-fault");

		// Act -- called outside Should.NotThrowAsync deliberately: if the guard is absent the escape
		// surfaces as the real exception and stack rather than a wrapped assertion message.
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- SAFETY. The delivered message is marked sent even though dead-lettering the OTHER
		// message threw. Without the guard this call never happens and the message redelivers next cycle.
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkSentAsync(DeliveredId, CapturedClaim(outboxStore, DeliveredId), A<CancellationToken>._))
			.MustHaveHappened();

		// The faulting sink was genuinely exercised -- otherwise the arm would pass on an implementation
		// that never dead-letters at all, which is the inaction that satisfies every safety-only assertion.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// The sibling of the arm above, for the OTHER sequential-path dead-letter call. Independent
	/// regression lock (author != fixer, TestsDeveloper).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why a second arm and not a theory.</b> The sequential path reaches the dead-letter queue from
	/// two distinct places — a decode failure (<see cref="DeadLetterReason.DeserializationFailed"/>) and a
	/// retry-ceiling failure (<see cref="DeadLetterReason.MaxRetriesExceeded"/>). They are different
	/// <c>catch</c> blocks in different parts of the method, and a guard added to one says nothing about
	/// the other. That is precisely the gap this bead turned out to have at the path level, so it is not a
	/// gap worth re-introducing at the call-site level.
	/// </para>
	/// <para>
	/// <b>The poison injection is the registry, not a corrupt payload.</b> A message whose declared type is
	/// absent from <c>MessageTypeRegistry</c> raises <c>TypeLoadException</c> inside
	/// <c>PrepareDispatchAsync</c>, which wraps every non-cancellation decode failure as
	/// <c>OutboxPoisonMessageException</c>. That is deterministic and needs no malformed bytes.
	/// </para>
	/// <para>
	/// <b>The reason assertion is what stops this arm being a duplicate.</b> Both sequential call sites
	/// route to the same queue, so an arm that only asserted "the delivered message was marked" could be
	/// exercising the retry-ceiling path and silently testing nothing new. Pinning the reason to
	/// <c>DeserializationFailed</c> binds it to the decode call specifically.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task DispatchPendingMessagesAsync_StillMarksTheDeliveredMessage_WhenDeadLetteringAPoisonMessageThrows()
	{
		// Arrange
		const string DeliveredId = "message-delivered-despite-poison-dlq-fault";
		const string PoisonId = "message-poison-unregistered-type";
		const string UnregisteredType = "ThisTypeIsDeliberatelyNotInTheRegistry";

		var deliveredType = typeof(TestParallelSuccessIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();

		// The poison row carries a well-formed envelope whose declared type the registry cannot resolve, so
		// the failure happens at DECODE and never reaches a dispatcher. The payload object is only there to
		// make the envelope well-formed -- its type is irrelevant because the lookup fails first.
		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope(DeliveredId, deliveredType, new TestParallelSuccessIntegrationEvent("ok")),
			CreateOutboundMessageWithEnvelope(PoisonId, UnregisteredType, new TestParallelSuccessIntegrationEvent("poison")));

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.ThrowsAsync(new InvalidOperationException("dead-letter sink is unavailable"));

		var processor = CreateProcessor(
			options: CreateNonBatchTerminalFailureOptions(),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-poison-dlq-fault");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- SAFETY. The decodable message is marked sent even though dead-lettering the poison row
		// threw. Without the guard the throw escapes DispatchReservedRecordAsync and the drain aborts.
		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkSentAsync(DeliveredId, CapturedClaim(outboxStore, DeliveredId), A<CancellationToken>._))
			.MustHaveHappened();

		// NON-VACUITY, and it is what distinguishes this arm from its MaxRetriesExceeded sibling: the fault
		// was injected on the DECODE call specifically. If this reason were ever to change, the arm is
		// testing a different call site than the one it documents.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				DeadLetterReason.DeserializationFailed,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappened();

		// The decodable message must never have been dead-lettered -- it dispatched cleanly. This rules out
		// an implementation that dead-letters the whole batch on one poison row.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == DeliveredId),
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// The liveness partner. A processor that swallowed everything -- never dead-lettering at all -- would
	/// satisfy the safety arm above perfectly, because nothing would ever throw from the sink. This arm
	/// fails on exactly that implementation: with no fault injected, the terminal failure MUST still reach
	/// the dead-letter queue and the delivered message MUST still be marked.
	/// </summary>
	[Fact]
	public async Task DispatchPendingMessagesAsync_StillDeadLettersNormally_WhenTheSinkDoesNotFault()
	{
		// Arrange
		const string DeliveredId = "message-delivered-no-fault";
		const string FailedId = "message-terminal-no-fault";

		var deliveredType = typeof(TestParallelSuccessIntegrationEvent).Name;
		var failingType = typeof(TestParallelFailureIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestParallelSuccessIntegrationEvent>();
		MessageTypeRegistry.RegisterType<TestParallelFailureIntegrationEvent>();

		var outboxStore = CreateParallelOutboxStore(
			CreateOutboundMessageWithEnvelope(DeliveredId, deliveredType, new TestParallelSuccessIntegrationEvent("ok")),
			CreateOutboundMessageWithEnvelope(FailedId, failingType, new TestParallelFailureIntegrationEvent("terminal")));

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
			.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Failed("terminal failure -> dead letter")));

		var deadLetterQueue = CreateDeadLetterQueue();

		var processor = CreateProcessor(
			options: CreateNonBatchTerminalFailureOptions(),
			outboxStore: outboxStore,
			serializer: new DispatchJsonSerializer(),
			serviceProvider: CreateServiceProvider(dispatcher),
			deadLetterQueue: deadLetterQueue);
		processor.Init("dispatcher-dlq-healthy");

		// Act
		_ = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- LIVENESS. The guard must not have turned dead-lettering into a no-op.
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._, A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappened();

		A.CallTo(() => ((IClaimScopedOutboxStore)outboxStore).MarkSentAsync(DeliveredId, CapturedClaim(outboxStore, DeliveredId), A<CancellationToken>._))
			.MustHaveHappened();
		AssertCapturedWrites(outboxStore);
	}

	/// <summary>
	/// At-least-once, batch database operations OFF, one message terminal on its first attempt. Stated
	/// explicitly rather than taken from a preset: the arms above bind the PER-ID completion fallback, and
	/// a preset whose <c>EnableBatchDatabaseOperations</c> later flipped would retarget them at the batch
	/// branch without any test failing.
	/// </summary>
	private static IOptions<DeliveryOutboxOptions> CreateNonBatchTerminalFailureOptions() =>
		Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 2,
			ConsumerBatchSize = 2,
			PerRunTotal = 2,
			MaxAttempts = 1,
			DeliveryGuarantee = DeliveryOutboxDeliveryGuarantee.AtLeastOnce,
			EnableBatchDatabaseOperations = false,
			BatchProcessing = { ParallelProcessingDegree = 1 },
		});

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
		IBinaryEnvelopeDeserializer? envelopeDeserializer = null,
		Excalibur.Dispatch.ILeaderProcessingGate? leaderGate = null)
	{
		return new OutboxProcessor(
			options ?? CreateValidOptions(),
			outboxStore ?? CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>()),
			serializer ?? new DispatchJsonSerializer(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			logger ?? NullLogger<OutboxProcessor>.Instance,

			envelopeDeserializer: envelopeDeserializer,
			deadLetterQueue: deadLetterQueue ?? Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance,
			circuitBreakerRegistry: circuitBreakerRegistry ?? PassThroughCircuitBreakerRegistry.Instance,
			backoffCalculator: backoffCalculator,
			deliveryGuaranteeOptions: deliveryGuaranteeOptions,
			leaderGate: leaderGate);
	}

	[Theory]
	[InlineData(1, "healthy")]
	[InlineData(2, "healthy")]
	[InlineData(1, "open")]
	[InlineData(2, "open")]
	[InlineData(1, "poison")]
	[InlineData(2, "poison")]
	[InlineData(1, "refused")]
	[InlineData(2, "refused")]
	public async Task CountOnlyAcceptedDispatchesAcrossSequentialAndParallelProcessing(int degree, string outcome)
	{
		MessageTypeRegistry.RegisterType<TestOutboxIntegrationEvent>();
		var message = CreateOutboundMessageWithEnvelope("count-" + outcome,
			outcome == "poison" ? "UNREGISTERED-COUNT-CONTROL" : typeof(TestOutboxIntegrationEvent).Name,
			new TestOutboxIntegrationEvent(outcome));
		var store = CreateSingleMessageOutboxStore(message);
		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(outcome == "refused"
				? DispatchMessageResult.Failed("CONTROLLED-REFUSAL") : DispatchMessageResult.Success()));
		var services = CreateServiceProvider(dispatcher);
		var options = CreateSingleMessageOptions(3);
		options.Value.BatchProcessing.ParallelProcessingDegree = degree;
		var processor = CreateProcessor(options: options, outboxStore: store, serviceProvider: services,
			deadLetterQueue: CreateDeadLetterQueue(), circuitBreakerRegistry: outcome == "open"
				? CreateCircuitOpenRegistry() : PassThroughCircuitBreakerRegistry.Instance);
		processor.Init("native-count-control");
		await using var scenario = new DispatchScenario(processor, store, CreateDeadLetterQueue(), services, dispatcher);
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None)).ShouldBe(outcome == "healthy" ? 1 : 0);
		if (outcome is "open" or "poison")
		{
			A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		}
		AssertCapturedWrites(store);
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

		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, [outboundMessage], null)));

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

	private static async Task<DispatchScenario> CreateParallelBatchRetryScenarioAsync(IBackoffCalculator backoffCalculator)
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
			deadLetterQueue: deadLetterQueue, backoffCalculator: backoffCalculator);
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
			circuitBreakerRegistry: circuitBreakerRegistry ?? PassThroughCircuitBreakerRegistry.Instance);
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
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var fetchCount = 0;
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? [message]
					: [];
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, batch, null));
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
			messageBody: System.Text.Encoding.UTF8.GetBytes(eventJson),
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
		var outboxStore = CreateWorkerStore(fake => fake.Implements<IDeadLetterableOutboxStore>());
		var fetchCount = 0;
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? [first, second]
					: [];
				return new ValueTask<IEnumerable<OutboundMessage>>(ClaimRows(outboxStore, batch, null));
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
			messageBody: System.Text.Encoding.UTF8.GetBytes(eventJson),
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

	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IOutboxStore, FixtureClaims> s_fixtureClaims = new();

	private static IOutboxStore CreateWorkerStore(Action<FakeItEasy.Creation.IFakeOptions<IOutboxStore>> configure)
	{
		var store = CapabilityHonouringFakes.OutboxStore(options =>
		{
			options.Implements<IClaimScopedOutboxStore>().Implements<IClaimScopedDeadLetterableOutboxStore>();
			configure(options);
		});
		var state = s_fixtureClaims.GetValue(store, _ => new FixtureClaims());
		var plain = (IClaimScopedOutboxStore)store;
		A.CallTo(() => plain.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string claim, CancellationToken token) => state.Complete(id, claim, null, token));
		A.CallTo(() => plain.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string _, int _, string claim, CancellationToken token) => state.Complete(id, claim, null, token));
		A.CallTo(() => plain.MarkFailedWithBackoffAsync(A<string>._, A<string>._, A<int>._, A<DateTimeOffset>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string _, int _, DateTimeOffset _, string claim, CancellationToken token) => state.Complete(id, claim, null, token));
		A.CallTo(() => ((IClaimScopedDeadLetterableOutboxStore)store).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string _, int _, string claim, CancellationToken token) => state.Complete(id, claim, null, token));
		if (store.GetService(typeof(IFencedClaimScopedOutboxStore)) is IFencedClaimScopedOutboxStore fenced)
		{
			A.CallTo(() => fenced.MarkSentAsync(A<string>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily((string id, OutboxWriteAuthority authority, CancellationToken token) => state.Complete(id, authority.ClaimIdentity, authority.FencingToken, token));
			A.CallTo(() => fenced.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, int _, DateTimeOffset? _, OutboxWriteAuthority authority, CancellationToken token) => state.Complete(id, authority.ClaimIdentity, authority.FencingToken, token));

		}
		if (store.GetService(typeof(IFencedDeadLetterableOutboxStore)) is IFencedDeadLetterableOutboxStore terminal)
		{
			A.CallTo(() => terminal.MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, int _, OutboxWriteAuthority authority, CancellationToken token) => state.Complete(id, authority.ClaimIdentity, authority.FencingToken, token));
		}
		return store;
	}

	private static IEnumerable<OutboundMessage> ClaimRows(IOutboxStore store, IEnumerable<OutboundMessage> rows, long? tenure)
	{
		var state = s_fixtureClaims.GetValue(store, _ => new FixtureClaims());
		var acquired = rows.ToArray();
		lock (state.Claims)
		{
			foreach (var row in acquired)
			{
				// Several producer controls deliberately re-observe the same row. That is not a
				// new lease: retain its captured identity so a producer cannot invalidate queued work.
				if (!state.Claims.TryGetValue(row.Id, out var claim))
				{
					claim = new FixtureClaim(string.IsNullOrWhiteSpace(row.DispatcherId) ? Guid.NewGuid().ToString("N") : row.DispatcherId, tenure);
					state.Claims.Add(row.Id, claim);
				}
				row.DispatcherId = claim.Identity;
			}
		}
		return acquired;
	}

	private static string CapturedClaim(IOutboxStore store, string id)
	{
		var state = s_fixtureClaims.GetValue(store, _ => new FixtureClaims());
		lock (state.Claims) { return state.Claims[id].Identity; }
	}

	private static bool IsExpectedAuthority(IOutboxStore store, string? id, long? tenure, OutboxWriteAuthority authority)
	{
		var state = s_fixtureClaims.GetValue(store, _ => new FixtureClaims());
		lock (state.Claims)
		{
			return authority.FencingToken > 0 && (tenure is null || tenure == authority.FencingToken)
				&& state.Claims.Any(pair => (id is null || pair.Key == id) && pair.Value.Tenure == authority.FencingToken
					&& string.Equals(pair.Value.Identity, authority.ClaimIdentity, StringComparison.Ordinal));
		}
	}

	private static void AssertCapturedWrites(IOutboxStore store)
	{
		var state = s_fixtureClaims.GetValue(store, _ => new FixtureClaims());
		foreach (var call in Fake.GetCalls(store).Where(call => call.Method.Name.StartsWith("Mark", StringComparison.Ordinal)))
		{
			var declaring = call.Method.DeclaringType;
			(declaring == typeof(IClaimScopedOutboxStore) || declaring == typeof(IClaimScopedDeadLetterableOutboxStore)
				|| declaring == typeof(IFencedClaimScopedOutboxStore) || declaring == typeof(IFencedDeadLetterableOutboxStore)).ShouldBeTrue("worker completions must not use ID-only or token-only writes");
			var id = (string)call.Arguments[0]!;
			FixtureClaim expected;
			lock (state.Claims) { expected = state.Claims[id]; }
			if (declaring == typeof(IFencedClaimScopedOutboxStore) || declaring == typeof(IFencedDeadLetterableOutboxStore))
			{
				var authority = call.Arguments.OfType<OutboxWriteAuthority>().Single();
				authority.ClaimIdentity.ShouldBe(expected.Identity);
				authority.FencingToken.ShouldBe(expected.Tenure ?? throw new InvalidOperationException("The fixture did not acquire this row under a tenure."));
			}
			else
			{
				var claimIndex = call.Method.Name == "MarkSentAsync" ? 1 : call.Method.Name == "MarkFailedWithBackoffAsync" ? 4 : 3;
				((string)call.Arguments[claimIndex]!).ShouldBe(expected.Identity);
			}
		}
	}

	private sealed class FixtureClaims
	{
		internal Dictionary<string, FixtureClaim> Claims { get; } = new(StringComparer.Ordinal);

		internal ValueTask<OutboxCompletionOutcome> Complete(string id, string claim, long? tenure, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			lock (Claims)
			{
				if (!Claims.TryGetValue(id, out var expected) || expected.Completed
					|| !string.Equals(expected.Identity, claim, StringComparison.Ordinal))
				{
					return ValueTask.FromResult(OutboxCompletionOutcome.ClaimLost);
				}
				if (expected.Tenure != tenure || tenure is <= 0)
				{
					return ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
				}
				expected.Completed = true;
				return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
			}
		}
	}

	private sealed record FixtureClaim(string Identity, long? Tenure)
	{
		internal bool Completed { get; set; }
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
