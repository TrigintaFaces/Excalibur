// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// An optional capability of <see cref="IActivityGroupGrantStore"/>: replacing a set of grants in one
/// atomic step instead of deleting and re-inserting them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is a separate interface.</b> Not every store can offer it. A replace has to remove a set of rows
/// and write another set so that no reader and no failure can observe the gap between them, which needs a
/// transaction and a lock the store can take across both statements. The relational and in-memory stores have
/// that; stores over document and key-value databases that do not offer a cross-document transaction cannot
/// provide it honestly, and declaring it on the base contract would have made them claim it. A consumer
/// composing one of those chooses explicitly, through
/// <c>ActivityGroupSyncOptions.GrantSyncAtomicity</c>, between refusing at start-up and accepting the
/// non-atomic sync with its window documented.
/// </para>
/// <para>
/// <b>The guarantee, in falsifiable terms.</b> Each member below either applies its whole snapshot or changes
/// nothing. A concurrent reader observes the whole set before the replace or the whole set after it, never a
/// partial or empty one. Two replaces of the same set are serialized, so the set that remains is the one whose
/// replace committed last — never the union of both. Nothing outside the member's own scope is touched: an
/// estate-wide replace leaves grants of other types alone, and a per-user replace leaves other users' grants
/// alone.
/// </para>
/// <para>
/// <b>Preconditions are carried by the snapshot.</b> An <see cref="ActivityGroupGrantSnapshot"/> can only be
/// constructed with every term present, free of leading and trailing whitespace and within the tenant width,
/// so every store receives an input every other store would also accept. The scope preconditions — a snapshot
/// that is entirely of this grant type, and for the per-user member entirely of this user — are checked
/// BEFORE the transaction opens, so a snapshot that cannot be applied whole is refused without anything having
/// been removed.
/// </para>
/// <para>
/// <b>It runs in its own transaction.</b> A database store refuses, with
/// <see cref="InvalidOperationException"/>, to run inside an ambient transaction, because it would then commit
/// or roll back with work it cannot see.
/// </para>
/// </remarks>
public interface IActivityGroupGrantReplacement
{
	/// <summary>
	/// Makes <paramref name="snapshot"/> the whole set of <paramref name="grantType"/> grants, for every user,
	/// in one atomic step.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This member is estate-wide by specification.</b> Afterwards the store holds exactly the snapshot's
	/// grants of this type: a user absent from it holds none of them. To revoke one user's grants, call
	/// <see cref="ReplaceActivityGroupGrantsForUserAsync"/>, which names the user; the estate-wide behaviour is
	/// never reached by omitting an argument.
	/// </para>
	/// <para>
	/// <b>An empty snapshot is refused</b>, because it would revoke every user's grants of this type and an
	/// empty result from an authority cannot be told apart from a fetch that returned nothing.
	/// </para>
	/// </remarks>
	/// <param name="grantType">The grant type to replace. Required; must be non-empty.</param>
	/// <param name="snapshot">The grants that are to be current afterwards. Every entry must carry
	/// <paramref name="grantType"/>, and there must be at least one.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The distinct users that held a grant of this type immediately before the replace, observed inside the
	/// same transaction, in no particular order. A caller invalidating state derived from the old grants needs
	/// these as well as <see cref="ActivityGroupGrantSnapshot.UserIds"/>: a user the replace revoked appears
	/// only here.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="grantType"/> is missing, when <paramref name="snapshot"/> is empty, or when
	/// any entry carries a different grant type. Nothing is changed.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a database store is called inside an ambient transaction, or cannot obtain the lock that
	/// orders replaces. Nothing is changed.
	/// </exception>
	Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsAsync(
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken);

	/// <summary>
	/// Makes <paramref name="snapshot"/> the whole set of <paramref name="userId"/>'s
	/// <paramref name="grantType"/> grants, in one atomic step, leaving every other user's alone.
	/// </summary>
	/// <remarks>
	/// <b>An empty snapshot is ACCEPTED here</b>, unlike the estate-wide member: a user who now holds no grants
	/// of this type is an ordinary state and every grant they held is revoked. A caller must therefore never
	/// pass an empty snapshot to stand for a fetch that failed — a failure is raised, never handed on as an
	/// empty set.
	/// </remarks>
	/// <param name="userId">The user whose grants to replace. Required; must be non-empty. Pass a BARE value.</param>
	/// <param name="grantType">The grant type to replace. Required; must be non-empty.</param>
	/// <param name="snapshot">The grants that are to be current for this user afterwards. Every entry must
	/// carry <paramref name="userId"/> and <paramref name="grantType"/>. May be empty.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The users that held a grant of this type immediately before the replace and had it removed — at most
	/// <paramref name="userId"/> — observed inside the same transaction.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="userId"/> or <paramref name="grantType"/> is missing, or when any entry
	/// carries a different user or grant type. Nothing is changed.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a database store is called inside an ambient transaction, or cannot obtain the lock that
	/// orders replaces. Nothing is changed.
	/// </exception>
	Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsForUserAsync(
		string userId,
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken);
}
