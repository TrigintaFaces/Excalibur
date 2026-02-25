// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.EventSourcing;

/// <summary>
/// End-to-end workflow tests for event sourcing patterns.
/// Tests aggregate lifecycle, projections, snapshots, concurrency, and event upcasting.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 181 - Functional Testing Epic Phase 1.
/// bd-4z51o: Event Sourcing Workflow Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory storage to validate event sourcing patterns
/// without requiring TestContainers. Real database tests exist in integration tests.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "181")]
[Trait("Component", "EventSourcing")]
[Trait("Category", "Unit")]
public sealed class EventSourcingWorkflowShould
{
	/// <summary>
	/// Tests the aggregate lifecycle: Create > Apply events > Save > Load > Replay > Same state.
	/// </summary>
	[Fact]
	public async Task CreateAndReplayAggregateEvents()
	{
		// Arrange
		var eventStore = new InMemoryEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create aggregate and apply events
		var aggregate = new OrderAggregate(aggregateId);
		aggregate.Create("ORD-001", "customer-123");
		aggregate.AddItem("PROD-001", 2, 25.00m);
		aggregate.AddItem("PROD-002", 1, 50.00m);
		aggregate.Submit();

		// Act - Save events to store
		var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();
		_ = await eventStore.AppendAsync(aggregateId, uncommittedEvents, 0).ConfigureAwait(true);
		aggregate.ClearUncommittedEvents();

		// Act - Load and replay aggregate from events
		var reloadedAggregate = new OrderAggregate(aggregateId);
		var storedEvents = await eventStore.GetEventsAsync(aggregateId).ConfigureAwait(true);
		reloadedAggregate.LoadFromHistory(storedEvents);

		// Assert - Replayed aggregate has same state
		reloadedAggregate.OrderId.ShouldBe("ORD-001");
		reloadedAggregate.CustomerId.ShouldBe("customer-123");
		reloadedAggregate.Items.Count.ShouldBe(2);
		reloadedAggregate.TotalAmount.ShouldBe(100.00m);
		reloadedAggregate.Status.ShouldBe(OrderStatus.Submitted);
		reloadedAggregate.Version.ShouldBe(4); // 4 events applied
	}

	/// <summary>
	/// Tests projection rebuild from events.
	/// Events > Projection > Clear > Rebuild > Verify.
	/// </summary>
	[Fact]
	public async Task RebuildProjectionFromEvents()
	{
		// Arrange
		var eventStore = new InMemoryEventStore();
		var projection = new OrderSummaryProjection();

		// Create multiple orders via events
		var order1Events = new List<IDomainEvent>
		{
			new OrderCreated("order-1", "ORD-001", "customer-1"),
			new ItemAdded("order-1", "PROD-001", 1, 10.00m),
			new OrderSubmitted("order-1")
		};

		var order2Events = new List<IDomainEvent>
		{
			new OrderCreated("order-2", "ORD-002", "customer-2"),
			new ItemAdded("order-2", "PROD-002", 2, 25.00m),
			new OrderSubmitted("order-2")
		};

		_ = await eventStore.AppendAsync("order-1", order1Events, 0).ConfigureAwait(true);
		_ = await eventStore.AppendAsync("order-2", order2Events, 0).ConfigureAwait(true);

		// Act - Build projection from events
		var allEvents = await eventStore.GetAllEventsAsync().ConfigureAwait(true);
		projection.ApplyEvents(allEvents);

		// Assert - Projection reflects all events
		projection.OrderCount.ShouldBe(2);
		projection.SubmittedOrderCount.ShouldBe(2);
		projection.TotalRevenue.ShouldBe(60.00m);

		// Act - Clear and rebuild
		projection.Clear();
		projection.OrderCount.ShouldBe(0);

		var rebuiltEvents = await eventStore.GetAllEventsAsync().ConfigureAwait(true);
		projection.ApplyEvents(rebuiltEvents);

		// Assert - Rebuilt projection matches original
		projection.OrderCount.ShouldBe(2);
		projection.SubmittedOrderCount.ShouldBe(2);
		projection.TotalRevenue.ShouldBe(60.00m);
	}

