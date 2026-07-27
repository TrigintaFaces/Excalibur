// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Turnkey registration for gating an ASP.NET Core endpoint on an Excalibur A3 grant, without
/// hand-building a <see cref="GrantsAuthorizationRequirement"/>.
/// </summary>
public static class GrantAuthorizationServiceCollectionExtensions
{
	/// <summary>
	/// Registers a named authorization policy backed by an Excalibur A3 grant requirement, so an endpoint
	/// can be gated with <c>RequireAuthorization(policyName)</c> (or, for the operational dashboard, via
	/// <c>DashboardOptions.ReadActionsPolicy</c> / <c>MutatingActionsPolicy</c>).
	/// </summary>
	/// <remarks>
	/// The A3 grant-evaluation services (grant stores, cache, current-user/tenant context) must already be
	/// registered — this helper only wires the ASP.NET Core policy and ensures the grant authorization
	/// handler is present. The handler evaluates the caller's grants for the supplied activity and resource
	/// at request time.
	/// </remarks>
	/// <param name="services"> The service collection to add the policy to. </param>
	/// <param name="policyName"> The authorization policy name to register (referenced by <c>RequireAuthorization</c>). </param>
	/// <param name="activityName"> The grant activity the caller must hold to satisfy the policy. </param>
	/// <param name="resourceTypes"> The resource types the activity applies to. </param>
	/// <param name="resourceId"> The optional specific resource identifier being accessed. </param>
	/// <returns> The same service collection, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> or <paramref name="resourceTypes"/> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="policyName"/> or <paramref name="activityName"/> is null or whitespace. </exception>
	public static IServiceCollection AddGrantAuthorization(
		this IServiceCollection services,
		string policyName,
		string activityName,
		string[] resourceTypes,
		string? resourceId = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
		ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
		ArgumentNullException.ThrowIfNull(resourceTypes);

		// Ensure the authorization core + grant handler are present (idempotent — safe to call repeatedly
		// and alongside AddExcaliburAuthorization).
		services.AddAuthorizationCore();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, GrantsAuthorizationHandler>());

		// Register the named policy backed by the A3 grant requirement.
		services.Configure<AuthorizationOptions>(options =>
			options.AddPolicy(
				policyName,
				policy => policy.AddRequirements(new GrantsAuthorizationRequirement(activityName, resourceTypes, resourceId))));

		return services;
	}
}
