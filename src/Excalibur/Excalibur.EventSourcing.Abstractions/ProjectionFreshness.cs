// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// What a consistent read can say about how current the projection it returned was.
/// </summary>
/// <remarks>
/// <b>Four states, not a <see cref="bool"/>, and the last three are why.</b> "No consistency was asked
/// for", "consistency was asked for and could not be confirmed in time", and "the caller deliberately
/// deferred the wait and has taken on catching the projection up" mean different things to a caller — an
/// ordinary read, a warning that the data may predate their own write, and a receipt for a choice they
/// made — and a boolean would force them to share a value. A type that cannot express the difference makes
/// every producer pick one and be wrong about the others.
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

	/// <summary>
	/// The caller suppressed the wait with <see cref="InlineProjectionWait.Suppress"/>, so none was
	/// performed. The events are committed; the projection has not been advanced by this save and the
	/// caller has taken on advancing it.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="NotRequested"/> on purpose, and the distinction is the point of the value.
	/// <see cref="NotRequested"/> means nothing was owed — there was no token, no inline projection, or no
	/// projection fed by these events — so there is nothing outstanding and nobody owes a follow-up.
	/// <c>Deferred</c> means a wait WAS owed and was deliberately skipped, which leaves an obligation on
	/// the caller to drive <see cref="IProjectionProcessor.ProcessPendingAsync"/> once the batch is done.
	/// Collapsing the two would make a suppressed batch that never calls it indistinguishable from an
	/// ordinary save with nothing pending, which is the one reading that loses data visibility silently.
	/// </remarks>
	Deferred = 3,
}