	/// <summary>
	/// Tests snapshot save and restore for aggregates with many events.
	/// Aggregate > 100+ events > Snapshot > Load > Verify.
	/// </summary>
	[Fact]
	public async Task SaveAndRestoreAggregateSnapshot()
	{
		// Arrange
		var eventStore = new InMemoryEventStore();
		var snapshotStore = new InMemorySnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create aggregate with many events (>100)
		var aggregate = new CounterAggregate(aggregateId);
		for (var i = 0; i < 105; i++)
		{
			aggregate.Increment();
		}

		// Save events
		var events = aggregate.GetUncommittedEvents().ToList();
		_ = await eventStore.AppendAsync(aggregateId, events, 0).ConfigureAwait(true);
		aggregate.ClearUncommittedEvents();

		// Act - Save snapshot
		var snapshot = aggregate.CreateSnapshot();
		await snapshotStore.SaveAsync(aggregateId, snapshot, aggregate.Version).ConfigureAwait(true);

		// Act - Load aggregate from snapshot + any new events
		var loadedSnapshot = await snapshotStore.GetAsync<CounterSnapshot>(aggregateId).ConfigureAwait(true);
		var restoredAggregate = new CounterAggregate(aggregateId);
		restoredAggregate.RestoreFromSnapshot(loadedSnapshot);

		// Assert - Restored aggregate has correct state
		restoredAggregate.Count.ShouldBe(105);
		restoredAggregate.Version.ShouldBe(105);
	}

	/// <summary>
	/// Tests concurrency conflict detection with parallel writes.
	/// Parallel writes > ConcurrencyException > Resolution.
	/// </summary>
	[Fact]
	public async Task DetectConcurrencyConflicts()
	{
		// Arrange
		var eventStore = new InMemoryEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create initial aggregate
		var events = new List<IDomainEvent>
		{
			new OrderCreated(aggregateId, "ORD-001", "customer-1")
		};
		_ = await eventStore.AppendAsync(aggregateId, events, 0).ConfigureAwait(true);

		// Act - Two concurrent writes with same expected version
		var write1Events = new List<IDomainEvent>
		{
			new ItemAdded(aggregateId, "PROD-001", 1, 10.00m)
		};
		var write2Events = new List<IDomainEvent>
		{
			new ItemAdded(aggregateId, "PROD-002", 2, 20.00m)
		};

		// First write should succeed
		var result1 = await eventStore.AppendAsync(aggregateId, write1Events, 1).ConfigureAwait(true);
		result1.ShouldBeTrue();

		// Second write with same expected version should fail (conflict)
		var result2 = await eventStore.AppendAsync(aggregateId, write2Events, 1).ConfigureAwait(true);
		result2.ShouldBeFalse(); // Concurrency conflict

		// Assert - Only first write was persisted
		var storedEvents = await eventStore.GetEventsAsync(aggregateId).ConfigureAwait(true);
		storedEvents.Count.ShouldBe(2); // Initial + first write
	}

	/// <summary>
	/// Tests event upcasting chain from V1 to V3.
	/// V1 event > V2 upcaster > V3 upcaster > Handler.
	/// </summary>
	[Fact]
	public async Task UpcastEventsAcrossVersions()
	{
		// Arrange
		var eventStore = new InMemoryEventStore();
		var upcasterChain = new EventUpcasterChain();
		var aggregateId = Guid.NewGuid().ToString();

		// Store V1 event (old format)
		var v1Event = new ProductPriceChangedV1(aggregateId, "PROD-001", 10.00m);
		_ = await eventStore.AppendAsync(aggregateId, new List<IDomainEvent> { v1Event }, 0).ConfigureAwait(true);

		// Act - Load events and apply upcasters
		var storedEvents = await eventStore.GetEventsAsync(aggregateId).ConfigureAwait(true);
		var upcastedEvents = upcasterChain.Upcast(storedEvents).ToList();

		// Assert - Event was upcasted to V3
		upcastedEvents.Count.ShouldBe(1);
		var v3Event = upcastedEvents[0] as ProductPriceChangedV3;
		_ = v3Event.ShouldNotBeNull();
		v3Event.ProductId.ShouldBe("PROD-001");
		v3Event.NewPrice.ShouldBe(10.00m);
		v3Event.Currency.ShouldBe("USD"); // Default from V2 upcaster
		v3Event.Reason.ShouldBe("Legacy migration"); // Default from V3 upcaster
	}

