// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// C.9 (t5wdic): Integration tests for E2E immutable inline projection
/// through the full notification pipeline.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ImmutableProjectionIntegrationShould
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
		IServiceProvider sp) =>
		new(
			processor, sp,
			Options.Create(new EventNotificationOptions()),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());

	[Fact]
	public async Task CreateAndTransformImmutableProjectionThroughPipeline()
	{
		// Arrange
		var store = new InMemoryProjectionStore<OrderRecord>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null));
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 300m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(version: 2), CancellationToken.None);

		// Assert
		var result = await store.GetByIdAsync("order-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Total.ShouldBe(300m);
		result.ShippedAt.ShouldBe(shipped);
	}

	[Fact]
	public async Task ThrowOnTransformWithoutCreate()
	{
		// Arrange -- only WhenTransforming, no WhenCreating
		var store = new InMemoryProjectionStore<OrderRecord>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", Version = 1 }
		};

		// Act & Assert -- Q1: null + Transforming = throw, wrapped in AggregateException by broker
		var ex = await Should.ThrowAsync<AggregateException>(
			() => broker.NotifyAsync(events, CreateContext(), CancellationToken.None));
		ex.InnerExceptions.OfType<InvalidOperationException>().ShouldNotBeEmpty();
	}

	[Fact]
	public async Task HandleDiResolvedImmutableHandler()
	{
		// Arrange -- WhenHandledBy with DI-resolved handler
		var store = new InMemoryProjectionStore<OrderRecord>();
		var registry = new InMemoryProjectionRegistry();

		var services = new ServiceCollection();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(services);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, TestImmutableHandler>();
		builder.Build(registry);

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.AddTransient<TestImmutableHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 777m, Version = 1 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None);

		// Assert
		var result = await store.GetByIdAsync("order-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Total.ShouldBe(777m);
	}

	[Fact]
	public async Task MergeWithExistingImmutableState()
	{
		// Arrange -- pre-seed store with existing immutable record
		var store = new InMemoryProjectionStore<OrderRecord>();
		await store.UpsertAsync("order-1", new OrderRecord(100m, null), CancellationToken.None);

		var registry = new InMemoryProjectionRegistry();
		var builder = new ImmutableProjectionBuilder<OrderRecord>(new ServiceCollection());
		builder.Inline();
		builder.WhenTransforming<TestOrderShipped>((current, e) =>
			current with { ShippedAt = e.ShippedAt });
		builder.Build(registry);

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderRecord>>(store)
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(version: 2), CancellationToken.None);

		// Assert -- new record with ShippedAt, Total preserved from original
		var result = await store.GetByIdAsync("order-1", CancellationToken.None);
		result.ShouldNotBeNull();
		result.Total.ShouldBe(100m);
		result.ShippedAt.ShouldBe(shipped);
	}
}
