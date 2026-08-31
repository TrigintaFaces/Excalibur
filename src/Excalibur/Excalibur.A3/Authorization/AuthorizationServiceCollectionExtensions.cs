// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection" /> to register Excalibur authorization services.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur authorization services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// <b>Requires <see cref="A3ServiceCollectionExtensions.AddExcaliburA3" /> on the same
	/// container.</b> This method is not self-sufficient: it registers components whose
	/// dependencies only that call supplies.
	/// </para>
	/// <para>
	/// The grants authorization handler registered here takes
	/// <see cref="Excalibur.A3.Authorization.IAuthorizationPolicyProvider" /> as a constructor
	/// dependency, and nothing in this method registers it -- so with this call alone the container
	/// cannot construct the handler, and validation at build time reports it as unresolvable.
	/// </para>
	/// <para>
	/// The authorization middleware registered here reads the caller's
	/// <see cref="Excalibur.A3.IAccessToken" /> from each message's request scope, and that too is
	/// registered only by the call above. Without it the token resolves to null and the middleware
	/// denies every message rather than throwing, so the omission surfaces as blanket denial rather
	/// than as a startup error.
	/// </para>
	/// <para>
	/// Microsoft's authorization engine is not a prerequisite: this method seats it itself via
	/// <c>AddAuthorizationCore()</c>, and a consumer's own <c>AddAuthorization()</c> still wins.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3();            // supplies the policy provider and the access token
	/// services.AddExcaliburAuthorization(); // adds the handler, the authorization service and the middleware
	/// </code>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"The authorization middleware resolves policy and resource types reflectively. It is registered unconditionally here, so composing authorization always brings the reflective path.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"The authorization middleware resolves policy and resource types reflectively. It is registered unconditionally here, so composing authorization always brings the reflective path.")]
	public static IServiceCollection AddExcaliburAuthorization(this IServiceCollection services)
	{
		// DispatchAuthorizationService resolves Microsoft's IAuthorizationService, and this method ships an
		// IAuthorizationHandler into that same system. No host registers IAuthorizationService by default --
		// it is absent from both Host.CreateApplicationBuilder() and WebApplication.CreateBuilder(). The
		// Core overload is the right one here: it seats the authorization engine without the web-only policy
		// evaluator, and it is TryAdd-based, so a consumer's own AddAuthorization() still wins.
		_ = services.AddAuthorizationCore();

		// Scoped because the handler consumes IAuthorizationPolicyProvider, which is registered scoped and
		// resolves the policy of whichever caller owns the current scope. A singleton handler would hold one
		// caller's provider and answer every later request from it. The handler itself keeps no grants -- it
		// re-reads the policy on each invocation -- so it is the dependency, not retained state, that fixes
		// the lifetime.
		services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, GrantsAuthorizationHandler>());
		// Scoped for its dependency subtree, not for anything it holds: it takes the caller's ClaimsPrincipal
		// as a method parameter and retains only Microsoft's IAuthorizationService, whose handler set includes
		// the scoped grants handler above. Declaring it singleton would put a scoped service under a singleton
		// root, which is what ValidateOnBuild rejects.
		services.TryAddScoped<IDispatchAuthorizationService, DispatchAuthorizationService>();
		services.TryAddSingleton(sp =>
			new AttributeAuthorizationCache(sp.GetRequiredService<ILogger<AttributeAuthorizationCache>>()));
		services.TryAddSingleton<ConditionExpressionEvaluator>();
		// Scoped, and deliberately not load-bearing. A middleware instance is materialised once, from the
		// root provider, and lives for the process, so no descriptor lifetime can give one per-request
		// state -- the middleware earns that by resolving the caller's access token from the message's own
		// request scope on each invocation instead of holding it. What the lifetime does state truthfully is
		// the dependency subtree: the authorization service above is scoped, so a singleton descriptor here
		// would be a captive dependency that ValidateOnBuild rejects.
		services.TryAddEnumerable(ServiceDescriptor.Scoped<IDispatchMiddleware, Excalibur.A3.Authorization.A3AuthorizationMiddleware>());

		return services;
	}
}
