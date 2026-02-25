// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Extension methods for configuring AOT-compatible handler invocation.
/// </summary>
public static class AotCompatibilityExtensions
{
	/// <summary>
	/// Configures the handler invoker to use AOT-compatible implementations when publishing for AOT.
	/// </summary>
	/// <remarks>
	/// When USE_SOURCE_GENERATION is defined (during AOT publish), this will use the HandlerInvokerAot. Otherwise, it will use the
	/// HandlerInvoker for better development experience.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"HandlerInvoker is only used when not compiling for AOT. USE_SOURCE_GENERATION or PUBLISH_AOT flags control this.")]
	public static IServiceCollection ConfigureHandlerInvoker(this IServiceCollection services)
	{
#if USE_SOURCE_GENERATION || PUBLISH_AOT
		// Use AOT-compatible invoker when publishing for AOT
		services.TryAddSingleton<IHandlerInvoker, HandlerInvokerAot>();
#else
		// Use compiled invoker for development (supports dynamic handler discovery)
		services.TryAddSingleton<IHandlerInvoker, HandlerInvoker>();
#endif
		return services;
	}

	/// <summary>
	/// Registers all handlers discovered by the source generator.
	/// </summary>
	/// <remarks>
	/// This method will use the precompiled handler registry when available, falling back to reflection-based discovery for development scenarios.
	/// </remarks>
	[RequiresUnreferencedCode("May use reflection for handler discovery in non-AOT scenarios")]
	public static IServiceCollection RegisterDiscoveredHandlers(
		this IServiceCollection services,
		params Assembly[] assemblies)
	{
		var registry = new HandlerRegistry();
		HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

		// Register all discovered handlers with DI
		foreach (var entry in registry.GetAll())
		{
			services.TryAddScoped(entry.HandlerType);
		}

		// Register the populated registry
		services.TryAddSingleton<IHandlerRegistry>(registry);

		return services;
	}

	/// <summary>
	/// Checks if the application is running in AOT mode.
	/// </summary>
	public static bool IsRunningAot()
	{
#if USE_SOURCE_GENERATION || PUBLISH_AOT
		return true;
#else
		// Runtime check for AOT
		return !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#endif
	}
}
