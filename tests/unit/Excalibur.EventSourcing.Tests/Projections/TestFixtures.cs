// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

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
