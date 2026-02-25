// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Storage;

/// <summary>
/// In-memory implementation of <see cref="ISagaTimeoutStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> for thread-safe
/// storage with an additional lock for consistent reads in <see cref="GetDueTimeoutsAsync"/>.
/// </para>
/// <para>
/// <b>Warning:</b> Timeouts are lost on process restart. Use a persistent implementation
/// (e.g., SQL Server, Redis) for production deployments.
/// </para>
/// </remarks>
public sealed class InMemorySagaTimeoutStore : ISagaTimeoutStore
{
	private readonly ConcurrentDictionary<string, SagaTimeout> _timeouts = new();
#if NET9_0_OR_GREATER

	private readonly Lock _dueLock = new();

#else
	private readonly object _dueLock = new();

#endif

	/// <inheritdoc />
	public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timeout);

		lock (_dueLock)
		{
			_timeouts[timeout.TimeoutId] = timeout;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		// Cancellation is idempotent - no error if not found
		lock (_dueLock)
		{
			_ = _timeouts.TryRemove(timeoutId, out _);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);

		// Lock for consistent read-then-remove (same as GetDueTimeoutsAsync)
		lock (_dueLock)
		{
			var keysToRemove = _timeouts
				.Where(kvp => kvp.Value.SagaId == sagaId)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				_ = _timeouts.TryRemove(key, out _);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		// Lock to ensure consistent snapshot for ordering
		lock (_dueLock)
		{
			var due = _timeouts.Values
				.Where(t => t.DueAt <= asOf)
				.OrderBy(t => t.DueAt)
				.ToList();

			return Task.FromResult<IReadOnlyList<SagaTimeout>>(due);
		}
	}

	/// <inheritdoc />
	public Task MarkDeliveredAsync(string timeoutId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		// Mark delivered = remove from pending (idempotent)
		lock (_dueLock)
		{
			_ = _timeouts.TryRemove(timeoutId, out _);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the count of pending timeouts. Used for testing.
	/// </summary>
	/// <returns>The number of pending timeouts.</returns>
	public int GetPendingCount()
	{
		lock (_dueLock)
		{
			return _timeouts.Count;
		}
	}

	/// <summary>
	/// Clears all pending timeouts. Used for testing cleanup.
	/// </summary>
	public void Clear()
	{
		lock (_dueLock)
		{
			_timeouts.Clear();
		}
	}
}
