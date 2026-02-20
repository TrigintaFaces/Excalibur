// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Authorization;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Authorization handler for <see cref="GrantsAuthorizationRequirement" /> that validates user permissions based on grants.
/// </summary>
/// <param name="policyProvider">The authorization policy provider to retrieve user policies.</param>
public sealed class GrantsAuthorizationHandler(IAuthorizationPolicyProvider policyProvider)
	: AuthorizationHandler<GrantsAuthorizationRequirement>
{
	/// <summary>
	/// Handles the grants authorization requirement by checking if the user has the necessary permissions.
	/// </summary>
	/// <param name="context">The authorization context containing user information.</param>
	/// <param name="requirement">The grants authorization requirement to evaluate.</param>
	/// <returns>A task representing the asynchronous authorization operation.</returns>
	protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GrantsAuthorizationRequirement requirement)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(requirement);

		var policy = await policyProvider.GetPolicyAsync().ConfigureAwait(false);
		var authorized = policy.IsAuthorized(requirement.ActivityName, requirement.ResourceId);

		if (authorized)
		{
			context.Succeed(requirement);
		}
		else
		{
			context.Fail();
		}
	}
}
