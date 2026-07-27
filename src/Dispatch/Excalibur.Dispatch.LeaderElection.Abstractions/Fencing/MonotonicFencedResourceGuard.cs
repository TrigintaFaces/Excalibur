// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// A thread-safe, in-memory <see cref="IFencedResource"/> that enforces the Chubby/Lamport sequencer
/// rule. A fencing token identifies a leadership <em>tenure</em>, not an individual operation, so an
/// operation is accepted when its token is greater than or equal to the highest token previously
/// accepted (a non-decreasing sequence), and the high-water mark advances atomically. Only a strictly
/// lower token — presented by a superseded leader — is rejected. This admits the same leader presenting
/// its stable per-tenure token across multiple operations, and an initial token of zero, while still
/// fencing out a stale predecessor.
/// </summary>
/// <remarks>
/// This is the default fenced-resource guard for protecting an in-process resource (a cache entry, an
/// in-memory aggregate, a connection) against stale-leader operations. Resources backed by an external
/// store (a database row, a Redis key) typically enforce the same rule server-side instead (a
/// conditional update keyed on a stored high-water column); this guard is for resources with no such
/// external enforcement point.
/// </remarks>
public sealed class MonotonicFencedResourceGuard : IFencedResource
{
	private readonly Lock _lock = new();
	private long _highWater;

	/// <inheritdoc/>
	public ValueTask GuardAsync(long fencingToken, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		lock (_lock)
		{
			if (fencingToken < _highWater)
			{
				throw new StaleFencingTokenException(
					$"Fencing token {fencingToken} is older than the current high-water mark {_highWater} (a superseded leader); operation rejected.")
				{
					PresentedToken = fencingToken,
					HighWaterToken = _highWater,
				};
			}

			_highWater = fencingToken;
		}

		return ValueTask.CompletedTask;
	}
}
