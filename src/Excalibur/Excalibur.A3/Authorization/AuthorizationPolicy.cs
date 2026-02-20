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
/// This policy evaluates grant data in pure C# to determine access rights.
/// Grants are keyed by scope string in the format <c>{TenantId}:{GrantType}:{Qualifier}</c>.
/// </remarks>
/// <param name="grants"> The user's grants, keyed by scope string. </param>
/// <param name="activityGroups"> Activity group mappings. </param>
/// <param name="tenantId"> The tenant identifier for the current context. </param>
/// <param name="userId"> The user identifier for the current context. </param>
public sealed class AuthorizationPolicy(
	IDictionary<string, object> grants,
	IDictionary<string, object> activityGroups,
	ITenantId tenantId,
	string userId) : IAuthorizationPolicy
{
	/// <inheritdoc />
	public string TenantId { get; } = tenantId.Value;

	/// <inheritdoc />
	public string UserId { get; } = userId;

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
			// Check direct activity grant: key = "{TenantId}:Activity:{activityName}"
			var activityKey = $"{TenantId}:{GrantType.Activity}:{activity}";
			hasActivityGrant = grants.ContainsKey(activityKey);

			// Check activity group grants
			if (!hasActivityGrant)
			{
				hasActivityGrant = HasActivityGroupGrant(activity);
			}
		}

		if (resourceType != null && resource != null)
		{
			// Check resource-level grant: key = "{TenantId}:{resourceType}:{resourceId}"
			var resourceKey = $"{TenantId}:{resourceType}:{resource}";
			hasResourceGrant = grants.ContainsKey(resourceKey);
		}

		return new PolicyResult
		{
			IsAuthorized = hasActivityGrant || hasResourceGrant,
			HasActivityGrant = hasActivityGrant,
			HasResourceGrant = hasResourceGrant,
		};
	}

	/// <summary>
	/// Checks whether the user has a grant for an activity group that contains the specified activity.
	/// </summary>
	/// <param name="activity"> The activity name to check. </param>
	/// <returns> <see langword="true"/> if the user has an activity group grant containing the activity. </returns>
	private bool HasActivityGroupGrant(string activity)
	{
		foreach (var (key, _) in grants)
		{
			var scope = TryParseScope(key);
			if (scope == null
				|| !string.Equals(scope.GrantType, GrantType.ActivityGroup, StringComparison.Ordinal)
				|| !string.Equals(scope.TenantId, TenantId, StringComparison.Ordinal))
			{
				continue;
			}

			if (activityGroups.TryGetValue(scope.Qualifier, out var groupData)
				&& groupData is IEnumerable<object> groupActivities
				&& groupActivities.Any(a => string.Equals(a?.ToString(), activity, StringComparison.Ordinal)))
			{
				return true;
			}
		}

		return false;
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
