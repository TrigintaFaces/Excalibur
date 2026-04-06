// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring threading services in the Excalibur framework. Provides keyed locking and background execution capabilities.
/// </summary>
public static class ThreadingServiceCollectionExtensions
{
	/// <summary>
	/// Registers threading services including keyed locks and background execution middleware.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configure"> Optional configuration action for threading options. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddDispatchThreading(this IServiceCollection services, Action<ThreadingOptions>? configure = null)
	{
		_ = services.ConfigureOptions(configure, static _ => { });
		_ = services.AddSingleton<IKeyedLock, KeyedLock>();
		services.TryAddSingleton<BackgroundExecutionMiddleware>();

		return services;
	}

	/// <summary>
	/// Registers threading services including keyed locks and background execution middleware
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="ThreadingOptions"/>. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchThreading(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ThreadingOptions>().Bind(configuration);
		_ = services.AddSingleton<IKeyedLock, KeyedLock>();
		services.TryAddSingleton<BackgroundExecutionMiddleware>();

		return services;
	}
}
