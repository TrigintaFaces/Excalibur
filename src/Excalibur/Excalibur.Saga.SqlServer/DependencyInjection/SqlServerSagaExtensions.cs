// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server saga stores.
/// </summary>
public static class SqlServerSagaExtensions
{
	/// <summary>
	/// Adds SQL Server saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure saga store options (connection string, schema, table names).</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerSagaStore(
		this IServiceCollection services,
		Action<SqlServerSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterSagaStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new SqlServerSagaStore(options.Value.ConnectionString!, options, logger, serializer);
		});
		services.AddKeyedSingleton<ISagaStore>("sqlserver", (sp, _) => sp.GetRequiredService<SqlServerSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("sqlserver"));

		return services;
	}

	/// <summary>
	/// Adds SQL Server saga store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddSqlServerSagaStore(sp => () => (SqlConnection)sp.GetRequiredService&lt;IDomainDb&gt;().Connection);
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaStore>>();
			var serializer = sp.GetRequiredService<DispatchJsonSerializer>();
			return new SqlServerSagaStore(connectionFactory, options, logger, serializer);
		});
		services.AddKeyedSingleton<ISagaStore>("sqlserver", (sp, _) => sp.GetRequiredService<SqlServerSagaStore>());
		services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("sqlserver"));

		return services;
	}

	/// <summary>
	/// Adds SQL Server saga store using a typed <see cref="Excalibur.Data.Abstractions.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.Abstractions.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Resolves <typeparamref name="TDb"/> from DI and extracts its connection as a <see cref="SqlConnection"/>.
	/// Eliminates the bridging ceremony:
	/// <c>sp =&gt; () =&gt; (SqlConnection)sp.GetRequiredService&lt;TDb&gt;().Connection</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaStore<TDb>(
		this IServiceCollection services,
		Action<SqlServerSagaStoreOptions>? configure = null)
		where TDb : class, Excalibur.Data.Abstractions.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddSqlServerSagaStore(
			sp => () => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
			configure);
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaStore(
		this IDispatchBuilder builder,
		Action<SqlServerSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerSagaStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaStore(connectionFactoryProvider, configure);

		return builder;
	}

	#region Saga Timeout Store Extensions

	/// <summary>
	/// Adds SQL Server saga timeout store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure saga timeout store options (connection string, schema, table names).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="SqlServerSagaTimeoutStore"/> as both its concrete type
	/// and as <see cref="ISagaTimeoutStore"/> for dependency injection.
	/// </para>
	/// <para>
	/// Ensure the SagaTimeouts table has been created using the schema script at:
	/// <c>Scripts/SagaTimeouts.sql</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaTimeoutStore(
		this IServiceCollection services,
		Action<SqlServerSagaTimeoutStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterSagaTimeoutStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaTimeoutStore>>();
			return new SqlServerSagaTimeoutStore(options.Value.ConnectionString!, options, logger);
		});
		services.TryAddSingleton<ISagaTimeoutStore>(sp => sp.GetRequiredService<SqlServerSagaTimeoutStore>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server saga timeout store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Optional action to configure saga timeout store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerSagaTimeoutStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaTimeoutStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaTimeoutStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaTimeoutStore>>();
			return new SqlServerSagaTimeoutStore(connectionFactory, options, logger);
		});
		services.TryAddSingleton<ISagaTimeoutStore>(sp => sp.GetRequiredService<SqlServerSagaTimeoutStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga timeout store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure saga timeout store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaTimeoutStore(
		this IDispatchBuilder builder,
		Action<SqlServerSagaTimeoutStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerSagaTimeoutStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga timeout store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Optional action to configure saga timeout store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaTimeoutStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaTimeoutStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaTimeoutStore(connectionFactoryProvider, configure);

		return builder;
	}

	#endregion

	#region Saga Monitoring Service Extensions

	/// <summary>
	/// Adds SQL Server saga monitoring service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure saga store options (connection string, schema, table names).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="SqlServerSagaMonitoringService"/> as both its concrete type
	/// and as <see cref="ISagaMonitoringService"/> for dependency injection.
	/// </para>
	/// <para>
	/// Ensure the monitoring columns have been added using the schema script at:
	/// <c>Scripts/02-SagaMonitoringSchema.sql</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaMonitoringService(
		this IServiceCollection services,
		Action<SqlServerSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterSagaStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaMonitoringService>>();
			return new SqlServerSagaMonitoringService(options.Value.ConnectionString!, options, logger);
		});
		services.TryAddSingleton<ISagaMonitoringService>(sp => sp.GetRequiredService<SqlServerSagaMonitoringService>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server saga monitoring service to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerSagaMonitoringService(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaStoreOptions(services, configure);

		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaMonitoringService>>();
			return new SqlServerSagaMonitoringService(connectionFactory, options, logger);
		});
		services.TryAddSingleton<ISagaMonitoringService>(sp => sp.GetRequiredService<SqlServerSagaMonitoringService>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga monitoring service.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaMonitoringService(
		this IDispatchBuilder builder,
		Action<SqlServerSagaStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerSagaMonitoringService(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga monitoring service with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaMonitoringService(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaMonitoringService(connectionFactoryProvider, configure);

		return builder;
	}

	#endregion

	private static void RegisterSagaStoreOptions(
		IServiceCollection services,
		Action<SqlServerSagaStoreOptions>? configure)
	{
		_ = services.AddOptions<SqlServerSagaStoreOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaStoreOptions>, SqlServerSagaStoreOptionsValidator>());
	}

	private static void RegisterSagaTimeoutStoreOptions(
		IServiceCollection services,
		Action<SqlServerSagaTimeoutStoreOptions>? configure)
	{
		_ = services.AddOptions<SqlServerSagaTimeoutStoreOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaTimeoutStoreOptions>, SqlServerSagaTimeoutStoreOptionsValidator>());
	}
}
