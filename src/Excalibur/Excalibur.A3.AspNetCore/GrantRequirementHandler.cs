// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using A3PolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Evaluates a <see cref="GrantRequirement"/> against the caller's grants, resolving the resource
/// identifier from the matched endpoint's route values when the requirement is route-scoped.
/// </summary>
/// <remarks>
/// Registered as scoped: the grant policy provider it depends on is scoped, because it reads the
/// per-request identity and tenant. A singleton handler would capture the first request's provider for
/// the lifetime of the process and authorize every later request as that first caller.
/// </remarks>
internal sealed partial class GrantRequirementHandler(
	A3PolicyProvider policyProvider,
	IHttpContextAccessor httpContextAccessor,
	ILogger<GrantRequirementHandler> logger) : AuthorizationHandler<GrantRequirement>
{
	/// <inheritdoc />
	protected override async Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		GrantRequirement requirement)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(requirement);

		if (context.User.Identity?.IsAuthenticated != true)
		{
			// Abstain rather than deny-and-log. An unauthenticated caller holds no grants by definition and
			// is already refused by the authentication requirement on the same policy, which produces a 401
			// challenge instead of a 403. Neither succeeding nor failing here leaves the requirement
			// unsatisfied, so the outcome is still a denial — but an unauthenticated scanner cannot flood
			// the log with warnings and stack traces describing a misconfiguration that does not exist.
			return;
		}

		var resourceId = requirement.ResourceId;

		if (requirement.ResourceIdRouteValue is { } routeValueName)
		{
			var httpContext = httpContextAccessor.HttpContext;

			if (httpContext is null
				|| !httpContext.Request.RouteValues.TryGetValue(routeValueName, out var routeValue)
				|| routeValue is null)
			{
				// Fail closed. A resource-scoped policy applied to an endpoint that has no such route
				// parameter is a misconfiguration, and the only alternative to denying is to evaluate the
				// unscoped grant instead — which is strictly more permissive than what was asked for.
				LogResourceScopeUnresolved(routeValueName, requirement.ActivityName);
				context.Fail();
				return;
			}

			resourceId = routeValue.ToString();
		}

		Authorization.IAuthorizationPolicy policy;
		try
		{
			policy = await policyProvider.GetPolicyAsync().ConfigureAwait(false);
		}
		catch (InvalidOperationException ex)
		{
			// A missing user or tenant is a definitive "not authorized", not a server fault, so this is a
			// clean denial rather than an escaping 500. It is logged because the resulting 403 is
			// otherwise indistinguishable from a caller who was identified and simply lacks the grant.
			LogIdentityUnresolved(requirement.ActivityName, ex);
			context.Fail();
			return;
		}

		if (policy.IsAuthorized(requirement.ActivityName, resourceId))
		{
			LogGrantAuthorized(requirement.ActivityName, requirement.ResourceType, resourceId);
			context.Succeed(requirement);
		}
		else
		{
			LogGrantDenied(requirement.ActivityName, requirement.ResourceType, resourceId);
			context.Fail();
		}
	}

	[LoggerMessage(GrantAuthorizationEventId.GrantAuthorized, LogLevel.Debug,
		"Grant authorization succeeded for activity {ActivityName} on {ResourceType} {ResourceId}")]
	private partial void LogGrantAuthorized(string activityName, string resourceType, string? resourceId);

	[LoggerMessage(GrantAuthorizationEventId.GrantDenied, LogLevel.Information,
		"Grant authorization denied for activity {ActivityName} on {ResourceType} {ResourceId}: the caller does not hold the grant")]
	private partial void LogGrantDenied(string activityName, string resourceType, string? resourceId);

	[LoggerMessage(GrantAuthorizationEventId.IdentityUnresolved, LogLevel.Warning,
		"Grant authorization denied for activity {ActivityName} because the caller's identity or tenant could not be resolved from the request. " +
		"Confirm authentication runs before authorization and that the principal carries the configured user and tenant claims.")]
	private partial void LogIdentityUnresolved(string activityName, Exception exception);

	[LoggerMessage(GrantAuthorizationEventId.ResourceScopeUnresolved, LogLevel.Warning,
		"Grant authorization denied for activity {ActivityName} because route parameter {RouteValueName} carried no value on the matched endpoint. " +
		"The policy is scoped to that route parameter, so the endpoint's route template must declare it.")]
	private partial void LogResourceScopeUnresolved(string routeValueName, string activityName);
}
