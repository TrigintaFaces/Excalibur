// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Postgres.EventSourcing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres event store services.
/// </summary>
public static class PostgresEventStoreExtensions
{
	/// <summary>
	/// Adds the Postgres event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="configureOptions">Optional action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		string connectionString,
		Action<PostgresEventStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Configure options
		var builder = services.AddOptions<PostgresEventStoreOptions>();
		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register event store with connection string
		services.TryAddScoped<IEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresEventStore>>();
			var internalSerializer = sp.GetService<IInternalSerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new PostgresEventStore(
				connectionString,
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}

	/// <summary>
	/// Adds the Postgres event store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory function that creates Postgres connections.</param>
	/// <param name="configureOptions">Optional action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like multi-database setups,
	/// custom connection pooling, or integration with IDb abstraction.
	/// </remarks>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		Func<IServiceProvider, NpgsqlConnection> connectionFactory,
		Action<PostgresEventStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Configure options
		var builder = services.AddOptions<PostgresEventStoreOptions>();
		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register event store with connection factory
		services.TryAddScoped<IEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresEventStore>>();
			var internalSerializer = sp.GetService<IInternalSerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new PostgresEventStore(
				() => connectionFactory(sp),
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}
}
