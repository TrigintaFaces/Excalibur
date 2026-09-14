// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// In-memory <see cref="IFencingTokenProvider"/> implementation, backed by a per-resource
/// <see cref="long"/> counter in <see cref="InMemoryLeaderElectionSharedState"/>.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory election is single-process by construction (<see cref="InMemoryLeaderElectionSharedState"/>
/// is shared, in-memory, per-process state). That collapses the distinction the distributed providers must
/// maintain between "the leader" and "the counter": here they are the same process, so a monotonically
/// increasing in-memory counter, minted under a per-resource lock, is a genuinely correct arbitrated fencing
/// source — not a weaker stand-in for one.
/// </para>
/// <para>
/// <b>Exhaustion:</b> the counter is a <see cref="long"/>. When it has reached <see cref="long.MaxValue"/>
/// the next mint would overflow, so the provider fails closed with <see cref="FencingTokenExhaustedException"/>
/// rather than wrapping, matching every other provider's exhaustion behavior.
/// </para>
/// </remarks>
internal sealed class InMemoryFencingTokenProvider : IFencingTokenProvider
{
	private readonly ConcurrentDictionary<string, long> _tokens;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="sharedState">
	/// The shared, per-process election state. Optional for test isolation; defaults to
	/// <see cref="InMemoryLeaderElectionSharedState.Default"/> — the same default the election and factory
	/// use, so a provider resolved without an explicit shared state still counts alongside the election it
	/// is fencing.
	/// </param>
	public InMemoryFencingTokenProvider(InMemoryLeaderElectionSharedState? sharedState = null)
	{
		_tokens = (sharedState ?? InMemoryLeaderElectionSharedState.Default).FencingTokens;
	}

	/// <inheritdoc />
	public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		// AddOrUpdate is the atomic mint: the update delegate may run more than once under contention, but
		// it never observes a torn write, and a thrown exhaustion never leaves a partial write behind.
		var next = _tokens.AddOrUpdate(
			resourceId,
			addValueFactory: static _ => 1L,
			updateValueFactory: (id, current) =>
			{
				if (current >= long.MaxValue)
				{
					// Next mint would overflow the int64 domain -> fail closed (never wrap a fencing token).
					throw new FencingTokenExhaustedException(
						string.Format(
							CultureInfo.InvariantCulture,
							"In-memory fencing token domain is exhausted for resource '{0}'.",
							id))
					{
						ResourceId = id,
					};
				}

				return current + 1;
			});

		return ValueTask.FromResult(next);
	}

	/// <inheritdoc />
	public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// No counter entry -> no token ever issued -> no active leader (idiomatic "no value" signal).
		long? current = _tokens.TryGetValue(resourceId, out var value) ? value : null;
		return ValueTask.FromResult(current);
	}

	/// <inheritdoc />
	public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no issued token nothing is valid; otherwise accept only
		// tokens at or above the current value, rejecting a stale leader's lower token.
		var current = _tokens.TryGetValue(resourceId, out var value) ? value : (long?)null;
		return ValueTask.FromResult(current.HasValue && token >= current.Value);
	}
}
