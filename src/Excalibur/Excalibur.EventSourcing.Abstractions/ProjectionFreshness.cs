// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// What a consistent read can say about how current the projection it returned was.
/// </summary>
/// <remarks>
/// <b>Three states, not a <see cref="bool"/>, and the third is why.</b> "No consistency was asked for"
/// and "consistency was asked for and could not be confirmed in time" mean opposite things to a caller —
/// the first is an ordinary read, the second is a warning that the data may predate their own write — and
/// a boolean would force them to share a value. A type that cannot express the difference makes every
/// producer pick one of the two and be wrong about the other.
/// </remarks>
public enum ProjectionFreshness
{
	/// <summary>
	/// No session token was supplied, so no consistency was requested and none was waited for. An
	/// ordinary, possibly stale read.
	/// </summary>
	NotRequested = 0,

	/// <summary>
	/// The projection had applied the supplied token's position before the read, so the read observes
	/// the write that produced that token.
	/// </summary>
	Fresh = 1,

	/// <summary>
	/// The projection had NOT reached the token's position within the configured bound. The read still
	/// returns what it found; the caller is told that value may predate their own write.
	/// </summary>
	/// <remarks>
	/// This is NOT an error and is deliberately not an exception. A bound expiring says the projection is
	/// behind or stopped, which a caller may reasonably choose to tolerate, retry or surface — a decision
	/// only they can make. Throwing would take it from them and would make the common "show slightly
	/// stale data rather than fail" choice the awkward one.
	/// </remarks>
	BoundExpired = 2,
}
