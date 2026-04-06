// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Data.IdentityMap;
using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring identity map store services.
/// </summary>
public static class IdentityMapServiceCollectionExtensions
{
	/// <summary>
	/// Adds identity map store services using a fluent builder configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The builder configuration action.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString(connectionString)
	///            .SchemaName("dbo")
	///            .TableName("IdentityMap");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddIdentityMap(
		this IServiceCollection services,
		Action<IIdentityMapBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterCoreServices(services);

		var builder = new IdentityMapBuilder(services);
		configure(builder);

		WrapWithDecorators(services);

		return services;
	}

	/// <summary>
	/// Adds identity map store services with the in-memory provider for testing and development.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddInMemoryIdentityMap(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		RegisterCoreServices(services);
		services.TryAddSingleton<IIdentityMapStore, InMemoryIdentityMapStore>();

		WrapWithDecorators(services);

		return services;
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		_ = services.AddOptions<IdentityMapOptions>()
			.ValidateOnStart();

		_ = services.AddOptions<IdentityMapCacheOptions>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IConfigureOptions<IdentityMapOptions>, DefaultIdentityMapOptionsSetup>());
	}

	private static void WrapWithDecorators(IServiceCollection services)
	{
		// Find the existing IIdentityMapStore registration and replace it with a factory
		// that conditionally wraps with telemetry and caching decorators.
		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(IIdentityMapStore));

		if (descriptor is null)
		{
			return;
		}

		_ = services.Remove(descriptor);

		services.Add(new ServiceDescriptor(typeof(IIdentityMapStore), sp =>
		{
			// Resolve the inner store from the original registration
			IIdentityMapStore inner;

			if (descriptor.ImplementationType is not null)
			{
				inner = (IIdentityMapStore)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
			}
			else if (descriptor.ImplementationFactory is not null)
			{
				inner = (IIdentityMapStore)descriptor.ImplementationFactory(sp);
			}
			else if (descriptor.ImplementationInstance is not null)
			{
				inner = (IIdentityMapStore)descriptor.ImplementationInstance;
			}
			else
			{
				throw new InvalidOperationException("IIdentityMapStore registration has no implementation.");
			}

			// Telemetry decorator: wraps the inner store when EnableTelemetry is true.
			var mapOptions = sp.GetRequiredService<IOptions<IdentityMapOptions>>().Value;

			if (mapOptions.EnableTelemetry)
			{
				var meterFactory = sp.GetService<IMeterFactory>();
				inner = new TelemetryIdentityMapStoreDecorator(inner, meterFactory);
			}

			// Caching decorator: wraps when IDistributedCache is available.
			var cache = sp.GetService<IDistributedCache>();

			if (cache is not null)
			{
				var cacheOptions = sp.GetRequiredService<IOptions<IdentityMapCacheOptions>>().Value;
				inner = new CachingIdentityMapStoreDecorator(inner, cache, cacheOptions);
			}

			return inner;
		}, descriptor.Lifetime));
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:AvoidUninstantiatedInternalClasses",
		Justification = "Instantiated by the options infrastructure.")]
	private sealed class DefaultIdentityMapOptionsSetup : IConfigureOptions<IdentityMapOptions>
	{
		public void Configure(IdentityMapOptions options)
		{
			// Defaults are set in IdentityMapOptions constructor
		}
	}
}
