// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// In-memory implementation of <see cref="IPostgresCdcStateStore"/> for testing
/// or single-instance deployments.
/// </summary>
/// <remarks>
/// <para>
/// This implementation does not persist state across process restarts.
/// For production multi-instance deployments, use <see cref="PostgresCdcStateStore"/>.
/// </para>
/// </remarks>
public sealed class InMemoryPostgresCdcStateStore : IPostgresCdcStateStore
{
	private readonly ConcurrentDictionary<string, PostgresCdcStateEntry> _states = new();

	/// <inheritdoc/>
	public Task<PostgresCdcPosition> GetLastPositionAsync(
		string processorId,
		string slotName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		var key = GetKey(processorId, slotName, null);

		if (_states.TryGetValue(key, out var entry) &&
			PostgresCdcPosition.TryParse(entry.Position, out var position))
		{
			return Task.FromResult(position);
		}

		return Task.FromResult(PostgresCdcPosition.Start);
	}

	/// <inheritdoc/>
	public Task SavePositionAsync(
		string processorId,
		string slotName,
		PostgresCdcPosition position,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

		var key = GetKey(processorId, slotName, null);
		var entry = new PostgresCdcStateEntry
		{
			ProcessorId = processorId,
			SlotName = slotName,
			TableName = null,
			Position = position.LsnString,
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		_ = _states.AddOrUpdate(key, entry, (_, existing) =>
		{
			existing.Position = position.LsnString;
			existing.UpdatedAt = DateTimeOffset.UtcNow;
			return existing;
		});

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<PostgresCdcStateEntry>> GetAllStatesAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var entries = _states.Values
			.Where(e => e.ProcessorId == processorId)
			.OrderBy(e => e.TableName)
			.ToList();

		return Task.FromResult<IReadOnlyList<PostgresCdcStateEntry>>(entries);
	}

	/// <inheritdoc/>
	public Task SaveStateAsync(
		PostgresCdcStateEntry entry,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(entry);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.ProcessorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.SlotName);

		var key = GetKey(entry.ProcessorId, entry.SlotName, entry.TableName);

		_ = _states.AddOrUpdate(key, entry, (_, existing) =>
		{
			existing.Position = entry.Position;
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
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var keysToRemove = _states.Keys
			.Where(k => k.StartsWith($"{processorId}:", StringComparison.Ordinal))
			.ToList();

		foreach (var key in keysToRemove)
		{
			_ = _states.TryRemove(key, out _);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		var position = await GetLastPositionAsync(consumerId, "default", cancellationToken).ConfigureAwait(false);
		return position.IsValid ? position.ToChangePosition() : null;
	}

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		var pgPosition = PostgresCdcPosition.FromChangePosition(position);
		return SavePositionAsync(consumerId, "default", pgPosition, cancellationToken);
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

		foreach (var entry in _states.Values)
		{
			if (entry.TableName is null &&
				PostgresCdcPosition.TryParse(entry.Position, out var position) &&
				position.IsValid)
			{
				yield return (entry.ProcessorId, position.ToChangePosition());
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// No resources to dispose
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		// No resources to dispose
		return ValueTask.CompletedTask;
	}

	private static string GetKey(string processorId, string slotName, string? tableName)
	{
		return $"{processorId}:{slotName}:{tableName ?? string.Empty}";
	}
}
