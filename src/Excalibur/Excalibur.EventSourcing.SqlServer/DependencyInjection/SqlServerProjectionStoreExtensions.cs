// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server projection store services.
/// </summary>
public static class SqlServerProjectionStoreExtensions
{
	/// <summary>
	/// Adds the SQL Server projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<SqlServerProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.Configure(configureOptions);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<SqlServerProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerProjectionStore<TProjection>>>();

			options.Value.Validate();

			return new SqlServerProjectionStore<TProjection>(
				options.Value.ConnectionString,
				logger,
				options.Value.TableName,
				options.Value.JsonSerializerOptions);
		});

		return services;
	}

	/// <summary>
	/// Adds the SQL Server projection store to the service collection with a connection string.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection>(
		this IServiceCollection services,
		string connectionString,
		Action<SqlServerProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerProjectionStore<TProjection>(options =>
		{
			options.ConnectionString = connectionString;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the SQL Server projection store to the service collection with a connection factory.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">A factory function that creates <see cref="SqlConnection"/> instances.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload when you want to provide custom connection management,
	/// such as integrating with an existing connection pool or IDb abstraction.
	/// </remarks>
	public static IServiceCollection AddSqlServerProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		Action<SqlServerProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<SqlServerProjectionStore<TProjection>>>();
			var optionsAccessor = sp.GetService<IOptions<SqlServerProjectionStoreOptions>>();
			var options = optionsAccessor?.Value;

			return new SqlServerProjectionStore<TProjection>(
				connectionFactory,
				logger,
				options?.TableName,
				options?.JsonSerializerOptions);
		});

		return services;
	}
}
