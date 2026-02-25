// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Snapshots.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Benchmarks.Scenarios;

/// <summary>
/// End-to-end scenario benchmarks that measure full framework overhead
/// by chaining multiple subsystems together using in-memory implementations.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks measure framework overhead, not I/O. All implementations are in-memory
/// to isolate the cost of the dispatch pipeline, event sourcing, outbox, and transport layers.
/// </para>
/// <list type="bullet">
/// <item>Scenario 1: Command dispatch through pipeline to outbox staging</item>
/// <item>Scenario 2: Full event sourcing read-modify-write cycle with snapshot</item>
/// <item>Scenario 3: Full pipeline with middleware, event store, outbox, and transport send</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class EndToEndScenarioBenchmarks
{
	// Scenario 1: Command dispatch to outbox
	private IServiceProvider _dispatchToOutboxProvider = null!;
	private IDispatcher _dispatchToOutboxDispatcher = null!;
	private IMessageContextFactory _dispatchToOutboxContextFactory = null!;
	private PlaceOrderCommand _placeOrderCommand = null!;

	// Scenario 2: Event sourcing full cycle
	private InMemoryEventStore _eventStore = null!;
	private InMemorySnapshotStore _snapshotStore = null!;
	private string _preloadedAggregateId = null!;

	// Scenario 3: Full pipeline with transport
	private IServiceProvider _fullPipelineProvider = null!;
	private IDispatcher _fullPipelineDispatcher = null!;
	private IMessageContextFactory _fullPipelineContextFactory = null!;
	private InMemoryTransportSender _transportSender = null!;
	private InMemoryEventStore _fullPipelineEventStore = null!;
	private InMemoryOutboxStore _fullPipelineOutboxStore = null!;
	private SubmitOrderCommand _submitOrderCommand = null!;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		SetupScenario1_CommandDispatchToOutbox();
		await SetupScenario2_EventSourcingFullCycleAsync();
		SetupScenario3_FullPipelineWithTransport();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		(_dispatchToOutboxProvider as IDisposable)?.Dispose();
		(_fullPipelineProvider as IDisposable)?.Dispose();
	}

	#region Scenario 1: Command Dispatch to Outbox

	private void SetupScenario1_CommandDispatchToOutbox()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.ClearProviders());

		// Register handler and outbox store
		_ = services.AddTransient<PlaceOrderCommandHandler>();
		_ = services.AddTransient<IActionHandler<PlaceOrderCommand>, PlaceOrderCommandHandler>();
		_ = services.AddSingleton<IOutboxStore>(sp =>
			new InMemoryOutboxStore(
				Microsoft.Extensions.Options.Options.Create(new InMemoryOutboxOptions()),
				sp.GetRequiredService<ILogger<InMemoryOutboxStore>>()));

		_ = services.AddDispatch();

		// Add 2 representative middleware (logging + validation simulation)
		_ = services.AddMiddleware<TimingMiddleware>();
		_ = services.AddMiddleware<ValidationSimulationMiddleware>();

		_dispatchToOutboxProvider = services.BuildServiceProvider();
		_dispatchToOutboxDispatcher = _dispatchToOutboxProvider.GetRequiredService<IDispatcher>();
		_dispatchToOutboxContextFactory = _dispatchToOutboxProvider.GetRequiredService<IMessageContextFactory>();

		_placeOrderCommand = new PlaceOrderCommand
		{
			OrderId = Guid.NewGuid(),
			CustomerId = "customer-bench-001",
			Amount = 99.99m,
		};
	}

	/// <summary>
	/// Scenario 1: Dispatch a command through the middleware pipeline into a handler
	/// that stages a message in the in-memory outbox.
	/// Measures: Dispatch + Middleware (2 layers) + Handler + Outbox stage.
	/// </summary>
	[Benchmark(Baseline = true, Description = "S1: Command -> Pipeline -> Outbox")]
	public async Task<IMessageResult> CommandDispatchToOutbox()
	{
		var context = _dispatchToOutboxContextFactory.CreateContext();
		return await _dispatchToOutboxDispatcher.DispatchAsync(_placeOrderCommand, context, CancellationToken.None);
	}

	/// <summary>
	/// Scenario 1 variant: 10 sequential command dispatches to outbox.
	/// Measures: Sustained throughput of the dispatch-to-outbox path.
	/// </summary>
	[Benchmark(Description = "S1: 10x Command -> Pipeline -> Outbox")]
	public async Task<int> CommandDispatchToOutbox_10Sequential()
	{
		var count = 0;
		for (var i = 0; i < 10; i++)
		{
			var context = _dispatchToOutboxContextFactory.CreateContext();
			var result = await _dispatchToOutboxDispatcher.DispatchAsync(_placeOrderCommand, context, CancellationToken.None);
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	#endregion

	#region Scenario 2: Event Sourcing Full Cycle

	private async Task SetupScenario2_EventSourcingFullCycleAsync()
	{
		_eventStore = new InMemoryEventStore();
		_snapshotStore = new InMemorySnapshotStore();

		// Pre-load an aggregate with 20 events for realistic load-modify-save benchmarks
		_preloadedAggregateId = Guid.NewGuid().ToString();
		var seedEvents = new List<BenchmarkOrderEvent>();
		for (var i = 1; i <= 20; i++)
		{
			seedEvents.Add(new BenchmarkOrderEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = _preloadedAggregateId,
				Version = i,
				OccurredAt = DateTimeOffset.UtcNow,
				EventType = "OrderItemAdded",
				Metadata = new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
				},
				ItemName = $"Item-{i}",
				Quantity = i,
			});
		}

		_ = await _eventStore.AppendAsync(
			_preloadedAggregateId,
			"BenchmarkOrder",
			seedEvents,
			expectedVersion: -1,
			CancellationToken.None);

		// Save a snapshot at version 20
		await _snapshotStore.SaveSnapshotAsync(
			new BenchmarkSnapshot
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId = _preloadedAggregateId,
				AggregateType = "BenchmarkOrder",
				Version = 20,
				CreatedAt = DateTimeOffset.UtcNow,
				Data = new byte[256], // Simulated serialized state
			},
			CancellationToken.None);
	}

	/// <summary>
	/// Scenario 2: Load aggregate events from in-memory event store, apply new events,
	/// append them, and save a snapshot.
	/// Measures: Load + Append + Snapshot save (full read-modify-write cycle).
	/// </summary>
	[Benchmark(Description = "S2: Load -> Append -> Snapshot")]
	public async Task EventSourcingFullCycle()
	{
		// 1. Load existing events
		var events = await _eventStore.LoadAsync(
			_preloadedAggregateId,
			"BenchmarkOrder",
			CancellationToken.None);

		// 2. Determine current version
		var currentVersion = events.Count > 0 ? events[^1].Version : -1;

		// 3. Create new events (simulate domain logic producing 3 new events)
		var newEvents = new BenchmarkOrderEvent[3];
		for (var i = 0; i < 3; i++)
		{
			newEvents[i] = new BenchmarkOrderEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = _preloadedAggregateId,
				Version = currentVersion + i + 1,
				OccurredAt = DateTimeOffset.UtcNow,
				EventType = "OrderItemAdded",
				Metadata = new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
				},
				ItemName = $"NewItem-{i}",
				Quantity = i + 1,
			};
		}

		// 4. Append new events with optimistic concurrency
		var appendResult = await _eventStore.AppendAsync(
			_preloadedAggregateId,
			"BenchmarkOrder",
			newEvents,
			expectedVersion: currentVersion,
			CancellationToken.None);

		// 5. Save updated snapshot
		await _snapshotStore.SaveSnapshotAsync(
			new BenchmarkSnapshot
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId = _preloadedAggregateId,
				AggregateType = "BenchmarkOrder",
				Version = appendResult.NextExpectedVersion,
				CreatedAt = DateTimeOffset.UtcNow,
				Data = new byte[256],
			},
			CancellationToken.None);
	}

	/// <summary>
	/// Scenario 2 variant: Load from snapshot, then load only new events since snapshot version.
	/// Measures: Snapshot load + partial event load (optimized hydration path).
	/// </summary>
	[Benchmark(Description = "S2: Snapshot + Partial Load")]
	public async Task EventSourcingSnapshotPartialLoad()
	{
		// 1. Load snapshot
		var snapshot = await _snapshotStore.GetLatestSnapshotAsync(
			_preloadedAggregateId,
			"BenchmarkOrder",
			CancellationToken.None);

		// 2. Load only events since snapshot version
		var fromVersion = snapshot?.Version ?? -1;
		_ = await _eventStore.LoadAsync(
			_preloadedAggregateId,
			"BenchmarkOrder",
			fromVersion,
			CancellationToken.None);
	}

	/// <summary>
	/// Scenario 2 variant: New aggregate creation (append to non-existent aggregate).
	/// Measures: First-write path with expectedVersion = -1.
	/// </summary>
	[Benchmark(Description = "S2: New Aggregate (5 events)")]
	public async Task<AppendResult> EventSourcingNewAggregate()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = new BenchmarkOrderEvent[5];
		for (var i = 0; i < 5; i++)
		{
			events[i] = new BenchmarkOrderEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = i + 1,
				OccurredAt = DateTimeOffset.UtcNow,
				EventType = "OrderCreated",
				Metadata = new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
				},
				ItemName = $"Item-{i}",
				Quantity = 1,
			};
		}

		return await _eventStore.AppendAsync(
			aggregateId,
			"BenchmarkOrder",
			events,
			expectedVersion: -1,
			CancellationToken.None);
	}

	#endregion

	#region Scenario 3: Full Pipeline with Transport

	private void SetupScenario3_FullPipelineWithTransport()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.ClearProviders());

		// In-memory event store
		_fullPipelineEventStore = new InMemoryEventStore();
		_ = services.AddSingleton<IEventStore>(_fullPipelineEventStore);

		// In-memory outbox store
		_fullPipelineOutboxStore = new InMemoryOutboxStore(
			Microsoft.Extensions.Options.Options.Create(new InMemoryOutboxOptions()),
			NullLogger<InMemoryOutboxStore>.Instance);
		_ = services.AddSingleton<IOutboxStore>(_fullPipelineOutboxStore);

		// In-memory transport sender
		_transportSender = new InMemoryTransportSender("benchmark-topic");
		_ = services.AddSingleton<ITransportSender>(_transportSender);

		// Register handler
		_ = services.AddTransient<FullPipelineCommandHandler>();
		_ = services.AddTransient<IActionHandler<SubmitOrderCommand>, FullPipelineCommandHandler>();

		_ = services.AddDispatch();

		// Add middleware pipeline: validation -> auth simulation -> timing
		_ = services.AddMiddleware<ValidationSimulationMiddleware>();
		_ = services.AddMiddleware<AuthSimulationMiddleware>();
		_ = services.AddMiddleware<TimingMiddleware>();

		_fullPipelineProvider = services.BuildServiceProvider();
		_fullPipelineDispatcher = _fullPipelineProvider.GetRequiredService<IDispatcher>();
		_fullPipelineContextFactory = _fullPipelineProvider.GetRequiredService<IMessageContextFactory>();

		_submitOrderCommand = new SubmitOrderCommand
		{
			OrderId = Guid.NewGuid(),
			CustomerId = "customer-bench-001",
			Amount = 149.99m,
			Items = 3,
		};
	}

	/// <summary>
	/// Scenario 3: Full end-to-end pipeline.
	/// Middleware (3 layers) -> Handler -> Event store append -> Outbox stage -> Transport send.
	/// Measures: Complete end-to-end latency through all framework layers.
	/// </summary>
	[Benchmark(Description = "S3: Pipeline -> EventStore -> Outbox -> Transport")]
	public async Task<IMessageResult> FullPipelineWithTransport()
	{
		var context = _fullPipelineContextFactory.CreateContext();
		return await _fullPipelineDispatcher.DispatchAsync(_submitOrderCommand, context, CancellationToken.None);
	}

	/// <summary>
	/// Scenario 3 variant: 10 sequential full-pipeline dispatches.
	/// Measures: Sustained throughput of the complete end-to-end path.
	/// </summary>
	[Benchmark(Description = "S3: 10x Full Pipeline")]
	public async Task<int> FullPipelineWithTransport_10Sequential()
	{
		var count = 0;
		for (var i = 0; i < 10; i++)
		{
			var context = _fullPipelineContextFactory.CreateContext();
			var result = await _fullPipelineDispatcher.DispatchAsync(_submitOrderCommand, context, CancellationToken.None);
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Scenario 3 variant: 10 concurrent full-pipeline dispatches.
	/// Measures: Concurrent throughput through the complete end-to-end path.
	/// </summary>
	[Benchmark(Description = "S3: 10x Concurrent Full Pipeline")]
	public async Task<int> FullPipelineWithTransport_10Concurrent()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _fullPipelineContextFactory.CreateContext();
			tasks[i] = _fullPipelineDispatcher.DispatchAsync(_submitOrderCommand, context, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks);
		var count = 0;
		foreach (var result in results)
		{
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	#endregion

	#region Test Types

	// -- Scenario 1 Types --

	private sealed record PlaceOrderCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	private sealed class PlaceOrderCommandHandler : IActionHandler<PlaceOrderCommand>
	{
		private readonly IOutboxStore _outboxStore;

		public PlaceOrderCommandHandler(IOutboxStore outboxStore)
		{
			_outboxStore = outboxStore;
		}

		public async Task HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
		{
			// Simulate handler work: stage an outbox message
			var message = new OutboundMessage(
				"OrderPlaced",
				new byte[64],
				"orders-topic",
				new Dictionary<string, object>
				{
					["OrderId"] = command.OrderId.ToString(),
					["CustomerId"] = command.CustomerId,
				});

			await _outboxStore.StageMessageAsync(message, cancellationToken);
		}
	}

	// -- Scenario 2 Types --

	private sealed class BenchmarkOrderEvent : IDomainEvent
	{
		public required string EventId { get; init; }
		public required string AggregateId { get; init; }
		public required long Version { get; init; }
		public required DateTimeOffset OccurredAt { get; init; }
		public required string EventType { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
		public required string ItemName { get; init; }
		public required int Quantity { get; init; }
	}

	private sealed class BenchmarkSnapshot : ISnapshot
	{
		public required string SnapshotId { get; init; }
		public required string AggregateId { get; init; }
		public required long Version { get; init; }
		public required DateTimeOffset CreatedAt { get; init; }
		public required byte[] Data { get; init; }
		public required string AggregateType { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}

	// -- Scenario 3 Types --

	private sealed record SubmitOrderCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public int Items { get; init; }
	}

	private sealed class FullPipelineCommandHandler : IActionHandler<SubmitOrderCommand>
	{
		private readonly IEventStore _eventStore;
		private readonly IOutboxStore _outboxStore;
		private readonly ITransportSender _transportSender;

		public FullPipelineCommandHandler(
			IEventStore eventStore,
			IOutboxStore outboxStore,
			ITransportSender transportSender)
		{
			_eventStore = eventStore;
			_outboxStore = outboxStore;
			_transportSender = transportSender;
		}

		public async Task HandleAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
		{
			// 1. Append domain event to event store
			var domainEvent = new BenchmarkOrderEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = command.OrderId.ToString(),
				Version = 1,
				OccurredAt = DateTimeOffset.UtcNow,
				EventType = "OrderSubmitted",
				Metadata = new Dictionary<string, object>
				{
					["CustomerId"] = command.CustomerId,
				},
				ItemName = "FullPipelineOrder",
				Quantity = command.Items,
			};

			_ = await _eventStore.AppendAsync(
				command.OrderId.ToString(),
				"Order",
				[domainEvent],
				expectedVersion: -1,
				cancellationToken);

			// 2. Stage outbox message
			var outboxMessage = new OutboundMessage(
				"OrderSubmitted",
				new byte[64],
				"orders-topic",
				new Dictionary<string, object>
				{
					["OrderId"] = command.OrderId.ToString(),
				});

			await _outboxStore.StageMessageAsync(outboxMessage, cancellationToken);

			// 3. Send via transport
			var transportMessage = new TransportMessage
			{
				Body = new byte[64],
				ContentType = "application/json",
				CorrelationId = command.OrderId.ToString(),
				Subject = "order.submitted",
				MessageType = "OrderSubmitted",
			};

			_ = await _transportSender.SendAsync(transportMessage, cancellationToken);
		}
	}

	// -- Middleware Types --

	private sealed class TimingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal timing simulation — access type name (common in real logging middleware)
			_ = message.GetType().Name;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ValidationSimulationMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal validation simulation — null check (common in real validation middleware)
			ArgumentNullException.ThrowIfNull(message);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class AuthSimulationMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal auth simulation — always authorize (common in real auth middleware)
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion
}
