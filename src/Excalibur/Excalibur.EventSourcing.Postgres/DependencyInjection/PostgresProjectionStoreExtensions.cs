// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres projection store services.
/// </summary>
public static class PostgresProjectionStoreExtensions
{
	/// <summary>
	/// Adds the Postgres projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<PostgresProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.Configure(configureOptions);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresProjectionStore<TProjection>>>();

			options.Value.Validate();

			return new PostgresProjectionStore<TProjection>(
				options.Value.ConnectionString,
				logger,
				options.Value.TableName,
				options.Value.JsonSerializerOptions);
		});

		return services;
	}

	/// <summary>
	/// Adds the Postgres projection store to the service collection with a connection string.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresProjectionStore<TProjection>(
		this IServiceCollection services,
		string connectionString,
		Action<PostgresProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresProjectionStore<TProjection>(options =>
		{
			options.ConnectionString = connectionString;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the Postgres projection store to the service collection with an NpgsqlDataSource factory.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSourceFactory">A factory function that creates an <see cref="NpgsqlDataSource"/>.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload when you want to provide a pre-configured <see cref="NpgsqlDataSource"/>
	/// with custom connection pooling or multi-host support.
	/// </remarks>
	public static IServiceCollection AddPostgresProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory,
		Action<PostgresProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSourceFactory);

		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var dataSource = dataSourceFactory(sp);
			var logger = sp.GetRequiredService<ILogger<PostgresProjectionStore<TProjection>>>();
			var optionsAccessor = sp.GetService<IOptions<PostgresProjectionStoreOptions>>();
			var options = optionsAccessor?.Value;

			return new PostgresProjectionStore<TProjection>(
				dataSource,
				logger,
				options?.TableName,
				options?.JsonSerializerOptions);
		});

		return services;
	}
}
