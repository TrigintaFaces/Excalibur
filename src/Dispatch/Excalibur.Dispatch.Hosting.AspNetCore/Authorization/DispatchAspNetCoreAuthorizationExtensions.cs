// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
	/// This method registers middleware that reads <c>[Authorize]</c> attributes from message and handler types,
	/// evaluates named policies via <c>IAuthorizationService</c>, and checks roles against the
	/// <c>ClaimsPrincipal</c> from <c>HttpContext.User</c>.
	/// </para>
	/// <para>
	/// Usage:
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseAspNetCoreAuthorization(options =>
	///     {
	///         options.RequireAuthenticatedUser = true;
	///         options.DefaultPolicy = "MyPolicy";
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

		var optionsBuilder = builder.Services.AddOptions<AspNetCoreAuthorizationOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		return builder.UseMiddleware<AspNetCoreAuthorizationMiddleware>();
	}
}
