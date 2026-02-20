// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// In-memory implementation of <see cref="IMongoDbCdcStateStore"/> for testing.
/// </summary>
public sealed class InMemoryMongoDbCdcStateStore : IMongoDbCdcStateStore
{
	private readonly ConcurrentDictionary<string, MongoDbCdcPosition> _positions = new();
	private readonly ConcurrentDictionary<string, MongoDbCdcStateEntry> _states = new();
	private volatile bool _disposed;

	/// <inheritdoc/>
	public Task<MongoDbCdcPosition> GetLastPositionAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		return Task.FromResult(
			_positions.TryGetValue(processorId, out var position)
				? position
				: MongoDbCdcPosition.Start);
	}

	/// <inheritdoc/>
	public Task SavePositionAsync(
		string processorId,
		MongoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		_positions[processorId] = position;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<MongoDbCdcStateEntry>> GetAllStatesAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var results = _states.Values
			.Where(s => s.ProcessorId == processorId)
			.OrderBy(s => s.Namespace)
			.ToList();

		return Task.FromResult<IReadOnlyList<MongoDbCdcStateEntry>>(results);
	}

	/// <inheritdoc/>
	public Task SaveStateAsync(
		MongoDbCdcStateEntry entry,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(entry);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.ProcessorId);

		var key = $"{entry.ProcessorId}:{entry.Namespace ?? "<null>"}";

		_ = _states.AddOrUpdate(
			key,
			_ =>
			{
				entry.UpdatedAt = DateTimeOffset.UtcNow;
				return entry;
			},
			(_, existing) =>
			{
				existing.ResumeToken = entry.ResumeToken;
				existing.LastEventTime = entry.LastEventTime;
				existing.UpdatedAt = DateTimeOffset.UtcNow;
				existing.EventCount += entry.EventCount;
				return existing;
			});

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task ClearStateAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		_ = _positions.TryRemove(processorId, out _);

		var keysToRemove = _states.Keys
			.Where(k => k.StartsWith($"{processorId}:", StringComparison.Ordinal))
			.ToList();

		foreach (var key in keysToRemove)
		{
			_ = _states.TryRemove(key, out _);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Clears all stored state (for testing).
	/// </summary>
	public void Clear()
	{
		_positions.Clear();
		_states.Clear();
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		var position = await GetLastPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return position.IsValid ? position.ToChangePosition() : null;
	}

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		var mongoPosition = MongoDbCdcPosition.FromChangePosition(position);
		return SavePositionAsync(consumerId, mongoPosition, cancellationToken);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await ClearStateAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await Task.CompletedTask.ConfigureAwait(false);

		foreach (var kvp in _positions)
		{
			if (kvp.Value.IsValid)
			{
				yield return (kvp.Key, kvp.Value.ToChangePosition());
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}
}
