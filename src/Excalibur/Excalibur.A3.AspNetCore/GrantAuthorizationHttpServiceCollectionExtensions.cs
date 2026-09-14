// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.AspNetCore;
using Excalibur.A3.Authentication;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers grant authorization for ASP.NET Core endpoints, so a controller action or a minimal API
/// endpoint can be gated on an Excalibur A3 grant without going through Dispatch.
/// </summary>
public static class GrantAuthorizationHttpServiceCollectionExtensions
{
	/// <summary>
	/// Bridges the authenticated request principal into grant authorization and enables convention-based
	/// grant policy names, including per-request resource scope taken from the endpoint's route.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This registers the identity and tenant that grant evaluation reads, derived from the
	/// <c>ClaimsPrincipal</c> that ASP.NET Core authentication established for the request. It performs
	/// no authentication of its own — configure an authentication scheme as usual, and ensure
	/// <c>UseAuthentication</c> runs before <c>UseAuthorization</c>.
	/// </para>
	/// <para>
	/// The A3 grant-evaluation services must also be registered (<c>AddExcaliburA3</c> or
	/// <c>AddExcaliburA3Core</c>). A startup check verifies the composition and fails with an actionable
	/// message if a piece is missing, rather than letting every request be denied.
	/// </para>
	/// <example>
	/// <code>
	/// builder.Services
	///     .AddExcaliburA3()
	///     .Services
	///     .AddHttpGrantAuthorization();
	///
	/// // Minimal API, scoped to the order named by the route:
	/// app.MapGet("/orders/{id}", (string id) =&gt; Results.Ok(id))
	///    .RequireAuthorization(GrantPolicyName.ForRouteValue("Read", "Order", "id"));
	///
	/// // Controller action, same policy written as the literal name:
	/// // [Authorize(Policy = "grant:Read:Order:{id}")]
	/// </code>
	/// </example>
	/// </remarks>
	/// <param name="services"> The service collection to register into. </param>
	/// <param name="configure"> Optional configuration of the claim types used to resolve user and tenant. </param>
	/// <returns> The same service collection, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	public static IServiceCollection AddHttpGrantAuthorization(
		this IServiceCollection services,
		Action<GrantAuthorizationHttpOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<GrantAuthorizationHttpOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		// An empty claim list resolves nobody, so authorization fails closed on every request and reads as a
		// broken grant store rather than a misconfigured mapping. Refuse it at start-up instead.
		_ = optionsBuilder.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GrantAuthorizationHttpOptions>, GrantAuthorizationHttpOptionsValidator>());

		_ = services.AddHttpContextAccessor();

		// AddAuthorization, not AddAuthorizationCore: UseAuthorization refuses to build a pipeline
		// without the services this registers, and this package exists precisely for hosts that call it.
		_ = services.AddAuthorization();

		// TryAdd, not Replace: an application that already supplies its own identity bridge keeps it.
		services.TryAddScoped<IAuthenticationToken, HttpContextAuthenticationToken>();

		// Replace, not TryAdd: AddTenantContext registers the ambient context with Replace, so a TryAdd
		// here would lose to it and the tenant claim would never be read. The replacement still honours
		// an ambient tenant when the principal carries no tenant claim.
		services.Replace(ServiceDescriptor.Scoped<ITenantContext, HttpContextTenantContext>());

		// Replace: the grant-aware provider must be the one ASP.NET Core asks, and it delegates every
		// name it does not recognize to the default provider it derives from.
		services.Replace(ServiceDescriptor.Transient<IAuthorizationPolicyProvider, GrantAuthorizationPolicyProvider>());

		// Scoped, matching the per-request lifetime of the identity and tenant it reads through.
		services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, GrantRequirementHandler>());

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, GrantAuthorizationBridgeStartupValidator>());

		return services;
	}
}
