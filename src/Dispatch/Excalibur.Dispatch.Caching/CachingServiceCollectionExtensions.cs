// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Resilience;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Extensions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
	/// Names the trimming requirement that registering result caching places on the composing application.
	/// </summary>
	internal const string CachingTrimmingReason =
		"Result caching reconstructs a cached value from the type name stored with the entry, which requires types "
		+ "that trimming may remove. An entry whose type can no longer be resolved is discarded and the handler runs again.";

	/// <summary>
	/// Names the runtime-code-generation requirement that registering result caching places on the composing application.
	/// </summary>
	internal const string CachingDynamicCodeReason =
		"Result caching deserializes a cached value by its runtime type, which requires runtime code generation. Under "
		+ "ahead-of-time compilation a serialized entry cannot be reconstructed and the handler runs again.";

	/// <summary>
	/// Names the trimming requirement that binding cache options from configuration adds on top of <see cref="CachingTrimmingReason"/>.
	/// </summary>
	private const string ConfigurationBindingTrimmingReason =
		"Binds the cache options from configuration by reflecting over the options type, which requires properties that "
		+ "trimming may remove. Use the overload that takes a configuration delegate for a trim-compatible composition. ";

	/// <summary>
	/// Names the runtime-code-generation requirement that binding cache options from configuration adds on top of
	/// <see cref="CachingDynamicCodeReason"/>.
	/// </summary>
	private const string ConfigurationBindingDynamicCodeReason =
		"Binds the cache options from configuration by reflecting over the options type, which requires runtime code "
		+ "generation. Use the overload that takes a configuration delegate for an ahead-of-time compatible composition. ";

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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(ConfigurationBindingTrimmingReason + CachingTrimmingReason)]
	[RequiresDynamicCode(ConfigurationBindingDynamicCodeReason + CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(ConfigurationBindingTrimmingReason + CachingTrimmingReason)]
	[RequiresDynamicCode(ConfigurationBindingDynamicCodeReason + CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(ConfigurationBindingTrimmingReason + CachingTrimmingReason)]
	[RequiresDynamicCode(ConfigurationBindingDynamicCodeReason + CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(ConfigurationBindingTrimmingReason + CachingTrimmingReason)]
	[RequiresDynamicCode(ConfigurationBindingDynamicCodeReason + CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(CachingTrimmingReason)]
	[RequiresDynamicCode(CachingDynamicCodeReason)]
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
	[RequiresUnreferencedCode(ConfigurationBindingTrimmingReason + CachingTrimmingReason)]
	[RequiresDynamicCode(ConfigurationBindingDynamicCodeReason + CachingDynamicCodeReason)]
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
	/// Marks a service collection whose distributed cache registration has already been bounded, so
	/// repeated caching registrations do not stack decorators.
	/// </summary>
	private sealed class DistributedCacheLatencyBoundMarker;

	/// <summary>
	/// Replaces the registered <see cref="IDistributedCache"/> with one whose asynchronous calls are bounded
	/// by <see cref="CacheBehaviorOptions.CacheTimeout"/>.
	/// </summary>
	/// <param name="services">The service collection to modify.</param>
	/// <remarks>
	/// <para>
	/// The bound lives here, on the backend call, rather than around the cache lookup-or-create operation.
	/// That operation runs the handler inside it and is shared between concurrent callers of one key, so a
	/// deadline around it bounds the handler and is abandoned per-caller, which defeats the single-flight
	/// behaviour that makes caching worth having. Bounding the backend call keeps the deadline inside the
	/// shared operation, where one timeout serves every waiting caller.
	/// </para>
	/// <para>
	/// This decorates whatever backend is registered at the time caching is added, which for every entry
	/// point in this class is the backend that entry point just registered. A consumer that registers a
	/// distributed cache <em>after</em> adding caching is simply not bounded by this option and relies on
	/// its cache client's own timeouts, as it would have anyway. Nothing here can add waiting: the
	/// decorator only ever shortens a call.
	/// </para>
	/// </remarks>
	private static void BoundDistributedCacheLatency(IServiceCollection services)
	{
		if (services.Any(static d => d.ServiceType == typeof(DistributedCacheLatencyBoundMarker)))
		{
			return;
		}

		var index = -1;
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var candidate = services[i];
			if (candidate.ServiceType == typeof(IDistributedCache) && !candidate.IsKeyedService)
			{
				index = i;
				break;
			}
		}

		if (index < 0)
		{
			// Memory-only composition: there is no distributed backend whose latency could be bounded.
			return;
		}

		var original = services[index];

		// An in-memory "distributed" cache cannot stall on I/O, so bounding it would buy nothing and cost a
		// linked token source per call.
		if (original.GetImplementationType() == typeof(MemoryDistributedCache))
		{
			return;
		}

		services[index] = ServiceDescriptor.Describe(
			typeof(IDistributedCache),
			sp =>
			{
				var inner = ResolveOriginalDistributedCache(original, sp);
				var options = sp.GetRequiredService<IOptions<CacheOptions>>();

				// Telemetry is resolved optionally: bounding backend latency is a correctness property, and
				// decorating IDistributedCache must not make its resolution depend on a consumer having
				// registered metrics or logging.
				var meterFactory = sp.GetService<IMeterFactory>();
				var logger = sp.GetService<ILogger<TimeoutDistributedCache>>()
					?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TimeoutDistributedCache>.Instance;

				// The breaker MUST be handed to the decorator, not merely exist in the container. Bounding a
				// backend call converts a slow backend into an ordinary cache miss, which is invisible to
				// everything above this point -- so if the decorator cannot report the timeout, nothing can,
				// and the breaker stays closed forever against a backend that is failing every request.
				var circuitBreaker = sp.GetService<ICircuitBreakerPolicy>();

				return inner is IBufferDistributedCache buffered
					? new BufferTimeoutDistributedCache(buffered, options, meterFactory, logger, circuitBreaker)
					: new TimeoutDistributedCache(inner, options, meterFactory, logger, circuitBreaker);
			},
			original.Lifetime);

		services.Add(ServiceDescriptor.Singleton(new DistributedCacheLatencyBoundMarker()));
	}

	/// <summary>
	/// Materializes the distributed cache described by the registration that was decorated.
	/// </summary>
	/// <param name="original">The registration that was replaced by the bounded one.</param>
	/// <param name="services">The service provider resolving the registration.</param>
	/// <returns>The undecorated distributed cache.</returns>
	/// <remarks>
	/// The keyed-safe accessors are used deliberately. ServiceDescriptor.ImplementationType,
	/// .ImplementationInstance and .ImplementationFactory THROW on a keyed descriptor, so reading them
	/// directly would turn a consumer registering IDistributedCache as a keyed service into an exception at
	/// container build. A boundary guard enforces this repo-wide.
	/// </remarks>
	private static IDistributedCache ResolveOriginalDistributedCache(ServiceDescriptor original, IServiceProvider services)
	{
		if (original.GetImplementationInstance() is IDistributedCache instance)
		{
			return instance;
		}

		var factory = original.GetImplementationFactory();
		if (factory is not null)
		{
			return (IDistributedCache)factory(services);
		}

		return (IDistributedCache)ActivatorUtilities.CreateInstance(services, original.GetImplementationType()!);
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

		// Bound the distributed backend so a slow L2 degrades to a miss rather than stalling the request.
		BoundDistributedCacheLatency(services);

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
					// Tell the consumer what they are getting. This tracker maintains its tag-to-keys set with a
					// read-modify-write, because IDistributedCache exposes no atomic set-add and no compare-and-swap
					// to build one on. Two instances registering different keys under one tag concurrently can lose
					// the earlier write, and a key dropped from the set is never invalidated when its tag is, so that
					// entry serves stale data until its own expiry. Redis does not have this problem and is selected
					// above when present. Saying so at startup is the difference between a known limitation and a
					// silent one.
					sp.GetService<ILoggerFactory>()
						?.CreateLogger("Excalibur.Dispatch.Caching")
						?.LogWarning(
							"Cache tag invalidation is running on a best-effort tracker over {CacheType}. Concurrent "
							+ "registrations under one tag can drop a key, and a dropped key is not invalidated with its "
							+ "tag, so that entry serves stale data until it expires. Register a Redis connection for "
							+ "atomic tag tracking, or keep entry lifetimes short enough that staleness is acceptable.",
							distributedCache.GetType().Name);

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

		// DefaultCacheKeyBuilder takes the concrete serializer, so caching composed on its own must seat it
		// rather than rely on the consumer also having called AddDispatchPipeline/AddDispatchSerializer.
		services.TryAddSingleton<Excalibur.Dispatch.Serialization.DispatchJsonSerializer>();

		// Register cache services
		services.TryAddSingleton<ICacheInvalidationService, HybridCacheInvalidationService>();

		// Note: Projection caching services moved to Excalibur.Caching.Projections
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
		[UnconditionalSuppressMessage("AOT", "IL2046:RequiresUnreferencedCode mismatch",
			Justification = "The wrapped middleware reflects; IDispatchMiddleware does not declare that, because a "
			+ "consumer-authored middleware need not reflect. The requirement reaches the consumer at the caching "
			+ "registration methods instead, which this type is internal to.")]
		[UnconditionalSuppressMessage("AOT", "IL3051:RequiresDynamicCode mismatch",
			Justification = "The wrapped middleware requires runtime code generation; IDispatchMiddleware does not "
			+ "declare that, because a consumer-authored middleware need not. The requirement reaches the consumer at "
			+ "the caching registration methods instead, which this type is internal to.")]
		[RequiresUnreferencedCode(CachingTrimmingReason)]
		[RequiresDynamicCode(CachingDynamicCodeReason)]
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
