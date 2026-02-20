// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server outbox store.
/// </summary>
public static class SqlServerOutboxExtensions
{
	/// <summary>
	/// Adds SQL Server outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerOutboxStore>();
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server outbox store to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerOutboxStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Adds SQL Server outbox store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddSqlServerOutboxStore(
	///     sp => () => (SqlConnection)sp.GetRequiredService&lt;IOutboxDb&gt;().Connection,
	///     options => options.SchemaName = "messaging");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
			var payloadSerializer = sp.GetService<IPayloadSerializer>();
			var logger = sp.GetRequiredService<ILogger<SqlServerOutboxStore>>();
			return new SqlServerOutboxStore(connectionFactory, options, payloadSerializer, logger);
		});
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerOutboxStore(
		this IDispatchBuilder builder,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server outbox store with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerOutboxStore(
		this IDispatchBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return builder.UseSqlServerOutboxStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server outbox store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerOutboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerOutboxStore(connectionFactoryProvider, configure);

		return builder;
	}

	/// <summary>
	/// Adds SQL Server dead letter queue to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterQueue(
		this IServiceCollection services,
		Action<SqlServerDeadLetterQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerDeadLetterQueue>();
		services.TryAddSingleton<IDeadLetterQueue>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server dead letter queue to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterQueue(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerDeadLetterQueue(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server dead letter queue.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerDeadLetterQueue(
		this IDispatchBuilder builder,
		Action<SqlServerDeadLetterQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerDeadLetterQueue(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server dead letter queue with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerDeadLetterQueue(
		this IDispatchBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return builder.UseSqlServerDeadLetterQueue(options => options.ConnectionString = connectionString);
	}
}
