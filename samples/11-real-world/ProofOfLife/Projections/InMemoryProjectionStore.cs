// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using Excalibur.EventSourcing.Abstractions;

namespace ProofOfLife.Projections;

/// <summary>
/// Simple in-memory projection store for the proof-of-life sample.
/// Demonstrates how consumers can implement <see cref="IProjectionStore{T}"/>
/// for their own storage backends.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
public sealed class InMemoryProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ConcurrentDictionary<string, TProjection> _store = new(StringComparer.Ordinal);

	/// <inheritdoc/>
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		_store.TryGetValue(id, out var projection);
		return Task.FromResult(projection);
	}

	/// <inheritdoc/>
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		_store[id] = projection;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		_store.TryRemove(id, out _);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		// Simple implementation: return all projections (filters not needed for demo)
		IReadOnlyList<TProjection> result = _store.Values.ToList();
		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		return Task.FromResult((long)_store.Count);
	}
}
