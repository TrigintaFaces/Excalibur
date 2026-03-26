// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionRecoveryServiceShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly InMemoryProjectionStore<OrderSummary> _projectionStore = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();

	private ProjectionRecoveryService CreateService()
	{
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore)
			.BuildServiceProvider();

		return new ProjectionRecoveryService(
			_registry,
			_eventStore,
			_eventSerializer,
			services,
			NullLogger<ProjectionRecoveryService>.Instance);
	}

	[Fact]
	public async Task ReplayEventsAndPersistProjection()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", nameof(TestOrderPlaced),
				Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow)
		};

		A.CallTo(() => _eventStore.LoadAsync("order-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(storedEvents));

		A.CallTo(() => _eventSerializer.ResolveType(nameof(TestOrderPlaced)))
			.Returns(typeof(TestOrderPlaced));

		var testEvent = new TestOrderPlaced { Amount = 200m, AggregateId = "order-1", Version = 1 };
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(TestOrderPlaced)))
			.Returns(testEvent);

		var service = CreateService();

		// Act
		await service.ReapplyAsync<OrderSummary>("order-1", CancellationToken.None);

		// Assert
		var recovered = _projectionStore.Get("order-1");
		recovered.ShouldNotBeNull();
		recovered.Total.ShouldBe(200m);
		recovered.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task CreateFreshProjectionState()
	{
		// Arrange -- pre-existing projection state should be replaced with fresh
		await _projectionStore.UpsertAsync("order-1",
			new OrderSummary { Total = 999m, EventCount = 99 },
			CancellationToken.None);

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		A.CallTo(() => _eventStore.LoadAsync("order-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(new List<StoredEvent>
			{
				new("e1", "order-1", "Order", nameof(TestOrderPlaced),
					Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow)
			}));

		A.CallTo(() => _eventSerializer.ResolveType(nameof(TestOrderPlaced)))
			.Returns(typeof(TestOrderPlaced));

		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(TestOrderPlaced)))
			.Returns(new TestOrderPlaced { Amount = 100m });

		var service = CreateService();

		// Act
		await service.ReapplyAsync<OrderSummary>("order-1", CancellationToken.None);

		// Assert -- fresh state, not accumulated on old state
		var recovered = _projectionStore.Get("order-1");
		recovered.ShouldNotBeNull();
		recovered.Total.ShouldBe(100m);
		recovered.EventCount.ShouldBe(0); // fresh instance, no increment handler
	}

	[Fact]
	public async Task ThrowForUnregisteredProjectionType()
	{
		// Arrange -- no registration for OrderSummary
		var service = CreateService();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			service.ReapplyAsync<OrderSummary>("order-1", CancellationToken.None));

		ex.Message.ShouldContain("OrderSummary");
		ex.Message.ShouldContain("AddProjection");
	}

	[Fact]
	public async Task ThrowOnNullAggregateId()
	{
		var service = CreateService();
		await Should.ThrowAsync<ArgumentException>(() =>
			service.ReapplyAsync<OrderSummary>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnEmptyAggregateId()
	{
		var service = CreateService();
		await Should.ThrowAsync<ArgumentException>(() =>
			service.ReapplyAsync<OrderSummary>("", CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var sp = A.Fake<IServiceProvider>();
		var logger = NullLogger<ProjectionRecoveryService>.Instance;

		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRecoveryService(null!, _eventStore, _eventSerializer, sp, logger));
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRecoveryService(_registry, null!, _eventSerializer, sp, logger));
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRecoveryService(_registry, _eventStore, null!, sp, logger));
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRecoveryService(_registry, _eventStore, _eventSerializer, null!, logger));
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRecoveryService(_registry, _eventStore, _eventSerializer, sp, null!));
	}

	[Fact]
	public async Task ReplayMultipleEvents()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total += e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", nameof(TestOrderPlaced),
				Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow),
			new("e2", "order-1", "Order", nameof(TestOrderPlaced),
				Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow)
		};

		A.CallTo(() => _eventStore.LoadAsync("order-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(storedEvents));

		A.CallTo(() => _eventSerializer.ResolveType(nameof(TestOrderPlaced)))
			.Returns(typeof(TestOrderPlaced));

		var call = 0;
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(TestOrderPlaced)))
			.ReturnsLazily(() => new TestOrderPlaced { Amount = ++call * 10m });

		var service = CreateService();

		// Act
		await service.ReapplyAsync<OrderSummary>("order-1", CancellationToken.None);

		// Assert
		var recovered = _projectionStore.Get("order-1");
		recovered.ShouldNotBeNull();
		recovered.Total.ShouldBe(30m); // 10 + 20
		recovered.EventCount.ShouldBe(2);
	}
}
