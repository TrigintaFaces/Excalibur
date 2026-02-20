// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring threading services in the Dispatch framework. Provides keyed locking and background execution capabilities.
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
		_ = services.AddSingleton<IDispatchMiddleware, BackgroundExecutionMiddleware>();

		return services;
	}
}
