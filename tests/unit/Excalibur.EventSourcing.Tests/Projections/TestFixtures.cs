// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Test projection state used across inline projection tests.
/// </summary>
internal sealed class OrderSummary
{
	public decimal Total { get; set; }
	public DateTimeOffset? ShippedAt { get; set; }
	public int EventCount { get; set; }
}

/// <summary>
/// Second projection type for concurrent projection tests.
/// </summary>
internal sealed class InventoryView
{
	public int Quantity { get; set; }
	public int EventCount { get; set; }
}

/// <summary>
/// Concrete test event for order placed.
/// </summary>
[MessageName("Test.TestOrderPlaced")]
public sealed class TestOrderPlaced : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 1;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
	public decimal Amount { get; init; } = 99.99m;
}

/// <summary>
/// Concrete test event for order shipped.
/// </summary>
[MessageName("Test.TestFixtures.TestOrderShipped")]
public sealed class TestOrderShipped : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 2;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
	public DateTimeOffset ShippedAt { get; init; } = DateTimeOffset.UtcNow;
}


/// <summary>A synchronous test accessor over the production positioned in-memory store.</summary>
internal sealed class TestProjectionStore<T> : IProjectionStore<T>
	where T : class
{
	private readonly Excalibur.EventSourcing.Projections.InMemoryProjectionStore<T> _store = new();

	public object? GetService(Type serviceType) => ((IServiceProvider)_store).GetService(serviceType);

	public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
		_store.GetByIdAsync(id, cancellationToken);

	// Test seeds represent an initial state before the numbered events, rather than an
	// unpositioned production write that a persisted async fold must refuse.
	public async Task UpsertAsync(string id, T projection, CancellationToken cancellationToken)
	{
		await _store.UpsertAsync(id, projection, cancellationToken);
		var rebuilt = await _store.RebuildAtPositionAsync(id, projection, 0, cancellationToken);
		rebuilt.Outcome.ShouldBe(ProjectionRebuildOutcome.Applied);
	}

	public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
		_store.DeleteAsync(id, cancellationToken);

	public Task<IReadOnlyList<T>> QueryAsync(IDictionary<string, object>? filters,
		QueryOptions? options, CancellationToken cancellationToken) =>
		_store.QueryAsync(filters, options, cancellationToken);

	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
		_store.CountAsync(filters, cancellationToken);

#pragma warning disable RS0030 // Production in-memory store completes synchronously; legacy test assertions use a synchronous accessor.
	internal T? Get(string id) => _store.GetByIdAsync(id, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
}

/// <summary>
/// Test event for order cancelled (used in handler resolution tests).
/// </summary>
[MessageName("Test.TestOrderCancelled")]
public sealed class TestOrderCancelled : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 3;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
	public string Reason { get; init; } = "Customer request";
}

/// <summary>
/// Sync handler for TestOrderPlaced that sets Total on OrderSummary.
/// Used by T.8 (handler resolution) and T.9 (assembly scanning) tests.
/// </summary>
internal sealed class OrderPlacedHandler : IProjectionEventHandler<OrderSummary, TestOrderPlaced>
{
	public Task HandleAsync(
		OrderSummary projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Total = @event.Amount;
		projection.EventCount++;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Async handler for TestOrderShipped that sets ShippedAt on OrderSummary.
/// </summary>
internal sealed class OrderShippedHandler : IProjectionEventHandler<OrderSummary, TestOrderShipped>
{
	private readonly ILogger<OrderShippedHandler> _logger;

	public OrderShippedHandler(ILogger<OrderShippedHandler> logger)
	{
		_logger = logger;
	}

	public Task HandleAsync(
		OrderSummary projection,
		TestOrderShipped @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.ShippedAt = @event.ShippedAt;
		projection.EventCount++;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler that overrides the projection ID via context.
/// </summary>
internal sealed class OrderCancelledWithOverrideHandler : IProjectionEventHandler<OrderSummary, TestOrderCancelled>
{
	public Task HandleAsync(
		OrderSummary projection,
		TestOrderCancelled @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		context.OverrideProjectionId = "cancelled-" + context.AggregateId;
		projection.EventCount++;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test event for order failed (used in error path testing to avoid duplicate handler conflicts).
/// </summary>
[MessageName("Test.TestOrderFailed")]
public sealed class TestOrderFailed : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 4;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
	public string FailureReason { get; init; } = "Payment declined";
}

/// <summary>
/// Handler that throws an exception (for error path testing).
/// Handles TestOrderFailed to avoid duplicate detection when scanning assembly.
/// </summary>
internal sealed class ThrowingHandler : IProjectionEventHandler<OrderSummary, TestOrderFailed>
{
	public Task HandleAsync(
		OrderSummary projection,
		TestOrderFailed @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		throw new InvalidOperationException("Handler failed intentionally");
	}
}

/// <summary>
/// Projection type used exclusively for duplicate detection testing (D3).
/// Has two handlers in the same assembly: DuplicateTestHandlerA and DuplicateTestHandlerB.
/// </summary>
internal sealed class DuplicateTestProjection
{
	public int Count { get; set; }
}

/// <summary>
/// First handler for (DuplicateTestProjection, TestOrderPlaced) -- duplicate detection target.
/// </summary>
internal sealed class DuplicateTestHandlerA : IProjectionEventHandler<DuplicateTestProjection, TestOrderPlaced>
{
	public Task HandleAsync(
		DuplicateTestProjection projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Count++;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Second handler for (DuplicateTestProjection, TestOrderPlaced) -- triggers duplicate detection.
/// </summary>
internal sealed class DuplicateTestHandlerB : IProjectionEventHandler<DuplicateTestProjection, TestOrderPlaced>
{
	public Task HandleAsync(
		DuplicateTestProjection projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Count += 10;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler for a different projection type -- should be ignored by scanning for OrderSummary.
/// </summary>
internal sealed class InventoryEventHandler : IProjectionEventHandler<InventoryView, TestOrderPlaced>
{
	public Task HandleAsync(
		InventoryView projection,
		TestOrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Quantity++;
		return Task.CompletedTask;
	}
}
