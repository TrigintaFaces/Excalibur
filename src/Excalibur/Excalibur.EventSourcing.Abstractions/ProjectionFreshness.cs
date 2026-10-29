// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// The projection completion observed by a save or verified by a consistent read.
/// </summary>
/// <remarks>
/// <b>Distinct outcomes rather than a <see cref="bool"/>.</b> "No consistency was asked
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
	/// <remarks>
	/// On a save this also covers "nothing was owed", and that reading is narrower than it looks: a
	/// process may only claim it when it can see every projection bound to the subscription. One bound
	/// elsewhere might consume the committed events, and whether it does is not recorded, so a process
	/// that does not host it reports <see cref="BoundExpired"/> rather than claiming nothing is outstanding.
	/// </remarks>
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
	/// <see cref="NotRequested"/> means nothing was owed — there was no token, or no projection this
	/// process hosts was fed by these events AND the subscription held no member hosted elsewhere — so
	/// there is nothing outstanding and nobody owes a follow-up. A process that cannot see every member of
	/// its subscription cannot reach that conclusion, and reports <see cref="BoundExpired"/> instead.
	/// <c>Deferred</c> means a wait WAS owed and was deliberately skipped, which leaves an obligation on
	/// the caller to drive <see cref="IProjectionProcessor.ProcessPendingAsync"/> once the batch is done.
	/// Collapsing the two would make a suppressed batch that never calls it indistinguishable from an
	/// ordinary save with nothing pending, which is the one reading that loses data visibility silently.
	/// </remarks>
	Deferred = 3,

	/// <summary>
	/// The save observed its required projections caught up to the session token, against the durable
	/// roster as it stood at that moment. An observation, not a standing promise.
	/// </summary>
	/// <remarks>
	/// <b>Distinct from <see cref="Fresh"/>, and the distinction is the point of the value.</b>
	/// <see cref="Fresh"/> is a read-time verdict: <see cref="IProjectionSessionReader"/> obtains its own
	/// witness, reads the data, and only then re-validates roster identity, binding lifetime, position and
	/// destination — so a caller holding <see cref="Fresh"/> knows the document it is looking at reflects
	/// its own write. <c>Attested</c> is a save-time receipt. The roster can change after the save observed
	/// it: a member bound immediately afterwards carries no checkpoint, and this value does not claim
	/// otherwise.
	/// <para>
	/// Collapsing the two is what made the inline completion guarantee unfalsifiable — one value stood for
	/// both "we checked at save time" and "we checked at read time against current state", so no assertion
	/// could distinguish a receipt from a verdict. A caller that needs read-your-own-write hands the token
	/// to <see cref="IProjectionSessionReader"/> and requires <see cref="Fresh"/>; a caller that only needs
	/// to know the save did not give up early accepts <c>Attested</c>.
	/// </para>
	/// </remarks>
	Attested = 4,
}
