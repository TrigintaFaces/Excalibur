// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="IDispatchBuilder"/> for registering the ASP.NET Core authorization bridge.
/// </summary>
/// <remarks>
/// This extension registers <see cref="AspNetCoreAuthorizationMiddleware"/> in the Dispatch pipeline,
/// bridging ASP.NET Core's <c>[Authorize]</c> attribute and <c>IAuthorizationService</c> policy evaluation
/// into message handling. It also ensures <see cref="IHttpContextAccessor"/> is available in DI.
/// </remarks>
public static class DispatchAspNetCoreAuthorizationExtensions
{
	/// <summary>
	/// Adds ASP.NET Core authorization bridge middleware to the Dispatch pipeline.
	/// </summary>
	/// <param name="builder">The Dispatch builder.</param>
	/// <param name="configure">
	/// An optional action to configure <see cref="AspNetCoreAuthorizationOptions"/>.
	/// When <see langword="null"/>, default options are used.
	/// </param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This method registers middleware that reads <c>[Authorize]</c> attributes from message and handler types
	/// and evaluates them against the <c>ClaimsPrincipal</c> from <c>HttpContext.User</c>.
	/// </para>
	/// <para>
	/// Policies are composed by the host's own <c>IAuthorizationPolicyProvider</c> and evaluated by its
	/// <c>IAuthorizationService</c>, so everything configured through <c>AddAuthorization</c> applies here
	/// unchanged: named policies, requirements, handlers, and the default policy a bare <c>[Authorize]</c>
	/// resolves to. There is no separate default-policy setting on this middleware, deliberately — a second
	/// place to configure the same thing is a second place for the two to disagree, and the host's is the one
	/// that governs the rest of the application.
	/// </para>
	/// <para>
	/// Usage:
	/// <code>
	/// // The host configures authorization once, as it would for controllers or endpoints.
	/// services.AddAuthorization(options =>
	/// {
	///     options.DefaultPolicy = new AuthorizationPolicyBuilder()
	///         .RequireAuthenticatedUser()
	///         .RequireClaim("scope", "orders.write")
	///         .Build();
	/// });
	///
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAspNetCoreAuthorization(options =>
	///     {
	///         options.RequireAuthenticatedUser = true;
	///     });
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseAspNetCoreAuthorization(
		this IDispatchBuilder builder,
		Action<AspNetCoreAuthorizationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AspNetCoreAuthorizationOptions>, AspNetCoreAuthorizationOptionsValidator>());

		var optionsBuilder = builder.Services.AddOptions<AspNetCoreAuthorizationOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateOnStart();

		return builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>();
	}
}
