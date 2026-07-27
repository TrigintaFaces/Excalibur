// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for per-tenant options.
/// </summary>
public static class TenantOptionsServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="ITenantOptions{TOptions}"/> so consumers can resolve <typeparamref name="TOptions"/>
	/// for the current ambient tenant. Configure per-tenant values with the standard named-options API keyed by
	/// tenant id (<c>services.Configure&lt;TOptions&gt;(tenantId, …)</c>); an unconfigured tenant gets the
	/// default options. Requires <c>AddTenantContext</c> to have been called.
	/// </summary>
	/// <typeparam name="TOptions">The options type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddTenantOptions<TOptions>(this IServiceCollection services)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions();
		services.TryAddSingleton<ITenantOptions<TOptions>, TenantOptions<TOptions>>();

		return services;
	}
}
