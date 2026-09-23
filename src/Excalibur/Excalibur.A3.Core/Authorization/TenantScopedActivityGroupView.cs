// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization;

/// <summary>
/// One tenant's view of the estate-wide activity-group catalogue: the group names that tenant can see,
/// and whether an activity belongs to one of them.
/// </summary>
/// <remarks>
/// <para>
/// The store returns the catalogue for the whole estate, keyed by a composed (tenant, name) pair. That is
/// deliberate and is not changed here — no predicate is added to any statement, so the tenancy doctrine
/// governing which statements carry a tenant term is untouched.
/// </para>
/// <para>
/// <b>What changes is that a foreign group is no longer ADDRESSABLE.</b> Neither member of this type
/// accepts a composed key, so there is no call through which a caller can name another tenant's group:
/// <see cref="GroupNames"/> yields only names belonging to the view's tenant, and
/// <see cref="Contains"/> composes the tenant itself, from the field, before it looks anything up. A
/// caller cannot supply that term and cannot override it.
/// </para>
/// <para>
/// This is the distinction between a foreign key that is addressable-but-conventionally-avoided and one
/// that is not addressable. The previous shape offered a membership test taking an already-composed key,
/// and a wildcard path that recovered a bare name from a key and matched on the name alone — so a grant
/// written against a name in one tenant could be satisfied by a group of the same name in another. The
/// two operations below cannot express that, which is why the escalation is closed by the seam and not by
/// a reviewer remembering to compose.
/// </para>
/// </remarks>
internal sealed class TenantScopedActivityGroupView
{
	/// <summary>The estate-wide catalogue, exactly as the store returned it.</summary>
	private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _estate;

	/// <summary>The one tenant this view speaks for. Every lookup composes with this and nothing else.</summary>
	private readonly string _tenantId;

	/// <summary>The visible names, resolved once at construction rather than per decision.</summary>
	private readonly string[] _visibleGroupNames;

	/// <summary>
	/// Initializes a view of <paramref name="estate"/> confined to <paramref name="tenantId"/>.
	/// </summary>
	/// <param name="estate">The estate-wide activity-group catalogue, keyed by composed (tenant, name).</param>
	/// <param name="tenantId">The ambient tenant this view is confined to.</param>
	public TenantScopedActivityGroupView(
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> estate,
		string tenantId)
	{
		_estate = estate;
		_tenantId = tenantId;
		_visibleGroupNames = BuildVisibleGroupNames(estate, tenantId);
	}

	/// <summary>
	/// Gets the names of the activity groups belonging to this view's tenant.
	/// </summary>
	/// <value>
	/// The bare group names — the form a grant qualifier is written against — for this tenant only. A
	/// wildcard grant enumerating this cannot reach another tenant's group, because no other tenant's name
	/// is in it.
	/// </value>
	public IReadOnlyList<string> GroupNames => _visibleGroupNames;

	/// <summary>
	/// Determines whether <paramref name="activity"/> belongs to this tenant's group named
	/// <paramref name="groupName"/>.
	/// </summary>
	/// <param name="groupName">The bare group name, as written in a grant qualifier.</param>
	/// <param name="activity">The activity to test for membership.</param>
	/// <returns>
	/// <see langword="true"/> if this tenant has a group of that name containing that activity; otherwise
	/// <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The tenant term is composed here, from the field. It is not a parameter, so a caller holding a
	/// foreign key has no way to present it to this method.
	/// </remarks>
	public bool Contains(string groupName, string activity) =>
		_estate.TryGetValue(SegmentedKey.Compose(_tenantId, groupName), out var activities)
		&& activities.Contains(activity, StringComparer.Ordinal);

	/// <summary>
	/// Resolves the group names visible to <paramref name="tenantId"/>, once.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Decomposition defers to the type that owns the key format rather than re-deriving it. A key that
	/// does not split into exactly two terms was not written by this framework, and
	/// <see cref="SegmentedKey.Split"/> signals that by throwing; such an entry is skipped rather than
	/// guessed at, because a key whose shape we do not recognise is a key whose tenant we cannot
	/// establish — and an entry of unknown tenancy must never be treated as this tenant's.
	/// </para>
	/// <para>
	/// This runs once per policy, not once per authorization decision, so the cost does not sit on the
	/// evaluation path.
	/// </para>
	/// </remarks>
	private static string[] BuildVisibleGroupNames(
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> estate,
		string tenantId)
	{
		var names = new List<string>();

		foreach (var groupKey in estate.Keys)
		{
			string[] terms;

			try
			{
				terms = SegmentedKey.Split(groupKey, 2);
			}
			catch (ArgumentException)
			{
				continue;
			}

			if (string.Equals(terms[0], tenantId, StringComparison.Ordinal))
			{
				names.Add(terms[1]);
			}
		}

		return [.. names];
	}
}
