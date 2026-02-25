// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Application;
using Excalibur.Application.Requests;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur application services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ExcaliburApplicationServiceCollectionExtensions
{
	/// <summary>
	/// Adds all required application services for an Excalibur application, including activity context, validators, Mediator, and AutoMapper.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> The assemblies containing types to register. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	public static IServiceCollection AddExcaliburApplicationServices(this IServiceCollection services, params Assembly[] assemblies)
	{
		_ = services.AddDispatchPipeline();

		// Register activity context as a scoped service.
		services.TryAddScoped<IActivityContext, ActivityContext>();

		// Register compatibility adapter for legacy IMessagePublisher interface
		// R0.8: Type or member is obsolete
#pragma warning disable CS0618
		services.TryAddScoped<IMessagePublisher, MessagePublisherAdapter>();
#pragma warning restore CS0618 // Type or member is obsolete

		// Register validators from the provided assemblies, including internal types.
		_ = services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

		// Register dispatch handlers from the provided assemblies.
		_ = services.AddDispatchHandlers(assemblies);

		return services;
	}

	/// <summary>
	/// Registers all concrete implementations of <see cref="IActivity" /> from the specified assemblies as singletons.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> The assemblies containing activity types to register. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	[RequiresUnreferencedCode("Assembly scanning may require unreferenced types for reflection-based type discovery")]
	public static IServiceCollection AddActivities(this IServiceCollection services, params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(assemblies);

		foreach (var assembly in assemblies)
		{
			assembly
				.GetExportedTypes()
				.Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false } && typeof(IActivity).IsAssignableFrom(type))
				.ToList()
				.ForEach(a => services.AddSingleton(typeof(IActivity), a));
		}

		return services;
	}
}
