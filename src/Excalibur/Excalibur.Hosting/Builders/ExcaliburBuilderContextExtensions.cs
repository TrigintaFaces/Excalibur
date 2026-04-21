// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Domain;
using Excalibur.Hosting.Builders;

using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="IExcaliburBuilder"/> for configuring Excalibur
/// cross-cutting context primitives (tenant, client address) and reflective
/// assembly scanning (handlers + validators).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExcaliburHostingServiceCollectionExtensions.AddExcalibur(IServiceCollection, System.Action{IExcaliburBuilder})"/>
/// registers <see cref="IActivityContext"/> and the context family (tenant, correlation,
/// ETag, client address) with safe <c>TryAdd</c> defaults AFTER the builder configure
/// callback runs. Calling <see cref="UseTenant(IExcaliburBuilder, string)"/> or
/// <see cref="UseLocalClientAddress(IExcaliburBuilder)"/> inside the configure callback
/// registers an override that wins against the default TryAdd.
/// </para>
/// <para>
/// These replace the <c>tenantId</c> / <c>useLocalClientAddress</c> / <c>assemblies</c>
/// parameters on <c>AddExcaliburBaseServices</c> / <c>AddExcaliburApplicationServices</c>
/// so consumers stay inside the single <see cref="IExcaliburBuilder"/> composition root
/// (ADR-321).
/// </para>
/// </remarks>
public static class ExcaliburBuilderContextExtensions
{
	/// <summary>
	/// Sets the default <see cref="ITenantId"/> registration before the context family
	/// defaults land. Consumers that need a per-request tenant resolver should register
	/// <c>ITenantId</c> explicitly via <c>services.TryAddTenantId(sp =&gt; ...)</c> instead.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder UseTenant(this IExcaliburBuilder builder, string tenantId)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		_ = builder.Services.TryAddTenantId(tenantId);
		return builder;
	}

	/// <summary>
	/// Replaces the default scoped <see cref="IClientAddress"/> with the local-machine
	/// resolver.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder UseLocalClientAddress(this IExcaliburBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.TryAddLocalClientAddress();
		return builder;
	}

	/// <summary>
	/// Explicit opt-in for reflective assembly scanning: registers dispatch handlers
	/// and FluentValidation validators discovered in the supplied assemblies.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="assemblies">Assemblies to scan.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the composition-root equivalent of the <c>params Assembly[]</c> argument
	/// previously passed to <c>AddExcaliburBaseServices</c> / <c>AddExcaliburApplicationServices</c>.
	/// Callers who want AOT-friendly registration should use explicit <c>AddScoped</c>
	/// / <c>AddDispatchHandler&lt;THandler&gt;</c> registrations instead.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover handlers and validators.")]
	public static IExcaliburBuilder ScanAssemblies(this IExcaliburBuilder builder, params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assemblies);

		_ = builder.Services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
		_ = builder.Services.AddDispatchHandlers(assemblies);
		return builder;
	}
}
