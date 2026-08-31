// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox;
using Excalibur.Dispatch.Configuration;
using Excalibur.Inbox.Oracle;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Oracle inbox store.
/// </summary>
public static class OracleInboxExtensions
{
	/// <summary>
	/// Adds the Oracle inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleInboxStore(
		this IServiceCollection services,
		Action<OracleInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<OracleInboxOptions>().Configure(configure).ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OracleInboxOptions>, OracleInboxOptionsValidator>());
		services.AddDefaultTenantContext();
		// The remaining constructor dependency, so this entry point can build its own store rather than only
		// working in hosts that happen to have composed logging already. TryAdd-based, so a host that
		// configures its own logging still wins.
		_ = services.AddLogging();
		// AddTenantAwareStore threads the resolved ITenantContext into construction (dep-gated: absent
		// context ⇒ resolution fails closed, on which the store filters every keyed read, since this
		// store's constructor declares one) AND emits the ITenantScopingCapability<IInboxStore> marker
		// inseparably.
		services.AddTenantAwareStore<IInboxStore, OracleInboxStore>();
		services.AddKeyedSingleton<IInboxStore>("oracle", (sp, _) => sp.GetRequiredService<OracleInboxStore>());
		services.AddInboxSchemaValidation();
		services.AddSingleton<IInboxSchemaValidator>(sp => sp.GetRequiredService<OracleInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("oracle"));

		return services;
	}

	/// <summary>
	/// Adds the Oracle inbox store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="OracleConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOracleInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactoryProvider,
		Action<OracleInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<OracleInboxOptions>().Configure(configure).ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OracleInboxOptions>, OracleInboxOptionsValidator>());
		services.AddDefaultTenantContext();
		// AddTenantAwareStore builds the store (injecting ITenantContext so this factory path applies the
		// tenant predicate rather than silently dropping it, since this store's constructor declares one)
		// AND emits the ITenantScopingCapability<IInboxStore> marker inseparably.
		services.AddTenantAwareStore<IInboxStore, OracleInboxStore>(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<OracleInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<OracleInboxStore>>();
			return new OracleInboxStore(connectionFactory, options, logger, sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IOptions<TenantContextOptions>>());
		});
		services.AddKeyedSingleton<IInboxStore>("oracle", (sp, _) => sp.GetRequiredService<OracleInboxStore>());
		services.AddInboxSchemaValidation();
		services.AddSingleton<IInboxSchemaValidator>(sp => sp.GetRequiredService<OracleInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("oracle"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use the Oracle inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOracleInboxStore(
		this IDispatchBuilder builder,
		Action<OracleInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOracleInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use the Oracle inbox store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="OracleConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOracleInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<OracleConnection>> connectionFactoryProvider,
		Action<OracleInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOracleInboxStore(connectionFactoryProvider, configure);

		return builder;
	}
}
