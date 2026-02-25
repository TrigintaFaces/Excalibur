// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots.InMemory;

/// <summary>
/// In-memory implementation of <see cref="ISnapshotStore"/> for testing scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by (aggregateType, aggregateId)
/// to store snapshots in memory. Thread-safe for concurrent access.
/// </para>
/// <para>
/// This implementation is intended for unit and integration testing only.
/// Data is not persisted across process restarts.
/// </para>
/// </remarks>
public sealed class InMemorySnapshotStore : ISnapshotStore
{
	private readonly ConcurrentDictionary<(string AggregateType, string AggregateId), ISnapshot> _snapshots = new();

	/// <inheritdoc />
	public ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		_snapshots.TryGetValue((aggregateType, aggregateId), out var snapshot);
		return new ValueTask<ISnapshot?>(snapshot);
	}

	/// <inheritdoc />
	public ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var key = (snapshot.AggregateType, snapshot.AggregateId);
		_snapshots.AddOrUpdate(key, snapshot, (_, existing) =>
			snapshot.Version >= existing.Version ? snapshot : existing);

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		_snapshots.TryRemove((aggregateType, aggregateId), out _);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var key = (aggregateType, aggregateId);
		if (_snapshots.TryGetValue(key, out var existing) && existing.Version < olderThanVersion)
		{
			_snapshots.TryRemove(key, out _);
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Clears all stored snapshots. Useful for test teardown.
	/// </summary>
	public void Clear() => _snapshots.Clear();

	/// <summary>
	/// Gets the count of stored snapshots.
	/// </summary>
	/// <value>The number of snapshots currently stored.</value>
	public int Count => _snapshots.Count;
}
