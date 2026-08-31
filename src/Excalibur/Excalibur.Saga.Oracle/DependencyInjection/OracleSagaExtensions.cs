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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddOracleSagaStore(
		this IServiceCollection services,
		Action<OracleSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure core saga registration, the way AddExcaliburSaga ensures AddDispatch: this is a public
		// entry point, so a consumer who calls only this must still receive a resolvable ISagaStore. Core
		// owns the non-keyed contract alias rather than each provider re-registering it. Idempotent, so a
		// consumer who also calls AddSagas composes cleanly.
		_ = services.AddExcaliburSaga();

		RegisterSagaStoreOptions(services, configure);

		// Fail-closed single-tenant default so the dep-gated AddTenantAwareStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// AddTenantAwareStore builds the store (injecting ITenantContext for the row-level tenant
		// predicate, since this store's constructor declares one) AND emits the
		// ITenantScopingCapability<ISagaStore> marker as one inseparable act, so a store wired WITHOUT the
		// ambient tenant can never carry a truthful-looking marker.
		services.AddTenantAwareStore<ISagaStore, OracleSagaStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new OracleSagaStore(options.Value.ConnectionString!, options, logger, serializer, sp.GetRequiredService<ITenantContext>());
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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddOracleSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactoryProvider,
		Action<OracleSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		// Ensure core saga registration, the way AddExcaliburSaga ensures AddDispatch: this is a public
		// entry point, so a consumer who calls only this must still receive a resolvable ISagaStore. Core
		// owns the non-keyed contract alias rather than each provider re-registering it. Idempotent, so a
		// consumer who also calls AddSagas composes cleanly.
		_ = services.AddExcaliburSaga();

		RegisterSagaStoreOptions(services, configure);

		// Fail-closed single-tenant default so the dep-gated AddTenantAwareStore seam resolves
		// ITenantContext. AddMultiTenancy REPLACES this registration (never TryAdd), so an ambient
		// multi-tenant context still wins regardless of composition order.
		services.AddDefaultTenantContext();
		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISagaStore> marker as one inseparable act
		//.
		services.AddTenantAwareStore<ISagaStore, OracleSagaStore>(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<OracleSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new OracleSagaStore(connectionFactory, options, logger, serializer, sp.GetRequiredService<ITenantContext>());
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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddOracleSagaStore<TDb>(
		this IServiceCollection services,
		Action<OracleSagaStoreOptions>? configure = null)
		where TDb : class, Excalibur.Data.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure core saga registration, the way AddExcaliburSaga ensures AddDispatch: this is a public
		// entry point, so a consumer who calls only this must still receive a resolvable ISagaStore. Core
		// owns the non-keyed contract alias rather than each provider re-registering it. Idempotent, so a
		// consumer who also calls AddSagas composes cleanly.
		_ = services.AddExcaliburSaga();

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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
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

		// The timeout store takes a required ITenantContext and is registered here by type, so it must
		// resolve. This registers the single-tenant default only when no context exists yet, so a
		// multi-tenant host keeps its own.
		_ = services.AddDefaultTenantContext();
		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<OracleSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaTimeoutStore>>();
			var tenantContext = sp.GetRequiredService<ITenantContext>();
			return new OracleSagaTimeoutStore(options.Value.ConnectionString!, options, logger, tenantContext);
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

		// The timeout store takes a required ITenantContext and is registered here by type, so it must
		// resolve. This registers the single-tenant default only when no context exists yet, so a
		// multi-tenant host keeps its own.
		_ = services.AddDefaultTenantContext();
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<OracleSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OracleSagaTimeoutStore>>();
			var tenantContext = sp.GetRequiredService<ITenantContext>();
			return new OracleSagaTimeoutStore(connectionFactory, options, logger, tenantContext);
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
