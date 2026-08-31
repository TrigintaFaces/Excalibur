// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the default <see cref="ITenantContext"/>.
/// </summary>
public static class DefaultTenantContextServiceCollectionExtensions
{
	/// <summary>
	/// Registers the fail-closed single-tenant default <see cref="ITenantContext"/> if no context has
	/// been registered yet.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	/// <remarks>
	/// A store that scopes rows by tenant requires an <see cref="ITenantContext"/>. This provides the
	/// non-null single-tenant default so <c>GetRequiredService&lt;ITenantContext&gt;()</c> always
	/// resolves; the multi-tenancy composition replaces it with the ambient, resolver-driven context.
	/// Idempotent: uses <c>TryAdd</c>, so an already-registered context wins.
	/// Also wires a fail-closed startup guard (<see cref="TenantContextConsistencyValidator"/>) that rejects
	/// the silent cross-tenant loss configuration — a resolving <see cref="ITenantContext"/> registered while
	/// the deployment stays in single-tenant mode (<see cref="TenantContextOptions.RequireTenant"/> false).
	/// The guard runs before the first message whenever a tenant-scoped store is registered.
	/// </remarks>
	public static IServiceCollection AddDefaultTenantContext(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ITenantContext, SingleTenantContext>();

		// Fail-closed consistency guard: RequireTenant==false requires the framework single-tenant default
		// context; a custom resolving context in single-tenant mode is the silent cross-tenant-loss config
		// and is rejected at startup. ValidateOnStart makes it fire before the first message.
		_ = services.AddOptions<TenantContextOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TenantContextOptions>, TenantContextConsistencyValidator>());

		return services;
	}
}
