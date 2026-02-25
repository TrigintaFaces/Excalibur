// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.EventSourcingStubs;

/// <summary>Non-generic projection store interface stub for integration tests.</summary>
public interface IProjectionStore
{
	/// <summary>Gets a read model by type, projection name, and ID.</summary>
	Task<T?> GetReadModelAsync<T>(string projectionName, string id, CancellationToken cancellationToken = default) where T : class;

	/// <summary>Saves a read model.</summary>
	Task SaveReadModelAsync<T>(string projectionName, string id, T model, CancellationToken cancellationToken = default) where T : class;

	/// <summary>Deletes a read model.</summary>
	Task DeleteReadModelAsync(string projectionName, string id, CancellationToken cancellationToken = default);
}

/// <summary>In-memory implementation of IProjectionStore for testing.</summary>
public class InMemoryProjectionStore : IProjectionStore
{
	private readonly Dictionary<string, object> _store = new();

	/// <inheritdoc/>
	public Task<T?> GetReadModelAsync<T>(string projectionName, string id, CancellationToken cancellationToken = default) where T : class
	{
		var key = $"{projectionName}:{id}";
		return Task.FromResult(_store.TryGetValue(key, out var value) ? value as T : null);
	}

	/// <inheritdoc/>
	public Task SaveReadModelAsync<T>(string projectionName, string id, T model, CancellationToken cancellationToken = default) where T : class
	{
		var key = $"{projectionName}:{id}";
		_store[key] = model;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeleteReadModelAsync(string projectionName, string id, CancellationToken cancellationToken = default)
	{
		var key = $"{projectionName}:{id}";
		_ = _store.Remove(key);
		return Task.CompletedTask;
	}
}

/// <summary>Projection cache invalidator interface stub.</summary>
public interface IProjectionCacheInvalidator
{
	/// <summary>Invalidates a specific projection.</summary>
	Task InvalidateAsync(string projectionName, string id, CancellationToken cancellationToken = default);

	/// <summary>Invalidates all projections of a given name.</summary>
	Task InvalidateAllAsync(string projectionName, CancellationToken cancellationToken = default);
}

/// <summary>In-memory inbox store stub for stress tests.</summary>
public class InMemoryInboxStore
{
	private readonly HashSet<string> _processedMessages = new();

	/// <summary>Checks if a message has been processed.</summary>
	public Task<bool> HasBeenProcessedAsync(string messageId, CancellationToken cancellationToken = default)
		=> Task.FromResult(_processedMessages.Contains(messageId));

	/// <summary>Marks a message as processed.</summary>
	public Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
	{
		_ = _processedMessages.Add(messageId);
		return Task.CompletedTask;
	}
}

/// <summary>Statistical summary for performance tests.</summary>
public class StatisticalSummary
{
	/// <summary>Gets or sets the mean.</summary>
	public double Mean { get; set; }

	/// <summary>Gets or sets the median.</summary>
	public double Median { get; set; }

	/// <summary>Gets or sets the standard deviation.</summary>
	public double StdDev { get; set; }

	/// <summary>Gets or sets the min value.</summary>
	public double Min { get; set; }

	/// <summary>Gets or sets the max value.</summary>
	public double Max { get; set; }

	/// <summary>Gets or sets the 95th percentile.</summary>
	public double P95 { get; set; }

	/// <summary>Gets or sets the 99th percentile.</summary>
	public double P99 { get; set; }

	/// <summary>Gets or sets the sample count.</summary>
	public int Count { get; set; }
}

/// <summary>Metric comparison for performance tests.</summary>
public class MetricComparison
{
	/// <summary>Gets or sets the metric name.</summary>
	public string MetricName { get; set; } = string.Empty;

	/// <summary>Gets or sets the baseline value.</summary>
	public double Baseline { get; set; }

	/// <summary>Gets or sets the current value.</summary>
	public double Current { get; set; }

	/// <summary>Gets or sets the percentage change.</summary>
	public double PercentageChange { get; set; }

	/// <summary>Gets whether this is a regression.</summary>
	public bool IsRegression { get; set; }
}

/// <summary>Generic aggregate root interface for event sourcing tests.</summary>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public interface IAggregateRoot<TId>
{
	/// <summary>Gets the aggregate identifier.</summary>
	TId Id { get; }

	/// <summary>Gets the current version of the aggregate.</summary>
	int Version { get; }

	/// <summary>Gets the uncommitted events raised by this aggregate.</summary>
	IReadOnlyList<object> GetUncommittedEvents();

	/// <summary>Marks all uncommitted events as committed.</summary>
	void MarkEventsAsCommitted();
}

/// <summary>Base class for aggregate root implementations in tests.</summary>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public abstract class AggregateRootBase<TId> : IAggregateRoot<TId>
{
	private readonly List<object> _uncommittedEvents = new();

	/// <inheritdoc/>
	public abstract TId Id { get; protected set; }

	/// <inheritdoc/>
	public int Version { get; protected set; }

	/// <inheritdoc/>
	public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

	/// <inheritdoc/>
	public void MarkEventsAsCommitted()
	{
		Version += _uncommittedEvents.Count;
		_uncommittedEvents.Clear();
	}

	/// <summary>Raises a domain event.</summary>
	protected void RaiseEvent(object @event)
	{
		_uncommittedEvents.Add(@event);
		ApplyEvent(@event);
	}

	/// <summary>Applies an event to update aggregate state.</summary>
	protected abstract void ApplyEvent(object @event);
}
