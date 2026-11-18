// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// A thread-safe, in-memory <see cref="IFencedResource"/> that enforces the Chubby/Lamport sequencer
/// rule. A fencing token identifies a leadership <em>tenure</em>, not an individual operation, so an
/// operation is accepted when its token is greater than or equal to the highest token previously
/// accepted (a non-decreasing sequence), and the high-water mark advances atomically. Only a strictly
/// lower token — presented by a superseded leader — is rejected. This admits the same leader presenting
/// its stable per-tenure token across multiple operations, while still fencing out a stale predecessor.
/// </summary>
/// <remarks>
/// <para>
/// <b>A non-positive token is refused before the comparison.</b> The sibling contract states that every
/// fencing token is strictly positive and that zero is UNSET rather than a value, and that it binds
/// every implementation. This type previously advertised admitting "an initial token of zero" as a
/// feature, and did: the high-water field starts at zero, <c>0 &lt; 0</c> is false, so a fabricated zero
/// was accepted and left the mark at zero — making never-guarded and guarded-at-zero the same state,
/// which a <see cref="long" /> cannot distinguish.
/// </para>
/// <para>
/// The practical consequence was narrow, and it is stated so nobody reads more into the fix than it
/// carries: a real superseded leader holds a POSITIVE token, so this was not split-brain by itself, and
/// admitting the first real token is correct and intended. What was wrong is that a value the family
/// invariant says is not a token at all was admitted, by a public type a consumer instantiates
/// directly.
/// </para>
/// <para>
/// This is the default fenced-resource guard for protecting an in-process resource (a cache entry, an
/// in-memory aggregate, a connection) against stale-leader operations. Resources backed by an external
/// store (a database row, a Redis key) typically enforce the same rule server-side instead (a
/// conditional update keyed on a stored high-water column); this guard is for resources with no such
/// external enforcement point.
/// </para>
/// </remarks>
public sealed class MonotonicFencedResourceGuard : IFencedResource
{
	private readonly Lock _lock = new();
	private long _highWater;

	/// <inheritdoc/>
	public ValueTask GuardAsync(long fencingToken, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Refused before the comparison, because the comparison cannot express it: the mark starts at
		// zero, so a presented zero is not lower than it and would be admitted while leaving the mark
		// unset. Zero is the contract's representation of "no token was ever issued", so presenting one
		// is a caller error rather than a stale tenure -- which is why this is an argument exception and
		// not StaleFencingTokenException.

		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fencingToken);

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
