// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Integration tests proving inline projections achieve immediate consistency
/// using real DI container and in-memory infrastructure.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InlineProjectionIntegrationShould
{
	private static EventNotificationContext CreateContext(
		string aggregateId = "order-1",
		long version = 1) =>
		new(aggregateId, "Order", version, DateTimeOffset.UtcNow);

	private static InlineProjectionProcessor CreateProcessor(
		InMemoryProjectionRegistry registry,
		IServiceProvider sp) =>
		new(registry, sp, NullLogger<InlineProjectionProcessor>.Instance);

	private static EventNotificationBroker CreateBroker(
		InlineProjectionProcessor processor,
		IServiceProvider sp,
		EventNotificationOptions? options = null) =>
		new(
			processor, sp,
			Options.Create(options ?? new EventNotificationOptions()),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());

	/// <summary>
	/// AC-1.1: SaveAsync -> inline projection immediately consistent.
	/// No polling, no delay -- immediate read-after-write consistency.
	/// </summary>
	[Fact]
	public async Task AchieveImmediateConsistencyAfterInlineProjection()
	{
		// Arrange
		var projectionStore = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(projectionStore)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);

		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 250m, Version = 1 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None);

		// Assert -- immediate consistency, no polling needed
		var projected = await projectionStore.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(250m);
		projected.EventCount.ShouldBe(1);
	}

	/// <summary>
	/// AC-1.2: No broker = identical behavior (regression test).
	/// When no IEventNotificationBroker is in DI, behavior is unchanged.
	/// </summary>
	[Fact]
	public void ResolveNullBrokerWhenNotRegistered()
	{
		// Arrange -- empty service collection, no UseEventNotification called
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		// Act
		var broker = sp.GetService<IEventNotificationBroker>();

		// Assert -- null, zero overhead path
		broker.ShouldBeNull();
	}

	/// <summary>
	/// AC-1.3: Notification handlers invoked after projections (R27.8).
	/// </summary>
	[Fact]
	public async Task InvokeHandlersAfterProjectionsComplete()
	{
		// Arrange
		var executionOrder = new List<string>();
		var projectionStore = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				executionOrder.Add("inline-projection");
				return Task.CompletedTask;
			}));

		var handler = new OrderingTestHandler(executionOrder);
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(projectionStore)
			.AddSingleton(typeof(IEventNotificationHandler<TestOrderPlaced>), handler)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, services);

		var broker = CreateBroker(processor, services);

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			CancellationToken.None);

		// Assert -- strict ordering: projections first, then handlers
		executionOrder.ShouldBe(new[] { "inline-projection", "notification-handler" });
	}

	/// <summary>
	/// AC-1.4: FailurePolicy.Propagate -> failing projection throws AggregateException.
	/// </summary>
	[Fact]
	public async Task PropagateFailurePolicyThrowsOnProjectionFailure()
	{
		// Arrange
		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store unavailable"))));

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>())
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, services);

		var broker = CreateBroker(processor, services, new EventNotificationOptions
		{
			FailurePolicy = NotificationFailurePolicy.Propagate
		});

		// Act & Assert
		var ex = await Should.ThrowAsync<AggregateException>(() =>
			broker.NotifyAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext(),
				CancellationToken.None));

		ex.InnerExceptions.Count.ShouldBeGreaterThan(0);
	}

	/// <summary>
	/// AC-1.5: FailurePolicy.LogAndContinue -> failing store, SaveAsync succeeds.
	/// </summary>
	[Fact]
	public async Task LogAndContinuePolicySucceedsOnProjectionFailure()
	{
		// Arrange
		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store unavailable"))));

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>())
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, services);

		var broker = CreateBroker(processor, services, new EventNotificationOptions
		{
			FailurePolicy = NotificationFailurePolicy.LogAndContinue
		});

		// Act -- should NOT throw
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			CancellationToken.None);
	}

	/// <summary>
	/// AC-1.7: Two inline projections for different types, both updated after notification.
	/// </summary>
	[Fact]
	public async Task UpdateMultipleConcurrentProjectionTypes()
	{
		// Arrange
		var orderStore = new InMemoryProjectionStore<OrderSummary>();
		var inventoryStore = new InMemoryProjectionStore<InventoryView>();
		var registry = new InMemoryProjectionRegistry();

		// Build OrderSummary projection
		var orderBuilder = new ProjectionBuilder<OrderSummary>(registry);
		orderBuilder.Inline();
		orderBuilder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		orderBuilder.Build();

		// Build InventoryView projection
		var inventoryBuilder = new ProjectionBuilder<InventoryView>(registry);
		inventoryBuilder.Inline();
		inventoryBuilder.When<TestOrderPlaced>((proj, _) => proj.Quantity--);
		inventoryBuilder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(orderStore)
			.AddSingleton<IProjectionStore<InventoryView>>(inventoryStore)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);

		var broker = CreateBroker(processor, sp);

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent>
			{
				new TestOrderPlaced { AggregateId = "order-1", Amount = 500m }
			},
			CreateContext(),
			CancellationToken.None);

		// Assert -- both projections updated
		var order = await orderStore.GetByIdAsync("order-1", CancellationToken.None);
		order.ShouldNotBeNull();
		order.Total.ShouldBe(500m);

		var inventory = await inventoryStore.GetByIdAsync("order-1", CancellationToken.None);
		inventory.ShouldNotBeNull();
		inventory.Quantity.ShouldBe(-1); // decremented from default 0
	}

	/// <summary>
	/// Full DI pipeline test: UseEventNotification registers and resolves all services.
	/// </summary>
	[Fact]
	public void RegisterAndResolveEventNotificationInfrastructureViaDI()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());
		services.AddLogging();
		services.AddMetrics();

		var fakeBuilder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => fakeBuilder.Services).Returns(services);

		// Act
		fakeBuilder.UseEventNotification();

		var sp = services.BuildServiceProvider();

		// Assert -- all services resolve correctly
		sp.GetService<IEventNotificationBroker>().ShouldNotBeNull();
		sp.GetService<IProjectionRegistry>().ShouldNotBeNull();
		sp.GetService<InlineProjectionProcessor>().ShouldNotBeNull();
	}

	/// <summary>
	/// UseEventNotification is idempotent -- multiple calls register services once.
	/// </summary>
	[Fact]
	public void BeIdempotentForMultipleUseEventNotificationCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new InMemoryProjectionStore<OrderSummary>());
		services.AddLogging();
		services.AddMetrics();

		var fakeBuilder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => fakeBuilder.Services).Returns(services);

		// Act -- call twice
		fakeBuilder.UseEventNotification();
		fakeBuilder.UseEventNotification();

		var sp = services.BuildServiceProvider();

		// Assert -- only one instance registered (TryAddSingleton)
		var brokers = sp.GetServices<IEventNotificationBroker>().ToList();
		brokers.Count.ShouldBe(1);
	}

	/// <summary>
	/// Multiple events in a single notification batch are applied in order.
	/// </summary>
	[Fact]
	public async Task ApplyMultipleEventsInOrder()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
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

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);

		var broker = CreateBroker(processor, sp);

		var shippedAt = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 75m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shippedAt, Version = 2 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext("order-1", 2), CancellationToken.None);

		// Assert -- both events applied in order
		var projected = await store.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(75m);
		projected.ShippedAt.ShouldBe(shippedAt);
		projected.EventCount.ShouldBe(2);
	}

	// -- Test helpers --

	private sealed class OrderingTestHandler : IEventNotificationHandler<TestOrderPlaced>
	{
		private readonly List<string> _order;

		public OrderingTestHandler(List<string> order) => _order = order;

		public Task HandleAsync(
			TestOrderPlaced @event,
			EventNotificationContext context,
			CancellationToken cancellationToken)
		{
			_order.Add("notification-handler");
			return Task.CompletedTask;
		}
	}
}
