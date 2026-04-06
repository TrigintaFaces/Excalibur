// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.Timeout;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering per-handler timeout middleware services.
/// </summary>
public static class HandlerTimeoutServiceCollectionExtensions
{
	/// <summary>
	/// Adds per-handler timeout middleware with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddHandlerTimeoutMiddleware(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<HandlerTimeoutMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds per-handler timeout middleware with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for handler timeout options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddHandlerTimeoutMiddleware(
		this IServiceCollection services,
		Action<HandlerTimeoutOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.Configure(configure);
		services.TryAddSingleton<HandlerTimeoutMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds per-handler timeout middleware using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="HandlerTimeoutOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddHandlerTimeoutMiddleware(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming
#pragma warning disable IL3050 // Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling
		_ = services.AddOptions<HandlerTimeoutOptions>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();
#pragma warning restore IL3050
#pragma warning restore IL2026
		services.TryAddSingleton<HandlerTimeoutMiddleware>();

		return services;
	}
}
