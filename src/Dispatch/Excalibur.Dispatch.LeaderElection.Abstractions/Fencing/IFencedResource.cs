// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// A resource protected by the fencing-token pattern: it records the highest fencing token it has
/// observed and rejects any operation whose token is <em>below</em> that high-water mark. A token at or
/// above the mark is accepted, and the mark never moves backward.
/// </summary>
/// <remarks>
/// <para>
/// This is the Chubby/Lamport sequencer rule applied to a protected resource: a stale leader that
/// resumes activity after losing its lease presents a fencing token below the one a successor has
/// already advanced the mark to, and the resource rejects the operation instead of applying it —
/// preventing split-brain writes. See <see cref="IFencingTokenProvider"/> for how the token is minted,
/// and <see cref="Leadership"/> for how a consumer pins the token for its tenure.
/// </para>
/// <para>
/// <b>The relation is at-or-above, not strictly greater, and an implementation that strengthens it to
/// strictly greater breaks its own leader.</b> A fencing token identifies a leadership <em>tenure</em>,
/// not an individual operation: the consumer captures the token once at the leadership transition and
/// re-presents that same pinned value on every protected operation of the tenure (the pattern
/// <see cref="IFencingTokenProvider"/> instructs consumers to follow). So the second and every later
/// operation of the incumbent leader presents a token exactly equal to the mark its own first operation
/// set. Requiring a strictly greater token refuses the live holder from its second write onward — the
/// sequence a fence needs is non-decreasing, and only a strictly lower token identifies a superseded
/// leader.
/// </para>
/// </remarks>
public interface IFencedResource
{
	/// <summary>
	/// Guards an operation with the given fencing token, advancing the resource's high-water mark when
	/// the token is accepted.
	/// </summary>
	/// <param name="fencingToken">The fencing token presented for this operation.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task that completes when the guard check has been evaluated.</returns>
	/// <exception cref="StaleFencingTokenException">
	/// Thrown when <paramref name="fencingToken"/> is below the highest fencing token this resource has
	/// already observed — a superseded leader. A token equal to that high-water mark is the incumbent
	/// leader re-presenting its pinned per-tenure token and is accepted, not thrown on.
	/// </exception>
	ValueTask GuardAsync(long fencingToken, CancellationToken cancellationToken);
}
