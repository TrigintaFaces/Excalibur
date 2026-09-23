// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

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
	/// Selects the source-generated <see cref="HandlerInvokerAot"/> unless the application both supports
	/// dynamic code and has left the reflective invoker enabled, in which case <see cref="HandlerInvoker"/>
	/// is used. An application that trims without compiling ahead-of-time can disable the reflective
	/// invoker to have it removed rather than trimmed around.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"The reflective invoker is reached only when DispatchFeatureSwitches.UseReflectionInvoker is true. "
			+ "That property is a declared feature switch, so a trimmed application that disables it has this "
			+ "branch removed by the trimmer rather than analysed. An application that leaves it enabled has "
			+ "chosen reflective handler resolution and must root its handler types.")]
	public static IServiceCollection ConfigureHandlerInvoker(this IServiceCollection services)
	{
		if (DispatchFeatureSwitches.UseReflectionInvoker)
		{
			// Reflective invoker: resolves handlers and builds typed invokers at run time.
			services.TryAddSingleton<IHandlerInvoker, HandlerInvoker>();
		}
		else
		{
			// Source-generated invoker: no reflection, no expression compilation.
			services.TryAddSingleton<IHandlerInvoker, HandlerInvokerAot>();
		}

		return services;
	}

	/// <summary>
	/// Checks if the application is running in AOT mode.
	/// </summary>
	public static bool IsRunningAot()
		=> !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
}
