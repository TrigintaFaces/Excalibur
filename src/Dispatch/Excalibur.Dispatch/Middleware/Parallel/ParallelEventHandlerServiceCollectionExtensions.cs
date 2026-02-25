// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.ParallelExecution;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering parallel event handler middleware services.
/// </summary>
public static class ParallelEventHandlerServiceCollectionExtensions
{
	/// <summary>
	/// Adds parallel event handler middleware with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddParallelEventHandlerMiddleware(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ParallelEventHandlerMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds parallel event handler middleware with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for parallel execution options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddParallelEventHandlerMiddleware(
		this IServiceCollection services,
		Action<ParallelEventHandlerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.Configure(configure);
		services.TryAddSingleton<ParallelEventHandlerMiddleware>();

		return services;
	}
}
