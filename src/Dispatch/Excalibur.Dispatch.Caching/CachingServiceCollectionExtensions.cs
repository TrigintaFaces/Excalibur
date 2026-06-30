// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Extensions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MsMemoryCacheOptions = Microsoft.Extensions.Caching.Memory.MemoryCacheOptions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring caching services in the Excalibur framework. Supports Microsoft.Extensions.Caching
/// for memory, distributed, and hybrid caching scenarios.
/// </summary>
public static class CachingServiceCollectionExtensions
{
	/// <summary>
	/// Static accumulator for cache policy registrations discovered during DI composition.
	/// Read by <see cref="CachePolicyRegistryPopulator"/> on first options resolution.
	/// </summary>
	internal static readonly ConcurrentBag<Action<CachePolicyRegistry, IServiceProvider>> CachePolicyPendingRegistrations = [];

	/// <summary>
	/// Registers a message-specific cache policy for AOT-safe resolution.
	/// </summary>
	/// <typeparam name="TMessage">The message type the policy applies to.</typeparam>
	/// <typeparam name="TPolicy">The cache policy implementation type.</typeparam>
	/// <param name="services">The service collection to register with.</param>
	/// <returns>The updated <see cref="IServiceCollection"/>.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the policy as a closed-generic DI service and accumulates a typed
	/// registration for the <see cref="CachePolicyRegistry"/>. In AOT mode, the registry is used
	/// to resolve policies without <see cref="Type.MakeGenericType"/>.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCachePolicy<TMessage, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
		this IServiceCollection services)
		where TMessage : class, IDispatchMessage
		where TPolicy : class, IResultCachePolicy<TMessage>
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register closed-generic policy in DI for JIT path
		services.TryAddSingleton<IResultCachePolicy<TMessage>, TPolicy>();

		// Accumulate typed registration for AOT-safe CachePolicyRegistry.
		// At DI composition time, TMessage and TPolicy are concrete types,
		// so the AOT compiler preserves the concrete ShouldCache instantiation.
		CachePolicyPendingRegistrations.Add(static (registry, sp) =>
		{
			registry.Register(typeof(TMessage), (serviceProvider, message, result) =>
			{
				var policy = serviceProvider.GetService<IResultCachePolicy<TMessage>>();
				return policy?.ShouldCache((TMessage)message, result) ?? true;
			});
		});

		// Ensure core services + registry + populator are registered
		RegisterCoreCachingServices(services);

		// Register populator (idempotent via TryAddEnumerable)
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IPostConfigureOptions<CacheOptions>, CachePolicyRegistryPopulator>());

		return services;
	}

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
			.ValidateOnStart();

		// Register core caching services (includes HybridCache registration)
		RegisterCoreCachingServices(services);

		return services;
	}

	/// <summary>
	/// Registers the caching middleware and related services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The configuration section to bind to <see cref="CacheOptions"/>. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchCaching(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CacheOptions>()
			.Bind(configuration)
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
		Action<MsMemoryCacheOptions>? configureMemory = null,
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
	/// Configures in-memory caching using IMemoryCache with options from <see cref="IConfiguration"/> sections.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="memoryCacheConfiguration"> Optional configuration section for memory cache options. </param>
	/// <param name="cachingConfiguration"> Optional configuration section for general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchMemoryCaching(
		this IServiceCollection services,
		IConfiguration? memoryCacheConfiguration,
		IConfiguration? cachingConfiguration)
	{
		if (cachingConfiguration is not null)
		{
			_ = services.AddOptions<CacheOptions>().Bind(cachingConfiguration).ValidateOnStart();
		}
		else
		{
			_ = services.ConfigureOptions<CacheOptions>(null, static defaults =>
			{
				defaults.Enabled = true;
				defaults.CacheMode = CacheMode.Memory;
			});
		}

		// Add memory cache with optional configuration
		if (memoryCacheConfiguration is not null)
		{
			_ = services.AddMemoryCache(o => memoryCacheConfiguration.Bind(o));
		}
		else
		{
			_ = services.AddMemoryCache();
		}

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
	/// Configures distributed caching using Redis with options from <see cref="IConfiguration"/> sections.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="redisConfiguration"> The configuration section for Redis cache options. </param>
	/// <param name="cachingConfiguration"> Optional configuration section for general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchRedisCaching(
		this IServiceCollection services,
		IConfiguration redisConfiguration,
		IConfiguration? cachingConfiguration = null)
	{
		ArgumentNullException.ThrowIfNull(redisConfiguration);

		if (cachingConfiguration is not null)
		{
			_ = services.AddOptions<CacheOptions>().Bind(cachingConfiguration).ValidateOnStart();
		}
		else
		{
			_ = services.ConfigureOptions<CacheOptions>(null, static defaults =>
			{
				defaults.Enabled = true;
				defaults.CacheMode = CacheMode.Distributed;
			});
		}

		// Add Redis distributed cache
		_ = services.AddStackExchangeRedisCache(o => redisConfiguration.Bind(o));

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
	/// Configures hybrid caching with options from <see cref="IConfiguration"/> sections.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="hybridConfiguration"> Optional configuration section for hybrid cache options. </param>
	/// <param name="redisConfiguration"> Optional configuration section for Redis as the distributed cache backend. </param>
	/// <param name="cachingConfiguration"> Optional configuration section for general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchHybridCaching(
		this IServiceCollection services,
		IConfiguration? hybridConfiguration,
		IConfiguration? redisConfiguration,
		IConfiguration? cachingConfiguration)
	{
		if (cachingConfiguration is not null)
		{
			_ = services.AddOptions<CacheOptions>().Bind(cachingConfiguration).ValidateOnStart();
		}
		else
		{
			_ = services.ConfigureOptions<CacheOptions>(null, static defaults =>
			{
				defaults.Enabled = true;
				defaults.CacheMode = CacheMode.Hybrid;
			});
		}

		// Add Redis as the distributed cache backend if configured
		if (redisConfiguration is not null)
		{
			_ = services.AddStackExchangeRedisCache(o => redisConfiguration.Bind(o));
		}

		// Add hybrid cache with optional configuration
		if (hybridConfiguration is not null)
		{
			_ = services.AddHybridCache(o => hybridConfiguration.Bind(o));
		}
		else
		{
			_ = services.AddHybridCache();
		}

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
	/// Adds a custom distributed cache implementation with options from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="TImplementation"> The type implementing IDistributedCache. </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="cachingConfiguration"> The configuration section for general cache options. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDispatchDistributedCaching<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services,
		IConfiguration cachingConfiguration)
		where TImplementation : class, IDistributedCache
	{
		ArgumentNullException.ThrowIfNull(cachingConfiguration);

		_ = services.AddOptions<CacheOptions>().Bind(cachingConfiguration).ValidateOnStart();

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

		// Register tag tracker: auto-selects DistributedCacheTagTracker for Distributed/Hybrid
		// modes with a real distributed cache, or InMemoryCacheTagTracker otherwise.
		services.TryAddSingleton<ICacheTagTracker>(sp =>
		{
			var opts = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
			if (opts.CacheMode is CacheMode.Distributed or CacheMode.Hybrid)
			{
				// Prefer the Redis-native tracker (atomic SADD/SMEMBERS/SREM — no lost-update race) whenever a
				// real Redis connection is registered. This is the correct multi-instance tracker; the generic
				// DistributedCacheTagTracker below is a best-effort fallback for non-Redis IDistributedCache
				// backends (e.g. SQL Server) whose abstraction lacks an atomic set primitive.
				var multiplexer = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
				if (multiplexer is not null)
				{
					return new RedisCacheTagTracker(
						multiplexer,
						sp.GetRequiredService<IOptions<CacheOptions>>());
				}

				var distributedCache = sp.GetService<IDistributedCache>();
				if (distributedCache is not null
					&& !string.Equals(distributedCache.GetType().Name, "MemoryDistributedCache", StringComparison.Ordinal))
				{
					return new DistributedCacheTagTracker(
						distributedCache,
						sp.GetRequiredService<IOptions<CacheOptions>>());
				}
			}

			return ActivatorUtilities.CreateInstance<InMemoryCacheTagTracker>(sp);
		});

		// Register middleware
		services.TryAddSingleton<CachingMiddleware>();
		services.TryAddSingleton<CacheInvalidationMiddleware>();

		// Register conditional wrapper middleware concrete types for pipeline resolution
		services.TryAddSingleton<CachingMiddlewareWrapper>();
		services.TryAddSingleton<CacheInvalidationMiddlewareWrapper>();

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

		// Register AOT-safe cache policy registry (populated via IPostConfigureOptions)
		services.TryAddSingleton<CachePolicyRegistry>();
	}

	/// <summary>
	/// Wrapper middleware that conditionally applies caching based on configuration.
	/// </summary>
	/// <param name="options">Cache configuration options.</param>
	/// <param name="cachingMiddleware">The underlying caching middleware.</param>
	[SuppressMessage("CodeQuality", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Class is instantiated by dependency injection container.")]
	internal sealed class CachingMiddlewareWrapper(IOptions<CacheOptions> options, CachingMiddleware cachingMiddleware) : IDispatchMiddleware
	{
		/// <inheritdoc />
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

		/// <inheritdoc />
		[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
			Justification = "CachingMiddleware is only invoked when caching is enabled and AOT limitations are acceptable")]
		[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
			Justification = "CachingMiddleware is only invoked when caching is enabled and AOT limitations are acceptable")]
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
	internal sealed class CacheInvalidationMiddlewareWrapper(
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

	/// <summary>
	/// Populates <see cref="CachePolicyRegistry"/> from cache policy actions accumulated during DI composition.
	/// Runs once on first <see cref="CacheOptions"/> resolution via <see cref="IPostConfigureOptions{TOptions}"/>.
	/// </summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via DI through TryAddEnumerable ServiceDescriptor")]
	internal sealed class CachePolicyRegistryPopulator(
		CachePolicyRegistry registry,
		IServiceProvider serviceProvider) : IPostConfigureOptions<CacheOptions>
	{
		private volatile bool _populated;

		public void PostConfigure(string? name, CacheOptions options)
		{
			if (_populated)
			{
				return;
			}

			_populated = true;

			foreach (var registration in CachePolicyPendingRegistrations)
			{
				registration(registry, serviceProvider);
			}

			registry.Freeze();
		}
	}
}
