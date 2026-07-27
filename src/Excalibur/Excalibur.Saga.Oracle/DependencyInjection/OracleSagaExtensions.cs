// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Oracle saga stores.
/// </summary>
public static class OracleSagaExtensions
{
	/// <summary>
	/// Adds Oracle saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure saga store options (connection string, schema, table names).</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSagaStore(
		this IServiceCollection services,
		Action<OracleSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterSagaStoreOptions(services, configure);

		// Fail-closed single-tenant default so the dep-gated AddTenantScopedStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// AddTenantScopedStore builds the store (injecting ITenantContext for the row-level tenant
		// predicate) AND emits the ITenantScopingCapability<ISagaStore> marker as one inseparable act, so a
		// store wired WITHOUT the ambient tenant can never carry a truthful-looking marker (S886 rw2ull).
		services.AddTenantScopedStore<ISagaStore, OracleSagaStore>((sp, tenantContext) =>
		{
			var options = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new OracleSagaStore(options.Value.ConnectionString!, options, logger, serializer, tenantContext);
		});
		services.AddKeyedSingleton<ISagaStore>("oracle", (sp, _) => sp.GetRequiredService<OracleSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("oracle"));

		// Admin/query surface (dashboard + operational tooling) -- the same store instance.
		services.TryAddSingleton<ISagaStoreAdmin>(static sp => sp.GetRequiredService<OracleSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds Oracle saga store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="OracleConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactoryProvider,
		Action<OracleSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaStoreOptions(services, configure);

		// Fail-closed single-tenant default so the dep-gated AddTenantScopedStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// AddTenantScopedStore builds the store (injecting ITenantContext) AND emits the
		// ITenantScopingCapability<ISagaStore> marker as one inseparable act (S886 rw2ull).
		services.AddTenantScopedStore<ISagaStore, OracleSagaStore>((sp, tenantContext) =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new OracleSagaStore(connectionFactory, options, logger, serializer, tenantContext);
		});
		services.AddKeyedSingleton<ISagaStore>("oracle", (sp, _) => sp.GetRequiredService<OracleSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("oracle"));

		services.TryAddSingleton<ISagaStoreAdmin>(static sp => sp.GetRequiredService<OracleSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds Oracle saga store using a typed <see cref="Excalibur.Data.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSagaStore<TDb>(
		this IServiceCollection services,
		Action<OracleSagaStoreOptions>? configure = null)
		where TDb : class, Excalibur.Data.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddOracleSagaStore(
			sp => () => (OracleConnection)sp.GetRequiredService<TDb>().Connection,
			configure);
	}

	/// <summary>
	/// Configures the dispatch builder to use Oracle saga store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOracleSagaStore(
		this IDispatchBuilder builder,
		Action<OracleSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOracleSagaStore(configure);

		return builder;
	}

	/// <summary>
	/// Adds Oracle saga timeout store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure saga timeout store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSagaTimeoutStore(
		this IServiceCollection services,
		Action<OracleSagaTimeoutStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterSagaTimeoutStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<OracleSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaTimeoutStore>>();
			return new OracleSagaTimeoutStore(options.Value.ConnectionString!, options, logger);
		});
		services.TryAddSingleton<ISagaTimeoutStore>(sp => sp.GetRequiredService<OracleSagaTimeoutStore>());

		return services;
	}

	/// <summary>
	/// Adds Oracle saga timeout store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="OracleConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Optional action to configure saga timeout store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleSagaTimeoutStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactoryProvider,
		Action<OracleSagaTimeoutStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaTimeoutStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<OracleSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaTimeoutStore>>();
			return new OracleSagaTimeoutStore(connectionFactory, options, logger);
		});
		services.TryAddSingleton<ISagaTimeoutStore>(sp => sp.GetRequiredService<OracleSagaTimeoutStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Oracle saga timeout store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure saga timeout store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOracleSagaTimeoutStore(
		this IDispatchBuilder builder,
		Action<OracleSagaTimeoutStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOracleSagaTimeoutStore(configure);

		return builder;
	}

	private static void RegisterSagaStoreOptions(
		IServiceCollection services,
		Action<OracleSagaStoreOptions>? configure)
	{
		_ = services.AddOptions<OracleSagaStoreOptions>()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OracleSagaStoreOptions>, OracleSagaStoreOptionsValidator>());
	}

	private static void RegisterSagaTimeoutStoreOptions(
		IServiceCollection services,
		Action<OracleSagaTimeoutStoreOptions>? configure)
	{
		_ = services.AddOptions<OracleSagaTimeoutStoreOptions>()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OracleSagaTimeoutStoreOptions>, OracleSagaTimeoutStoreOptionsValidator>());
	}
}
