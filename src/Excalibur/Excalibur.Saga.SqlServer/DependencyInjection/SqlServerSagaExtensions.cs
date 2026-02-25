// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
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
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerSagaStore(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		RegisterSagaStoreOptions(services, configureOptions);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new SqlServerSagaStore(connectionString, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<SqlServerSagaStore>());

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
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
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
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaStoreOptions(services, configureOptions);

		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new SqlServerSagaStore(connectionFactory, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<SqlServerSagaStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaStore(
		this IDispatchBuilder builder,
		string connectionString,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerSagaStore(connectionString, configureOptions);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaStore(connectionFactoryProvider, configureOptions);

		return builder;
	}

	#region Saga Timeout Store Extensions

	/// <summary>
	/// Adds SQL Server saga timeout store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga timeout store options.</param>
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
		string connectionString,
		Action<SqlServerSagaTimeoutStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		RegisterSagaTimeoutStoreOptions(services, configureOptions);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaTimeoutStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaTimeoutStore>>();
			return new SqlServerSagaTimeoutStore(connectionString, options, logger);
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
	/// <param name="configureOptions">Optional action to configure saga timeout store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddSqlServerSagaTimeoutStore(sp => () => (SqlConnection)sp.GetRequiredService&lt;IDomainDb&gt;().Connection);
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaTimeoutStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaTimeoutStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaTimeoutStoreOptions(services, configureOptions);

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
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga timeout store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaTimeoutStore(
		this IDispatchBuilder builder,
		string connectionString,
		Action<SqlServerSagaTimeoutStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerSagaTimeoutStore(connectionString, configureOptions);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga timeout store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configureOptions">Optional action to configure saga timeout store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaTimeoutStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaTimeoutStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaTimeoutStore(connectionFactoryProvider, configureOptions);

		return builder;
	}

	#endregion

	#region Saga Monitoring Service Extensions

	/// <summary>
	/// Adds SQL Server saga monitoring service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="SqlServerSagaMonitoringService"/> as both its concrete type
	/// and as <see cref="ISagaMonitoringService"/> for dependency injection.
	/// </para>
	/// <para>
	/// Ensure the monitoring columns have been added using the schema script at:
	/// <c>Schema/02-SagaMonitoringSchema.sql</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaMonitoringService(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		RegisterSagaStoreOptions(services, configureOptions);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerSagaMonitoringService>>();
			return new SqlServerSagaMonitoringService(connectionString, options, logger);
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
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddSqlServerSagaMonitoringService(sp => () => (SqlConnection)sp.GetRequiredService&lt;IDomainDb&gt;().Connection);
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSagaMonitoringService(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		RegisterSagaStoreOptions(services, configureOptions);

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
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaMonitoringService(
		this IDispatchBuilder builder,
		string connectionString,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerSagaMonitoringService(connectionString, configureOptions);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server saga monitoring service with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configureOptions">Optional action to configure saga store options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerSagaMonitoringService(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerSagaStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);

		_ = builder.Services.AddSqlServerSagaMonitoringService(connectionFactoryProvider, configureOptions);

		return builder;
	}

	#endregion

	private static void RegisterSagaStoreOptions(
		IServiceCollection services,
		Action<SqlServerSagaStoreOptions>? configureOptions)
	{
		_ = services.AddOptions<SqlServerSagaStoreOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaStoreOptions>, SqlServerSagaStoreOptionsValidator>());
	}

	private static void RegisterSagaTimeoutStoreOptions(
		IServiceCollection services,
		Action<SqlServerSagaTimeoutStoreOptions>? configureOptions)
	{
		_ = services.AddOptions<SqlServerSagaTimeoutStoreOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaTimeoutStoreOptions>, SqlServerSagaTimeoutStoreOptionsValidator>());
	}
}
