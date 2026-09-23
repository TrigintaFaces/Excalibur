// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

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

		AddTenantContextResolution(services);

		// Fail-closed consistency guard: RequireTenant==false requires the framework single-tenant default
		// context; a custom resolving context in single-tenant mode is the silent cross-tenant-loss config
		// and is rejected at startup. ValidateOnStart makes it fire before the first message.
		_ = services.AddOptions<TenantContextOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TenantContextOptions>, TenantContextConsistencyValidator>());

		return services;
	}

	/// <summary>
	/// Contributes a tenant-context mode. <see cref="ITenantContext"/> resolves to the contributed mode with the
	/// highest <see cref="ITenantContextMode.Precedence"/>, whatever order modes were contributed in.
	/// </summary>
	/// <typeparam name="TMode">The mode to contribute.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Idempotent: contributing the same mode twice has the effect of contributing it once. The single-tenant
	/// default is always present, at precedence 0.
	/// </para>
	/// <para>
	/// Two different modes with the same highest precedence are refused when the host starts, and on first
	/// resolution, naming both. A context registered directly as <see cref="ITenantContext"/> takes the place
	/// of every mode.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddTenantContextMode<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMode>(
		this IServiceCollection services)
		where TMode : class, ITenantContextMode
	{
		ArgumentNullException.ThrowIfNull(services);

		AddTenantContextResolution(services);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantContextMode, TMode>());

		return services;
	}

	// One resolver, one default mode, and the startup check for an ambiguous choice. Every mode registration
	// goes through here, so no package ever replaces ITenantContext itself: that replacement is what made the
	// winner depend on which package's registration ran last.
	private static void AddTenantContextResolution(IServiceCollection services)
	{
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantContextMode, SingleTenantContextMode>());
		services.TryAddSingleton<ITenantContext>(TenantContextModeResolver.Resolve);

		_ = services.AddOptions<TenantContextOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TenantContextOptions>, TenantContextModeValidator>());
	}
}
