// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Represents an authorization policy that evaluates user permissions and grants based on activities and resources.
/// </summary>
/// <remarks>
/// <para>
/// This policy evaluates grant data in pure C# to determine access rights.
/// Grants are keyed by scope string in the format <c>{TenantId}:{GrantType}:{Qualifier}</c>.
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
	private readonly IDictionary<string, object> _activityGroups;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizationPolicy"/> class.
	/// </summary>
	/// <param name="grants">The user's grants, keyed by scope string.</param>
	/// <param name="activityGroups">Activity group mappings.</param>
	/// <param name="tenantId">The tenant identifier for the current context.</param>
	/// <param name="userId">The user identifier for the current context.</param>
	public AuthorizationPolicy(
		IDictionary<string, object> grants,
		IDictionary<string, object> activityGroups,
		ITenantId tenantId,
		string userId)
	{
		_activityGroups = activityGroups;
		TenantId = tenantId.Value;
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
	public bool IsAuthorized(string activityName, string? resourceId = null)
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
			var activityKey = $"{TenantId}:{GrantType.Activity}:{activity}";
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
			var resourceKey = $"{TenantId}:{resourceType}:{resource}";
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

			if (IsActivityInGroup(scope.Qualifier, activity))
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

			// For wildcard activity group grants, check all activity groups
			foreach (var (groupName, _) in _activityGroups)
			{
				if (WildcardGrantMatcher.Matches(
					scope.TenantId,
					scope.GrantType,
					scope.Qualifier,
					TenantId,
					GrantType.ActivityGroup,
					groupName) && IsActivityInGroup(groupName, activity))
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Checks whether the specified activity is contained in the named activity group.
	/// </summary>
	private bool IsActivityInGroup(string groupName, string activity)
	{
		return _activityGroups.TryGetValue(groupName, out var groupData)
			&& groupData is IEnumerable<object> groupActivities
			&& groupActivities.Any(a => string.Equals(a?.ToString(), activity, StringComparison.Ordinal));
	}

	/// <summary>
	/// Attempts to parse a scope key string in the format <c>{TenantId}:{GrantType}:{Qualifier}</c>.
	/// </summary>
	/// <param name="key"> The scope key string. </param>
	/// <returns> A <see cref="GrantScope"/> if parsing succeeds; otherwise, <see langword="null"/>. </returns>
	private static GrantScope? TryParseScope(string key)
	{
		var parts = key.Split(':', 3, StringSplitOptions.RemoveEmptyEntries);
		return parts.Length == 3 ? new GrantScope(parts[0], parts[1], parts[2]) : null;
	}
}
