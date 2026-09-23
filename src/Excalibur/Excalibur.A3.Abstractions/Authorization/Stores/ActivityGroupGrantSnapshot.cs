// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization;

/// <summary>
/// A validated set of activity-group grants that <see cref="IActivityGroupGrantReplacement"/> makes current.
/// </summary>
/// <remarks>
/// <para>
/// <b>Construction is the validation.</b> A store is only ever handed a snapshot whose every term has already
/// been checked, so no store can apply an input another store would refuse, and nothing is checked after a
/// lock has been taken or a row removed.
/// </para>
/// <list type="bullet">
/// <item><description><b>Every required term is present and carries no leading or trailing whitespace.</b>
/// SQL Server ignores trailing spaces when it compares keys, so <c>"Support"</c> and <c>"Support "</c> are one
/// qualifier there and two everywhere else. Refusing the whitespace makes a snapshot mean the same thing on
/// every provider. <see cref="ActivityGroupGrantEntry.FullName"/> and
/// <see cref="ActivityGroupGrantEntry.GrantedBy"/> are the two optional terms — neither is part of a grant's
/// identity, and an authority that sends no display name is not sending a malformed
/// row.</description></item>
/// <item><description><b>It is a set.</b> A repeated (user, tenant, grant type, qualifier) key — the identity
/// every store writes a grant under — is kept once, in the order it first appeared, rather than refused.
/// </description></item>
/// </list>
/// <para>
/// <b>Emptiness is NOT decided here, because it does not have one answer.</b> An empty ESTATE-WIDE snapshot is
/// far more likely a failed fetch than a true state, and applying it would revoke every user's activity-group
/// grants at once; an empty PER-USER snapshot is an ordinary state — that user now holds none, and every one
/// of theirs must be revoked. The two replaces therefore validate the snapshot differently, through
/// <see cref="ValidateAsEstateReplacement"/> and <see cref="ValidateAsUserReplacement"/>, which is where that
/// asymmetry is written down once instead of being re-derived per store.
/// </para>
/// </remarks>
public sealed class ActivityGroupGrantSnapshot
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityGroupGrantSnapshot"/> class.
	/// </summary>
	/// <param name="entries">The grants that are to be current after the replace. May be empty.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when any entry has a missing or whitespace-padded term, or a tenant longer than
	/// <see cref="TenantId.MaxLength"/>. The message names the entry and the term.
	/// </exception>
	public ActivityGroupGrantSnapshot(IEnumerable<ActivityGroupGrantEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		var seen = new HashSet<string>(StringComparer.Ordinal);
		var ordered = new List<ActivityGroupGrantEntry>();

		foreach (var entry in entries)
		{
			Require(entry.UserId, nameof(ActivityGroupGrantEntry.UserId), entry, nameof(entries));
			Require(entry.TenantId, nameof(ActivityGroupGrantEntry.TenantId), entry, nameof(entries));
			Require(entry.GrantType, nameof(ActivityGroupGrantEntry.GrantType), entry, nameof(entries));
			Require(entry.Qualifier, nameof(ActivityGroupGrantEntry.Qualifier), entry, nameof(entries));

			// FullName and GrantedBy are the two terms that are not part of a grant's identity, and an
			// authority that sends no display name is not sending a malformed row. Requiring them would refuse
			// a payload every store accepts today, so they are only checked for the padding that would make
			// them mean different things on different providers.
			RequireUnpadded(entry.FullName, nameof(ActivityGroupGrantEntry.FullName), entry, nameof(entries));
			RequireUnpadded(entry.GrantedBy, nameof(ActivityGroupGrantEntry.GrantedBy), entry, nameof(entries));

			if (entry.TenantId.Length > TenantId.MaxLength)
			{
				throw new ArgumentException(
					$"Activity-group grant ({Describe(entry)}) has a {nameof(ActivityGroupGrantEntry.TenantId)} of "
					+ $"{entry.TenantId.Length} characters; a tenant identifier is at most {TenantId.MaxLength}.",
					nameof(entries));
			}

			// The identity a grant is stored under on every provider. Escaped before joining, so a term
			// containing the separator cannot make two different grants look like one.
			if (seen.Add(SegmentedKey.Compose(entry.UserId, entry.TenantId, entry.GrantType, entry.Qualifier)))
			{
				ordered.Add(entry);
			}
		}

		Entries = ordered.AsReadOnly();
		UserIds = [.. ordered.Select(static e => e.UserId).Distinct(StringComparer.Ordinal)];
	}

	/// <summary>
	/// Gets the snapshot's entries.
	/// </summary>
	/// <value>Each distinct grant once, in the order it first appeared. May be empty.</value>
	public IReadOnlyList<ActivityGroupGrantEntry> Entries { get; }

	/// <summary>
	/// Gets the distinct users the snapshot grants to.
	/// </summary>
	/// <value>Each user once, in the order it first appears. May be empty.</value>
	public IReadOnlyCollection<string> UserIds { get; }

	/// <summary>
	/// Checks the preconditions of an ESTATE-WIDE replace, before any lock is taken or row removed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Empty is refused.</b> An estate-wide replace with nothing in it revokes every user's grants of that
	/// type, and an empty result from an authority cannot be told apart from an upstream filter, an
	/// authorization scope or a schema change returning nothing. Emptying the estate is an explicit act — the
	/// caller deletes by name — and never the result of passing nothing.
	/// </para>
	/// <para>
	/// <b>A mixed snapshot is refused whole.</b> The replace removes the rows carrying
	/// <paramref name="grantType"/> and writes the snapshot in their place, so a row of another type would be
	/// written without its own type's rows having been removed — a partial application of two replaces at
	/// once. Refusing here means nothing is deleted.
	/// </para>
	/// </remarks>
	/// <param name="snapshot">The snapshot the replace was given.</param>
	/// <param name="grantType">The grant type the replace is scoped to.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="grantType"/> is missing, when the snapshot is empty, or when any entry
	/// carries a different grant type.
	/// </exception>
	public static void ValidateAsEstateReplacement(ActivityGroupGrantSnapshot snapshot, string grantType)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrWhiteSpace(grantType);

		if (snapshot.Entries.Count == 0)
		{
			throw new ArgumentException(
				$"An estate-wide activity-group grant replace must carry at least one grant. Replacing the "
				+ $"'{grantType}' grants with an empty snapshot would revoke every user's, and an empty snapshot "
				+ "cannot be told apart from a fetch that failed to return anything. Revoke one user's grants "
				+ "with the per-user replace instead.",
				nameof(snapshot));
		}

		RequireEveryEntryCarries(snapshot, grantType);
	}

	/// <summary>
	/// Checks the preconditions of a PER-USER replace, before any lock is taken or row removed.
	/// </summary>
	/// <remarks>
	/// <b>Empty is ACCEPTED here, and that difference is deliberate.</b> A user who now holds no activity-group
	/// grants is an ordinary state, and every grant they held must be revoked; refusing the empty snapshot
	/// would keep revoked access alive. What must never reach this method is a FETCH THAT FAILED — that is a
	/// failure and its caller must raise it as one, never hand it on as an empty set.
	/// </remarks>
	/// <param name="snapshot">The snapshot the replace was given.</param>
	/// <param name="userId">The user the replace is scoped to.</param>
	/// <param name="grantType">The grant type the replace is scoped to.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="userId"/> or <paramref name="grantType"/> is missing, or when any entry
	/// carries a different user or grant type.
	/// </exception>
	public static void ValidateAsUserReplacement(
		ActivityGroupGrantSnapshot snapshot,
		string userId,
		string grantType)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		ArgumentException.ThrowIfNullOrWhiteSpace(grantType);

		RequireEveryEntryCarries(snapshot, grantType);

		foreach (var entry in snapshot.Entries)
		{
			if (!string.Equals(entry.UserId, userId, StringComparison.Ordinal))
			{
				throw new ArgumentException(
					$"A per-user activity-group grant replace for '{userId}' was given a grant held by "
					+ $"'{entry.UserId}' ({Describe(entry)}). The replace removes only {userId}'s grants, so that "
					+ "row would be written without the rows it replaces having been removed. Nothing was changed.",
					nameof(snapshot));
			}
		}
	}

	private static void RequireEveryEntryCarries(ActivityGroupGrantSnapshot snapshot, string grantType)
	{
		foreach (var entry in snapshot.Entries)
		{
			if (!string.Equals(entry.GrantType, grantType, StringComparison.Ordinal))
			{
				throw new ArgumentException(
					$"An activity-group grant replace of '{grantType}' was given a '{entry.GrantType}' grant "
					+ $"({Describe(entry)}). A replace removes only the rows carrying its own grant type, so a "
					+ "snapshot mixing types cannot be applied whole. Nothing was changed.",
					nameof(snapshot));
			}
		}
	}

	private static void Require(string? value, string term, ActivityGroupGrantEntry entry, string paramName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException($"Activity-group grant ({Describe(entry)}) has no {term}.", paramName);
		}

		RequireUnpadded(value, term, entry, paramName);
	}

	private static void RequireUnpadded(
		string? value,
		string term,
		ActivityGroupGrantEntry entry,
		string paramName)
	{
		if (string.IsNullOrEmpty(value))
		{
			return;
		}

		if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
		{
			throw new ArgumentException(
				$"Activity-group grant ({Describe(entry)}) has a {term} with leading or trailing whitespace. "
				+ "SQL Server ignores trailing spaces when it compares keys, so such a value would identify a "
				+ "different grant there than on other providers.",
				paramName);
		}
	}

	private static string Describe(ActivityGroupGrantEntry entry) =>
		$"user '{entry.UserId}', tenant '{entry.TenantId}', type '{entry.GrantType}', qualifier '{entry.Qualifier}'";
}
