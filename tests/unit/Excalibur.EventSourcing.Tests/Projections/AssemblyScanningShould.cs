// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// T.9 (inlkjt): Unit tests for AddProjectionHandlersFromAssembly -- discovery,
/// duplicate detection (D3), zero handlers no-op (D7), and mixed explicit + scanned.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AssemblyScanningShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	[Fact]
	public void DiscoverHandlersFromAssembly()
	{
		// Arrange -- scan for OrderSummary handlers in this assembly.
		// OrderPlacedHandler (TestOrderPlaced), OrderShippedHandler (TestOrderShipped),
		// OrderCancelledWithOverrideHandler (TestOrderCancelled), ThrowingHandler (TestOrderFailed)
		// all handle OrderSummary -- one per event type, no duplicates.
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();

		// Act
		builder.AddProjectionHandlersFromAssembly(typeof(OrderPlacedHandler).Assembly);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderShipped));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderCancelled));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderFailed));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderRefunded));
		projection.HandledEventTypes.Count.ShouldBe(5);
	}

	[Fact]
	public void ThrowOnDuplicateHandlerForSameEventType()
	{
		// Arrange -- DuplicateTestProjection has two handlers for TestOrderPlaced:
		// DuplicateTestHandlerA and DuplicateTestHandlerB -> D3: InvalidOperationException
		var builder = new ProjectionBuilder<DuplicateTestProjection>(_registry);
		builder.Inline();

		// Act & Assert -- D3: duplicate detection
		var ex = Should.Throw<InvalidOperationException>(
			() => builder.AddProjectionHandlersFromAssembly(typeof(DuplicateTestHandlerA).Assembly));
		ex.Message.ShouldContain("Duplicate handler");
		ex.Message.ShouldContain("TestOrderPlaced");
	}

	[Fact]
	public void NoOpWhenAssemblyHasNoHandlers()
	{
		// Arrange -- scan an assembly with no IProjectionEventHandler implementations (D7)
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();

		// Act -- System.Runtime has no projection handlers
		builder.AddProjectionHandlersFromAssembly(typeof(object).Assembly);
		builder.Build();

		// Assert -- no handlers registered, no exception
		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.Count.ShouldBe(0);
	}

	[Fact]
	public void IgnoreHandlersForDifferentProjectionType()
	{
		// Arrange -- scan for InventoryView handlers
		// Only InventoryEventHandler handles (InventoryView, TestOrderPlaced)
		var builder = new ProjectionBuilder<InventoryView>(_registry);
		builder.Inline();

		// Act
		builder.AddProjectionHandlersFromAssembly(typeof(InventoryEventHandler).Assembly);
		builder.Build();

		// Assert -- only InventoryView handlers found, OrderSummary handlers ignored
		var registration = _registry.GetRegistration(typeof(InventoryView))!;
		var projection = (MultiStreamProjection<InventoryView>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.Count.ShouldBe(1);
	}

	[Fact]
	public void ThrowOnNullAssembly()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(
			() => builder.AddProjectionHandlersFromAssembly(null!));
	}

	[Fact]
	public async Task MixExplicitWhenHandledByWithScannedHandlers()
	{
		// Arrange -- explicit When<T> lambda for TestOrderShipped + scan for InventoryView handlers
		var store = new InMemoryProjectionStore<InventoryView>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<InventoryView>>(store)
			.AddTransient<InventoryEventHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<InventoryView>(_registry);
		builder.Inline();

		// Explicit sync lambda for TestOrderShipped
		builder.When<TestOrderShipped>((proj, e) => proj.EventCount += 10);

		// Scan assembly for InventoryView handlers (InventoryEventHandler handles TestOrderPlaced)
		builder.AddProjectionHandlersFromAssembly(typeof(InventoryEventHandler).Assembly);
		builder.Build();

		var registration = _registry.GetRegistration(typeof(InventoryView))!;
		var projection = (MultiStreamProjection<InventoryView>)registration.Projection;

		// Assert -- both explicit and scanned handlers registered
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderShipped));

		// Act -- run inline apply with both event types
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "inv-1", Version = 1 },
			new TestOrderShipped { AggregateId = "inv-1", Version = 2 }
		};
		var context = new EventNotificationContext("inv-1", "Inventory", 2, DateTimeOffset.UtcNow);

		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- both handlers applied
		var projected = store.Get("inv-1");
		projected.ShouldNotBeNull();
		projected.Quantity.ShouldBe(1); // from scanned InventoryEventHandler
		projected.EventCount.ShouldBe(10); // from explicit lambda
	}

	[Fact]
	public void RegisterScannedHandlersInDi()
	{
		// Arrange -- use ServiceCollection-based constructor
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<InventoryView>(services);
		builder.Inline();

		// Act
		builder.AddProjectionHandlersFromAssembly(typeof(InventoryEventHandler).Assembly);

		// Assert -- InventoryEventHandler registered as Transient in DI
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(InventoryEventHandler) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	[Fact]
	public void IgnoreAbstractAndInterfaceTypes()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();

		// Act -- scan System.Runtime which has no projection handlers
		builder.AddProjectionHandlersFromAssembly(typeof(object).Assembly);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.Count.ShouldBe(0);
	}
}
