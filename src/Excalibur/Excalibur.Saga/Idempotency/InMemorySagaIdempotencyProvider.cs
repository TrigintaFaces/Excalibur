// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Saga.Idempotency;

/// <summary>
/// In-memory implementation of <see cref="ISagaIdempotencyProvider"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> to track processed keys.
/// It is NOT suitable for production use:
/// </para>
/// <list type="bullet">
/// <item><description>State is not persisted across application restarts.</description></item>
/// <item><description>Memory grows unbounded with no TTL-based eviction.</description></item>
/// <item><description>Not shared across multiple instances.</description></item>
/// </list>
/// <para>For production, use a persistent implementation backed by SQL Server, Redis, or similar.</para>
/// </remarks>
public sealed class InMemorySagaIdempotencyProvider : ISagaIdempotencyProvider
{
	private readonly ConcurrentDictionary<(string SagaId, string Key), byte> _processedKeys = new();

	/// <inheritdoc />
	public Task<bool> IsProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
		cancellationToken.ThrowIfCancellationRequested();

		var exists = _processedKeys.ContainsKey((sagaId, idempotencyKey));
		return Task.FromResult(exists);
	}

	/// <inheritdoc />
	public Task MarkProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
		cancellationToken.ThrowIfCancellationRequested();

		_ = _processedKeys.TryAdd((sagaId, idempotencyKey), 0);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the count of tracked idempotency keys. For testing purposes only.
	/// </summary>
	public int Count => _processedKeys.Count;

	/// <summary>
	/// Clears all tracked idempotency keys. For testing purposes only.
	/// </summary>
	public void Clear() => _processedKeys.Clear();
}
