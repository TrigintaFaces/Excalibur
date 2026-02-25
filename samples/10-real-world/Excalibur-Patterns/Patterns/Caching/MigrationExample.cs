// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace examples.Excalibur.Patterns.Caching;

// Sample DTOs and interfaces for the example

/// <summary>
///     Example demonstrating migration from old custom caching to new Microsoft.Extensions.Caching.Hybrid.
/// </summary>
public static class MigrationExample
{
	/// <summary>
	///     Example 1: Simple migration maintaining backward compatibility.
	/// </summary>
	public static void ConfigureSimpleMigration(IServiceCollection services, IConfiguration configuration) =>
		// BEFORE: Old custom caching implementation services.AddSingleton<ICacheInvalidationService, CustomCacheInvalidationService>(); services.Configure<CacheOptions>(configuration.GetSection("Cache"));
		// AFTER: New hybrid caching with backward compatibility
		services.MigrateToHybridCaching(removeOldServices: true);

	// The ICacheInvalidationService interface is still available through HybridCacheAdapter, so existing code continues to work
	/// <summary>
	///     Example 2: Migration with Redis L2 cache for distributed scenarios.
	/// </summary>
	public static void ConfigureDistributedMigration(IServiceCollection services, IConfiguration configuration)
	{
		// BEFORE: Custom distributed caching with manual stampede prevention services.AddDistributedMemoryCache();
		// services.AddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>(); services.AddSingleton<ICacheStampedeProtector,
		// CustomStampedeProtector>(); services.AddSingleton<ICacheInvalidationService, DistributedCacheInvalidationService>();

		// AFTER: Hybrid caching with built-in stampede protection and L1/L2 layers
		var redisConnection = configuration.GetConnectionString("Redis")
								?? "localhost:6379";

		services.AddHybridCachingWithRedis(redisConnection, options =>
		{
			// Built-in stampede protection - no need for custom implementation
			options.DefaultEntryOptions = new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(15),
				LocalCacheExpiration = TimeSpan.FromMinutes(5),
				Flags = HybridCacheEntryFlags.DisableDistributedCache // Can disable L2 per-entry if needed
			};
		});
	}

	/// <summary>
	///     Example 3: Advanced migration with custom configuration for high-throughput scenarios.
	/// </summary>
	public static void ConfigureHighThroughputMigration(IServiceCollection services, IConfiguration configuration)
	{
		// BEFORE: Complex custom caching with multiple layers and policies services.AddMemoryCache();
		// services.AddStackExchangeRedisCache(options => { /* config */ }); services.AddSingleton<IResultCachePolicy,
		// CustomResultCachePolicy>(); services.AddSingleton<ICacheKeyBuilder, DefaultCacheKeyBuilder>();
		// services.AddSingleton<ICacheInvalidationService, HybridCacheInvalidationService>();

		// AFTER: Simplified with hybrid caching
		var redisConnection = configuration.GetConnectionString("Redis")
								?? "localhost:6379";

		services.AddHighThroughputHybridCaching(redisConnection);

		// Can still use custom policies if needed
		services.AddSingleton<IResultCachePolicy, CustomResultCachePolicy>();
		services.AddSingleton<ICacheKeyBuilder, DefaultCacheKeyBuilder>();
	}

	/// <summary>
	///     Example 4: Gradual migration approach - run both systems in parallel.
	/// </summary>
	public static void ConfigureGradualMigration(IServiceCollection services, IConfiguration configuration)
	{
		// Keep old system running
		services.AddSingleton<IOldCacheService, OldCacheService>();

		// Add new hybrid caching alongside
		services.AddHybridCaching(configuration);

		// Use feature flag to switch between implementations
		services.AddSingleton<ICacheInvalidationService>(provider =>
		{
			var config = provider.GetRequiredService<IConfiguration>();
			var useNewCache = config.GetValue<bool>("FeatureFlags:UseHybridCache");

			if (useNewCache)
			{
				return provider.GetRequiredService<HybridCacheAdapter>();
			}

			// Fall back to old implementation
			return provider.GetRequiredService<IOldCacheService>();
		});
	}
}
