// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

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
public sealed class TestOrderPlaced : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 1;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(TestOrderPlaced);
	public IDictionary<string, object>? Metadata { get; init; }
	public decimal Amount { get; init; } = 99.99m;
}

/// <summary>
/// Concrete test event for order shipped.
/// </summary>
public sealed class TestOrderShipped : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 2;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(TestOrderShipped);
	public IDictionary<string, object>? Metadata { get; init; }
	public DateTimeOffset ShippedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// In-memory projection store for testing without external dependencies.
/// </summary>
internal sealed class InMemoryProjectionStore<T> : IProjectionStore<T>
	where T : class
{
	private readonly Dictionary<string, T> _store = new();

	public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		_store.TryGetValue(id, out var result);
		return Task.FromResult(result);
	}

	public Task UpsertAsync(string id, T projection, CancellationToken cancellationToken)
	{
		_store[id] = projection;
		return Task.CompletedTask;
	}

	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		_store.Remove(id);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<T>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<T>>(_store.Values.ToList());
	}

	public Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		return Task.FromResult((long)_store.Count);
	}

	/// <summary>
	/// Test helper: get stored projection directly.
	/// </summary>
	internal T? Get(string id) => _store.TryGetValue(id, out var v) ? v : null;
}

/// <summary>
/// Test event for order cancelled (used in handler resolution tests).
/// </summary>
public sealed class TestOrderCancelled : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 3;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(TestOrderCancelled);
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
public sealed class TestOrderFailed : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "order-1";
	public long Version { get; init; } = 4;
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(TestOrderFailed);
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
