// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Pooling;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering context pooling services.
/// </summary>
public static class ContextPoolingServiceCollectionExtensions
{
	/// <summary>
	/// Adds configurable message context pooling for high-throughput scenarios.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for context pooling options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// When enabled, <see cref="IMessageContextPool"/> instances are recycled from an object pool
	/// instead of being allocated fresh for each dispatch, reducing GC pressure.
	/// </remarks>
	public static IServiceCollection AddContextPooling(
		this IServiceCollection services,
		Action<ContextPoolingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.Configure(configure);
		services.Replace(ServiceDescriptor.Singleton<IMessageContextPool, MessageContextPoolAdapter>());

		return services;
	}

	/// <summary>
	/// Adds configurable message context pooling with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddContextPooling(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.Replace(ServiceDescriptor.Singleton<IMessageContextPool, MessageContextPoolAdapter>());

		return services;
	}
}
