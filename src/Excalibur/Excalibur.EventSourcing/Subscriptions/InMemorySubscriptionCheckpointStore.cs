// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Linq;

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
internal sealed class InMemorySubscriptionCheckpointStore : ISubscriptionCheckpointStore
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
	public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
		string subscriptionName,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		if (expectedPosition is { } prior)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(prior, nameof(expectedPosition));
			ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, prior);
		}


		// TryAdd and TryUpdate are the atomic primitives ConcurrentDictionary offers. An indexer
		// assignment would be the blind write the contract forbids, and a TryGetValue-then-assign would
		// reintroduce the interleaving while still satisfying the signature.
		//
		// The null case is TryAdd rather than TryUpdate because "no checkpoint yet" is a distinct prior
		// state: a caller that believes there is none must LOSE the race against a writer that has since
		// created one, which is exactly what TryAdd does.
		var advanced = expectedPosition is null
			? _checkpoints.TryAdd(subscriptionName, newPosition)
			: _checkpoints.TryUpdate(subscriptionName, newPosition, expectedPosition.Value);

		return Task.FromResult(
			advanced ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken)
	{
		// Snapshot the dictionary; ConcurrentDictionary enumeration is a moment-in-time view.
		IReadOnlyList<SubscriptionCheckpoint> checkpoints =
			[.. _checkpoints.Select(static kvp => new SubscriptionCheckpoint(kvp.Key, kvp.Value))];
		return Task.FromResult(checkpoints);
	}
}
