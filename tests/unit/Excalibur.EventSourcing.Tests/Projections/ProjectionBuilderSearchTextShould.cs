// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Tests for <see cref="IProjectionBuilder{TProjection}.WithSearchText"/> (bd-afz81j).
/// Covers builder configuration, null guards, registration propagation,
/// and inline apply behavior across all 3 code paths (async, sync+keyed, sync fast path).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionBuilderSearchTextShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	// ───────────────────────────────────────────────────────────────
	// Builder configuration tests
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public void StoreSearchTextDelegates()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}",
			(p, text) => p.SearchText = text);

		// Assert
		builder.SearchTextComputer.ShouldNotBeNull();
		builder.SearchTextSetter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnBuilderForFluentChaining()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act
		var result = builder
			.Inline()
			.WithSearchText(p => p.Label ?? "", (p, text) => p.SearchText = text)
			.When<TestOrderPlaced>((proj, e) => proj.Label = "Test");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowOnNullComputeDelegate()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithSearchText(null!, (p, text) => p.SearchText = text))
			.ParamName.ShouldBe("computeSearchText");
	}

	[Fact]
	public void ThrowOnNullSetDelegate()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithSearchText(p => p.Label ?? "", null!))
			.ParamName.ShouldBe("setSearchText");
	}

	// ───────────────────────────────────────────────────────────────
	// Registration propagation tests
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public void PropagateSearchTextDelegatesToRegistration()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act
		builder.Inline();
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}",
			(p, text) => p.SearchText = text);
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(SearchableOrder));
		registration.ShouldNotBeNull();
		registration.SearchTextComputer.ShouldNotBeNull();
		registration.SearchTextSetter.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNullSearchTextDelegatesWhenNotConfigured()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);

		// Act — no WithSearchText call (zero overhead opt-in)
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(SearchableOrder));
		registration.ShouldNotBeNull();
		registration.SearchTextComputer.ShouldBeNull();
		registration.SearchTextSetter.ShouldBeNull();
	}

	[Fact]
	public void TypeEraseSearchTextDelegatesInRegistration()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);
		builder.Inline();
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}",
			(p, text) => p.SearchText = text);
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Act — invoke the type-erased delegates with an object
		var registration = _registry.GetRegistration(typeof(SearchableOrder));
		registration.ShouldNotBeNull();
		var order = new SearchableOrder { Label = "Alice", Total = 100m };
		var computed = registration.SearchTextComputer!(order);
		registration.SearchTextSetter!(order, computed);

		// Assert
		computed.ShouldBe("Alice 100");
		order.SearchText.ShouldBe("Alice 100");
	}

	// ───────────────────────────────────────────────────────────────
	// Inline apply integration tests (sync fast path — no key selectors)
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public async Task ComputeSearchTextAfterEventApplication_SyncFastPath()
	{
		// Arrange
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);
		builder.Inline();
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}".Trim(),
			(p, text) => p.SearchText = text);
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Label = "Placed";
			proj.Total = e.Amount;
		});
		builder.Build();

		var store = new InMemoryProjectionStore<SearchableOrder>();
		var services = BuildServiceProvider(store);
		var registration = _registry.GetRegistration(typeof(SearchableOrder))!;
		var context = CreateContext("order-1");
		IDomainEvent[] events = [CreateOrderPlaced(100m)];

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Label.ShouldBe("Placed");
		result.SearchText.ShouldBe("Placed 100");
	}

	[Fact]
	public async Task NotSetSearchTextWhenNotConfigured_SyncFastPath()
	{
		// Arrange — no WithSearchText (zero overhead)
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Label = "Placed";
			proj.Total = e.Amount;
		});
		builder.Build();

		var store = new InMemoryProjectionStore<SearchableOrder>();
		var services = BuildServiceProvider(store);
		var registration = _registry.GetRegistration(typeof(SearchableOrder))!;
		var context = CreateContext("order-2");
		IDomainEvent[] events = [CreateOrderPlaced(50m)];

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — SearchText should remain null (zero overhead)
		var result = store.Get("order-2");
		result.ShouldNotBeNull();
		result.SearchText.ShouldBeNull();
	}

	[Fact]
	public async Task ComputeSearchTextOncePerUpsert_MultipleEvents()
	{
		// Arrange — track how many times the computer is called
		var computeCount = 0;
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);
		builder.Inline();
		builder.WithSearchText(
			p =>
			{
				Interlocked.Increment(ref computeCount);
				return $"{p.Label} {p.Total}";
			},
			(p, text) => p.SearchText = text);
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Label = "Placed";
			proj.Total = e.Amount;
		});
		builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
		});
		builder.Build();

		var store = new InMemoryProjectionStore<SearchableOrder>();
		var services = BuildServiceProvider(store);
		var registration = _registry.GetRegistration(typeof(SearchableOrder))!;
		var context = CreateContext("order-3");
		IDomainEvent[] events = [CreateOrderPlaced(75m), CreateOrderShipped()];

		// Act — batch of 2 events, search text computed once after both applied
		await registration.InlineApply!(events, context, services, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		computeCount.ShouldBe(1);
		var result = store.Get("order-3");
		result.ShouldNotBeNull();
		result.SearchText.ShouldBe("Placed 75");
	}

	// ───────────────────────────────────────────────────────────────
	// Inline apply integration tests (sync + keyed path)
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public async Task ComputeSearchTextPerProjectionInstance_KeyedPath()
	{
		// Arrange — two events keyed to different projection IDs by amount
		var builder = new ProjectionBuilder<SearchableOrder>(_registry);
		builder.Inline();
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}",
			(p, text) => p.SearchText = text);
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Label = $"Order-{e.Amount}";
			proj.Total = e.Amount;
		});
		builder.KeyedBy<TestOrderPlaced>(e => $"amount-{e.Amount}");
		builder.Build();

		var store = new InMemoryProjectionStore<SearchableOrder>();
		var services = BuildServiceProvider(store);
		var registration = _registry.GetRegistration(typeof(SearchableOrder))!;
		var context = CreateContext("agg-1");

		IDomainEvent[] events = [CreateOrderPlaced(100m), CreateOrderPlaced(200m)];

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — each projection instance gets its own search text
		var p100 = store.Get("amount-100");
		p100.ShouldNotBeNull();
		p100.SearchText.ShouldBe("Order-100 100");

		var p200 = store.Get("amount-200");
		p200.ShouldNotBeNull();
		p200.SearchText.ShouldBe("Order-200 200");
	}

	// ───────────────────────────────────────────────────────────────
	// Inline apply integration tests (async handler path)
	// ───────────────────────────────────────────────────────────────

	[Fact]
	public async Task ComputeSearchTextAfterAsyncHandlers()
	{
		// Arrange — use WhenHandledBy to force the async handler path
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<SearchableOrder>(services);
		builder.Inline();
		builder.WithSearchText(
			p => $"{p.Label} {p.Total}",
			(p, text) => p.SearchText = text);
		builder.WhenHandledBy<TestOrderPlaced, TestSearchOrderPlacedHandler>();

		var registry = new InMemoryProjectionRegistry();
		builder.Build(registry);

		var store = new InMemoryProjectionStore<SearchableOrder>();
		services.AddSingleton<IProjectionStore<SearchableOrder>>(store);
		var sp = services.BuildServiceProvider();

		var registration = registry.GetRegistration(typeof(SearchableOrder))!;
		var context = CreateContext("order-async-1");
		IDomainEvent[] events = [CreateOrderPlaced(500m)];

		// Act
		await registration.InlineApply!(events, context, sp, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var result = store.Get("order-async-1");
		result.ShouldNotBeNull();
		result.Label.ShouldBe("AsyncPlaced");
		result.SearchText.ShouldBe("AsyncPlaced 500");
	}

	// ───────────────────────────────────────────────────────────────
	// Helpers
	// ───────────────────────────────────────────────────────────────

	private static IServiceProvider BuildServiceProvider(IProjectionStore<SearchableOrder> store)
	{
		var services = new ServiceCollection();
		services.AddSingleton(store);
		return services.BuildServiceProvider();
	}

	private static EventNotificationContext CreateContext(string aggregateId)
		=> new(aggregateId, "Order", 1, DateTimeOffset.UtcNow);

	private static TestOrderPlaced CreateOrderPlaced(decimal amount)
		=> new() { Amount = amount };

	private static TestOrderShipped CreateOrderShipped()
		=> new() { ShippedAt = DateTimeOffset.UtcNow };
}

/// <summary>
/// Projection type with a SearchText property for testing WithSearchText.
/// </summary>
internal sealed class SearchableOrder
{
	public string? Label { get; set; }
	public decimal Total { get; set; }
	public DateTimeOffset? ShippedAt { get; set; }
	public string? SearchText { get; set; }
}

/// <summary>
/// Async projection handler for testing the async code path with SearchText.
/// </summary>
internal sealed class TestSearchOrderPlacedHandler : IProjectionEventHandler<SearchableOrder, TestOrderPlaced>
{
	public Task HandleAsync(
		SearchableOrder projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Label = "AsyncPlaced";
		projection.Total = @event.Amount;
		return Task.CompletedTask;
	}
}
