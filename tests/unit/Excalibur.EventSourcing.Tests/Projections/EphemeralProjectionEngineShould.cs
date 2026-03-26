// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Unit tests for EphemeralProjectionEngine (R27.39-R27.44).
/// Validates on-demand projection building by replaying events.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EphemeralProjectionEngineShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();
	private readonly NullLogger<EphemeralProjectionEngine> _logger = NullLogger<EphemeralProjectionEngine>.Instance;

	private EphemeralProjectionEngine CreateEngine(IDistributedCache? cache = null)
		=> new(_eventStore, _serializer, _registry, _logger, cache);

	private void RegisterOrderSummaryProjection()
	{
		var msp = new MultiStreamProjection<OrderSummary>();
		msp.AddHandler<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		msp.AddHandler<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Ephemeral,
			msp,
			inlineApply: null));
	}

	private void SetupEventStore(string aggregateId, string aggregateType, params StoredEvent[] events)
	{
		A.CallTo(() => _eventStore.LoadAsync(aggregateId, aggregateType, A<CancellationToken>._))
			.Returns(events.ToList());
	}

	private void SetupSerializer<TEvent>(TEvent instance) where TEvent : IDomainEvent
	{
		A.CallTo(() => _serializer.ResolveType(typeof(TEvent).Name))
			.Returns(typeof(TEvent));
		A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, typeof(TEvent)))
			.Returns(instance);
	}

	/// <summary>
	/// AC-2.1: Ephemeral projection builds from events and returns hydrated state.
	/// </summary>
	[Fact]
	public async Task BuildProjectionFromReplayedEvents()
	{
		// Arrange
		RegisterOrderSummaryProjection();

		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 250m, Version = 1 };
		SetupSerializer(orderPlaced);
		SetupEventStore("order-1", "Order",
			new StoredEvent(
				EventId: Guid.NewGuid().ToString(),
				AggregateId: "order-1",
				AggregateType: "Order",
				EventType: nameof(TestOrderPlaced),
				EventData: [1],
				Metadata: null,
				Version: 1,
				Timestamp: DateTimeOffset.UtcNow));

		var engine = CreateEngine();

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Total.ShouldBe(250m);
		result.EventCount.ShouldBe(1);
	}

	/// <summary>
	/// R27.42: Ephemeral projections use same When&lt;T&gt; handlers as inline.
	/// </summary>
	[Fact]
	public async Task UseSameHandlersAsInlineProjections()
	{
		// Arrange
		RegisterOrderSummaryProjection();

		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 100m, Version = 1 };
		var shipped = DateTimeOffset.UtcNow;
		var orderShipped = new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 };

		SetupSerializer(orderPlaced);
		A.CallTo(() => _serializer.ResolveType(nameof(TestOrderShipped)))
			.Returns(typeof(TestOrderShipped));
		A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>.That.Matches(b => b[0] == 2), typeof(TestOrderShipped)))
			.Returns(orderShipped);

		SetupEventStore("order-1", "Order",
			new StoredEvent("e1", "order-1", "Order", nameof(TestOrderPlaced), [1], null, 1, DateTimeOffset.UtcNow),
			new StoredEvent("e2", "order-1", "Order", nameof(TestOrderShipped), [2], null, 2, DateTimeOffset.UtcNow));

		var engine = CreateEngine();

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert -- both handlers applied
		result.Total.ShouldBe(100m);
		result.ShippedAt.ShouldBe(shipped);
		result.EventCount.ShouldBe(2);
	}

	/// <summary>
	/// Each call returns a fresh projection instance (no shared mutable state).
	/// </summary>
	[Fact]
	public async Task ReturnFreshProjectionOnEachCall()
	{
		// Arrange
		RegisterOrderSummaryProjection();
		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 50m, Version = 1 };
		SetupSerializer(orderPlaced);
		SetupEventStore("order-1", "Order",
			new StoredEvent("e1", "order-1", "Order", nameof(TestOrderPlaced), [1], null, 1, DateTimeOffset.UtcNow));

		var engine = CreateEngine();

		// Act
		var result1 = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);
		var result2 = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert -- different instances, same values
		result1.ShouldNotBeSameAs(result2);
		result1.Total.ShouldBe(50m);
		result2.Total.ShouldBe(50m);
	}

	/// <summary>
	/// Throws when no registration found for projection type.
	/// </summary>
	[Fact]
	public async Task ThrowWhenNoRegistrationExists()
	{
		// Arrange -- no projections registered
		var engine = CreateEngine();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None));
	}

	/// <summary>
	/// Validates null argument guards on constructor.
	/// </summary>
	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(null!, _serializer, _registry, _logger));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, null!, _registry, _logger));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, _serializer, null!, _logger));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, _serializer, _registry, null!));
	}

	/// <summary>
	/// Validates null argument guards on BuildAsync.
	/// </summary>
	[Fact]
	public async Task ThrowOnNullBuildAsyncArguments()
	{
		RegisterOrderSummaryProjection();
		var engine = CreateEngine();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			engine.BuildAsync<OrderSummary>(null!, "Order", CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			engine.BuildAsync<OrderSummary>("order-1", null!, CancellationToken.None));
	}

	/// <summary>
	/// AC-2.2: Optional caching via IDistributedCache with configurable TTL.
	/// When cache is configured and no cached value exists, result is cached.
	/// </summary>
	[Fact]
	public async Task CacheResultWhenCacheTtlConfigured()
	{
		// Arrange -- register with cache TTL
		var msp = new MultiStreamProjection<OrderSummary>();
		msp.AddHandler<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Ephemeral,
			msp,
			inlineApply: null,
			cacheTtl: TimeSpan.FromMinutes(5)));

		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 75m, Version = 1 };
		SetupSerializer(orderPlaced);
		SetupEventStore("order-1", "Order",
			new StoredEvent("e1", "order-1", "Order", nameof(TestOrderPlaced), [1], null, 1, DateTimeOffset.UtcNow));

		var cache = A.Fake<IDistributedCache>();
		A.CallTo(() => cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		var engine = CreateEngine(cache);

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert -- cached
		result.Total.ShouldBe(75m);
		A.CallTo(() => cache.SetAsync(
			"ephemeral:OrderSummary:order-1",
			A<byte[]>._,
			A<DistributedCacheEntryOptions>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// No cache interaction when cache TTL is not configured.
	/// </summary>
	[Fact]
	public async Task NotInteractWithCacheWhenTtlNotConfigured()
	{
		// Arrange -- no TTL set
		RegisterOrderSummaryProjection();
		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 50m, Version = 1 };
		SetupSerializer(orderPlaced);
		SetupEventStore("order-1", "Order",
			new StoredEvent("e1", "order-1", "Order", nameof(TestOrderPlaced), [1], null, 1, DateTimeOffset.UtcNow));

		var cache = A.Fake<IDistributedCache>();
		var engine = CreateEngine(cache);

		// Act
		await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert -- cache never touched
		A.CallTo(() => cache.GetAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => cache.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// Returns empty projection when no events exist for aggregate.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyProjectionWhenNoEventsExist()
	{
		// Arrange
		RegisterOrderSummaryProjection();
		SetupEventStore("order-1", "Order"); // no events

		var engine = CreateEngine();

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None);

		// Assert -- fresh, default state
		result.ShouldNotBeNull();
		result.Total.ShouldBe(0m);
		result.EventCount.ShouldBe(0);
	}
}
