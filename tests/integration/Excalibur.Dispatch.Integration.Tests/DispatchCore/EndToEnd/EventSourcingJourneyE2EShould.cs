// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - acceptable in test classes
#pragma warning disable IL2026 // RequiresUnreferencedCode -- test code, not AOT-published

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// End-to-end event sourcing journey tests proving CQRS/ES works:
/// create aggregate -> append events -> load -> snapshot -> reload from snapshot -> project read model.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EndToEnd")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcingJourneyE2EShould : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IEventSourcedRepository<TestOrderAggregate, string> _repository;
	private readonly IEventStore _eventStore;
	private readonly ISnapshotStore _snapshotStore;

	public EventSourcingJourneyE2EShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Use JSON serializer (MemoryPack requires explicit type registration per type)
		_ = services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

		_ = services.AddExcaliburEventSourcing(builder =>
			builder.AddRepository<TestOrderAggregate, string>(_ => new TestOrderAggregate()));

		_ = services.AddInMemoryEventStore();
		_ = services.AddInMemorySnapshotStore();

		_serviceProvider = services.BuildServiceProvider();
		_repository = _serviceProvider.GetRequiredService<IEventSourcedRepository<TestOrderAggregate, string>>();
		_eventStore = _serviceProvider.GetRequiredKeyedService<IEventStore>("default");
		_snapshotStore = _serviceProvider.GetRequiredKeyedService<ISnapshotStore>("default");
	}

	public async ValueTask DisposeAsync()
	{
		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void AssembleEventSourcingComponents()
	{
		_repository.ShouldNotBeNull();
		_eventStore.ShouldNotBeNull();
		_snapshotStore.ShouldNotBeNull();
	}

	[Fact]
	public async Task CreateAggregate_AppendEvents_PersistsToEventStore()
	{
		// Arrange
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-1", 100.00m);
		aggregate.AddItem("item-A", 2);
		aggregate.AddItem("item-B", 3);

		// Act
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Assert - events persisted
		var stored = await _eventStore.LoadAsync(orderId, nameof(TestOrderAggregate), CancellationToken.None);
		stored.Count.ShouldBe(3);
		stored[0].EventType.ShouldContain("TestOrderCreated");
		stored[1].EventType.ShouldContain("TestOrderItemAdded");
		stored[2].EventType.ShouldContain("TestOrderItemAdded");
	}

	[Fact]
	public async Task LoadAggregate_RestoresStateFromEvents()
	{
		// Arrange
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-2", 250.00m);
		aggregate.AddItem("widget", 5);
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Act
		var loaded = await _repository.GetByIdAsync(orderId, CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Id.ShouldBe(orderId);
		loaded.CustomerId.ShouldBe("cust-2");
		loaded.Total.ShouldBe(250.00m);
		loaded.ItemCount.ShouldBe(5);
		loaded.Version.ShouldBe(2); // 2 events = version 2
	}

	[Fact]
	public async Task CreateSnapshot_SaveAndReload()
	{
		// Arrange
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-snap", 500.00m);
		aggregate.AddItem("gear", 10);
		aggregate.Ship();
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Load to get committed state
		var loaded = await _repository.GetByIdAsync(orderId, CancellationToken.None);
		loaded.ShouldNotBeNull();

		// Act - Create and save snapshot
		var snapshot = loaded.CreateSnapshot();
		await _snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert - Retrieve snapshot
		var retrieved = await _snapshotStore.GetLatestSnapshotAsync(
			orderId, nameof(TestOrderAggregate), CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.AggregateId.ShouldBe(orderId);
		retrieved.Version.ShouldBe(loaded.Version);
	}

	[Fact]
	public async Task ReloadFromSnapshot_RestoresState()
	{
		// Arrange - Create and save aggregate with multiple events
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-reload", 300.00m);
		aggregate.AddItem("part-1", 4);
		aggregate.AddItem("part-2", 6);
		aggregate.Ship();
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Save snapshot at current version
		var loaded = await _repository.GetByIdAsync(orderId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		var snapshot = loaded.CreateSnapshot();
		await _snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Act - Restore a fresh aggregate from snapshot
		var restored = new TestOrderAggregate();
		restored.LoadFromSnapshot(snapshot);

		// Assert
		restored.Id.ShouldBe(orderId);
		restored.CustomerId.ShouldBe("cust-reload");
		restored.Total.ShouldBe(300.00m);
		restored.ItemCount.ShouldBe(10);
		restored.IsShipped.ShouldBeTrue();
	}

	[Fact]
	public async Task ProjectReadModel_FromEventStream()
	{
		// Arrange - Create an aggregate with events
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-proj", 150.00m);
		aggregate.AddItem("product-A", 3);
		aggregate.AddItem("product-B", 7);
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Act - Load raw events and project a read model
		var events = await _eventStore.LoadAsync(orderId, nameof(TestOrderAggregate), CancellationToken.None);
		var readModel = new OrderReadModel();
		foreach (var storedEvent in events)
		{
			readModel.Apply(storedEvent);
		}

		// Assert
		readModel.OrderId.ShouldBe(orderId);
		readModel.CustomerId.ShouldBe("cust-proj");
		readModel.Total.ShouldBe(150.00m);
		readModel.TotalItems.ShouldBe(10);
		readModel.EventCount.ShouldBe(3);
	}

	[Fact]
	public async Task MultipleCommits_VersionIncrementsCorrectly()
	{
		// Arrange
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-multi", 50.00m);
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Act - Reload, add more events, save again
		var loaded = await _repository.GetByIdAsync(orderId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.AddItem("extra-1", 1);
		loaded.AddItem("extra-2", 2);
		await _repository.SaveAsync(loaded, CancellationToken.None);

		// Assert
		var final = await _repository.GetByIdAsync(orderId, CancellationToken.None);
		final.ShouldNotBeNull();
		final.Version.ShouldBe(3); // 1 create + 2 add items
		final.ItemCount.ShouldBe(3);

		var allEvents = await _eventStore.LoadAsync(orderId, nameof(TestOrderAggregate), CancellationToken.None);
		allEvents.Count.ShouldBe(3);
	}

	[Fact]
	public async Task LoadNonExistentAggregate_ReturnsNull()
	{
		var result = await _repository.GetByIdAsync("does-not-exist", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ExistsCheck_ReturnsTrueForSavedAggregate()
	{
		// Arrange
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "cust-exist", 1.00m);
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Act & Assert
		(await _repository.ExistsAsync(orderId, CancellationToken.None)).ShouldBeTrue();
		(await _repository.ExistsAsync("ghost-order", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task FullJourney_CreateToSnapshotToProjection()
	{
		// Complete journey: create -> events -> save -> load -> snapshot -> reload -> project

		// Step 1: Create aggregate and raise events
		var orderId = $"order-{Guid.NewGuid():N}";
		var aggregate = new TestOrderAggregate();
		aggregate.Create(orderId, "journey-customer", 999.99m);
		aggregate.AddItem("premium-widget", 5);
		aggregate.AddItem("standard-widget", 10);

		// Step 2: Save to event store via repository
		await _repository.SaveAsync(aggregate, CancellationToken.None);

		// Step 3: Load from event store - verify full state restored
		var loaded = await _repository.GetByIdAsync(orderId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Id.ShouldBe(orderId);
		loaded.CustomerId.ShouldBe("journey-customer");
		loaded.Total.ShouldBe(999.99m);
		loaded.ItemCount.ShouldBe(15);
		loaded.Version.ShouldBe(3);

		// Step 4: Create and save snapshot
		var snapshot = loaded.CreateSnapshot();
		await _snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Step 5: Reload from snapshot - verify state matches
		var fromSnapshot = new TestOrderAggregate();
		fromSnapshot.LoadFromSnapshot(snapshot);
		fromSnapshot.Id.ShouldBe(loaded.Id);
		fromSnapshot.CustomerId.ShouldBe(loaded.CustomerId);
		fromSnapshot.Total.ShouldBe(loaded.Total);
		fromSnapshot.ItemCount.ShouldBe(loaded.ItemCount);

		// Step 6: Project read model from raw event stream
		var events = await _eventStore.LoadAsync(orderId, nameof(TestOrderAggregate), CancellationToken.None);
		events.Count.ShouldBe(3);

		var readModel = new OrderReadModel();
		foreach (var e in events)
		{
			readModel.Apply(e);
		}

		readModel.OrderId.ShouldBe(orderId);
		readModel.CustomerId.ShouldBe("journey-customer");
		readModel.Total.ShouldBe(999.99m);
		readModel.TotalItems.ShouldBe(15);
		readModel.EventCount.ShouldBe(3);
	}

	// ── Test Domain Events ─────────────────────────────────────────────

	public record TestOrderCreated(string OrderId, string CustomerId, decimal Total) : DomainEvent
	{
		public override string AggregateId => OrderId;
	}

	public record TestOrderItemAdded(string OrderId, string ItemId, int Quantity) : DomainEvent
	{
		public override string AggregateId => OrderId;
	}

	public record TestOrderShipped(string OrderId, DateTimeOffset ShippedAt) : DomainEvent
	{
		public override string AggregateId => OrderId;
	}

	// ── Test Snapshot ──────────────────────────────────────────────────

	public record TestOrderSnapshot : ISnapshot
	{
		public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public string AggregateType { get; init; } = nameof(TestOrderAggregate);
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public byte[] Data { get; init; } = [];
		public IDictionary<string, object>? Metadata { get; init; }

		// Domain state
		public string CustomerId { get; init; } = string.Empty;
		public decimal Total { get; init; }
		public int ItemCount { get; init; }
		public bool IsShipped { get; init; }
	}

	// ── Test Aggregate ─────────────────────────────────────────────────

	public sealed class TestOrderAggregate : AggregateRoot
	{
		public string CustomerId { get; private set; } = string.Empty;
		public decimal Total { get; private set; }
		public int ItemCount { get; private set; }
		public bool IsShipped { get; private set; }

		public TestOrderAggregate() { }

		public void Create(string orderId, string customerId, decimal total)
		{
			RaiseEvent(new TestOrderCreated(orderId, customerId, total));
		}

		public void AddItem(string itemId, int quantity)
		{
			RaiseEvent(new TestOrderItemAdded(Id, itemId, quantity));
		}

		public void Ship()
		{
			RaiseEvent(new TestOrderShipped(Id, DateTimeOffset.UtcNow));
		}

		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			TestOrderCreated e => ApplyCreated(e),
			TestOrderItemAdded e => ApplyItemAdded(e),
			TestOrderShipped e => ApplyShipped(e),
			_ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}"),
		};

		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			if (snapshot is TestOrderSnapshot s)
			{
				Id = s.AggregateId;
				CustomerId = s.CustomerId;
				Total = s.Total;
				ItemCount = s.ItemCount;
				IsShipped = s.IsShipped;
			}
		}

		public override ISnapshot CreateSnapshot() => new TestOrderSnapshot
		{
			AggregateId = Id,
			Version = Version,
			CustomerId = CustomerId,
			Total = Total,
			ItemCount = ItemCount,
			IsShipped = IsShipped,
		};

		private bool ApplyCreated(TestOrderCreated e)
		{
			Id = e.OrderId;
			CustomerId = e.CustomerId;
			Total = e.Total;
			return true;
		}

		private bool ApplyItemAdded(TestOrderItemAdded e)
		{
			ItemCount += e.Quantity;
			return true;
		}

		private bool ApplyShipped(TestOrderShipped _)
		{
			IsShipped = true;
			return true;
		}
	}

	// ── Read Model Projection ──────────────────────────────────────────

	/// <summary>
	/// Simple read model projected from the event stream.
	/// </summary>
	public sealed class OrderReadModel
	{
		public string OrderId { get; private set; } = string.Empty;
		public string CustomerId { get; private set; } = string.Empty;
		public decimal Total { get; private set; }
		public int TotalItems { get; private set; }
		public int EventCount { get; private set; }

		public void Apply(StoredEvent storedEvent)
		{
			EventCount++;

			// JsonEventSerializer uses camelCase naming policy
			var jsonOpts = new System.Text.Json.JsonDocumentOptions();

			if (storedEvent.EventType.Contains("TestOrderCreated", StringComparison.Ordinal))
			{
				OrderId = storedEvent.AggregateId;
				var json = System.Text.Json.JsonDocument.Parse(storedEvent.EventData, jsonOpts);
				if (json.RootElement.TryGetProperty("customerId", out var custId))
				{
					CustomerId = custId.GetString() ?? string.Empty;
				}

				if (json.RootElement.TryGetProperty("total", out var total))
				{
					Total = total.GetDecimal();
				}
			}
			else if (storedEvent.EventType.Contains("TestOrderItemAdded", StringComparison.Ordinal))
			{
				var json = System.Text.Json.JsonDocument.Parse(storedEvent.EventData, jsonOpts);
				if (json.RootElement.TryGetProperty("quantity", out var qty))
				{
					TotalItems += qty.GetInt32();
				}
			}
		}
	}
}
