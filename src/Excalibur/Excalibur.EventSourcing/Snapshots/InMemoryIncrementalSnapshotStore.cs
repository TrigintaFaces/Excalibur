// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// In-memory implementation of <see cref="IIncrementalSnapshotStore{TState}"/>
/// for development and testing.
/// </summary>
/// <typeparam name="TState">The aggregate state type.</typeparam>
/// <remarks>
/// <para>
/// Stores base snapshots and deltas in memory. Deltas are ordered by version
/// and merged on load. Full snapshots replace the base and clear deltas.
/// </para>
/// <para>
/// Uses JSON serialization for deep-copy semantics to prevent shared mutable state.
/// </para>
/// </remarks>
internal sealed class InMemoryIncrementalSnapshotStore<TState> : IIncrementalSnapshotStore<TState>
	where TState : class
{
	private readonly ConcurrentDictionary<string, SnapshotEntry> _snapshots = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<TState?> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var key = MakeKey(aggregateId, aggregateType);

		if (!_snapshots.TryGetValue(key, out var entry))
		{
			return Task.FromResult<TState?>(null);
		}

		// Return a deep copy of the base state (deltas are already merged into base on save)
		var result = DeepCopy(entry.BaseState);
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task SaveDeltaAsync(
		string aggregateId,
		string aggregateType,
		TState delta,
		long version,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(delta);

		var key = MakeKey(aggregateId, aggregateType);
		var deltaCopy = DeepCopy(delta)!;

		_snapshots.AddOrUpdate(
			key,
			_ => new SnapshotEntry(deltaCopy, version, 1),
			(_, existing) => existing with
			{
				// In-memory: for simplicity, the "base" always holds latest state
				// Real providers store separate base + delta rows
				BaseState = deltaCopy,
				Version = version,
				DeltaCount = existing.DeltaCount + 1,
			});

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task SaveFullAsync(
		string aggregateId,
		string aggregateType,
		TState state,
		long version,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(state);

		var key = MakeKey(aggregateId, aggregateType);
		var stateCopy = DeepCopy(state)!;

		// Full snapshot: reset delta count (compaction)
		_snapshots[key] = new SnapshotEntry(stateCopy, version, 0);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the current delta count for an aggregate (for testing).
	/// </summary>
	internal int GetDeltaCount(string aggregateId, string aggregateType)
	{
		var key = MakeKey(aggregateId, aggregateType);
		return _snapshots.TryGetValue(key, out var entry) ? entry.DeltaCount : 0;
	}

	private static string MakeKey(string aggregateId, string aggregateType)
		=> $"{aggregateType}:{aggregateId}";

	private static TState? DeepCopy(TState? state)
	{
		if (state is null)
		{
			return null;
		}

		var json = JsonSerializer.SerializeToUtf8Bytes(state);
		return JsonSerializer.Deserialize<TState>(json);
	}

	private sealed record SnapshotEntry(TState BaseState, long Version, int DeltaCount);
}
