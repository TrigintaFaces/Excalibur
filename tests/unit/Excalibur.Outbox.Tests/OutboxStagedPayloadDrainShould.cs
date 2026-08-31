// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;
using DispatchMessageResult = Excalibur.Dispatch.MessageResult;
using ProducerMetadata = Excalibur.Dispatch.Metadata.MessageMetadata;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Locks the agreement between what the outbox WRITES and what the hosted drain READS.
/// </summary>
/// <remarks>
/// <para>
/// Every arm stages through the real producer API (<see cref="MessageOutbox.SaveEventsAsync"/>) into a
/// real store, and drains through the real hosted consumer
/// (<see cref="OutboxProcessor.DispatchPendingMessagesAsync"/>). Nothing here hand-builds the stored
/// payload: a fixture that constructs the body itself asserts only that the fixture and the reader agree,
/// which stays green whether or not the WRITER agrees with either of them. That is precisely the hole
/// this class exists to close, so the shape is load-bearing -- do not "simplify" these arms by
/// substituting a hand-built body for the producer call.
/// </para>
/// <para>
/// The one arm that does stage a body directly is <c>PoisonPayload</c>, which models a row that is
/// already corrupt (or was written by something else). There is no producer call that emits an
/// undecodable payload, so staging one is the only honest way to reach that path.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxStagedPayloadDrainShould : UnitTestBase
{
	[Theory]
	// Both drain paths, because the processor picks between them on this number and they do NOT share
	// their poison-decode, circuit-breaker or dead-letter handling: 1 walks the single-message loop,
	// 4 walks the parallel batch path. Every arm here previously ran at 1 only.
	[InlineData(1)]
	[InlineData(4)]
	public async Task DispatchEventStagedThroughSaveEventsAsync(int parallelProcessingDegree)
	{
		// The core agreement: SaveEventsAsync stages the serialized EVENT as the message body, so the
		// drain must read the body as that event. RED before the fix -- the drain re-read the body as a
		// nested OutboxMessage envelope, which has five `required` members a serialized event cannot
		// satisfy, so binding threw and the handler was never reached.
		MessageTypeRegistry.RegisterType<StagedProbeEvent>();

		await using var harness = new DrainHarness(maxAttempts: 1, parallelProcessingDegree: parallelProcessingDegree);
		var probe = new StagedProbeEvent("staged-through-the-real-api");

		await harness.Outbox.SaveEventsAsync([probe], CreateProducerMetadata(), CancellationToken.None)
			.ConfigureAwait(false);

		var processed = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None)
			.ConfigureAwait(false);

		processed.ShouldBe(1);

		// The handler received the event the producer staged -- same type, same value.
		A.CallTo(() => harness.Dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(m => m.GetType() == typeof(StagedProbeEvent) && ((StagedProbeEvent)m).Value == probe.Value),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// A dispatched message is a delivered message, not a dead-lettered one.
		A.CallTo(() => harness.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Theory]
	// Both drain paths, because the processor picks between them on this number and they do NOT share
	// their poison-decode, circuit-breaker or dead-letter handling: 1 walks the single-message loop,
	// 4 walks the parallel batch path. Every arm here previously ran at 1 only.
	[InlineData(1)]
	[InlineData(4)]
	public async Task NotChargeTheCircuitBreakerForAPoisonPayload(int parallelProcessingDegree)
	{
		// The second half of the same defect. An undecodable body can never succeed and never reaches the
		// transport, so it is not evidence that the transport is unhealthy. Charging it to the breaker
		// lets a single bad row open the circuit and stall delivery of every healthy message behind it.
		//
		// RED before the fix on all three assertions: decoding happened INSIDE the breaker (and inside
		// the guarded call), so the failure was recorded twice -- once by the breaker's own catch and
		// once by the explicit RecordFailure in the caller -- and the row was filed as a transport
		// failure rather than as a corrupt one.
		MessageTypeRegistry.RegisterType<StagedProbeEvent>();

		await using var harness = new DrainHarness(maxAttempts: 1, parallelProcessingDegree: parallelProcessingDegree);

		await harness.Store.StageMessageAsync(
			new OutboundMessage
			{
				Id = "poison-row",
				MessageType = typeof(StagedProbeEvent).FullName!,
				Payload = Encoding.UTF8.GetBytes("this is not the json anybody wrote"),
				CreatedAt = DateTimeOffset.UtcNow,
				RetryCount = 0,
			},
			CancellationToken.None).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		// The breaker was never told anything about this message -- it was neither entered nor charged.
		harness.CircuitBreaker.ExecutionsEntered.ShouldBe(0);
		harness.CircuitBreaker.FailuresObserved.ShouldBe(0);

		// And the row is filed for what it actually is.
		A.CallTo(() => harness.DeadLetterQueue.EnqueueAsync(
				A<IOutboxMessage>.That.Matches(m => m.MessageId == "poison-row"),
				DeadLetterReason.DeserializationFailed,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Theory]
	// Both drain paths, because the processor picks between them on this number and they do NOT share
	// their poison-decode, circuit-breaker or dead-letter handling: 1 walks the single-message loop,
	// 4 walks the parallel batch path. Every arm here previously ran at 1 only.
	[InlineData(1)]
	[InlineData(4)]
	public async Task KeepDeliveringHealthyMessagesAlongsideAPoisonOne(int parallelProcessingDegree)
	{
		// Liveness arm for the clause above: the poison row must not take the healthy message with it.
		// Without the fix the breaker is charged for the corrupt row, which is the mechanism by which one
		// bad row degrades delivery for everything sharing its transport.
		MessageTypeRegistry.RegisterType<StagedProbeEvent>();

		await using var harness = new DrainHarness(
			maxAttempts: 1, perRunTotal: 2, consumerBatchSize: 2, parallelProcessingDegree: parallelProcessingDegree);

		await harness.Store.StageMessageAsync(
			new OutboundMessage
			{
				Id = "poison-row-mixed",
				MessageType = typeof(StagedProbeEvent).FullName!,
				Payload = Encoding.UTF8.GetBytes("{ this is not valid json"),
				CreatedAt = DateTimeOffset.UtcNow,
				RetryCount = 0,
			},
			CancellationToken.None).ConfigureAwait(false);

		await harness.Outbox.SaveEventsAsync(
			[new StagedProbeEvent("healthy-behind-the-poison")],
			CreateProducerMetadata(),
			CancellationToken.None).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => harness.Dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(m => m.GetType() == typeof(StagedProbeEvent) && ((StagedProbeEvent)m).Value == "healthy-behind-the-poison"),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		harness.CircuitBreaker.FailuresObserved.ShouldBe(0);
	}

	[Fact]
	public async Task DeliverFarMoreDistinctMessagesThanTheDrainsOwnQueueBound()
	{
		// The producer once fed every claimed id into a bounded set that nothing ever read, so a drain
		// stopped delivering permanently once it had seen QueueCapacity + ProducerBatchSize distinct ids —
		// and because the outbox host calls Init once, outside its polling loop, that state was never
		// reset. The bound is not a constant, so this asserts the property rather than a number: the
		// options below put it at 1,100 and the run pushes 5,200 through, far past it.
		//
		// A reintroduced bound of that shape does not fail an assertion, it BLOCKS — so the drain runs
		// under a token. If it stalls, the token fires, the drain returns short, and the count assertion
		// reports it instead of the run hanging.
		const int messageCount = 5200;
		MessageTypeRegistry.RegisterType<StagedProbeEvent>();

		await using var harness = new DrainHarness(
			maxAttempts: 1,
			perRunTotal: messageCount + 1000,
			consumerBatchSize: 100,
			queueCapacity: 1000,
			producerBatchSize: 100);

		var staged = new List<IIntegrationEvent>(messageCount);
		for (var i = 0; i < messageCount; i++)
		{
			staged.Add(new StagedProbeEvent($"bulk-{i}"));
		}

		await harness.Outbox.SaveEventsAsync(staged, CreateProducerMetadata(), CancellationToken.None)
			.ConfigureAwait(false);

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var processed = await harness.Processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		processed.ShouldBe(messageCount);
		A.CallTo(() => harness.Dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappened(messageCount, Times.Exactly);
	}

	private static ProducerMetadata CreateProducerMetadata() => new()
	{
		MessageId = Guid.NewGuid().ToString(),
		CorrelationId = "correlation-staged-probe",
		MessageType = typeof(StagedProbeEvent).FullName!,
		ContentType = "application/json",
		CreatedTimestampUtc = DateTimeOffset.UtcNow,
	};

	private sealed record StagedProbeEvent(string Value) : IIntegrationEvent;

	/// <summary>
	/// A real in-memory store with the real producer and the real hosted consumer bound to it, so a
	/// message written by one is read by the other with nothing in between shaping the bytes.
	/// </summary>
	private sealed class DrainHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _provider;

		public DrainHarness(
			int maxAttempts,
			int perRunTotal = 1,
			int consumerBatchSize = 1,
			int queueCapacity = 4,
			int producerBatchSize = 0,
			int parallelProcessingDegree = 1)
		{
			Dispatcher = A.Fake<IDispatcher>();
			_ = A.CallTo(() => Dispatcher.DispatchAsync(
					A<IDispatchMessage>._,
					A<IMessageContext>._,
					A<CancellationToken>._))
				.Returns(Task.FromResult<IMessageResult>(DispatchMessageResult.Success()));

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryOutboxStore();
			_ = services.AddScoped(_ => Dispatcher);
			_provider = services.BuildServiceProvider();

			// AddInMemoryOutboxStore registers the contract under the provider key, not unkeyed.
			Store = _provider.GetRequiredKeyedService<IOutboxStore>("inmemory");

			DeadLetterQueue = A.Fake<IDeadLetterQueue>();
			CircuitBreakerRegistry = new RecordingCircuitBreakerRegistry();

			var options = Options.Create(new DeliveryOutboxOptions
			{
				QueueCapacity = queueCapacity,
				ProducerBatchSize = producerBatchSize == 0 ? consumerBatchSize : producerBatchSize,
				ConsumerBatchSize = consumerBatchSize,
				PerRunTotal = perRunTotal,
				MaxAttempts = maxAttempts,
				// The processor selects its whole drain path on this number: at 1 it walks the
				// single-message loop, above 1 it walks the parallel batch path, and the two carry
				// SEPARATE poison-decode, circuit-breaker and dead-letter handling. Hardcoding it to 1
				// left every arm below exercising one of the two, so the parallel path's copy of this
				// logic was covered by nothing at all while the suite reported green.
				BatchProcessing = { ParallelProcessingDegree = parallelProcessingDegree },
			});

			var serializer = new DispatchJsonSerializer();

			Processor = new OutboxProcessor(
				options,
				Store,
				serializer,
				_provider,
				NullLogger<OutboxProcessor>.Instance,
				deadLetterQueue: DeadLetterQueue,
				circuitBreakerRegistry: CircuitBreakerRegistry);
			Processor.Init("dispatcher-staged-payload");

			Outbox = new MessageOutbox(
				Store,
				Processor,
				serializer,
				options,
				NullLogger<MessageOutbox>.Instance);
		}

		public IDispatcher Dispatcher { get; }

		public IOutboxStore Store { get; }

		public IDeadLetterQueue DeadLetterQueue { get; }

		public RecordingCircuitBreakerRegistry CircuitBreakerRegistry { get; }

		public RecordingCircuitBreaker CircuitBreaker => CircuitBreakerRegistry.Breaker;

		public OutboxProcessor Processor { get; }

		public MessageOutbox Outbox { get; }

		public async ValueTask DisposeAsync()
		{
			Outbox.Dispose();
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _provider.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Counts every failure signal the breaker receives, from either direction.
	/// </summary>
	/// <remarks>
	/// <see cref="ExecuteAsync"/> counts an escaping exception because the real
	/// <c>CircuitBreakerPolicy.ExecuteAsync</c> records one itself for anything its filter handles --
	/// counting only explicit <see cref="RecordFailure"/> calls would miss half of what the production
	/// breaker actually sees, and would read as a pass while the circuit still tripped.
	/// </remarks>
	private sealed class RecordingCircuitBreaker : ICircuitBreakerPolicy
	{
		private int _executionsEntered;
		private int _failuresObserved;

		public int ExecutionsEntered => Volatile.Read(ref _executionsEntered);

		public int FailuresObserved => Volatile.Read(ref _failuresObserved);

		public CircuitState State => CircuitState.Closed;

		public async Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(action);

			_ = Interlocked.Increment(ref _executionsEntered);

#pragma warning disable CA1031 // Counting every escaping exception is the point of this double.
			try
			{
				return await action(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				_ = Interlocked.Increment(ref _failuresObserved);
				throw;
			}
#pragma warning restore CA1031
		}

		public void RecordSuccess()
		{
		}

		public void RecordFailure(Exception? exception = null) => Interlocked.Increment(ref _failuresObserved);

		public void Reset()
		{
		}
	}

	private sealed class RecordingCircuitBreakerRegistry : ITransportCircuitBreakerRegistry
	{
		public RecordingCircuitBreaker Breaker { get; } = new();

		public ICircuitBreakerPolicy GetOrCreate(string transportName) => Breaker;

		public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options) => Breaker;

		public ICircuitBreakerPolicy? TryGet(string transportName) => Breaker;
	}
}
