// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Produces an authorization policy on demand for a convention-based grant policy name, so an endpoint
/// can be scoped to the resource named by its own route without a policy registered in advance for every
/// activity and resource combination.
/// </summary>
/// <remarks>
/// Derives from <see cref="DefaultAuthorizationPolicyProvider"/> so that every policy name this provider
/// does not recognize — including the named policies registered by <c>AddGrantAuthorization</c> and any
/// policy the application registered itself — resolves exactly as it did before.
/// </remarks>
internal sealed class GrantAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
	: DefaultAuthorizationPolicyProvider(options)
{
	/// <summary>
	/// Maximum number of generated policies retained. Policy names originate in endpoint metadata and are
	/// therefore a finite set, but the cap keeps a hostile or generated name space from growing the cache
	/// without bound; past it, policies are built per call rather than cached.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	private static readonly ConcurrentDictionary<string, AuthorizationPolicy> PolicyCache =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
	{
		ArgumentNullException.ThrowIfNull(policyName);

		if (PolicyCache.TryGetValue(policyName, out var cached))
		{
			return Task.FromResult<AuthorizationPolicy?>(cached);
		}

		if (!GrantPolicyName.TryParse(policyName, out var parsed))
		{
			return base.GetPolicyAsync(policyName);
		}

		var policy = BuildPolicy(parsed);

		if (PolicyCache.Count < MaxCacheEntries)
		{
			policy = PolicyCache.GetOrAdd(policyName, policy);
		}

		return Task.FromResult<AuthorizationPolicy?>(policy);
	}

	private static AuthorizationPolicy BuildPolicy(ParsedGrantPolicy parsed) =>
		new AuthorizationPolicyBuilder()
			// An unauthenticated caller has no grants by definition. Requiring authentication here makes
			// that case a 401 challenge rather than a 403, which is the honest status: the caller has not
			// been identified yet, as distinct from having been identified and found to lack the grant.
			.RequireAuthenticatedUser()
			.AddRequirements(
				new GrantRequirement(
					parsed.ActivityName,
					parsed.ResourceType,
					parsed.ResourceId,
					parsed.RouteValueName))
			.Build();
}
