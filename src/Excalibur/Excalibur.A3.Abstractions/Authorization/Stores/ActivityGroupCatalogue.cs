// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// A complete, validated activity-group catalogue: the whole set of groups, for every tenant, that
/// <see cref="IActivityGroupStore.ReplaceAllActivityGroupsAsync"/> makes current.
/// </summary>
/// <remarks>
/// <para>
/// <b>Construction is the validation.</b> A store is only ever handed a catalogue that has already passed
/// every check below, so no store can apply an input that another store would refuse, and nothing is checked
/// after a lock has been taken or a row removed.
/// </para>
/// <list type="bullet">
/// <item><description><b>Non-empty.</b> An empty catalogue would remove every tenant's groups. Removing a
/// tenant's groups is done by name with <see cref="IActivityGroupStore.DeleteActivityGroupsForTenantAsync"/>;
/// it is never the result of passing nothing.</description></item>
/// <item><description><b>Every term is present and carries no leading or trailing whitespace.</b> SQL Server
/// ignores trailing spaces when it compares keys, so <c>"Support"</c> and <c>"Support "</c> are one group
/// there and two groups everywhere else. Refusing the whitespace makes a catalogue mean the same thing on
/// every provider.</description></item>
/// <item><description><b>Every term fits the shipped schema:</b> a tenant of at most
/// <see cref="Excalibur.Dispatch.TenantId.MaxLength"/> characters, a name of at most
/// <see cref="MaxNameLength"/> and an activity of at most <see cref="MaxActivityNameLength"/>.</description></item>
/// <item><description><b>It is a set.</b> A repeated (tenant, name, activity) triple, compared ordinally, is
/// kept once rather than refused.</description></item>
/// </list>
/// </remarks>
public sealed class ActivityGroupCatalogue
{
	/// <summary>The longest activity-group name the shipped schemas accept.</summary>
	public const int MaxNameLength = 128;

	/// <summary>The longest activity name the shipped schemas accept.</summary>
	public const int MaxActivityNameLength = 256;

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityGroupCatalogue"/> class.
	/// </summary>
	/// <param name="entries">Every (tenant, group, activity) entry of the catalogue.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="entries"/> is empty, or when any entry has a missing, whitespace-padded or
	/// over-length term. The message names the entry and the term.
	/// </exception>
	public ActivityGroupCatalogue(IEnumerable<ActivityGroupEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		var distinct = new HashSet<ActivityGroupEntry>();
		var ordered = new List<ActivityGroupEntry>();

		foreach (var entry in entries)
		{
			Require(entry.TenantId, Excalibur.Dispatch.TenantId.MaxLength, nameof(ActivityGroupEntry.TenantId), entry, nameof(entries));
			Require(entry.Name, MaxNameLength, nameof(ActivityGroupEntry.Name), entry, nameof(entries));
			Require(entry.ActivityName, MaxActivityNameLength, nameof(ActivityGroupEntry.ActivityName), entry, nameof(entries));

			// Record-struct equality compares the strings ordinally, which is the comparison every store
			// uses for identity.
			if (distinct.Add(entry))
			{
				ordered.Add(entry);
			}
		}

		if (ordered.Count == 0)
		{
			throw new ArgumentException(
				"An activity-group catalogue must contain at least one entry. Replacing the catalogue with an "
				+ "empty one would remove every tenant's groups; remove a tenant's groups by name instead.",
				nameof(entries));
		}

		Entries = ordered.AsReadOnly();
		TenantIds = [.. ordered.Select(static e => e.TenantId).Distinct(StringComparer.Ordinal)];
	}

	/// <summary>
	/// Gets the distinct tenants that hold at least one group in this catalogue.
	/// </summary>
	/// <value>Each tenant once, in the order it first appears.</value>
	public IReadOnlyCollection<string> TenantIds { get; }

	/// <summary>
	/// Gets the catalogue's entries.
	/// </summary>
	/// <value>Each distinct entry once, in the order it first appeared; never empty.</value>
	public IReadOnlyList<ActivityGroupEntry> Entries { get; }

	private static void Require(string? value, int maxLength, string term, ActivityGroupEntry entry, string paramName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException(
				$"Activity-group entry ({Describe(entry)}) has no {term}.", paramName);
		}

		if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
		{
			throw new ArgumentException(
				$"Activity-group entry ({Describe(entry)}) has a {term} with leading or trailing whitespace. "
				+ "SQL Server ignores trailing spaces when it compares keys, so such a value would identify a "
				+ "different group there than on other providers.",
				paramName);
		}

		if (value.Length > maxLength)
		{
			throw new ArgumentException(
				$"Activity-group entry ({Describe(entry)}) has a {term} of {value.Length} characters; the "
				+ $"shipped schema accepts at most {maxLength}.",
				paramName);
		}
	}

	private static string Describe(ActivityGroupEntry entry) =>
		$"tenant '{entry.TenantId}', group '{entry.Name}', activity '{entry.ActivityName}'";
}
