// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server inbox store.
/// </summary>
public static class SqlServerInboxExtensions
{
	/// <summary>
	/// Adds SQL Server inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerInboxStore(
		this IServiceCollection services,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerInboxStore>();
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<SqlServerInboxStore>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server inbox store to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerInboxStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerInboxStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Adds SQL Server inbox store to the service collection with a connection factory.
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
	/// services.AddSqlServerInboxStore(
	///     sp => () => (SqlConnection)sp.GetRequiredService&lt;IInboxDb&gt;().Connection,
	///     options => options.SchemaName = "messaging");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<SqlServerInboxStore>>();
			return new SqlServerInboxStore(connectionFactory, options, logger);
		});
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<SqlServerInboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerInboxStore(
		this IDispatchBuilder builder,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server inbox store with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerInboxStore(
		this IDispatchBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return builder.UseSqlServerInboxStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Configures the dispatch builder to use SQL Server inbox store with a connection factory.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="SqlConnection"/> instances from the service provider.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseSqlServerInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerInboxStore(connectionFactoryProvider, configure);

		return builder;
	}
}