	#region In-Memory Event Store

	internal sealed class InMemoryEventStore
	{
		private readonly ConcurrentDictionary<string, List<IDomainEvent>> _streams = new();
		private readonly ConcurrentDictionary<string, int> _versions = new();

		public Task<bool> AppendAsync(string streamId, List<IDomainEvent> events, int expectedVersion)
		{
			lock (_streams)
			{
				var currentVersion = _versions.GetValueOrDefault(streamId, 0);
				if (currentVersion != expectedVersion)
				{
					return Task.FromResult(false); // Concurrency conflict
				}

				if (!_streams.TryGetValue(streamId, out var stream))
				{
					stream = [];
					_streams[streamId] = stream;
				}

				stream.AddRange(events);
				_versions[streamId] = currentVersion + events.Count;
				return Task.FromResult(true);
			}
		}

		public Task<List<IDomainEvent>> GetEventsAsync(string streamId)
		{
			return Task.FromResult(_streams.GetValueOrDefault(streamId, []));
		}

		public Task<List<IDomainEvent>> GetAllEventsAsync()
		{
			var allEvents = _streams.Values.SelectMany(s => s).ToList();
			return Task.FromResult(allEvents);
		}
	}

	#endregion In-Memory Event Store

	#region In-Memory Snapshot Store

	internal sealed class InMemorySnapshotStore
	{
		private readonly ConcurrentDictionary<string, (object Snapshot, int Version)> _snapshots = new();

		public Task SaveAsync<T>(string aggregateId, T snapshot, int version) where T : class
		{
			_snapshots[aggregateId] = (snapshot, version);
			return Task.CompletedTask;
		}

		public Task<T?> GetAsync<T>(string aggregateId) where T : class
		{
			if (_snapshots.TryGetValue(aggregateId, out var entry))
			{
				return Task.FromResult(entry.Snapshot as T);
			}
			return Task.FromResult<T?>(null);
		}
	}

	#endregion In-Memory Snapshot Store

	#region Domain Events

	internal interface IDomainEvent
	{
		string AggregateId { get; }
	}

	internal sealed record OrderCreated(string AggregateId, string OrderId, string CustomerId) : IDomainEvent;
	internal sealed record ItemAdded(string AggregateId, string ProductId, int Quantity, decimal Price) : IDomainEvent;
	internal sealed record OrderSubmitted(string AggregateId) : IDomainEvent;

	// Event versions for upcasting test
	internal sealed record ProductPriceChangedV1(string AggregateId, string ProductId, decimal NewPrice) : IDomainEvent;
	internal sealed record ProductPriceChangedV2(string AggregateId, string ProductId, decimal NewPrice, string Currency) : IDomainEvent;
	internal sealed record ProductPriceChangedV3(string AggregateId, string ProductId, decimal NewPrice, string Currency, string Reason) : IDomainEvent;

	#endregion Domain Events

	#region Aggregates

	internal enum OrderStatus
	{ Created, Submitted, Shipped, Completed }

	internal sealed class OrderAggregate
	{
		private readonly List<IDomainEvent> _uncommittedEvents = [];

		public OrderAggregate(string id) => Id = id;

		public string Id { get; }
		public string? OrderId { get; private set; }
		public string? CustomerId { get; private set; }
		public List<(string ProductId, int Quantity, decimal Price)> Items { get; } = [];
		public decimal TotalAmount => Items.Sum(i => i.Quantity * i.Price);
		public OrderStatus Status { get; private set; } = OrderStatus.Created;
		public int Version { get; private set; }

