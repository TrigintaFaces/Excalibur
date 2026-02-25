// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering performance measurement services.
/// </summary>
public static class PerformanceServiceCollectionExtensions
{
	/// <summary>
	/// Registers performance metrics collection services with the DI container.
	/// </summary>
	/// <param name="services"> The service collection to add Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddPerformanceMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register core performance services
		services.TryAddSingleton<IPerformanceMetricsCollector, PerformanceMetricsCollector>();
		services.TryAddSingleton<PerformanceMiddleware>();

		// Register middleware using TryAddEnumerable to allow multiple middleware registrations
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, PerformanceMiddleware>(static sp =>
				sp.GetRequiredService<PerformanceMiddleware>()));

		return services;
	}

	/// <summary>
	/// Registers performance metrics collection services with a custom implementation.
	/// </summary>
	/// <typeparam name="TImplementation"> The custom implementation type. </typeparam>
	/// <param name="services"> The service collection to add Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddPerformanceMetrics<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TImplementation>(this IServiceCollection services)
		where TImplementation : class, IPerformanceMetricsCollector
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register custom implementation
		services.TryAddSingleton<TImplementation>();
		services.TryAddSingleton<IPerformanceMetricsCollector>(static sp => sp.GetRequiredService<TImplementation>());
		services.TryAddSingleton<PerformanceMiddleware>();

		// Register middleware using TryAddEnumerable
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, PerformanceMiddleware>(static sp =>
				sp.GetRequiredService<PerformanceMiddleware>()));

		return services;
	}

	/// <summary>
	/// Registers performance metrics collection services with a factory delegate.
	/// </summary>
	/// <param name="services"> The service collection to add Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="implementationFactory"> Factory to create the implementation. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or factory is null. </exception>
	public static IServiceCollection AddPerformanceMetrics(
		this IServiceCollection services,
		Func<IServiceProvider, IPerformanceMetricsCollector> implementationFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(implementationFactory);

		// Register with factory
		services.TryAddSingleton(implementationFactory);
		services.TryAddSingleton<PerformanceMiddleware>();

		// Register middleware using TryAddEnumerable
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, PerformanceMiddleware>(static sp =>
				sp.GetRequiredService<PerformanceMiddleware>()));

		return services;
	}

	/// <summary>
	/// Registers the Dispatch cache manager for centralized cache coordination.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// PERF-22: This method registers <see cref="IDispatchCacheManager"/> which provides
	/// centralized control over cache freezing for production optimization.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDispatchCacheManager(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IDispatchCacheManager, DispatchCacheManager>();

		return services;
	}

	/// <summary>
	/// Registers the auto-freeze hosted service for automatic cache optimization on startup.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// PERF-22: This method registers a hosted service that automatically freezes all
	/// Dispatch caches when <see cref="Hosting.IHostApplicationLifetime.ApplicationStarted"/> fires.
	/// </para>
	/// <para>
	/// The hosted service respects <see cref="PerformanceOptions.AutoFreezeOnStart"/> configuration
	/// and automatically disables freezing when hot reload is detected.
	/// </para>
	/// <para>
	/// This method also registers the <see cref="IDispatchCacheManager"/> if not already registered.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDispatchAutoFreeze(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure cache manager is registered
		_ = services.AddDispatchCacheManager();

		// Register the hosted service for auto-freeze
		_ = services.AddHostedService<DispatchCacheOptimizationHostedService>();

		return services;
	}
}
