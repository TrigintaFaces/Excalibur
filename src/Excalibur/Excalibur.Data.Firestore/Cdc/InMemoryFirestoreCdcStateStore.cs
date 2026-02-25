// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// In-memory state store for Firestore CDC position tracking.
/// </summary>
/// <remarks>
/// This implementation is intended for testing and development.
/// Positions are not persisted and will be lost when the process exits.
/// </remarks>
public sealed class InMemoryFirestoreCdcStateStore : IFirestoreCdcStateStore
{
	private readonly ConcurrentDictionary<string, FirestoreCdcStateEntry> _positions = new();
	private volatile bool _disposed;

	/// <inheritdoc/>
	public Task<FirestoreCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		if (!_positions.TryGetValue(processorName, out var entry))
		{
			return Task.FromResult<FirestoreCdcPosition?>(null);
		}

		if (!FirestoreCdcPosition.TryFromBase64(entry.PositionData, out var position))
		{
			return Task.FromResult<FirestoreCdcPosition?>(null);
		}

		return Task.FromResult(position);
	}

	/// <inheritdoc/>
	public Task SavePositionAsync(
		string processorName,
		FirestoreCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);

		var entry = new FirestoreCdcStateEntry
		{
			ProcessorName = processorName,
			PositionData = position.ToBase64(),
			UpdatedAt = DateTimeOffset.UtcNow,
			EventCount = 0,
		};

		_positions[processorName] = entry;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		_ = _positions.TryRemove(processorName, out _);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets all stored positions.
	/// </summary>
	/// <returns>A read-only dictionary of processor names to state entries.</returns>
	public IReadOnlyDictionary<string, FirestoreCdcStateEntry> GetAllPositions()
	{
		return _positions;
	}

	/// <summary>
	/// Clears all stored positions.
	/// </summary>
	public void Clear()
	{
		_positions.Clear();
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not FirestoreCdcPosition firestorePosition)
		{
			firestorePosition = FirestoreCdcPosition.FromBase64(position.ToToken());
		}

		return SavePositionAsync(consumerId, firestorePosition, cancellationToken);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await DeletePositionAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await Task.CompletedTask.ConfigureAwait(false);

		foreach (var kvp in _positions)
		{
			if (FirestoreCdcPosition.TryFromBase64(kvp.Value.PositionData, out var position) && position is not null)
			{
				yield return (kvp.Key, position);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_disposed = true;
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_disposed = true;
	}
}
