// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Roles;
using Excalibur.A3.Governance.SeparationOfDuties;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default implementation of <see cref="ISoDEvaluator"/> that checks a user's grants
/// against all SoD policies from <see cref="ISoDPolicyStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// For Role-scoped policies, the evaluator checks whether the user holds grants for
/// two or more of the conflicting role names. For Activity-scoped policies, the evaluator
/// expands the user's role grants to effective activities using <see cref="RolePermissionResolver"/>
/// and checks the expanded set.
/// </para>
/// </remarks>
internal sealed class DefaultSoDEvaluator(
	ISoDPolicyStore policyStore,
	IGrantStore grantStore,
	RolePermissionResolver? rolePermissionResolver = null) : ISoDEvaluator
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<SoDConflict>> EvaluateCurrentAsync(
		string userId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);

		var policies = await policyStore.GetAllPoliciesAsync(cancellationToken).ConfigureAwait(false);
		if (policies.Count == 0)
		{
			return [];
		}

		var grants = await grantStore.GetAllGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
		if (grants.Count == 0)
		{
			return [];
		}

		return await EvaluatePoliciesAsync(userId, grants, additionalScope: null,
			additionalGrantType: null, policies, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SoDConflict>> EvaluateHypotheticalAsync(
		string userId,
		string proposedScope,
		string proposedGrantType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);
		ArgumentException.ThrowIfNullOrEmpty(proposedScope);
		ArgumentException.ThrowIfNullOrEmpty(proposedGrantType);

		var policies = await policyStore.GetAllPoliciesAsync(cancellationToken).ConfigureAwait(false);
		if (policies.Count == 0)
		{
			return [];
		}

		var grants = await grantStore.GetAllGrantsAsync(userId, cancellationToken).ConfigureAwait(false);

		return await EvaluatePoliciesAsync(userId, grants, additionalScope: proposedScope,
			additionalGrantType: proposedGrantType, policies, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}

	private async Task<IReadOnlyList<SoDConflict>> EvaluatePoliciesAsync(
		string userId,
		IReadOnlyList<Excalibur.A3.Abstractions.Authorization.Grant> grants,
		string? additionalScope,
		string? additionalGrantType,
		IReadOnlyList<SoDPolicy> policies,
		CancellationToken cancellationToken)
	{
		var conflicts = new List<SoDConflict>();
		var now = DateTimeOffset.UtcNow;

		// Build the user's effective role set and activity set
		var userRoles = new HashSet<string>(StringComparer.Ordinal);
		var userActivities = new HashSet<string>(StringComparer.Ordinal);

		foreach (var grant in grants)
		{
			if (string.Equals(grant.GrantType, GrantType.Role, StringComparison.Ordinal))
			{
				userRoles.Add(grant.Qualifier);

				// Expand role to activities for Activity-scoped policy checks
				if (rolePermissionResolver is not null)
				{
					var activities = await rolePermissionResolver.ResolveRolePermissionsAsync(
						grant.Qualifier, cancellationToken).ConfigureAwait(false);
					foreach (var activity in activities)
					{
						userActivities.Add(activity);
					}
				}
			}
			else if (string.Equals(grant.GrantType, GrantType.Activity, StringComparison.Ordinal))
			{
				userActivities.Add(grant.Qualifier);
			}
		}

		// Add hypothetical scope if provided, using the grant type for targeted placement
		if (additionalScope is not null)
		{
			if (string.Equals(additionalGrantType, GrantType.Role, StringComparison.Ordinal))
			{
				userRoles.Add(additionalScope);

				// Expand hypothetical role to activities for Activity-scoped policy checks
				if (rolePermissionResolver is not null)
				{
					var activities = await rolePermissionResolver.ResolveRolePermissionsAsync(
						additionalScope, cancellationToken).ConfigureAwait(false);
					foreach (var activity in activities)
					{
						userActivities.Add(activity);
					}
				}
			}
			else
			{
				// Activity or ActivityGroup grants go to the activity set
				userActivities.Add(additionalScope);
			}
		}

		// Evaluate each policy
		foreach (var policy in policies)
		{
			var itemSet = policy.PolicyScope switch
			{
				SoDPolicyScope.Role => userRoles,
				SoDPolicyScope.Activity => userActivities,
				_ => userRoles,
			};

			// Find which conflicting items the user has
			var matchedItems = new List<string>();
			foreach (var item in policy.ConflictingItems)
			{
				if (itemSet.Contains(item))
				{
					matchedItems.Add(item);
				}
			}

			// SoD violation if user has 2 or more of the conflicting items
			if (matchedItems.Count < 2)
			{
				continue;
			}

			// Generate conflict records for each pair
			for (var i = 0; i < matchedItems.Count; i++)
			{
				for (var j = i + 1; j < matchedItems.Count; j++)
				{
					conflicts.Add(new SoDConflict(
						PolicyId: policy.PolicyId,
						UserId: userId,
						ConflictingItem1: matchedItems[i],
						ConflictingItem2: matchedItems[j],
						DetectedAt: now,
						Severity: policy.Severity));
				}
			}
		}

		return conflicts;
	}
}
