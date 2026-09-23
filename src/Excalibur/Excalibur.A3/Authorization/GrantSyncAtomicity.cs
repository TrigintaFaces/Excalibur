// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// What an activity-group grant sync does when the configured grant store cannot replace a set of grants
/// atomically.
/// </summary>
/// <remarks>
/// <para>
/// A grant sync is a full refresh: it makes the authority's snapshot the whole set of grants. A store that
/// implements <see cref="IActivityGroupGrantReplacement"/> does that in one transaction, so the old set is
/// never observed half-removed and a failure leaves it untouched. A store that cannot — one over a database
/// with no transaction spanning the rows involved — can only delete and then insert, which has a window.
/// </para>
/// <para>
/// This is the same shape Entity Framework Core uses for the same problem on Cosmos: the consumer says which
/// they want rather than the framework guessing, and neither answer is hidden.
/// </para>
/// </remarks>
public enum GrantSyncAtomicity
{
	/// <summary>
	/// The sync requires an atomic replace. A store that does not implement
	/// <see cref="IActivityGroupGrantReplacement"/> is refused at start-up, naming the store and this option.
	/// </summary>
	/// <remarks>
	/// This is the default because the alternative silently accepts a window in which a user's authorization
	/// is partly revoked, and a consumer who has not thought about that window should not be given it.
	/// </remarks>
	Required = 0,

	/// <summary>
	/// The sync accepts a non-atomic delete-then-insert on a store that cannot replace atomically, and warns
	/// once at start-up.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The window this accepts, stated so it can be checked.</b> Between the delete and the last insert, a
	/// reader observes a partial set of grants: some of the grants the snapshot confers are present and the
	/// rest are not, and any grant the previous set had that the snapshot keeps is briefly absent. Because the
	/// authorization decision denies on a missing grant, that reader is denied access the snapshot grants
	/// them. If the sync fails part-way, the set stays partial until the next sync succeeds — there is no
	/// rollback.
	/// </para>
	/// <para>
	/// A store that does implement <see cref="IActivityGroupGrantReplacement"/> ignores this setting and is
	/// always atomic.
	/// </para>
	/// </remarks>
	BestEffort = 1,
}
