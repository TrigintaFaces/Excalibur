// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// In-memory implementation of CDC state store for testing scenarios.
/// </summary>
/// <remarks>
/// This implementation stores positions in memory and is not persistent across restarts.
/// Use only for testing and development purposes.
/// </remarks>
public sealed class InMemoryCosmosDbCdcStateStore : ICosmosDbCdcStateStore
{
	private readonly ConcurrentDictionary<string, CosmosDbCdcStateEntry> _positions = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <inheritdoc/>
	public Task<CosmosDbCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_positions.TryGetValue(processorName, out var entry) &&
			!string.IsNullOrEmpty(entry.PositionData))
		{
			if (CosmosDbCdcPosition.TryFromBase64(entry.PositionData, out var position))
			{
				return Task.FromResult<CosmosDbCdcPosition?>(position);
			}
		}

		return Task.FromResult<CosmosDbCdcPosition?>(null);
	}

	/// <inheritdoc/>
	public Task SavePositionAsync(
		string processorName,
		CosmosDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var entry = _positions.AddOrUpdate(
			processorName,
			_ => new CosmosDbCdcStateEntry
			{
				ProcessorName = processorName,
				PositionData = position.ToBase64(),
				UpdatedAt = DateTimeOffset.UtcNow,
				EventCount = 0,
			},
			(_, existing) =>
			{
				existing.PositionData = position.ToBase64();
				existing.UpdatedAt = DateTimeOffset.UtcNow;
				existing.EventCount++;
				return existing;
			});

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		_ = _positions.TryRemove(processorName, out _);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets all stored positions.
	/// </summary>
	/// <returns>A read-only dictionary of all positions.</returns>
	public IReadOnlyDictionary<string, CosmosDbCdcStateEntry> GetAllPositions()
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

		if (position is not CosmosDbCdcPosition cosmosPosition)
		{
			cosmosPosition = CosmosDbCdcPosition.FromContinuationToken(position.ToToken());
		}

		return SavePositionAsync(consumerId, cosmosPosition, cancellationToken);
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
			if (CosmosDbCdcPosition.TryFromBase64(kvp.Value.PositionData, out var position))
			{
				yield return (kvp.Key, position);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_positions.Clear();
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_positions.Clear();
	}
}
