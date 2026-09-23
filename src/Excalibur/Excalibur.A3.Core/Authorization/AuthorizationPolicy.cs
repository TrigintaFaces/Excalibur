// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics;

using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Represents an authorization policy that evaluates user permissions and grants based on activities and resources.
/// </summary>
/// <remarks>
/// <para>
/// This policy evaluates grant data in pure C# to determine access rights.
/// Grants are keyed by scope string in the format <c>{TenantId}:{GrantType}:{Qualifier}</c>, composed
/// through <see cref="SegmentedKey"/> so that no two distinct triples can address one grant. Every
/// producer and every probe of this key MUST go through that composer: a raw interpolation here would
/// not match the escaped key the store wrote.
/// </para>
/// <para>
/// Uses a dual-index approach: exact grants are stored in a dictionary for O(1) lookup,
/// while wildcard grants are stored in a list sorted by specificity for fallback matching.
/// </para>
/// </remarks>
public sealed class AuthorizationPolicy : IAuthorizationPolicy
{
	private readonly IDictionary<string, object> _exactGrants;
	private readonly List<GrantScope> _wildcardGrants;
	/// <summary>
	/// This tenant's view of the estate-wide activity-group catalogue. The policy holds the VIEW rather
	/// than the raw catalogue so that another tenant's group has no expression on any path below: the view
	/// exposes no member taking a composed key, so a foreign group cannot be named, let alone read.
	/// </summary>
	private readonly TenantScopedActivityGroupView _groups;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizationPolicy"/> class.
	/// </summary>
	/// <param name="grants">The user's grants, keyed by scope string.</param>
	/// <param name="activityGroups">Activity group mappings.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="userId">The user identifier for the current context.</param>
	public AuthorizationPolicy(
		IDictionary<string, object> grants,
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> activityGroups,
		ITenantContext tenantContext,
		string userId)
	{
		TenantId = tenantContext.TenantId ?? throw new InvalidOperationException(
			"No ambient tenant is resolved; authorization requires an established tenant (TenantContextHolder.BeginScope / tenant middleware).");

		// Confined to the ambient tenant at construction, so no later path has to remember to do it. The
		// order matters: the view is built FROM TenantId, so it cannot be created before the line above
		// has established which tenant this policy speaks for.
		_groups = new TenantScopedActivityGroupView(activityGroups, TenantId);
		UserId = userId;

		// Partition grants into exact-match dictionary and wildcard list
		var exactGrants = new Dictionary<string, object>(StringComparer.Ordinal);
		var wildcardGrants = new List<GrantScope>();

		foreach (var (key, value) in grants)
		{
			var scope = TryParseScope(key);
			if (scope is null)
			{
				continue;
			}

			if (scope.IsWildcard)
			{
				wildcardGrants.Add(scope);
			}
			else
			{
				exactGrants[key] = value;
			}
		}

		// Sort wildcard grants by specificity descending (most specific first)
		wildcardGrants.Sort(static (a, b) => b.SpecificityScore.CompareTo(a.SpecificityScore));

		_exactGrants = exactGrants;
		_wildcardGrants = wildcardGrants;
	}

	/// <inheritdoc />
	public string TenantId { get; }

	/// <inheritdoc />
	public string UserId { get; }

	/// <inheritdoc />
	public bool IsAuthorized(string activityName, string? resourceId)
	{
		var result = Evaluate(activityName, resourceId, resourceType: null);

		return result.IsAuthorized;
	}

	/// <inheritdoc />
	public bool HasGrant(string activityName)
	{
		var result = Evaluate(activityName, resource: null, resourceType: null);

		return result.HasActivityGrant;
	}

	/// <inheritdoc />
	public bool HasGrant<TActivity>()
	{
		var activity = TypeNameHelper.GetTypeDisplayName(typeof(TActivity), fullName: false);
		return HasGrant(activity);
	}

	/// <inheritdoc />
	public bool HasGrant(string resourceType, string resourceId)
	{
		var result = Evaluate(activity: null, resourceId, resourceType);

		return result.HasResourceGrant;
	}

