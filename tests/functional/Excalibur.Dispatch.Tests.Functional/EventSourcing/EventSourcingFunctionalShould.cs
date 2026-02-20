// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.EventSourcing;

/// <summary>
/// End-to-end functional tests for event sourcing patterns including aggregates and event stores.
/// Tests demonstrate event sourcing concepts in isolation without framework dependencies.
/// </summary>
[Trait("Category", "Functional")]
public sealed class EventSourcingFunctionalShould : FunctionalTestBase
{
	#region Aggregate Tests

	[Fact]
	public void Aggregate_AppliesEventsCorrectly()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var aggregate = new OrderAggregate(aggregateId);

		// Act
		aggregate.CreateOrder("customer-123", "ORD-001");
		aggregate.AddItem("PROD-001", "Widget", 2, 9.99m);
		aggregate.AddItem("PROD-002", "Gadget", 1, 19.99m);

		// Assert
		aggregate.Id.ShouldBe(aggregateId);
		aggregate.CustomerId.ShouldBe("customer-123");
		aggregate.OrderNumber.ShouldBe("ORD-001");
		aggregate.Items.Count.ShouldBe(2);
		aggregate.TotalAmount.ShouldBe(39.97m);
	}

	[Fact]
	public void Aggregate_TracksUncommittedEvents()
	{
		// Arrange
		var aggregate = new OrderAggregate(Guid.NewGuid());

		// Act
		aggregate.CreateOrder("customer-456", "ORD-002");
		aggregate.AddItem("PROD-003", "Item", 1, 10.00m);
		var uncommittedEvents = aggregate.GetUncommittedEvents().ToList();

		// Assert
		uncommittedEvents.Count.ShouldBe(2);
		_ = uncommittedEvents[0].ShouldBeOfType<OrderCreatedEvent>();
		_ = uncommittedEvents[1].ShouldBeOfType<OrderItemAddedEvent>();
	}

	[Fact]
	public void Aggregate_ClearsEventsAfterCommit()
	{
		// Arrange
		var aggregate = new OrderAggregate(Guid.NewGuid());
		aggregate.CreateOrder("customer-789", "ORD-003");

		// Act
		aggregate.ClearUncommittedEvents();
		var events = aggregate.GetUncommittedEvents();

		// Assert
		events.ShouldBeEmpty();
	}

	[Fact]
	public void Aggregate_RehydratesFromEvents()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var events = new List<ITestDomainEvent>
		{
			new OrderCreatedEvent(aggregateId, "customer-abc", "ORD-004"),
			new OrderItemAddedEvent(aggregateId, "PROD-010", "Test Product", 3, 15.00m)
		};

		// Act
		var aggregate = new OrderAggregate(aggregateId);
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.CustomerId.ShouldBe("customer-abc");
		aggregate.OrderNumber.ShouldBe("ORD-004");
		aggregate.Items.Count.ShouldBe(1);
		aggregate.TotalAmount.ShouldBe(45.00m);
	}

	[Fact]
	public void Aggregate_TracksVersion()
	{
		// Arrange
		var aggregateId = Guid.NewGuid();
		var events = new List<ITestDomainEvent>
		{
			new OrderCreatedEvent(aggregateId, "cust", "ORD"),
			new OrderItemAddedEvent(aggregateId, "P1", "Product 1", 1, 10.00m),
			new OrderItemAddedEvent(aggregateId, "P2", "Product 2", 1, 20.00m)
		};

		// Act
		var aggregate = new OrderAggregate(aggregateId);
		aggregate.LoadFromHistory(events);

		// Assert
		aggregate.Version.ShouldBe(3);
	}

	#endregion

	#region Event Store Tests

	[Fact]
	public async Task InMemoryEventStore_PersistsAndLoadsEvents()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryEventStore>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var eventStore = host.Services.GetRequiredService<InMemoryEventStore>();
		var aggregateId = Guid.NewGuid();
		var events = new List<ITestDomainEvent>
		{
			new OrderCreatedEvent(aggregateId, "cust-100", "ORD-100"),
			new OrderItemAddedEvent(aggregateId, "P-001", "Product 1", 1, 25.00m)
		};

		// Act
		await eventStore.SaveEventsAsync(aggregateId, events, 0).ConfigureAwait(false);
		var loadedEvents = await eventStore.GetEventsAsync(aggregateId).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		loadedEvents.Count.ShouldBe(2);
		_ = loadedEvents[0].ShouldBeOfType<OrderCreatedEvent>();
		_ = loadedEvents[1].ShouldBeOfType<OrderItemAddedEvent>();
	}

	[Fact]
	public async Task InMemoryEventStore_DetectsConcurrencyConflict()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryEventStore>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var eventStore = host.Services.GetRequiredService<InMemoryEventStore>();
		var aggregateId = Guid.NewGuid();

		// First save
		var events1 = new List<ITestDomainEvent>
		{
			new OrderCreatedEvent(aggregateId, "cust-200", "ORD-200")
		};
		await eventStore.SaveEventsAsync(aggregateId, events1, 0).ConfigureAwait(false);

		// Act - Attempt to save with wrong expected version
		var events2 = new List<ITestDomainEvent>
		{
			new OrderItemAddedEvent(aggregateId, "P-002", "Product 2", 1, 30.00m)
		};

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = await Should.ThrowAsync<ConcurrencyException>(
			() => eventStore.SaveEventsAsync(aggregateId, events2, 0)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryEventStore_ReturnsEmptyForNonexistentAggregate()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryEventStore>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var eventStore = host.Services.GetRequiredService<InMemoryEventStore>();

		// Act
		var events = await eventStore.GetEventsAsync(Guid.NewGuid()).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		events.ShouldBeEmpty();
	}

	#endregion

	#region Full Event Sourcing Flow

	[Fact]
	public async Task EventSourcingFlow_CompleteWorkflow()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryEventStore>();
			_ = services.AddSingleton<OrderRepository>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var repository = host.Services.GetRequiredService<OrderRepository>();
		var aggregateId = Guid.NewGuid();

		// Act - Create and modify aggregate
		var order = new OrderAggregate(aggregateId);
		order.CreateOrder("CUST-E2E", "ORD-E2E-001");
		order.AddItem("SKU-001", "E2E Product 1", 2, 29.99m);
		order.AddItem("SKU-002", "E2E Product 2", 1, 49.99m);

		await repository.SaveAsync(order).ConfigureAwait(false);

		// Reload aggregate from store
		var reloadedOrder = await repository.GetByIdAsync(aggregateId).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = reloadedOrder.ShouldNotBeNull();
		reloadedOrder.CustomerId.ShouldBe("CUST-E2E");
		reloadedOrder.Items.Count.ShouldBe(2);
		reloadedOrder.TotalAmount.ShouldBe(109.97m);
	}

	[Fact]
	public async Task EventSourcingFlow_SupportsMultipleAggregates()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryEventStore>();
			_ = services.AddSingleton<OrderRepository>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var repository = host.Services.GetRequiredService<OrderRepository>();

		// Act - Create multiple aggregates
		var order1 = new OrderAggregate(Guid.NewGuid());
		order1.CreateOrder("CUST-1", "ORD-001");
		order1.AddItem("SKU-A", "Product A", 1, 10.00m);

		var order2 = new OrderAggregate(Guid.NewGuid());
		order2.CreateOrder("CUST-2", "ORD-002");
		order2.AddItem("SKU-B", "Product B", 2, 20.00m);

		await repository.SaveAsync(order1).ConfigureAwait(false);
		await repository.SaveAsync(order2).ConfigureAwait(false);

		// Reload
		var loaded1 = await repository.GetByIdAsync(order1.Id).ConfigureAwait(false);
		var loaded2 = await repository.GetByIdAsync(order2.Id).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - Each aggregate maintains independent state
		_ = loaded1.ShouldNotBeNull();
		loaded1.CustomerId.ShouldBe("CUST-1");
		loaded1.TotalAmount.ShouldBe(10.00m);

		_ = loaded2.ShouldNotBeNull();
		loaded2.CustomerId.ShouldBe("CUST-2");
		loaded2.TotalAmount.ShouldBe(40.00m);
	}

	#endregion

	#region Test Domain Model

	private interface ITestDomainEvent
	{
		Guid AggregateId { get; }
		string EventType { get; }
		DateTimeOffset OccurredAt { get; }
	}

	private sealed class OrderAggregate
	{
		private readonly List<ITestDomainEvent> _uncommittedEvents = [];

		public Guid Id { get; private set; }
		public string? CustomerId { get; private set; }
		public string? OrderNumber { get; private set; }
		public List<OrderItem> Items { get; } = [];
		public decimal TotalAmount { get; private set; }
		public int Version { get; private set; }

		public OrderAggregate(Guid id)
		{
			Id = id;
		}

		public void CreateOrder(string customerId, string orderNumber)
		{
			var evt = new OrderCreatedEvent(Id, customerId, orderNumber);
			Apply(evt);
			_uncommittedEvents.Add(evt);
		}

		public void AddItem(string productId, string productName, int quantity, decimal price)
		{
			var evt = new OrderItemAddedEvent(Id, productId, productName, quantity, price);
			Apply(evt);
			_uncommittedEvents.Add(evt);
		}

		public IEnumerable<ITestDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

		public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

		public void LoadFromHistory(IEnumerable<ITestDomainEvent> events)
		{
			foreach (var evt in events)
			{
				Apply(evt);
				Version++;
			}
		}

		private void Apply(ITestDomainEvent evt)
		{
			switch (evt)
			{
				case OrderCreatedEvent e:
					CustomerId = e.CustomerId;
					OrderNumber = e.OrderNumber;
					break;
				case OrderItemAddedEvent e:
					Items.Add(new OrderItem(e.ProductId, e.ProductName, e.Quantity, e.Price));
					TotalAmount += e.Quantity * e.Price;
					break;
			}
		}
	}

	private sealed record OrderItem(string ProductId, string ProductName, int Quantity, decimal Price);

	private sealed record OrderCreatedEvent(Guid AggregateId, string CustomerId, string OrderNumber) : ITestDomainEvent
	{
		public string EventType => nameof(OrderCreatedEvent);
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	}

	private sealed record OrderItemAddedEvent(Guid AggregateId, string ProductId, string ProductName, int Quantity, decimal Price) : ITestDomainEvent
	{
		public string EventType => nameof(OrderItemAddedEvent);
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	}

	private sealed class ConcurrencyException : Exception
	{
		public ConcurrencyException(string message) : base(message)
		{
		}
	}

	private sealed class InMemoryEventStore
	{
		private readonly Dictionary<Guid, List<ITestDomainEvent>> _eventStreams = [];
		private readonly Dictionary<Guid, int> _versions = [];

		public Task SaveEventsAsync(Guid aggregateId, IList<ITestDomainEvent> events, int expectedVersion)
		{
			var currentVersion = _versions.GetValueOrDefault(aggregateId, 0);

			if (currentVersion != expectedVersion)
			{
				throw new ConcurrencyException($"Expected version {expectedVersion} but found {currentVersion}");
			}

			if (!_eventStreams.TryGetValue(aggregateId, out var stream))
			{
				stream = [];
				_eventStreams[aggregateId] = stream;
			}

			stream.AddRange(events);
			_versions[aggregateId] = currentVersion + events.Count;

			return Task.CompletedTask;
		}

		public Task<List<ITestDomainEvent>> GetEventsAsync(Guid aggregateId)
		{
			var events = _eventStreams.GetValueOrDefault(aggregateId, []);
			return Task.FromResult(events.ToList());
		}

		public int GetVersion(Guid aggregateId) => _versions.GetValueOrDefault(aggregateId, 0);
	}

	private sealed class OrderRepository(InMemoryEventStore eventStore)
	{
		public async Task SaveAsync(OrderAggregate aggregate)
		{
			var events = aggregate.GetUncommittedEvents().ToList();
			await eventStore.SaveEventsAsync(aggregate.Id, events, aggregate.Version).ConfigureAwait(false);
			aggregate.ClearUncommittedEvents();
		}

		public async Task<OrderAggregate?> GetByIdAsync(Guid id)
		{
			var events = await eventStore.GetEventsAsync(id).ConfigureAwait(false);
			if (events.Count == 0)
			{
				return null;
			}

			var aggregate = new OrderAggregate(id);
			aggregate.LoadFromHistory(events);
			return aggregate;
		}
	}

	#endregion
}
