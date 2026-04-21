// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;

namespace Excalibur.A3.Authorization.Roles;

/// <summary>
/// Decorator for <see cref="IAuthorizationEvaluator"/> that resolves <see cref="GrantType.Role"/>
/// grants to their effective permissions before delegating to the inner evaluator.
/// </summary>
/// <remarks>
/// <para>
/// For each Role grant the subject holds, the decorator expands the role's
/// <see cref="RoleSummary.ActivityGroupNames"/> and <see cref="RoleSummary.ActivityNames"/>
/// into concrete activity names, then synthesizes virtual Activity grants so the inner
/// evaluator can authorize normally.
/// </para>
/// <para>
/// Direct Activity and ActivityGroup grants pass through unchanged. This ensures the
/// three-tier permission model (Activity → ActivityGroup → Role) is fully transparent
/// to the inner evaluator.
/// </para>
/// </remarks>
internal sealed class RoleAwareAuthorizationEvaluator(
	IAuthorizationEvaluator inner,
	IGrantStore grantStore,
	RolePermissionResolver resolver) : IAuthorizationEvaluator
{
	/// <inheritdoc />
	public async Task<AuthorizationDecision> EvaluateAsync(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource,
		CancellationToken cancellationToken)
	{
		// Get all grants for the subject
		var grants = await grantStore.GetAllGrantsAsync(subject.ActorId, cancellationToken)
			.ConfigureAwait(false);

		// Collect effective activities from Role grants
		var expandedActivities = new HashSet<string>(StringComparer.Ordinal);
		var hasRoleGrants = false;

		foreach (var grant in grants)
		{
			if (!string.Equals(grant.GrantType, GrantType.Role, StringComparison.Ordinal))
			{
				continue;
			}

			hasRoleGrants = true;
			var roleActivities = await resolver.ResolveRolePermissionsAsync(
				grant.Qualifier, cancellationToken).ConfigureAwait(false);

			foreach (var activity in roleActivities)
			{
				expandedActivities.Add(activity);
			}
		}

		if (!hasRoleGrants || expandedActivities.Count == 0)
		{
			// No Role grants or no expanded activities -- delegate directly
			return await inner.EvaluateAsync(subject, action, resource, cancellationToken)
				.ConfigureAwait(false);
		}

		// Enrich the subject's attributes with expanded role activities so the
		// inner evaluator can make decisions based on the full effective permission set.
		var enrichedAttributes = new Dictionary<string, string>(
			subject.Attributes ?? new Dictionary<string, string>(),
			StringComparer.Ordinal)
		{
			["RoleExpandedActivities"] = string.Join(",", expandedActivities),
		};

		var enrichedSubject = subject with { Attributes = enrichedAttributes };

		return await inner.EvaluateAsync(enrichedSubject, action, resource, cancellationToken)
			.ConfigureAwait(false);
	}
}
