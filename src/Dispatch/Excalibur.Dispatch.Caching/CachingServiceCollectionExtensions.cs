// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Extensions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring caching services in the Dispatch messaging framework. Supports Microsoft.Extensions.Caching
/// for memory, distributed, and hybrid caching scenarios.
/// </summary>
public static class CachingServiceCollectionExtensions
{
	/// <summary>
	/// Registers the caching middleware and related services with default hybrid caching.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configure"> Optional callback to configure <see cref="CacheOptions" />. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchCaching(this IServiceCollection services, Action<CacheOptions>? configure = null)
	{
		_ = services.ConfigureOptions(configure, static defaults =>
		{
			defaults.Enabled = true;
			defaults.CacheMode = CacheMode.Hybrid;
		});

		_ = services.AddOptions<CacheOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core caching services (includes HybridCache registration)
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Configures in-memory caching using IMemoryCache. Best for single-server scenarios with fast, temporary caching needs.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configureMemory"> Optional callback to configure memory cache options. </param>
	/// <param name="configureCaching"> Optional callback to configure general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchMemoryCaching(
		this IServiceCollection services,
		Action<MemoryCacheOptions>? configureMemory = null,
		Action<CacheOptions>? configureCaching = null)
	{
		_ = services.ConfigureOptions(configureCaching, static defaults =>
		{
			defaults.Enabled = true;
			defaults.CacheMode = CacheMode.Memory;
		});

		// Add memory cache with optional configuration
		_ = configureMemory != null ? services.AddMemoryCache(configureMemory) : services.AddMemoryCache();

		// Register core caching services
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Configures distributed caching using Redis (StackExchange.Redis). Best for multi-server scenarios requiring shared cache state.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configureRedis"> Callback to configure Redis cache options. </param>
	/// <param name="configureCaching"> Optional callback to configure general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchRedisCaching(
		this IServiceCollection services,
		Action<RedisCacheOptions> configureRedis,
		Action<CacheOptions>? configureCaching = null)
	{
		ArgumentNullException.ThrowIfNull(configureRedis);

		_ = services.ConfigureOptions(configureCaching, static defaults =>
		{
			defaults.Enabled = true;
			defaults.CacheMode = CacheMode.Distributed;
		});

		// Add Redis distributed cache
		_ = services.AddStackExchangeRedisCache(configureRedis);

		// Register core caching services
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Configures hybrid caching combining memory and distributed caching. Provides fast local cache with distributed cache fallback and synchronization.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configureHybrid"> Optional callback to configure hybrid cache options. </param>
	/// <param name="configureRedis"> Optional callback to configure Redis as the distributed cache backend. </param>
	/// <param name="configureCaching"> Optional callback to configure general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchHybridCaching(
		this IServiceCollection services,
		Action<HybridCacheOptions>? configureHybrid = null,
		Action<RedisCacheOptions>? configureRedis = null,
		Action<CacheOptions>? configureCaching = null)
	{
		_ = services.ConfigureOptions(configureCaching, static defaults =>
		{
			defaults.Enabled = true;
			defaults.CacheMode = CacheMode.Hybrid;
		});

		// Add Redis as the distributed cache backend if configured
		if (configureRedis != null)
		{
			_ = services.AddStackExchangeRedisCache(configureRedis);
		}

		// Add hybrid cache with optional configuration
		_ = configureHybrid != null ? services.AddHybridCache(configureHybrid) : services.AddHybridCache();

		// Register core caching services
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Adds a custom distributed cache implementation.
	/// </summary>
	/// <typeparam name="TImplementation"> The type implementing IDistributedCache. </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configureCaching"> Optional callback to configure general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchDistributedCaching<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services,
		Action<CacheOptions>? configureCaching = null)
		where TImplementation : class, IDistributedCache
	{
		_ = services.ConfigureOptions(configureCaching, static defaults =>
		{
			defaults.Enabled = true;
			defaults.CacheMode = CacheMode.Distributed;
		});

		// Register the custom distributed cache
		services.TryAddSingleton<IDistributedCache, TImplementation>();

		// Register core caching services
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Registers core caching services including middleware and invalidation services.
	/// </summary>
	/// <param name="services">The service collection to register services with.</param>
	private static void RegisterCoreCachingServices(IServiceCollection services)
	{
		// Register cross-property validator for CacheOptions
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CacheOptions>, CacheOptionsValidator>());

		// HybridCache is required by CachingMiddleware regardless of CacheMode.
		// In Memory-only mode it acts as L1-only; in Distributed mode the DisableLocalCache flag is set.
		_ = services.AddHybridCache();

		// Register tag tracker for Memory and Distributed modes (tag-based invalidation).
		// Hybrid mode uses HybridCache's native tag support but having this registered is harmless.
		services.TryAddSingleton<ICacheTagTracker, InMemoryCacheTagTracker>();

		// Register middleware
		services.TryAddSingleton<CachingMiddleware>();
		services.TryAddSingleton<CacheInvalidationMiddleware>();

		// Register conditional wrapper middleware
		services.TryAddSingleton<CachingMiddlewareWrapper>();
		services.TryAddSingleton<CacheInvalidationMiddlewareWrapper>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, CachingMiddlewareWrapper>(static sp =>
				sp.GetRequiredService<CachingMiddlewareWrapper>()));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDispatchMiddleware, CacheInvalidationMiddlewareWrapper>(static sp =>
				sp.GetRequiredService<CacheInvalidationMiddlewareWrapper>()));

		// Register cache services
		services.TryAddSingleton<ICacheInvalidationService, HybridCacheInvalidationService>();

		// Note: Projection caching services moved to Excalibur.Caching.Projections (Sprint 330 T1.2, AD-330-3)
		// Use services.AddExcaliburProjectionCaching() after AddDispatchCaching() for projection invalidation

		// Note: CachedRouterService decoration should be done in Excalibur.Patterns where the implementation belongs (architectural
		// boundary separation)

		// Use default key builder unless overridden
		services.TryAddSingleton<ICacheKeyBuilder, DefaultCacheKeyBuilder>();

		// Register cache result policy with a default policy that always caches
		services.TryAddSingleton<IResultCachePolicy>(new DefaultResultCachePolicy(static (_, _) => true));
	}

	/// <summary>
	/// Wrapper middleware that conditionally applies caching based on configuration.
	/// </summary>
	/// <param name="options">Cache configuration options.</param>
	/// <param name="cachingMiddleware">The underlying caching middleware.</param>
	[SuppressMessage("CodeQuality", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Class is instantiated by dependency injection container.")]
	private sealed class CachingMiddlewareWrapper(IOptions<CacheOptions> options, CachingMiddleware cachingMiddleware) : IDispatchMiddleware
	{
		/// <inheritdoc />
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

		/// <inheritdoc />
		[UnconditionalSuppressMessage("AOT", "IL3050:Using RequiresDynamicCode member in AOT", Justification = "CachingMiddleware is only invoked when caching is enabled and AOT limitations are acceptable")]
		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			options.Value.Enabled
				? cachingMiddleware.InvokeAsync(message, context, nextDelegate, cancellationToken)
				: nextDelegate(message, context, cancellationToken);
	}

	/// <summary>
	/// Wrapper middleware that conditionally applies cache invalidation based on configuration.
	/// </summary>
	/// <param name="options">Cache configuration options.</param>
	/// <param name="invalidationMiddleware">The underlying cache invalidation middleware.</param>
	[SuppressMessage("CodeQuality", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Class is instantiated by dependency injection container.")]
	private sealed class CacheInvalidationMiddlewareWrapper(
		IOptions<CacheOptions> options,
		CacheInvalidationMiddleware invalidationMiddleware) : IDispatchMiddleware
	{
		/// <inheritdoc />
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

		/// <inheritdoc />
		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			options.Value.Enabled
				? invalidationMiddleware.InvokeAsync(message, context, nextDelegate, cancellationToken)
				: nextDelegate(message, context, cancellationToken);
	}
}
