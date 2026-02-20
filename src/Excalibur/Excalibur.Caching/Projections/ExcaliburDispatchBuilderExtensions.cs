// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Caching.Projections;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Extension methods for configuring projection caching on an <see cref="IDispatchBuilder"/>.
/// </summary>
/// <remarks>
/// These extensions enable registration of projection tag resolvers for CQRS cache invalidation.
/// Use these methods after configuring base caching with <c>AddDispatchCaching()</c>.
/// </remarks>
public static class ExcaliburDispatchBuilderExtensions
{
	/// <summary>
	/// Registers custom projection tag resolver types.
	/// </summary>
	/// <param name="builder">The <see cref="IDispatchBuilder"/> to configure.</param>
	/// <param name="resolverTypes">Resolver types implementing <see cref="IProjectionTagResolver{T}"/>.</param>
	/// <returns>The configured <see cref="IDispatchBuilder"/>.</returns>
	/// <example>
	/// <code>
	/// builder.WithProjectionResolvers(typeof(OrderUpdatedTagResolver), typeof(CustomerUpdatedTagResolver));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetInterfaces may break with trimming",
		Justification = "IProjectionTagResolver<T> interface discovery is expected to work with explicitly provided types")]
	[UnconditionalSuppressMessage("Trimming", "IL2072:GetGenericTypeDefinition may break with trimming",
		Justification = "Generic type definition comparison is safe for explicitly provided resolver types")]
	public static IDispatchBuilder WithProjectionResolvers(this IDispatchBuilder builder, params Type[] resolverTypes)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(resolverTypes);

		foreach (var type in resolverTypes)
		{
			var projectionTagResolver = type.GetInterfaces()
				.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProjectionTagResolver<>));

			if (projectionTagResolver is not null)
			{
				_ = builder.Services.AddSingleton(projectionTagResolver, type);
			}
		}

		return builder;
	}

	/// <summary>
	/// Registers projection tag resolvers discovered in the provided assembly.
	/// </summary>
	/// <param name="builder">The <see cref="IDispatchBuilder"/> to configure.</param>
	/// <param name="assembly">Assembly to scan for resolvers.</param>
	/// <returns>The configured <see cref="IDispatchBuilder"/>.</returns>
	/// <example>
	/// <code>
	/// builder.WithProjectionResolversFromAssembly(typeof(OrderUpdatedTagResolver).Assembly);
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "This method performs type discovery for dependency injection registration. The types implementing IProjectionTagResolver<T> are expected to be explicitly referenced by the consuming application.")]
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover projection tag resolvers which may reference types not preserved during trimming.")]
	public static IDispatchBuilder WithProjectionResolversFromAssembly(this IDispatchBuilder builder, Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		var types = assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetInterfaces().Any(static i =>
				i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProjectionTagResolver<>)));

		foreach (var type in types)
		{
			foreach (var projectionTagResolver in type.GetInterfaces()
						 .Where(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProjectionTagResolver<>)))
			{
				_ = builder.Services.AddSingleton(projectionTagResolver, type);
			}
		}

		return builder;
	}

	/// <summary>
	/// Adds projection cache invalidation services to the dispatch builder.
	/// </summary>
	/// <param name="builder">The <see cref="IDispatchBuilder"/> to configure.</param>
	/// <returns>The configured <see cref="IDispatchBuilder"/>.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the <see cref="IProjectionCacheInvalidator"/> service which enables
	/// CQRS projection handlers to invalidate cached query results when data changes.
	/// </para>
	/// <para>
	/// <strong>Prerequisites:</strong> This method requires Excalibur.Dispatch.Caching to be configured first
	/// (via <c>AddDispatchCaching()</c>) as it depends on <c>ICacheInvalidationService</c>.
	/// </para>
	/// <example>
	/// <code>
	/// builder.AddDispatchCaching()  // Register ICacheInvalidationService first
	///        .AddProjectionCaching(); // Then add projection caching
	/// </code>
	/// </example>
	/// </remarks>
	public static IDispatchBuilder AddProjectionCaching(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburProjectionCaching();
		return builder;
	}
}
