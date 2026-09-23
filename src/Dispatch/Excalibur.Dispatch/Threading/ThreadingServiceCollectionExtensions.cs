// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;


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
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ThreadingOptions>, ThreadingOptionsValidator>());

		_ = services.ConfigureOptions(configure, static _ => { });
		_ = services.AddOptions<ThreadingOptions>()
			.ValidateOnStart();
		services.TryAddSingleton<IKeyedLock, KeyedLock>();
		services.TryAddSingleton<BackgroundExecutionMiddleware>();

		// Union in as IDispatchMiddleware -- without this the middleware was only reachable via
		// UseBackgroundExecution()'s explicit UseMiddleware<T>() call, so the bare Add form registered
		// the type but never ran it.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, BackgroundExecutionMiddleware>());

		// Background execution's shutdown drain and failure policy. The drain is registered here rather than at
		// the call site so it covers every route into the runner -- including a consumer calling
		// BackgroundTaskRunner.RunDetachedInBackground directly, which is public and shipped.
		_ = services.AddBackgroundExecutionServices();

		return services;
	}

	/// <summary>
	/// Registers threading services including keyed locks and background execution middleware
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="ThreadingOptions"/>. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddDispatchThreading(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ThreadingOptions>, ThreadingOptionsValidator>());

		_ = services.AddOptions<ThreadingOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddSingleton<IKeyedLock, KeyedLock>();
		services.TryAddSingleton<BackgroundExecutionMiddleware>();

		// Union in as IDispatchMiddleware -- without this the middleware was only reachable via
		// UseBackgroundExecution()'s explicit UseMiddleware<T>() call, so the bare Add form registered
		// the type but never ran it.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, BackgroundExecutionMiddleware>());

		// Background execution's shutdown drain and failure policy. The drain is registered here rather than at
		// the call site so it covers every route into the runner -- including a consumer calling
		// BackgroundTaskRunner.RunDetachedInBackground directly, which is public and shipped.
		_ = services.AddBackgroundExecutionServices();

		return services;
	}

	/// <summary>
	/// Registers what background execution needs besides its middleware: the hosted service that awaits
	/// in-flight background work when the host stops gracefully, and the validated
	/// <see cref="BackgroundExecutionOptions"/> that set the failure policy.
	/// </summary>
	/// <remarks>
	/// Shared by every entry point that enables background execution, so none of them can enable the work
	/// without also enabling its drain and its startup validation. Idempotent: each piece is registered at most
	/// once however many entry points a consumer calls.
	/// </remarks>
	/// <param name="services"> The service collection to add the services to. </param>
	/// <returns> The service collection for method chaining. </returns>
	internal static IServiceCollection AddBackgroundExecutionServices(this IServiceCollection services)
	{
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BackgroundTaskDrainService>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<BackgroundExecutionOptions>, BackgroundExecutionOptionsValidator>());
		_ = services.AddOptions<BackgroundExecutionOptions>().ValidateOnStart();
		return services;
	}
}
