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
/// T.10 (022si3): Integration tests exercising end-to-end inline projection with
/// DI-resolved handlers through the full notification pipeline.
/// Validates: handler receives correct context, OverrideProjectionId works,
/// NotificationFailurePolicy.Propagate throws on handler error.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class InlineProjectionHandlerIntegrationShould
{
	private static EventNotificationContext CreateContext(
		string aggregateId = "order-1",
		string aggregateType = "Order",
		long version = 1) =>
		new(aggregateId, aggregateType, version, DateTimeOffset.UtcNow);

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

	[Fact]
	public async Task ResolveHandlerFromDiThroughFullPipeline()
	{
		// Arrange -- full DI pipeline with WhenHandledBy handler
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 500m, Version = 1 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None);

		// Assert
		var projected = await store.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(500m);
		projected.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task MixLambdaAndDiHandlersThroughPipeline()
	{
		// Arrange -- When<T> lambda + WhenHandledBy in same projection
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddSingleton<ILogger<OrderShippedHandler>>(NullLogger<OrderShippedHandler>.Instance)
			.AddTransient<OrderShippedHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderPlaced { AggregateId = "order-1", Amount = 200m, Version = 1 },
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 2 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(version: 2), CancellationToken.None);

		// Assert -- both handler types applied
		var projected = await store.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(200m);
		projected.ShippedAt.ShouldBe(shipped);
		projected.EventCount.ShouldBe(2);
	}

	[Fact]
	public async Task ApplyOverrideProjectionIdThroughPipeline()
	{
		// Arrange -- handler sets OverrideProjectionId
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderCancelled, OrderCancelledWithOverrideHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderCancelledWithOverrideHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderCancelled { AggregateId = "order-42", Version = 3 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext("order-42", version: 3), CancellationToken.None);

		// Assert -- both default and overridden projections created
		var defaultProjection = await store.GetByIdAsync("order-42", CancellationToken.None);
		defaultProjection.ShouldNotBeNull();

		var overriddenProjection = await store.GetByIdAsync("cancelled-order-42", CancellationToken.None);
		overriddenProjection.ShouldNotBeNull();
		overriddenProjection.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateHandlerExceptionWithPropagatePolicy()
	{
		// Arrange -- NotificationFailurePolicy.Propagate throws on handler error (D2)
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderFailed, ThrowingHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<ThrowingHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp,
			new EventNotificationOptions { FailurePolicy = NotificationFailurePolicy.Propagate });

		var events = new List<IDomainEvent>
		{
			new TestOrderFailed { AggregateId = "order-1", Version = 1 }
		};

		// Act & Assert -- broker wraps projection errors in AggregateException
		var ex = await Should.ThrowAsync<AggregateException>(
			() => broker.NotifyAsync(events, CreateContext(), CancellationToken.None));
		ex.InnerExceptions.Count.ShouldBeGreaterThan(0);
		var inner = ex.InnerExceptions.OfType<InvalidOperationException>().FirstOrDefault();
		inner.ShouldNotBeNull();
		inner.Message.ShouldContain("Handler failed intentionally");
	}

	[Fact]
	public async Task LogAndContinueOnHandlerExceptionWithLogAndContinuePolicy()
	{
		// Arrange -- NotificationFailurePolicy.LogAndContinue suppresses handler errors
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderFailed, ThrowingHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<ThrowingHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp,
			new EventNotificationOptions { FailurePolicy = NotificationFailurePolicy.LogAndContinue });

		var events = new List<IDomainEvent>
		{
			new TestOrderFailed { AggregateId = "order-1", Version = 1 }
		};

		// Act -- should not throw
		await broker.NotifyAsync(events, CreateContext(), CancellationToken.None);

		// Assert -- projection not created (handler threw before persisting)
		var projected = await store.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldBeNull();
	}

	[Fact]
	public async Task PassCorrectContextToHandler()
	{
		// Arrange -- verify handler receives correct aggregate metadata
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderRefunded, ContextCapturingHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddSingleton(new ContextCapturingHandler.CapturedContext())
			.AddTransient<ContextCapturingHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var events = new List<IDomainEvent>
		{
			new TestOrderRefunded { AggregateId = "order-99", RefundAmount = 42m, Version = 7 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext("order-99", "SpecialOrder", 7), CancellationToken.None);

		// Assert -- handler received correct context
		var captured = sp.GetRequiredService<ContextCapturingHandler.CapturedContext>();
		captured.AggregateId.ShouldBe("order-99");
		captured.AggregateType.ShouldBe("SpecialOrder");
		captured.CommittedVersion.ShouldBe(7);
	}

	[Fact]
	public async Task CreateNewProjectionWhenNoneExists()
	{
		// Arrange -- empty store, handler should get a new TProjection()
		var store = new InMemoryProjectionStore<OrderSummary>();
		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddTransient<OrderPlacedHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced { AggregateId = "new-order", Amount = 77m, Version = 1 } },
			CreateContext("new-order"),
			CancellationToken.None);

		// Assert -- new projection created
		var projected = await store.GetByIdAsync("new-order", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(77m);
		projected.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task MergeWithExistingProjectionState()
	{
		// Arrange -- pre-seed store
		var store = new InMemoryProjectionStore<OrderSummary>();
		await store.UpsertAsync("order-1",
			new OrderSummary { Total = 100m, EventCount = 2 },
			CancellationToken.None);

		var registry = new InMemoryProjectionRegistry();

		var builder = new ProjectionBuilder<OrderSummary>(registry);
		builder.Inline();
		builder.WhenHandledBy<TestOrderShipped, OrderShippedHandler>();
		builder.Build();

		var sp = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(store)
			.AddSingleton<ILogger<OrderShippedHandler>>(NullLogger<OrderShippedHandler>.Instance)
			.AddTransient<OrderShippedHandler>()
			.BuildServiceProvider();

		var processor = CreateProcessor(registry, sp);
		var broker = CreateBroker(processor, sp);

		var shipped = DateTimeOffset.UtcNow;
		var events = new List<IDomainEvent>
		{
			new TestOrderShipped { AggregateId = "order-1", ShippedAt = shipped, Version = 3 }
		};

		// Act
		await broker.NotifyAsync(events, CreateContext(version: 3), CancellationToken.None);

		// Assert -- existing state preserved, new fields updated
		var projected = await store.GetByIdAsync("order-1", CancellationToken.None);
		projected.ShouldNotBeNull();
		projected.Total.ShouldBe(100m); // preserved
		projected.ShippedAt.ShouldBe(shipped); // updated
		projected.EventCount.ShouldBe(3); // incremented
	}
}

