// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// A resource protected by the fencing-token pattern: it records the highest fencing token it has
/// observed and rejects any operation whose token is not strictly greater than that high-water mark.
/// </summary>
/// <remarks>
/// This is the Chubby/Lamport sequencer rule applied to a protected resource: a stale leader that
/// resumes activity after losing its lease presents a fencing token that is less than or equal to the
/// token a successor has already advanced, and the resource rejects the operation instead of applying
/// it — preventing split-brain writes. See <see cref="IFencingTokenProvider"/> for how the token is
/// minted, and <see cref="Leadership"/> for how a consumer pins the token for its tenure.
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
	/// Thrown when <paramref name="fencingToken"/> is not strictly greater than the highest fencing
	/// token this resource has already observed.
	/// </exception>
	ValueTask GuardAsync(long fencingToken, CancellationToken cancellationToken);
}
