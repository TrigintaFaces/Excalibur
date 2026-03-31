// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionBuilderShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	[Fact]
	public void DefaultToAsyncMode()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- no Inline() or Async() call
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
		registration.InlineApply.ShouldBeNull(); // no inline delegate for async mode
	}

	[Fact]
	public void SetInlineMode()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Inline);
		registration.InlineApply.ShouldNotBeNull(); // inline delegate captured
	}

	[Fact]
	public void SetAsyncModeExplicitly()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Async();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void SupportMultipleEventHandlers()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.When<TestOrderShipped>((proj, e) => proj.ShippedAt = e.ShippedAt);
		builder.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		var projection = (MultiStreamProjection<OrderSummary>)registration.Projection;
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderPlaced));
		projection.HandledEventTypes.ShouldContain(typeof(TestOrderShipped));
		projection.HandledEventTypes.Count.ShouldBe(2);
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);

		// Act -- all methods return the builder for fluent chaining
		var result = builder
			.Inline()
			.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount)
			.WithCacheTtl(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void SetCacheTtl()
	{
		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		var ttl = TimeSpan.FromMinutes(10);

		// Act
		builder.WithCacheTtl(ttl);

		// Assert
		builder.CacheTtl.ShouldBe(ttl);
	}

	[Fact]
	public void ReplaceExistingRegistrationOnSecondBuild()
	{
		// Arrange -- first registration as Inline
		var builder1 = new ProjectionBuilder<OrderSummary>(_registry);
		builder1.Inline();
		builder1.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder1.Build();

		// Act -- second registration as Async (R27.37: idempotent, replaces)
		var builder2 = new ProjectionBuilder<OrderSummary>(_registry);
		builder2.Async();
		builder2.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount * 2);
		builder2.Build();

		// Assert
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void ThrowOnNullRegistry()
	{
		Should.Throw<ArgumentNullException>(() => new ProjectionBuilder<OrderSummary>((IProjectionRegistry)null!));
	}

	[Fact]
	public void ThrowOnNullHandler()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		Should.Throw<ArgumentNullException>(() =>
			builder.When<TestOrderPlaced>(null!));
	}

	[Fact]
	public async Task InlineApplyDelegateAppliesEventsAndPersists()
	{
		// Arrange
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
		var events = new List<Dispatch.Abstractions.IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 150m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(150m);
		projected.ShippedAt.ShouldNotBeNull();
		projected.EventCount.ShouldBe(2);
	}

	[Fact]
	public async Task InlineApplyDelegateMergesWithExistingState()
	{
		// Arrange -- pre-seed store with existing projection state
		var store = new InMemoryProjectionStore<OrderSummary>();
		await store.UpsertAsync("order-1",
			new OrderSummary { Total = 50m, EventCount = 1 },
			CancellationToken.None);

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.BuildServiceProvider();

		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});
		builder.Build();

		var registration = _registry.GetRegistration(typeof(OrderSummary))!;
		var events = new List<Dispatch.Abstractions.IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- existing Total preserved, new fields updated
		var projected = store.Get("order-1");
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(50m); // preserved from pre-seed
		projected.ShippedAt.ShouldNotBeNull();
		projected.EventCount.ShouldBe(2); // incremented
	}
}
