// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
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
	// A SCOPE FACTORY, because the engine is a singleton whose handlers may take scoped dependencies
	// and must therefore resolve them inside a scope per build rather than from the root container.
	private static readonly IServiceScopeFactory EngineServices =
		new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();
	private readonly NullLogger<EphemeralProjectionEngine> _logger = NullLogger<EphemeralProjectionEngine>.Instance;

	private EphemeralProjectionEngine CreateEngine(IDistributedCache? cache = null)
		=> new(_eventStore, _serializer, _registry, _logger, EngineServices, cache);

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
			new EphemeralProjectionEngine(null!, _serializer, _registry, _logger, EngineServices));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, null!, _registry, _logger, EngineServices));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, _serializer, null!, _logger, EngineServices));
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, _serializer, _registry, null!, EngineServices));

		// The provider is REQUIRED, not optional. It is what resolves a DI-registered handler, so an
		// engine without one silently folds nothing for any projection whose handlers came from
		// WhenHandledBy -- the defect this parameter was added to close. An optional parameter would
		// let that state be constructed again.
		Should.Throw<ArgumentNullException>(() =>
			new EphemeralProjectionEngine(_eventStore, _serializer, _registry, _logger, null!));
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

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task FoldAnEventWhoseHandlerWasRegisteredByWhenHandledBy()
	{
		// WhenHandledBy registers an ASYNC handler entry -- the only shape a DI-resolved handler can take
		// (ProjectionBuilder.cs:92 -> MultiStreamProjection.AddAsyncHandler). Every other arm in this suite
		// registers a SYNC handler, so the suite cannot see what happens to the async shape here.
		//
		// The synchronous three-argument Apply dispatches SyncAction and SyncContextAction and returns
		// false for an async entry; the engine discards that result. A projection whose handlers were all
		// registered by WhenHandledBy therefore folds nothing and returns its empty initial state, with no
		// exception and nothing logged -- the caller cannot distinguish it from an aggregate with no events.
		// REAL REGISTRATION, through WhenHandledBy and a real container. The previous version installed
		// an async handler by hand with a delegate that ignored the service provider -- which proved
		// async dispatch happens and could not prove the named DI scenario works at all. It therefore
		// missed that the engine resolved handlers from the ROOT provider, so a handler taking a SCOPED
		// dependency threw under scope validation.
		//
		// The dependency below is scoped on purpose: it is the shape that failed.
		var services = new ServiceCollection();
		_ = services.AddScoped<ScopedAmountSource>();

		var builder = new ProjectionBuilder<ScopedFoldTarget>(services);
		_ = builder.Ephemeral().WhenHandledBy<TestOrderPlaced, ScopedOrderPlacedHandler>();

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			// The validation that turns a root-resolved scoped dependency from a silent lifetime bug
			// into a failure. Without it the defect is invisible and the handler simply shares one
			// instance across every concurrent build.
			ValidateScopes = true,
			ValidateOnBuild = true,
		});

		// The PRODUCTION registration path, so the registry holds exactly what AddProjection would put
		// there -- rather than a registration hand-built around the builder's internals.
		builder.Build(_registry);

		var placed = new TestOrderPlaced { AggregateId = "order-1", Amount = 250m, Version = 1 };
		SetupSerializer(placed);
		SetupEventStore("order-1", "Order", new StoredEvent(
			placed.EventId, "order-1", "Order", nameof(TestOrderPlaced), [1], null, 1, placed.OccurredAt));

		var engine = new EphemeralProjectionEngine(
			_eventStore,
			_serializer,
			_registry,
			_logger,
			provider.GetRequiredService<IServiceScopeFactory>());

		var result = await engine.BuildAsync<ScopedFoldTarget>(
			"order-1", "Order", TestContext.Current.CancellationToken).ConfigureAwait(false);

		result.EventCount.ShouldBe(1, "a WhenHandledBy handler must be invoked when the projection is built");
		result.Total.ShouldBe(250m);
	}

	// SCOPED, which is the lifetime that exposed the defect: resolving this from the root container is
	// either a validation failure or a process-lifetime singleton shared across concurrent builds.
	// Its OWN projection type, deliberately. A second handler for a (projection, event) pair that this
	// assembly already covers is refused by assembly scanning -- correctly, since one pair may have only
	// one handler -- and a fixture that collides with a production-path arm breaks that arm rather than
	// testing anything.
	private sealed class ScopedFoldTarget
	{
		public decimal Total { get; set; }

		public int EventCount { get; set; }
	}

	private sealed class ScopedAmountSource
	{
		internal decimal Amount => 250m;
	}

	private sealed class ScopedOrderPlacedHandler(ScopedAmountSource amounts)
		: IProjectionEventHandler<ScopedFoldTarget, TestOrderPlaced>
	{
		public Task HandleAsync(
			ScopedFoldTarget projection,
			TestOrderPlaced @event,
			ProjectionHandlerContext context,
			CancellationToken cancellationToken)
		{
			projection.Total = amounts.Amount;
			projection.EventCount++;
			return Task.CompletedTask;
		}
	}

	[Fact]
	[Trait("Pattern", "Control")]
	public void RejectResolvingTheScopedHandlerFromTheRootProvider()
	{
		// NON-VACUITY CONTROL for the arm above, not a mutation of the engine.
		//
		// That arm passes because the engine creates a scope per build. It would be worthless if the
		// handler's dependency were not actually scope-sensitive -- so this asserts the other half
		// directly: resolving the same handler from the ROOT container is refused. That refusal is
		// exactly what the engine used to hit, because it was constructed with the root provider and
		// resolved handlers from it.
		var services = new ServiceCollection();
		_ = services.AddScoped<ScopedAmountSource>();
		_ = services.AddTransient<ScopedOrderPlacedHandler>();

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
		});

		_ = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<ScopedOrderPlacedHandler>(),
			customMessage:
				"if the root container could satisfy this handler, the scoping arm above would prove nothing");
	}
}
