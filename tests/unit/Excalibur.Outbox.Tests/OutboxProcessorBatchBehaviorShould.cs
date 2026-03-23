using Microsoft.Extensions.Logging.Abstractions;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DeliveryMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;
using DeliveryOutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Additional behavior tests for batch/error paths in <see cref="OutboxProcessor"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxProcessorBatchBehaviorShould : UnitTestBase
{
	private static readonly MethodInfo PerformBatchDatabaseOperationsAsyncMethod = typeof(OutboxProcessor)
		.GetMethod("PerformBatchDatabaseOperationsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private PerformBatchDatabaseOperationsAsync method.");

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_MarksSentRetryAndDeadLetterBuckets()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		await using var processor = CreateProcessor(outboxStore: outboxStore);

		// Act
		await InvokePrivateAsync(
			PerformBatchDatabaseOperationsAsyncMethod,
			processor,
			new List<string> { "sent-1", "sent-2" },
			new List<(string, int)> { ("retry-1", 2) },
			new List<(string, int)> { ("dead-1", 3) },
			CancellationToken.None);

		// Assert
		A.CallTo(() => outboxStore.MarkSentAsync("sent-1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => outboxStore.MarkSentAsync("sent-2", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => outboxStore.MarkFailedAsync("retry-1", ErrorConstants.RetryAttempt, 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => outboxStore.MarkFailedAsync(
				"dead-1",
				ErrorConstants.MaxRetriesReachedMovedToDeadLetter,
				3,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_DoesNothing_WhenBucketsAreEmpty()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		await using var processor = CreateProcessor(outboxStore: outboxStore);

		// Act
		await InvokePrivateAsync(
			PerformBatchDatabaseOperationsAsyncMethod,
			processor,
			new List<string>(),
			new List<(string, int)>(),
			new List<(string, int)>(),
			CancellationToken.None);

		// Assert
		A.CallTo(() => outboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_ThrowsOperationCanceledException_WhenTokenIsCanceled()
	{
		// Arrange
		var outboxStore = A.Fake<IOutboxStore>();
		await using var processor = CreateProcessor(outboxStore: outboxStore);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var ex = await Should.ThrowAsync<OperationCanceledException>(() => InvokePrivateAsync(
			PerformBatchDatabaseOperationsAsyncMethod,
			processor,
			new List<string> { "sent-1" },
			new List<(string, int)>(),
			new List<(string, int)>(),
			cts.Token));

		// Assert
		ex.ShouldNotBeNull();
		A.CallTo(() => outboxStore.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => outboxStore.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PerformBatchDatabaseOperationsAsync_ContinuesAfterStoreException_WhenAnyBucketFails()
	{
		// Arrange - T.2: Per-message try/catch prevents partial batch failure cascade
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.MarkFailedAsync("retry-1", ErrorConstants.RetryAttempt, 1, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("mark-failed"));
		await using var processor = CreateProcessor(outboxStore: outboxStore);

		// Act - Should NOT throw; individual failures are caught per-message
		await InvokePrivateAsync(
			PerformBatchDatabaseOperationsAsyncMethod,
			processor,
			new List<string> { "sent-1" },
			new List<(string, int)> { ("retry-1", 1) },
			new List<(string, int)>(),
			CancellationToken.None);

		// Assert - The sent message marking should still have been attempted
		A.CallTo(() => outboxStore.MarkSentAsync("sent-1", A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task DispatchPendingMessagesAsync_RoutesToDeadLetter_WhenCircuitOpensDuringExecution()
	{
		// Arrange
		await using var scenario = await CreateCircuitOpenDuringExecutionScenarioAsync();

		// Act
		_ = await scenario.Processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => scenario.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(message => message.MessageId == "message-open-during-execution"),
				DeadLetterReason.CircuitBreakerOpen,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.OutboxStore.MarkFailedAsync(
				"message-open-during-execution",
				A<string>.That.Contains("Moved to DLQ: Circuit breaker open"),
				3,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => scenario.Dispatcher.DispatchAsync(A<IIntegrationEvent>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static IOptions<DeliveryOutboxOptions> CreateParallelOptions(int maxAttempts)
	{
		return Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = maxAttempts,
			BatchProcessing = { ParallelProcessingDegree = 2 },
			EnableBatchDatabaseOperations = true,
		});
	}

	private static OutboxProcessor CreateProcessor(
		IOptions<DeliveryOutboxOptions>? options = null,
		IOutboxStore? outboxStore = null,
		DispatchJsonSerializer? serializer = null,
		IServiceProvider? serviceProvider = null,
		ILogger<OutboxProcessor>? logger = null,
		IDeadLetterQueue? deadLetterQueue = null,
		ITransportCircuitBreakerRegistry? circuitBreakerRegistry = null)
	{
		return new OutboxProcessor(
			options ?? CreateParallelOptions(maxAttempts: 3),
			outboxStore ?? A.Fake<IOutboxStore>(),
			serializer ?? new DispatchJsonSerializer(),
			serviceProvider ?? A.Fake<IServiceProvider>(),
			logger ?? NullLogger<OutboxProcessor>.Instance,

			internalSerializer: null,
			deadLetterQueue: deadLetterQueue,
			circuitBreakerRegistry: circuitBreakerRegistry);
	}

	private static IOutboxStore CreateOutboxStore(OutboundMessage message)
	{
		var outboxStore = A.Fake<IOutboxStore>();
		var fetchCount = 0;
		_ = A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				fetchCount++;
				IEnumerable<OutboundMessage> batch = fetchCount <= 2
					? new[] { message }
					: Array.Empty<OutboundMessage>();
				return new ValueTask<IEnumerable<OutboundMessage>>(batch);
			});
		return outboxStore;
	}

	private static readonly JsonSerializerOptions s_testJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	private static OutboundMessage CreateOutboundMessageWithEnvelope(
		string messageId,
		string messageType,
		TestCircuitIntegrationEvent integrationEvent)
	{
		var eventJson = JsonSerializer.Serialize(integrationEvent, s_testJsonOptions);
		var metadata = new DeliveryMessageMetadata(
			MessageId: messageId,
			CorrelationId: "corr",
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
			Payload = Encoding.UTF8.GetBytes(envelopeJson),
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = 0
		};
	}

	private static Task<CircuitOpenScenario> CreateCircuitOpenDuringExecutionScenarioAsync()
	{
		var messageSetup = CreateCircuitOpenMessageSetup();
		var runtimeSetup = CreateCircuitOpenRuntimeSetup();
		var serviceProvider = CreateServiceProvider(runtimeSetup.Dispatcher);
		var processor = CreateProcessor(
			options: CreateParallelOptions(maxAttempts: 3),
			outboxStore: messageSetup.OutboxStore,
			serializer: messageSetup.Serializer,
			serviceProvider: serviceProvider,
			deadLetterQueue: runtimeSetup.DeadLetterQueue,
			circuitBreakerRegistry: runtimeSetup.CircuitBreakerRegistry);
		processor.Init("dispatcher-circuit-opens");

		var scenario = new CircuitOpenScenario(
			processor,
			messageSetup.OutboxStore,
			runtimeSetup.DeadLetterQueue,
			runtimeSetup.Dispatcher,
			serviceProvider);
		return Task.FromResult(scenario);
	}

	private static CircuitOpenMessageSetup CreateCircuitOpenMessageSetup()
	{
		var messageType = typeof(TestCircuitIntegrationEvent).Name;
		MessageTypeRegistry.RegisterType<TestCircuitIntegrationEvent>();
		var outboundMessage = CreateOutboundMessageWithEnvelope(
			"message-open-during-execution",
			messageType,
			new TestCircuitIntegrationEvent("value"));
		var outboxStore = CreateOutboxStore(outboundMessage);
		var serializer = new DispatchJsonSerializer();
		return new CircuitOpenMessageSetup(outboxStore, serializer);
	}

	private static CircuitOpenRuntimeSetup CreateCircuitOpenRuntimeSetup()
	{
		var dispatcher = A.Fake<IDispatcher>();
		var deadLetterQueue = CreateDeadLetterQueue();
		var circuitBreakerRegistry = CreateCircuitBreakerRegistry();
		return new CircuitOpenRuntimeSetup(dispatcher, deadLetterQueue, circuitBreakerRegistry);
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

	private static ITransportCircuitBreakerRegistry CreateCircuitBreakerRegistry()
	{
		var circuitBreaker = A.Fake<ICircuitBreakerPolicy>();
		A.CallTo(() => circuitBreaker.State).Returns(CircuitState.Closed);
		A.CallTo(() => circuitBreaker.ExecuteAsync<bool>(A<Func<CancellationToken, Task<bool>>>._, A<CancellationToken>._))
			.Throws(new CircuitBreakerOpenException("transport"));

		var circuitBreakerRegistry = A.Fake<ITransportCircuitBreakerRegistry>();
		A.CallTo(() => circuitBreakerRegistry.GetOrCreate(A<string>._)).Returns(circuitBreaker);
		return circuitBreakerRegistry;
	}

	private static ServiceProvider CreateServiceProvider(IDispatcher dispatcher)
	{
		var services = new ServiceCollection();
		_ = services.AddScoped(_ => dispatcher);
		return services.BuildServiceProvider();
	}

	private static async Task InvokePrivateAsync(
		MethodInfo method,
		object target,
		params object?[] args)
	{
		try
		{
			var task = (Task?)method.Invoke(target, args);
			if (task is null)
			{
				throw new InvalidOperationException($"Expected Task return from '{method.Name}'.");
			}

			await task;
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	private sealed class CircuitOpenScenario : IAsyncDisposable
	{
		public CircuitOpenScenario(
			OutboxProcessor processor,
			IOutboxStore outboxStore,
			IDeadLetterQueue deadLetterQueue,
			IDispatcher dispatcher,
			ServiceProvider serviceProvider)
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

	private sealed record CircuitOpenMessageSetup(
		IOutboxStore OutboxStore,
		DispatchJsonSerializer Serializer);

	private sealed record CircuitOpenRuntimeSetup(
		IDispatcher Dispatcher,
		IDeadLetterQueue DeadLetterQueue,
		ITransportCircuitBreakerRegistry CircuitBreakerRegistry);

	private sealed record TestCircuitIntegrationEvent(string Value) : IIntegrationEvent;
}
