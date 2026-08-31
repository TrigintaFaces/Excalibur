// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the ambient tenant context.
/// </summary>
public static class TenantContextServiceCollectionExtensions
{
	/// <summary>
	/// Registers the ambient <see cref="ITenantContext"/> with validated
	/// <see cref="TenantContextOptions"/>. The context reads the ambient tenant established by
	/// <see cref="TenantContextHolder.BeginScope"/>, which a host opens for the duration of a request
	/// or message from the tenant its inbound pipeline resolved.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration for <see cref="TenantContextOptions"/>.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddTenantContext(
		this IServiceCollection services,
		Action<TenantContextOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var builder = services.AddOptions<TenantContextOptions>();
		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TenantContextOptions>, TenantContextOptionsValidator>());

		// Replace (not TryAdd): the ambient context must win over the fail-closed single-tenant
		// default registered on the core store path, regardless of composition order.
		services.Replace(ServiceDescriptor.Singleton<ITenantContext, AmbientTenantContext>());

		return services;
	}
}
