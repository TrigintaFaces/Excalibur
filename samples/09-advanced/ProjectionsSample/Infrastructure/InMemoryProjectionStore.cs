// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using Excalibur.EventSourcing.Abstractions;

namespace ProjectionsSample.Infrastructure;

// ============================================================================
// In-Memory Projection Store
// ============================================================================
// A simple in-memory implementation of IProjectionStore<T> for demonstration.
// In production, you would use SqlServerProjectionStore, PostgresProjectionStore,
// MongoDbProjectionStore, CosmosDbProjectionStore, or ElasticSearchProjectionStore.

/// <summary>
/// In-memory implementation of <see cref="IProjectionStore{TProjection}"/> for demonstration.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores projections in a ConcurrentDictionary for thread-safe
/// access. It's suitable for demos and testing, but not for production use where
/// persistence is required.
/// </para>
/// <para>
/// For production, use one of the database-backed implementations:
/// <list type="bullet">
/// <item><c>SqlServerProjectionStore</c> - SQL Server with JSON columns</item>
/// <item><c>PostgresProjectionStore</c> - PostgreSQL with JSONB columns</item>
/// <item><c>MongoDbProjectionStore</c> - MongoDB document store</item>
/// <item><c>CosmosDbProjectionStore</c> - Azure Cosmos DB</item>
/// <item><c>ElasticSearchProjectionStore</c> - Elasticsearch for full-text search</item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type.</typeparam>
public sealed class InMemoryProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ConcurrentDictionary<string, TProjection> _store = new();
	private readonly Func<TProjection, string> _idSelector;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="idSelector">Function to extract the ID from a projection.</param>
	public InMemoryProjectionStore(Func<TProjection, string> idSelector)
	{
		_idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));
	}

	/// <summary>
	/// Gets all projections (for demonstration purposes).
	/// </summary>
	public IEnumerable<TProjection> GetAll() => _store.Values;

	/// <inheritdoc/>
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		_ = _store.TryGetValue(id, out var projection);
		return Task.FromResult(projection);
	}

	/// <inheritdoc/>
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		_ = _store.AddOrUpdate(id, projection, (_, _) => projection);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		_ = _store.TryRemove(id, out _);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		IEnumerable<TProjection> results = _store.Values;

		// Apply filters (simplified - production stores support full operator syntax)
		if (filters != null)
		{
			foreach (var (key, value) in filters)
			{
				var propertyName = key.Split(':')[0];
				var property = typeof(TProjection).GetProperty(propertyName);
				if (property != null)
				{
					results = results.Where(p =>
					{
						var propValue = property.GetValue(p);
						return propValue?.Equals(value) == true;
					});
				}
			}
		}

		// Apply ordering
		if (options?.OrderBy != null)
		{
			var property = typeof(TProjection).GetProperty(options.OrderBy);
			if (property != null)
			{
				results = options.Descending
					? results.OrderByDescending(p => property.GetValue(p))
					: results.OrderBy(p => property.GetValue(p));
			}
		}

		// Apply pagination
		if (options?.Skip > 0)
		{
			results = results.Skip(options.Skip.Value);
		}

		if (options?.Take > 0)
		{
			results = results.Take(options.Take.Value);
		}

		return Task.FromResult<IReadOnlyList<TProjection>>(results.ToList());
	}

	/// <inheritdoc/>
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		if (filters == null || filters.Count == 0)
		{
			return Task.FromResult((long)_store.Count);
		}

		// Apply filters and count
		IEnumerable<TProjection> results = _store.Values;
		foreach (var (key, value) in filters)
		{
			var propertyName = key.Split(':')[0];
			var property = typeof(TProjection).GetProperty(propertyName);
			if (property != null)
			{
				results = results.Where(p =>
				{
					var propValue = property.GetValue(p);
					return propValue?.Equals(value) == true;
				});
			}
		}

		return Task.FromResult((long)results.Count());
	}
}
