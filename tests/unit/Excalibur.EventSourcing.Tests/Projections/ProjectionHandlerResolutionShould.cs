// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// T.8 (j92lmk): Unit tests for WhenHandledBy handler resolution from DI,
/// sync+async handler mixing, error propagation (D2), and unhandled event behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHandlerResolutionShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	[Fact]
	public async Task ResolveHandlerFromDiAndApplyEvent()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 250m, Version = 1 }
		};
		var context = new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(250m);
		projected.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task ResolveHandlerWithConstructorInjection()
	{
		// Arrange -- OrderShippedHandler requires ILogger<OrderShippedHandler>
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddSingleton<ILogger<OrderShippedHandler>>(NullLogger<OrderShippedHandler>.Instance)
			.AddTransient<OrderShippedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 1 }
		};
		var context = new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.ShippedAt.ShouldBe(shipped);
		projected.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task MixSyncLambdaWithAsyncHandlerInSameProjection()
	{
		// Arrange -- When<T> lambda (sync) + WhenHandledBy (async DI handler)
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddSingleton<ILogger<OrderShippedHandler>>(NullLogger<OrderShippedHandler>.Instance)
			.AddTransient<OrderShippedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 100m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(100m);
		projected.ShippedAt.ShouldBe(shipped);
		projected.EventCount.ShouldBe(2);
	}

	[Fact]
	public async Task SkipUnhandledEventTypes()
	{
		// Arrange -- only handle TestOrderPlaced, not TestOrderShipped
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 50m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", Version = 2 } // no handler registered
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- only the handled event was applied
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(50m);
		projected.EventCount.ShouldBe(1);
		projected.ShippedAt.ShouldBeNull(); // unhandled, not applied
	}

	[Fact]
	public async Task PropagateHandlerExceptionPerD2()
	{
		// Arrange -- D2: error handling follows NotificationFailurePolicy (abort on throw)
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<ThrowingHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderFailed, ThrowingHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderFailed { AggregateId = "order-1", Version = 1 }
		};
		var context = new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act & Assert -- exception propagates, not swallowed
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => registration.InlineApply!(events, context, services, CancellationToken.None));
		ex.Message.ShouldContain("Handler failed intentionally");
	}

	[Fact]
	public async Task ApplyOverrideProjectionIdFromHandler()
	{
		// Arrange -- handler sets OverrideProjectionId (D1)
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderCancelledWithOverrideHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderCancelled, OrderCancelledWithOverrideHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderCancelled { AggregateId = "order-1", Version = 3 }
		};
		var context = new EventNotificationContext("order-1", "Order", 3, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- both default and overridden IDs should have projections
		var defaultProjection = store.Get("order-1");
		defaultProjection.ShouldNotBeNull();

		var overriddenProjection = store.Get("cancelled-order-1");
		overriddenProjection.ShouldNotBeNull();
		overriddenProjection.EventCount.ShouldBe(1);
	}

	[Fact]
	public void RegisterWhenHandledByReturnsSameBuilderForFluentChaining()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		var result = builder
			.Inline()
			.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReplaceExistingHandlerOnSameEventType()
	{
		// Arrange -- register sync lambda first, then replace with async handler (R27.37: last-wins)
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = 0);
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		// Assert -- projection has handler for the event type (only one entry, last-wins)
		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.Count.ShouldBe(1);

		// The async handler entry should be the one registered (last-wins)
		projection.HasAsyncHandlers.ShouldBeTrue();
	}

	[Fact]
	public void RegisterHandlerTypeInDiViaTryAddTransient()
	{
		// Arrange -- use ServiceCollection-based constructor (DI path)
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<OrderSummary>(services);

		// Act
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();

		// Assert -- handler types were registered in DI
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(OrderPlacedHandler) &&
			sd.Lifetime == ServiceLifetime.Transient);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(OrderShippedHandler) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	[Fact]
	public void RegisterHandlerIdempotentlyViaTryAdd()
	{
		// Arrange -- register same handler type twice
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<OrderSummary>(services);

		// Act
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();

		// Assert -- only one registration (TryAdd is idempotent)
		services.Count(sd => sd.ServiceType == typeof(OrderPlacedHandler)).ShouldBe(1);
	}

	[Fact]
	public async Task UseSyncOnlyFastPathWhenNoAsyncHandlers()
	{
		// Arrange -- all sync handlers -> sync-only fast path (no Dictionary allocation)
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;

		// Verify sync-only path was chosen
		projection.HasAsyncHandlers.ShouldBeFalse();

		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 75m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(75m);
		projected.ShippedAt.ShouldNotBeNull();
		projected.EventCount.ShouldBe(2);
	}

	[Fact]
	public async Task HandleMultipleEventsSequentially()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 10m, Version = 1 },
			new TestOrderPlaced { AggregateId = "order-1", Amount = 20m, Version = 2 },
			new TestOrderPlaced { AggregateId = "order-1", Amount = 30m, Version = 3 }
		};
		var context = new EventNotificationContext("order-1", "Order", 3, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- last event wins on Total, all 3 incremented EventCount
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(30m);
		projected.EventCount.ShouldBe(3);
	}

	[Fact]
	public async Task ResetOverrideProjectionIdBetweenEventsInSameBatch()
	{
		// Arrange -- two async handlers: first sets OverrideProjectionId, second should see it reset
		// This validates the per-event reset behavior in CreateInlineApplyDelegate
		var store = new InMemoryProjectionStore<OrderSummary>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderCancelledWithOverrideHandler>()
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		// First event: handler sets OverrideProjectionId
		builder.WhenHandledBy<TestOrderCancelled, OrderCancelledWithOverrideHandler>();
		// Second event: handler should NOT see previous override
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderCancelled { AggregateId = "order-1", Version = 1 },
			new TestOrderPlaced { AggregateId = "order-1", Amount = 999m, Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- default ID projection has both events applied
		var defaultProjection = store.Get("order-1");
		defaultProjection.ShouldNotBeNull();
		defaultProjection.Total.ShouldBe(999m); // from OrderPlacedHandler on second event
		defaultProjection.EventCount.ShouldBeGreaterThanOrEqualTo(2);

		// Assert -- overridden ID projection only has the cancelled event
		var overriddenProjection = store.Get("cancelled-order-1");
		overriddenProjection.ShouldNotBeNull();
		overriddenProjection.EventCount.ShouldBe(1);
	}

	[Fact]
	public void ThrowOnBuildWithNullRegistryParameter()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);

		// Act & Assert -- Build(null) should throw
		Should.Throw<ArgumentNullException>(
			() => builder.Build(null!));
	}

	[Fact]
	public void ThrowOnBuildWithoutRegistryWhenConstructedWithServiceCollection()
	{
		// Arrange -- ServiceCollection-based builder has no _registry
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<OrderSummary>(services);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);

		// Act & Assert -- Build() (parameterless) should throw since _registry is null
		Should.Throw<InvalidOperationException>(
			() => builder.Build());
	}
}