/// <summary>
/// Test event used exclusively by ContextCapturingHandler to avoid duplicate detection.
/// </summary>
public sealed class TestOrderRefunded : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 5;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(TestOrderRefunded);
	public IDictionary<string, object>? Metadata { get; init; }
	public decimal RefundAmount { get; init; } = 50m;
}

/// <summary>
/// Handler that captures context metadata for assertion in tests.
/// Uses TestOrderRefunded to avoid duplicate detection with OrderPlacedHandler.
/// </summary>
internal sealed class ContextCapturingHandler : IProjectionEventHandler<OrderSummary, TestOrderRefunded>
{
	private readonly CapturedContext _captured;

	public ContextCapturingHandler(CapturedContext captured)
	{
		_captured = captured;
	}

	public Task HandleAsync(
		OrderSummary projection,
		TestOrderRefunded @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		_captured.AggregateId = context.AggregateId;
		_captured.AggregateType = context.AggregateType;
		_captured.CommittedVersion = context.CommittedVersion;
		_captured.Timestamp = context.Timestamp;
		projection.Total = @event.RefundAmount;
		projection.EventCount++;
		return Task.CompletedTask;
	}

	internal sealed class CapturedContext
	{
		public string? AggregateId { get; set; }
		public string? AggregateType { get; set; }
		public long CommittedVersion { get; set; }
		public DateTimeOffset Timestamp { get; set; }
	}
}
