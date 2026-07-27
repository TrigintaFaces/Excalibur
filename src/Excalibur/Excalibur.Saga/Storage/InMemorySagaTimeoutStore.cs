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
/// storage with an additional lock for consistent reads and atomic claims in
/// <see cref="ClaimDueTimeoutsAsync"/>.
/// </para>
/// <para>
/// <b>Warning:</b> Timeouts are lost on process restart. Use a persistent implementation
/// (e.g., SQL Server, Redis) for production deployments.
/// </para>
/// </remarks>
internal sealed class InMemorySagaTimeoutStore : ISagaTimeoutStore
{
	private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(120);

	private readonly ConcurrentDictionary<string, SagaTimeout> _timeouts = new();
	private readonly Dictionary<string, DateTimeOffset> _claims = new(StringComparer.Ordinal);
	private readonly Lock _dueLock = new();

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
			_ = _claims.Remove(timeoutId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);

		// Lock for consistent read-then-remove (same as ClaimDueTimeoutsAsync)
		lock (_dueLock)
		{
			var keysToRemove = _timeouts
				.Where(kvp => kvp.Value.SagaId == sagaId)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				_ = _timeouts.TryRemove(key, out _);
				_ = _claims.Remove(key);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		// Atomically select+claim under a single lock: two concurrent callers can never observe
		// (and therefore never claim) the same due-and-unclaimed-or-stale timeout, because the
		// claim map is updated before the lock is released.
		lock (_dueLock)
		{
			var claimed = _timeouts.Values
				.Where(t => t.DueAt <= asOf
					&& (!_claims.TryGetValue(t.TimeoutId, out var claimedAt) || claimedAt + LeaseTimeout < asOf))
				.OrderBy(t => t.DueAt)
				.Take(batchSize)
				.ToList();

			foreach (var timeout in claimed)
			{
				_claims[timeout.TimeoutId] = asOf;
			}

			return Task.FromResult<IReadOnlyList<SagaTimeout>>(claimed);
		}
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		// Pure read snapshot: no claim/lease is recorded, unlike ClaimDueTimeoutsAsync.
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
			_ = _claims.Remove(timeoutId);
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
