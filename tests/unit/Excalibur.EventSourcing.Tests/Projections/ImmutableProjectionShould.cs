// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// C.8 (r3ocil) + C.10 (fm0xsn): Unit tests for immutable projections --
/// records, null guards, Q1 cases, assembly scanning.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ImmutableProjectionShould
{
	// --- Q1 Cases: WhenCreating ---

	[Fact]
	public async Task CreateProjectionFromFactoryWhenNullCurrent()
	{
		// Q1: null current + Creating = factory returns new instance
		var store = new InMemoryProjectionStore<OrderRecord>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 500m, Version = 1 }
		};
		var context = new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert
		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Total.ShouldBe(500m);
	}

	[Fact]
	public async Task ReplaceProjectionWhenCreatingCalledWithNonNullCurrent()
	{
		// Q1: non-null current + Creating = factory replaces (last-wins)
		var store = new InMemoryProjectionStore<OrderRecord>();
		await store.UpsertAsync("order-1", new OrderRecord(100m, null), CancellationToken.None);

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 999m, Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- replaced, not merged
		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Total.ShouldBe(999m);
	}

	// --- Q1 Cases: WhenTransforming ---

	[Fact]
	public async Task TransformExistingProjection()
	{
		// Q1: non-null current + Transforming = reducer returns new instance
		var store = new InMemoryProjectionStore<OrderRecord>();
		await store.UpsertAsync("order-1", new OrderRecord(100m, null), CancellationToken.None);

		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		// Act
		await registration.InlineApply!(events, context, services, CancellationToken.None);

		// Assert -- new instance with ShippedAt set, Total preserved
		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Total.ShouldBe(100m);
		result.ShippedAt.ShouldBe(shipped);
	}

	[Fact]
	public async Task ThrowWhenTransformingWithNullCurrent()
	{
		// Q1: null current + Transforming = throws
		var store = new InMemoryProjectionStore<OrderRecord>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", Version = 1 }
		};
		var context = new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => registration.InlineApply!(events, context, services, CancellationToken.None));
		ex.Message.ShouldContain("Cannot transform");
		ex.Message.ShouldContain("OrderRecord");
	}

	// --- Mixed Creating + Transforming ---

	[Fact]
	public async Task ChainCreatingThenTransforming()
	{
		var store = new InMemoryProjectionStore<OrderRecord>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 250m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		await registration.InlineApply!(events, context, services, CancellationToken.None);

		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Total.ShouldBe(250m);
		result.ShippedAt.ShouldBe(shipped);
	}

	// --- Unhandled events ---

	[Fact]
	public async Task SkipUnhandledEventTypes()
	{
		var store = new InMemoryProjectionStore<OrderRecord>();
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		builder.Build(registry);

		var registration = registry.GetRegistration(typeof(OrderRecord))!;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 50m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", Version = 2 } // no handler
		};
		var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

		await registration.InlineApply!(events, context, services, CancellationToken.None);

		var result = store.Get("order-1");
		result.ShouldNotBeNull();
		result.Total.ShouldBe(50m);
		result.ShippedAt.ShouldBeNull(); // unhandled, not applied
	}

	// --- Null guards ---

	[Fact]
	public void ThrowOnNullFactory()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.WhenCreating<TestOrderPlaced>(null!));
	}

	[Fact]
	public void ThrowOnNullTransform()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.WhenTransforming<TestOrderShipped>(null!));
	}

	[Fact]
	public void ThrowOnNullAssembly()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.AddImmutableProjectionHandlersFromAssembly(null!));
	}

	[Fact]
	public void ThrowOnBuildWithNullRegistry()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		Should.Throw<ArgumentNullException>(() => builder.Build(null!));
	}

	// --- Fluent chaining ---

	[Fact]
	public void SupportFluentChaining()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		var result = builder
			.Inline()
			.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null))
			.WhenTransforming<TestOrderShipped>((c, e) => c with { ShippedAt = e.ShippedAt })
			.WithCacheTtl(TimeSpan.FromMinutes(5));
		result.ShouldBeSameAs(builder);
	}

	// --- C.10: Assembly scanning ---

	[Fact]
	public void DiscoverImmutableHandlersFromAssembly()
	{
		var services = new ServiceCollection();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(services);
		builder.Inline();
		builder.AddImmutableProjectionHandlersFromAssembly(typeof(TestImmutableHandler).Assembly);
		builder.Build(new InMemoryProjectionRegistry());

		// Handler should be registered in DI
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestImmutableHandler) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	[Fact]
	public void NoOpWhenAssemblyHasNoImmutableHandlers()
	{
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.AddImmutableProjectionHandlersFromAssembly(typeof(object).Assembly);
		var registry = new InMemoryProjectionRegistry();
		builder.Build(registry);

		// No handlers found, no exception
		var reg = registry.GetRegistration(typeof(OrderRecord))!;
		reg.ShouldNotBeNull();
	}

}

/// <summary>
/// Test immutable projection record (must be public for FakeItEasy proxy generation).
/// </summary>
public sealed record OrderRecord(decimal Total, DateTimeOffset? ShippedAt);

/// <summary>
/// Test immutable handler for assembly scanning discovery.
/// </summary>
public sealed class TestImmutableHandler : IImmutableProjectionHandler<OrderRecord, TestOrderPlaced>
{
	public Task<OrderRecord> TransformAsync(
		OrderRecord? current,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(new OrderRecord(@event.Amount, null));
	}
}
