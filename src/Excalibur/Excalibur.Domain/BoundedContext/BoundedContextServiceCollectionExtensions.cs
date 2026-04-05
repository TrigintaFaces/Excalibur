// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.BoundedContext;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring bounded context enforcement services.
/// </summary>
public static class BoundedContextServiceCollectionExtensions
{
	/// <summary>
	/// Adds bounded context enforcement services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure bounded context options.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("DefaultBoundedContextValidator uses AppDomain.GetAssemblies() to discover bounded context types at runtime.")]
	public static IServiceCollection AddBoundedContextEnforcement(
		this IServiceCollection services,
		Action<BoundedContextOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<BoundedContextOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddSingleton<IBoundedContextValidator, DefaultBoundedContextValidator>();

		return services;
	}

	/// <summary>
	/// Adds bounded context enforcement services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("DefaultBoundedContextValidator uses AppDomain.GetAssemblies() to discover bounded context types at runtime.")]
	public static IServiceCollection AddBoundedContextEnforcement(this IServiceCollection services)
	{
		return services.AddBoundedContextEnforcement(_ => { });
	}

	/// <summary>
	/// Adds bounded context enforcement services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("DefaultBoundedContextValidator uses AppDomain.GetAssemblies() to discover bounded context types at runtime. Configuration binding requires unreferenced code.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation at runtime.")]
	public static IServiceCollection AddBoundedContextEnforcement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<BoundedContextOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddSingleton<IBoundedContextValidator, DefaultBoundedContextValidator>();

		return services;
	}
}
