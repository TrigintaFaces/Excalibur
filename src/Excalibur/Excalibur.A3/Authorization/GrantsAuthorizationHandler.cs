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

		IAuthorizationPolicy policy;
		try
		{
			policy = await policyProvider.GetPolicyAsync().ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// A missing principal or ambient tenant means we cannot determine the caller's grants — this is a
			// definitive "not authorized", not a server fault. Fail closed with a clean authorization failure
			// (surfaces as 403) rather than letting the exception escape as an unhandled 500. The provider
			// documents these as InvalidOperationException (missing UserId / TenantId); a transient grant-store
			// outage throws a store-specific exception and is intentionally left to surface as 500 (an honest
			// transient server fault — the request is still denied, so fail-closed holds either way).
			context.Fail();
			return;
		}

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
