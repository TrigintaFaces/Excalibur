// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// In-memory implementation of <see cref="ISubscriptionCheckpointStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// Checkpoints are stored in a <see cref="ConcurrentDictionary{TKey, TValue}"/> and are lost
/// when the process restarts. For production use, use a durable implementation such as
/// a SQL Server or Redis-based checkpoint store.
/// </para>
/// </remarks>
public sealed class InMemorySubscriptionCheckpointStore : ISubscriptionCheckpointStore
{
	private readonly ConcurrentDictionary<string, long> _checkpoints = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		long? result = _checkpoints.TryGetValue(subscriptionName, out var position) ? position : null;
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task StoreCheckpointAsync(string subscriptionName, long position, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		_checkpoints[subscriptionName] = position;
		return Task.CompletedTask;
	}
}
