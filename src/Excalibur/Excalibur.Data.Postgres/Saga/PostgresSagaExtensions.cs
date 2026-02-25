// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Postgres.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres saga store.
/// </summary>
public static class PostgresSagaExtensions
{
	/// <summary>
	/// Adds Postgres saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="PostgresSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses Postgres's JSONB column type for efficient saga state serialization.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresSagaStore(options =>
	/// {
	///     options.ConnectionString = "Host=localhost;Database=myapp;";
	///     options.Schema = "dispatch";
	///     options.TableName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresSagaStore(
		this IServiceCollection services,
		Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresSagaOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<PostgresSagaStore>();
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<PostgresSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds Postgres saga store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="schema">The schema name. Defaults to "dispatch".</param>
	/// <param name="tableName">The table name. Defaults to "sagas".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Simplified registration for common scenarios where only the connection string and
	/// optional schema/table names are needed.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresSagaStore("Host=localhost;Database=myapp;");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresSagaStore(
		this IServiceCollection services,
		string connectionString,
		string schema = "dispatch",
		string tableName = "sagas")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresSagaStore(options =>
		{
			options.ConnectionString = connectionString;
			options.Schema = schema;
			options.TableName = tableName;
		});
	}

	/// <summary>
	/// Adds Postgres saga store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactoryProvider">
	/// A factory function that creates <see cref="NpgsqlConnection"/> instances from the service provider.
	/// Useful for multi-database setups, custom connection pooling, or IDb integration.
	/// </param>
	/// <param name="configure">Action to configure the options (used for table names, timeouts, etc.).</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload supports advanced scenarios where connection management is controlled externally.
	/// </para>
	/// <para>
	/// Example with IDb:
	/// <code>
	/// services.AddPostgresSagaStore(
	///     sp => () => (NpgsqlConnection)sp.GetRequiredService&lt;ISagaDb&gt;().Connection,
	///     options => options.Schema = "sagas");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactoryProvider,
		Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresSagaOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(sp =>
		{
			var connectionFactory = connectionFactoryProvider(sp);
			var options = sp.GetRequiredService<IOptions<PostgresSagaOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<PostgresSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new PostgresSagaStore(connectionFactory, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<PostgresSagaStore>());

		return services;
	}
}
