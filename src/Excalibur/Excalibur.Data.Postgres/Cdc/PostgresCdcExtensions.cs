// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Postgres.Cdc;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres CDC services.
/// </summary>
public static class PostgresCdcExtensions
{
	/// <summary>
	/// Adds Postgres CDC processor to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the CDC options.</param>
	/// <param name="configureStateStoreOptions">Optional action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Requires Postgres server configuration:
	/// <list type="bullet">
	/// <item><description>wal_level = logical</description></item>
	/// <item><description>A publication for the tables to capture</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Example:
	/// <code>
	/// services.AddPostgresCdc(options =>
	/// {
	///     options.ConnectionString = "Host=localhost;Database=mydb;Username=repl;Password=secret";
	///     options.PublicationName = "my_publication";
	///     options.ReplicationSlotName = "my_slot";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresCdc(
		this IServiceCollection services,
		Action<PostgresCdcOptions> configure,
		Action<PostgresCdcStateStoreOptions>? configureStateStoreOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCdcStateStoreOptions(services, configureStateStoreOptions);

		// Register state store
		services.TryAddSingleton<IPostgresCdcStateStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>().Value;
			var stateStoreOptions = sp.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
			return new PostgresCdcStateStore(options.ConnectionString, stateStoreOptions);
		});

		// Register processor
		services.TryAddSingleton<IPostgresCdcProcessor, PostgresCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Postgres CDC processor with a custom state store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the CDC options.</param>
	/// <param name="stateStoreFactory">Factory to create the state store instance.</param>
	/// <param name="configureStateStoreOptions">Optional action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresCdc(
		this IServiceCollection services,
		Action<PostgresCdcOptions> configure,
		Func<IServiceProvider, IPostgresCdcStateStore> stateStoreFactory,
		Action<PostgresCdcStateStoreOptions>? configureStateStoreOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentNullException.ThrowIfNull(stateStoreFactory);

		_ = services.AddOptions<PostgresCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCdcStateStoreOptions(services, configureStateStoreOptions);

		// Register custom state store
		services.TryAddSingleton(stateStoreFactory);

		// Register processor
		services.TryAddSingleton<IPostgresCdcProcessor, PostgresCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Postgres CDC processor with in-memory state (for testing or single-instance deployments).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the CDC options.</param>
	/// <param name="configureStateStoreOptions">Optional action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The in-memory state store does not persist positions across restarts.
	/// Use this only for testing or single-instance deployments where position
	/// persistence is not required.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresCdcWithInMemoryState(
		this IServiceCollection services,
		Action<PostgresCdcOptions> configure,
		Action<PostgresCdcStateStoreOptions>? configureStateStoreOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCdcStateStoreOptions(services, configureStateStoreOptions);

		// Register in-memory state store
		services.TryAddSingleton<IPostgresCdcStateStore, InMemoryPostgresCdcStateStore>();

		// Register processor
		services.TryAddSingleton<IPostgresCdcProcessor, PostgresCdcProcessor>();

		return services;
	}

	private static void RegisterCdcStateStoreOptions(
		IServiceCollection services,
		Action<PostgresCdcStateStoreOptions>? configureStateStoreOptions)
	{
		var builder = services.AddOptions<PostgresCdcStateStoreOptions>();
		if (configureStateStoreOptions is not null)
		{
			_ = builder.Configure(configureStateStoreOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PostgresCdcStateStoreOptions>, PostgresCdcStateStoreOptionsValidator>());
	}
}
