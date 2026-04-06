// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Middleware.Timeout;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<HandlerTimeoutOptions>, HandlerTimeoutOptionsValidator>());
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
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<HandlerTimeoutOptions>, HandlerTimeoutOptionsValidator>());
		services.TryAddSingleton<HandlerTimeoutMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds per-handler timeout middleware using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="HandlerTimeoutOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddHandlerTimeoutMiddleware(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<HandlerTimeoutOptions>, HandlerTimeoutOptionsValidator>());
		_ = services.AddOptions<HandlerTimeoutOptions>().Bind(configuration).ValidateOnStart();
		services.TryAddSingleton<HandlerTimeoutMiddleware>();

		return services;
	}
}
