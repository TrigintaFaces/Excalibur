// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Application.Requests;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur application services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ExcaliburApplicationServiceCollectionExtensions
{
	// AddExcaliburApplicationServices(...) was deleted in S804 (bd-sdhocq A7) per ADR-325 §2.
	// Its internal helpers (AddValidatorsFromAssemblies + AddDispatchHandlers) are now reachable
	// via IExcaliburBuilder.ScanAssemblies(...) — see Excalibur.Hosting/Builders/ExcaliburBuilderContextExtensions.cs.

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