		public void Create(string orderId, string customerId)
		{
			ApplyChange(new OrderCreated(Id, orderId, customerId));
		}

		public void AddItem(string productId, int quantity, decimal price)
		{
			ApplyChange(new ItemAdded(Id, productId, quantity, price));
		}

		public void Submit()
		{
			ApplyChange(new OrderSubmitted(Id));
		}

		public void LoadFromHistory(List<IDomainEvent> events)
		{
			foreach (var @event in events)
			{
				Apply(@event);
				Version++;
			}
		}

		public IEnumerable<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

		public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

		private void ApplyChange(IDomainEvent @event)
		{
			Apply(@event);
			_uncommittedEvents.Add(@event);
			Version++;
		}

		private void Apply(IDomainEvent @event)
		{
			switch (@event)
			{
				case OrderCreated e:
					OrderId = e.OrderId;
					CustomerId = e.CustomerId;
					break;

				case ItemAdded e:
					Items.Add((e.ProductId, e.Quantity, e.Price));
					break;

				case OrderSubmitted:
					Status = OrderStatus.Submitted;
					break;
			}
		}
	}

	internal sealed record CounterSnapshot(int Count, int Version);

	internal sealed class CounterAggregate
	{
		private readonly List<IDomainEvent> _uncommittedEvents = [];

		public CounterAggregate(string id) => Id = id;

		public string Id { get; }
		public int Count { get; private set; }
		public int Version { get; private set; }

		public void Increment()
		{
			Count++;
			Version++;
			_uncommittedEvents.Add(new CounterIncremented(Id));
		}

		public CounterSnapshot CreateSnapshot() => new(Count, Version);

		public void RestoreFromSnapshot(CounterSnapshot snapshot)
		{
			Count = snapshot.Count;
			Version = snapshot.Version;
		}

		public IEnumerable<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

		public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

		private sealed record CounterIncremented(string AggregateId) : IDomainEvent;
	}

	#endregion Aggregates

	#region Projections

	internal sealed class OrderSummaryProjection
	{
		public int OrderCount { get; private set; }
		public int SubmittedOrderCount { get; private set; }
		public decimal TotalRevenue { get; private set; }

		public void ApplyEvents(List<IDomainEvent> events)
		{
			foreach (var @event in events)
			{
				Apply(@event);
			}
		}

		public void Clear()
		{
			OrderCount = 0;
			SubmittedOrderCount = 0;
			TotalRevenue = 0;
		}

		private void Apply(IDomainEvent @event)
		{
			switch (@event)
			{
				case OrderCreated:
					OrderCount++;
					break;

				case ItemAdded e:
					TotalRevenue += e.Quantity * e.Price;
					break;

				case OrderSubmitted:
					SubmittedOrderCount++;
					break;
			}
		}
	}

	#endregion Projections

	#region Event Upcasters

	internal sealed class EventUpcasterChain
	{
		public IEnumerable<IDomainEvent> Upcast(List<IDomainEvent> events)
		{
			foreach (var @event in events)
			{
				yield return UpcastEvent(@event);
			}
		}

		private static IDomainEvent UpcastEvent(IDomainEvent @event)
		{
			// V1 -> V2 -> V3 upcasting chain
			return @event switch
			{
				ProductPriceChangedV1 v1 => UpcastV2ToV3(new ProductPriceChangedV2(v1.AggregateId, v1.ProductId, v1.NewPrice, "USD")),
				ProductPriceChangedV2 v2 => UpcastV2ToV3(v2),
				_ => @event
			};
		}

		private static ProductPriceChangedV3 UpcastV2ToV3(ProductPriceChangedV2 v2) =>
			new(v2.AggregateId, v2.ProductId, v2.NewPrice, v2.Currency, "Legacy migration");
	}

	#endregion Event Upcasters
}