	/// <inheritdoc />
	public bool HasGrant<TResourceType>(string resourceId)
	{
		var resourceType = TypeNameHelper.GetTypeDisplayName(typeof(TResourceType), fullName: false);
		return HasGrant(resourceType, resourceId);
	}

	/// <summary>
	/// Evaluates grants for the specified activity, resource, and resource type.
	/// </summary>
	/// <param name="activity"> The name of the activity to evaluate. </param>
	/// <param name="resource"> The identifier of the resource (optional). </param>
	/// <param name="resourceType"> The type of the resource (optional). </param>
	/// <returns> A <see cref="PolicyResult" /> representing the evaluation result. </returns>
	private PolicyResult Evaluate(string? activity, string? resource, string? resourceType)
	{
		var hasActivityGrant = false;
		var hasResourceGrant = false;

		if (activity != null)
		{
			// 1. Exact match (O(1) -- existing behavior, preserved)
			// Composed through the canonical composer, NOT interpolated. The stored key was written
			// escaped, so a raw probe cannot match it: a tenant, activity or resource term containing
			// ':' or '%' would compose a string that is absent from the dictionary, and a grant the
			// user genuinely holds would read as no grant at all.
			// Composed through the canonical composer, NOT interpolated. The stored key was written
			// escaped, so a raw probe cannot match it: a tenant, activity or resource term containing
			// ':' or '%' would compose a string that is absent from the dictionary, and a grant the
			// user genuinely holds would read as no grant at all.
			var activityKey = SegmentedKey.Compose(TenantId, GrantType.Activity, activity);
			hasActivityGrant = _exactGrants.ContainsKey(activityKey);

			// 2. Wildcard match (O(W) -- only if exact fails)
			if (!hasActivityGrant)
			{
				hasActivityGrant = HasWildcardMatch(TenantId, GrantType.Activity, activity);
			}

			// 3. Activity group match (existing behavior, preserved)
			if (!hasActivityGrant)
			{
				hasActivityGrant = HasActivityGroupGrant(activity);
			}
		}

		if (resourceType != null && resource != null)
		{
			// 1. Exact match
			var resourceKey = SegmentedKey.Compose(TenantId, resourceType, resource);
			hasResourceGrant = _exactGrants.ContainsKey(resourceKey);

			// 2. Wildcard match
			if (!hasResourceGrant)
			{
				hasResourceGrant = HasWildcardMatch(TenantId, resourceType, resource);
			}
		}

		return new PolicyResult
		{
			IsAuthorized = hasActivityGrant || hasResourceGrant,
			HasActivityGrant = hasActivityGrant,
			HasResourceGrant = hasResourceGrant,
		};
	}

	/// <summary>
	/// Checks whether any wildcard grant matches the specified request scope.
	/// Iterates the pre-sorted wildcard list; the first match is the most specific.
	/// </summary>
	private bool HasWildcardMatch(string requestTenant, string requestType, string requestQualifier)
	{
		foreach (var scope in _wildcardGrants)
		{
			if (WildcardGrantMatcher.Matches(
				scope.TenantId,
				scope.GrantType,
				scope.Qualifier,
				requestTenant,
				requestType,
				requestQualifier))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Checks whether the user has a grant for an activity group that contains the specified activity.
	/// Checks both exact and wildcard grants for activity group keys.
	/// </summary>
	/// <param name="activity"> The activity name to check. </param>
	/// <returns> <see langword="true"/> if the user has an activity group grant containing the activity. </returns>
	private bool HasActivityGroupGrant(string activity)
	{
		// Check exact activity group grants
		foreach (var (key, _) in _exactGrants)
		{
			var scope = TryParseScope(key);
			if (scope == null
				|| !string.Equals(scope.GrantType, GrantType.ActivityGroup, StringComparison.Ordinal)
				|| !string.Equals(scope.TenantId, TenantId, StringComparison.Ordinal))
			{
				continue;
			}

			if (_groups.Contains(scope.Qualifier, activity))
			{
				return true;
			}
		}

		// Check wildcard grants that match ActivityGroup type
		foreach (var scope in _wildcardGrants)
		{
			// Pre-filter: skip wildcards that can't match ActivityGroup scope
			if (scope.GrantType is not "*" &&
				!string.Equals(scope.GrantType, GrantType.ActivityGroup, StringComparison.Ordinal))
			{
				continue;
			}

			if (scope.TenantId is not "*" &&
				!string.Equals(scope.TenantId, TenantId, StringComparison.Ordinal))
			{
				continue;
			}

			// For wildcard activity group grants, check this tenant's activity groups.
			//
			// A wildcard qualifier is written against a BARE group name, so the match needs names rather
			// than composed keys -- and the estate holds both, for every tenant. Enumerating the view
			// yields only the names this tenant owns, and the membership test composes the tenant itself,
			// so neither half of this condition can reach a group belonging to anyone else.
			//
			// Iterating the raw catalogue here instead would re-open a cross-tenant escalation: a grant
			// for "*" in the user's own tenant matches any name in the estate, and membership would then
			// be read from that other tenant's entry.
			foreach (var groupName in _groups.GroupNames)
			{
				if (WildcardGrantMatcher.Matches(
					scope.TenantId,
					scope.GrantType,
					scope.Qualifier,
					TenantId,
					GrantType.ActivityGroup,
					groupName) && _groups.Contains(groupName, activity))
				{
					return true;
				}
			}
		}

		return false;
	}

	// Membership and name-recovery used to live here, as a helper taking an already-composed group key
	// and another recovering a bare name from one. Both are gone: the first made a foreign group
	// addressable by anyone holding its key, and the second discarded the tenant it had just decoded.
	// TenantScopedActivityGroupView owns both operations now and composes the tenant itself, so this class
	// no longer has a member that can be handed a key belonging to somebody else.

	/// <summary>
	/// Attempts to parse a scope key, deferring to the type that owns the format.
	/// </summary>
	/// <param name="key"> The scope key string. </param>
	/// <returns> A <see cref="GrantScope"/> if parsing succeeds; otherwise, <see langword="null"/>. </returns>
	/// <remarks>
	/// <para>
	/// This re-derived the parse instead of calling the owner, and drifted from it in three ways. Every one
	/// of them fails the same direction — the scope does not parse, both callers treat that as "skip", and
	/// the grant is SILENTLY NOT APPLIED. A denial produced by a parse defect is indistinguishable from a
	/// denial the policy meant, which is why this could not be noticed from the outside.
	/// </para>
	/// <para>
	/// <b>It passed <c>RemoveEmptyEntries</c>.</b> That option does not blank an empty segment, it REMOVES
	/// it and shifts the rest left — so an untenanted scope lost its leading empty term, the grant type slid
	/// into the tenant position, and the length check then failed on a scope that was perfectly well formed.
	/// </para>
	/// <para>
	/// <b>It split on a literal <c>':'</c></b> rather than the separator the writer uses, so the two could
	/// diverge silently if it ever changed.
	/// </para>
	/// <para>
	/// <b>It never unescaped.</b> The writer escapes every term, so any scope whose tenant, grant type or
	/// qualifier contained a separator came back still escaped and compared unequal to itself. That one is
	/// not a parse failure at all — it produces a scope that looks valid and matches nothing.
	/// </para>
	/// <para>
	/// Delegating removes the second parser rather than correcting it. The format has one owner, and the
	/// reasoning for the complete split and the absent <c>RemoveEmptyEntries</c> is written where that
	/// owner lives; a copy here could only drift from it again.
	/// </para>
	/// </remarks>
	private static GrantScope? TryParseScope(string key)
	{
		try
		{
			return GrantScope.FromString(key);
		}
		catch (ArgumentException)
		{
			// The owner throws on a malformed scope; both callers here want to skip one. Converting at this
			// boundary keeps their contract unchanged while leaving the format's definition in one place.
			return null;
		}
	}
}
